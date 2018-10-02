import { Component, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { Title } from '@angular/platform-browser';

import { ApiService } from './shared/services/api.service';

import { remote } from 'electron';

import 'rxjs/add/operator/retryWhen';
import 'rxjs/add/operator/delay';
import { GlobalService } from './shared/services/global.service';

@Component({
  selector: 'app-root',
  templateUrl: './app.component.html',
  styleUrls: ['./app.component.css'],
})

export class AppComponent implements OnInit {
  private errorMessage: any;
  private responseMessage: any;
  public loading = true;

  constructor(private router: Router, 
              private apiService: ApiService, 
              private titleService: Title, 
              private globalService: GlobalService) { }

  ngOnInit() {
    this.setTitle();
    this.apiService
        .getWalletFiles()
        .retryWhen(errors => errors.delay(2000))
        .subscribe(() => this.checkStratisDaemon());
  }

  private checkStratisDaemon() {
    this.apiService
        .getStratisWalletFiles()
        .retryWhen(errors => errors.delay(2000))
        .subscribe(() => this.startApp());
  }

  private startApp() {
    this.loading = false;

    if (this.globalService.masternodeMode) {
        this.apiService.masternodeDoWalletsExist()
                       .subscribe(exist => this.router.navigate([`/${exist ? 'login' : 'setup'}`]));
    } else {
        this.router.navigate(['/login']);
    }
    
  }

  private setTitle() {
    const applicationName = 'Breeze Wallet';
    const applicationVersion = remote.app.getVersion();
    const releaseCycle = 'beta';
    const newTitle = `${applicationName} v${applicationVersion} ${releaseCycle}`;
    this.titleService.setTitle(newTitle);
  }
}
