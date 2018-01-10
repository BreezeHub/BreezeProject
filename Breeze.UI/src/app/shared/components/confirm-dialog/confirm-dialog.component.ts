import { Component, OnInit, Input } from '@angular/core';

import { NgbActiveModal } from '@ng-bootstrap/ng-bootstrap';

@Component({
  selector: 'app-confirm-dialog',
  templateUrl: './confirm-dialog.component.html',
  styleUrls: ['./confirm-dialog.component.css']
})
export class ConfirmDialogComponent implements OnInit {

  @Input() public title = 'Are you sure you want to proceed?';
  @Input() public body = '';
  @Input() public noLabel = 'Cancel';
  @Input() public yesLabel = 'Yes, proceed';

  constructor(public activeModal: NgbActiveModal) {}

  ngOnInit() {
  }
}
