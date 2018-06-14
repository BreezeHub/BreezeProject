import { ApiService } from '../../shared/services/api.service';
import { Component, OnInit, OnDestroy } from '@angular/core';
import { Error } from '../../shared/classes/error';
import { GlobalService } from '../../shared/services/global.service';
import { ModalService } from '../../shared/services/modal.service';
import { NgbModal } from '@ng-bootstrap/ng-bootstrap';
import { ReceiveComponent } from '../receive/receive.component';
import { SendComponent } from '../send/send.component';
import { Subscription } from 'rxjs/Subscription';
import { TransactionInfo } from '../../shared/classes/transaction-info';
import { WalletInfo } from '../../shared/classes/wallet-info';
import { Transaction } from 'interfaces/transaction';

@Component({
  // tslint:disable-next-line:component-selector
  selector: 'dashboard-component',
  templateUrl: './dashboard.component.html',
  styleUrls: ['./dashboard.component.css']
})

export class DashboardComponent implements OnInit, OnDestroy {
  public walletName: string;
  public coinUnit: string;
  public confirmedBalance: number;
  public unconfirmedBalance: number;
  public transactionArray: TransactionInfo[];
  private walletBalanceSubscription: Subscription;
  private walletHistorySubscription: Subscription;

  constructor(
    private apiService: ApiService,
    private globalService: GlobalService,
    private modalService: NgbModal,
    private genericModalService: ModalService) { }

  ngOnInit() {
    this.startSubscriptions();
    this.walletName = this.globalService.getWalletName();
  };

  ngOnDestroy() {
    this.cancelSubscriptions();
  };

  public openSendDialog() {
    const modalRef = this.modalService.open(SendComponent);
  };

  public openReceiveDialog() {
    const modalRef = this.modalService.open(ReceiveComponent);
  };

  private getWalletBalance() {
    const walletInfo = new WalletInfo(this.globalService.getWalletName());
    this.walletBalanceSubscription = this.apiService.getWalletBalance(walletInfo)
      .subscribe(
        response => {
          if (response.status >= 200 && response.status < 400) {
            const balanceResponse = response.json();
            this.confirmedBalance = balanceResponse.balances[0].amountConfirmed;
            this.unconfirmedBalance = balanceResponse.balances[0].amountUnconfirmed;
          }
        },
        error => {
          console.log(error);
          if (error.status === 0) {
            this.cancelSubscriptions();
            this.genericModalService.openModal(
              Error.toDialogOptions('Failed to get wallet balance. Reason: API is not responding or timing out.', null));
          } else if (error.status >= 400) {
            if (!error.json().errors[0]) {
              console.log(error);
            } else {
              if (error.json().errors[0].description) {
                this.genericModalService.openModal(Error.toDialogOptions(error, null));
              } else {
                this.cancelSubscriptions();
                this.startSubscriptions();
              }
            }
          }
        }
      )
      ;
  };

  // todo: add history in seperate service to make it reusable
  private getHistory() {
    const walletInfo = new WalletInfo(this.globalService.getWalletName());
    let historyResponse;
    this.walletHistorySubscription = this.apiService.getWalletHistory(walletInfo)
      .subscribe(
        response => {
          if (response.status >= 200 && response.status < 400) {
            if (response.json().transactionsHistory.length > 0) {
              historyResponse = response.json().transactionsHistory;
              this.getTransactionInfo(historyResponse);
            }
          }
        },
        error => {
          console.log(error);
          if (error.status === 0) {
            this.cancelSubscriptions();
            this.genericModalService.openModal(
              Error.toDialogOptions('Failed to get wallet history. Reason: API is not responding or timing out.', null));
          } else if (error.status >= 400) {
            if (!error.json().errors[0]) {
              console.log(error);
            } else {
              if (error.json().errors[0].description) {
                this.genericModalService.openModal(Error.toDialogOptions(error, null));
              } else {
                this.cancelSubscriptions();
                this.startSubscriptions();
              }
            }
          }
        }
      )
      ;
  };

  private getTransactionInfo(transactions: Array<Transaction>) {
    this.transactionArray = transactions.map(transaction => new TransactionInfo(transaction));
  }

  private cancelSubscriptions() {
    if (this.walletBalanceSubscription) {
      this.walletBalanceSubscription.unsubscribe();
    }

    if (this.walletHistorySubscription) {
      this.walletHistorySubscription.unsubscribe();
    }
  };

  private startSubscriptions() {
    this.getWalletBalance();
    this.getHistory();
  }
}
