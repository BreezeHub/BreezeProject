import { Component, Input, OnInit, OnDestroy } from '@angular/core';
import { NgbModal, NgbActiveModal } from '@ng-bootstrap/ng-bootstrap';
import { Subscription } from 'rxjs';

import { ApiService } from '../../shared/services/api.service';
import { GlobalService } from '../../shared/services/global.service';
import { ModalService } from '../../shared/services/modal.service';

import { WalletInfo } from '../../shared/classes/wallet-info';
import { Error } from '../../shared/classes/error';
import { TransactionInfo } from '../../shared/classes/transaction-info';

@Component({
  // tslint:disable-next-line:component-selector
  selector: 'transaction-details',
  templateUrl: './transaction-details.component.html',
  styleUrls: ['./transaction-details.component.css']
})
export class TransactionDetailsComponent implements OnInit, OnDestroy {

  @Input() transaction: TransactionInfo;
  public copied = false;
  public coinUnit: string;
  public confirmations: number;
  private generalWalletInfoSubscription: Subscription;
  private lastBlockSyncedHeight: number;

  constructor(
    private apiService: ApiService,
    private globalService: GlobalService,
    private genericModalService: ModalService,
    public activeModal: NgbActiveModal) {}

  ngOnInit() {
    this.startSubscriptions();
    this.coinUnit = this.globalService.getCoinUnit();
  }

  ngOnDestroy() {
    this.cancelSubscriptions();
  }

  public onCopiedClick() {
    this.copied = true;
  }

  private getGeneralWalletInfo() {
    let walletInfo = new WalletInfo(this.globalService.getWalletName())
    this.generalWalletInfoSubscription = this.apiService.getGeneralInfo(walletInfo)
      .subscribe(
        response =>  {
          let generalWalletInfoResponse = response;
          this.lastBlockSyncedHeight = generalWalletInfoResponse.lastBlockSyncedHeight;
          this.getConfirmations(this.transaction);
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
      );
  };

  private getConfirmations(transaction: TransactionInfo) {
    if (transaction.transactionConfirmedInBlock) {
      this.confirmations = this.lastBlockSyncedHeight - Number(transaction.transactionConfirmedInBlock) + 1;
    } else {
      this.confirmations = 0;
    }
  }

  private cancelSubscriptions() {
    if (this.generalWalletInfoSubscription) {
      this.generalWalletInfoSubscription.unsubscribe();
    }
  };

  private startSubscriptions() {
    this.getGeneralWalletInfo();
  }
}
