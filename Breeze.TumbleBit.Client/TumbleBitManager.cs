using System;
using System.Linq;
using System.Threading.Tasks;
using Breeze.TumbleBit.Client.Models;
using Flurl.Util;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NTumbleBit.ClassicTumbler;
using NTumbleBit.ClassicTumbler.CLI;
using NTumbleBit.ClassicTumbler.Client;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.WatchOnlyWallet;
using Stratis.Bitcoin.Signals;
using NTumbleBit.Services;

namespace Breeze.TumbleBit.Client
{
    /// <summary>
    /// An implementation of a tumbler manager.
    /// </summary>
    /// <seealso cref="Breeze.TumbleBit.Client.ITumbleBitManager" />
    public class TumbleBitManager : ITumbleBitManager
    {
        public enum TumbleState
        {
            Tumbling,
            OnlyMonitor
        }

        private ILoggerFactory loggerFactory;
        private readonly WalletManager walletManager;
        private readonly IWatchOnlyWalletManager watchOnlyWalletManager;
        private readonly WalletSyncManager walletSyncManager;
        private readonly WalletTransactionHandler walletTransactionHandler;
        private readonly ILogger logger;
        private readonly Signals signals;
        private readonly ConcurrentChain chain;
        private readonly Network network;
        private readonly IWalletFeePolicy walletFeePolicy;
        private IDisposable blockReceiver;
        private TumblerClientRuntime runtime;
        private StateMachinesExecutor stateMachine;
        private BroadcasterJob broadcasterJob;

        public TumblingState tumblingState { get; private set; }
        public TumbleState State { get; private set; } = TumbleState.OnlyMonitor;
        public ClassicTumblerParameters TumblerParameters { get; private set; } = null;
        public string TumblerAddress { get; private set; } = null;

        public TumbleBitManager(
            ILoggerFactory loggerFactory,
            NodeSettings nodeSettings,
            IWalletManager walletManager,
            IWatchOnlyWalletManager watchOnlyWalletManager,
            ConcurrentChain chain,
            Network network,
            Signals signals,
            IWalletTransactionHandler walletTransactionHandler,
            IWalletSyncManager walletSyncManager,
            IWalletFeePolicy walletFeePolicy)
        {
            this.walletManager = walletManager as WalletManager;
            this.watchOnlyWalletManager = watchOnlyWalletManager;
            this.walletSyncManager = walletSyncManager as WalletSyncManager;
            this.walletTransactionHandler = walletTransactionHandler as WalletTransactionHandler;
            this.chain = chain;
            this.signals = signals;
            this.network = network;
            this.loggerFactory = loggerFactory;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.walletFeePolicy = walletFeePolicy;
            this.TumblerAddress = nodeSettings.TumblerAddress;

            this.tumblingState = new TumblingState(
                this.loggerFactory,
                this.chain,
                this.walletManager,
                this.watchOnlyWalletManager,
                this.network,
                this.walletTransactionHandler,
                this.walletSyncManager,
                this.walletFeePolicy);
        }

        /// <inheritdoc />
        public async Task<ClassicTumblerParameters> ConnectToTumblerAsync()
        {
            this.tumblingState.TumblerUri = new Uri(this.TumblerAddress);
            var config = new FullNodeTumblerClientConfiguration(this.tumblingState, onlyMonitor: false, connectionTest: true);
            TumblerClientRuntime rt = null;
            try
            {
                rt = await TumblerClientRuntime.FromConfigurationAsync(config, connectionTest:true).ConfigureAwait(false);
                return rt.TumblerParameters;
            }
            finally
            {
                rt?.Dispose();
            }
        }

        /// <inheritdoc />
        public async Task TumbleAsync(string originWalletName, string destinationWalletName, string originWalletPassword)
        {
            // make sure it won't start new tumbling round
            if (this.State == TumbleState.Tumbling)
            {
                throw new Exception("Tumbling is already running");
            }

            this.tumblingState.TumblerUri = new Uri(this.TumblerAddress);

            // TODO: Check if in IBD

            Wallet destinationWallet = this.walletManager.GetWallet(destinationWalletName);
            Wallet originWallet = this.walletManager.GetWallet(originWalletName);

            // TODO: Check if password is valid

            // Update the state and save
            this.tumblingState.DestinationWallet = destinationWallet ?? throw new Exception($"Destination wallet not found. Have you created a wallet with name {destinationWalletName}?");
            this.tumblingState.DestinationWalletName = destinationWalletName;
            this.tumblingState.OriginWallet = originWallet ?? throw new Exception($"Origin wallet not found. Have you created a wallet with name {originWalletName}?");
            this.tumblingState.OriginWalletName = originWalletName;
            this.tumblingState.OriginWalletPassword = originWalletPassword;

            var accounts = this.tumblingState.DestinationWallet.GetAccountsByCoinType(this.tumblingState.CoinType);
            // TODO: Possibly need to preserve destination account name in tumbling state. Default to first account for now
            string accountName = null;
            foreach (var account in accounts)
            {
                if (account.Index == 0)
                    accountName = account.Name;
            }
            var destAccount = this.tumblingState.DestinationWallet.GetAccountByCoinType(accountName, this.tumblingState.CoinType);

            var key = destAccount.ExtendedPubKey;
            var keyPath = new KeyPath("0");

            // stop and dispose onlymonitor
            if (this.broadcasterJob != null && this.broadcasterJob.Started)
            {
                await this.broadcasterJob.Stop().ConfigureAwait(false);
            }
            this.runtime?.Dispose();

            var config = new FullNodeTumblerClientConfiguration(this.tumblingState, onlyMonitor: false);
            this.runtime = await TumblerClientRuntime.FromConfigurationAsync(config).ConfigureAwait(false);

            var extPubKey = new BitcoinExtPubKey(key, this.runtime.Network);
            if (key != null)
                this.runtime.DestinationWallet =
                    new ClientDestinationWallet(extPubKey, keyPath, this.runtime.Repository, this.runtime.Network);
            this.TumblerParameters = this.runtime.TumblerParameters;
            // run onlymonitor mode
            this.broadcasterJob = this.runtime.CreateBroadcasterJob();
            this.broadcasterJob.Start();

            // Subscribe to receive new block notifications
            // TODO: Is this the right BlockObserver or should the one used by the Wallet feature be used?
            this.blockReceiver = this.signals.SubscribeForBlocks(new BlockObserver(this.chain, this));
            // run tumbling mode
            this.stateMachine = new StateMachinesExecutor(this.runtime);
            this.stateMachine.Start();

            this.State = TumbleState.Tumbling;

            return;
        }

        public async Task OnlyMonitorAsync()
        {
            // onlymonitor is running by default, so it's enough if statemachine is stopped
            if (this.stateMachine != null && this.stateMachine.Started)
            {
                await this.stateMachine.Stop().ConfigureAwait(false);
            }
            this.State = TumbleState.OnlyMonitor;
        }

        /// <inheritdoc />
        public void ProcessBlock(int height, Block block)
        {
            this.logger.LogDebug($"Received block with height {height} during tumbling session.");

            // Update the block height in the tumbling state
            this.tumblingState.LastBlockReceivedHeight = height;

            // TODO: Update the state of the tumbling session in this new block
            // TODO: Does anything else need to be done here? Transaction housekeeping is done in the wallet features
        }

        public async Task Initialize()
        {
            // Start broadcasterJob (onlymonitor mode)
            if (this.broadcasterJob == null || !this.broadcasterJob.Started)
            {
                var config = new FullNodeTumblerClientConfiguration(this.tumblingState, onlyMonitor: true);
                this.runtime = await TumblerClientRuntime.FromConfigurationAsync(config).ConfigureAwait(false);

                this.State = TumbleState.OnlyMonitor;

                this.broadcasterJob = this.runtime.CreateBroadcasterJob();
                this.broadcasterJob.Start();
            }
        }

        public void Dispose()
        {
            this.blockReceiver?.Dispose();

            if (this.broadcasterJob != null && this.broadcasterJob.Started)
            {
                this.broadcasterJob.Stop();
            }

            if (this.stateMachine != null && this.stateMachine.Started)
            {
                this.stateMachine.Stop();
            }

            this.runtime?.Dispose();
        }
    }
}
