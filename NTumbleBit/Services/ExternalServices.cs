using NBitcoin;
using NBitcoin.RPC;
using NTumbleBit.Services.RPC;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace NTumbleBit.Services
{
	public class ExternalServices : IExternalServices
    {
        public IFeeService FeeService { get; set; }
        public IWalletService WalletService { get; set; }
        public IBroadcastService BroadcastService { get; set; }
        public IBlockExplorerService BlockExplorerService { get; set; }
        public ITrustedBroadcastService TrustedBroadcastService { get; set; }

        /*
        public static ExternalServices CreateFromFullNode(IRepository repository, Tracker tracker)
        {
            var service = new ExternalServices();

            var minimumRate = tumblingState.NodeSettings.MinRelayTxFeeRate;
            // On regtest the estimatefee always fails
            if (tumblingState.TumblerNetwork == Network.RegTest)
            {
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
        }*/


        public static ExternalServices CreateFromRPCClient(RPCClient rpc, IRepository repository, Tracker tracker, bool useBatching)
		{
			var info = rpc.SendCommand(RPCOperations.getinfo);

		    JToken relayFee = info.Result["relayfee"] ?? info.Result["mininput"];
            var minimumRate = new NBitcoin.FeeRate(NBitcoin.Money.Coins((decimal)(double)((Newtonsoft.Json.Linq.JValue)(relayFee)).Value * 2), 1000);
			
			ExternalServices service = new ExternalServices();
			service.FeeService = new RPCFeeService(rpc) {
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
					AddressGenerationBatchInterval = useBatching ? TimeSpan.FromSeconds(1) : TimeSpan.FromMilliseconds(10)
				};
			
				service.BroadcastService = new RPCBroadcastService(rpc, cache, repository)
				{
					BatchInterval = useBatching ? TimeSpan.FromSeconds(5) : clientBatchInterval
				};
				service.BlockExplorerService = new RPCBlockExplorerService(rpc, cache, repository)
				{
					BatchInterval = useBatching ? TimeSpan.FromSeconds(5) : clientBatchInterval
				};
				service.TrustedBroadcastService = new RPCTrustedBroadcastService(rpc, service.BroadcastService, service.BlockExplorerService, repository, cache, tracker)
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
					AddressGenerationBatchInterval = useBatching ? TimeSpan.FromSeconds(1) : TimeSpan.FromMilliseconds(10)
				};
			
				service.BroadcastService = new RPCBroadcastService(rpc, cache, repository)
				{
					BatchInterval = useBatching ? TimeSpan.FromSeconds(1) : clientBatchInterval
				};
				service.BlockExplorerService = new RPCBlockExplorerService(rpc, cache, repository)
				{
					BatchInterval = useBatching ? TimeSpan.FromSeconds(1) : clientBatchInterval
				};
				service.TrustedBroadcastService = new RPCTrustedBroadcastService(rpc, service.BroadcastService, service.BlockExplorerService, repository, cache, tracker)
				{
					//BlockExplorer will already track the addresses, since they used a shared bitcoind, no need of tracking again (this would overwrite labels)
					TrackPreviousScriptPubKey = false
				};				
			}
			
			return service;
		}

    }
}
