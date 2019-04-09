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
    if (this.globalService.getNetwork() === "MainNet" || this.globalService.getNetwork() === "TestNet") {
      this.bitcoin = true;
    } else if (this.globalService.getNetwork() === "StratisMain" || this.globalService.getNetwork() === "StratisTest") {
      this.bitcoin = false;
    }
  }
}
