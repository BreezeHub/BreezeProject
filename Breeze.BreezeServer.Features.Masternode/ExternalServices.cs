using System;
using Breeze.BreezeServer.Features.Masternode.Services;
using Breeze.TumbleBit.Client.Services;
using NBitcoin;
using NTumbleBit.Services;
using NTumbleBit.Services.RPC;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Interfaces;

namespace Breeze.BreezeServer.Features.Masternode
{
	public class ExternalServices : IExternalServices
    {
        public IFeeService FeeService { get; set; }
        public IWalletService WalletService { get; set; }
        public IBroadcastService BroadcastService { get; set; }
        public IBlockExplorerService BlockExplorerService { get; set; }
        public ITrustedBroadcastService TrustedBroadcastService { get; set; }

        private NodeSettings nodeSettings;
        private MasternodeSettings masternodeSettings;
        private IWalletManager walletManager;
        private IWalletTransactionHandler walletTransactionHandler;
        private IWalletFeePolicy walletFeePolicy;
        private IBroadcasterManager broadcasterManager;

        private static ExternalServices services = null;

        public ExternalServices(NodeSettings nodeSettings, MasternodeSettings masternodeSettings, IWalletManager walletManager, IWalletTransactionHandler walletTransactionHandler, IWalletFeePolicy walletFeePolicy, IBroadcasterManager broadcasterManager)
        {
            this.nodeSettings = nodeSettings;
            this.masternodeSettings = masternodeSettings;
            this.walletManager = walletManager;
            this.walletTransactionHandler = walletTransactionHandler;
            this.walletFeePolicy = walletFeePolicy;
            this.broadcasterManager = broadcasterManager;

            if (services == null)
                services = this;

            if (services != this)
                throw new Exception("External Services cannot be changed.");
        }

        public static ExternalServices CreateFromFullNode(IRepository repository, Tracker tracker)
        { 
            var minimumRate = services.nodeSettings.MinRelayTxFeeRate;
            // On regtest the estimatefee always fails
            if (services.masternodeSettings.IsRegTest)
            {
                services.FeeService = new FullNodeFeeService(services.walletFeePolicy)
                {
                    MinimumFeeRate = minimumRate,
                    FallBackFeeRate = new FeeRate(Money.Satoshis(50), 1)
                };
            }
            else // On test and mainnet fee estimation should just fail, not fall back to fixed fee
            {
                services.FeeService = new FullNodeFeeService(services.walletFeePolicy)
                {
                    MinimumFeeRate = minimumRate
                };
            }


            var clientBatchInterval = TimeSpan.FromMilliseconds(100);
            if (rpc.Network != NBitcoin.Network.RegTest)
            {
                var tumblerWallet = services.walletManager.GetWallet(services.masternodeSettings.TumblerWalletName);
                services.WalletService = new FullNodeWalletService(tumblerWallet, services.masternodeSettings.TumblerWalletPassword, services.walletTransactionHandler, services.broadcasterManager, services.walletManager)
                {
                    BatchInterval = useBatching ? TimeSpan.FromSeconds(160) : clientBatchInterval,
                    AddressGenerationBatchInterval = useBatching ? TimeSpan.FromSeconds(1) : TimeSpan.FromMilliseconds(10)
                };

                //var cache = new FullNodeWalletCache(tumblingState);
                services.BroadcastService = new FullNodeBroadcastService(cache, repository, tumblingState);
                services.BlockExplorerService = new FullNodeBlockExplorerService(cache, tumblingState);
                services.TrustedBroadcastService = new FullNodeTrustedBroadcastService(services.BroadcastService, services.BlockExplorerService, repository, cache, tracker, tumblingState)
                {
                    // BlockExplorer will already track the addresses, since they used a shared bitcoind, no need of tracking again (this would overwrite labels)
                    TrackPreviousScriptPubKey = false
                };
            }
            else
            {
                // For integration tests on regtest the batching needs to be almost nonexistent due to the low
                // inter-block delays

                var tumblerWallet = services.walletManager.GetWallet(services.masternodeSettings.TumblerWalletName);
                services.WalletService = new FullNodeWalletService(tumblerWallet, services.masternodeSettings.TumblerWalletPassword, services.walletTransactionHandler, services.broadcasterManager, services.walletManager);

                //var cache = new FullNodeWalletCache(tumblingState);

                services.BroadcastService = new FullNodeBroadcastService(cache, repository, tumblingState);
                services.BlockExplorerService = new FullNodeBlockExplorerService(cache, tumblingState);
                services.TrustedBroadcastService = new FullNodeTrustedBroadcastService(services.BroadcastService, services.BlockExplorerService, repository, cache, tracker, tumblingState)
                {
                    // BlockExplorer will already track the addresses, since they used a shared bitcoind, no need of tracking again (this would overwrite labels)
                    TrackPreviousScriptPubKey = false
                };
            }



            return services;
        }

    }
}
