import { Component, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { Title } from '@angular/platform-browser';
import { remote } from 'electron';
import { Subscription } from 'rxjs';
import { retryWhen, delay, tap } from 'rxjs/operators';

import { ApiService } from './shared/services/api.service';
import { NodeStatus } from './shared/classes/node-status';
import { GlobalService } from './shared/services/global.service';

@Component({
  selector: 'app-root',
  templateUrl: './app.component.html',
  styleUrls: ['./app.component.css'],
})

export class AppComponent implements OnInit {
  public loading = true;
  public loadingFailed = false;
  private readonly MaxRetryCount = 50;
  private readonly TryDelayMilliseconds = 3000;
  private subscription: Subscription;

  constructor(private router: Router, private apiService: ApiService, private titleService: Title, private globalService: GlobalService) {}

  ngOnInit() {
    this.setTitle();
    this.tryStart();
  }

  private tryStart() {
    let retry = 0;
    const stream$ = this.apiService.getNodeStatus(true).pipe(
      retryWhen(errors =>
        errors.pipe(delay(this.TryDelayMilliseconds)).pipe(
          tap(errorStatus => {
            if (retry++ === this.MaxRetryCount) {
              throw errorStatus;
            }
            console.log(`Retrying ${retry}...`);
          })
        )
      )
    );

    this.subscription = stream$.subscribe(
      (data: NodeStatus) => {
        this.loading = false;
        this.router.navigate(['login'])
      }, (error: any) => {
        console.log('Failed to start wallet');
        this.loading = false;
        this.loadingFailed = true;
      }
    )
  }

  private setTitle() {
    let applicationName = "Breeze Wallet";
    let applicationVersion = this.globalService.getApplicationVersion();
    let newTitle = applicationName + " " + applicationVersion;
    this.titleService.setTitle(newTitle);
  }

}
