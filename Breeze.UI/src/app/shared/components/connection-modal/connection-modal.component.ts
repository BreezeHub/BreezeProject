import { Component, OnInit, Input } from '@angular/core';
import { Router } from '@angular/router';

import { NgbActiveModal } from '@ng-bootstrap/ng-bootstrap';

@Component({
  selector: 'app-connection-modal',
  templateUrl: './connection-modal.component.html',
  styleUrls: ['./connection-modal.component.css']
})
export class ConnectionModalComponent implements OnInit {

  @Input() public server = '';
  @Input() public denomination = '';
  @Input() public fees = '';
  @Input() public estimatedTime = '';
  @Input() public coinUnit = '';

  constructor(public activeModal: NgbActiveModal, private router: Router) {}

  ngOnInit() {
  }

  skip() {
    this.activeModal.close('skip');
  }

  connect() {
    this.activeModal.close('connect');
  }
}
