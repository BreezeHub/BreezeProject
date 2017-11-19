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
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.WatchOnlyWallet;
using Stratis.Bitcoin.Signals;
using NTumbleBit.Services;
using BreezeCommon;
using System.Collections.Generic;
using System.Text;
using Stratis.Bitcoin;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Interfaces;
using System.IO;
using NTumbleBit;

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

        private const int MINIMUM_MASTERNODE_COUNT = 1;

        private static Random random = new Random();

        private ILoggerFactory loggerFactory;
        private readonly WalletManager walletManager;
        private readonly IWatchOnlyWalletManager watchOnlyWalletManager;
        private readonly WalletSyncManager walletSyncManager;
        private readonly WalletTransactionHandler walletTransactionHandler;
        private readonly ILogger logger;
        private readonly Signals signals;
        private readonly ConcurrentChain chain;
        private readonly Network network;
        private readonly NodeSettings nodeSettings;
        private readonly IWalletFeePolicy walletFeePolicy;
        private IBroadcasterManager broadcasterManager;
        private FullNode fullNode;
        private TumblerClientRuntime runtime;
        private StateMachinesExecutor stateMachine;
        private BroadcasterJob broadcasterJob;

        public TumblingState tumblingState { get; private set; }
        public TumbleState State { get; private set; } = TumbleState.OnlyMonitor;
        public ClassicTumblerParameters TumblerParameters { get; private set; } = null;
        public string TumblerAddress { get; private set; } = null;
        public RegistrationStore registrationStore { get; private set; }

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
            IWalletFeePolicy walletFeePolicy,
            IBroadcasterManager broadcasterManager,
            FullNode fullNode,
            ConfigurationOptionWrapper<string> registrationStoreDirectory)
        {
            this.walletManager = walletManager as WalletManager;
            this.watchOnlyWalletManager = watchOnlyWalletManager;
            this.walletSyncManager = walletSyncManager as WalletSyncManager;
            this.walletTransactionHandler = walletTransactionHandler as WalletTransactionHandler;
            this.chain = chain;
            this.signals = signals;
            this.network = network;
            this.nodeSettings = nodeSettings;
            this.loggerFactory = loggerFactory;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.walletFeePolicy = walletFeePolicy;
            this.broadcasterManager = broadcasterManager;
            this.fullNode = fullNode;

            if (registrationStoreDirectory.Value != null)
            {
                this.registrationStore = new RegistrationStore(registrationStoreDirectory.Value);
            }
            else
            {
                this.registrationStore = new RegistrationStore(this.nodeSettings.DataDir);
            }

            this.tumblingState = new TumblingState(
                this.loggerFactory,
                this.chain,
                this.walletManager,
                this.watchOnlyWalletManager,
                this.network,
                this.walletTransactionHandler,
                this.walletSyncManager,
                this.walletFeePolicy,
                this.nodeSettings,
                this.broadcasterManager);

            // Load saved state e.g. previously selected server
	        if (File.Exists(this.tumblingState.GetStateFilePath()))
	        {
		        this.tumblingState.LoadStateFromMemory();
	        }
	        else
	        {
		        this.tumblingState.Save();
	        }

			//remove and progress file from previous session
			//as it is now stale
			ProgressInfo.RemoveProgressFile();
        }

        public async Task<bool> BlockGenerate(int numberOfBlocks)
        {
            var wallet = this.tumblingState.WalletManager;
            var w = wallet.GetWalletsNames().FirstOrDefault();
            if (w == null)
                throw new Exception("No wallet found");
            var acc = wallet.GetAccounts(w).FirstOrDefault();
            var account = new WalletAccountReference(w, acc.Name);
            var address = wallet.GetUnusedAddress(account);

            try
            {
                PowMining powMining = this.fullNode.NodeService<PowMining>();

                if (this.network == Network.Main || this.network == Network.TestNet || this.network == Network.RegTest)
                {
                    // Bitcoin PoW
                    powMining.GenerateBlocks(new ReserveScript(address.Pubkey), (ulong)numberOfBlocks, int.MaxValue);
                }

                if (this.network == Network.StratisMain || this.network == Network.StratisTest || this.network == Network.StratisRegTest)
                {
                    // Stratis PoW
                    powMining.GenerateBlocks(new ReserveScript { reserveSfullNodecript = address.ScriptPubKey }, (ulong)numberOfBlocks, int.MaxValue);
                }

                return true;
            }
            catch (Exception e)
            {
                this.logger.LogError("Error generating block(s): " + e);
                return false;
            }
        }

        public async Task DummyRegistration(string originWalletName, string originWalletPassword)
        {
            var token = new List<byte>();

            // Server ID
            token.AddRange(Encoding.ASCII.GetBytes("".PadRight(34)));

            // IPv4 address
            token.Add(0x00); token.Add(0x00); token.Add(0x00); token.Add(0x00);

            // IPv6 address
            token.Add(0x00); token.Add(0x00); token.Add(0x00); token.Add(0x00);
            token.Add(0x00); token.Add(0x00); token.Add(0x00); token.Add(0x00);
            token.Add(0x00); token.Add(0x00); token.Add(0x00); token.Add(0x00);
            token.Add(0x00); token.Add(0x00); token.Add(0x00); token.Add(0x00);

            // Onion address
            token.Add(0x00); token.Add(0x00); token.Add(0x00); token.Add(0x00);
            token.Add(0x00); token.Add(0x00); token.Add(0x00); token.Add(0x00);
            token.Add(0x00); token.Add(0x00); token.Add(0x00); token.Add(0x00);
            token.Add(0x00); token.Add(0x00); token.Add(0x00); token.Add(0x00);

            // Port number
            byte[] portNumber = BitConverter.GetBytes(37123);
            token.Add(portNumber[0]);
            token.Add(portNumber[1]);

            // RSA sig length
            byte[] rsaLength = BitConverter.GetBytes(256);
            token.Add(rsaLength[0]);
            token.Add(rsaLength[1]);

            // RSA signature
            byte[] rsaSig = new byte[256];
            token.AddRange(rsaSig);

            // ECDSA sig length
            byte[] ecdsaLength = BitConverter.GetBytes(128);
            token.Add(ecdsaLength[0]);
            token.Add(ecdsaLength[1]);

            // ECDSA signature
            byte[] ecdsaSig = new byte[128];
            token.AddRange(ecdsaSig);

            // Configuration hash
            token.AddRange(Encoding.ASCII.GetBytes("aa4e984c5655a677716539acc8cbc0ce29331429"));

            // Finally add protocol byte and computed length to beginning of header
            byte[] protocolVersionByte = BitConverter.GetBytes(254);
            byte[] headerLength = BitConverter.GetBytes(token.Count);

            token.Insert(0, protocolVersionByte[0]);
            token.Insert(1, headerLength[0]);
            token.Insert(2, headerLength[1]);

            Money outputValue = new Money(0.0001m, MoneyUnit.BTC);

            Transaction sendTx = new Transaction();

            // Recognisable string used to tag the transaction within the blockchain
            byte[] bytes = Encoding.UTF8.GetBytes("BREEZE_REGISTRATION_MARKER");
            sendTx.Outputs.Add(new TxOut()
            {
                Value = outputValue,
                ScriptPubKey = TxNullDataTemplate.Instance.GenerateScriptPubKey(bytes)
            });

            // Add each data-encoding PubKey as a TxOut
            foreach (PubKey pubKey in BlockChainDataConversions.BytesToPubKeys(token.ToArray()))
            {
                TxOut destTxOut = new TxOut()
                {
                    Value = outputValue,
                    ScriptPubKey = pubKey.ScriptPubKey
                };

                sendTx.Outputs.Add(destTxOut);
            }

            HdAccount highestAcc = null;
            foreach (HdAccount account in this.walletManager.GetAccounts(originWalletName))
            {
                if (highestAcc == null)
                {
                    highestAcc = account;
                }

                if (account.GetSpendableAmount().ConfirmedAmount > highestAcc.GetSpendableAmount().ConfirmedAmount)
                {
                    highestAcc = account;
                }
            }

            // This fee rate is primarily for regtest, testnet and mainnet have actual estimators that work
            FeeRate feeRate = new FeeRate(new Money(10000, MoneyUnit.Satoshi));
            WalletAccountReference accountRef = new WalletAccountReference(originWalletName, highestAcc.Name);
            List<Recipient> recipients = new List<Recipient>();
            TransactionBuildContext txBuildContext = new TransactionBuildContext(accountRef, recipients);
            txBuildContext.WalletPassword = originWalletPassword;
            txBuildContext.OverrideFeeRate = feeRate;
            txBuildContext.Sign = true;
            txBuildContext.MinConfirmations = 0;

            this.walletTransactionHandler.FundTransaction(txBuildContext, sendTx);

            this.logger.LogDebug("Trying to broadcast transaction: " + sendTx.GetHash());

            var bcResult = await this.broadcasterManager.TryBroadcastAsync(sendTx).ConfigureAwait(false);
            if (bcResult == Stratis.Bitcoin.Broadcasting.Success.Yes)
            {
                this.logger.LogDebug("Broadcasted transaction: " + sendTx.GetHash());
            }
            else if (bcResult == Stratis.Bitcoin.Broadcasting.Success.No)
            {
                this.logger.LogDebug("Could not propagate transaction: " + sendTx.GetHash());
            }
            else if (bcResult == Stratis.Bitcoin.Broadcasting.Success.DontKnow)
            {
                // wait for propagation
                var waited = TimeSpan.Zero;
                var period = TimeSpan.FromSeconds(1);
                while (TimeSpan.FromSeconds(21) > waited)
                {
                    // if broadcasts doesn't contain then success
                    var transactionEntry = this.broadcasterManager.GetTransaction(sendTx.GetHash());
                    if (transactionEntry != null && transactionEntry.State == Stratis.Bitcoin.Broadcasting.State.Propagated)
                    {
                        this.logger.LogDebug("Propagated transaction: " + sendTx.GetHash());
                    }
                    await Task.Delay(period).ConfigureAwait(false);
                    waited += period;
                }
            }

            this.logger.LogDebug("Uncertain if transaction was propagated: " + sendTx.GetHash());
        }

        /// <inheritdoc />
        public async Task<ClassicTumblerParameters> ConnectToTumblerAsync()
        {
            /// If the -ppuri command line option wasn't used to bypass the registration store lookup
            if (this.TumblerAddress == null)
            {
                List<RegistrationRecord> registrations = this.registrationStore.GetAll();

                if (registrations.Count < MINIMUM_MASTERNODE_COUNT)
                {
                    this.logger.LogDebug("Not enough masternode registrations downloaded yet: " + registrations.Count);
                    return null;
                }

                RegistrationRecord record = null;
                RegistrationToken registrationToken = null;
                bool validRegistrationFound = false;
                
                // TODO: Search the registration store more robustly
                for (int i = 1; i < 10; i++)
                {
                    record = registrations[random.Next(registrations.Count)];
                    registrationToken = record.Record;

                    // Implicitly, the registration feature will have deleted the registration if the collateral
                    // requirement was not met within 30 blocks
                    if ((this.walletManager.LastBlockHeight() - record.BlockReceived) >= 32)
                    {
                        validRegistrationFound = true;
                        break;
                    }
                }
                
                if (!validRegistrationFound)
                {
                    this.logger.LogDebug("Did not find a valid registration");
                    return null;
                }
                
                this.TumblerAddress = "ctb://" + record.Record.OnionAddress + ".onion?h=" + record.Record.ConfigurationHash;
            }

            this.tumblingState.TumblerUri = new Uri(this.TumblerAddress);
            var config = new FullNodeTumblerClientConfiguration(this.tumblingState, onlyMonitor: false, connectionTest: true);
            TumblerClientRuntime rt = null;
            try
            {
                rt = await TumblerClientRuntime.FromConfigurationAsync(config, connectionTest:true).ConfigureAwait(false);
                return rt.TumblerParameters;
            }
            catch (Exception e)
            {
                this.logger.LogError("Error obtaining tumbler parameters: " + e);
                return null;
            }
            finally
            {
                rt?.Dispose();
            }
        }

        /// <inheritdoc />
        public async Task TumbleAsync(string originWalletName, string destinationWalletName, string originWalletPassword)
        {
            // Make sure it won't start new tumbling round if already started
            if (this.State == TumbleState.Tumbling)
            {
                this.logger.LogDebug("Tumbler is already running");
                throw new Exception("Tumbling is already running");
            }

            this.tumblingState.TumblerUri = new Uri(this.TumblerAddress);

            // Check if in initial block download
            if (!this.chain.IsDownloaded())
            {
                this.logger.LogDebug("Chain is still being downloaded: " + this.chain.Tip.ToString());
                throw new Exception("Chain is still being downloaded");
            }

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

            // Stop and dispose onlymonitor
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
            // Run onlymonitor mode
            this.broadcasterJob = this.runtime.CreateBroadcasterJob();
            this.broadcasterJob.Start();

            // Run tumbling mode
            this.stateMachine = new StateMachinesExecutor(this.runtime);
            this.stateMachine.Start();

            this.State = TumbleState.Tumbling;

            return;
        }

        public async Task OnlyMonitorAsync()
        {
            // Onlymonitor is running by default, so it's enough if statemachine is stopped
            if (this.stateMachine != null && this.stateMachine.Started)
            {
                await this.stateMachine.Stop().ConfigureAwait(false);
            }
            this.State = TumbleState.OnlyMonitor;
        }

        public int RegistrationCount()
        {
            return this.registrationStore.GetAll().Count;
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

        /// <inheritdoc />
        public void ProcessBlock(int height, Block block)
        {
            this.tumblingState.LastBlockReceivedHeight = height;
            this.tumblingState.Save();
        }

        public void Dispose()
        {
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
