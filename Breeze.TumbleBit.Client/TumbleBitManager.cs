using System;
using System.Linq;
using System.Threading.Tasks;
using Breeze.TumbleBit.Client.Models;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NTumbleBit.ClassicTumbler;
using NTumbleBit.ClassicTumbler.Client;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.WatchOnlyWallet;
using Stratis.Bitcoin.Signals;
using NTumbleBit.Services;
using BreezeCommon;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Stratis.Bitcoin;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Interfaces;
using System.IO;
using NTumbleBit;
using NTumbleBit.Configuration;
using NTumbleBit.Logging;
using Stratis.Bitcoin.Connection;
using TransactionData = Stratis.Bitcoin.Features.Wallet.TransactionData;

namespace Breeze.TumbleBit.Client
{
    /// <summary>
    /// Manages the logic of the Breeze Privacy Protocol
    /// </summary>
    /// <seealso cref="Breeze.TumbleBit.Client.ITumbleBitManager" />
    public class TumbleBitManager : ITumbleBitManager
    {
        public enum TumbleState
        {
            Tumbling,
            OnlyMonitor
        }

        public static readonly int MINIMUM_MASTERNODE_COUNT = 1;

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
        private ConnectionManager connectionManager;
        private FullNode fullNode;
        private TumblerClientRuntime runtime;
        private StateMachinesExecutor stateMachine;
        private BroadcasterJob broadcasterJob;

        public TumblingState TumblingState { get; private set; }
        public TumbleState State => (stateMachine != null && stateMachine.IsTumbling) ? TumbleState.Tumbling : TumbleState.OnlyMonitor;
        public ClassicTumblerParameters TumblerParameters { get; private set; } = null;
        public string TumblerAddress { get; private set; } = null;
        public RegistrationStore RegistrationStore { get; private set; }

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
            ConfigurationOptionWrapper<string>[] configurationOptions)
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
            this.connectionManager = fullNode.ConnectionManager as ConnectionManager;
            this.fullNode = fullNode;

            foreach (var option in configurationOptions)
            {
                if (option.Name.Equals("RegistrationStoreDirectory"))
                {
                    if (option.Value != null)
                    {
                        this.RegistrationStore = new RegistrationStore(option.Value);
                    }
                    else
                    {
                        this.RegistrationStore = new RegistrationStore(this.nodeSettings.DataDir);
                    }
                }

                if (option.Name.Equals("MasterNodeUri"))
                {
                    if (option.Value != null)
                    {
                        this.TumblerAddress = option.Value;
                    }
                }
            }

            this.TumblingState = new TumblingState(
                this.loggerFactory,
                this.chain,
                this.walletManager,
                this.watchOnlyWalletManager,
                this.network,
                this.walletTransactionHandler,
                this.walletSyncManager,
                this.walletFeePolicy,
                this.nodeSettings,
                this.broadcasterManager,
                this.connectionManager);

            // Load saved state e.g. previously selected server
            if (File.Exists(this.TumblingState.GetStateFilePath()))
            {
                try
                {
                    this.TumblingState.LoadStateFromMemory();
                }
                catch (NullReferenceException)
                {
                    // The file appears to get corrupted sometimes, not clear why
                    // May be if the node is not shut down correctly
                }
            }

            this.TumblingState.Save();

            // If there was a server address saved, that means we were previously
            // connected to it, and should try to reconnect to it by default when
            // the connect method is invoked by the UI
            if ((this.TumblerAddress == null) && (this.TumblingState.TumblerUri != null))
                this.TumblerAddress = this.TumblingState.TumblerUri.ToString();

            RemoveProgress();
        }

        /// <inheritdoc />
        public async Task<Result<ClassicTumblerParameters>> ConnectToTumblerAsync(HashSet<string> masternodeBlacklist = null)
        {
            // Assumptions about the current state coming into this method:
            // - If this is a first connection, this.TumblerAddress will be null
            // - If we were previously connected to a server, its URI would have been stored in the
            //   tumbling_state.json, and will have been loaded into this.TumblerAddress already
            if (this.TumblerAddress == null)
            {
                List<RegistrationRecord> registrations = this.RegistrationStore.GetAll();

                if (registrations.Count < MINIMUM_MASTERNODE_COUNT)
                {
                    this.logger.LogDebug($"Not enough masternode registrations downloaded yet: {registrations.Count}");
                    return Result.Fail<ClassicTumblerParameters>("Not enough masternode registrations downloaded yet", PostResultActionType.CanContinue);
                }

                registrations.Shuffle();

                // Since the list is shuffled, we can simply iterate through it and try each server until one is valid & reachable.
                foreach (RegistrationRecord record in registrations)
                {
                    this.TumblerAddress = $"ctb://{record.Record.OnionAddress}.onion?h={record.Record.ConfigurationHash}";

                    //Do not attempt a connection to the Masternode which is blacklisted
                    if (masternodeBlacklist != null && masternodeBlacklist.Contains(this.TumblerAddress))
                    {
                        this.logger.LogDebug($"Skipping connection attempt to blacklisted masternode {this.TumblerAddress}");
                        continue;
                    }

                    var tumblerParameterResult = await TryUseServer();

                    if (tumblerParameterResult.Success)
                    {
                        return tumblerParameterResult;
                    }
                    else if (tumblerParameterResult.PostResultAction == PostResultActionType.ShouldStop)
                    {
                        return tumblerParameterResult;
                    }
                }

                this.logger.LogDebug($"Attempted connection to {registrations.Count} masternodes and did not find a valid registration");
                return Result.Fail<ClassicTumblerParameters>("Did not find a valid registration", PostResultActionType.ShouldStop);
            }
            else
            {
                var tumblerParameterResult = await TryUseServer();

                if (tumblerParameterResult.Success)
                {
                    return tumblerParameterResult;
                }

                // Blacklist masternode address which we have just failed to connect to so that
                // we won't attempt to connect to it again in the next call to ConnectToTumblerAsync.
                HashSet<string> blacklistedMasternodes = new HashSet<string>() { this.TumblerAddress };

                // The masternode that was being used in a previous run is now unreachable.
                // Restart the connection process and try to find a working server.
                this.TumblerAddress = null;
                return await ConnectToTumblerAsync(blacklistedMasternodes);
            }
        }

        /// <inheritdoc />
        public async Task<Result<ClassicTumblerParameters>> ChangeServerAsync()
        {
            // First stop the state machine if applicable
            if (this.stateMachine != null && this.stateMachine.Started)
            {
                await this.stateMachine.Stop().ConfigureAwait(false);
            }

            // Blacklist masternode address which we are currently connected to so that
            // we won't attempt to connect to it again in the next call to ConnectToTumblerAsync.
            HashSet<string> blacklistedMasternodes = new HashSet<string>() { this.TumblerAddress };

            // The masternode that was being used in a previous run is now unreachable.
            // Restart the connection process and try to find a working server.
            this.TumblerAddress = null;
            return await ConnectToTumblerAsync(blacklistedMasternodes);
        }

        private async Task<Result<ClassicTumblerParameters>> TryUseServer()
        {
            logger.LogInformation($"Attempting connection to the masternode at address {this.TumblerAddress}");

            this.TumblingState.TumblerUri = new Uri(this.TumblerAddress);

            FullNodeTumblerClientConfiguration config;
            if (this.TumblerAddress.Contains("127.0.0.1"))
            {
                config = new FullNodeTumblerClientConfiguration(this.TumblingState, onlyMonitor: false,
                    connectionTest: true, useProxy: false);
            }
            else
            {
                config = new FullNodeTumblerClientConfiguration(this.TumblingState, onlyMonitor: false,
                    connectionTest: true, useProxy: true);
            }

            TumblerClientRuntime rt = null;
            try
            {
                rt = await TumblerClientRuntime.FromConfigurationAsync(config, connectionTest: true)
                    .ConfigureAwait(false);

                // This is overwritten by the tumble method, but it is needed at the beginning of that method for the balance check
                this.TumblerParameters = rt.TumblerParameters;

                return Result.Ok(rt.TumblerParameters);
            }
            catch (PrivacyProtocolConfigException e)
            {
                this.logger.LogError(e, "Privacy protocol exception: {0}", e.Message);
                return Result.Fail<ClassicTumblerParameters>("TOR is required for connectivity to an active Stratis Masternode. Please restart Breeze Wallet with Privacy Protocol and ensure that an instance of TOR is running.", PostResultActionType.ShouldStop);
            }
            catch (ConfigException e)
            {
                this.logger.LogError(e, "Privacy protocol config exception: {0}", e.Message);
                return Result.Fail<ClassicTumblerParameters>(e.Message, PostResultActionType.CanContinue);
            }
            catch (Exception e)
            {
                this.logger.LogError(e, "Error obtaining tumbler parameters: {0}", e.Message);
                return Result.Fail<ClassicTumblerParameters>("Error obtaining tumbler parameters", PostResultActionType.CanContinue);
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

            this.TumblingState.TumblerUri = new Uri(this.TumblerAddress);

            // Check if in initial block download
            if (!this.chain.IsDownloaded())
            {
                this.logger.LogDebug("Chain is still being downloaded: " + this.chain.Tip);
                throw new Exception("Chain is still being downloaded");
            }

            Wallet originWallet = this.walletManager.GetWallet(originWalletName);
            Wallet destinationWallet = this.walletManager.GetWallet(destinationWalletName);

            // Check if password is valid before starting any cycles
            try
            {
                HdAddress tempAddress = originWallet.GetAccountsByCoinType(this.TumblingState.CoinType).First()
                    .GetFirstUnusedReceivingAddress();
                originWallet.GetExtendedPrivateKeyForAddress(originWalletPassword, tempAddress);
            }
            catch (Exception)
            {
                this.logger.LogDebug("Origin wallet password appears to be invalid");
                throw new Exception("Origin wallet password appears to be invalid");
            }

            // Update the state and save
            this.TumblingState.DestinationWallet = destinationWallet ?? throw new Exception($"Destination wallet not found. Have you created a wallet with name {destinationWalletName}?");
            this.TumblingState.DestinationWalletName = destinationWalletName;
            this.TumblingState.OriginWallet = originWallet ?? throw new Exception($"Origin wallet not found. Have you created a wallet with name {originWalletName}?");
            this.TumblingState.OriginWalletName = originWalletName;
            this.TumblingState.OriginWalletPassword = originWalletPassword;

            var accounts = this.TumblingState.DestinationWallet.GetAccountsByCoinType(this.TumblingState.CoinType);
            // TODO: Possibly need to preserve destination account name in tumbling state. Default to first account for now
            string accountName = accounts.First().Name;
            HdAccount destAccount = this.TumblingState.DestinationWallet.GetAccountByCoinType(accountName, this.TumblingState.CoinType);
            string key = destAccount.ExtendedPubKey;
            KeyPath keyPath = new KeyPath("0");

            // Stop and dispose onlymonitor
            if (this.broadcasterJob != null && this.broadcasterJob.Started)
            {
                await this.broadcasterJob.Stop().ConfigureAwait(false);
            }
            this.runtime?.Dispose();

            // Bypass Tor for integration tests
            FullNodeTumblerClientConfiguration config;
            if (this.TumblerAddress.Contains("127.0.0.1"))
            {
                config = new FullNodeTumblerClientConfiguration(this.TumblingState, onlyMonitor: false,
                    connectionTest: false, useProxy: false);
            }
            else
            {
                config = new FullNodeTumblerClientConfiguration(this.TumblingState, onlyMonitor: false,
                    connectionTest: false, useProxy: true);
            }

            this.runtime = await TumblerClientRuntime.FromConfigurationAsync(config).ConfigureAwait(false);

            // Check if origin wallet has a sufficient balance to begin tumbling at least 1 cycle
            if (!this.runtime.HasEnoughFundsForCycle(true))
            {
                this.logger.LogDebug("Insufficient funds in origin wallet");
                throw new Exception("Insufficient funds in origin wallet");
            }

            BitcoinExtPubKey extPubKey = new BitcoinExtPubKey(key, this.runtime.Network);
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
        }

        public async Task OnlyMonitorAsync()
        {
            // Onlymonitor is running by default, so it's enough if statemachine is stopped
            if (this.stateMachine != null && this.stateMachine.Started)
            {
                RemoveProgress();

                await this.stateMachine.Stop().ConfigureAwait(false);
            }
        }

        public int RegistrationCount()
        {
            try
            {
                return this.RegistrationStore.GetAll().Count;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        public async Task Initialize()
        {
            // Start broadcasterJob (onlymonitor mode)
            if (this.broadcasterJob == null || !this.broadcasterJob.Started)
            {
                var config = new FullNodeTumblerClientConfiguration(this.TumblingState, onlyMonitor: true);
                this.runtime = await TumblerClientRuntime.FromConfigurationAsync(config).ConfigureAwait(false);
                this.broadcasterJob = this.runtime.CreateBroadcasterJob();
                this.broadcasterJob.Start();
            }
        }

        /// <inheritdoc />
        public void ProcessBlock(int height, Block block)
        {
            // TODO: This double spend removal logic should be incorporated into the wallet
            if (this.TumblingState.OriginWallet != null && this.TumblingState.DestinationWallet != null)
            {
                this.logger.LogDebug("Checking origin/destination wallets for double spends");

                // Examine origin wallet for transactions that were erroneously included.
                // Specifically, those spending inputs that have been spent by transactions
                // in the destination wallet. This would otherwise frequently happen with
                // the ClientRedeem transaction in particular.

                HashSet<OutPoint> inputsSpent = new HashSet<OutPoint>();
                HashSet<TransactionData> txToRemove = new HashSet<TransactionData>();

                // Build cache of prevOuts from confirmed destination wallet transactions
                // TODO: This can probably be substantially optimised, e.g. make cache persistent between blocks
                foreach (TransactionData destTx in this.TumblingState.DestinationWallet.GetAllTransactionsByCoinType(
                    this.TumblingState.CoinType))
                {
                    foreach (TxIn input in destTx.Transaction.Inputs)
                    {
                        inputsSpent.Add(input.PrevOut);
                    }
                }

                // Now check inputs of unconfirmed transactions in origin wallet.
                // It is implicitly assumed that a confirmed transaction is usually not double spending.
                foreach (TransactionData originTx in this.TumblingState.OriginWallet.GetAllTransactionsByCoinType(
                    this.TumblingState.CoinType))
                {
                    if (originTx.IsConfirmed())
                        continue;

                    foreach (TxIn input in originTx.Transaction.Inputs)
                    {
                        if (inputsSpent.Contains(input.PrevOut))
                        {
                            this.logger.LogDebug($"Found double spend in origin wallet with txid {originTx.Id} spending {input.PrevOut.Hash} index {input.PrevOut.N}");
                            txToRemove.Add(originTx);
                        }
                    }
                }

                // Now remove the transactions identified as double spends from the origin wallet
                foreach (TransactionData tx in txToRemove)
                {
                    this.logger.LogDebug($"Detected double spend transaction in origin wallet, deleting {tx.Id}");

                    foreach (HdAccount account in this.TumblingState.OriginWallet.GetAccountsByCoinType(
                        this.TumblingState.CoinType))
                    {
                        foreach (HdAddress address in account.FindAddressesForTransaction(transaction =>
                            transaction.Id == tx.Id))
                        {
                            address.Transactions.Remove(tx);
                        }
                    }
                }

                // Now perform the same operation the other way around - there
                // can also be unconfirmed transactions in the destination that
                // double spend transactions in the source wallet
                inputsSpent.Clear();
                txToRemove.Clear();

                // Build cache of prevOuts from confirmed origin wallet transactions
                // TODO: This can probably be substantially optimised, e.g. make cache persistent between blocks
                foreach (TransactionData originTx in this.TumblingState.OriginWallet.GetAllTransactionsByCoinType(
                    this.TumblingState.CoinType))
                {
                    foreach (TxIn input in originTx.Transaction.Inputs)
                    {
                        inputsSpent.Add(input.PrevOut);
                    }
                }

                // Now check inputs of unconfirmed transactions in destination wallet.
                // It is implicitly assumed that a confirmed transaction is usually not double spending.
                foreach (TransactionData destTx in this.TumblingState.DestinationWallet.GetAllTransactionsByCoinType(
                    this.TumblingState.CoinType))
                {
                    if (destTx.IsConfirmed())
                        continue;

                    foreach (TxIn input in destTx.Transaction.Inputs)
                    {
                        if (inputsSpent.Contains(input.PrevOut))
                        {
                            this.logger.LogDebug($"Found double spend in destination wallet with txid {destTx.Id} spending {input.PrevOut.Hash} index {input.PrevOut.N}");
                            txToRemove.Add(destTx);
                        }
                    }
                }

                // Now remove the transactions identified as double spends from the destination wallet
                foreach (TransactionData tx in txToRemove)
                {
                    foreach (HdAccount account in this.TumblingState.DestinationWallet.GetAccountsByCoinType(
                        this.TumblingState.CoinType))
                    {
                        foreach (HdAddress address in account.FindAddressesForTransaction(transaction =>
                            transaction.Id == tx.Id))
                        {
                            address.Transactions.Remove(tx);
                        }
                    }
                }

                // Now check for other transactions within the same wallet that spend the same input as each transaction

                txToRemove.Clear();

                foreach (TransactionData originTx in this.TumblingState.OriginWallet.GetAllTransactionsByCoinType(this.TumblingState.CoinType))
                {
                    if (originTx.IsConfirmed())
                        continue;

                    foreach (TransactionData comparedTx in this.TumblingState.OriginWallet.GetAllTransactionsByCoinType(this.TumblingState.CoinType))
                    {
                        if (originTx.Id == comparedTx.Id)
                            continue;

                        foreach (TxIn input in originTx.Transaction.Inputs)
                        {
                            foreach (TxIn comparedTxInput in originTx.Transaction.Inputs)
                            {
                                if (input.PrevOut == comparedTxInput.PrevOut)
                                {
                                    this.logger.LogDebug($"Detected unconfirmed double spend transaction in origin wallet, deleting {originTx.Id}");

                                    txToRemove.Add(originTx);
                                }
                            }
                        }
                    }
                }

                foreach (TransactionData tx in txToRemove)
                {
                    foreach (HdAccount account in this.TumblingState.OriginWallet.GetAccountsByCoinType(this.TumblingState.CoinType))
                    {
                        foreach (HdAddress address in account.FindAddressesForTransaction(transaction => transaction.Id == tx.Id))
                        {
                            address.Transactions.Remove(tx);
                        }
                    }
                }

                txToRemove.Clear();

                foreach (TransactionData destTx in this.TumblingState.DestinationWallet.GetAllTransactionsByCoinType(this.TumblingState.CoinType))
                {
                    if (destTx.IsConfirmed())
                        continue;

                    foreach (TransactionData comparedTx in this.TumblingState.DestinationWallet.GetAllTransactionsByCoinType(this.TumblingState.CoinType))
                    {
                        if (destTx.Id == comparedTx.Id)
                            continue;

                        foreach (TxIn input in destTx.Transaction.Inputs)
                        {
                            foreach (TxIn comparedTxInput in destTx.Transaction.Inputs)
                            {
                                if (input.PrevOut == comparedTxInput.PrevOut)
                                {
                                    this.logger.LogDebug($"Detected unconfirmed double spend transaction in destination wallet, deleting {destTx.Id}");

                                    txToRemove.Add(destTx);
                                }
                            }
                        }
                    }
                }

                foreach (TransactionData tx in txToRemove)
                {
                    foreach (HdAccount account in this.TumblingState.DestinationWallet.GetAccountsByCoinType(this.TumblingState.CoinType))
                    {
                        foreach (HdAddress address in account.FindAddressesForTransaction(transaction => transaction.Id == tx.Id))
                        {
                            address.Transactions.Remove(tx);
                        }
                    }
                }
            }

            // TumbleBit housekeeping
            this.TumblingState.LastBlockReceivedHeight = height;
            this.TumblingState.Save();
        }

        public string CalculateTumblingDuration(string originWalletName)
        {
            if (TumblerParameters == null)
                return string.Empty;

			/* Tumbling cycles occur up to 117 blocks (cycleDuration) and overlap every 24 blocks (cycleOverlap) :-

                Start block	End block
                ----------- ---------
                0	        117
                24	        141
                48	        165
             */
			const int cycleDuration = 117;
            const int cycleOverlap = 24;

	        Money walletBalance = this.runtime.Services.WalletService.GetBalance(originWalletName);
	        FeeRate networkFeeRate = this.runtime.Services.FeeService.GetFeeRateAsync().GetAwaiter().GetResult();

			if (!this.HasEnoughFundsForCycle(true, originWalletName))
		        return TimeSpanInWordFormat(0);

			var demonination = TumblerParameters.Denomination;
            var tumblerFee = TumblerParameters.Fee;
	        var networkFee = networkFeeRate.GetFee(TumblerClientRuntime.AverageClientEscrowTransactionSizeInBytes);

            var cycleCost = demonination + tumblerFee + networkFee;

            var numberOfCycles = Math.Truncate(walletBalance.ToUnit(MoneyUnit.BTC) / cycleCost.ToDecimal(MoneyUnit.BTC));
            var durationInBlocks = cycleDuration + ((numberOfCycles - 1) * cycleOverlap);
            var durationInHours = durationInBlocks * 10 / 60;

            return TimeSpanInWordFormat(durationInHours);
        }

	    public bool HasEnoughFundsForCycle(bool firstCycle, string originWalletName)
	    {
			Money walletBalance = this.runtime.Services.WalletService.GetBalance(originWalletName);
		    FeeRate networkFeeRate = this.runtime.Services.FeeService.GetFeeRateAsync().GetAwaiter().GetResult();

		    return TumblerClientRuntime.HasEnoughFundsForCycle(firstCycle, walletBalance, networkFeeRate, TumblerParameters.Denomination, TumblerParameters.Fee);
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

        public int LastBlockTime
        {
            get
            {
                try
                {
                    ChainedBlock chainedBlock = this.chain.Tip;
                    TimeSpan timespan = DateTimeOffset.UtcNow - chainedBlock.Header.BlockTime;
                    return timespan.Minutes;
                }
                catch (Exception)
                {
                    return -1;
                }
            }
        }

        private static string TimeSpanInWordFormat(decimal fromHours)
        {
	        if (fromHours == 0)
		        return "N/A";

            var timeSpan = TimeSpan.FromHours((double)fromHours);

            var days = timeSpan.Days.ToString();
            var hours = timeSpan.Hours.ToString();

            var formattedTimeSpan = string.Empty;

            if (timeSpan.Days > 0)
            {
                formattedTimeSpan = $"{days} days";

                if (timeSpan.Hours > 0)
                    formattedTimeSpan += $", and {hours} hours";
            }
            else
            {
                if (timeSpan.Hours > 0)
                    formattedTimeSpan = $"{hours} hours";
            }

            return formattedTimeSpan;
        }

        private void RemoveProgress()
        {
            // Remove the progress file from previous session as it is now stale
            string dataDir = TumblingState.NodeSettings.DataDir;
            string tumbleBitDataDir = FullNodeTumblerClientConfiguration.GetTumbleBitDataDir(dataDir);

            ProgressInfo.RemoveProgressFile(tumbleBitDataDir);
        }
    }

    static class List
    {
        private static Random rng = new Random();

        /// <summary>
        /// Shuffles a list randomly
        /// </summary>
        public static void Shuffle<T>(this IList<T> list)
        {
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = rng.Next(n + 1);
                T value = list[k];
                list[k] = list[n];
                list[n] = value;
            }
        }
    }
}
