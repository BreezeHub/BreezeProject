import { Component, OnInit, OnDestroy } from '@angular/core';

import { Observable, Subscription } from 'rxjs';

import { ApiService } from '../../shared/services/api.service';
import { GlobalService } from '../../shared/services/global.service';
import { ModalService } from '../../shared/services/modal.service';

import { WalletInfo } from '../../shared/classes/wallet-info';
import { Error } from '../../shared/classes/error';

@Component({
  // tslint:disable-next-line:component-selector
  selector: 'status-bar',
  templateUrl: './status-bar.component.html',
  styleUrls: ['./status-bar.component.css']
})
export class StatusBarComponent implements OnInit, OnDestroy {

  private generalWalletInfoSubscription: Subscription;
  public lastBlockSyncedHeight: number;
  public chainTip: number;
  private isChainSynced: boolean;
  public connectedNodes = 0;
  private percentSyncedNumber = 0;
  public percentSynced: string;

  constructor(
    private apiService: ApiService,
    private globalService: GlobalService,
    private genericModalService: ModalService) { }

  ngOnInit() {
    this.startSubscriptions();
  }

  ngOnDestroy() {
    this.cancelSubscriptions();
  }

  private getGeneralWalletInfo() {
    const walletInfo = new WalletInfo(this.globalService.getWalletName())
    this.generalWalletInfoSubscription = this.apiService.getGeneralInfo(walletInfo)
      .subscribe(
        response =>  {
          const generalWalletInfoResponse = response;
          this.lastBlockSyncedHeight = generalWalletInfoResponse.lastBlockSyncedHeight;
          this.chainTip = generalWalletInfoResponse.chainTip;
          this.isChainSynced = generalWalletInfoResponse.isChainSynced;
          this.connectedNodes = generalWalletInfoResponse.connectedNodes;

          if (!this.isChainSynced) {
            this.percentSynced = 'syncing...';
          } else {
            this.percentSyncedNumber = ((this.lastBlockSyncedHeight / this.chainTip) * 100);
            if (this.percentSyncedNumber.toFixed(0) === '100' && this.lastBlockSyncedHeight !== this.chainTip) {
              this.percentSyncedNumber = 99;
            }

            this.percentSynced = this.percentSyncedNumber.toFixed(0) + '%';
          }
        },
        error => {
          if (error.status === 0) {
            this.cancelSubscriptions();
          } else if (error.status >= 400) {
            if (!error.error.errors[0].message) {
              this.cancelSubscriptions();
              this.startSubscriptions();
            }
          }
        }
      )
    ;
  };

  private cancelSubscriptions() {
    if (this.generalWalletInfoSubscription) {
      this.generalWalletInfoSubscription.unsubscribe();
    }
  };

  private startSubscriptions() {
    this.getGeneralWalletInfo();
  }
}
