import {Injectable} from '@angular/core';

@Injectable()
export class GlobalService {
  private currentBitcoinApiPort = 37220;
  private currentStratisApiPort = 37221;
  private walletPath: string;
  private currentWalletName: string;
  private coinType: number;
  private coinName: string;
  private coinUnit: string;
  private network: string;

  constructor() {}

  get bitcoinApiPort() {
    return this.currentBitcoinApiPort;
  }

  get stratisApiPort() {
    return this.currentStratisApiPort;
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
    if (!!network && network.toLowerCase() === 'testnet') {
      this.currentBitcoinApiPort = 38220;
      this.currentStratisApiPort = 38221;
    } else {
      this.currentBitcoinApiPort = 37220;
      this.currentStratisApiPort = 37221;
    }
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

  setCoinType (coinType: number) {
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
