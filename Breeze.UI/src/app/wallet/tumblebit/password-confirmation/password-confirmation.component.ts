import { Component, OnInit, Input, AfterViewInit } from '@angular/core';
import { FormGroup, FormControl, Validators, FormBuilder } from '@angular/forms';

import { NgbModal, NgbActiveModal } from '@ng-bootstrap/ng-bootstrap';

import { TumblebitService } from '../tumblebit.service';
import { TumbleRequest } from '../classes/tumble-request';
import { Error } from '../../../shared/classes/error';

import { ModalService } from '../../../shared/services/modal.service';

const { shell } = require('electron');

@Component({
  selector: 'app-password-confirmation',
  templateUrl: './password-confirmation.component.html',
  styleUrls: ['./password-confirmation.component.css']
})
export class PasswordConfirmationComponent {

  @Input() sourceWalletName: string;
  @Input() destinationWalletName: string;
  @Input() denomination: number;
  @Input() fee: number;
  @Input() balance: number;
  @Input() coinUnit: string;

  public startingTumble: Boolean = false;
  private walletPasswordForm: FormGroup;
  public showLegalText = false;
  private termsConditionsRead = false;
  private readonly LegalTextUrl = 'https://github.com/BreezeHub/BreezeProject/blob/master/Licenses/breeze-license.txt';

  formErrors = {
    'walletPassword': ''
  };

  validationMessages = {
    'walletPassword': {
      'required': 'The wallet password is required.'
    }
  };

  constructor(
    private tumblebitService: TumblebitService,
    public activeModal: NgbActiveModal,
    private fb: FormBuilder,
    private genericModalService: ModalService) {
    this.buildWalletPasswordForm();
    this.setWalletPasswordEnabledState();
  }

  public onTermsConditionsRead() {
    if (this.termsConditionsRead) {
      this.termsConditionsRead = false;
    } else {
      this.termsConditionsRead = true;
    }

    this.walletPasswordForm.get('walletPassword').setValue('');

    this.setWalletPasswordEnabledState();
  }

  private setWalletPasswordEnabledState() {
    var control = this.walletPasswordForm.get('walletPassword');
    if (this.termsConditionsRead) {
      control.enable();
    } else {
      control.disable();
    }
  }

  public onLegalTextClicked() {
    shell.openExternal(this.LegalTextUrl);
  }

  private buildWalletPasswordForm(): void {
    this.walletPasswordForm = this.fb.group({
      'walletPassword': ['', Validators.required]
    });

    this.walletPasswordForm.valueChanges
      .subscribe(data => this.onValueChanged(data));

    this.onValueChanged();
  }

  onValueChanged(data?: any) {
    if (!this.walletPasswordForm) { return; }
    const form = this.walletPasswordForm;

    for (const field in this.formErrors) {
      if (!this.formErrors.hasOwnProperty(field)) { continue; }
      this.formErrors[field] = '';
      const control = form.get(field);
      if (control && control.dirty && !control.valid) {
        const messages = this.validationMessages[field];
        for (const key in control.errors) {
          if (control.errors.hasOwnProperty(key)) {
            this.formErrors[field] += messages[key] + ' ';
          }
        }
      }
    }
  }

  private onConfirm() {
    this.startingTumble = true;

    const tumbleRequest = new TumbleRequest(
      this.sourceWalletName,
      this.destinationWalletName,
      this.walletPasswordForm.get('walletPassword').value
    )

    this.tumblebitService
      .startTumbling(tumbleRequest)
      .subscribe(
        response => {
          if (response.status >= 200 && response.status < 400) {
            this.activeModal.close();
          }
        },
        error => {
          console.error(error);
          if (error.status === 0) {
            this.startingTumble = false;
            this.genericModalService.openModal(
                Error.toDialogOptions('Failed to start tumbling. Reason: API is not responding or timing out.', null));
          } else if (error.status >= 400) {
            if (!error.json().errors[0]) {
              console.error(error);
              this.startingTumble = false;
            } else {
              this.startingTumble = false;
              this.genericModalService.openModal(Error.toDialogOptions(error, null));
            }
          }
        },
      )
    ;
  }
}
