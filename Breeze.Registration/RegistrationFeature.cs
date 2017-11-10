using System;
using System.Collections.Generic;
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
using Stratis.Bitcoin.Signals;
using Stratis.Bitcoin.Features.WatchOnlyWallet;

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

        private ILoggerFactory loggerFactory;
        private readonly IRegistrationManager registrationManager;
        private IDisposable blockSubscriberdDisposable;
        //private IDisposable transactionSubscriberdDisposable;

        private bool isBitcoin;
        private Network network;

        public RegistrationFeature(ILoggerFactory loggerFactory, NodeSettings nodeSettings, RegistrationManager registrationManager, RegistrationStore registrationStore, ConcurrentChain chain, Signals signals, IWatchOnlyWalletManager watchOnlyWalletManager)
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

		public override void Start()
		{
            if (!this.isBitcoin)
            {
                // Only need to subscribe to receive blocks and transactions on the Stratis network
                this.blockSubscriberdDisposable = this.signals.SubscribeForBlocks(new RegistrationBlockObserver(this.chain, this.registrationManager));
                //this.transactionSubscriberdDisposable = this.signals.SubscribeForTransactions(new TransactionObserver(this.registrationManager));
            }

            this.registrationManager.Initialize(this.loggerFactory, this.registrationStore, this.isBitcoin, this.network, this.watchOnlyWalletManager);
        }

        public override void Stop()
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
