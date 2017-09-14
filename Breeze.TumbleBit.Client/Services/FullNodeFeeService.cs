using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin;
using NTumbleBit.Services;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.Wallet;

namespace Breeze.TumbleBit.Client.Services
{
    public class FullNodeFeeService : IFeeService
    {
        public FeeRate FallBackFeeRate { get; set; }
        public FeeRate MinimumFeeRate { get; set; }

        private IWalletFeePolicy WalletFeePolicy { get; }

        public FullNodeFeeService(IWalletFeePolicy walletFeePolicy)
        {
            WalletFeePolicy = walletFeePolicy ?? throw new ArgumentNullException(nameof(walletFeePolicy));
        }

        private FeeRate cachedValue;
        private DateTimeOffset cachedValueTime;
        private TimeSpan cacheExpiration = TimeSpan.FromSeconds(60 * 5);
        public async Task<FeeRate> GetFeeRateAsync()
        {
            if (DateTimeOffset.UtcNow - this.cachedValueTime > this.cacheExpiration)
            {
                var rate = await FetchRateAsync();
                this.cachedValue = rate;
                this.cachedValueTime = DateTimeOffset.UtcNow;
                return rate;
            }
            else
            {
                return this.cachedValue;
            }
        }

        private async Task<FeeRate> FetchRateAsync()
        {
            return await Task.Run(() =>
            {
                var rate = WalletFeePolicy.GetFeeRate(1) ??
                           WalletFeePolicy.GetFeeRate(2) ??
                           WalletFeePolicy.GetFeeRate(3) ??
                           FallBackFeeRate;
                if (rate == null)
                    throw new FeeRateUnavailableException("The fee rate is unavailable");
                if (rate < MinimumFeeRate)
                    rate = MinimumFeeRate;
                return rate;
            }).ConfigureAwait(false);
        }
    }
}
