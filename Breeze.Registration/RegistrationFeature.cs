using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Builder.Feature;
using Stratis.Bitcoin.Configuration;
using BreezeCommon;
using Stratis.Bitcoin.Signals;
using Stratis.Bitcoin.Features.WatchOnlyWallet;
using Stratis.Bitcoin.Features.Notifications.Interfaces;
using Stratis.Bitcoin.Features.Wallet.Interfaces;

namespace Breeze.Registration
{
    public class RegistrationFeature : FullNodeFeature
    {
        private const int SyncHeightMain = 772272;
        private const int SyncHeightTest = 335760;
        private const int SyncHeightRegTest = 0;
        private readonly ILogger logger;
        private readonly RegistrationStore registrationStore;
        private readonly ConcurrentChain chain;
        private readonly Signals signals;
        private readonly IWatchOnlyWalletManager watchOnlyWalletManager;
        private readonly IBlockNotification blockNotification;
        private readonly IWalletSyncManager walletSyncManager;

        private readonly ILoggerFactory loggerFactory;
        private readonly IRegistrationManager registrationManager;
        private IDisposable blockSubscriberdDisposable;

        private readonly bool isBitcoin;
        private readonly Network network;

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
            this.registrationManager = registrationManager;
            this.registrationStore = registrationStore;
            this.chain = chain;
            this.signals = signals;
            this.network = nodeSettings.Network;
            this.watchOnlyWalletManager = watchOnlyWalletManager;
            this.blockNotification = blockNotification;
            this.walletSyncManager = walletSyncManager;

            if (nodeSettings.Network == Network.Main || nodeSettings.Network == Network.TestNet ||
                nodeSettings.Network == Network.RegTest)
            {
                // Bitcoin networks - these currently only interrogate the registration store for initial master-node selection
                this.isBitcoin = true;
            }
            else
            {
                // Stratis networks - these write to the registration store as new registrations come in via blocks
                this.isBitcoin = false;

                // Force registration store to be kept in same folder as other node data
                this.registrationStore.SetStorePath(nodeSettings.DataDir);
            }
        }

        public override void Initialize()
        {
            if (!this.isBitcoin)
                this.InitializeStratis();

            this.registrationManager.Initialize(this.loggerFactory, this.registrationStore, this.isBitcoin,
                this.network, this.watchOnlyWalletManager);
        }

        public override void Dispose()
        {
            this.blockSubscriberdDisposable?.Dispose();
        }

        private void InitializeStratis()
        {
            this.logger.LogTrace("()");

            IList<RegistrationRecord> registrationRecords = this.registrationStore.GetAll();

            // If there are no registrations then revert back to the block height of when the MasterNodes were set-up.
            if (registrationRecords.Count == 0)
                RevertRegistrations();
            else
                VerifyRegistrationStore(registrationRecords);

            // Only need to subscribe to receive blocks and transactions on the Stratis network
            this.blockSubscriberdDisposable =
                this.signals.SubscribeForBlocks(new RegistrationBlockObserver(this.chain, this.registrationManager));

            this.logger.LogTrace("(-)");
        }

        private void RevertRegistrations()
        {
            this.logger.LogTrace("()");

            // For RegTest, it is not clear that re-issuing a sync command will be beneficial. Generally you want to sync from genesis in that case.
            var syncHeight = this.network == Network.StratisMain ? SyncHeightMain :
                this.network == Network.StratisTest ? SyncHeightTest : SyncHeightRegTest;

            this.logger.LogTrace("Syncing from height {0} in order to get masternode registrations", syncHeight);

            this.walletSyncManager.SyncFromHeight(syncHeight);

            this.logger.LogTrace("(-)");
        }

        private void VerifyRegistrationStore(IList<RegistrationRecord> list)
        {
            this.logger.LogTrace("()");

            this.logger.LogTrace("VerifyRegistrationStore");

            // Verify that the registration store is in a consistent state on start-up. The signatures of all the records need to be validated.
            foreach (var registrationRecord in list)
            {
                if (registrationRecord.Record.Validate(this.network)) continue;

                this.logger.LogTrace("Deleting invalid registration : {0}", registrationRecord.RecordGuid);

                this.registrationStore.Delete(registrationRecord.RecordGuid);
            }

            this.logger.LogTrace("(-)");
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