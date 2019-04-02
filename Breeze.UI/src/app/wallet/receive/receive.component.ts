import { Component, OnInit } from '@angular/core';

import { ApiService } from '../../shared/services/api.service';
import { GlobalService } from '../../shared/services/global.service';
import { ModalService } from '../../shared/services/modal.service';

import { WalletInfo } from '../../shared/classes/wallet-info';
import { Error } from '../../shared/classes/error';

import { NgbModal, NgbActiveModal } from '@ng-bootstrap/ng-bootstrap';

@Component({
  // tslint:disable-next-line:component-selector
  selector: 'receive-component',
  templateUrl: './receive.component.html',
  styleUrls: ['./receive.component.css'],
})

export class ReceiveComponent implements OnInit {
  public address: any = '';
  public copied = false;
  private errorMessage: string;

  constructor(
    private apiService: ApiService,
    private globalService: GlobalService,
    public activeModal: NgbActiveModal,
    private genericModalService: ModalService) {}

  ngOnInit() {
    this.getUnusedReceiveAddresses();
  }

  public onCopiedClick() {
    this.copied = true;
  }

  private getUnusedReceiveAddresses() {
    const walletInfo = new WalletInfo(this.globalService.getWalletName())
    this.apiService.getUnusedReceiveAddress(walletInfo)
      .subscribe(
        response => {
          this.address = response;
        }
      )
    ;
  }
}
