import { Injectable } from '@angular/core';
import { HttpClient, HttpParams, HttpErrorResponse } from '@angular/common/http';
import { Observable, interval, throwError } from 'rxjs';
import { catchError, switchMap, startWith} from 'rxjs/operators';

import { GlobalService } from './global.service';

import { WalletCreation } from '../classes/wallet-creation';
import { WalletRecovery } from '../classes/wallet-recovery';
import { WalletLoad } from '../classes/wallet-load';
import { WalletInfo } from '../classes/wallet-info';
import { FeeEstimation } from '../classes/fee-estimation';
import { TransactionBuilding } from '../classes/transaction-building';
import { TransactionSending } from '../classes/transaction-sending';
import { ServiceShared } from './shared';
import { ModalService } from './modal.service';
import { Router } from '@angular/router';
import { NodeStatus } from '../classes/node-status';

@Injectable({
  providedIn: 'root'
})
export class ApiService {

  constructor(private http: HttpClient, private globalService: GlobalService, private modalService: ModalService, private router: Router) { }

  private _currentApiUrl;
  private headers = new Headers({ 'Content-Type': 'application/json' });
  private pollingInterval = interval(5000);
  private static isBitcoin(coin: string): boolean {
    return coin === 'Bitcoin' || coin === 'TestBitcoin';
  }
  private static isStratis(coin: string): boolean {
    return coin === 'Stratis' || coin === 'TestStratis';
  }
  private getCurrentCoin() {
    const currentCoin = this.globalService.getCoinName();
    if (ApiService.isBitcoin(currentCoin)) {
      this._currentApiUrl = this.bitcoinApiUrl;
    } else if (ApiService.isStratis(currentCoin)) {
      this._currentApiUrl = this.stratisApiUrl;
    }
  }
  get bitcoinApiUrl() {
    return `http://localhost:${this.globalService.bitcoinApiPort}/api`;
  }
  get stratisApiUrl() {
    return `http://localhost:${this.globalService.stratisApiPort}/api`;
  }
  get currentApiUrl() {
    if (!this._currentApiUrl) {
      this._currentApiUrl = `http://localhost:${this.globalService.bitcoinApiPort}/api`;
    }
    return this._currentApiUrl;
  }

  getNodeStatus(silent?: boolean): Observable<NodeStatus> {
    this.getCurrentCoin();
    return this.http.get<NodeStatus>(this._currentApiUrl + '/node/status').pipe(
      catchError(err => this.handleHttpError(err, silent))
    );
  }

  getNodeStatusInterval(): Observable<NodeStatus> {
    this.getCurrentCoin();
    return this.pollingInterval.pipe(
      startWith(0),
      switchMap(() => this.http.get<NodeStatus>(this._currentApiUrl + '/node/status')),
      catchError(err => this.handleHttpError(err))
    )
  }
  /**
   * Gets available wallets at the default path
   */
  getWalletFiles(): Observable<any> {
    return this.http.get(this.bitcoinApiUrl + '/wallet/files').pipe(
      catchError(err => this.handleHttpError(err))
    );
  }
  /**
  * Gets available wallets at the default path
  */
  getStratisWalletFiles(): Observable<any> {
    return this.http.get(this.stratisApiUrl + '/wallet/files').pipe(
      catchError(err => this.handleHttpError(err))
    );
  }
  /**
   * Get a new mnemonic
   */
  getNewMnemonic(): Observable<any> {
    let params = new HttpParams()
      .set('language', 'English')
      .set('wordCount', '12');

    return this.http.get(this._currentApiUrl + '/wallet/mnemonic', { params }).pipe(
      catchError(err => this.handleHttpError(err))
    );
  }
  /**
   * Create a new Bitcoin wallet.
   */
  createBitcoinWallet(data: WalletCreation): Observable<any> {
    return this.http.post(this.bitcoinApiUrl + '/wallet/create/', JSON.stringify(data)).pipe(
      catchError(err => this.handleHttpError(err))
    );
  }
  /**
   * Create a new Stratis wallet.
   */
  createStratisWallet(data: WalletCreation): Observable<any> {
    return this.http.post(this.stratisApiUrl + '/wallet/create/', JSON.stringify(data)).pipe(
      catchError(err => this.handleHttpError(err))
    );
  }
  /**
   * Recover a Bitcoin wallet.
   */
  recoverBitcoinWallet(data: WalletRecovery): Observable<any> {
    return this.http.post(this.bitcoinApiUrl + '/wallet/recover/', JSON.stringify(data)).pipe(
      catchError(err => this.handleHttpError(err))
    );
  }
  /**
   * Recover a Stratis wallet.
   */
  recoverStratisWallet(data: WalletRecovery): Observable<any> {
    return this.http.post(this.stratisApiUrl + '/wallet/recover/', JSON.stringify(data)).pipe(
      catchError(err => this.handleHttpError(err))
    );
  }
  /**
   * Load a Bitcoin wallet
   */
  loadBitcoinWallet(data: WalletLoad): Observable<any> {
    return this.http.post(this.bitcoinApiUrl + '/wallet/load/', JSON.stringify(data)).pipe(
      catchError(err => this.handleHttpError(err))
    );
  }
  /**
   * Load a Stratis wallet
   */
  loadStratisWallet(data: WalletLoad): Observable<any> {
    return this.http.post(this.stratisApiUrl + '/wallet/load/', JSON.stringify(data)).pipe(
      catchError(err => this.handleHttpError(err))
    );
  }
  /**
   * Get wallet status info from the API.
   */
  getWalletStatus(): Observable<any> {
    this.getCurrentCoin();
    return this.http.get(this._currentApiUrl + '/wallet/status').pipe(
      catchError(err => this.handleHttpError(err))
    );
  }
  /**
   * Get general wallet info from the API once.
   */
  getGeneralInfoOnce(data: WalletInfo): Observable<any> {
    let params = new HttpParams().set('Name', data.walletName);
    return this.http.get(this._currentApiUrl + '/wallet/general-info', { params }).pipe(
      catchError(err => this.handleHttpError(err))
    );
  }
  /**
   * Get general wallet info from the API.
   */
  getGeneralInfo(data: WalletInfo): Observable<any> {
    this.getCurrentCoin();
    let params = new HttpParams().set('Name', data.walletName);
    return this.pollingInterval.pipe(
      startWith(0),
      switchMap(() => this.http.get(this._currentApiUrl + '/wallet/general-info', { params })),
      catchError(err => this.handleHttpError(err))
    )
  }
  getGeneralInfoForCoin(data: WalletInfo, coin: string): Observable<any> {
    let url;
    if (ApiService.isBitcoin(coin)) {
      url = this.bitcoinApiUrl;
    } else if (ApiService.isStratis(coin)) {
      url = this.stratisApiUrl;
    } else {
      return Observable.throw(`No such coin '${coin}'`);
    }
    return this.pollingInterval.pipe(
      startWith(0),
      switchMap(() => this.http.get(this._currentApiUrl + '/wallet/general-info', { params })),
      catchError(err => this.handleHttpError(err))
    )
  }
  /**
   * Get wallet balance info from the API.
   */
  getWalletBalance(data: WalletInfo): Observable<any> {
    this.getCurrentCoin();
    let params = new HttpParams()
      .set('walletName', data.walletName)
      .set('accountName', 'account 0');
    return this.pollingInterval.pipe(
      startWith(0),
      switchMap(() => this.http.get(this._currentApiUrl + '/wallet/balance', { params })),
      catchError(err => this.handleHttpError(err))
    )
  }
  /**
   * Get the maximum sendable amount for a given fee from the API
   */
  getMaximumBalance(data): Observable<any> {
    this.getCurrentCoin();
    let params = new HttpParams()
      .set('walletName', data.walletName)
      .set('accountName', "account 0")
      .set('feeType', data.feeType)
      .set('allowUnconfirmed', "true");
    return this.http.get(this._currentApiUrl + '/wallet/maxbalance', { params }).pipe(
      catchError(err => this.handleHttpError(err))
    );
  }
  /**
   * Get a wallets transaction history info from the API.
   */
  getWalletHistory(data: WalletInfo): Observable<any> {
    this.getCurrentCoin();
    let params = new HttpParams()
      .set('walletName', data.walletName)
      .set('accountName', "account 0");
    return this.pollingInterval.pipe(
      startWith(0),
      switchMap(() => this.http.get(this._currentApiUrl + '/wallet/history', { params: params })),
      catchError(err => this.handleHttpError(err))
    )
  }
  /**
   * Get unused receive addresses for a certain wallet from the API.
   */
  getUnusedReceiveAddress(data: WalletInfo): Observable<any> {
    this.getCurrentCoin();
    let params = new HttpParams()
      .set('walletName', data.walletName)
      .set('accountName', "account 0");
    return this.http.get(this._currentApiUrl + '/wallet/unusedaddress', { params }).pipe(
      catchError(err => this.handleHttpError(err))
    );
  }
  /**
   * Estimate the fee of a transaction
   */
  estimateFee(data: FeeEstimation): Observable<any> {
    this.getCurrentCoin();
    let params = new HttpParams()
      .set('walletName', data.walletName)
      .set('accountName', data.accountName)
      .set('recipients[0].destinationAddress', data.recipients[0].destinationAddress)
      .set('recipients[0].amount', data.recipients[0].amount)
      .set('feeType', data.feeType)
      .set('allowUnconfirmed', 'true');
    return this.http.get(this._currentApiUrl + '/wallet/estimate-txfee', { params }).pipe(
      catchError(err => this.handleHttpError(err))
    );
  }
  /**
   * Build a transaction
   */
  buildTransaction(data: TransactionBuilding): Observable<any> {
    this.getCurrentCoin();
    return this.http.post(this._currentApiUrl + '/wallet/build-transaction', JSON.stringify(data)).pipe(
      catchError(err => this.handleHttpError(err))
    );
  }
  /**
   * Send transaction
   */
  sendTransaction(data: TransactionSending): Observable<any> {
    this.getCurrentCoin();
    return this.http.post(this._currentApiUrl + '/wallet/send-transaction', JSON.stringify(data)).pipe(
      catchError(err => this.handleHttpError(err))
    );
  }

  private handleHttpError(error: HttpErrorResponse, silent?: boolean) {
    console.log(error);
    if (error.status === 0) {
      if(!silent) {
        this.modalService.openModal(null, null);
        this.router.navigate(['app']);
      }
    } else if (error.status >= 400) {
      if (!error.error.errors[0].message) {
        console.log(error);
      }
      else {
        this.modalService.openModal(null, error.error.errors[0].message);
      }
    }
    return throwError(error);
  }
}
