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
using System.Runtime.InteropServices;
using NTumbleBit.ClassicTumbler.Server;
using NLogConfig = NLog.Config.LoggingConfiguration;

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
	            var comparer = new CommandlineArgumentComparer();

	            var isRegTest = args.Contains("regtest", comparer);
                var isTestNet = args.Contains("testnet", comparer);
				var isStratis = args.Contains("stratis", comparer);
				var isLight = args.Contains("light", comparer);

				var useRegistration = args.Contains("registration", comparer);
				var useTumblebit = args.Contains("tumblebit", comparer);
				var useTor = !args.Contains("noTor", comparer);
	            string registrationStoreDirectoryPath = args.GetValueOf("-storedir");

				TumblerProtocolType tumblerProtocol;
	            try
	            {
		            string tumblerProtocolString = args.GetValueOf("-tumblerProtocol");
		            if (!isRegTest && (tumblerProtocolString != null || !useTor))
		            {
			            Console.WriteLine("Options -TumblerProtocol and -NoTor can only be used in combination with -RegTest switch.");
						return;
		            }

		            if (tumblerProtocolString != null)
			            tumblerProtocol = Enum.Parse<TumblerProtocolType>(tumblerProtocolString, true);
		            else
			            tumblerProtocol = TumblerProtocolType.Tcp;

		            if (useTor && tumblerProtocol == TumblerProtocolType.Http)
		            {
			            Console.WriteLine("TumblerProtocol can only be changed to Http when Tor is disabled. Please use -NoTor switch to disable Tor.");
			            return;
					}
				}
	            catch
	            {
					Console.WriteLine($"Incorrect tumbling prococol specified; the valid values are {TumblerProtocolType.Tcp} and {TumblerProtocolType.Http}");
					return;
	            }

				var agent = "Breeze";

				NodeSettings nodeSettings;

                if (isStratis)
                {
                    //if (NodeSettings.PrintHelp(args, Network.StratisMain))
                    //    return;

                    Network network;
	                if (isRegTest)
	                {
		                network = Network.StratisRegTest;
	                }
                    else if (isTestNet)
	                {
		                args = args.Append("-addnode=51.141.28.47").ToArray(); // TODO: fix this temp hack
		                network = Network.StratisTest;
					}
	                else
	                {
		                network = Network.StratisMain;
					}

                    nodeSettings = new NodeSettings(network, ProtocolVersion.ALT_PROTOCOL_VERSION, agent, args: args, loadConfiguration: false);
                }
                else
                {
                    nodeSettings = new NodeSettings(agent: agent, args: args, loadConfiguration: false);
                }

                IFullNodeBuilder fullNodeBuilder = null;

                if (isLight)
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

                    if (isStratis)
                        fullNodeBuilder.UsePosConsensus();
                    else
                        fullNodeBuilder.UsePowConsensus();

	                fullNodeBuilder.UseBlockStore()
		                .UseMempool()
		                .UseBlockNotification()
		                .UseTransactionNotification()
		                .UseWallet()
		                .UseWatchOnlyWallet();

	                if (isStratis)
		                fullNodeBuilder.AddPowPosMining();
	                else
		                fullNodeBuilder.AddMining();
					
					fullNodeBuilder.AddRPC()
                        .UseApi();
                }

                if (useRegistration)
                {
					//fullNodeBuilder.UseInterNodeCommunication();
					fullNodeBuilder.UseRegistration();
                }

                // Need this to happen for both TB and non-TB daemon
                string dataDir = nodeSettings.DataDir;
                if (string.IsNullOrEmpty(dataDir))
                {
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                        dataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "StratisNode");
                    else
                        dataDir = Path.Combine(Environment.GetEnvironmentVariable("HOME"), ".stratisnode");
                }

                string logDir = Path.Combine(dataDir, nodeSettings.Network.RootFolderName, nodeSettings.Network.Name, "Logs");
                Logs.Configure(new FuncLoggerFactory(i => new DualLogger(i, (a, b) => true, false)), logDir);

                // Start NTumbleBit logging to the console
                SetupTumbleBitConsoleLogs(nodeSettings);

                // Currently TumbleBit is bitcoin only
                if (useTumblebit)
                {
                    if (string.IsNullOrEmpty(registrationStoreDirectoryPath))
                    {
                        string networkName;
                        if (isRegTest)
                            networkName = "StratisRegTest";
                        else if (isTestNet)
                            networkName = "StratisTest";
                        else
                            networkName = "StratisMain";

                        registrationStoreDirectoryPath = Path.Combine(dataDir, "stratis", networkName, "registrationHistory.json");
                    }
                    
                    // Those settings are not in NodeSettings yet, so get it directly from the args
                    ConfigurationOptionWrapper<object> registrationStoreDirectory = new ConfigurationOptionWrapper<object>("RegistrationStoreDirectory", registrationStoreDirectoryPath);
                    ConfigurationOptionWrapper<object> torOption = new ConfigurationOptionWrapper<object>("Tor", useTor);
                    ConfigurationOptionWrapper<object> tumblerProtocolOption = new ConfigurationOptionWrapper<object>("TumblerProtocol", tumblerProtocol);
                    ConfigurationOptionWrapper<object> useDummyAddressOption = new ConfigurationOptionWrapper<object>("UseDummyAddress", true);

                    ConfigurationOptionWrapper<object>[] tumblebitConfigurationOptions = { registrationStoreDirectory, torOption, tumblerProtocolOption, useDummyAddressOption };


                    // We no longer pass the URI in via the command line, the registration feature selects a random one
                    fullNodeBuilder.UseTumbleBit(tumblebitConfigurationOptions);
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
				{"Breeze.TumbleBit.Client", Microsoft.Extensions.Logging.LogLevel.Information},
                {"Breeze.Registration", Microsoft.Extensions.Logging.LogLevel.Information}
            };
            
            ConsoleLoggerSettings settings = nodeSettings.LoggerFactory.GetConsoleSettings();
            settings.Switches = switches;
            settings.Reload();
        }

        private static void SetupTumbleBitNLogs(NodeSettings nodeSettings)
        {
            NLogConfig config = LogManager.Configuration;
            var folder = Path.Combine(nodeSettings.DataDir, "Logs");

            var tbTarget = new FileTarget
            {
                Name = "tumblebit",
                FileName = Path.Combine(folder, "tumblebit.txt"),
                ArchiveFileName = Path.Combine(folder, "tb-${date:universalTime=true:format=yyyy-MM-dd}.txt"),
                ArchiveNumbering = ArchiveNumberingMode.Sequence,
                ArchiveEvery = FileArchivePeriod.Day,
                MaxArchiveFiles = 7,
                Layout =
                    "[${longdate:universalTime=true} ${threadid}${mdlc:item=id}] ${level:uppercase=true}: ${callsite} ${message}",
                Encoding = Encoding.UTF8
            };
            
            SetupLogs(config, tbTarget);

            config.AddTarget(tbTarget);

            // Apply new rules.
            LogManager.ReconfigExistingLoggers();
        }

        private static void SetupLogs(NLogConfig config, FileTarget tbTarget)
        {
            SetupLogDebug(config, tbTarget, LogLevel.Debug);
            SetupLogError(config, tbTarget, LogLevel.Error);
            SetupLogInfo(config, tbTarget, LogLevel.Info);
            SetupLogFatal(config, tbTarget, LogLevel.Fatal);
            SetupLogWarning(config, tbTarget, LogLevel.Warn);
        }

        private static void SetupLogWarning(NLogConfig config, FileTarget tbTarget, LogLevel logLevel)
        {
            // Catch all for any remaining warnings/errors that slip through the filters
            config.LoggingRules.Add(new LoggingRule("*", logLevel, tbTarget));
        }

        private static void SetupLogInfo(NLogConfig config, FileTarget tbTarget, LogLevel logLevel)
        {
            config.LoggingRules.Add(new LoggingRule("Stratis.Bitcoin.Features.WatchOnlyWallet.*", logLevel, tbTarget));

            config.LoggingRules.Add(new LoggingRule("Stratis.Bitcoin.BlockPulling.*", logLevel, tbTarget)); // Has quite verbose Trace logs
            config.LoggingRules.Add(new LoggingRule("Stratis.Bitcoin.Connection.*", logLevel, tbTarget));
            config.LoggingRules.Add(new LoggingRule("Stratis.Bitcoin.FullNode", logLevel, tbTarget));
            config.LoggingRules.Add(new LoggingRule("Stratis.Bitcoin.Utilities.*", logLevel, tbTarget));

            config.LoggingRules.Add(new LoggingRule("api.request.logger", LogLevel.Info, tbTarget)); // Shows incoming API requests. Errors should be trapped by feature logs

            // The log rules specific to Breeze Privacy Protocol and masternode functionality.
            // Note however that the NTB runtime performs its own logging internally, and it is non-trivial to override it.
            config.LoggingRules.Add(new LoggingRule("Breeze.TumbleBit.Client.*", logLevel, tbTarget));
            config.LoggingRules.Add(new LoggingRule("Breeze.Registration.*", logLevel, tbTarget));
        }

        private static void SetupLogError(NLogConfig config, FileTarget tbTarget, LogLevel logLevel)
        {
            config.LoggingRules.Add(new LoggingRule("Stratis.Bitcoin.Features.RPC.*", logLevel, tbTarget));
            config.LoggingRules.Add(new LoggingRule("Breeze.*", logLevel, tbTarget));
            
        }

        private static void SetupLogFatal(NLogConfig config, FileTarget tbTarget, LogLevel logLevel)
        {
            config.LoggingRules.Add(new LoggingRule("Breeze.*", logLevel, tbTarget));
        }

        private static void SetupLogDebug(NLogConfig config, FileTarget tbTarget, LogLevel logLevel)
        {
            config.LoggingRules.Add(new LoggingRule("Stratis.Bitcoin.Features.Api.*", logLevel, tbTarget));
            config.LoggingRules.Add(new LoggingRule("Stratis.Bitcoin.Features.LightWallet.*", logLevel, tbTarget));
            config.LoggingRules.Add(new LoggingRule("Stratis.Bitcoin.Features.Wallet.*", logLevel, tbTarget));
            config.LoggingRules.Add(new LoggingRule("Stratis.Bitcoin.P2P.*", logLevel, tbTarget)); // Quite verbose Trace logs
            config.LoggingRules.Add(new LoggingRule("BreezeCommon.*", logLevel, tbTarget));
            config.LoggingRules.Add(new LoggingRule("NTumbleBit.*", logLevel, tbTarget));
        }
    }
}
