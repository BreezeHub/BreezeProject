using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NLog;
using NLog.Config;
using NLog.Targets;
using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

using NBitcoin;
using Stratis.Bitcoin;
using Stratis.Bitcoin.Features.Api;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.LightWallet;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.Features.Notifications;
using Stratis.Bitcoin.Features.RPC;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Features.WatchOnlyWallet;
using Stratis.Bitcoin.IntegrationTests;

using Breeze.BreezeServer;
using Breeze.BreezeServer.Services;
using Breeze.TumbleBit.Models;
using BreezeCommon;
using NTumbleBit.Logging;
using Stratis.Bitcoin.IntegrationTests.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.Utilities.Extensions;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace Breeze.TumbleBit.Client.Tests
{
    public class Tests
    {
        [Fact]
        public void TestWithTor()
        {
            // Workaround for segwit not correctly activating
            Network.RegTest.Consensus.BIP9Deployments[BIP9Deployments.Segwit] = new BIP9DeploymentsParameters(1, 0, DateTime.Now.AddDays(50).ToUnixTimestamp());

            using (NodeBuilder builder = NodeBuilder.Create(version: "0.15.1"))
            {
                HttpClient client = null;

                var coreNode = builder.CreateNode(false);

                // This line has no effect currently as the changes to the config get overwritten
                coreNode.ConfigParameters.AddOrReplace("printtoconsole", "0");

                coreNode.ConfigParameters.AddOrReplace("debug", "1");
                //coreNode.ConfigParameters.AddOrReplace("prematurewitness", "1");
                //coreNode.ConfigParameters.AddOrReplace("walletprematurewitness", "1");
                coreNode.ConfigParameters.AddOrReplace("rpcworkqueue", "100");

                coreNode.Start();

                // Replicate portions of BreezeServer's Program.cs. Maybe refactor it into a class/function in future
                var serviceProvider = new ServiceCollection()
                    .AddLogging()
                    .AddSingleton<Breeze.BreezeServer.Services.ITumblerService, Breeze.BreezeServer.Services.TumblerService>()
                    .BuildServiceProvider();

                serviceProvider
                    .GetService<ILoggerFactory>()
                    .AddConsole(LogLevel.Debug);

                // Skip the registration code - that can be tested separately

                string configPath = Path.Combine(coreNode.DataFolder, "breeze.conf");
                string[] breezeServerConfig =
                {
                    "network=regtest", // Only the network setting is currently used from this file
                    "rpc.user=dummy",
                    "rpc.password=dummy",
                    "rpc.url=http://127.0.0.1:26174/",
                    "breeze.ipv4=127.0.0.1",
                    "breeze.ipv6=2001:0db8:85a3:0000:0000:8a2e:0370:7334",
                    "breeze.onion=0123456789ABCDEF",
                    "breeze.port=37123",
                    "breeze.regtxfeevalue=10000",
                    "breeze.regtxoutputvalue=1000",
                    "tumbler.url=http://127.0.0.1:37123/api/v1/",
                    "tumbler.rsakeyfile=/Users/username/.ntumblebitserver/RegTest/Tumbler.pem",
                    "tumbler.ecdsakeyaddress=TVwRFmEKRCnQAgShf3QshBjp1Tmucm1e87"
                };
                File.WriteAllLines(configPath, breezeServerConfig);

                BreezeConfiguration config = new BreezeConfiguration(configPath);

                var coreRpc = coreNode.CreateRPCClient();
                string ntbServerConfigPath = Path.Combine(coreNode.DataFolder, "server.config");
                string[] ntbServerConfig =
                {
                    "regtest=1",
                    "rpc.url=http://127.0.0.1:" + coreRpc.Address.Port + "/",
                    "rpc.user=" + coreRpc.CredentialString.UserPassword.UserName,
                    "rpc.password=" + coreRpc.CredentialString.UserPassword.Password,
                    //"cycle=kotori",
                    "tor.enabled=true",
                    "tor.server=127.0.0.1:9051" // We assume for now that tor has been manually started
                };

                File.WriteAllLines(ntbServerConfigPath, ntbServerConfig);

                // We need to start up the masternode prior to creating the SBFN instance so that
                // we have the URI available for starting the TumbleBit feature
                // TODO: Also need to see if NTB interactive console interferes with later parts of the test
                new Thread(delegate ()
                {
                    Thread.CurrentThread.IsBackground = true;
                    // By instantiating the TumblerService directly the registration logic is skipped
                    var tumbler = serviceProvider.GetService<Breeze.BreezeServer.Services.ITumblerService>();
                    tumbler.StartTumbler(config, false, "server.config", Path.GetFullPath(coreNode.DataFolder), false);
                }).Start();

                // Wait for URI file to be written out by the TumblerService
                while (!File.Exists(Path.Combine(coreNode.DataFolder, "uri.txt")))
                {
                    Thread.Sleep(1000);
                }

                Console.WriteLine("* URI file detected *");
                Thread.Sleep(5000);

                var serverAddress = File.ReadAllText(Path.Combine(coreNode.DataFolder, "uri.txt"));

                // Not used for this test
                ConfigurationOptionWrapper<string> registrationStoreDirectory = new ConfigurationOptionWrapper<string>("RegistrationStoreDirectory", "");

                // Force SBFN to connect to the server
                ConfigurationOptionWrapper<string> masternodeUri = new ConfigurationOptionWrapper<string>("MasterNodeUri", serverAddress);
                ConfigurationOptionWrapper<string>[] configurationOptions = { registrationStoreDirectory, masternodeUri };

                // Logging for NTB client code
                ConsoleLoggerProcessor loggerProcessor = new ConsoleLoggerProcessor();
                Logs.Configure(new FuncLoggerFactory(i => new CustomerConsoleLogger(i, Logs.SupportDebug(true), false, loggerProcessor)));

                CoreNode node1 = builder.CreateStratisPowNode(true, fullNodeBuilder =>
                {
                    fullNodeBuilder
                        .UsePosConsensus()
                        .UseBlockStore()
                        .UseMempool()
                        .UseBlockNotification()
                        .UseTransactionNotification()
                        .AddMining()
                        .UseWallet()
                        .UseWatchOnlyWallet()
                        .UseApi()
                        .AddRPC()
                        .UseTumbleBit(configurationOptions);
                });

                var apiSettings = node1.FullNode.NodeService<ApiSettings>();

                NLog.Config.LoggingConfiguration config1 = LogManager.Configuration;
                var folder = Path.Combine(node1.DataFolder, "Logs");

                var tbTarget = new FileTarget();
                tbTarget.Name = "tumblebit";
                tbTarget.FileName = Path.Combine(folder, "tumblebit.txt");
                tbTarget.ArchiveFileName = Path.Combine(folder, "tb-${date:universalTime=true:format=yyyy-MM-dd}.txt");
                tbTarget.ArchiveNumbering = ArchiveNumberingMode.Sequence;
                tbTarget.ArchiveEvery = FileArchivePeriod.Day;
                tbTarget.MaxArchiveFiles = 7;
                tbTarget.Layout = "[${longdate:universalTime=true} ${threadid}${mdlc:item=id}] ${level:uppercase=true}: ${callsite} ${message}";
                tbTarget.Encoding = Encoding.UTF8;

                var ruleTb = new LoggingRule("*", NLog.LogLevel.Debug, tbTarget);
                config1.LoggingRules.Add(ruleTb);

                config1.AddTarget(tbTarget);

                // Apply new rules.
                LogManager.ReconfigExistingLoggers();

                //node1.NotInIBD();

                // Create the source and destination wallets
                var wm1 = node1.FullNode.NodeService<IWalletManager>() as WalletManager;
                //var wm2 = node2.FullNode.NodeService<IWalletManager>() as WalletManager;
                wm1.CreateWallet("TumbleBit1", "alice");
                wm1.CreateWallet("TumbleBit1", "bob");

                // Mined coins only mature after 100 blocks on regtest
                // Additionally, we need to force Segwit to activate in order for NTB to work correctly
                coreRpc.Generate(450);

                var rpc1 = node1.CreateRPCClient();
                //var rpc2 = node2.CreateRPCClient();

                coreRpc.AddNode(node1.Endpoint, false);
                rpc1.AddNode(coreNode.Endpoint, false);

                var amount = new Money(5.0m, MoneyUnit.BTC);
                var destination = wm1.GetUnusedAddress(new WalletAccountReference("alice", "account 0"));

                coreRpc.SendToAddress(BitcoinAddress.Create(destination.Address, Network.RegTest), amount);

                Console.WriteLine("Waiting for transaction to propagate and finalise");
                Thread.Sleep(5000);

                coreRpc.Generate(1);

                // Wait for SBFN to sync with the core node
                TestHelper.WaitLoop(() => rpc1.GetBestBlockHash() == coreRpc.GetBestBlockHash());

                // Test implementation note: the coins do not seem to immediately appear in the wallet.
                // This is possibly some sort of race condition between the wallet manager and block generation/sync.
                // This extra delay seems to ensure that the coins are definitely in the wallet by the time the
                // transaction count gets logged to the console below.

                // Wait instead of generating a block
                Thread.Sleep(5000);

                //var log = node1.FullNode.NodeService<ILogger>();
                Console.WriteLine("Number of wallet transactions: " + wm1.GetSpendableTransactionsInWallet("alice").Count());

                // Connect to server and start tumbling
                using (client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Accept.Clear();
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                    // Sample returned output
                    // {"tumbler":"ctb://<onionaddress>.onion?h=<confighash>","denomination":"0.01000000","fee":"0.00010000","network":"RegTest","estimate":"22200"}
                    var connectResponse = client.GetStringAsync(apiSettings.ApiUri + "api/TumbleBit/connect").GetAwaiter().GetResult();

                    //Assert.StartsWith("[{\"", connectResponse);

                    var tumbleModel = new TumbleRequest { OriginWalletName = "alice", OriginWalletPassword = "TumbleBit1", DestinationWalletName = "bob" };
                    var tumbleContent = new StringContent(tumbleModel.ToString(), Encoding.UTF8, "application/json");

                    var tumbleResponse = client.PostAsync(apiSettings.ApiUri + "api/TumbleBit/tumble", tumbleContent).GetAwaiter().GetResult();

                    // Note that the TB client takes about 30 seconds to completely start up, as it has to check the server parameters and
                    // RSA key proofs

                    //Assert.StartsWith("[{\"", tumbleResponse);
                }

                HdAccount alice;
                HdAccount bob;
                // TODO: Move forward specific numbers of blocks and check interim states? TB tests already do that
                for (int i = 0; i < 80; i++)
                {
                    Console.WriteLine("Wallet balance height: " + node1.FullNode.Chain.Height);

                    alice = wm1.GetWalletByName("alice").GetAccountByCoinType("account 0", (CoinType)Network.RegTest.Consensus.CoinType);

                    Console.WriteLine("(A) Confirmed: " + alice.GetSpendableAmount().ConfirmedAmount.ToString());
                    Console.WriteLine("(A) Unconfirmed: " + alice.GetSpendableAmount().UnConfirmedAmount.ToString());

                    bob = wm1.GetWalletByName("bob").GetAccountByCoinType("account 0", (CoinType)Network.RegTest.Consensus.CoinType);

                    Console.WriteLine("(B) Confirmed: " + bob.GetSpendableAmount().ConfirmedAmount.ToString());
                    Console.WriteLine("(B) Unconfirmed: " + bob.GetSpendableAmount().UnConfirmedAmount.ToString());

                    coreRpc.Generate(1);
                    builder.SyncNodes();

                    // Try to ensure the invalid phase error does not occur
                    // (seems to occur when the server has not yet processed a new block and the client has)
                    TestHelper.WaitLoop(() => rpc1.GetBestBlockHash() == coreRpc.GetBestBlockHash());

                    var mempool = node1.FullNode.NodeService<MempoolManager>();
                    var mempoolTx = mempool.GetMempoolAsync().Result;
                    if (mempoolTx.Count > 0)
                    {
                        Console.WriteLine("--- Mempool contents ---");
                        foreach (var tx in mempoolTx)
                        {
                            var hex = mempool.GetTransaction(tx).Result;
                            Console.WriteLine(tx + " ->");
                            Console.WriteLine(hex);
                            Console.WriteLine("---");
                        }
                    }

                    Thread.Sleep(20000);
                }

                // Check destination wallet for tumbled coins

                // TODO: Need to amend TumblerService so that it can be shut down within the test

                if (client != null)
                {
                    client.Dispose();
                    client = null;
                }
            }
        }

        [Fact]
        public void TestWithoutTor()
        {
            // Workaround for segwit not correctly activating
            Network.RegTest.Consensus.BIP9Deployments[BIP9Deployments.Segwit] = new BIP9DeploymentsParameters(1, 0, DateTime.Now.AddDays(50).ToUnixTimestamp());

            using (NodeBuilder builder = NodeBuilder.Create(version: "0.15.1"))
            {
                HttpClient client = null;

                var coreNode = builder.CreateNode(false);

                // This line has no effect currently as the changes to the config get overwritten
                coreNode.ConfigParameters.AddOrReplace("printtoconsole", "0");

                coreNode.ConfigParameters.AddOrReplace("debug", "1");
                //coreNode.ConfigParameters.AddOrReplace("prematurewitness", "1");
                //coreNode.ConfigParameters.AddOrReplace("walletprematurewitness", "1");
                coreNode.ConfigParameters.AddOrReplace("rpcworkqueue", "100");
                
                coreNode.Start();

                // Replicate portions of BreezeServer's Program.cs. Maybe refactor it into a class/function in future
                var serviceProvider = new ServiceCollection()
                    .AddLogging()
                    .AddSingleton<Breeze.BreezeServer.Services.ITumblerService, Breeze.BreezeServer.Services.TumblerService>()
                    .BuildServiceProvider();

                serviceProvider
                    .GetService<ILoggerFactory>()
                    .AddConsole(LogLevel.Debug);

                // Skip the registration code - that can be tested separately

                string configPath = Path.Combine(coreNode.DataFolder, "breeze.conf");
                string[] breezeServerConfig =
                {
                    "network=regtest", // Only the network setting is currently used from this file
                    "rpc.user=dummy",
                    "rpc.password=dummy",
                    "rpc.url=http://127.0.0.1:26174/",
                    "breeze.ipv4=127.0.0.1",
                    "breeze.ipv6=2001:0db8:85a3:0000:0000:8a2e:0370:7334",
                    "breeze.onion=0123456789ABCDEF",
                    "breeze.port=37123",
                    "breeze.regtxfeevalue=10000",
                    "breeze.regtxoutputvalue=1000",
                    "tumbler.url=http://127.0.0.1:37123/api/v1/",
                    "tumbler.rsakeyfile=/Users/username/.ntumblebitserver/RegTest/Tumbler.pem",
                    "tumbler.ecdsakeyaddress=TVwRFmEKRCnQAgShf3QshBjp1Tmucm1e87"
                };
                File.WriteAllLines(configPath, breezeServerConfig);

                BreezeConfiguration config = new BreezeConfiguration(configPath);

                var coreRpc = coreNode.CreateRPCClient();
                string ntbServerConfigPath = Path.Combine(coreNode.DataFolder, "server.config");
                string[] ntbServerConfig =
                {
                    "regtest=1",
                    "rpc.url=http://127.0.0.1:" + coreRpc.Address.Port + "/",
                    "rpc.user=" + coreRpc.CredentialString.UserPassword.UserName,
                    "rpc.password=" + coreRpc.CredentialString.UserPassword.Password,
                    //"cycle=kotori",
                    "tor.enabled=false"
                };

                File.WriteAllLines(ntbServerConfigPath, ntbServerConfig);

                // We need to start up the masternode prior to creating the SBFN instance so that
                // we have the URI available for starting the TumbleBit feature
                // TODO: Also need to see if NTB interactive console interferes with later parts of the test
                new Thread(delegate ()
                {
                    Thread.CurrentThread.IsBackground = true;
                    // By instantiating the TumblerService directly the registration logic is skipped
                    var tumbler = serviceProvider.GetService<Breeze.BreezeServer.Services.ITumblerService>();
                    tumbler.StartTumbler(config, false, "server.config", Path.GetFullPath(coreNode.DataFolder), false);
                }).Start();

                // Wait for URI file to be written out by the TumblerService
                while (!File.Exists(Path.Combine(coreNode.DataFolder, "uri.txt")))
                {
                    Thread.Sleep(1000);
                }

                Console.WriteLine("* URI file detected *");
                Thread.Sleep(5000);

                var serverAddress = File.ReadAllText(Path.Combine(coreNode.DataFolder, "uri.txt"));

                // Not used for this test
                ConfigurationOptionWrapper<string> registrationStoreDirectory = new ConfigurationOptionWrapper<string>("RegistrationStoreDirectory", "");

                // Force SBFN to connect to the server
                ConfigurationOptionWrapper<string> masternodeUri = new ConfigurationOptionWrapper<string>("MasterNodeUri", serverAddress);
                ConfigurationOptionWrapper<string>[] configurationOptions = { registrationStoreDirectory, masternodeUri };

                // Logging for NTB client code
                ConsoleLoggerProcessor loggerProcessor = new ConsoleLoggerProcessor();
                Logs.Configure(new FuncLoggerFactory(i => new CustomerConsoleLogger(i, Logs.SupportDebug(true), false, loggerProcessor)));

                CoreNode node1 = builder.CreateStratisPowNode(true, fullNodeBuilder =>
                {
                    fullNodeBuilder
                        .UsePosConsensus()
                        .UseBlockStore()
                        .UseMempool()
                        .UseBlockNotification()
                        .UseTransactionNotification()
                        .AddMining()
                        .UseWallet()
                        .UseWatchOnlyWallet()
                        .UseApi()
                        .AddRPC()
                        .UseTumbleBit(configurationOptions);
                });

                var apiSettings = node1.FullNode.NodeService<ApiSettings>();

                NLog.Config.LoggingConfiguration config1 = LogManager.Configuration;
                var folder = Path.Combine(node1.DataFolder, "Logs");

                var tbTarget = new FileTarget();
                tbTarget.Name = "tumblebit";
                tbTarget.FileName = Path.Combine(folder, "tumblebit.txt");
                tbTarget.ArchiveFileName = Path.Combine(folder, "tb-${date:universalTime=true:format=yyyy-MM-dd}.txt");
                tbTarget.ArchiveNumbering = ArchiveNumberingMode.Sequence;
                tbTarget.ArchiveEvery = FileArchivePeriod.Day;
                tbTarget.MaxArchiveFiles = 7;
                tbTarget.Layout = "[${longdate:universalTime=true} ${threadid}${mdlc:item=id}] ${level:uppercase=true}: ${callsite} ${message}";
                tbTarget.Encoding = Encoding.UTF8;

                var ruleTb = new LoggingRule("*", NLog.LogLevel.Debug, tbTarget);
                config1.LoggingRules.Add(ruleTb);

                config1.AddTarget(tbTarget);

                // Apply new rules.
                LogManager.ReconfigExistingLoggers();

                //node1.NotInIBD();

                // Create the source and destination wallets
                var wm1 = node1.FullNode.NodeService<IWalletManager>() as WalletManager;
                //var wm2 = node2.FullNode.NodeService<IWalletManager>() as WalletManager;
                wm1.CreateWallet("TumbleBit1", "alice");
                wm1.CreateWallet("TumbleBit1", "bob");

                // Mined coins only mature after 100 blocks on regtest
                // Additionally, we need to force Segwit to activate in order for NTB to work correctly
                coreRpc.Generate(450);

                var rpc1 = node1.CreateRPCClient();
                //var rpc2 = node2.CreateRPCClient();

                coreRpc.AddNode(node1.Endpoint, false);
                rpc1.AddNode(coreNode.Endpoint, false);

                var amount = new Money(5.0m, MoneyUnit.BTC);
                var destination = wm1.GetUnusedAddress(new WalletAccountReference("alice", "account 0"));
                
                coreRpc.SendToAddress(BitcoinAddress.Create(destination.Address, Network.RegTest), amount);

                Console.WriteLine("Waiting for transaction to propagate and finalise");
                Thread.Sleep(5000);

                coreRpc.Generate(1);

                // Wait for SBFN to sync with the core node
                TestHelper.WaitLoop(() => rpc1.GetBestBlockHash() == coreRpc.GetBestBlockHash());

                // Test implementation note: the coins do not seem to immediately appear in the wallet.
                // This is possibly some sort of race condition between the wallet manager and block generation/sync.
                // This extra delay seems to ensure that the coins are definitely in the wallet by the time the
                // transaction count gets logged to the console below.

                // Wait instead of generating a block
                Thread.Sleep(5000);

                //var log = node1.FullNode.NodeService<ILogger>();
                Console.WriteLine("Number of wallet transactions: " + wm1.GetSpendableTransactionsInWallet("alice").Count());
                
                // Connect to server and start tumbling
                using (client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Accept.Clear();
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                    // Sample returned output
                    // {"tumbler":"ctb://<onionaddress>.onion?h=<confighash>","denomination":"0.01000000","fee":"0.00010000","network":"RegTest","estimate":"22200"}
                    var connectResponse = client.GetStringAsync(apiSettings.ApiUri + "api/TumbleBit/connect").GetAwaiter().GetResult();

                    //Assert.StartsWith("[{\"", connectResponse);

                    var tumbleModel = new TumbleRequest { OriginWalletName = "alice", OriginWalletPassword = "TumbleBit1", DestinationWalletName = "bob" };
                    var tumbleContent = new StringContent(tumbleModel.ToString(), Encoding.UTF8, "application/json");

                    var tumbleResponse = client.PostAsync(apiSettings.ApiUri + "api/TumbleBit/tumble", tumbleContent).GetAwaiter().GetResult();
                    
                    // Note that the TB client takes about 30 seconds to completely start up, as it has to check the server parameters and
                    // RSA key proofs

                    //Assert.StartsWith("[{\"", tumbleResponse);
                }

                HdAccount alice;
                HdAccount bob;
                // TODO: Move forward specific numbers of blocks and check interim states? TB tests already do that
                for (int i = 0; i < 200; i++)
                {
                    Console.WriteLine("Wallet balance height: " + node1.FullNode.Chain.Height);

                    alice = wm1.GetWalletByName("alice").GetAccountByCoinType("account 0", (CoinType)Network.RegTest.Consensus.CoinType);

                    Console.WriteLine("(A) Confirmed: " + alice.GetSpendableAmount().ConfirmedAmount.ToString());
                    Console.WriteLine("(A) Unconfirmed: " + alice.GetSpendableAmount().UnConfirmedAmount.ToString());

                    bob = wm1.GetWalletByName("bob").GetAccountByCoinType("account 0", (CoinType)Network.RegTest.Consensus.CoinType);

                    Console.WriteLine("(B) Confirmed: " + bob.GetSpendableAmount().ConfirmedAmount.ToString());
                    Console.WriteLine("(B) Unconfirmed: " + bob.GetSpendableAmount().UnConfirmedAmount.ToString());

                    coreRpc.Generate(1);
                    builder.SyncNodes();

                    // Try to ensure the invalid phase error does not occur
                    // (seems to occur when the server has not yet processed a new block and the client has)
                    TestHelper.WaitLoop(() => rpc1.GetBestBlockHash() == coreRpc.GetBestBlockHash());

                    var mempool = node1.FullNode.NodeService<MempoolManager>();
                    var mempoolTx = mempool.GetMempoolAsync().Result;
                    if (mempoolTx.Count > 0)
                    {
                        Console.WriteLine("--- Mempool contents ---");
                        foreach (var tx in mempoolTx)
                        {
                            var hex = mempool.GetTransaction(tx).Result;
                            Console.WriteLine(tx + " ->");
                            Console.WriteLine(hex);
                            Console.WriteLine("---");
                        }
                    }

                    Thread.Sleep(20000);
                }

                // Check destination wallet for tumbled coins

                // TODO: Need to amend TumblerService so that it can be shut down within the test

                if (client != null)
                {
                    client.Dispose();
                    client = null;
                }
            }
        }

        [Fact]
        public void TestDualClientWithoutTor()
        {
            using (NodeBuilder builder = NodeBuilder.Create(version: "0.15.1"))
            {
                HttpClient client = null;

                var coreNode = builder.CreateNode(false);

                coreNode.ConfigParameters.AddOrReplace("debug", "1");
                coreNode.ConfigParameters.AddOrReplace("printtoconsole", "0");
                coreNode.ConfigParameters.AddOrReplace("prematurewitness", "1");
                coreNode.ConfigParameters.AddOrReplace("walletprematurewitness", "1");
                coreNode.ConfigParameters.AddOrReplace("rpcworkqueue", "100");

                coreNode.Start();

                // Replicate portions of BreezeServer's Program.cs. Maybe refactor it into a class/function in future
                var serviceProvider = new ServiceCollection()
                    .AddLogging()
                    .AddSingleton<Breeze.BreezeServer.Services.ITumblerService, Breeze.BreezeServer.Services.TumblerService>()
                    .BuildServiceProvider();

                serviceProvider
                    .GetService<ILoggerFactory>()
                    .AddConsole(LogLevel.Debug);

                // Skip the registration code - that can be tested separately

                string configPath = Path.Combine(coreNode.DataFolder, "breeze.conf");
                string[] breezeServerConfig =
                {
                    "network=regtest", // Only the network setting is currently used from this file
                    "rpc.user=dummy",
                    "rpc.password=dummy",
                    "rpc.url=http://127.0.0.1:26174/",
                    "breeze.ipv4=127.0.0.1",
                    "breeze.ipv6=2001:0db8:85a3:0000:0000:8a2e:0370:7334",
                    "breeze.onion=0123456789ABCDEF",
                    "breeze.port=37123",
                    "breeze.regtxfeevalue=10000",
                    "breeze.regtxoutputvalue=1000",
                    "tumbler.url=http://127.0.0.1:37123/api/v1/",
                    "tumbler.rsakeyfile=/Users/username/.ntumblebitserver/RegTest/Tumbler.pem",
                    "tumbler.ecdsakeyaddress=TVwRFmEKRCnQAgShf3QshBjp1Tmucm1e87"
                };
                File.WriteAllLines(configPath, breezeServerConfig);

                BreezeConfiguration config = new BreezeConfiguration(configPath);

                var rpc3 = coreNode.CreateRPCClient();
                string ntbServerConfigPath = Path.Combine(coreNode.DataFolder, "server.config");
                string[] ntbServerConfig =
                {
                    "regtest=1",
                    "rpc.url=http://127.0.0.1:" + rpc3.Address.Port + "/",
                    "rpc.user=" + rpc3.CredentialString.UserPassword.UserName,
                    "rpc.password=" + rpc3.CredentialString.UserPassword.Password,
                    //"cycle=kotori",
                    "tor.enabled=false"
                };

                File.WriteAllLines(ntbServerConfigPath, ntbServerConfig);

                // We need to start up the masternode prior to creating the SBFN instance so that
                // we have the URI available for starting the TumbleBit feature
                // TODO: Also need to see if NTB interactive console interferes with later parts of the test
                new Thread(delegate ()
                {
                    Thread.CurrentThread.IsBackground = true;
                    // By instantiating the TumblerService directly the registration logic is skipped
                    var tumbler = serviceProvider.GetService<Breeze.BreezeServer.Services.ITumblerService>();
                    tumbler.StartTumbler(config, false, "server.config", Path.GetFullPath(coreNode.DataFolder), false);
                }).Start();

                // Wait for URI file to be written out by the TumblerService
                while (!File.Exists(Path.Combine(coreNode.DataFolder, "uri.txt")))
                {
                    Thread.Sleep(1000);
                }

                Console.WriteLine("* URI file detected *");
                Thread.Sleep(5000);

                var serverAddress = File.ReadAllText(Path.Combine(coreNode.DataFolder, "uri.txt"));

                // Not used for this test
                ConfigurationOptionWrapper<string> registrationStoreDirectory = new ConfigurationOptionWrapper<string>("RegistrationStoreDirectory", "");

                // Force SBFN to use the temporary hidden service to connect to the server
                ConfigurationOptionWrapper<string> masternodeUri = new ConfigurationOptionWrapper<string>("MasterNodeUri", serverAddress);

                ConfigurationOptionWrapper<string>[] configurationOptions = { registrationStoreDirectory, masternodeUri };

                // Logging for NTB client code
                ConsoleLoggerProcessor loggerProcessor = new ConsoleLoggerProcessor();
                Logs.Configure(new FuncLoggerFactory(i => new CustomerConsoleLogger(i, Logs.SupportDebug(true), false, loggerProcessor)));

                CoreNode node1 = builder.CreateStratisPowNode(false, fullNodeBuilder =>
                {
                    fullNodeBuilder
                        .UsePosConsensus()
                        .UseBlockStore()
                        .UseMempool()
                        .UseBlockNotification()
                        .UseTransactionNotification()
                        .AddMining()
                        .UseWallet()
                        .UseWatchOnlyWallet()
                        .UseApi()
                        .AddRPC()
                        .UseTumbleBit(configurationOptions);
                });

                node1.ConfigParameters.AddOrReplace("apiuri", "http://localhost:37229");

                CoreNode node2 = builder.CreateStratisPowNode(false, fullNodeBuilder =>
                {
                    fullNodeBuilder
                        .UsePosConsensus()
                        .UseBlockStore()
                        .UseMempool()
                        .UseBlockNotification()
                        .UseTransactionNotification()
                        .AddMining()
                        .UseWallet()
                        .UseWatchOnlyWallet()
                        .UseApi()
                        .AddRPC()
                        .UseTumbleBit(configurationOptions);
                });

                node2.ConfigParameters.AddOrReplace("apiuri", "http://localhost:37228");

                var apiSettings1 = node1.FullNode.NodeService<ApiSettings>();
                var apiSettings2 = node2.FullNode.NodeService<ApiSettings>();

                node1.Start();
                node2.Start();

                // TODO: See if it is possible to split node1 and node2's logs into separate folders
                NLog.Config.LoggingConfiguration config1 = LogManager.Configuration;
                var folder = Path.Combine(node1.DataFolder, "Logs");

                var tbTarget = new FileTarget();
                tbTarget.Name = "tumblebit";
                tbTarget.FileName = Path.Combine(folder, "tumblebit.txt");
                tbTarget.ArchiveFileName = Path.Combine(folder, "tb-${date:universalTime=true:format=yyyy-MM-dd}.txt");
                tbTarget.ArchiveNumbering = ArchiveNumberingMode.Sequence;
                tbTarget.ArchiveEvery = FileArchivePeriod.Day;
                tbTarget.MaxArchiveFiles = 7;
                tbTarget.Layout = "[${longdate:universalTime=true} ${threadid}${mdlc:item=id}] ${level:uppercase=true}: ${callsite} ${message}";
                tbTarget.Encoding = Encoding.UTF8;

                var ruleTb = new LoggingRule("*", NLog.LogLevel.Debug, tbTarget);
                config1.LoggingRules.Add(ruleTb);
                config1.AddTarget(tbTarget);

                // Apply new rules.
                LogManager.ReconfigExistingLoggers();

                node1.NotInIBD();
                node2.NotInIBD();

                // Create the source and destination wallets for node 1
                var wm1 = node1.FullNode.NodeService<IWalletManager>() as WalletManager;
                wm1.CreateWallet("TumbleBit1", "alice1");
                wm1.CreateWallet("TumbleBit1", "bob1");

                // Create the source and destination wallets for node 2
                var wm2 = node2.FullNode.NodeService<IWalletManager>() as WalletManager;
                wm2.CreateWallet("TumbleBit1", "alice2");
                wm2.CreateWallet("TumbleBit1", "bob2");

                // Mined coins only mature after 100 blocks on regtest
                // Additionally, we need to force Segwit to activate in order for NTB to work correctly
                coreNode.FindBlock(450);

                var rpc1 = node1.CreateRPCClient();
                var rpc2 = node2.CreateRPCClient();

                rpc3.AddNode(node1.Endpoint, false);
                rpc3.AddNode(node2.Endpoint, false);

                rpc1.AddNode(coreNode.Endpoint, false);
                rpc1.AddNode(node2.Endpoint, false);

                var amount = new Money(5.0m, MoneyUnit.BTC);
                var destination1 = wm1.GetUnusedAddress(new WalletAccountReference("alice1", "account 0"));
                var destination2 = wm2.GetUnusedAddress(new WalletAccountReference("alice2", "account 0"));

                rpc3.SendToAddress(BitcoinAddress.Create(destination1.Address, Network.RegTest), amount);
                rpc3.SendToAddress(BitcoinAddress.Create(destination2.Address, Network.RegTest), amount);

                Console.WriteLine("Waiting for transactions to propagate and finalise");
                Thread.Sleep(5000);

                coreNode.FindBlock(1);

                // Wait for SBFN to sync with the core node
                TestHelper.WaitLoop(() => rpc1.GetBestBlockHash() == rpc3.GetBestBlockHash());
                TestHelper.WaitLoop(() => rpc2.GetBestBlockHash() == rpc3.GetBestBlockHash());

                // Test implementation note: the coins do not seem to immediately appear in the wallet.
                // This is possibly some sort of race condition between the wallet manager and block generation/sync.
                // This extra delay seems to ensure that the coins are definitely in the wallet by the time the
                // transaction count gets logged to the console below.

                // Wait instead of generating a block
                Thread.Sleep(5000);

                var loggerFactory1 = node1.FullNode.NodeService<ILoggerFactory>();
                var loggerFactory2 = node2.FullNode.NodeService<ILoggerFactory>();

                var logger1 = loggerFactory1.CreateLogger(this.GetType().FullName);
                var logger2 = loggerFactory2.CreateLogger(this.GetType().FullName);

                logger1.LogError("(1) Number of wallet transactions: " + wm1.GetSpendableTransactionsInWallet("alice1").Count());
                logger2.LogError("(2) Number of wallet transactions: " + wm2.GetSpendableTransactionsInWallet("alice2").Count());

                // Connect to server and start tumbling
                using (client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Accept.Clear();
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                    // Sample returned output
                    // {"tumbler":"ctb://<onionaddress>.onion?h=<confighash>","denomination":"0.01000000","fee":"0.00010000","network":"RegTest","estimate":"22200"}
                    var connectResponse = client.GetStringAsync(apiSettings1.ApiUri + "api/TumbleBit/connect").GetAwaiter().GetResult();
                    //Assert.StartsWith("[{\"", connectResponse);
                    var tumbleModel = new TumbleRequest { OriginWalletName = "alice1", OriginWalletPassword = "TumbleBit1", DestinationWalletName = "bob1" };
                    var tumbleContent = new StringContent(tumbleModel.ToString(), Encoding.UTF8, "application/json");
                    var tumbleResponse = client.PostAsync(apiSettings1.ApiUri + "api/TumbleBit/tumble", tumbleContent).GetAwaiter().GetResult();

                    // Note that the TB client takes about 30 seconds to completely start up, as it has to check the server parameters and
                    // RSA key proofs

                    //Assert.StartsWith("[{\"", tumbleResponse);
                }

                using (client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Accept.Clear();
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    
                    var connectResponse = client.GetStringAsync(apiSettings2.ApiUri + "api/TumbleBit/connect").GetAwaiter().GetResult();
                    var tumbleModel = new TumbleRequest { OriginWalletName = "alice2", OriginWalletPassword = "TumbleBit1", DestinationWalletName = "bob2" };
                    var tumbleContent = new StringContent(tumbleModel.ToString(), Encoding.UTF8, "application/json");
                    var tumbleResponse = client.PostAsync(apiSettings2.ApiUri + "api/TumbleBit/tumble", tumbleContent).GetAwaiter().GetResult();

                    // Note that the TB client takes about 30 seconds to completely start up, as it has to check the server parameters and
                    // RSA key proofs
                }

                logger1.LogError("(1) About to start tumbling loop");
                logger2.LogError("(2) About to start tumbling loop");

                // TODO: Move forward specific numbers of blocks and check interim states? TB tests already do that
                for (int i = 0; i < 80; i++)
                {
                    rpc3.Generate(1);
                    builder.SyncNodes();

                    // Try to ensure the invalid phase error does not occur
                    // (seems to occur when the server has not yet processed a new block and the client has)
                    TestHelper.WaitLoop(() => rpc1.GetBestBlockHash() == rpc3.GetBestBlockHash());
                    TestHelper.WaitLoop(() => rpc2.GetBestBlockHash() == rpc3.GetBestBlockHash());

                    /*var mempool = node1.FullNode.NodeService<MempoolManager>();
                    var mempoolTx = mempool.GetMempoolAsync().Result;
                    if (mempoolTx.Count > 0)
                    {
                        Console.WriteLine("--- Mempool contents ---");
                        foreach (var tx in mempoolTx)
                        {
                            var hex = mempool.GetTransaction(tx).Result;
                            Console.WriteLine(tx + " ->");
                            Console.WriteLine(hex);
                            Console.WriteLine("---");
                        }
                    }*/

                    Thread.Sleep(20000);
                }

                // Check destination wallet for tumbled coins

                // TODO: Need to amend TumblerService so that it can be shut down within the test

                if (client != null)
                {
                    client.Dispose();
                    client = null;
                }
            }
        }
    }
}
