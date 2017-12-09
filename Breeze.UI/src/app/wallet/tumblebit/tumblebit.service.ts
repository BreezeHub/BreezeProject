import { Injectable } from '@angular/core';
import { Http, Headers, Response, RequestOptions, URLSearchParams } from '@angular/http';
import { Observable } from 'rxjs/Observable';
import 'rxjs/add/operator/map';

import { TumblerConnectionRequest } from './classes/tumbler-connection-request';
import { TumbleRequest } from './classes/tumble-request';

@Injectable()
export class TumblebitService {
  // The service to connect to & operate a TumbleBit Server via the
  // TumbleBit.Client.CLI tool
  constructor(private http: Http) { };

  private tumblerClientUrl = 'http://localhost:37220/api/TumbleBit/';
  private headers = new Headers({ 'Content-Type': 'application/json' });

  private pollingInterval = 3000;

  // Might make sense to populate tumblerParams here because services are singletons

  connectToTumbler(): Observable<any> {
    return this.http
      .get(this.tumblerClientUrl + 'connect')
      .map((response: Response) => response);
  };

  getTumblingState(): Observable<any> {
    return Observable
      .interval(1000)
      .startWith(0)
      .switchMap(() => this.http.get(this.tumblerClientUrl + 'tumbling-state'))
      .map((response: Response) => response);
  }

  startTumbling(body: TumbleRequest): Observable<any> {
    return this.http
      .post(this.tumblerClientUrl + 'tumble', JSON.stringify(body), {headers: this.headers})
      .map((response: Response) => response);
  };

  stopTumbling(): Observable<any> {
    return this.http
      .get(this.tumblerClientUrl + 'onlymonitor')
      .map((response: Response) => response);
  }

  getProgress(): Observable<any> {
    return Observable
      .interval(this.pollingInterval)
      .startWith(0)
      .switchMap(() => this.http.get(this.tumblerClientUrl + 'progress'))
      .map((response: Response) => response);
  }

  getWalletDestinationBalance(data: string): Observable<any> {
    let params: URLSearchParams = new URLSearchParams();
    params.set('walletName', data);

    return Observable
      .interval(this.pollingInterval)
      .startWith(0)
      .switchMap(() => this.http.get(this.tumblerClientUrl + 'destination-balance', new RequestOptions({headers: this.headers, search: params})))
      .map((response: Response) => response);
  }
}
