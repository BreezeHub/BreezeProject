﻿using System;

using Breeze.TumbleBit.Client.Services;
using NBitcoin;
using NTumbleBit;
using NTumbleBit.Services;
using Stratis.Bitcoin;

namespace Breeze.TumbleBit.Client
{
    public class ExternalServices : IExternalServices
    {
        public IFeeService FeeService { get; set; }
        public IWalletService WalletService { get; set; }
        public IBroadcastService BroadcastService { get; set; }
        public IBlockExplorerService BlockExplorerService { get; set; }
        public ITrustedBroadcastService TrustedBroadcastService { get; set; }

        public static ExternalServices CreateFromFullNode(IRepository repository, Tracker tracker, TumblingState tumblingState)
        {
            var minimumRate = tumblingState.NodeSettings.MinRelayTxFeeRate;

            var service = new ExternalServices();

            // On regtest the estimatefee always fails
            if (tumblingState.TumblerNetwork == Network.RegTest || tumblingState.TumblerNetwork == Network.StratisRegTest)
            {
                if (minimumRate == FeeRate.Zero)
                    minimumRate = new FeeRate(Money.Satoshis(1500));

                service.FeeService = new FullNodeFeeService(tumblingState.WalletFeePolicy)
                {
                    MinimumFeeRate = minimumRate,
                    FallBackFeeRate = new FeeRate(Money.Satoshis(50), 1)
                };
            }
            else // On test and mainnet fee estimation should just fail, not fall back to fixed fee
            {
                service.FeeService = new FullNodeFeeService(tumblingState.WalletFeePolicy)
                {
                    MinimumFeeRate = minimumRate
                };
            }

            var cache = new FullNodeWalletCache(tumblingState);
            service.WalletService = new FullNodeWalletService(tumblingState);
            service.BroadcastService = new FullNodeBroadcastService(cache, repository, tumblingState);
            service.BlockExplorerService = new FullNodeBlockExplorerService(cache, tumblingState);
            service.TrustedBroadcastService = new FullNodeTrustedBroadcastService(service.BroadcastService, service.BlockExplorerService, repository, cache, tracker, tumblingState)
            {
                // BlockExplorer will already track the addresses, since they used a shared bitcoind, no need of tracking again (this would overwrite labels)
                TrackPreviousScriptPubKey = false
            };
            return service;
        }
    }
}
