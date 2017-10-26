import { Component, OnInit } from '@angular/core';

import { GlobalService } from '../../shared/services/global.service';

@Component({
  selector: 'app-menu',
  templateUrl: './menu.component.html',
  styleUrls: ['./menu.component.css'],
})
export class MenuComponent implements OnInit {
  constructor(private globalService: GlobalService) {}
  public bitcoin: Boolean = false;

  ngOnInit (){
    if (this.globalService.getCoinName() === "Bitcoin" || this.globalService.getCoinName() === "TestBitcoin") {
      this.bitcoin = true;
    } else if (this.globalService.getCoinName() === "Stratis" || this.globalService.getCoinName() === "TestStratis") {
      this.bitcoin = false;
    }
  }
}
