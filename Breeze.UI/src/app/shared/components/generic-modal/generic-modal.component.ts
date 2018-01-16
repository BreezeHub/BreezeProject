import { Component, OnInit, Input } from '@angular/core';
import { Router } from '@angular/router';

import { NgbActiveModal } from '@ng-bootstrap/ng-bootstrap';

@Component({
  selector: 'app-generic-modal',
  templateUrl: './generic-modal.component.html',
  styleUrls: ['./generic-modal.component.css']
})
export class GenericModalComponent implements OnInit {

  @Input() public title = 'Something went wrong';
  @Input() public body = 'Something went wrong while connecting to the API. Please restart the application.';
  @Input() public helpUrl: string = null;
  @Input() public showHomeLink = false;

  constructor(public activeModal: NgbActiveModal, private router: Router) {}

  ngOnInit() {
  }

  goBack() {
    this.router.navigate(['/wallet']);
    this.activeModal.dismiss();
  }
}
