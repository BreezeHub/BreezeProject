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

  private tumblerClientUrl = 'http://localhost:5000/api/TumbleBit/';
  private headers = new Headers({ 'Content-Type': 'application/json' });

  // Might make sense to populate tumblerParams here because services are singletons

  connectToTumbler(body: TumblerConnectionRequest): Observable<any> {
    return this.http
      .post(this.tumblerClientUrl + 'connect', JSON.stringify(body), {headers: this.headers})
      .map((response: Response) => response);

  };

  getTumblingStatus(): Observable<any> {
    return this.http
      .get(this.tumblerClientUrl + 'is_tumbling')
      .map((response: Response) => response);
  }

  startTumbling(body: TumbleRequest): Observable<any> {
    return this.http
      .post(this.tumblerClientUrl + 'tumble', JSON.stringify(body), {headers: this.headers})
      .map((response: Response) => response);
  };

  stopTumbling(): Observable<any> {
    return this.http
      .get(this.tumblerClientUrl + 'stop')
      .map((response: Response) => response);
  }
}
