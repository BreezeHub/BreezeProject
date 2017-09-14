using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.ComTypes;
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
using NTumbleBit;

namespace Breeze.TumbleBit.Client.Services
{
    class ClientEscapeData
    {
        public ScriptCoin EscrowedCoin { get; set; }
        public TransactionSignature ClientSignature { get; set; }
        public Key EscrowKey { get; set; }
    }

    public class FullNodeWalletService : IWalletService
    {
        private TumblingState TumblingState { get; }

        public FullNodeWalletService(TumblingState tumblingState)
        {
            TumblingState = tumblingState ?? throw new ArgumentNullException(nameof(tumblingState));
        }

        public async Task<IDestination> GenerateAddressAsync()
        {
            return await Task.Run(() =>
            {
                // TODO: Equivalent of addwitnessaddress rpc?

                Wallet wallet = this.TumblingState.WalletManager.GetWallet(this.TumblingState.OriginWalletName);

                HdAddress hdAddress = null;
                BitcoinAddress address = null;

                foreach (var account in wallet.GetAccountsByCoinType(this.TumblingState.CoinType))
                {
                    // Iterate through accounts until unused address is found
                    hdAddress = account.GetFirstUnusedReceivingAddress();
                    address = BitcoinAddress.Create(hdAddress.Address, wallet.Network);
                    if (address != null)
                        return address;
                }

                return null;
            }).ConfigureAwait(false);
        }

        public Coin AsCoin(UnspentCoin c)
        {
            var coin = new Coin(c.OutPoint, new TxOut(c.Amount, c.ScriptPubKey));
            if (c.RedeemScript != null)
                coin = coin.ToScriptCoin(c.RedeemScript);
            return coin;
        }

        public async Task<Transaction> FundTransactionAsync(TxOut txOut, FeeRate feeRate)
        {
            return await Task.Run(() =>
            {
                Transaction tx = new Transaction();
                tx.Outputs.Add(txOut);

                var spendable = this.TumblingState.WalletManager.GetSpendableTransactionsInWallet(this.TumblingState.OriginWalletName);

                Dictionary<HdAccount, Money> accountBalances = new Dictionary<HdAccount, Money>();

                // If multiple accounts are available find and choose the one with the most spendable funds
                foreach (var spendableTx in spendable)
                {
                    accountBalances[spendableTx.Account] = spendableTx.Transaction.SpendableAmount(false);
                }

                HdAccount highestBalance = null;
                foreach (var accountKey in accountBalances.Keys)
                {
                    if (highestBalance == null)
                    {
                        highestBalance = accountKey;
                        continue;
                    }

                    if (accountBalances[accountKey] > accountBalances[highestBalance])
                    {
                        highestBalance = accountKey;
                    }
                }

                WalletAccountReference accountRef = new WalletAccountReference(this.TumblingState.OriginWalletName, highestBalance.Name);

                List<Recipient> recipients = new List<Recipient>();

                var txBuildContext = new TransactionBuildContext(accountRef, recipients);
                txBuildContext.WalletPassword = this.TumblingState.OriginWalletPassword;
                txBuildContext.OverrideFeeRate = feeRate;
                txBuildContext.Sign = true;
                txBuildContext.MinConfirmations = 0;

                // FundTransaction modifies tx directly
                this.TumblingState.WalletTransactionHandler.FundTransaction(txBuildContext, tx);

                return tx;
            }).ConfigureAwait(false);
        }

        public async Task<Transaction> ReceiveAsync(ScriptCoin escrowedCoin, TransactionSignature clientSignature, Key escrowKey, FeeRate feeRate)
        {
            var input = new ClientEscapeData()
            {
                ClientSignature = clientSignature,
                EscrowedCoin = escrowedCoin,
                EscrowKey = escrowKey
            };

            var cashout = await GenerateAddressAsync();
            var tx = new Transaction();

            // Note placeholders - this step is performed again further on
            var txin = new TxIn(input.EscrowedCoin.Outpoint);
            txin.ScriptSig = new Script(
            Op.GetPushOp(TrustedBroadcastRequest.PlaceholderSignature),
            Op.GetPushOp(TrustedBroadcastRequest.PlaceholderSignature),
            Op.GetPushOp(input.EscrowedCoin.Redeem.ToBytes())
            );
            txin.Witnessify();
            tx.AddInput(txin);

            tx.Outputs.Add(new TxOut()
            {
                ScriptPubKey = cashout.ScriptPubKey,
                Value = input.EscrowedCoin.Amount
            });

            ScriptCoin[] coinArray = { input.EscrowedCoin };

            var currentFee = tx.GetFee(coinArray);
            tx.Outputs[0].Value -= feeRate.GetFee(tx) - currentFee;

            var txin2 = tx.Inputs[0];
            var signature = tx.SignInput(input.EscrowKey, input.EscrowedCoin);
            txin2.ScriptSig = new Script(
            Op.GetPushOp(input.ClientSignature.ToBytes()),
            Op.GetPushOp(signature.ToBytes()),
            Op.GetPushOp(input.EscrowedCoin.Redeem.ToBytes())
            );
            txin2.Witnessify();

            this.TumblingState.WalletManager.SendTransaction(tx.ToHex());

            return tx;
        }
    }
}
