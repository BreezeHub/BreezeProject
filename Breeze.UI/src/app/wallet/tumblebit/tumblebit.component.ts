import { Component, OnInit, OnDestroy } from '@angular/core';
import { NgbModal, NgbActiveModal, NgbDropdown } from '@ng-bootstrap/ng-bootstrap';
import { FormGroup, FormControl, Validators, FormBuilder } from '@angular/forms';

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
  private watchWalletName: string;
  private watchConfirmedBalance: number;
  private watchUnconfirmedBalance: number;
  private watchTotalBalance: number;
  private walletBalanceSubscription: Subscription;
  private watchWalletBalanceSubscription: Subscription;
  private isConnected: Boolean = false;
  private tumblerParameters: any;
  private tumbleStatus: any;
  private tumbleForm: FormGroup;
  private tumbling: Boolean = false;
  private connectForm: FormGroup;
  private wallets: [string];
  private tumblerAddress: string = "ctb://7obtcd7mkosmxeuh.onion/?h=03c632023c4a8587845ad918b8e5f53f7bf18319";

  ngOnInit() {
    this.getWalletFiles();
    this.getWalletBalance();
    this.getWatchWalletBalance();
    this.checkTumblingStatus();
    this.connect();
  };

  ngOnDestroy() {
    this.walletBalanceSubscription.unsubscribe();
    this.watchWalletBalanceSubscription.unsubscribe();
  };

  private buildTumbleForm(): void {
    this.tumbleForm = this.fb.group({
      'selectWallet': ['', Validators.required],
      'walletPassword': ['', Validators.required]
    });

    this.tumbleForm.valueChanges
      .subscribe(data => this.onValueChanged(this.tumbleForm, this.tumbleFormErrors, data));

    this.onValueChanged(this.tumbleForm, this.tumbleFormErrors);
  }

  // TODO: abstract to a shared utility lib
  onValueChanged(originalForm: FormGroup, formErrors: object, data?: any) {
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
    'selectWallet': '',
    'walletPassword': ''
  }

  validationMessages = {
    'selectWallet': {
      'required': 'A destination address is required.',
    },
    'walletPassword': {
      'required': 'The source wallet password is required.',
    }
  }

  private checkTumblingStatus() {
    //TODO: check if tumbling is already enabled.
    this.tumbling = true;
  }

  private connect() {
    let connection = new TumblerConnectionRequest(
      this.tumblerAddress,
      this.globalService.getNetwork()
    );

    this.tumblebitService
      .connect(connection)
      .subscribe(
        // TODO abstract into shared utility method
        response => {
          if (response.status >= 200 && response.status < 400) {
            this.tumblerParameters = response.json();
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
        },
      )
  }

  private tumbleClicked() {
    if (this.tumbling) {
      this.stopTumble();
    } else {
      this.startTumbling();
    }
  }

  private startTumbling() {
    if (!this.isConnected) {
      alert("Can't start tumbling when you're not connected to a server. Please try again later.")
    } else {

      let tumbleRequest = new TumbleRequest(
        this.globalService.getWalletName(),
        this.tumbleForm.get('selectWallet').value,
        this.tumbleForm.get('walletPassword').value
      )

      this.tumblebitService
        .tumble(tumbleRequest)
        .subscribe(
          response => {
            if (response.status >= 200 && response.status < 400) {
              this.tumbleStatus = response.json();
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

  private stopTumble() {
    //TODO: attach API stop method
    console.log('stopping tumble...');
    this.tumbling = false;
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

  private getWatchWalletBalance() {
    //TODO: attach to its own API function
    let walletInfo = new WalletInfo(this.globalService.getWalletName(), this.globalService.getCoinType())
    this.watchWalletBalanceSubscription = this.apiService.getWalletBalance(walletInfo)
      .subscribe(
        response =>  {
          if (response.status >= 200 && response.status < 400) {
              let balanceResponse = response.json();
              this.watchConfirmedBalance = balanceResponse.balances[0].amountConfirmed;
              this.watchUnconfirmedBalance = balanceResponse.balances[0].amountUnconfirmed;
              this.watchTotalBalance = this.watchConfirmedBalance + this.watchUnconfirmedBalance;
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
            this.globalService.setWalletPath(responseMessage.walletsPath);
            if (this.wallets.length > 0) {
              for (let wallet in this.wallets) {
                this.wallets[wallet] = this.wallets[wallet].slice(0, -12);
              }
              this.updateWalletFileDisplay(this.wallets[0]);
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
        }
      )
    ;
  }

  private updateWalletFileDisplay(walletName: string) {
    this.tumbleForm.patchValue({selectWallet: walletName})
  }
}
