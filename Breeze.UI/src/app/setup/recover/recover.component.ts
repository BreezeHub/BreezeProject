import { Component, OnInit } from '@angular/core';
import { FormGroup, FormControl, Validators, FormBuilder } from '@angular/forms';
import { Router } from '@angular/router';
import { BsDatepickerConfig } from 'ngx-bootstrap/datepicker';

import { ApiService } from '../../shared/services/api.service';
import { ModalService } from '../../shared/services/modal.service';

import { WalletRecovery } from '../../shared/classes/wallet-recovery';
import { Error } from '../../shared/classes/error';

@Component({
  selector: 'app-recover',
  templateUrl: './recover.component.html',
  styleUrls: ['./recover.component.css']
})
export class RecoverComponent implements OnInit {
  formErrors = {
    'walletName': '',
    'walletMnemonic': '',
    'walletDate': '',
    'walletPassword': '',
    'walletPassphrase': ''
  };

  validationMessages = {
    'walletName': {
      'required': 'A wallet name is required.',
      'minlength': 'A wallet name must be at least one character long.',
      'maxlength': 'A wallet name cannot be more than 24 characters long.',
      'pattern': 'Please enter a valid wallet name. [a-Z] and [0-9] are the only characters allowed.'
    },
    'walletMnemonic': {
      'required': 'Please enter your 12 word phrase.'
    },
    'walletDate': {
      'required': 'Please choose the date the wallet should sync from.'
    },
    'walletPassword': {
      'required': 'A password is required.'
    },

  };

  public recoverWalletForm: FormGroup;
  public creationDate: Date;
  public isRecovering = false;
  public maxDate = new Date();
  public bsConfig: Partial<BsDatepickerConfig>;
  private walletRecovery: WalletRecovery;

  constructor(
    private apiService: ApiService,
    private router: Router,
    private genericModalService: ModalService,
    private fb: FormBuilder) {
    this.buildRecoverForm();
  }

  ngOnInit() {
    this.bsConfig = Object.assign({}, {showWeekNumbers: false, containerClass: 'theme-dark-blue'});
  }

  private buildRecoverForm(): void {
    this.recoverWalletForm = this.fb.group({
      'walletName': ['', [
          Validators.required,
          Validators.minLength(1),
          Validators.maxLength(24),
          Validators.pattern(/^[a-zA-Z0-9]*$/)
        ]
      ],
      'walletMnemonic': ['', Validators.required],
      'walletDate': ['', Validators.required],
      'walletPassphrase': [''],
      'walletPassword': ['', Validators.required],
      'selectNetwork': ['test', Validators.required]
    });

    this.recoverWalletForm.valueChanges
      .subscribe(data => this.onValueChanged(data));

    this.onValueChanged();
  }

  onValueChanged(data?: any) {
    if (!this.recoverWalletForm) { return; }
    const form = this.recoverWalletForm;
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

  public onBackClicked() {
    this.router.navigate(['/setup']);
  }

  public onRecoverClicked() {
    this.isRecovering = true;

    const recoveryDate = new Date(this.recoverWalletForm.get('walletDate').value);
    recoveryDate.setDate(recoveryDate.getDate() - 1);

    this.walletRecovery = new WalletRecovery(
      this.recoverWalletForm.get('walletName').value,
      this.recoverWalletForm.get('walletMnemonic').value,
      this.recoverWalletForm.get('walletPassword').value,
      this.recoverWalletForm.get("walletPassphrase").value,
      recoveryDate
    );
    this.recoverWallets(this.walletRecovery);
  }

  private recoverWallets(recoverWallet: WalletRecovery) {
    this.apiService.recoverBitcoinWallet(recoverWallet)
      .subscribe(
        response => {
          this.recoverStratisWallet(recoverWallet);
        },
        error => {
          this.isRecovering = false;
        }
      )
    ;
  }

  private recoverStratisWallet(recoverWallet: WalletRecovery) {
    this.apiService.recoverStratisWallet(recoverWallet)
      .subscribe(
        response => {
          let body = "Your wallet has been recovered. \nYou will be redirected to the decryption page.";
          this.genericModalService.openModal("Wallet Recovered", body)
          this.router.navigate([''])
        },
        error => {
          this.isRecovering = false;
        }
      );
  }
}
