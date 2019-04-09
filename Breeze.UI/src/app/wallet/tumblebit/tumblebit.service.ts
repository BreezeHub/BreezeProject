import { Injectable } from '@angular/core';
import { Http, Headers, Response, RequestOptions, URLSearchParams } from '@angular/http';
import { Observable, of, interval } from 'rxjs';
import { delay, retryWhen, map, mergeMap, take, concat, startWith, switchMap } from 'rxjs/operators';
import { TumbleRequest } from './classes/tumble-request';
import { ConnectRequest } from './classes/connect-request';
import { GlobalService } from '../../shared/services/global.service';
import { ServiceShared } from '../../shared/services/shared';

@Injectable({
  providedIn: 'root'
})
export class TumblebitService {

  // The service to connect to & operate a TumbleBit Server via the
  // TumbleBit.Client.CLI tool
  private headers = new Headers({ 'Content-Type': 'application/json' });
  private pollingInterval = interval(3000);
  constructor(private http: Http, private globalService: GlobalService) { };
  get tumblerClientUrl() {
    const bitcoinApiPort = this.globalService.getBitcoinApiPort();
    return `http://localhost:${bitcoinApiPort}/api/TumbleBit/`;
  }

  // Might make sense to populate tumblerParams here because services are singletons
  connectToTumbler(operation: 'connect' | 'changeserver', body: ConnectRequest): Observable<any> {
    return this.http
      .post(`${this.tumblerClientUrl}${operation}`, JSON.stringify(body), {headers: this.headers})
      .pipe(retryWhen(e => {
        return e
           .pipe(mergeMap((error: any) => {
              if (error.status  === 0) {
                return of(error.status).pipe(delay(5000))
              }
              return Observable.throw(error);
           }),
           take(5),
           concat(Observable.throw(e)))
      }),
      map((response: Response) => response));
  };

  changeTumblerServer(body: ConnectRequest): Observable<any> {
    return this.http
      .post(`${this.tumblerClientUrl}changeserver`, JSON.stringify(body), {headers: this.headers})
      .pipe(retryWhen(e => {
        return e.pipe(
           mergeMap((error: any) => {
              if (error.status  === 0) {
                return of(error.status).pipe(delay(5000))
              }
              return Observable.throw(error);
           }),
           take(5),
           concat(Observable.throw(e)));
      }),
      map((response: Response) => response));
  };

  getTumblingState(): Observable<any> {
    return this.pollingInterval.pipe(
      startWith(0),
      switchMap(
        () => this.http.get(`${this.tumblerClientUrl}tumbling-state`)
                      .pipe(retryWhen(e => ServiceShared.onRetryWhen(e)))),
      map((response: Response) => response));
  }

  startTumbling(body: TumbleRequest): Observable<any> {
    return this.http
      .post(`${this.tumblerClientUrl}tumble`, JSON.stringify(body), {headers: this.headers})
      .pipe(map((response: Response) => response));
  };

  stopTumbling(): Observable<any> {
    return this.http
      .get(`${this.tumblerClientUrl}onlymonitor`)
      .pipe(map((response: Response) => response));
  }

  getProgress(): Observable<any> {
    return this.pollingInterval.pipe(
      startWith(0),
      switchMap(
        () => this.http.get(`${this.tumblerClientUrl}progress`)
                  .pipe(retryWhen(e => ServiceShared.onRetryWhen(e)))),
      (map((response: Response) => response)));
  }

  getWalletDestinationBalance(data: string): Observable<any> {
    const params: URLSearchParams = new URLSearchParams();
    params.set('walletName', data);
    return this.pollingInterval.pipe(
      startWith(0),
      switchMap(
        () => this.http.get(`${this.tumblerClientUrl}destination-balance`, new RequestOptions({headers: this.headers, search: params}))
                        .pipe(retryWhen(e => ServiceShared.onRetryWhen(e)))),
      map((response: Response) => response));
  }
}
