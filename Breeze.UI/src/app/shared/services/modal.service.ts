import {Injectable} from '@angular/core';

import { NgbModal, NgbActiveModal, NgbModalRef } from '@ng-bootstrap/ng-bootstrap';

import { GenericModalComponent } from '../components/generic-modal/generic-modal.component';
import { ConfirmDialogComponent } from '../components/confirm-dialog/confirm-dialog.component';

import { DialogOptions } from '../classes/dialog-options';
import { ConfirmDialogOptions } from '../classes/confirm-dialog-options';

import { Log } from '../services/logger.service';

@Injectable()
export class ModalService {
  private modalRef: NgbModalRef;
  private confirmDialogRef: NgbModalRef;
  constructor(private modalService: NgbModal) {}

  public openModal(options: DialogOptions) {
    // check and dispose existing modal popup
    if (!!this.modalRef) {
      this.modalRef.dismiss();
    }

    if (!!options && !!options.modalOptions) {
      this.modalRef = this.modalService.open(GenericModalComponent, options.modalOptions);
    } else {
      this.modalRef = this.modalService.open(GenericModalComponent);
    }

    if (!options) { return; }
    if (!!options.title) {
      this.modalRef.componentInstance.title = options.title;
    }

    if (!!options.body) {
      this.modalRef.componentInstance.body = options.body;
    }

    if (!!options.helpUrl) {
      this.modalRef.componentInstance.helpUrl = options.helpUrl;
    }

    if (options.showHomeLink) {
      this.modalRef.componentInstance.showHomeLink = options.showHomeLink;
    }
  }

  public confirm(options: ConfirmDialogOptions, confirmCallback: Function) {
    // check and dispose existing modal popup
    if (!!this.confirmDialogRef) {
      this.confirmDialogRef.dismiss();
    }

    this.confirmDialogRef = this.modalService.open(ConfirmDialogComponent);
    this.confirmDialogRef.result.then((result) => {
      if (!!confirmCallback && !!result) {
        confirmCallback();
      }
    });

    if (!options) { return; }
    if (!!options.title) {
      this.confirmDialogRef.componentInstance.title = options.title;
    }

    if (!!options.body) {
      this.confirmDialogRef.componentInstance.body = options.body;
    }

    if (!!options.noLabel) {
      this.confirmDialogRef.componentInstance.noLabel = options.noLabel;
    }

    if (!!options.yesLabel) {
      this.confirmDialogRef.componentInstance.yesLabel = options.yesLabel;
    }
  }
}
