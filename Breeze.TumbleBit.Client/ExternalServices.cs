﻿using System;

using Breeze.TumbleBit.Client.Services;
using NBitcoin;
using NTumbleBit;
using NTumbleBit.Services;
using Stratis.Bitcoin;
using Stratis.Bitcoin.Features.MemoryPool;

namespace Breeze.TumbleBit.Client
{
    public class ExternalServices : IExternalServices
    {
        public static ExternalServices CreateUsingFullNode(IRepository repository, Tracker tracker, TumblingState tumblingState)
        {
            FeeRate minimumRate = new FeeRate(MempoolValidator.MinRelayTxFee.FeePerK);

            ExternalServices service = new ExternalServices();
                      
            service.FeeService = new FullNodeFeeService()
            {
                MinimumFeeRate = minimumRate
            };

            // on regtest the estimatefee always fails
            if (tumblingState.TumblerNetwork == Network.RegTest)
            {
                service.FeeService = new FullNodeFeeService()
                {
                    MinimumFeeRate = minimumRate,
                    FallBackFeeRate = new FeeRate(Money.Satoshis(50), 1)
                };
            }

            // TODO: These ultimately need to be brought in from the tumblebit client UI
            string dummyWalletName = "";
            string dummyAccountName = "";

            FullNodeWalletCache cache = new FullNodeWalletCache(repository, tumblingState);
            service.WalletService = new FullNodeWalletService(tumblingState, dummyWalletName, dummyAccountName);
            service.BroadcastService = new FullNodeBroadcastService(cache, repository, tumblingState);
            service.BlockExplorerService = new FullNodeBlockExplorerService(cache, repository, tumblingState);
            service.TrustedBroadcastService = new FullNodeTrustedBroadcastService(service.BroadcastService, service.BlockExplorerService, repository, cache, tracker, tumblingState)
            {
                // BlockExplorer will already track the addresses, since they used a shared bitcoind, no need of tracking again (this would overwrite labels)
                TrackPreviousScriptPubKey = false
            };
            return service;
        }

        public IFeeService FeeService
        {
            get; set;
        }
        public IWalletService WalletService
        {
            get; set;
        }
        public IBroadcastService BroadcastService
        {
            get; set;
        }
        public IBlockExplorerService BlockExplorerService
        {
            get; set;
        }
        public ITrustedBroadcastService TrustedBroadcastService
        {
            get; set;
        }
    }
}
