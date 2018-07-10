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
                // ToDo: Implement SegWit to Breeze
                // NTumbleBit uses SegWit
                // Breeze does not yet
                // https://github.com/BreezeHub/Breeze/issues/10

                var accounts = TumblingState.OriginWallet.GetAccountsByCoinType((CoinType)TumblingState.OriginWallet.Network.Consensus.CoinType);
                var account = accounts.First(); // In Breeze at this point only the first account is used
                var addressString = account.GetFirstUnusedReceivingAddress().Address;
                return BitcoinAddress.Create(addressString);
            }).ConfigureAwait(false);
        }

        public async Task<Transaction> FundTransactionAsync(TxOut txOut, FeeRate feeRate)
        {
            return await Task.Run(() =>
            {
                var walletReference = new WalletAccountReference()
                {
                    // Currently on the first wallet account is used in Breeze
                    AccountName = TumblingState.OriginWallet.GetAccountsByCoinType((CoinType)TumblingState.OriginWallet.Network.Consensus.CoinType).First().Name,
                    WalletName = TumblingState.OriginWallet.Name
                };

                var context = new TransactionBuildContext(
                    walletReference,
                    new[]
                    {
                    new Recipient { Amount = txOut.Value, ScriptPubKey = txOut.ScriptPubKey },
                    }
                    .ToList(), TumblingState.OriginWalletPassword)
                {
                    // To avoid using un-propagated (and hence unconfirmed) transactions we require at least 1 confirmation.
                    // If a transaction is somehow invalid, all transactions using it as an input are invalidated. This
                    // tries to guard against that when using a light wallet, which has no ability to correct it.
                    MinConfirmations = 1,
                    OverrideFeeRate = feeRate,
                    Sign = true
                };

                return TumblingState.WalletTransactionHandler.BuildTransaction(context);
            }).ConfigureAwait(false);
        }

        // This interface implementation method is only used by the Tumbler server
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

            //LogDebug("Trying to broadcast transaction: " + tx.GetHash());

            await this.TumblingState.BroadcasterManager.BroadcastTransactionAsync(tx).ConfigureAwait(false);
            var bcResult = TumblingState.BroadcasterManager.GetTransaction(tx.GetHash()).State;
            switch (bcResult)
            {
                case Stratis.Bitcoin.Broadcasting.State.Broadcasted:
                case Stratis.Bitcoin.Broadcasting.State.Propagated:
                    //LogDebug("Broadcasted transaction: " + tx.GetHash());
                    break;
                case Stratis.Bitcoin.Broadcasting.State.ToBroadcast:
                    // Wait for propagation
                    var waited = TimeSpan.Zero;
                    var period = TimeSpan.FromSeconds(1);
                    while (TimeSpan.FromSeconds(21) > waited)
                    {
                        // Check BroadcasterManager for broadcast success
                        var transactionEntry = this.TumblingState.BroadcasterManager.GetTransaction(tx.GetHash());
                        if (transactionEntry != null && transactionEntry.State == Stratis.Bitcoin.Broadcasting.State.Propagated)
                        {
                            //LogDebug("Propagated transaction: " + tx.GetHash());
                        }
                        await Task.Delay(period).ConfigureAwait(false);
                        waited += period;
                    }
                    break;
                case Stratis.Bitcoin.Broadcasting.State.CantBroadcast:
                    // Do nothing
                    break;
            }
                
            //LogDebug("Uncertain if transaction was propagated: " + tx.GetHash());

            return tx;
        }

	    /// <summary>
	    /// Retrieves the remaining unspent balance in the origin wallet. Includes unconfirmed transactions.
	    /// </summary>
	    public Money GetBalance(string walletName = null)
	    {
			if (walletName == null)
				walletName = this.TumblingState.OriginWalletName;
			var unspentOutputs = this.TumblingState.WalletManager.GetSpendableTransactionsInWallet(walletName);

		    return new Money(unspentOutputs.Sum(s => s.Transaction.Amount));
	    }
	}
}
