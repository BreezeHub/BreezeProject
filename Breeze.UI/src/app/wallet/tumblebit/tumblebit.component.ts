import { Component, OnInit, OnDestroy } from '@angular/core';
import { NgbModal, NgbActiveModal, NgbDropdown } from '@ng-bootstrap/ng-bootstrap';
import { FormGroup, FormControl, Validators, FormBuilder } from '@angular/forms';

import { PasswordConfirmationComponent } from './password-confirmation/password-confirmation.component';
import { ApiService } from '../../shared/services/api.service';
import { GlobalService } from '../../shared/services/global.service';
import { WalletInfo } from '../../shared/classes/wallet-info';
import { TumblebitService } from './tumblebit.service';
import { TumblerConnectionRequest } from './classes/tumbler-connection-request';
import { TumbleRequest } from './classes/tumble-request';

import { Observable } from 'rxjs/Rx';
import { Subscription } from 'rxjs/Subscription';

@Component({
  selector: 'tumblebit-component',
  providers: [TumblebitService],
  templateUrl: './tumblebit.component.html',
  styleUrls: ['./tumblebit.component.css'],
})

export class TumblebitComponent implements OnInit {
  constructor(private apiService: ApiService, private tumblebitService: TumblebitService, private globalService: GlobalService, private modalService: NgbModal, private fb: FormBuilder) {
    this.buildTumbleForm();
  }
  private confirmedBalance: number;
  private unconfirmedBalance: number;
  private totalBalance: number;
  private walletBalanceSubscription: Subscription;
  private destinationWalletName: string;
  private destinationConfirmedBalance: number;
  private destinationUnconfirmedBalance: number;
  private destinationTotalBalance: number;
  private destinationWalletBalanceSubscription: Subscription;
  private isConnected: Boolean = false;
  private tumblerAddressCopied: boolean = false;
  private tumblerParameters: any;
  private estimate: number;
  private fee: number;
  private denomination: number;
  private tumbleStatus: any;
  private tumbleStateSubscription: Subscription;
  private tumbleForm: FormGroup;
  private tumbling: Boolean = false;
  private connectForm: FormGroup;
  private wallets: [string];
  private tumblerAddress: string = "Connecting...";

  ngOnInit() {
    this.connectToTumbler();
    this.checkTumblingStatus();
    this.getWalletFiles();
    this.getWalletBalance();
  };

  ngOnDestroy() {
    if (this.walletBalanceSubscription){
      this.walletBalanceSubscription.unsubscribe();
    }

    if (this.destinationWalletBalanceSubscription) {
      this.destinationWalletBalanceSubscription.unsubscribe();
    }

    if (this.tumbleStateSubscription) {
      this.tumbleStateSubscription.unsubscribe();
    }
  };

  private buildTumbleForm(): void {
    this.tumbleForm = this.fb.group({
      'selectWallet': ['', Validators.required]
    });

    this.tumbleForm.valueChanges
      .subscribe(data => this.onValueChanged(this.tumbleForm, this.tumbleFormErrors, data));

    this.onValueChanged(this.tumbleForm, this.tumbleFormErrors);
  }

  // TODO: abstract to a shared utility lib
  onValueChanged(originalForm: FormGroup, formErrors: object, data?: any) {
    this.destinationWalletName = this.tumbleForm.get("selectWallet").value;

    if (this.destinationWalletName) {
      this.getDestinationWalletBalance();
    }

    if (!originalForm) { return; }
    const form = originalForm;
    for (const field in formErrors) {
      formErrors[field] = '';
      const control = form.get(field);
      if (control && control.dirty && !control.valid) {
        const messages = this.validationMessages[field];
        for (const key in control.errors) {
          formErrors[field] += messages[key] + ' ';
        }
      }
    }
  }
  tumbleFormErrors = {
    'selectWallet': ''
  }

  validationMessages = {
    'selectWallet': {
      'required': 'A destination address is required.',
    }
  }

  private checkTumblingStatus() {
    this.tumbleStateSubscription = this.tumblebitService.getTumblingState()
      .subscribe(
        response => {
          if (response.status >= 200 && response.status < 400) {
            if (response.json().state === "OnlyMonitor") {
              this.tumbling = false;
            } else if (response.json().state === "Tumbling") {
              this.tumbling = true;
              this.destinationWalletName = response.json().destinationWallet;
              this.getDestinationWalletBalance();
            }
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
        }
      )
    ;
  }

  private connectToTumbler() {
    let connection = new TumblerConnectionRequest(
      this.tumblerAddress,
      this.globalService.getNetwork()
    );

    this.tumblebitService
      .connectToTumbler()
      .subscribe(
        // TODO abstract into shared utility method
        response => {
          if (response.status >= 200 && response.status < 400) {
            this.tumblerParameters = response.json();
            this.tumblerAddress = this.tumblerParameters.tumbler
            this.estimate = this.tumblerParameters.estimate / 3600;
            this.fee = this.tumblerParameters.fee * 100;
            this.denomination = this.tumblerParameters.denomination;
            this.isConnected = true;
          }
        },
        error => {
          console.error(error);
          this.isConnected = false;
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
        }
      )
    ;
  }

  private tumbleClicked() {
    this.checkTumblingStatus();
    if (this.tumbling) {
      this.stopTumbling();
    } else {
      this.startTumbling();
    }
  }

  private startTumbling() {
    if (!this.isConnected) {
      alert("Can't start tumbling when you're not connected to a server. Please try again later.")
    } else {
      const modalRef = this.modalService.open(PasswordConfirmationComponent);
      modalRef.componentInstance.sourceWalletName = this.globalService.getWalletName();
      modalRef.componentInstance.destinationWalletName = this.destinationWalletName;
    }
  }

  private stopTumbling() {
    this.tumblebitService.stopTumbling()
      .subscribe(
        response => {
          if (response.status >= 200 && response.status < 400) {
            this.tumbling = false;
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
        }
      )
    ;
  }

  // TODO: move into a shared service
  private getWalletBalance() {
    let walletInfo = new WalletInfo(this.globalService.getWalletName(), this.globalService.getCoinType())
    this.walletBalanceSubscription = this.apiService.getWalletBalance(walletInfo)
      .subscribe(
        response =>  {
          if (response.status >= 200 && response.status < 400) {
              let balanceResponse = response.json();
              this.confirmedBalance = balanceResponse.balances[0].amountConfirmed;
              this.unconfirmedBalance = balanceResponse.balances[0].amountUnconfirmed;
              this.totalBalance = this.confirmedBalance + this.unconfirmedBalance;
          }
        },
        error => {
          console.log(error);
          if (error.status === 0) {
            alert('Something went wrong while connecting to the API. Make sure your address is correct and try again.');
          } else if (error.status >= 400) {
            if (!error.json().errors[0]) {
              console.log(error);
            }
            else {
              alert(error.json().errors[0].description);
            }
          }
        }
      )
    ;
  };

  private getDestinationWalletBalance() {
    if (this.destinationWalletBalanceSubscription) {
      this.destinationWalletBalanceSubscription.unsubscribe();
    }
    this.destinationWalletBalanceSubscription = this.tumblebitService.getWalletDestinationBalance(this.destinationWalletName)
      .subscribe(
        response =>  {
          if (response.status >= 200 && response.status < 400) {
            let balanceResponse = response.json();
            this.destinationConfirmedBalance = balanceResponse.balances[0].amountConfirmed;
            this.destinationUnconfirmedBalance = balanceResponse.balances[0].amountUnconfirmed;
            this.destinationTotalBalance = this.destinationConfirmedBalance + this.destinationUnconfirmedBalance;
          }
        },
        error => {
          console.log(error);
          if (error.status === 0) {
            alert('Something went wrong while connecting to the API. Make sure your address is correct and try again.');
          } else if (error.status >= 400) {
            if (!error.json().errors[0]) {
              console.log(error);
            }
            else {
              alert(error.json().errors[0].description);
            }
          }
        }
      )
    ;
  };

  private getWalletFiles() {
    this.apiService.getWalletFiles()
      .subscribe(
        response => {
          if (response.status >= 200 && response.status < 400) {
            let responseMessage = response.json();
            this.wallets = responseMessage.walletsFiles;
            if (this.wallets.length > 0) {
              for (let wallet in this.wallets) {
                this.wallets[wallet] = this.wallets[wallet].slice(0, -12);
              }
              //this.updateWalletFileDisplay(this.wallets[0]);
            } else {
            }
          }
        },
        error => {
          if (error.status === 0) {
            alert("Something went wrong while connecting to the API. Please restart the application.");
          } else if (error.status >= 400) {
            if (!error.json().errors[0]) {
              console.log(error);
            }
            else {
              alert(error.json().errors[0].message);
            }
          }
        },
        () => {
          // this.destinationWalletName = this.tumbleForm.get("selectWallet").value;
          // this.getDestinationWalletBalance()
        }
      )
    ;
  }

  private updateWalletFileDisplay(walletName: string) {
    this.tumbleForm.patchValue({selectWallet: walletName})
  }
}
