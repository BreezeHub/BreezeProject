import { Component, OnDestroy, Input } from '@angular/core';

import { Observable } from 'rxjs/Observable';
import { Subscription } from 'rxjs/Subscription';
import 'rxjs/observable/interval';
import 'rxjs/add/operator/filter';

@Component({
  selector: 'app-connection-progress',
  templateUrl: './connection-progress.component.html',
  styleUrls: ['./connection-progress.component.css']
})
export class ConnectionProgressComponent implements OnDestroy {

  private _run = false;
  private _progress = 0;
  private _progressWidth = "";
  private _progressSeconds = 0;
  private _durationSeconds = 0;
  private _info = "";
  private intervalSubscription: Subscription;

  ngOnDestroy() {
    if (this.intervalSubscription) {
      this.intervalSubscription.unsubscribe();
    }
  }

  get progress(): number {
    return this._progress;
  }

  get progressWidth(): string {
    return `${this.progress}%`; 
  }

  get info(): string {
    return this._info;
  }

  @Input()
  set durationSeconds(value: number) {
    if (this._durationSeconds !== value) {
      this._durationSeconds = value;
    }
  }

  @Input()
  set run(value: boolean) {
    if (this._run !== value) {
      this._run = value;
      this.onRunChanged();
    }
  }

  private onRunChanged() {
    if (this.intervalSubscription) {
      this.intervalSubscription.unsubscribe();
      this.intervalSubscription = null; 
    } 
    if (this._run) {
      this._progressSeconds = this._durationSeconds;
      this.intervalSubscription = Observable.interval(1000).subscribe(_ => this.onInterval());
    }
  }

  private onInterval() {
    this.setProgress();
    if (this._run) {
      this._progressSeconds--;
    }
  }

  private setProgress() {
    const oneHundred = 100;
    const difference = this._durationSeconds - this._progressSeconds;
    const result = Math.ceil((difference / this._durationSeconds) * oneHundred);
    if (result <= oneHundred) {
      this._progress = result; 
      this.makeInfo();
      if (result === oneHundred) {
        this.run = false;
      } 
    }
  }

  private makeInfo() {
    let secondsRemaining = this._durationSeconds - this._progressSeconds;
    secondsRemaining = this._durationSeconds - secondsRemaining;
    
    let date = new Date(null);
    date.setSeconds(secondsRemaining);
    const remaining = `${date.getMinutes()}m:${date.getSeconds()}s`;

    date = new Date(null);
    date.setSeconds(this._durationSeconds);
    const duration = `${date.getMinutes()}mins`;

    this._info = `Connecting to the MasterNode can take time.  This connection cycle of ${duration} will timeout in ${remaining}, and then retry.`;
  }
}
