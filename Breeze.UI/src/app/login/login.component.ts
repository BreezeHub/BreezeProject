import { ApiService } from '../shared/services/api.service';
import { Component, OnInit } from '@angular/core';
import { Error } from '../shared/classes/error';
import { FormGroup, Validators, FormBuilder } from '@angular/forms';
import { formValidator } from '../shared/helpers/form-validation-helper';
import { GlobalService } from '../shared/services/global.service';
import { ModalService } from '../shared/services/modal.service';
import { Router } from '@angular/router';
import { WalletInfo } from '../shared/classes/wallet-info';
import { WalletLoad } from '../shared/classes/wallet-load';

@Component({
  selector: 'app-login',
  templateUrl: './login.component.html',
  styleUrls: ['./login.component.css']
})

export class LoginComponent implements OnInit {
  public hasWallet = false;
  public isDecrypting = false;
  private openWalletForm: FormGroup;
  private wallets: [string];
  formErrors = {
    'password': ''
  };

  validationMessages = {
    'password': {
      'required': 'Please enter your password.'
    }
  };

  constructor(private globalService: GlobalService,
    private apiService: ApiService,
    private genericModalService: ModalService,
    private router: Router,
    private fb: FormBuilder) {
    this.buildDecryptForm();
  }

  ngOnInit() {
    this.getWalletFiles();
  }

  private buildDecryptForm(): void {
    this.openWalletForm = this.fb.group({
      'selectWallet': ['', Validators.required],
      'password': ['', Validators.required]
    });
    this.openWalletForm.valueChanges.subscribe(data => formValidator(this.openWalletForm, this.formErrors, this.validationMessages));
    formValidator(this.openWalletForm, this.formErrors, this.validationMessages)
  }

  private getWalletFiles() {
    this.apiService.getWalletFiles()
      .subscribe(
        response => {
          this.wallets = response.walletsFiles;
          this.globalService.setWalletPath(response.walletsPath);
          if (this.wallets.length > 0) {
            this.hasWallet = true;
            for (let wallet in this.wallets) {
              this.wallets[wallet] = this.wallets[wallet].slice(0, -12);
            }
          } else {
            this.hasWallet = false;
          }
        }
      )
    ;
  }

  private onCreateClicked() {
    this.router.navigate(['/setup']);
  }

  private onEnter() {
    if (this.openWalletForm.valid) {
      this.onDecryptClicked();
    }
  }

  private onDecryptClicked() {
    this.isDecrypting = true;
    this.globalService.setWalletName(this.openWalletForm.get('selectWallet').value);
    this.getCurrentNetwork();
    const walletLoad = new WalletLoad(
      this.openWalletForm.get('selectWallet').value,
      this.openWalletForm.get('password').value
    );
    this.loadWallets(walletLoad);
  }

  private loadWallets(walletLoad: WalletLoad) {
    this.loadBitcoinWallet(walletLoad);
  }

  private loadBitcoinWallet(walletLoad: WalletLoad) {
    this.apiService.loadBitcoinWallet(walletLoad)
      .subscribe(
        response => {
          this.loadStratisWallet(walletLoad);
        },
        error => {
          this.isDecrypting = false;
        }
      )
    ;
  }

  private loadStratisWallet(walletLoad: WalletLoad) {
    this.apiService.loadStratisWallet(walletLoad)
      .subscribe(
        response => {
          this.router.navigate(['wallet/dashboard']);
        },
        error => {
          this.isDecrypting = false;
        }
      )
    ;
  }

  private getCurrentNetwork() {
    this.apiService.getNodeStatus()
      .subscribe(
        response => {
          let responseMessage = response;
          this.globalService.setCoinUnit(responseMessage.coinTicker);
          this.globalService.setNetwork(responseMessage.network);
        }
      )
    ;
  }
}
