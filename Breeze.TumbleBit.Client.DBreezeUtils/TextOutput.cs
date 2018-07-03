using System;
using Breeze.TumbleBit.Client.Services;
using NTumbleBit.Services;
using Stratis.Bitcoin.Features.Wallet;

namespace Breeze.TumbleBit.Client.DBreezeUtils
{
    public class TextOutput
    {
        private DBreezeUtils repo;
        private SmartBitApi api;
        private WalletUtils walletUtils;
        
        public TextOutput(DBreezeUtils repo, SmartBitApi api, WalletUtils walletUtils)
        {
            this.repo = repo;
            this.api = api;
            this.walletUtils = walletUtils;
        }

        public void DumpServers()
        {
            foreach (var server in repo.GetServerAddresses())
            {
                Console.WriteLine("Server: " + server);
            }
        }

		public void DumpCycleTransactions(bool apiLookup)
		{
			Console.WriteLine("=====");
			var separator = ",";
			Console.WriteLine("CycleNumber, RecordType, TransactionType, TransactionId, InWallets, IsBroadcasted");
			foreach (var cycle in repo.GetCycles())
			{
				Console.WriteLine("Cycle: " + cycle);
				foreach (var record in repo.GetCycleRecords(cycle))
				{
					// Only interested in transaction records
					if (record.RecordType == RecordType.ScriptPubKey)
						continue;

					Console.Write(cycle);
					Console.Write(separator);
					Console.Write(record.RecordType);
					Console.Write(separator);
					Console.Write(record.TransactionType);
					Console.Write(separator);

					if (record.TransactionId == null)
						Console.Write("null");
					else
						Console.Write(record.TransactionId);

					//Console.Write(separator);
					//Console.Write(record.ScriptPubKey);

					Console.Write(separator);

					if (walletUtils.TransactionExistsInWallet(record.TransactionId.ToString()))
						Console.Write("true");
					else
						Console.Write("false");

					Console.Write(separator);

					if (apiLookup)
					{
						string status = "";

						try
						{
							var result = api.GetTransaction(record.TransactionId.ToString()).Result;

							if (result.State == SmartBitResultState.Success)
								status = "broadcasted";
							else if (result.State == SmartBitResultState.Failure)
							{
								status = "unbroadcasted";
							}
							else
								status = "unknown";
						}
						catch (Exception)
						{
							status = "unknown";
						}

						Console.Write(status);
					}

					Console.Write(Environment.NewLine);
				}
			}
		}


		public void DumpCycleTransactionsShort()
		{
			Console.WriteLine("=====");
			var separator = ",";
			Console.WriteLine("CycleNumber, RecordType, TransactionType, TransactionId, InWallets, IsBroadcasted");

			foreach (var cycle in repo.GetCycles())
			{
				bool hasClientEscrow = false;
				bool hasClientRedeem = false;
				bool hasTumblerCashout = false;
				foreach (var record in repo.GetCycleRecords(cycle))
				{
					if (record.RecordType == RecordType.ScriptPubKey)
						continue;

					TransactionData transaction = walletUtils.FindTransaction(record.TransactionId.ToString());
					bool transactionExistsInWallet = transaction != null;
					bool transactionIsConfirmed = transaction != null ? transaction.IsConfirmed() : false;

					if (record.TransactionType == TransactionType.ClientEscrow && transactionExistsInWallet && transactionIsConfirmed)
						hasClientEscrow = true;
					if (record.TransactionType == TransactionType.ClientRedeem && transactionExistsInWallet && transactionIsConfirmed)
						hasClientRedeem = true;
					if (record.TransactionType == TransactionType.TumblerCashout && transactionExistsInWallet && transactionIsConfirmed)
						hasTumblerCashout = true;
				}

				if (!hasClientEscrow || (hasClientRedeem && !hasTumblerCashout) || (!hasClientRedeem && hasTumblerCashout)) continue;

				Console.Write("Cycle: " + cycle);
				if (hasClientRedeem && hasTumblerCashout)
					Console.WriteLine(" (ClientRedeem and TumblerCashout transactions have been used)");
				if (!hasClientRedeem && !hasTumblerCashout)
					Console.WriteLine(" (Lost funds, no transactions have been used)");

				foreach (var record in repo.GetCycleRecords(cycle))
				{
					// Only interested in transaction records
					if (record.RecordType == RecordType.ScriptPubKey)
						continue;

					Console.Write(cycle);
					Console.Write(separator);
					Console.Write(record.RecordType);
					Console.Write(separator);
					Console.Write(record.TransactionType);
					Console.Write(separator);

					if (record.TransactionId == null)
						Console.Write("null");
					else
						Console.Write(record.TransactionId);

					Console.Write(separator);

					if (walletUtils.TransactionExistsInWallet(record.TransactionId.ToString()))
						Console.Write("true");
					else
						Console.Write("false");

					Console.WriteLine();
				}
			}
		}
	}
}