import { Component, OnInit, Input } from '@angular/core';
import { Transaction } from 'interfaces/transaction';
import { TransactionDetailsComponent } from '../../../wallet/transaction-details/transaction-details.component';
import { TransactionInfo } from '../../classes/transaction-info';
import { NgbModal } from '@ng-bootstrap/ng-bootstrap';
import { GlobalService } from '../../../shared/services/global.service';

@Component({
    selector: 'app-transaction',
    templateUrl: './transaction.component.html',
    styleUrls: ['./transaction.component.css']
})
export class TransactionComponent implements OnInit {

    constructor(private modalService: NgbModal, private globalService: GlobalService) { }

    coinUnit;
    @Input() public transaction: Transaction;

    ngOnInit() {
        this.coinUnit = this.globalService.getCoinUnit();
    }

    public openTransactionDetailDialog(transaction: TransactionInfo) {
        const modalRef = this.modalService.open(TransactionDetailsComponent);
        modalRef.componentInstance.transaction = transaction;
    }
}
