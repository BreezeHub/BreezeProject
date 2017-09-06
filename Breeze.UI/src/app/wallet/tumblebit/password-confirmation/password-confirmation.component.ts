import { Component, OnInit, Input } from '@angular/core';
import { FormGroup, FormControl, Validators, FormBuilder } from '@angular/forms';

import { NgbModal, NgbActiveModal } from '@ng-bootstrap/ng-bootstrap';

import { TumblebitService } from '../tumblebit.service';
import { TumbleRequest } from '../classes/tumble-request';

@Component({
  selector: 'app-password-confirmation',
  templateUrl: './password-confirmation.component.html',
  styleUrls: ['./password-confirmation.component.css']
})
export class PasswordConfirmationComponent implements OnInit {

  @Input() sourceWalletName: string;
  @Input() destinationWalletName: string;
  constructor(private tumblebitService: TumblebitService, public activeModal: NgbActiveModal, private fb: FormBuilder) {
    this.buildWalletPasswordForm();
  }

  private walletPasswordForm: FormGroup;

  ngOnInit() {
  }

  private buildWalletPasswordForm(): void {
    this.walletPasswordForm = this.fb.group({
      "walletPassword": ["", Validators.required]
    });

    this.walletPasswordForm.valueChanges
      .subscribe(data => this.onValueChanged(data));

    this.onValueChanged();
  }

  onValueChanged(data?: any) {
    if (!this.walletPasswordForm) { return; }
    const form = this.walletPasswordForm;
    for (const field in this.formErrors) {
      this.formErrors[field] = '';
      const control = form.get(field);
      if (control && control.dirty && !control.valid) {
        const messages = this.validationMessages[field];
        for (const key in control.errors) {
          this.formErrors[field] += messages[key] + ' ';
        }
      }
    }
  }

  formErrors = {
    'walletPassword': ''
  };

  validationMessages = {
    'walletPassword': {
      'required': 'The wallet password is required.'
    }
  };

  private onConfirm() {
    let tumbleRequest = new TumbleRequest(
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
            alert('Something went wrong while connecting to the TumbleBit Client. Please restart the application.');
          } else if (error.status >= 400) {
            if (!error.json().errors[0]) {
              console.error(error);
            }
            else {
              alert(error.json().errors[0].message);
            }
          }
        },
      )
    ;
  }
}
