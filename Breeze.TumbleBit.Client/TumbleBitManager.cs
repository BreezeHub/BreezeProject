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
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.WatchOnlyWallet;
using Stratis.Bitcoin.Signals;
using NTumbleBit.Services;
using BreezeCommon;
using System.Collections.Generic;
using System.Text;

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

            this.registrationStore = new RegistrationStore(network);

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

            WalletAccountReference accountRef = new WalletAccountReference(originWalletName, highestAcc.Name);
            List<Recipient> recipients = new List<Recipient>();
            TransactionBuildContext txBuildContext = new TransactionBuildContext(accountRef, recipients);
            txBuildContext.WalletPassword = originWalletPassword;
            //txBuildContext.OverrideFeeRate = feeRate;
            txBuildContext.Sign = true;
            txBuildContext.MinConfirmations = 0;

            this.walletTransactionHandler.FundTransaction(txBuildContext, sendTx);
            this.walletManager.SendTransaction(sendTx.ToHex());
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

            // Check for any server registration transactions
            if (block.Transactions != null)
            {
                foreach (Transaction tx in block.Transactions)
                {
                    // Check if the transaction has the Breeze registration marker output
                    if (tx.Outputs[0].ScriptPubKey.ToHex().ToLower() == "6a1a425245455a455f524547495354524154494f4e5f4d41524b4552")
                    {
                        RegistrationToken registrationToken = new RegistrationToken();
                        registrationToken.ParseTransaction(tx, this.network);
                        MerkleBlock merkleBlock = new MerkleBlock(block, new uint256[] { tx.GetHash() });
                        RegistrationRecord registrationRecord = new RegistrationRecord(DateTime.Now, Guid.NewGuid(), tx.GetHash().ToString(), tx.ToHex(), registrationToken, merkleBlock);
                        this.registrationStore.Add(registrationRecord);
                    }
                }
            }
            
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
