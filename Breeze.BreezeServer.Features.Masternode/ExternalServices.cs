using System;
using Breeze.BreezeServer.Features.Masternode.Services;
using Breeze.TumbleBit.Client.Services;
using NBitcoin;
using NTumbleBit.Services;
using NTumbleBit.Services.RPC;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Features.WatchOnlyWallet;
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
        private ConcurrentChain chain { get; set; }
        private IWalletManager walletManager;
        public IWatchOnlyWalletManager watchOnlyWalletManager { get; set; }
        private IWalletTransactionHandler walletTransactionHandler;
        private IWalletFeePolicy walletFeePolicy;
        private IBroadcasterManager broadcasterManager;
        private IConnectionManager connectionManager;

        private static ExternalServices services = null;

        public ExternalServices(NodeSettings nodeSettings, MasternodeSettings masternodeSettings, ConcurrentChain chain,
            IWalletManager walletManager, IWatchOnlyWalletManager watchOnlyWalletManager,
            IWalletTransactionHandler walletTransactionHandler, IWalletFeePolicy walletFeePolicy,
            IBroadcasterManager broadcasterManager, IConnectionManager connectionManager)
        {
            this.nodeSettings = nodeSettings ?? throw new ArgumentNullException(nameof(nodeSettings));
            this.masternodeSettings = masternodeSettings ?? throw new ArgumentNullException(nameof(masternodeSettings));
            this.chain = chain ?? throw new ArgumentNullException(nameof(chain));
            this.walletManager = walletManager ?? throw new ArgumentNullException(nameof(walletManager));
            this.watchOnlyWalletManager = watchOnlyWalletManager ?? throw new ArgumentNullException(nameof(watchOnlyWalletManager));
            this.walletTransactionHandler = walletTransactionHandler ?? throw new ArgumentNullException(nameof(walletTransactionHandler));
            this.walletFeePolicy = walletFeePolicy ?? throw new ArgumentNullException(nameof(walletFeePolicy));
            this.broadcasterManager = broadcasterManager ?? throw new ArgumentNullException(nameof(broadcasterManager));
            this.connectionManager = connectionManager ?? throw new ArgumentNullException(nameof(connectionManager));

            if (services == null)
                services = this;

            if (services != this)
                throw new Exception("External Services cannot be changed.");
        }

        public static ExternalServices CreateFromFullNode(IRepository repository, Tracker tracker, bool useBatching)
        { 
            var minimumRate = services.nodeSettings.MinRelayTxFeeRate;

            // On regtest the estimatefee always fails
            if (services.masternodeSettings.IsRegTest)
            {
                if (minimumRate == FeeRate.Zero)
                    minimumRate = new FeeRate(Money.Satoshis(1500));

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
            var tumblerWallet = services.walletManager.GetWallet(services.masternodeSettings.TumblerWalletName);
            var cache = new FullNodeWalletCache(services.chain, services.walletManager, services.watchOnlyWalletManager, services.nodeSettings.Network);
            if (!services.masternodeSettings.IsRegTest)
            {
                services.WalletService = new FullNodeWalletService(tumblerWallet, services.masternodeSettings.TumblerWalletPassword, services.walletTransactionHandler, services.broadcasterManager, services.walletManager)
                {
                    BatchInterval = useBatching ? TimeSpan.FromSeconds(160) : clientBatchInterval,
                };

                services.BroadcastService = new FullNodeBroadcastService(cache, repository, services.chain, services.nodeSettings.Network, services.walletManager, services.watchOnlyWalletManager, services.broadcasterManager, services.connectionManager);
                services.BlockExplorerService = new FullNodeBlockExplorerService(cache, services.chain, services.nodeSettings.Network, services.walletManager, services.watchOnlyWalletManager, services.connectionManager);
                services.TrustedBroadcastService = new FullNodeTrustedBroadcastService(services.BroadcastService, services.BlockExplorerService, repository, cache, tracker, services.chain, services.watchOnlyWalletManager, services.nodeSettings.Network)
                {
                    // BlockExplorer will already track the addresses, since they used a shared bitcoind, no need of tracking again (this would overwrite labels)
                    TrackPreviousScriptPubKey = false
                };
            }
            else
            {
                // For integration tests on regtest the batching needs to be almost nonexistent due to the low
                // inter-block delays

                services.WalletService = new FullNodeWalletService(tumblerWallet, services.masternodeSettings.TumblerWalletPassword, services.walletTransactionHandler, services.broadcasterManager, services.walletManager);

                services.BroadcastService = new FullNodeBroadcastService(cache, repository, services.chain, services.nodeSettings.Network, services.walletManager, services.watchOnlyWalletManager, services.broadcasterManager, services.connectionManager);
                services.BlockExplorerService = new FullNodeBlockExplorerService(cache, services.chain, services.nodeSettings.Network, services.walletManager, services.watchOnlyWalletManager, services.connectionManager);
                services.TrustedBroadcastService = new FullNodeTrustedBroadcastService(services.BroadcastService, services.BlockExplorerService, repository, cache, tracker, services.chain, services.watchOnlyWalletManager, services.nodeSettings.Network)
                {
                    // BlockExplorer will already track the addresses, since they used a shared bitcoind, no need of tracking again (this would overwrite labels)
                    TrackPreviousScriptPubKey = false
                };
            }



            return services;
        }

    }
}
