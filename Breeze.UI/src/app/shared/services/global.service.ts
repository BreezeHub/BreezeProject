import { Injectable } from '@angular/core';
import { ElectronService } from 'ngx-electron';

@Injectable({
  providedIn: 'root'
})
export class GlobalService {
  private applicationVersion: string = "1.1.0";
  private walletPath: string;
  private activeCoin: string = "Bitcoin";
  private currentWalletName: string;
  private coinUnit: string;
  private network: string;
  private bitcoinApiPort: number;
  private stratisApiPort: number;
  private testnet: boolean = false;

  constructor(private electronService: ElectronService) {
    this.setApplicationVersion();
    this.setTestnetEnabled();
    this.setBitcoinApiPort();
    this.setStratisApiPort();
  }

  getApplicationVersion() {
    return this.applicationVersion;
  }

  setApplicationVersion() {
    if (this.electronService.isElectronApp) {
      this.applicationVersion = this.electronService.remote.app.getVersion();
    }
  }

  getActiveCoin() {
    return this.activeCoin;
  }

  setActiveCoin(activeCoin: string) {
    this.activeCoin = activeCoin;
  }

  getTestnetEnabled() {
    return this.testnet;
  }

  setTestnetEnabled() {
    if (this.electronService.isElectronApp) {
      this.testnet = this.electronService.ipcRenderer.sendSync('get-testnet');
    }
  }

  getBitcoinApiPort() {
    return this.bitcoinApiPort;
  }

  setBitcoinApiPort() {
    this.bitcoinApiPort = this.electronService.remote.getGlobal('bitcoinApiPort');
  }

  getStratisApiPort() {
    return this.stratisApiPort;
  }

  setStratisApiPort() {
    this.stratisApiPort = this.electronService.remote.getGlobal('stratisApiPort');
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

  getCoinUnit() {
    return this.coinUnit;
  }

  setCoinUnit(coinUnit: string) {
    this.coinUnit = coinUnit;
  }
}
