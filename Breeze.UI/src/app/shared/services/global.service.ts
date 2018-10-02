import { Injectable } from '@angular/core';
import { remote } from 'electron';

@Injectable()
export class GlobalService {
  private walletPath: string;
  private currentWalletName: string;
  private coinType: number;
  private coinName: string;
  private coinUnit: string;
  private network: string;

  constructor() {
  }

  get bitcoinApiPort() {
    return remote.getGlobal('bitcoinApiPort');
  }

  get stratisApiPort() {
    return remote.getGlobal('stratisApiPort');
  }

  get masternodeMode(): boolean {
    return remote.getGlobal('masternodeMode');
  }

  getWalletPath() {
    return this.walletPath;
  }

  setWalletPath(walletPath: string) {
    this.walletPath = walletPath;
  }

  getNetwork() {
    return this.network;
  }

  setNetwork(network: string) {
    this.network = network;
  }

  getWalletName() {
    return this.currentWalletName;
  }

  setWalletName(currentWalletName: string) {
    this.currentWalletName = currentWalletName;
  }

  getCoinType() {
    return this.coinType;
  }

  setCoinType(coinType: number) {
    this.coinType = coinType;
  }

  getCoinName() {
    return this.coinName;
  }

  setCoinName(coinName: string) {
    this.coinName = coinName;
  }

  getCoinUnit() {
    return this.coinUnit;
  }

  setCoinUnit(coinUnit: string) {
    this.coinUnit = coinUnit;
  }
}
