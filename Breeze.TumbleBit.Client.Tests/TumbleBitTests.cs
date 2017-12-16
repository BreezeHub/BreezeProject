using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

using NBitcoin;
using Stratis.Bitcoin;
using Stratis.Bitcoin.Features.Api;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.Features.RPC;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.IntegrationTests;

using Breeze.BreezeServer;
using Breeze.BreezeServer.Services;
using Breeze.TumbleBit.Models;
using BreezeCommon;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Features.Notifications;
using Stratis.Bitcoin.Features.WatchOnlyWallet;
using System.Text;

namespace Breeze.TumbleBit.Client.Tests
{
    public class Tests
    {
        [Fact]
        public void MakeNode()
        {
            using (NodeBuilder builder = NodeBuilder.Create(version : "0.15.1"))
            {
                HttpClient client = null;

                var coreNode = builder.CreateNode(true);
                
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
                    tumbler.StartTumbler(config, false, "server.config", Path.GetFullPath(coreNode.DataFolder));
                }).Start();
                
                // Wait for URI file to be written out by the TumblerService
                while (!File.Exists(Path.Combine(coreNode.DataFolder, "uri.txt")))
                {
                    Thread.Sleep(1000);
                }

                Console.WriteLine("* URI file detected *");
                Thread.Sleep(5000);
                
                var serverAddress = File.ReadAllText(Path.Combine(coreNode.DataFolder, "uri.txt"));

                /* For some reason this is not able to actually connect to the server. Perhaps something to do with proxy endpoint mapping?
                string serverAddress;
                using (client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Accept.Clear();
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    var uri = new Uri("http://127.0.0.1:37123/api/v1/tumblers/address");
                    serverAddress = client.GetStringAsync(uri).GetAwaiter().GetResult();

                    Console.WriteLine(serverAddress);
                }*/

                // Not used for this test
                ConfigurationOptionWrapper<string> registrationStoreDirectory = new ConfigurationOptionWrapper<string>("RegistrationStoreDirectory", "");

                // Force SBFN to use the temporary hidden service to connect to the server
                ConfigurationOptionWrapper<string> masternodeUri = new ConfigurationOptionWrapper<string>("MasterNodeUri", serverAddress);

                ConfigurationOptionWrapper<string>[] configurationOptions = { registrationStoreDirectory, masternodeUri };

                CoreNode node1 = builder.CreateStratisPowNode(true, fullNodeBuilder =>
                {
                    fullNodeBuilder
                        .UseConsensus()
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
               
                node1.NotInIBD();
                
                // Create the source and destination wallets
                var wm1 = node1.FullNode.NodeService<IWalletManager>() as WalletManager;
                //var wm2 = node2.FullNode.NodeService<IWalletManager>() as WalletManager;
                wm1.CreateWallet("TumbleBit1", "alice");
                wm1.CreateWallet("TumbleBit1", "bob");
                
                // Mined coins only mature after 100 blocks on regtest
                coreNode.FindBlock(101);

                var rpc1 = node1.CreateRPCClient();
                //var rpc2 = node2.CreateRPCClient();
                
                rpc3.AddNode(node1.Endpoint, false);
                rpc1.AddNode(coreNode.Endpoint, false);

                var amount = new Money(5.0m, MoneyUnit.BTC);
                var destination = wm1.GetUnusedAddress(new WalletAccountReference("alice", "account 0"));
                
                rpc3.SendToAddress(BitcoinAddress.Create(destination.Address, Network.RegTest), amount);

                coreNode.FindBlock(1);

                // Wait for SBFN to sync with the core node
                TestHelper.WaitLoop(() => rpc1.GetBestBlockHash() == rpc3.GetBestBlockHash());

                //var unspent = rpc1.ListUnspent();

                // Connect to server and start tumbling
                using (client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Accept.Clear();
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                    // Sample returned output
                    // {"tumbler":"ctb://<onionaddress>.onion?h=<confighash>","denomination":"0.01000000","fee":"0.00010000","network":"RegTest","estimate":"22200"}
                    var connectResponse = client.GetStringAsync(node1.FullNode.Settings.ApiUri + "api/TumbleBit/connect").GetAwaiter().GetResult();

                    //Assert.StartsWith("[{\"", connectResponse);

                    var tumbleModel = new TumbleRequest { OriginWalletName = "alice", OriginWalletPassword = "TumbleBit1", DestinationWalletName = "bob"};
                    var tumbleContent = new StringContent(tumbleModel.ToString(), Encoding.UTF8, "application/json");
                    Console.WriteLine("Sending tumble request...");
                    var tumbleResponse = client.PostAsync(node1.FullNode.Settings.ApiUri + "api/TumbleBit/tumble", tumbleContent).GetAwaiter().GetResult();
                    Console.WriteLine("Tumble request sent");

                    //Assert.StartsWith("[{\"", tumbleResponse);
                }

                // TODO: Move forward specific numbers of blocks and check interim states? TB tests should already do that
                for (int i = 0; i < 10; i++)
                {
                    coreNode.FindBlock(1);
                    Thread.Sleep(5000);
                }
                
                // Check destination wallet for tumbled coins
                
                // TODO: Need to amend TumblerService so that it can be shut down within the test
                
                coreNode.Kill(false);
                node1.Kill(false);

                if (client != null)
                {
                    client.Dispose();
                    client = null;
                }
            }
        }
    }
}
