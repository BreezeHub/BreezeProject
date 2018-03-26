using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.Protocol;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Builder.Feature;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.Notifications;
using Stratis.Bitcoin.Utilities;
using BreezeCommon;
using NBitcoin.Crypto;
using Stratis.Bitcoin.Signals;
using Stratis.Bitcoin.Features.WatchOnlyWallet;
using Stratis.Bitcoin.Features.Notifications.Interfaces;
using Stratis.Bitcoin.Features.Wallet.Interfaces;

namespace Breeze.Registration
{
	public class RegistrationFeature : FullNodeFeature
	{
        private ILogger logger;
        private NodeSettings nodeSettings;
		private RegistrationStore registrationStore;
        private readonly ConcurrentChain chain;
        private readonly Signals signals;
        private IWatchOnlyWalletManager watchOnlyWalletManager;
	    private readonly BlockNotification blockNotification;
		private IWalletSyncManager walletSyncManager;

        private ILoggerFactory loggerFactory;
        private readonly IRegistrationManager registrationManager;
        private IDisposable blockSubscriberdDisposable;
        //private IDisposable transactionSubscriberdDisposable;

        private bool isBitcoin;
        private Network network;

        public RegistrationFeature(ILoggerFactory loggerFactory,
            NodeSettings nodeSettings,
            RegistrationManager registrationManager,
            RegistrationStore registrationStore,
            ConcurrentChain chain,
            Signals signals,
            IWatchOnlyWalletManager watchOnlyWalletManager,
            IBlockNotification blockNotification,
	        IWalletSyncManager walletSyncManager)
		{
            this.loggerFactory = loggerFactory;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.nodeSettings = nodeSettings;
            this.registrationManager = registrationManager;
			this.registrationStore = registrationStore;
            this.chain = chain;
            this.signals = signals;
            this.network = nodeSettings.Network;
            this.watchOnlyWalletManager = watchOnlyWalletManager;
		    this.blockNotification = blockNotification as BlockNotification;
			this.walletSyncManager = walletSyncManager;

            if (nodeSettings.Network == Network.Main || nodeSettings.Network == Network.TestNet || nodeSettings.Network == Network.RegTest)
            {
                // Bitcoin networks - these currently only interrogate the registration store for initial masternode selection
                this.isBitcoin = true;
            }
            else
            {
                // Stratis networks - these write to the registration store as new registrations come in via blocks
                this.isBitcoin = false;

                // Force registration store to be kept in same folder as other node data
                this.registrationStore.SetStorePath(this.nodeSettings.DataDir);
            }
		}

		public override void Initialize()
		{
            if (!this.isBitcoin)
            {
                // Start syncing from the height of the earliest possible registration transaction instead of the genesis block. This saves time.
                this.logger.LogDebug("Registration: blockNotification.StartHash = " + this.blockNotification.StartHash);
	            
	            // Note that the headers (Headers.Height in console output) still need to sync first, which takes some
	            // time. Once that height is reached the blocks begin being downloaded from that height in full.
                if (this.blockNotification.StartHash == null)
                {
	                if (this.network == Network.StratisMain)
	                {
		                this.walletSyncManager.SyncFromHeight(772272); // 1175552642fb0881a8de4fbc7553e1fd112ced42e5ce192559fa41b25d897e2f
					}

	                if (this.network == Network.StratisTest)
	                {
		                this.walletSyncManager.SyncFromHeight(335760); // 9997fb0d8d027fd95f6b36a3cea5c86e2926077cd430cef6b1ae735ddf7c5312
					}

	                // For regtest, it is not clear that re-issuing a sync command will be beneficial. Generally you want to sync from genesis in that case.
                }

	            // Verify that the registration store is in a consistent state on startup
	            // The signatures of all the records need to be validated
	            foreach (var registrationRecord in this.registrationStore.GetAll())
	            {
		            var registrationToken = registrationRecord.Record;

		            if (!registrationToken.Validate(this.network))
			            this.registrationStore.Delete(registrationRecord.RecordGuid);
	            }

	            // Only need to subscribe to receive blocks and transactions on the Stratis network
                this.blockSubscriberdDisposable = this.signals.SubscribeForBlocks(new RegistrationBlockObserver(this.chain, this.registrationManager));
                //this.transactionSubscriberdDisposable = this.signals.SubscribeForTransactions(new TransactionObserver(this.registrationManager));
            }

            this.registrationManager.Initialize(this.loggerFactory, this.registrationStore, this.isBitcoin, this.network, this.watchOnlyWalletManager);
        }

        public override void Dispose()
        {
            this.blockSubscriberdDisposable?.Dispose();
            //this.transactionSubscriberdDisposable?.Dispose();
        }
    }
    
	public static class RegistrationFeatureExtension
	{
		public static IFullNodeBuilder UseRegistration(this IFullNodeBuilder fullNodeBuilder)
		{
			fullNodeBuilder.ConfigureFeature(features =>
			{
				features
					.AddFeature<RegistrationFeature>()
					.FeatureServices(services =>
					{
						services.AddSingleton<RegistrationStore>();
                        services.AddSingleton<RegistrationManager>();
                    });
			});
			return fullNodeBuilder;
		}
	}
}
