import { Component, Output, EventEmitter } from '@angular/core';

import { LicenseService } from '../../shared/services/license.service';

@Component({
  selector: 'app-license-agreement',
  templateUrl: './license-agreement.component.html',
  styleUrls: ['./license-agreement.component.css']
})
export class LicenseAgreementComponent {

  @Output() onOkClicked = new EventEmitter();

  constructor(private service: LicenseService) { }

  get licenseText() {
    return this.service.licenseText;
  }

  okClicked() {
    this.onOkClicked.emit();
  }
}
