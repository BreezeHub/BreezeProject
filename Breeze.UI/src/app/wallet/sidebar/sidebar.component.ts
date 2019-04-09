import { Component, OnInit } from '@angular/core';
import { NgbModal, NgbActiveModal } from '@ng-bootstrap/ng-bootstrap';
import { LogoutConfirmationComponent } from '../logout-confirmation/logout-confirmation.component';
import { Router } from '@angular/router';

import { GlobalService } from '../../shared/services/global.service';

@Component({
  selector: 'sidebar',
  templateUrl: './sidebar.component.html',
  styleUrls: ['./sidebar.component.css']
})
export class SidebarComponent implements OnInit {
  public bitcoinActive: boolean;
  public stratisActive: boolean;

  constructor(
    private globalService: GlobalService,
    private router: Router,
    private modalService: NgbModal) { }


  ngOnInit() {
    if (this.globalService.getNetwork() === "MainNet" || this.globalService.getNetwork() === "TestNet") {
      this.bitcoinActive = true;
      this.stratisActive = false;
    } else if (this.globalService.getNetwork() === "StratisMain" || this.globalService.getNetwork() === "StratisTest") {
      this.bitcoinActive = false;
      this.stratisActive = true;
    }
  }

  public loadBitcoinWallet() {
    this.bitcoinActive = true;
    this.stratisActive = false;
    this.router.navigate(['/wallet']);
  }

  public loadStratisWallet() {
    this.bitcoinActive = false;
    this.stratisActive = true;
    this.router.navigate(['/stratis-wallet']);
  }

  public logOut() {
    const modalRef = this.modalService.open(LogoutConfirmationComponent);
  }
}
