using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NLog;
using NLog.Config;
using NLog.Targets;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;

using NBitcoin;
using Stratis.Bitcoin.Features.Api;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.Features.Notifications;
using Stratis.Bitcoin.Features.RPC;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Features.WatchOnlyWallet;
using Stratis.Bitcoin.IntegrationTests;

using Breeze.BreezeServer;
using Breeze.TumbleBit.Client;
using Breeze.TumbleBit.Models;
using BreezeCommon;
using NBitcoin.RPC;
using NTumbleBit.Logging;
using Stratis.Bitcoin.IntegrationTests.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.Utilities.Extensions;
using ILogger = Microsoft.Extensions.Logging.ILogger;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace Breeze.IntegrationTestConsole
{
    public class IntegrationTest
    {
        private string[] breezeServerConfig =
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

        private CoreNode GetCoreNode(NodeBuilder builder)
        {
            var coreNode = builder.CreateNode(false);

            coreNode.ConfigParameters.AddOrReplace("debug", "0");
            coreNode.ConfigParameters.AddOrReplace("printtoconsole", "0");
            //coreNode.ConfigParameters.AddOrReplace("prematurewitness", "1");
            //coreNode.ConfigParameters.AddOrReplace("walletprematurewitness", "1");
            coreNode.ConfigParameters.AddOrReplace("rpcworkqueue", "100");

            return coreNode;
        }

        private string[] GetNTBServerConfig(RPCClient coreRpc)
        {
            string[] ntbServerConfig =
            {
                "regtest=1",
                "rpc.url=http://127.0.0.1:" + coreRpc.Address.Port + "/",
                "rpc.user=" + coreRpc.CredentialString.UserPassword.UserName,
                "rpc.password=" + coreRpc.CredentialString.UserPassword.Password,
                //"cycle=kotori",
                "tor.enabled=false"
            };

            return ntbServerConfig;
        }

        public void TestWithoutTor()
        {
            // Workaround for segwit not correctly activating
            Network.RegTest.Consensus.BIP9Deployments[BIP9Deployments.Segwit] = new BIP9DeploymentsParameters(1, 0, DateTime.Now.AddDays(50).ToUnixTimestamp());

            NodeBuilder builder = NodeBuilder.Create(version: "0.15.1");

            HttpClient client = null;

            var coreNode = GetCoreNode(builder);
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
            File.WriteAllLines(configPath, this.breezeServerConfig);

            BreezeConfiguration config = new BreezeConfiguration(configPath);

            var coreRpc = coreNode.CreateRPCClient();
            string ntbServerConfigPath = Path.Combine(coreNode.DataFolder, "server.config");

            File.WriteAllLines(ntbServerConfigPath, GetNTBServerConfig(coreRpc));

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

            CoreNode node1 = builder.CreateStratisPowNode(true, fullNodeBuilder =>
            {
                fullNodeBuilder
                    .UsePowConsensus()
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

            // Logging for NTB client code
            ConsoleLoggerProcessor loggerProcessor = new ConsoleLoggerProcessor();
            Logs.Configure(new FuncLoggerFactory(i => new CustomerConsoleLogger(i, Logs.SupportDebug(true), false, loggerProcessor)), node1.DataFolder);

            var apiSettings = node1.FullNode.NodeService<ApiSettings>();

            // Create the source and destination wallets
            var wm1 = node1.FullNode.NodeService<IWalletManager>() as WalletManager;
            //var wm2 = node2.FullNode.NodeService<IWalletManager>() as WalletManager;
            wm1.CreateWallet("TumbleBit1", "alice");
            wm1.CreateWallet("TumbleBit1", "bob");

            // Mined coins only mature after 100 blocks on regtest
            // Additionally, we need to force Segwit to activate in order for NTB to work correctly
            coreRpc.Generate(450);

            var rpc1 = node1.CreateRPCClient();

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

                var connectContent = new StringContent(new ConnectRequest { OriginWalletName = "alice" }.ToString(), Encoding.UTF8, "application/json");
                var connectResponse = client.PostAsync(apiSettings.ApiUri + "api/TumbleBit/connect", connectContent).GetAwaiter().GetResult();
                var tumbleContent = new StringContent(new TumbleRequest { OriginWalletName = "alice", OriginWalletPassword = "TumbleBit1", DestinationWalletName = "bob" }.ToString(), Encoding.UTF8, "application/json");
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

            if (builder != null)
                builder.Dispose();
        }

        public void TestDualClientWithoutTor()
        {
            // Workaround for segwit not correctly activating
            Network.RegTest.Consensus.BIP9Deployments[BIP9Deployments.Segwit] = new BIP9DeploymentsParameters(1, 0, DateTime.Now.AddDays(50).ToUnixTimestamp());

            using (NodeBuilder builder = NodeBuilder.Create(version: "0.15.1"))
            {
                HttpClient client = null;

                var coreNode = builder.CreateNode(false);

                coreNode.ConfigParameters.AddOrReplace("debug", "0");
                coreNode.ConfigParameters.AddOrReplace("printtoconsole", "0");
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
                File.WriteAllLines(configPath, this.breezeServerConfig);

                BreezeConfiguration config = new BreezeConfiguration(configPath);

                var coreRpc = coreNode.CreateRPCClient();
                string ntbServerConfigPath = Path.Combine(coreNode.DataFolder, "server.config");

                File.WriteAllLines(ntbServerConfigPath, GetNTBServerConfig(coreRpc));

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
                        .UsePowConsensus()
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
                        .UsePowConsensus()
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

                node1.Start();
                node2.Start();

                var apiSettings1 = node1.FullNode.NodeService<ApiSettings>();
                var apiSettings2 = node2.FullNode.NodeService<ApiSettings>();

                var loggerFactory1 = node1.FullNode.NodeService<ILoggerFactory>();
                var loggerFactory2 = node2.FullNode.NodeService<ILoggerFactory>();

                var logger1 = loggerFactory1.CreateLogger(this.GetType().FullName);
                var logger2 = loggerFactory2.CreateLogger(this.GetType().FullName);

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
                coreRpc.Generate(450);

                var rpc1 = node1.CreateRPCClient();
                var rpc2 = node2.CreateRPCClient();

                coreRpc.AddNode(node1.Endpoint, false);
                coreRpc.AddNode(node2.Endpoint, false);

                rpc1.AddNode(coreNode.Endpoint, false);
                rpc1.AddNode(node2.Endpoint, false);

                var amount = new Money(5.0m, MoneyUnit.BTC);
                var destination1 = wm1.GetUnusedAddress(new WalletAccountReference("alice1", "account 0"));
                var destination2 = wm2.GetUnusedAddress(new WalletAccountReference("alice2", "account 0"));

                coreRpc.SendToAddress(BitcoinAddress.Create(destination1.Address, Network.RegTest), amount);
                coreRpc.SendToAddress(BitcoinAddress.Create(destination2.Address, Network.RegTest), amount);

                Console.WriteLine("Waiting for transactions to propagate and finalise");
                Thread.Sleep(5000);

                coreRpc.Generate(1);

                // Wait for SBFN to sync with the core node
                TestHelper.WaitLoop(() => rpc1.GetBestBlockHash() == coreRpc.GetBestBlockHash());
                TestHelper.WaitLoop(() => rpc2.GetBestBlockHash() == coreRpc.GetBestBlockHash());

                // Test implementation note: the coins do not seem to immediately appear in the wallet.
                // This is possibly some sort of race condition between the wallet manager and block generation/sync.
                // This extra delay seems to ensure that the coins are definitely in the wallet by the time the
                // transaction count gets logged to the console below.

                // Wait instead of generating a block
                Thread.Sleep(5000);

                logger1.LogError("(1) Number of wallet transactions: " + wm1.GetSpendableTransactionsInWallet("alice1").Count());
                logger2.LogError("(2) Number of wallet transactions: " + wm2.GetSpendableTransactionsInWallet("alice2").Count());

                // Connect to server and start tumbling
                using (client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Accept.Clear();
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                    var connectContent = new StringContent(new ConnectRequest { OriginWalletName = "alice1" }.ToString(), Encoding.UTF8, "application/json");
                    var connectResponse = client.PostAsync(apiSettings1.ApiUri + "api/TumbleBit/connect", connectContent).GetAwaiter().GetResult();
                    var tumbleContent = new StringContent(new TumbleRequest { OriginWalletName = "alice1", OriginWalletPassword = "TumbleBit1", DestinationWalletName = "bob1" }.ToString(), Encoding.UTF8, "application/json");
                    var tumbleResponse = client.PostAsync(apiSettings1.ApiUri + "api/TumbleBit/tumble", tumbleContent).GetAwaiter().GetResult();
                }

                using (client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Accept.Clear();
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                    var connectContent = new StringContent(new ConnectRequest { OriginWalletName = "alice2" }.ToString(), Encoding.UTF8, "application/json");
                    var connectResponse = client.PostAsync(apiSettings2.ApiUri + "api/TumbleBit/connect", connectContent).GetAwaiter().GetResult();
                    var tumbleContent = new StringContent(new TumbleRequest { OriginWalletName = "alice2", OriginWalletPassword = "TumbleBit1", DestinationWalletName = "bob2" }.ToString(), Encoding.UTF8, "application/json");
                    var tumbleResponse = client.PostAsync(apiSettings2.ApiUri + "api/TumbleBit/tumble", tumbleContent).GetAwaiter().GetResult();
                }

                logger1.LogError("(1) About to start tumbling loop");
                logger2.LogError("(2) About to start tumbling loop");

                HdAccount alice1;
                HdAccount bob1;
                HdAccount alice2;
                HdAccount bob2;

                // TODO: Move forward specific numbers of blocks and check interim states? TB tests already do that
                for (int i = 0; i < 200; i++)
                {
                    Console.WriteLine("Wallet 1 balance height: " + node1.FullNode.Chain.Height);

                    alice1 = wm1.GetWalletByName("alice1").GetAccountByCoinType("account 0", (CoinType)Network.RegTest.Consensus.CoinType);

                    Console.WriteLine("(A1) Confirmed: " + alice1.GetSpendableAmount().ConfirmedAmount.ToString());
                    Console.WriteLine("(A1) Unconfirmed: " + alice1.GetSpendableAmount().UnConfirmedAmount.ToString());

                    bob1 = wm1.GetWalletByName("bob1").GetAccountByCoinType("account 0", (CoinType)Network.RegTest.Consensus.CoinType);

                    Console.WriteLine("(B1) Confirmed: " + bob1.GetSpendableAmount().ConfirmedAmount.ToString());
                    Console.WriteLine("(B1) Unconfirmed: " + bob1.GetSpendableAmount().UnConfirmedAmount.ToString());

                    Console.WriteLine("===");

                    Console.WriteLine("Wallet 2 balance height: " + node2.FullNode.Chain.Height);

                    alice2 = wm2.GetWalletByName("alice2").GetAccountByCoinType("account 0", (CoinType)Network.RegTest.Consensus.CoinType);

                    Console.WriteLine("(A2) Confirmed: " + alice2.GetSpendableAmount().ConfirmedAmount.ToString());
                    Console.WriteLine("(A2) Unconfirmed: " + alice2.GetSpendableAmount().UnConfirmedAmount.ToString());

                    bob2 = wm2.GetWalletByName("bob2").GetAccountByCoinType("account 0", (CoinType)Network.RegTest.Consensus.CoinType);

                    Console.WriteLine("(B2) Confirmed: " + bob2.GetSpendableAmount().ConfirmedAmount.ToString());
                    Console.WriteLine("(B2) Unconfirmed: " + bob2.GetSpendableAmount().UnConfirmedAmount.ToString());
                    
                    coreRpc.Generate(1);
                    builder.SyncNodes();

                    // Try to ensure the invalid phase error does not occur
                    // (seems to occur when the server has not yet processed a new block and the client has)
                    TestHelper.WaitLoop(() => rpc1.GetBestBlockHash() == coreRpc.GetBestBlockHash());
                    TestHelper.WaitLoop(() => rpc2.GetBestBlockHash() == coreRpc.GetBestBlockHash());

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

        public void TestMultiClientWithoutTor(int numClients)
        {
            // Workaround for segwit not correctly activating
            Network.RegTest.Consensus.BIP9Deployments[BIP9Deployments.Segwit] = new BIP9DeploymentsParameters(1, 0, DateTime.Now.AddDays(50).ToUnixTimestamp());

            NodeBuilder builder = NodeBuilder.Create(version: "0.15.1");

            CoreNode coreNode = GetCoreNode(builder);
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
            File.WriteAllLines(configPath, this.breezeServerConfig);

            BreezeConfiguration config = new BreezeConfiguration(configPath);

            var coreRpc = coreNode.CreateRPCClient();
            string ntbServerConfigPath = Path.Combine(coreNode.DataFolder, "server.config");

            File.WriteAllLines(ntbServerConfigPath, GetNTBServerConfig(coreRpc));

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

            List<CoreNode> clientNodes = new List<CoreNode>();

            int apiPortNum = 37229;

            for (int i = 0; i < numClients; i++)
            {
                var temp = builder.CreateStratisPowNode(false, fullNodeBuilder =>
                {
                    fullNodeBuilder
                        .UsePowConsensus()
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

                temp.ConfigParameters.AddOrReplace("apiuri", $"http://localhost:{apiPortNum}");

                clientNodes.Add(temp);

                apiPortNum++;
            }

            foreach (var node in clientNodes)
                node.Start();

            // Create the source and destination wallets for nodes
            for (int i=0; i<numClients; i++)
            { 
                var wm1 = clientNodes[i].FullNode.NodeService<IWalletManager>() as WalletManager;
                wm1.CreateWallet("TumbleBit1", $"alice{i}");
                wm1.CreateWallet("TumbleBit1", $"bob{i}");
            }

            // Mined coins only mature after 100 blocks on regtest
            // Additionally, we need to force Segwit to activate in order for NTB to work correctly
            coreRpc.Generate(450);

            while (coreRpc.GetBlockCount() < 450)
                Thread.Sleep(100);

            for (int i = 0; i < numClients; i++)
            {
                coreRpc.AddNode(clientNodes[i].Endpoint, false);
                var rpc = clientNodes[i].CreateRPCClient();
                rpc.AddNode(coreNode.Endpoint, false);

                for (int j = 0; j < numClients; j++)
                {
                    if (i != j)
                        rpc.AddNode(clientNodes[j].Endpoint, false);
                }
            }

            for (int i = 0; i < numClients; i++)
            {
                var wm1 = clientNodes[i].FullNode.NodeService<IWalletManager>() as WalletManager;
                var destination1 = wm1.GetUnusedAddress(new WalletAccountReference($"alice{i}", "account 0"));
                coreRpc.SendToAddress(BitcoinAddress.Create(destination1.Address, Network.RegTest), new Money(5.0m, MoneyUnit.BTC));
            }

            clientNodes[0].FullNode.Settings.Logger.LogInformation("Waiting for transactions to propagate and finalise");
            Thread.Sleep(5000);

            coreRpc.Generate(1);

            // Wait for SBFN clients to sync with the core node
            foreach (var node in clientNodes)
                TestHelper.WaitLoop(() => node.CreateRPCClient().GetBestBlockHash() == coreRpc.GetBestBlockHash());

            // Test implementation note: the coins do not seem to immediately appear in the wallet.
            // This is possibly some sort of race condition between the wallet manager and block generation/sync.
            // This extra delay seems to ensure that the coins are definitely in the wallet by the time the
            // transaction count gets logged to the console below.

            // Wait instead of generating a block
            Thread.Sleep(5000);

            for (int i = 0; i < numClients; i++)
            {
                var wm1 = clientNodes[i].FullNode.NodeService<IWalletManager>() as WalletManager;
                //logger1.LogError($"({i}) Number of wallet transactions: " + wm1.GetSpendableTransactionsInWallet($"alice{i}").Count());

                // Connect each client to server and start tumbling
                using (HttpClient client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Accept.Clear();
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                    var apiSettings1 = clientNodes[i].FullNode.NodeService<ApiSettings>();
                    var connectContent = new StringContent(new ConnectRequest { OriginWalletName = $"alice{i}" }.ToString(), Encoding.UTF8, "application/json");
                    var connectResponse = client.PostAsync(apiSettings1.ApiUri + "api/TumbleBit/connect", connectContent).GetAwaiter().GetResult();
                    var tumbleContent = new StringContent(new TumbleRequest { OriginWalletName = $"alice{i}", OriginWalletPassword = "TumbleBit1", DestinationWalletName = $"bob{i}" }.ToString(), Encoding.UTF8, "application/json");
                    var tumbleResponse = client.PostAsync(apiSettings1.ApiUri + "api/TumbleBit/tumble", tumbleContent).GetAwaiter().GetResult();

                    // Note that the TB client takes about 30 seconds to completely start up, as it has to check the server parameters and RSA key proofs
                }

                clientNodes[i].FullNode.Settings.Logger.LogInformation($"Client ({i}) About to start tumbling loop");
            }

            while (true)
            {
                for (int i = 0; i < numClients; i++)
                {
                    clientNodes[i].FullNode.Settings.Logger.LogInformation($"Wallet {i} balance height: " + clientNodes[i].FullNode.Chain.Height);

                    var wm1 = clientNodes[i].FullNode.NodeService<IWalletManager>() as WalletManager;

                    HdAccount alice1 = wm1.GetWalletByName($"alice{i}").GetAccountByCoinType("account 0", (CoinType) Network.RegTest.Consensus.CoinType);

                    clientNodes[i].FullNode.Settings.Logger.LogInformation($"(A{i}) Confirmed: " + alice1.GetSpendableAmount().ConfirmedAmount.ToString());
                    clientNodes[i].FullNode.Settings.Logger.LogInformation($"(A{i}) Unconfirmed: " + alice1.GetSpendableAmount().UnConfirmedAmount.ToString());

                    HdAccount bob1 = wm1.GetWalletByName($"bob{i}").GetAccountByCoinType("account 0", (CoinType) Network.RegTest.Consensus.CoinType);

                    clientNodes[i].FullNode.Settings.Logger.LogInformation($"(B{i}) Confirmed: " + bob1.GetSpendableAmount().ConfirmedAmount.ToString());
                    clientNodes[i].FullNode.Settings.Logger.LogInformation($"(B{i}) Unconfirmed: " + bob1.GetSpendableAmount().UnConfirmedAmount.ToString());

                    clientNodes[i].FullNode.Settings.Logger.LogInformation("===");
                }

                coreRpc.Generate(1);

                // Try to ensure the invalid phase error does not occur
                // (seems to occur when the server has not yet processed a new block and the client has)
                //TestHelper.WaitLoop(() => rpc1.GetBestBlockHash() == coreRpc.GetBestBlockHash());
                //TestHelper.WaitLoop(() => rpc2.GetBestBlockHash() == coreRpc.GetBestBlockHash());

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

            if (builder != null)
                builder.Dispose();
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            var test = new IntegrationTest();
            //test.TestWithoutTor();
            //test.TestDualClientWithoutTor();
            test.TestMultiClientWithoutTor(1);
        }
    }
}
