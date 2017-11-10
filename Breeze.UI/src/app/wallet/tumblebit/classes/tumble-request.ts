export class TumbleRequest {

    constructor(originWalletName: string, destinationWalletName: string, originWalletPassword: string) {
      this.OriginWalletName = originWalletName;
      this.DestinationWalletName = destinationWalletName;
      this.OriginWalletPassword = originWalletPassword;
    }

    OriginWalletName: string;
    DestinationWalletName: string;
    OriginWalletPassword: string;
  }
