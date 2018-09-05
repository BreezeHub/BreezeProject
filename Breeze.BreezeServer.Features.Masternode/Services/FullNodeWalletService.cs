using System;
using System.Linq;
using System.Threading.Tasks;
using Breeze.BreezeServer.Features.Masternode.Services.FullNodeBatches;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NTumbleBit;
using NTumbleBit.Logging;
using NTumbleBit.Services;
using NTumbleBit.Services.RPC;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Interfaces;

namespace Breeze.BreezeServer.Features.Masternode.Services
{
    class ClientEscapeData
    {
        public ScriptCoin EscrowedCoin { get; set; }
        public TransactionSignature ClientSignature { get; set; }
        public Key EscrowKey { get; set; }
    }

    public class FullNodeWalletService : IWalletService
    {
        private Wallet tumblerWallet { get; }
        private string tumblerWalletPassword { get; }
        private IWalletTransactionHandler walletTransactionHandler;
        private IWalletManager walletManager { get; }
        private IBroadcasterManager broadcasterManager;

        private ReceiveBatch ReceiveBatch;
        private FundingBatch FundingBatch;

        public TimeSpan BatchInterval
        {
            get
            {
                return FundingBatch.BatchInterval;
            }
            set
            {
                FundingBatch.BatchInterval = value;
                ReceiveBatch.BatchInterval = value;
            }
        }

        public FullNodeWalletService(Wallet tumblerWallet, string tumblerWalletPassword, IWalletTransactionHandler walletTransactionHandler, IBroadcasterManager broadcasterManager, IWalletManager walletManager)
        {
            this.tumblerWallet = tumblerWallet ?? throw new ArgumentNullException(nameof(tumblerWallet));
            this.tumblerWalletPassword = tumblerWalletPassword ?? throw new ArgumentNullException(nameof(tumblerWalletPassword));
            this.walletTransactionHandler = walletTransactionHandler;
            this.walletManager = walletManager;
            this.broadcasterManager = broadcasterManager;

            FundingBatch = new FundingBatch(walletTransactionHandler, tumblerWallet, tumblerWalletPassword);
            ReceiveBatch = new ReceiveBatch();
            BatchInterval = TimeSpan.Zero;
        }

        public async Task<IDestination> GenerateAddressAsync()
        {
            return await Task.Run(() =>
            {
                // ToDo: Implement SegWit to Breeze
                // NTumbleBit uses SegWit when running over RPC
                // Breeze does not yet
                // https://github.com/BreezeHub/Breeze/issues/10

                var accounts = tumblerWallet.GetAccountsByCoinType((CoinType)tumblerWallet.Network.Consensus.CoinType);
                var account = accounts.First(); // In Breeze at this point only the first account is used
                var addressString = account.GetFirstUnusedReceivingAddress().Address;
                return BitcoinAddress.Create(addressString, tumblerWallet.Network);
            }).ConfigureAwait(false);
        }

        public async Task<Transaction> FundTransactionAsync(TxOut txOut, FeeRate feeRate)
        {
            FundingBatch.FeeRate = feeRate;

            Recipient recipient = new Recipient {Amount = txOut.Value, ScriptPubKey = txOut.ScriptPubKey};
            var task = FundingBatch.WaitTransactionAsync(recipient).ConfigureAwait(false);
            Logs.Tumbler.LogDebug($"FundingBatch batch count {FundingBatch.BatchCount}");

            return await task;
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

            await this.broadcasterManager.BroadcastTransactionAsync(tx).ConfigureAwait(false);
            var bcResult = this.broadcasterManager.GetTransaction(tx.GetHash()).State;
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
                        var transactionEntry = this.broadcasterManager.GetTransaction(tx.GetHash());
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
				walletName = this.tumblerWallet.Name;
			var unspentOutputs = this.walletManager.GetSpendableTransactionsInWallet(walletName);

		    return new Money(unspentOutputs.Sum(s => s.Transaction.Amount));
	    }
	}
}
