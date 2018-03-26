using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BreezeCommon;
using Breeze.TumbleBit.Client;
using Breeze.Registration;
using Microsoft.Extensions.Logging.Console;
using NBitcoin;
using NBitcoin.Protocol;
using NLog;
using NLog.Config;
using NLog.Targets;
using NTumbleBit.Logging;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Features.Api;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.LightWallet;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.Features.Notifications;
using Stratis.Bitcoin.Features.RPC;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.WatchOnlyWallet;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Utilities.Extensions;
using Stratis.Bitcoin;
using System.Threading.Tasks;

namespace Breeze.Daemon
{
    public class Program
    {
        private const string DefaultBitcoinUri = "http://localhost:37220";
        private const string DefaultStratisUri = "http://localhost:37221";

        public static void Main(string[] args)
        {
            MainAsync(args).Wait();
        }

        public static async Task MainAsync(string[] args)
        {
            try
            { 
                var isTestNet = args.Contains("-testnet");
                var isStratis = args.Contains("stratis");
                var agent = "Breeze";

                // This setting is not in NodeSettings yet, so get it directly from the args
                ConfigurationOptionWrapper<string> registrationStoreDirectory = new ConfigurationOptionWrapper<string>("RegistrationStoreDirectory", args.GetValueOf("-storedir"));
                ConfigurationOptionWrapper<string>[] configurationOptions = { registrationStoreDirectory };

                NodeSettings nodeSettings;

                if (isStratis)
                {
                    //if (NodeSettings.PrintHelp(args, Network.StratisMain))
                    //    return;

                    Network network = isTestNet ? Network.StratisTest : Network.StratisMain;
                    if (isTestNet)
                        args = args.Append("-addnode=51.141.28.47").ToArray(); // TODO: fix this temp hack

                    nodeSettings = new NodeSettings(network, ProtocolVersion.ALT_PROTOCOL_VERSION, agent, args: args, loadConfiguration: false);
                }
                else
                {
                    nodeSettings = new NodeSettings(agent: agent, args: args, loadConfiguration: false);
                }

                IFullNodeBuilder fullNodeBuilder = null;

                if (args.Contains("light"))
                {
                    fullNodeBuilder = new FullNodeBuilder()
                        .UseNodeSettings(nodeSettings)
                        .UseLightWallet()
                        .UseWatchOnlyWallet()
                        .UseBlockNotification()
                        .UseTransactionNotification()
                        .UseApi();
                }
                else
                {
                    fullNodeBuilder = new FullNodeBuilder()
                        .UseNodeSettings(nodeSettings);

                    if (args.Contains("stratis"))
                        fullNodeBuilder.UsePosConsensus();
                    else
                        fullNodeBuilder.UsePowConsensus();

	                fullNodeBuilder.UseBlockStore()
		                .UseMempool()
		                .UseBlockNotification()
		                .UseTransactionNotification()
		                .UseWallet()
		                .UseWatchOnlyWallet();

	                if (args.Contains("stratis"))
		                fullNodeBuilder.AddPowPosMining();
	                else
		                fullNodeBuilder.AddMining();
					
					fullNodeBuilder.AddRPC()
                        .UseApi();
                }

                if (args.Contains("registration"))
                {
                    //fullNodeBuilder.UseInterNodeCommunication();
                    fullNodeBuilder.UseRegistration();
                }

                // Need this to happen for both TB and non-TB daemon
                Logs.Configure(new FuncLoggerFactory(i => new DualLogger(i, (a, b) => true, false)));

                // Start NTumbleBit logging to the console
                SetupTumbleBitConsoleLogs(nodeSettings);

                // Currently TumbleBit is bitcoin only
                if (args.Contains("-tumblebit"))
                {
                    // We no longer pass the URI in via the command line, the registration feature selects a random one
                    fullNodeBuilder.UseTumbleBit(configurationOptions);
                }
                
                IFullNode node = fullNodeBuilder.Build();

	            // Add logging to NLog
	            SetupTumbleBitNLogs(nodeSettings);

				// Start Full Node - this will also start the API.
				await node.RunAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine("There was a problem initializing the node. Details: '{0}'", ex.Message);
            }
        }

        private static void SetupTumbleBitConsoleLogs(NodeSettings nodeSettings)
        {
            // Switch Stratis.Bitcoin to Error level only so it does not flood the console
            var switches = new Dictionary<string, Microsoft.Extensions.Logging.LogLevel>()
            {
                {"Default", Microsoft.Extensions.Logging.LogLevel.Error},
                {"System", Microsoft.Extensions.Logging.LogLevel.Warning},
                {"Microsoft", Microsoft.Extensions.Logging.LogLevel.Warning},
                {"Microsoft.AspNetCore", Microsoft.Extensions.Logging.LogLevel.Error},
                {"Stratis.Bitcoin", Microsoft.Extensions.Logging.LogLevel.Information},
	            {"Stratis.Bitcoin.Features.WatchOnlyWallet.WatchOnlyWalletManager", Microsoft.Extensions.Logging.LogLevel.Information},
				{"Breeze.TumbleBit.Client", Microsoft.Extensions.Logging.LogLevel.Debug},
                {"Breeze.Registration", Microsoft.Extensions.Logging.LogLevel.Debug}
            };
            
            ConsoleLoggerSettings settings = nodeSettings.LoggerFactory.GetConsoleSettings();
            settings.Switches = switches;
            settings.Reload();
        }

        private static void SetupTumbleBitNLogs(NodeSettings nodeSettings)
        {
            NLog.Config.LoggingConfiguration config = LogManager.Configuration;
            var folder = Path.Combine(nodeSettings.DataDir, "Logs");

            var tbTarget = new FileTarget();
            tbTarget.Name = "tumblebit";
            tbTarget.FileName = Path.Combine(folder, "tumblebit.txt");
            tbTarget.ArchiveFileName = Path.Combine(folder, "tb-${date:universalTime=true:format=yyyy-MM-dd}.txt");
            tbTarget.ArchiveNumbering = ArchiveNumberingMode.Sequence;
            tbTarget.ArchiveEvery = FileArchivePeriod.Day;
            tbTarget.MaxArchiveFiles = 7;
            tbTarget.Layout = "[${longdate:universalTime=true} ${threadid}${mdlc:item=id}] ${level:uppercase=true}: ${callsite} ${message}";
            tbTarget.Encoding = Encoding.UTF8;

			// Attempt to log all information that is pertinent for debugging.
			// In particular, suppress the watch only wallet's debug-level entries as they are highly verbose.
	        config.LoggingRules.Add(new LoggingRule("Stratis.Bitcoin.Features.Api.*", NLog.LogLevel.Debug, tbTarget));
	        //config.LoggingRules.Add(new LoggingRule("Stratis.Bitcoin.Features.BlockStore.*", NLog.LogLevel.Error, tbTarget));
	        //config.LoggingRules.Add(new LoggingRule("Stratis.Bitcoin.Features.Consensus.*", NLog.LogLevel.Error, tbTarget));
	        config.LoggingRules.Add(new LoggingRule("Stratis.Bitcoin.Features.LightWallet.*", NLog.LogLevel.Debug, tbTarget));
	        //config.LoggingRules.Add(new LoggingRule("Stratis.Bitcoin.Features.MemoryPool.*", NLog.LogLevel.Error, tbTarget));
	        //config.LoggingRules.Add(new LoggingRule("Stratis.Bitcoin.Features.Notifications.*", NLog.LogLevel.Error, tbTarget));
	        config.LoggingRules.Add(new LoggingRule("Stratis.Bitcoin.Features.RPC.*", NLog.LogLevel.Error, tbTarget));
	        config.LoggingRules.Add(new LoggingRule("Stratis.Bitcoin.Features.Wallet.*", NLog.LogLevel.Debug, tbTarget));
	        config.LoggingRules.Add(new LoggingRule("Stratis.Bitcoin.Features.WatchOnlyWallet.*", NLog.LogLevel.Info, tbTarget));
	        
	        config.LoggingRules.Add(new LoggingRule("Stratis.Bitcoin.BlockPulling.*", NLog.LogLevel.Info, tbTarget)); // Has quite verbose Trace logs
			config.LoggingRules.Add(new LoggingRule("Stratis.Bitcoin.Connection.*", NLog.LogLevel.Info, tbTarget));
			config.LoggingRules.Add(new LoggingRule("Stratis.Bitcoin.FullNode", NLog.LogLevel.Info, tbTarget));
			config.LoggingRules.Add(new LoggingRule("Stratis.Bitcoin.P2P.*", NLog.LogLevel.Debug, tbTarget)); // Quite verbose Trace logs
			config.LoggingRules.Add(new LoggingRule("Stratis.Bitcoin.Utilities.*", NLog.LogLevel.Info, tbTarget));

	        config.LoggingRules.Add(new LoggingRule("api.request.logger", NLog.LogLevel.Trace, tbTarget)); // Shows incoming API requests. Errors should be trapped by feature logs

			// The log rules specific to Breeze Privacy Protocol and masternode functionality.
			// Note however that the NTB runtime performs its own logging internally, and it is non-trivial to override it.
			config.LoggingRules.Add(new LoggingRule("Breeze.TumbleBit.Client.*", NLog.LogLevel.Debug, tbTarget));
	        config.LoggingRules.Add(new LoggingRule("Breeze.Registration.*", NLog.LogLevel.Debug, tbTarget));
	        config.LoggingRules.Add(new LoggingRule("BreezeCommon.*", NLog.LogLevel.Debug, tbTarget));

			// Catch all for any remaining warnings/errors that slip through the filters
			config.LoggingRules.Add(new LoggingRule("*", NLog.LogLevel.Warn, tbTarget));

			config.AddTarget(tbTarget);

            // Apply new rules.
            LogManager.ReconfigExistingLoggers();
        }
    }
}
