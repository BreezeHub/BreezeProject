using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Newtonsoft.Json;
using NTumbleBit.ClassicTumbler;
using NTumbleBit.PuzzlePromise;
using NTumbleBit.PuzzleSolver;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.WatchOnlyWallet;

namespace Breeze.TumbleBit.Client
{
    public class TumblingState : IStateMachine
    {
        private const string StateFileName = "tumblebit_state.json";

        private readonly ILogger logger;
        private readonly ConcurrentChain chain;
        private readonly IWalletManager walletManager;
        private readonly IWatchOnlyWalletManager watchOnlyWalletManager;
        private readonly CoinType coinType;
        
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

        [JsonIgnore]
        public Wallet OriginWallet { get; set; }

        [JsonIgnore]
        public Wallet DestinationWallet { get; set; }
        
        [JsonConstructor]
        public TumblingState()
        {
        }

        public TumblingState(ILoggerFactory loggerFactory, 
            ConcurrentChain chain,
            IWalletManager walletManager,
            IWatchOnlyWalletManager  watchOnlyWalletManager,
            Network network)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.chain = chain;
            this.walletManager = walletManager;
            this.watchOnlyWalletManager = watchOnlyWalletManager;
            this.coinType = (CoinType)network.Consensus.CoinType;
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
            var savedState = JsonConvert.DeserializeObject<TumblingState>(File.ReadAllText(stateFilePath));
            
            this.OriginWalletName = savedState.OriginWalletName;
            this.DestinationWalletName = savedState.DestinationWalletName;
            this.LastBlockReceivedHeight = savedState.LastBlockReceivedHeight;
            this.TumblerParameters = savedState.TumblerParameters;
            this.TumblerUri = savedState.TumblerUri;
        }

        /// <inheritdoc />
        public void Delete()
        {
            var stateFilePath = GetStateFilePath();
            File.Delete(stateFilePath);
        }
        
        /// <summary>
        /// Gets the file path of the file containing the state of the tumbling execution.
        /// </summary>
        /// <returns></returns>
        private static string GetStateFilePath()
        {
            string defaultFolderPath;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                defaultFolderPath = $@"{Environment.GetEnvironmentVariable("AppData")}\Breeze\TumbleBit";
            }
            else
            {
                defaultFolderPath = $"{Environment.GetEnvironmentVariable("HOME")}/.breeze/TumbleBit";
            }

            // create the directory if it doesn't exist
            Directory.CreateDirectory(defaultFolderPath);
            return Path.Combine(defaultFolderPath, StateFileName);
        }
    }
}
