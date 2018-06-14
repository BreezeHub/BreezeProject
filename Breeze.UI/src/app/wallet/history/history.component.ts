import { ApiService } from '../../shared/services/api.service';
import { Component, OnInit, OnDestroy } from '@angular/core';
import { Error } from '../../shared/classes/error';
import { FormGroup, FormControl, Validators, FormBuilder } from '@angular/forms';
import { formValidator } from '../../shared/helpers/form-validation-helper';
import { GlobalService } from '../../shared/services/global.service';
import { Transaction } from 'interfaces/transaction'
import { ModalService } from '../../shared/services/modal.service';
import { NgbModal, NgbActiveModal } from '@ng-bootstrap/ng-bootstrap';
import { Subscription } from 'rxjs/Subscription';
import { TransactionInfo } from '../../shared/classes/transaction-info';
import { WalletInfo } from '../../shared/classes/wallet-info';


@Component({
  selector: 'history-component',
  templateUrl: './history.component.html',
  styleUrls: ['./history.component.css'],
})
export class HistoryComponent implements OnInit, OnDestroy {
  private isSearching;
  private searchForm: FormGroup;
  private txiIdLength = 64;
  private walletHistorySubscription: Subscription;
  public coinUnit: string;
  public transactions: TransactionInfo[] = [];
  private formErrors = {
    'transactionId': ''
  };
  private validationMessages = {
    'transactionId': {
      'required': 'Please enter transaction id.'
    }
  };

  constructor(
    private apiService: ApiService,
    private globalService: GlobalService,
    private modalService: NgbModal,
    private genericModalService: ModalService,
    private fb: FormBuilder) {
    this.buildSearchFormValidation();
  }

  ngOnInit() {
    this.startSubscriptions();
    this.coinUnit = this.globalService.getCoinUnit();
  }

  ngOnDestroy() {
    this.cancelSubscriptions();
  }

  buildSearchFormValidation() {
    this.searchForm = this.fb.group({
      'transactionId': ['', Validators.required]
    });
    this.searchForm.valueChanges.subscribe(data => formValidator(this.searchForm, this.formErrors, this.validationMessages));
    formValidator(this.searchForm, this.formErrors, this.validationMessages)
  }

  // todo: add history in seperate service to make it reusable
  private getHistory() {
    const walletInfo = new WalletInfo(this.globalService.getWalletName())
    let historyResponse;
    this.walletHistorySubscription = this.apiService.getWalletHistory(walletInfo)
      .subscribe(
        response => {
          if (response.status >= 200 && response.status < 400) {
            if (response.json().transactionsHistory.length > 0) {
              if (!this.isSearching) {
                historyResponse = response.json().transactionsHistory;
                this.getTransactionInfo(historyResponse);
              }
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
      );
  };

  private getTransactionInfo(transactions: Array<Transaction>) {
    this.transactions = transactions.map(transaction => new TransactionInfo(transaction));
  }

  public onBlur() {
    this.isSearching = !!this.searchForm.get('transactionId').value;
  }

  public onClick(event) {
    event.target.select();
  }

  public searchTransactions() {
    const searchValue = this.searchForm.get('transactionId').value;
    if (!!searchValue) {
      //TODO : We probably need a regular expression to check whether transaction Id is valid when searching, check length for now.
      if (searchValue.length === this.txiIdLength) {
        this.transactions = searchValue ? this.transactions.filter(t => t.transactionId === searchValue) : this.transactions;
        this.isSearching = true;
      } else {
        this.formErrors.transactionId = 'Transaction id not valid';
      }

    } else {
      this.isSearching = false;
    }
  }

  private cancelSubscriptions() {
    if (this.walletHistorySubscription) {
      this.walletHistorySubscription.unsubscribe();
    }
  };

  private startSubscriptions() {
    this.getHistory();
  }
}
