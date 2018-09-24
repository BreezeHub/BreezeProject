using System;
using System.Linq;
using System.Security;
using System.Threading.Tasks;
using NBitcoin;
using NBitcoin.RPC;
using Newtonsoft.Json.Linq;
using NTumbleBit.Services;
using NTumbleBit.Services.RPC;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Utils = NTumbleBit.Utils;

namespace Breeze.BreezeServer.Features.Masternode.Services.FullNodeBatches
{
	public class FundingBatch : BatchBase<Recipient, Transaction>
	{
	    private Wallet tumblerWallet;
	    private SecureString tumblerWalletPassword;
	    private IWalletTransactionHandler walletTransactionHandler;
	    private IWalletService walletService;

        public FundingBatch(IWalletService walletService, IWalletTransactionHandler walletTransactionHandler, Wallet tumblerWallet, SecureString tumblerWalletPassword)
        {
            this.walletService = walletService;
            this.tumblerWallet = tumblerWallet;
            this.tumblerWalletPassword = tumblerWalletPassword;
            this.walletTransactionHandler = walletTransactionHandler;
        }

		public FeeRate FeeRate
		{
			get; set;
		}

		protected override async Task<Transaction> RunAsync(Recipient[] data)
		{
		    return await Task.Run(() =>
		    {
		        try
		        {
		            var walletReference = new WalletAccountReference()
		            {
		                // Currently only the first wallet account is used
		                AccountName = tumblerWallet.GetAccountsByCoinType((CoinType) tumblerWallet.Network.Consensus.CoinType)
		                    .First().Name,
		                WalletName = tumblerWallet.Name
		            };

		            var context = new TransactionBuildContext(
		                walletReference,
		                data.ToList(), tumblerWalletPassword.ToString())
		            {
		                // To avoid using un-propagated (and hence unconfirmed) transactions we require at least 1 confirmation.
		                // If a transaction is somehow invalid, all transactions using it as an input are invalidated. This
		                // tries to guard against that when using a light wallet, which has no ability to correct it.
		                MinConfirmations = 1,
		                OverrideFeeRate = FeeRate,
		                Sign = true,
		                Shuffle = true
		            };

		            return walletTransactionHandler.BuildTransaction(context);
		        }
		        catch (Exception ex)
		        {
                    var balance = walletService.GetBalance(tumblerWallet.Name);
		            var needed = data.Select(r => r.Amount).Sum() + FeeRate.GetFee(2000);
		            var missing = needed - balance;
		            if (missing > Money.Zero)
		                throw new NotEnoughFundsException("Not enough funds", "", missing);
		            throw;
                }

            }).ConfigureAwait(false);

		}
	}
}
