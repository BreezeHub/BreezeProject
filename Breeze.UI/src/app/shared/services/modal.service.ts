import {Injectable} from '@angular/core';

import { NgbModal, NgbActiveModal, NgbModalRef } from '@ng-bootstrap/ng-bootstrap';

import { GenericModalComponent } from '../components/generic-modal/generic-modal.component';

import { DialogOptions } from '../classes/dialog-options';

import { Log } from '../services/logger.service';

@Injectable()
export class ModalService {
  private modalRef: NgbModalRef;
  constructor(private modalService: NgbModal) {}

  public openModal(options: DialogOptions) {
    // check and dispose existing modal popup
    if (!!this.modalRef) {
      this.modalRef.dismiss();
    }

    this.modalRef = this.modalService.open(GenericModalComponent);
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
  }
}
