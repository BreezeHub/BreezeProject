using NBitcoin.RPC;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin;
using Newtonsoft.Json.Linq;
using NTumbleBit.ClassicTumbler.Client;
using NTumbleBit.Logging;
using Stratis.Bitcoin.Features.Wallet;

namespace Breeze.TumbleBit.Client
{
    public class FullNodeDestinationWallet : IDestinationWallet
    {
        private TumblingState tumblingState;
        public FullNodeDestinationWallet(TumblingState tumblingState)
        {
            if (tumblingState == null)
                throw new ArgumentNullException("tumblingState");
            this.tumblingState = tumblingState;
        }

        public KeyPath GetKeyPath(Script script)
        {
            var address = script.GetDestinationAddress(this.tumblingState.TumblerNetwork);
            if (address == null)
                return null;

            foreach (var account in this.tumblingState.WalletManager.GetAccounts(this.tumblingState.DestinationWalletName))
            {
                foreach (var hdAddress in account.GetCombinedAddresses())
                {
                    if (address.ToString() == hdAddress.Address)
                    {
                        var path = new KeyPath(hdAddress.HdPath);
                        Logs.Wallet.LogInformation($"Created address {address} with HD path {path}");
                        return path;
                    }
                }
            }

            return null;
        }

        public Script GetNewDestination()
        {
            Wallet wallet = this.tumblingState.WalletManager.GetWallet(this.tumblingState.DestinationWalletName);

            // TODO: Ideally need some mechanism for ensuring the same account is always used
            foreach (var account in wallet.GetAccountsByCoinType(this.tumblingState.CoinType))
            {
                // Iterate through accounts until unused address is found
                HdAddress hdAddress = account.GetFirstUnusedReceivingAddress();
                return hdAddress.ScriptPubKey;
            }

            // This shouldn't happen
            return null;
        }
    }
}
