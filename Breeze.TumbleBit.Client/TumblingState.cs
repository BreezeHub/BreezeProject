    using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using NBitcoin;
    using NBitcoin.JsonConverters;
    using Newtonsoft.Json;
using NTumbleBit.ClassicTumbler;
using Stratis.Bitcoin.Configuration;
    using Stratis.Bitcoin.Connection;
    using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.WatchOnlyWallet;
using Stratis.Bitcoin.Features.Wallet.Interfaces;

namespace Breeze.TumbleBit.Client
{
    public class TumblingState : IStateMachine
    {
        private const string StateFileName = "tumblebit_state.json";

        [JsonIgnore]
        public ILogger Logger;

        [JsonIgnore]
        public ConcurrentChain Chain;

        [JsonIgnore]
        public WalletManager WalletManager;

        [JsonIgnore]
        public WalletSyncManager WalletSyncManager;

        [JsonIgnore]
        public IWatchOnlyWalletManager WatchOnlyWalletManager;

        [JsonIgnore]
        public WalletTransactionHandler WalletTransactionHandler;

        [JsonIgnore]
        public bool IsConnected { get; set; }

        // TODO: Does this need to be saved? Can be derived from network
        public CoinType CoinType;

        // TODO: Remove or store the tumbler parameters for every used tumbler
        [JsonProperty("tumblerParameters")]
        public ClassicTumblerParameters TumblerParameters { get; set; }

        [JsonProperty("tumblerUri")]
        public Uri TumblerUri { get; set; }

        [JsonProperty("lastBlockReceivedHeight", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public int LastBlockReceivedHeight { get; set; }

        [JsonProperty("originWalletName", NullValueHandling = NullValueHandling.Ignore)]
        public string OriginWalletName { get; set; }

        [JsonProperty("destinationWalletName", NullValueHandling = NullValueHandling.Ignore)]
        public string DestinationWalletName { get; set; }       

        [JsonProperty("network", NullValueHandling = NullValueHandling.Ignore)]
        [JsonConverter(typeof(NetworkJsonConverter))]
        public Network TumblerNetwork { get; set; }

        [JsonIgnore]
        public Wallet OriginWallet { get; set; }

        [JsonIgnore]
        public Wallet DestinationWallet { get; set; }

        [JsonIgnore]
        public string OriginWalletPassword { get; set; }

        [JsonIgnore]
        public IWalletFeePolicy WalletFeePolicy { get; set; }

        [JsonIgnore]
        public NodeSettings NodeSettings { get; set; }

        [JsonIgnore]
        public IBroadcasterManager BroadcasterManager { get; set; }

        [JsonIgnore]
        public ConnectionManager ConnectionManager { get; set; }

        [JsonConstructor]
        public TumblingState()
        {
        }

        public TumblingState(ILoggerFactory loggerFactory, 
            ConcurrentChain chain,
            WalletManager walletManager,
            IWatchOnlyWalletManager  watchOnlyWalletManager,
            Network network, 
            WalletTransactionHandler walletTransactionHandler,
            WalletSyncManager walletSyncManager,
            IWalletFeePolicy walletFeePolicy,
            NodeSettings nodeSettings,
            IBroadcasterManager broadcasterManager,
            ConnectionManager connectionManager)
        {
            this.Logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.Chain = chain;
            this.WalletManager = walletManager;
            this.WatchOnlyWalletManager = watchOnlyWalletManager;
            this.CoinType = (CoinType)network.Consensus.CoinType;
            this.WalletTransactionHandler = walletTransactionHandler;
            this.WalletSyncManager = walletSyncManager;
            this.TumblerNetwork = network;
            this.WalletFeePolicy = walletFeePolicy;
            this.NodeSettings = nodeSettings;
            this.BroadcasterManager = broadcasterManager;
            this.ConnectionManager = connectionManager;
        }

        /// <inheritdoc />
        public void Save()
        {
            File.WriteAllText(GetStateFilePath(), JsonConvert.SerializeObject(this));
        }

        /// <inheritdoc />
        public void LoadStateFromMemory()
        {
            var stateFilePath = GetStateFilePath();
            if (!File.Exists(stateFilePath))
            {
                return;
            }

            // load the file from the local system
            TumblingState savedState = JsonConvert.DeserializeObject<TumblingState>(File.ReadAllText(stateFilePath));
            
            this.OriginWalletName = savedState.OriginWalletName;
            this.DestinationWalletName = savedState.DestinationWalletName;
            this.LastBlockReceivedHeight = savedState.LastBlockReceivedHeight;
            this.TumblerParameters = savedState.TumblerParameters;
            this.TumblerUri = savedState.TumblerUri;
            this.TumblerNetwork = savedState.TumblerNetwork;
        }

        /// <inheritdoc />
        public void Delete()
        {
            string stateFilePath = GetStateFilePath();
            File.Delete(stateFilePath);
        }
        
        /// <summary>
        /// Gets the file path of the file containing the state of the tumbling execution.
        /// </summary>
        /// <returns></returns>
        public string GetStateFilePath()
        {
            return Path.Combine(this.NodeSettings.DataDir, StateFileName);
        }
    }
}
