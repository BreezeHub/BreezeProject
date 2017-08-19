using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin;
using NBitcoin.RPC;
using NTumbleBit.Services;
using Stratis.Bitcoin.Features.MemoryPool;

namespace Breeze.TumbleBit.Client.Services
{
    public class FullNodeFeeService : IFeeService
    {
        public FullNodeFeeService()
        {
        }

        public FeeRate FallBackFeeRate
        {
            get; set;
        }
        public FeeRate MinimumFeeRate
        {
            get; set;
        }

        public FeeRate GetFeeRate()
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
