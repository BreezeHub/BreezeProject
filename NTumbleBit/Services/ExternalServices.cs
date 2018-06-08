﻿using NBitcoin;
using NBitcoin.RPC;
using NTumbleBit.Services.RPC;
using System;

namespace NTumbleBit.Services
{
    public class ExternalServices : IExternalServices
    {
        public static ExternalServices CreateFromRPCClient(RPCClient rpc, IRepository repository, Tracker tracker,
            bool useBatching)
        {
            var info = rpc.SendCommand(RPCOperations.getinfo);
            var minimumRate =
                new NBitcoin.FeeRate(
                    NBitcoin.Money.Coins(
                        (decimal) (double) ((Newtonsoft.Json.Linq.JValue) (info.Result["relayfee"])).Value * 2), 1000);

            ExternalServices service = new ExternalServices();
            service.FeeService = new RPCFeeService(rpc)
            {
                MinimumFeeRate = minimumRate
            };

            // on regtest or testnet the estimatefee often fails
            if (rpc.Network == NBitcoin.Network.RegTest || rpc.Network == Network.TestNet)
            {
                service.FeeService = new RPCFeeService(rpc)
                {
                    MinimumFeeRate = minimumRate,
                    FallBackFeeRate = new NBitcoin.FeeRate(NBitcoin.Money.Satoshis(50), 1)
                };
            }

            var cache = new RPCWalletCache(rpc, repository);

            var clientBatchInterval = TimeSpan.FromMilliseconds(100);
            if (rpc.Network != NBitcoin.Network.RegTest)
            {
                service.WalletService = new RPCWalletService(rpc)
                {
                    BatchInterval = useBatching ? TimeSpan.FromSeconds(160) : clientBatchInterval,
                    AddressGenerationBatchInterval =
                        useBatching ? TimeSpan.FromSeconds(1) : TimeSpan.FromMilliseconds(10)
                };

                service.BroadcastService = new RPCBroadcastService(rpc, cache, repository)
                {
                    BatchInterval = useBatching ? TimeSpan.FromSeconds(5) : clientBatchInterval
                };
                service.BlockExplorerService = new RPCBlockExplorerService(rpc, cache, repository)
                {
                    BatchInterval = useBatching ? TimeSpan.FromSeconds(5) : clientBatchInterval
                };
                service.TrustedBroadcastService = new RPCTrustedBroadcastService(rpc, service.BroadcastService,
                    service.BlockExplorerService, repository, cache, tracker, rpc.Network)
                {
                    //BlockExplorer will already track the addresses, since they used a shared bitcoind, no need of tracking again (this would overwrite labels)
                    TrackPreviousScriptPubKey = false
                };
            }
            else
            {
                // For integration tests on regtest the batching needs to be almost nonexistent due to the low
                // inter-block delays

                service.WalletService = new RPCWalletService(rpc)
                {
                    BatchInterval = useBatching ? TimeSpan.FromSeconds(1) : clientBatchInterval,
                    AddressGenerationBatchInterval =
                        useBatching ? TimeSpan.FromSeconds(1) : TimeSpan.FromMilliseconds(10)
                };

                service.BroadcastService = new RPCBroadcastService(rpc, cache, repository)
                {
                    BatchInterval = useBatching ? TimeSpan.FromSeconds(1) : clientBatchInterval
                };
                service.BlockExplorerService = new RPCBlockExplorerService(rpc, cache, repository)
                {
                    BatchInterval = useBatching ? TimeSpan.FromSeconds(1) : clientBatchInterval
                };
                service.TrustedBroadcastService = new RPCTrustedBroadcastService(rpc, service.BroadcastService,
                    service.BlockExplorerService, repository, cache, tracker, rpc.Network)
                {
                    //BlockExplorer will already track the addresses, since they used a shared bitcoind, no need of tracking again (this would overwrite labels)
                    TrackPreviousScriptPubKey = false
                };
            }

            return service;
        }

        public IFeeService FeeService { get; set; }
        public IWalletService WalletService { get; set; }
        public IBroadcastService BroadcastService { get; set; }
        public IBlockExplorerService BlockExplorerService { get; set; }
        public ITrustedBroadcastService TrustedBroadcastService { get; set; }
    }
}