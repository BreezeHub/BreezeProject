import { Component, OnInit } from '@angular/core';

import { ApiService } from '../../shared/services/api.service';
import { GlobalService } from '../../shared/services/global.service';
import { ModalService } from '../../shared/services/modal.service';

import { WalletInfo } from '../../shared/classes/wallet-info';
import { Error } from '../../shared/classes/error';

import { NgbModal, NgbActiveModal } from '@ng-bootstrap/ng-bootstrap';

@Component({
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
          if (response.status >= 200 && response.status < 400) {
            this.address = response.json();
          }
        },
        error => {
          console.log(error);
          if (error.status === 0) {
            this.genericModalService.openModal(null);
          } else if (error.status >= 400) {
            if (!error.json().errors[0]) {
              console.log(error);
            } else {
              this.genericModalService.openModal(Error.toDialogOptions(error, null));
            }
          }
        }
      )
    ;
  }
}
