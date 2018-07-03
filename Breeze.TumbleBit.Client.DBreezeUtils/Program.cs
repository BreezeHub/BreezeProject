using System;
using System.IO;
using System.Linq;
using NBitcoin;
using NTumbleBit;
using NTumbleBit.ClassicTumbler;
using NTumbleBit.Services;
using Breeze.TumbleBit.Client;
using Breeze.TumbleBit.Client.Services;
using Breeze.TumbleBit.Client.DBreezeUtils;
using BreezeCommon;
using NBitcoin.Protocol;

namespace Breeze.TumbleBit.Client.DBreezeUtils
{
    class Program
    {
        static void Main(string[] args)
        {
            // args[0] = path to folder containing db2 directory & wallet jsons
            // args[1] = origin wallet filename
            // args[2] = destination wallet filename
			// args[3] = short output
            
            string repoPath = Path.Combine(args[0], "TumbleBit\\db2");
            var network = Network.RegTest;
            var api = (network != Network.RegTest ? new SmartBitApi(network) : null);
            var repo = new DBreezeUtils(repoPath, network);
            var walletUtils = new WalletUtils(args[0], args[1], args[2]);

            var textOutput = new TextOutput(repo, api, walletUtils);
            
            textOutput.DumpServers();
	        if (args.Length >= 4 && args[3] == "-s")
	        {
		        textOutput.DumpCycleTransactionsShort();
		        repo.Dispose();
				return;
	        }

            textOutput.DumpCycleTransactions(false);
            
            Console.WriteLine("=====");
            Console.WriteLine("TxToRecord transactions");
            foreach (var tx in repo.FindAllTxToRecordEntries())
            {
                Console.WriteLine("TxToRecord entry: " + tx);
                Console.WriteLine(tx.TxData.ToHex());
                Console.WriteLine("===");
            }
            
            Console.WriteLine("=====");
            Console.WriteLine("Broadcast transactions");
            foreach (var tx in repo.FindAllBroadcastTransactions())
            {
                Console.WriteLine(tx.TxData.ToHex());
                Console.WriteLine(tx.TxData);

                if (tx.TxId.ToString().Equals("1f8c1db597c59c367bb72e95ec88f815ffb7e923d64f6f336d806554b9e1dfe8"))
                {
                    Console.WriteLine("Break");
                }

                /*try
                {
                    var result = api.GetTransaction(tx.TxId).Result;

                    if (result.State == SmartBitResultState.Success)
                        Console.WriteLine("Broadcast found: " + tx);
                    else if (result.State == SmartBitResultState.Failure)
                    {
                        Console.WriteLine("Broadcast NOT found: " + tx);

                        Console.WriteLine(tx.TxData.ToHex());
                        
                        var pushResult = api.PushTx(tx.TxData).Result;
                    }
                    else
                        Console.WriteLine("* Broadcast UNKNOWN status: " + tx);
                }
                catch (Exception e)
                {
                    Console.WriteLine("* Broadcast UNKNOWN status: " + tx);
                }*/
            }

            Console.WriteLine("=====");
            Console.WriteLine("Trusted broadcast transactions");
            foreach (var tx in repo.FindAllTrustedBroadcastTransactions())
            {
                if (tx.TxId.ToString().Equals("1f8c1db597c59c367bb72e95ec88f815ffb7e923d64f6f336d806554b9e1dfe8"))
                {
                    Console.WriteLine("Break");
                }

                /*try
                {
                    var result = api.GetTransaction(tx.TxId).Result;

                    if (result.State == SmartBitResultState.Success)
                        Console.WriteLine("Trusted Broadcast found: " + tx);
                    else if (result.State == SmartBitResultState.Failure)
                    {
                        Console.WriteLine("Trusted Broadcast NOT found: " + tx);
                        
                        var pushResult = api.PushTx(tx.TxData).Result;
                    }
                    else
                        Console.WriteLine("* Trusted Broadcast UNKNOWN status: " + tx);
                }
                catch (Exception e)
                {
                    Console.WriteLine("* Trusted Broadcast UNKNOWN status: " + tx);
                }*/
            }
            
            Console.WriteLine("=====");

            foreach (var txType in Enum.GetValues(typeof(TransactionType)).Cast<TransactionType>())
            {
                var txTypeName = ((TransactionType) txType).ToString();

                foreach (var txData in repo.FindAllTransactions(txType))
                {
                    try
                    {
                        var result = api.GetTransaction(txData).Result;

                        if (result.State == SmartBitResultState.Success)
                            Console.WriteLine(txTypeName + " found: " + txData);
                        else if (result.State == SmartBitResultState.Failure)
                        {
                            Console.WriteLine(txTypeName + " NOT found: " + txData);
                        }
                        else
                            Console.WriteLine("* " + txTypeName + " UNKNOWN status: " + txData);
                    }
                    catch (Exception)
                    {
                        Console.WriteLine("* " + txTypeName + " UNKNOWN status: " + txData);
                    }
                }
            }

			repo.Dispose();
            Console.ReadLine();
        }
    }
}
