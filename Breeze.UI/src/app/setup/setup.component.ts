import { Component } from '@angular/core';
import {Location} from '@angular/common';
import { Router } from '@angular/router';
import { GlobalService } from '../shared/services/global.service';

@Component({
  selector: 'setup-component',
  templateUrl: './setup.component.html',
  styleUrls: ['./setup.component.css'],
})
export class SetupComponent {
  private _allowNavigationBack = false;

  constructor(private router: Router, private location: Location, private globalService: GlobalService) {
      this._allowNavigationBack = !globalService.masternodeMode;
  }
  
  get allowNavigationBack(): boolean {
      return this._allowNavigationBack;
  }

  public onCreateClicked() {
    this.router.navigate(['/setup/create']);
  }

  public onRecoverClicked() {
    this.router.navigate(['/setup/recover']);
  }

  public onBackClicked() {
    this.router.navigate(['']);
  }
}
