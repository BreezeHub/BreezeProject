import { Injectable } from '@angular/core';
import { Http, Headers, Response, RequestOptions, URLSearchParams } from '@angular/http';
import { Observable } from 'rxjs/Observable';
import 'rxjs/add/operator/map';
import 'rxjs/add/operator/mergeMap';
import 'rxjs/add/operator/take';
import 'rxjs/add/operator/concat';
import 'rxjs/add/observable/throw';

import { TumblerConnectionRequest } from './classes/tumbler-connection-request';
import { TumbleRequest } from './classes/tumble-request';
import { ConnectRequest } from './classes/connect-request';
import { GlobalService } from '../../shared/services/global.service';

@Injectable()
export class TumblebitService {
  // The service to connect to & operate a TumbleBit Server via the
  // TumbleBit.Client.CLI tool
  private headers = new Headers({ 'Content-Type': 'application/json' });

  private pollingInterval = 3000;

  constructor(private http: Http, private globalService: GlobalService) { };

  get tumblerClientUrl() {
    return `http://localhost:${this.globalService.bitcoinApiPort}/api/TumbleBit/`;
  }

  // Might make sense to populate tumblerParams here because services are singletons

  connectToTumbler(operation: 'connect' | 'changeserver', body: ConnectRequest): Observable<any> {
    return this.http
      .post(`${this.tumblerClientUrl}${operation}`, JSON.stringify(body), {headers: this.headers})
      .retryWhen(e => {
        return e
           .mergeMap((error: any) => {
              if (error.status  === 0) {
                return Observable.of(error.status).delay(5000)
              }
              return Observable.throw(error);
           })
           .take(5)
           .concat(Observable.throw(e));
      })
      .map((response: Response) => response);
  };

  changeTumblerServer(body: ConnectRequest): Observable<any> {
    return this.http
      .post(`${this.tumblerClientUrl}changeserver`, JSON.stringify(body), {headers: this.headers})
      .retryWhen(e => {
        return e
           .mergeMap((error: any) => {
              if (error.status  === 0) {
                return Observable.of(error.status).delay(5000)
              }
              return Observable.throw(error);
           })
           .take(5)
           .concat(Observable.throw(e));
      })
      .map((response: Response) => response);
  };

  getTumblingState(): Observable<any> {
    return Observable
      .interval(1000)
      .startWith(0)
      .switchMap(
        () => this.http.get(`${this.tumblerClientUrl}tumbling-state`)
                      .retryWhen(e => {
                        return e
                           .mergeMap((error: any) => {
                              if (error.status  === 0) {
                                return Observable.of(error.status).delay(5000)
                              }
                              return Observable.throw(error);
                           })
                           .take(5)
                           .concat(Observable.throw(e));
                      })
      )
      .map((response: Response) => response);
  }

  startTumbling(body: TumbleRequest): Observable<any> {
    return this.http
      .post(`${this.tumblerClientUrl}tumble`, JSON.stringify(body), {headers: this.headers})
      .map((response: Response) => response);
  };

  stopTumbling(): Observable<any> {
    return this.http
      .get(`${this.tumblerClientUrl}onlymonitor`)
      .map((response: Response) => response);
  }

  getProgress(): Observable<any> {
    return Observable
      .interval(this.pollingInterval)
      .startWith(0)
      .switchMap(
        () => this.http.get(`${this.tumblerClientUrl}progress`)
                  .retryWhen(e => {
                    return e
                       .mergeMap((error: any) => {
                          if (error.status  === 0) {
                            return Observable.of(error.status).delay(5000)
                          }
                          return Observable.throw(error);
                       })
                       .take(5)
                       .concat(Observable.throw(e));
                  })
      )
      .map((response: Response) => response);
  }

  getWalletDestinationBalance(data: string): Observable<any> {
    const params: URLSearchParams = new URLSearchParams();
    params.set('walletName', data);

    return Observable
      .interval(this.pollingInterval)
      .startWith(0)
      .switchMap(
        () => this.http.get(
          `${this.tumblerClientUrl}destination-balance`,
          new RequestOptions({headers: this.headers, search: params})))
      .map((response: Response) => response);
  }
}
