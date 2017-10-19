using Breeze.TumbleBit;
using Microsoft.Extensions.Logging.Console;
using NBitcoin;
using NBitcoin.Protocol;
using NLog;
using NLog.Config;
using NLog.Targets;
using NTumbleBit.Logging;
using Stratis.Bitcoin.Api;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.MasterNode.Features.InterNodeComms;
using Stratis.Bitcoin.Features.LightWallet;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.Features.Notifications;
using Stratis.Bitcoin.Features.RPC;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.WatchOnlyWallet;
using Stratis.Bitcoin.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Target = NBitcoin.Target;

namespace Breeze.Daemon
{
    public class Program
    {
        private const string DefaultBitcoinUri = "http://localhost:5000";
        private const string DefaultStratisUri = "http://localhost:5105";

        public static void Main(string[] args)
        {
            IFullNodeBuilder fullNodeBuilder = null;

            // get the api uri 
            var apiUri = args.GetValueOf("apiuri");

            if (args.Contains("stratis"))
            {
                if (NodeSettings.PrintHelp(args, Network.StratisMain))
                    return;

                var network = args.Contains("-testnet") ? Network.StratisTest : Network.StratisMain;

                if (args.Contains("-regtest"))
                    network = Network.StratisRegTest;

                if (args.Contains("-testnet"))
                    args = args.Append("-addnode=13.64.76.48").ToArray(); // TODO: fix this temp hack 
                var nodeSettings = NodeSettings.FromArguments(args, "stratis", network, ProtocolVersion.ALT_PROTOCOL_VERSION);
                nodeSettings.ApiUri = new Uri(string.IsNullOrEmpty(apiUri) ? DefaultStratisUri : apiUri);

                if (args.Contains("light"))
                {
                    fullNodeBuilder = new FullNodeBuilder()
                        .UseNodeSettings(nodeSettings)
                        .UseLightWallet()
                        .UseWatchOnlyWallet()
                        .UseBlockNotification()
                        .UseTransactionNotification()
                        .UseInterNodeCommunication()
                        .UseApi();

                    //currently tumblebit is bitcoin only
                    if (args.Contains("-tumblebit"))
                    {
                        Logs.Configure(new FuncLoggerFactory(i => new DualLogger(i, (a, b) => true, false)));

                        //start NTumbleBit logging to the console
                        //and switch the full node to log level: 
                        //error only
                        SetupTumbleBitConsoleLogs(nodeSettings);

                        //add logging to NLog
                        SetupTumbleBitNLogs(nodeSettings);

                        //var tumblerAddress = args.GetValueOf("-ppuri");
                        //if (tumblerAddress != null)
                        //    nodeSettings.TumblerAddress = tumblerAddress;


                        //we no longer pass the cbt uri in on the command line
                        //we always get it from the config. 
                        fullNodeBuilder.UseTumbleBit();
                    }
                }
                else
                {
                    fullNodeBuilder = new FullNodeBuilder()
                        .UseNodeSettings(nodeSettings)
                        .UseConsensus()
                        .UseBlockStore()
                        .UseMempool()
                        .UseBlockNotification()
                        .UseTransactionNotification()
                        .UseWallet()
                        .UseWatchOnlyWallet()
                        .UseInterNodeCommunication()
                        .AddMining()
                        .AddRPC()
                        .UseApi();

                    //currently tumblebit is bitcoin only
                    if (args.Contains("-tumblebit"))
                    {
                        Logs.Configure(new FuncLoggerFactory(i => new DualLogger(i, (a, b) => true, false)));

                        //start NTumbleBit logging to the console
                        //and switch the full node to log level: 
                        //error only
                        SetupTumbleBitConsoleLogs(nodeSettings);

                        //add logging to NLog
                        SetupTumbleBitNLogs(nodeSettings);

                        // TODO: Put this back in
                        //var tumblerAddress = args.GetValueOf("-ppuri");
                        //if (tumblerAddress != null)
                        //    nodeSettings.TumblerAddress = tumblerAddress;


                        //we no longer pass the cbt uri in on the command line
                        //we always get it from the config. 
                        fullNodeBuilder.UseTumbleBit();
                    }
                }
            }
            else
            {
                NodeSettings nodeSettings = NodeSettings.FromArguments(args);
                nodeSettings.ApiUri = new Uri(string.IsNullOrEmpty(apiUri) ? DefaultBitcoinUri : apiUri);

                if (args.Contains("light"))
                {
                    fullNodeBuilder = new FullNodeBuilder()
                        .UseNodeSettings(nodeSettings)
                        .UseLightWallet()
                        .UseWatchOnlyWallet()
                        .UseBlockNotification()
                        .UseTransactionNotification()
                        .UseInterNodeCommunication()
                        .UseApi();

                    //currently tumblebit is bitcoin only
                    if (args.Contains("-tumblebit"))
                    {
                        Logs.Configure(new FuncLoggerFactory(i => new DualLogger(i, (a, b) => true, false)));

                        //start NTumbleBit logging to the console
                        //and switch the full node to log level: 
                        //error only
                        SetupTumbleBitConsoleLogs(nodeSettings);

                        //add logging to NLog
                        SetupTumbleBitNLogs(nodeSettings);

                        //var tumblerAddress = args.GetValueOf("-ppuri");
                        //if (tumblerAddress != null)
                        //    nodeSettings.TumblerAddress = tumblerAddress;


                        //we no longer pass the cbt uri in on the command line
                        //we always get it from the config. 
                        fullNodeBuilder.UseTumbleBit();
                    }
                }
                else
                {
                    fullNodeBuilder = new FullNodeBuilder()
                        .UseNodeSettings(nodeSettings)
                        .UseConsensus()
                        .UseBlockStore()
                        .UseMempool()
                        .UseBlockNotification()
                        .UseTransactionNotification()
                        .UseWallet()
                        .UseWatchOnlyWallet()
                        .UseInterNodeCommunication()
                        .AddMining()
                        .AddRPC()
                        .UseApi();

                    //currently tumblebit is bitcoin only
                    if (args.Contains("-tumblebit"))
                    {
                        Logs.Configure(new FuncLoggerFactory(i => new DualLogger(i, (a, b) => true, false)));

                        //start NTumbleBit logging to the console
                        //and switch the full node to log level: 
                        //error only
                        SetupTumbleBitConsoleLogs(nodeSettings);

                        //add logging to NLog
                        SetupTumbleBitNLogs(nodeSettings);

                        //var tumblerAddress = args.GetValueOf("-ppuri");
                        //if (tumblerAddress != null)
                        //    nodeSettings.TumblerAddress = tumblerAddress;


                        //we no longer pass the cbt uri in on the command line
                        //we always get it from the config. 
                        fullNodeBuilder.UseTumbleBit();
                    }
                }
            }

            var node = fullNodeBuilder.Build();

            //start Full Node - this will also start the API
            node.Run();
        }

        private static void SetupTumbleBitConsoleLogs(NodeSettings nodeSettings)
        {
            //switch Stratis.Bitcoin to Error level only so it does not flood the console
            var switches = new Dictionary<string, Microsoft.Extensions.Logging.LogLevel>()
            {
                {"Default", Microsoft.Extensions.Logging.LogLevel.Information},
                {"System", Microsoft.Extensions.Logging.LogLevel.Warning},
                {"Microsoft", Microsoft.Extensions.Logging.LogLevel.Warning},
                {"Microsoft.AspNetCore", Microsoft.Extensions.Logging.LogLevel.Error},
                {"Stratis.Bitcoin", Microsoft.Extensions.Logging.LogLevel.Information},
                {"Breeze.TumbleBit.Client", Microsoft.Extensions.Logging.LogLevel.Information}
            };
            ConsoleLoggerSettings settings = nodeSettings.LoggerFactory.GetConsoleSettings();
            settings.Switches = switches;
            settings.Reload();
        }

        private static void SetupTumbleBitNLogs(NodeSettings nodeSettings)
        {
            var config = LogManager.Configuration;
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

            var ruleTb = new LoggingRule("*", NLog.LogLevel.Info, tbTarget);
            config.LoggingRules.Add(ruleTb);

            config.AddTarget(tbTarget);

            // Apply new rules.
            LogManager.ReconfigExistingLoggers();
        }
    }
}
