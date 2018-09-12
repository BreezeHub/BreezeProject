import { Router, NavigationEnd, RouterEvent } from '@angular/router';
import { Component, OnDestroy } from '@angular/core';
import { NgbModal, NgbModalRef, NgbModalOptions } from '@ng-bootstrap/ng-bootstrap';
import { FormGroup, Validators, FormBuilder } from '@angular/forms';
import { Subscription } from 'rxjs/Subscription';
import { ReplaySubject } from 'rxjs/ReplaySubject';
import 'rxjs/add/operator/filter';
import 'rxjs/add/operator/takeUntil';
import { forkJoin } from 'rxjs/observable/forkJoin';

import { ApiService } from '../../shared/services/api.service';
import { GlobalService } from '../../shared/services/global.service';
import { WalletInfo } from '../../shared/classes/wallet-info';
import { Error } from '../../shared/classes/error';
import { TumblebitService } from './tumblebit.service';
import { ConnectRequest } from './classes/connect-request';
import { CycleInfo } from './classes/cycle-info';
import { ModalService } from '../../shared/services/modal.service';
import { CompositeDisposable } from '../../shared/classes/composite-disposable';
import { Observable, Subject } from 'rxjs';

import { PasswordConfirmationComponent } from './password-confirmation/password-confirmation.component';
import { ConnectionModalComponent } from '../../shared/components/connection-modal/connection-modal.component';

@Component({
  selector: 'tumblebit-component',
  providers: [TumblebitService],
  templateUrl: './tumblebit.component.html',
  styleUrls: ['./tumblebit.component.css'],
})
export class TumblebitComponent implements OnDestroy {
  
    private destroyed$ = new ReplaySubject<any>();
    private walletInfosSubscription: Subscription;
    private progressSubscription: Subscription;
    private timer: any;
    private connectionModal: NgbModalRef;
    private started = false;
    private routerSubscriptions: CompositeDisposable;
    private startSubscriptions: CompositeDisposable;
    private connectionFatalError = false;
    private readonly loginPath = '/login';
    private routerPath;
    private _isBitcoin = true;
    private _tumbling = false;
    public coinUnit: string;
    public confirmedBalance: number;
    public unconfirmedBalance: number;
    public totalBalance: number;
    public destinationWalletName: string;
    public destinationConfirmedBalance: number;
    public destinationUnconfirmedBalance: number;
    public destinationTotalBalance: number;
    public destinationWalletBalanceSubscription: Subscription;
    public connectionSubscription: Subscription;
    public isConnected = false;
    public isSynced = false;
    public tumblerAddressCopied = false;
    public tumblerParameters: any;
    public estimate: number;
    public fee: number;
    public denomination: number;
    public progressDataArray: CycleInfo[];
    public tumbleForm: FormGroup;
    public wallets: [string];
    public tumblerAddress = 'Connecting...';
    public hasRegistrations = false;
    public connectionInProgress = false;
    public operation: 'connect' | 'changeserver' = 'connect';
    public parametersAreStandard = false;
    public shouldStayConnected = false;

  tumbleFormErrors = {
    'selectWallet': ''
  }

  validationMessages = {
    'selectWallet': {
      'required': 'A destination address is required.',
    }
  }

  constructor(
    private apiService: ApiService,
    private tumblebitService: TumblebitService,
    private globalService: GlobalService,
    private modalService: NgbModal,
    private genericModalService: ModalService,
    private fb: FormBuilder,
    private router: Router) {

      this.buildTumbleForm();
      this.initialise();
  }

  ngOnDestroy() {
    this.stop();
    if (this.routerSubscriptions) {
      this.routerSubscriptions.unsubscribe();
    }
  }

  get tumbling(): boolean {
    return this._tumbling;
  }
  set tumbling(value: boolean) {
      if (this.tumbling !== value) {
        this._tumbling = value;
        if (this.bitcoin) {
            this.tumblebitService.bitcoinTumbling = value;
        } else {
            this.tumblebitService.stratisTumbling = value;
        }
      }
  }

  private static isNavigationEnd(event: RouterEvent, path: string): boolean {
    return (event instanceof NavigationEnd && event.url === path);
  }

  public get bitcoin(): boolean {
      return this._isBitcoin;
  }

  protected set isBitcoin(value: boolean) {
    this._isBitcoin = value;
    this.routerPath = this._isBitcoin ? '/wallet/privacy' : '/wallet/stratis-wallet/privacy-strat';
  }

  get connectionRequestTimeoutSeconds(): number {
    return 600;
  }

  get allowChangeServer(): boolean {
    return !this.tumbling && this.isConnected;
  }

  private initialise(): void {

    this.isBitcoin = true;

    if (this.routerSubscriptions) {
      this.routerSubscriptions.unsubscribe();
    }

    const routerEvents = this.router.events;

    this.routerSubscriptions = new CompositeDisposable(
        [
            routerEvents.filter(x => this.started && TumblebitComponent.isNavigationEnd(<RouterEvent>x, this.loginPath))
                .subscribe(_ => this.stop()), 
            routerEvents.filter(x => !this.started && TumblebitComponent.isNavigationEnd(<RouterEvent>x, this.routerPath))
                .subscribe(_ => this.start())
        ]);
  }

  private start() {
    this.destroyed$ = new ReplaySubject<any>();
    this.operation = 'connect';
    this.tumblerAddress = 'Connecting...';
    this.coinUnit = this.globalService.getCoinUnit();
    this.connectionFatalError = false;

    this.startSubscriptions = new CompositeDisposable([
        this.checkTumblingStatus(),
        this.checkWalletStatus(),
        this.getWalletFiles(),
        this.getWalletBalance()
    ]);

    this.started = true;
  }

  private stop() {
    if (this.destinationWalletBalanceSubscription) {
      this.destinationWalletBalanceSubscription.unsubscribe();
      this.destinationWalletBalanceSubscription = null;
    }

    if (this.progressSubscription) {
      this.progressSubscription.unsubscribe();
      this.progressSubscription = null;
    }

    if (this.connectionSubscription) {
      this.connectionSubscription.unsubscribe();
      this.connectionSubscription = null;
    }

    if (this.startSubscriptions) {
      this.startSubscriptions.unsubscribe();
      this.startSubscriptions = null;
    }

    if (this.walletInfosSubscription) {
      this.walletInfosSubscription.unsubscribe();
      this.walletInfosSubscription = null;
    }

    this.stopConnectionRequest();
    this.isConnected = false;

    this.started = false;
  }

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
    this.destinationWalletName = this.tumbleForm.get('selectWallet').value;

    if (this.destinationWalletName) {
      this.getDestinationWalletBalance();
    }

    if (!originalForm) { return; }
    const form = originalForm;
    for (const field in formErrors) {
      if (!formErrors.hasOwnProperty(field)) { continue; }
      formErrors[field] = '';
      const control = form.get(field);
      if (control && control.dirty && !control.valid) {
        const messages = this.validationMessages[field];
        for (const key in control.errors) {
          if (control.errors.hasOwnProperty(key)) {
            formErrors[field] += messages[key] + ' ';
          }
        }
      }
    }
  }

  private checkWalletStatus(): Subscription {
    const walletInfo = new WalletInfo(this.globalService.getWalletName());
    return Observable.interval(this.apiService.pollingInterval)
                     .subscribe(x => this.getWalletInfos(walletInfo));
  }

  private getWalletInfos(walletInfo: WalletInfo): void {
    if (this.walletInfosSubscription) {
      this.walletInfosSubscription.unsubscribe();
    }
    this.walletInfosSubscription = forkJoin(this.apiService.getGeneralInfoForCoin(walletInfo, 'Bitcoin'), 
                                            this.apiService.getGeneralInfoForCoin(walletInfo, 'Stratis')).
                                   subscribe(x => this.onCoinsGeneralInfo(x[0], x[1]), 
                                             e => this.onPollingError(e, 'Failed to get general wallet information. Reason: API is not responding or timing out.'));
  
  }

  private onCoinsGeneralInfo(bitcoinInfo, stratisInfo) {
    this.isSynced = TumblebitComponent.isCoinSynced(bitcoinInfo) && 
                    TumblebitComponent.isCoinSynced(stratisInfo);
  }

  private onPollingError(error: any, errorMessage: string) {
    console.log(error);
    if (error.status === 0) {
      this.genericModalService.openModal(Error.toDialogOptions(errorMessage, null));
    } else if (error.status >= 400) {
      const firstError = Error.getFirstError(error);
      if (!firstError) {
        console.log(error);
      } else {
        if (firstError.description) {
          this.genericModalService.openModal(Error.toDialogOptions(error, null));
        }
      }
    }
  }

  private static isCoinSynced(coinInfo: any) {
    return coinInfo.lastBlockSyncedHeight === coinInfo.chainTip;
  }

  private checkTumblingStatus(): Subscription {

    this.hasRegistrations = this.tumbling = false;

    return this.tumblebitService.getTumblingState()
      .takeUntil(this.destroyed$)
      .subscribe(
        response => {

          if (response.status >= 200 && response.status < 400) {
            if (response.json().registrations >= response.json().minRegistrations) {
              this.hasRegistrations = true;
            } else {
              this.hasRegistrations = false;
            }

            if (!this.isConnected && this.isSynced) {
              this.connectToTumbler();
            }

            if (response.json().state === 'OnlyMonitor') {
              this.tumbling = false;
              if (this.progressSubscription) {
                this.progressSubscription.unsubscribe();
              }
            } else if (response.json().state === 'Tumbling') {
              this.tumbling = true;
              if (!this.progressSubscription) {
                this.getProgress();
              }
              this.destinationWalletName = response.json().destinationWallet;
              this.getDestinationWalletBalance();
            }
          }
        },
        e => this.onPollingError(e, 'Failed to get tumbling state. Reason: API is not responding or timing out.')
      );
  }

  private connectToTumbler() {

    if (this.connectionInProgress) {
      return;
    }

    this.startConnectionRequest();

    if (this.connectionSubscription) {
      this.connectionSubscription.unsubscribe();
    }

    const connectRequest = new ConnectRequest(
      this.globalService.getWalletName()
    );
	
    this.connectionSubscription = this.tumblebitService
      .connectToTumbler(this.operation, connectRequest)
      .subscribe(
        // TODO abstract into shared utility method
        response => {
          this.connectionFatalError = response.status >= 400;
          if (response.status >= 200 && response.status < 400) {
            this.tumblerParameters = response.json();
            this.tumblerAddress = this.tumblerParameters.tumbler
            this.estimate = this.tumblerParameters.estimate;
            this.fee = this.tumblerParameters.fee;
            this.denomination = this.tumblerParameters.denomination;
            this.parametersAreStandard = this.tumblerParameters.parameters_are_standard.toLowerCase() === "true";

            if (!!this.connectionModal) {
              this.connectionModal.dismiss();
            }

            if (this.tumbling) {
              this.markAsConnected();
              return;
            }

            const ngbModalOptions: NgbModalOptions = {
              backdrop : 'static',
              keyboard : false
            };
            this.connectionModal = this.modalService.open(ConnectionModalComponent, ngbModalOptions);
            this.connectionModal.componentInstance.server = this.tumblerAddress;
            this.connectionModal.componentInstance.denomination = this.denomination;
            this.connectionModal.componentInstance.fees = this.fee;
            this.connectionModal.componentInstance.estimatedTime = this.estimate;
            this.connectionModal.componentInstance.coinUnit = this.coinUnit;
            this.connectionModal.componentInstance.isStandardServer = this.parametersAreStandard;
            this.connectionModal.result.then(result => {
              this.stopConnectionRequest();
              if (result === 'skip') {
                this.markAsServerChangeRequired();
              } else {
                this.markAsConnected();
              }
            });
          }
        },
        error => {

          this.stop();

          console.error(error);
          this.isConnected = false;
          this.connectionFatalError = error.status >= 400;
          if (error.status === 0 && !this.connectionFatalError) {
            this.genericModalService.openModal(
              Error.toDialogOptions('Failed to connect to tumbler. Reason: API is not responding or timing out.', null));
          } else if (error.status >= 400) {
            if (!error.json().errors[0]) {
              console.error(error);
            } else {
                
              this.genericModalService.openModal(Error.toDialogOptions(error, null));
            
              this.router.navigate(['/wallet']);
              this.started = false;
              this.destroyed$.next(true);
              this.destroyed$.complete();
            }
          }
        }
      )
    ;
  }

  private startTumbling() {
    if (!this.destinationWalletName) {
      return;
    }

    if (!this.isConnected) {
      this.genericModalService.openModal({ body: 'Can\'t start tumbling when you\'re not connected to a server. Please try again later.'});
    } else {
      const modalRef = this.modalService.open(PasswordConfirmationComponent);
      modalRef.componentInstance.sourceWalletName = this.globalService.getWalletName();
      modalRef.componentInstance.destinationWalletName = this.destinationWalletName;
      modalRef.componentInstance.denomination = this.denomination;
      modalRef.componentInstance.fee = this.fee;
      modalRef.componentInstance.balance = this.unconfirmedBalance + this.confirmedBalance;
      modalRef.componentInstance.coinUnit = this.coinUnit;
    }
  }

  private stopTumbling() {
    this.genericModalService.confirm(
      {
        title: 'Are you sure you want to proceed?',
        body:
         'By stopping all current cycles, any current funds that are mid-cycle may take up to 12 hours to reimburse depending on the phase.'
      },
      () => {
        this.tumblebitService.stopTumbling()
          .subscribe(
            response => {
              if (response.status >= 200 && response.status < 400) {
                this.tumbling = false;
                this.progressSubscription.unsubscribe();
              }
            },
            e => this.onPollingError(e, 'Failed to stop tumbler. Reason: API is not responding or timing out.')
          );
        });
  }

  private markAsServerChangeRequired() {
    this.isConnected = false;
    this.operation = 'changeserver';
    this.tumblerAddress = 'Changing server...';
  }

  private markAsConnected() {
    this.isConnected = true;
    this.operation = 'connect';
    this.tumblerAddress = this.tumblerParameters.tumbler;
  }

  private getProgress() {

    this.progressSubscription = this.tumblebitService.getProgress()
      .subscribe(
        response => {
          if (response.status >= 200 && response.status < 400) {
            const responseArray = response.json().cycleProgressInfoList;
            if (responseArray) {
              this.progressDataArray = [];
              const responseData = responseArray;
              for (const cycle of responseData) {
                const periodStart = cycle.period.start;
                const periodEnd = cycle.period.end;
                const height = cycle.height;
                const blocksLeft = cycle.blocksLeft;
                const cycleStart = cycle.start;
                const cycleFailed = cycle.failed;
                const cycleAsciiArt = cycle.asciiArt;
                const cycleStatus = cycle.status;
                const cyclePhase = this.getPhaseString(cycle.phase, cycle.safetyPeriod);
                const cyclePhaseNumber = this.getPhaseNumber(cycle.phase);

                const item = new CycleInfo(
                  periodStart,
                    periodEnd,
                    height,
                    blocksLeft,
                    cycleStart,
                    cycleFailed,
                    cycleAsciiArt,
                    cycleStatus,
                    cyclePhase,
                    cyclePhaseNumber, 
                    cycle.shouldStayConnected);
                    
                  this.progressDataArray.push(item);
                  
                this.progressDataArray.sort(function(cycle1, cycle2) {
                  return cycle1.cycleStart - cycle2.cycleStart;
                });
              }
              this.shouldStayConnected = false;
              for (const item of this.progressDataArray) {
                if (item.shouldStayConnected) {
                  this.shouldStayConnected = true;
                  break;
                }
              }                
            }
          }
        },
        e => this.onPollingError(e, 'Failed to get tumbling progress. Reason: API is not responding or timing out.')
      );
  }

  private getPhaseNumber(phase: string) {
    switch (phase) {
      case 'Registration':
        return 1;
      case 'ClientChannelEstablishment':
        return 2;
      case 'TumblerChannelEstablishment':
        return 3;
      case 'PaymentPhase':
        return 4;
      case 'TumblerCashoutPhase':
        return 5;
      case 'ClientCashoutPhase':
        return 6;
    }
  }

  private getPhaseString(phase: string, isSafetyPeriod: boolean): string {
    let result;
    switch (phase) {
      case 'Registration':
        result = 'Registration'; 
        break;
      case 'ClientChannelEstablishment':
        result = 'Client Channel Establishment'; 
        break;
      case 'TumblerChannelEstablishment':
        result = 'Tumbler Channel Establishment'; 
        break;
      case 'PaymentPhase':
        result = 'Payment Phase'; 
        break;
      case 'TumblerCashoutPhase':
        result = 'Tumbler Cashout Phase'; 
        break;
      case 'ClientCashoutPhase':
        result = 'Client Cashout Phase'; 
        break;
    }
    return result && isSafetyPeriod ? `${result} Safety Period` : result;
  }

  // TODO: move into a shared service
  private getWalletBalance(): Subscription {
    const walletInfo = new WalletInfo(this.globalService.getWalletName())
    return this.apiService.getWalletBalance(walletInfo)
      .subscribe(
        response =>  {
          if (response.status >= 200 && response.status < 400) {
            var milli = new Date().getMilliseconds();
            const balanceResponse = response.json();
            this.confirmedBalance = balanceResponse.balances[0].amountConfirmed;
            this.unconfirmedBalance = balanceResponse.balances[0].amountUnconfirmed;
            this.totalBalance = this.confirmedBalance + this.unconfirmedBalance;
          }
        },
        e => {
          this.onPollingError(e, 'Failed to get wallet balance. Reason: API is not responding or timing out.');
        }
      );
  };

  private getDestinationWalletBalance() {
    if (this.destinationWalletBalanceSubscription) {
      this.destinationWalletBalanceSubscription.unsubscribe();
    }
    this.destinationWalletBalanceSubscription = this.tumblebitService.getWalletDestinationBalance(this.destinationWalletName)
      .subscribe(
        response =>  {
          if (response.status >= 200 && response.status < 400) {
            const balanceResponse = response.json();
            this.destinationConfirmedBalance = balanceResponse.balances[0].amountConfirmed;
            this.destinationUnconfirmedBalance = balanceResponse.balances[0].amountUnconfirmed;
            this.destinationTotalBalance = this.destinationConfirmedBalance + this.destinationUnconfirmedBalance;
          }
        },
        e => this.onPollingError(e, 'Failed to get general wallet informationdestination wallet balance. Reason: API is not responding or timing out.')
      )
    ;
  };

  private getWalletFiles(): Subscription {
    return this.apiService.getWalletFiles()
      .subscribe(
        response => {
          if (response.status >= 200 && response.status < 400) {
            const responseMessage = response.json();
            this.wallets = responseMessage.walletsFiles;
            if (this.wallets.length > 0) {
              for (const wallet in this.wallets) {
                if (this.wallets.hasOwnProperty(wallet)) {
                  this.wallets[wallet] = this.wallets[wallet].slice(0, -12);
                }
              }

              this.removeSourceWallet();

              this.destinationConfirmedBalance = this.destinationUnconfirmedBalance = this.destinationTotalBalance = null;
              this.tumbleForm.controls['selectWallet'].setValue('');
            }
          }
        },
        e => this.onPollingError(e, 'Failed to get Wallet files')
      );
  }

  private removeSourceWallet() {
    const sourceWalletIndex = this.wallets.indexOf(this.globalService.getWalletName());
    if (sourceWalletIndex >= 0) {
      this.wallets.splice(sourceWalletIndex, 1);
    }
  }

  private startConnectionRequest() {
    this.connectionInProgress = true;
    this.timer = setTimeout(() => {
      this.connectionInProgress = false;
    }, this.connectionRequestTimeoutSeconds * 1000);
  }

  private stopConnectionRequest() {
    if (!!this.timer) {
      clearTimeout(this.timer);
    }
    this.connectionInProgress = false;
  }
}