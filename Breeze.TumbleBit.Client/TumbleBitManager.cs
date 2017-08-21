using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NTumbleBit.ClassicTumbler;
using NTumbleBit.ClassicTumbler.CLI;
using NTumbleBit.ClassicTumbler.Client;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.WatchOnlyWallet;
using Stratis.Bitcoin.Signals;

namespace Breeze.TumbleBit.Client
{
    /// <summary>
    /// An implementation of a tumbler manager.
    /// </summary>
    /// <seealso cref="Breeze.TumbleBit.Client.ITumbleBitManager" />
    public class TumbleBitManager : ITumbleBitManager
    {
        private ILoggerFactory loggerFactory;
        private readonly BlockStoreManager blockStoreManager;
        private readonly MempoolManager mempoolManager;
        private readonly WalletManager walletManager;
        private readonly IWatchOnlyWalletManager watchOnlyWalletManager;
        private readonly WalletSyncManager walletSyncManager;
        private readonly WalletTransactionHandler walletTransactionHandler;
        private readonly ILogger logger;
        private readonly Signals signals;
        private readonly ConcurrentChain chain;
        private readonly Network network;
        private TumblingState tumblingState;
        private IDisposable blockReceiver;
        private TumblerClientRuntime runtime;
        private StateMachinesExecutor stateMachine;
     
        private ClassicTumblerParameters TumblerParameters { get; set; }

        public TumbleBitManager(ILoggerFactory loggerFactory, IWalletManager walletManager, IWatchOnlyWalletManager watchOnlyWalletManager, ConcurrentChain chain, Network network, Signals signals, IWalletTransactionHandler walletTransactionHandler, BlockStoreManager blockStoreManager, MempoolManager mempoolManager, IWalletSyncManager walletSyncManager)
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
            this.blockStoreManager = blockStoreManager;
            this.mempoolManager = mempoolManager;

            this.tumblingState = new TumblingState(loggerFactory, this.chain, this.walletManager, this.watchOnlyWalletManager, this.network, this.walletTransactionHandler, this.blockStoreManager, this.mempoolManager, this.walletSyncManager);
        }

        /// <inheritdoc />
        public async Task<ClassicTumblerParameters> ConnectToTumblerAsync(Uri serverAddress)
        {
            // TODO this method will probably need to change as the connection to a tumbler is currently done during configuration
            // of the TumblebitRuntime. This method can then be modified to potentially be a convenience method 
            // where a user wants to check a tumbler's paramters before commiting to tumbling (and therefore before configuring the runtime).

            // TODO: Temporary measure
            string[] args = { "-testnet" };

            var config = new FullNodeTumblerClientConfiguration(this.tumblingState);
            config.LoadArgs(args);

            // AcceptAllClientConfiguration should be used if the interaction is null
            this.runtime = TumblerClientRuntime.FromConfiguration(config, null);

            //this.tumblerService = new TumblerService(serverAddress);
            //this.TumblerParameters = await this.tumblerService.GetClassicTumblerParametersAsync();
            this.TumblerParameters = runtime.TumblerParameters;

            if (this.TumblerParameters.Network != this.network)
            {
                throw new Exception($"The tumbler is on network {this.TumblerParameters.Network} while the wallet is on network {this.network}.");
            }
            
            // Load the current tumbling state fromt the file system
            this.tumblingState.LoadStateFromMemory();
            
            // Update and save the state
            this.tumblingState.TumblerUri = serverAddress;
            this.tumblingState.TumblerParameters = this.TumblerParameters;
            //cdthis.tumblingState.Save();

            return this.TumblerParameters;
        }

        /// <inheritdoc />
        public Task TumbleAsync(string originWalletName, string destinationWalletName)
        {
            // make sure the tumbler service is initialized
            if (this.TumblerParameters == null || this.runtime == null)
            {
                throw new Exception("Please connect to the tumbler first.");
            }

            // TODO: Remove wallet logic while getting tumbler interaction to work
            /*
            // make sure that the user is not trying to resume the process with a different wallet
            if (!string.IsNullOrEmpty(this.tumblingState.DestinationWalletName) && this.tumblingState.DestinationWalletName != destinationWalletName)
            {
                throw new Exception("Please use the same destination wallet until the end of this tumbling session.");
            }

            Wallet destinationWallet = this.walletManager.GetWallet(destinationWalletName);
            if (destinationWallet == null)
            {
                throw new Exception($"Destination wallet not found. Have you created a wallet with name {destinationWalletName}?");
            }

            Wallet originWallet = this.walletManager.GetWallet(originWalletName);
            if (originWallet == null)
            {
                throw new Exception($"Origin wallet not found. Have you created a wallet with name {originWalletName}?");
            }

            // update the state and save
            this.tumblingState.DestinationWallet = destinationWallet;
            this.tumblingState.DestinationWalletName = destinationWalletName;
            this.tumblingState.OriginWallet = originWallet;
            this.tumblingState.OriginWalletName = originWalletName;
            */
            //this.tumblingState.Save();

            // Subscribe to receive new block notifications
            // TODO: Is this the right BlockObserver or should the one used by the Wallet feature be used?
            this.blockReceiver = this.signals.SubscribeForBlocks(new BlockObserver(this.chain, this));

            this.stateMachine = new StateMachinesExecutor(this.runtime);
            this.stateMachine.Start();

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public void PauseTumbling()
        {
            this.logger.LogDebug($"Stopping the tumbling. Current height is {this.chain.Tip.Height}.");
            this.blockReceiver.Dispose();
            //this.tumblingState.Save();
        }

        /// <inheritdoc />
        public void FinishTumbling()
        {
            this.logger.LogDebug($"The tumbling process is wrapping up. Current height is {this.chain.Tip.Height}.");
            this.blockReceiver.Dispose();
            this.tumblingState.Delete();
            this.tumblingState = null;
        }

        /// <inheritdoc />
        public void ProcessBlock(int height, Block block)
        {            
            this.logger.LogDebug($"Received block with height {height} during tumbling session.");

            // update the block height in the tumbling state
            this.tumblingState.LastBlockReceivedHeight = height;
            //this.tumblingState.Save();
            
            // TODO update the state of the tumbling session in this new block
        }
    }
}
