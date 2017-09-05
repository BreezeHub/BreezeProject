using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin;
using NTumbleBit.Services;
using Stratis.Bitcoin.Features.MemoryPool;

namespace Breeze.TumbleBit.Client.Services
{
    public class FullNodeFeeService : IFeeService
    {
        public FeeRate FallBackFeeRate
        {
            get; set;
        }
        public FeeRate MinimumFeeRate
        {
            get; set;
        }

        FeeRate _CachedValue;
        DateTimeOffset _CachedValueTime;
        TimeSpan CacheExpiration = TimeSpan.FromSeconds(60 * 5);
        public async Task<FeeRate> GetFeeRateAsync()
        {
            if (DateTimeOffset.UtcNow - _CachedValueTime > CacheExpiration)
            {
                var rate = await FetchRateAsync();
                _CachedValue = rate;
                _CachedValueTime = DateTimeOffset.UtcNow;
                return rate;
            }
            else
            {
                return _CachedValue;
            }
        }

        private async Task<FeeRate> FetchRateAsync()
        {
            decimal relayFee = MempoolValidator.MinRelayTxFee.FeePerK.ToUnit(MoneyUnit.BTC);
            var minimumRate = new FeeRate(Money.Coins(relayFee * 2), 1000); //0.00002000 BTC/kB
            var fallbackFeeRate = new FeeRate(Money.Satoshis(50), 1);       //0.00050000 BTC/kB

            // TODO add real fee estimation 
            //var rate = _RPCClient.TryEstimateFeeRate(1) ??
            //           _RPCClient.TryEstimateFeeRate(2) ??
            //           _RPCClient.TryEstimateFeeRate(3) ??
            //           FallBackFeeRate;

            //if (rate < MinimumFeeRate)
            //    rate = MinimumFeeRa

            return fallbackFeeRate;
        }
    }
}
