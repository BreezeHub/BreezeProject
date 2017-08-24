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
using Stratis.Bitcoin.Features.LightWallet;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.Features.Notifications;
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

                var network = args.Contains("-testnet") ? InitStratisTest() : Network.StratisMain;
                if (args.Contains("-testnet"))
                    args = args.Append("-addnode=13.64.76.48").ToArray(); // TODO: fix this temp hack 
                var nodeSettings = NodeSettings.FromArguments(args, "stratis", network, ProtocolVersion.ALT_PROTOCOL_VERSION);                
                nodeSettings.ApiUri = new Uri(string.IsNullOrEmpty(apiUri) ? DefaultStratisUri : apiUri);

                if (args.Contains("light"))
                {
                    fullNodeBuilder = new FullNodeBuilder()
                        .UseNodeSettings(nodeSettings)
                        .UseLightWallet()
                        .UseBlockNotification()
                        .UseTransactionNotification()
                        .UseApi();
                }
                else
                {
                    fullNodeBuilder = new FullNodeBuilder()
                        .UseNodeSettings(nodeSettings)
                        .UseStratisConsensus()
                        .UseBlockStore()
                        .UseMempool()
                        .UseWallet()
                        .AddPowPosMining()
                        .UseApi();
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

                        //we no longer pass the cbt uri in on the command line
                        //we always get it from the config. 
                        fullNodeBuilder.UseTumbleBit(null);
                    }
                }
                else
                {
                    fullNodeBuilder = new FullNodeBuilder()
                        .UseNodeSettings(nodeSettings)
                        .UseConsensus()
                        .UseBlockStore()
                        .UseMempool()
                        .UseWallet()
                        .UseApi();
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
                {"Stratis.Bitcoin", Microsoft.Extensions.Logging.LogLevel.Error}
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

        private static Network InitStratisTest()
        {
            Block.BlockSignature = true;
            Transaction.TimeStamp = true;

            var consensus = Network.StratisMain.Consensus.Clone();
            consensus.PowLimit = new Target(uint256.Parse("0000ffff00000000000000000000000000000000000000000000000000000000"));

            // The message start string is designed to be unlikely to occur in normal data.
            // The characters are rarely used upper ASCII, not valid as UTF-8, and produce
            // a large 4-byte int at any alignment.
            var pchMessageStart = new byte[4];
            pchMessageStart[0] = 0x71;
            pchMessageStart[1] = 0x31;
            pchMessageStart[2] = 0x21;
            pchMessageStart[3] = 0x11;
            var magic = BitConverter.ToUInt32(pchMessageStart, 0); //0x5223570; 

            var genesis = Network.StratisMain.GetGenesis().Clone();
            genesis.Header.Time = 1493909211;
            genesis.Header.Nonce = 2433759;
            genesis.Header.Bits = consensus.PowLimit;
            consensus.HashGenesisBlock = genesis.GetHash();

            Guard.Assert(consensus.HashGenesisBlock == uint256.Parse("0x00000e246d7b73b88c9ab55f2e5e94d9e22d471def3df5ea448f5576b1d156b9"));

            var builder = new NetworkBuilder()
                .SetName("StratisTest")
                .SetConsensus(consensus)
                .SetMagic(magic)
                .SetGenesis(genesis)
                .SetPort(26178)
                .SetRPCPort(26174)
                .SetBase58Bytes(Base58Type.PUBKEY_ADDRESS, new byte[] { (65) })
                .SetBase58Bytes(Base58Type.SCRIPT_ADDRESS, new byte[] { (196) })
                .SetBase58Bytes(Base58Type.SECRET_KEY, new byte[] { (65 + 128) })
                .SetBase58Bytes(Base58Type.ENCRYPTED_SECRET_KEY_NO_EC, new byte[] { 0x01, 0x42 })
                .SetBase58Bytes(Base58Type.ENCRYPTED_SECRET_KEY_EC, new byte[] { 0x01, 0x43 })
                .SetBase58Bytes(Base58Type.EXT_PUBLIC_KEY, new byte[] { (0x04), (0x88), (0xB2), (0x1E) })
                .SetBase58Bytes(Base58Type.EXT_SECRET_KEY, new byte[] { (0x04), (0x88), (0xAD), (0xE4) })
                .AddDNSSeeds(new[]
                {
                    new DNSSeedData("stratisplatform.com", "testnode1.stratisplatform.com"),
                });

            return builder.BuildAndRegister();
        }

    }
}
