using System;
using System.Linq;
using System.Security;
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
    public class FullNodeWalletService : IWalletService
    {
        private Wallet tumblerWallet { get; }
        private SecureString tumblerWalletPassword { get; }
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

        public FullNodeWalletService(Wallet tumblerWallet, SecureString tumblerWalletPassword, IWalletTransactionHandler walletTransactionHandler, IBroadcasterManager broadcasterManager, IWalletManager walletManager)
        {
            this.tumblerWallet = tumblerWallet ?? throw new ArgumentNullException(nameof(tumblerWallet));
            this.tumblerWalletPassword = tumblerWalletPassword ?? throw new ArgumentNullException(nameof(tumblerWalletPassword));
            this.walletTransactionHandler = walletTransactionHandler;
            this.walletManager = walletManager;
            this.broadcasterManager = broadcasterManager;

            FundingBatch = new FundingBatch(this, walletTransactionHandler, tumblerWallet, tumblerWalletPassword);
            ReceiveBatch = new ReceiveBatch(this);
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
            ReceiveBatch.FeeRate = feeRate;
            var task = ReceiveBatch.WaitTransactionAsync(new ClientEscapeData()
            {
                ClientSignature = clientSignature,
                EscrowedCoin = escrowedCoin,
                EscrowKey = escrowKey
            }).ConfigureAwait(false);

            Logs.Tumbler.LogDebug($"ReceiveBatch batch count {ReceiveBatch.BatchCount}");
            return await task;
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
