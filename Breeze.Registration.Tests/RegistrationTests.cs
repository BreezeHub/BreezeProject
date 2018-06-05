using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using BreezeCommon;
using NBitcoin;
using NTumbleBit;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.Features.Notifications;
using Stratis.Bitcoin.Features.RPC;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Features.WatchOnlyWallet;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Xunit;

namespace Breeze.Registration.Tests
{
    public class Tests
    {        
        [Fact]
        public void RegistrationTest()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {   
                CoreNode node1 = builder.CreateCustomNode(true, fullNodeBuilder =>
                {
                    fullNodeBuilder
                        .UsePosConsensus()
                        .UseBlockStore()
                        .UseMempool()
                        .UseBlockNotification()
                        .UseTransactionNotification()
                        .UseWallet()
                        .UseWatchOnlyWallet()
                        .AddPowPosMining()
                        .AddRPC();
                }, Network.RegTest);
               
                CoreNode node2 = builder.CreateCustomNode(true, fullNodeBuilder =>
                {
                    fullNodeBuilder
                        .UsePosConsensus()
                        .UseBlockStore()
                        .UseMempool()
                        .UseBlockNotification()
                        .UseTransactionNotification()
                        .UseWallet()
                        .UseWatchOnlyWallet()
                        .AddPowPosMining()
                        .AddRPC()
                        .UseRegistration();
                }, Network.RegTest);
                
                node1.NotInIBD();
                node2.NotInIBD();

                var rpc1 = node1.CreateRPCClient();
                var rpc2 = node2.CreateRPCClient();

                // addnode RPC call does not seem to work, so connect directly
                node1.FullNode.ConnectionManager.AddNodeAddress(node2.Endpoint);
                
                // Create the originating node's wallet
                var wm1 = node1.FullNode.NodeService<IWalletManager>() as WalletManager;
                wm1.CreateWallet("Registration1", "registration");

                var wallet1 = wm1.GetWalletByName("registration");
                var account1 = wallet1.GetAccountsByCoinType((CoinType) node1.FullNode.Network.Consensus.CoinType).First();
                var address1 = account1.GetFirstUnusedReceivingAddress();
                var secret1 = wallet1.GetExtendedPrivateKeyForAddress("Registration1", address1);
                node1.SetDummyMinerSecret(new BitcoinSecret(secret1.PrivateKey, node1.FullNode.Network));

                // Generate a block so we have some funds to create a transaction with
                node1.GenerateStratisWithMiner(52);
                
                TestHelper.TriggerSync(node1);
                TestHelper.TriggerSync(node2);
                
                TestHelper.WaitLoop(() => rpc1.GetBestBlockHash() == rpc2.GetBestBlockHash());

                var rsa = new RsaKey();
                var ecdsa = new Key().GetBitcoinSecret(Network.RegTest);
                var serverAddress = ecdsa.GetAddress().ToString();
			
                var token = new RegistrationToken(1,
                    serverAddress,
                    IPAddress.Parse("127.0.0.1"),
                    IPAddress.Parse("2001:0db8:85a3:0000:0000:8a2e:0370:7334"),
                    "0123456789ABCDEF",
                    "",
                    37123,
                    ecdsa.PubKey);

                var cryptoUtils = new CryptoUtils(rsa, ecdsa);
                token.RsaSignature = cryptoUtils.SignDataRSA(token.GetHeaderBytes().ToArray());
                token.EcdsaSignature = cryptoUtils.SignDataECDSA(token.GetHeaderBytes().ToArray());
                
                byte[] msgBytes = token.GetRegistrationTokenBytes(rsa, ecdsa);

                Transaction sendTx = new Transaction();
                Money outputValue = new Money(0.01m, MoneyUnit.BTC);
                Money feeValue = new Money(0.01m, MoneyUnit.BTC);
                
                byte[] bytes = Encoding.UTF8.GetBytes("BREEZE_REGISTRATION_MARKER");
                sendTx.Outputs.Add(new TxOut()
                {
                    Value = outputValue,
                    ScriptPubKey = TxNullDataTemplate.Instance.GenerateScriptPubKey(bytes)
                });

                foreach (PubKey pubKey in BlockChainDataConversions.BytesToPubKeys(msgBytes))
                {
                    TxOut destTxOut = new TxOut()
                    {
                        Value = outputValue,
                        ScriptPubKey = pubKey.ScriptPubKey
                    };

                    sendTx.Outputs.Add(destTxOut);
                }
                
                var wth1 = node1.FullNode.NodeService<IWalletTransactionHandler>() as WalletTransactionHandler;

                List<Recipient> recipients = new List<Recipient>();
                
                foreach (TxOut txOut in sendTx.Outputs)
                {
                    recipients.Add(new Recipient() {Amount = txOut.Value, ScriptPubKey = txOut.ScriptPubKey});
                }
                
                var walletReference = new WalletAccountReference()
                {
                    // Default to the first wallet & first account
                    AccountName = wm1.Wallets.First().GetAccountsByCoinType((CoinType)node1.FullNode.Network.Consensus.CoinType).First().Name,
                    WalletName = wm1.Wallets.First().Name
                };

                var context = new TransactionBuildContext(
                    walletReference,
                    recipients,
                    "Registration1")
                {
                    MinConfirmations = 0,
                    OverrideFeeRate = new FeeRate(new Money(0.001m, MoneyUnit.BTC)),
                    Shuffle = false,
                    Sign = true
                };

                var tx = wth1.BuildTransaction(context);                
                
                TestHelper.WaitLoop(() => rpc1.GetRawMempool().Length > 0);

                node1.GenerateStratisWithMiner(1);
                
                Thread.Sleep(10000);

                TestHelper.WaitLoop(() => rpc1.GetBestBlockHash() == rpc2.GetBestBlockHash());

                Console.WriteLine("Checking if registration was received...");

                var rm2 = node2.FullNode.NodeService<RegistrationManager>();
                var rs2 = rm2.GetRegistrationStore();

                foreach (var record in rs2.GetAll())
                {
                    Console.WriteLine("Received registration: " + record.RecordTxId);
                }
                
                Console.WriteLine(rs2.GetAll().Count);
                
                Thread.Sleep(10000);
                
                node1.Kill();
                node2.Kill();
            }
        }
        
        [Fact]
        public void MinimalTest()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {   
                CoreNode node1 = builder.CreateCustomNode(true, fullNodeBuilder =>
                {
                    fullNodeBuilder
                        .UsePosConsensus()
                        .UseBlockStore()
                        .UseMempool()
                        .UseBlockNotification()
                        .UseTransactionNotification()
                        .UseWallet()
                        .UseWatchOnlyWallet()
                        .AddPowPosMining()
                        .AddRPC();
                }, Network.RegTest);
		
                node1.NotInIBD();

                var rpc1 = node1.CreateRPCClient();
		
                // Create the originating node's wallet
                var wm1 = node1.FullNode.NodeService<IWalletManager>() as WalletManager;
                wm1.CreateWallet("Registration1", "registration");

                var wallet1 = wm1.GetWalletByName("registration");
                var account1 = wallet1.GetAccountsByCoinType((CoinType) node1.FullNode.Network.Consensus.CoinType).First();
                var address1 = account1.GetFirstUnusedReceivingAddress();
                var secret1 = wallet1.GetExtendedPrivateKeyForAddress("Registration1", address1);

                // We can use SetDummyMinerSecret here because the private key is already in the wallet
                node1.SetDummyMinerSecret(new BitcoinSecret(secret1.PrivateKey, node1.FullNode.Network));

                // Generate a block so we have some funds to create a transaction with
                node1.GenerateStratisWithMiner(10);
                
                Thread.Sleep(20000);

                node1.GenerateStratisWithMiner(10);
                
                Thread.Sleep(20000);

                node1.GenerateStratisWithMiner(10);
                
                Thread.Sleep(600000);
                
                node1.Kill();
            }
        }
    }
}