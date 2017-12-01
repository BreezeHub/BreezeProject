using System;
using System.IO;
using System.Linq;
using System.Threading;
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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Breeze.TumbleBit.Client.Tests
{
    public class Tests
    {
        [Fact]
        public void Test1()
        {
            Assert.True(true);
        }

        [Fact]
        public void MakeNode()
        {
            using (NodeBuilder builder = NodeBuilder.Create(version : "0.15.1"))
            {
                var core3 = builder.CreateNode(true);
                var rpc3 = core3.CreateRPCClient();
                
                // Replicate portions of BreezeServer's Program.cs. Maybe refactor it into a class/function in future
                var serviceProvider = new ServiceCollection()
                    .AddLogging()
                    .AddSingleton<Breeze.BreezeServer.Services.ITumblerService, Breeze.BreezeServer.Services.TumblerService>()
                    .BuildServiceProvider();
                
                serviceProvider
                    .GetService<ILoggerFactory>()
                    .AddConsole(LogLevel.Debug);
                
                // Skip the registration code - that can be tested separately
                
                string configPath = Path.Combine(core3.DataFolder, "breeze.conf");
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
                
                string ntbServerConfigPath = Path.Combine(core3.DataFolder, "server.config");
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
                
                // TODO: Maybe move this to after the initial block generation so they don't have to be processed
                // TODO: Also need to see if NTB interactive console interferes with later parts of the test
                var tumbler = serviceProvider.GetService<Breeze.BreezeServer.Services.ITumblerService>();
			    tumbler.StartTumbler(config, false, "server.config", Path.GetFullPath(core3.DataFolder));
                
                //var node1 = builder.CreateStratisPowNode();
                CoreNode node1 = builder.CreateStratisPowNode(true, fullNodeBuilder =>
                {
                    fullNodeBuilder
                        .UseConsensus()
                        .UseBlockStore()
                        .UseMempool()
                        .AddMining()
                        .UseWallet()
                        .UseApi()
                        .AddRPC();
                    //.UseTumbleBit();
                });
               
                node1.NotInIBD();
                
                // Create the source and destination wallets
                var wm1 = node1.FullNode.NodeService<IWalletManager>() as WalletManager;
                //var wm2 = node2.FullNode.NodeService<IWalletManager>() as WalletManager;
                wm1.CreateWallet("TumbleBit1", "alice");
                wm1.CreateWallet("TumbleBit1", "bob");
                
                // Mined coins only mature after 100 blocks on regtest
                core3.FindBlock(101);

                var rpc1 = node1.CreateRPCClient();
                //var rpc2 = node2.CreateRPCClient();
                
                rpc1.AddNode(core3.Endpoint, false);

                TestHelper.WaitLoop(() => rpc1.GetBestBlockHash() == rpc3.GetBestBlockHash());
                
                var amount = new Money(5.0m, MoneyUnit.BTC);
                var destination = wm1.GetUnusedAddress(new WalletAccountReference("alice", "account 0"));
                
                rpc3.SendToAddress(BitcoinAddress.Create(destination.Address, Network.RegTest), amount);

                core3.FindBlock(1);
                
                var unspent = rpc1.ListUnspent();

                // TODO: Move forward specific numbers of blocks and check interim states? TB tests should already do that
                for (int i = 0; i < 100; i++)
                {
                    core3.FindBlock(1);
                    Thread.Sleep(30); // <- is TumblerService in its own thread? If not, move it into one and we wait for it
                }
                
                // Check destination wallet for tumbled coins
                
                // TODO: Need to amend TumblerService so that it can be shut down within the test
                
                core3.Kill(false);
                node1.Kill(false);
            }
        }
    }
}
