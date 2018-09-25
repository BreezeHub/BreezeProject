import { Component, OnInit } from '@angular/core';

import { GlobalService } from '../../shared/services/global.service';
import { TumblebitService } from '../../wallet/tumblebit/tumblebit.service';

@Component({
  selector: 'app-menu',
  templateUrl: './menu.component.html',
  styleUrls: ['./menu.component.css'],
})
export class MenuComponent implements OnInit {
  constructor(private globalService: GlobalService, private tumblebitService: TumblebitService) {}
  
  bitcoin: Boolean = false;

  get allowBitcoinPrivacy(): boolean {
      return !this.tumblebitService.stratisTumbling;
  }

  get allowStratisPrivacy(): boolean {
      return !this.tumblebitService.bitcoinTumbling;
  }

  disabledReason = (ticker) => `(Not available - ${ticker} is tumbling)`;

  ngOnInit (){
    if (this.globalService.getCoinName() === "Bitcoin" || this.globalService.getCoinName() === "TestBitcoin") {
      this.bitcoin = true;
    } else if (this.globalService.getCoinName() === "Stratis" || this.globalService.getCoinName() === "TestStratis") {
      this.bitcoin = false;
    }
  }
}
