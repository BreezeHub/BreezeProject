using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin;
using NBitcoin.RPC;
using Newtonsoft.Json.Linq;
using NTumbleBit.PuzzlePromise;
using NBitcoin.DataEncoders;
using NTumbleBit.Services;
using Stratis.Bitcoin;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.WatchOnlyWallet;

namespace Breeze.TumbleBit.Client.Services
{
    public class FullNodeWalletService : IWalletService
    {
        private TumblingState tumblingState;
        private string walletName;
        private string accountName;

        public FullNodeWalletService(TumblingState tumblingState, string walletName, string accountName)
        {
            this.tumblingState = tumblingState;
            this.walletName = walletName;
            this.accountName = accountName;
        }

        public IDestination GenerateAddress()
        {
            Wallet wallet = this.tumblingState.walletManager.GetWallet(walletName);

            HdAddress hdAddress = null;
            BitcoinAddress address = null;

            foreach (var account in wallet.GetAccountsByCoinType(this.tumblingState.coinType))
            {
                // Iterate through accounts until unused address is found
                hdAddress = account.GetFirstUnusedReceivingAddress();
                address = BitcoinAddress.Create(hdAddress.Address, wallet.Network);
                if (address != null)
                    return address;
            }

            return null;
        }

        public Coin AsCoin(UnspentCoin c)
        {
            var coin = new Coin(c.OutPoint, new TxOut(c.Amount, c.ScriptPubKey));
            if (c.RedeemScript != null)
                coin = coin.ToScriptCoin(c.RedeemScript);
            return coin;
        }

        public Transaction FundTransaction(TxOut txOut, FeeRate feeRate)
        {
            Transaction tx = new Transaction();
            tx.Outputs.Add(txOut);

            WalletAccountReference accountRef = new WalletAccountReference(this.walletName, this.accountName);

            List<Recipient> recipients = new List<Recipient>();
            Recipient recipient = new Recipient()
            {
                ScriptPubKey = txOut.ScriptPubKey,
                Amount = txOut.Value
            };

            recipients.Add(recipient);

            var txBuildContext = new TransactionBuildContext(accountRef, recipients);
            txBuildContext.OverrideFeeRate = feeRate;
            txBuildContext.Sign = true;

            // FundTransaction modifies tx directly
            this.tumblingState.walletTransactionHandler.FundTransaction(txBuildContext, tx);

            return tx;
        }
    }
}
