using NBitcoin.RPC;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin;
using NTumbleBit;
using NTumbleBit.Logging;
using NTumbleBit.Services;
using Stratis.Bitcoin;

namespace Breeze.TumbleBit.Client.Services
{
    public class FullNodeBroadcastService : IBroadcastService
    {
        public class Record
        {
            public int Expiration
            {
                get; set;
            }
            public Transaction Transaction
            {
                get; set;
            }
        }

        private FullNodeWalletCache Cache { get; }
        private TumblingState TumblingState { get; }
        private IRepository Repository { get; }

        public FullNodeBlockExplorerService BlockExplorerService { get; }

        public FullNodeBroadcastService(FullNodeWalletCache cache, IRepository repository, TumblingState tumblingState)
        {
            Cache = cache ?? throw new ArgumentNullException(nameof(cache));
            Repository = repository ?? throw new ArgumentNullException(nameof(repository));
            TumblingState = tumblingState ?? throw new ArgumentNullException(nameof(tumblingState));
            BlockExplorerService = new FullNodeBlockExplorerService(cache, tumblingState);
        }

        

        public Record[] GetTransactions()
        {
            var transactions = Repository.List<Record>("Broadcasts");
            foreach (var tx in transactions)
                tx.Transaction.CacheHashes();

            var txByTxId = transactions.ToDictionary(t => t.Transaction.GetHash());
            var dependsOn = transactions.Select(t => new
            {
                Tx = t,
                Depends = t.Transaction.Inputs.Select(i => i.PrevOut)
                                              .Where(o => txByTxId.ContainsKey(o.Hash))
                                              .Select(o => txByTxId[o.Hash])
            })
            .ToDictionary(o => o.Tx, o => o.Depends.ToArray());
            return transactions.TopologicalSort(tx => dependsOn[tx]).ToArray();
        }
        public Transaction[] TryBroadcast()
        {
            uint256[] r = null;
            return TryBroadcast(ref r);
        }
        public Transaction[] TryBroadcast(ref uint256[] knownBroadcasted)
        {
            var startTime = DateTimeOffset.UtcNow;
            int totalEntries = 0;
            List<Transaction> broadcasted = new List<Transaction>();
            var broadcasting = new List<Tuple<Transaction, Task<bool>>>();
            HashSet<uint256> knownBroadcastedSet = new HashSet<uint256>(knownBroadcasted ?? new uint256[0]);
            int height = TumblingState.Chain.Height;
            foreach (var obj in Cache.FindAllTransactionsAsync().Result)
            {
                if (obj.Confirmations > 0)
                    knownBroadcastedSet.Add(obj.Transaction.GetHash());
            }

            foreach (var tx in GetTransactions())
            {
                totalEntries++;
                if (!knownBroadcastedSet.Contains(tx.Transaction.GetHash()))
                {
                    broadcasting.Add(Tuple.Create(tx.Transaction, TryBroadcastCoreAsync(tx, height)));
                }
                knownBroadcastedSet.Add(tx.Transaction.GetHash());
            }

            knownBroadcasted = knownBroadcastedSet.ToArray();

            foreach (var broadcast in broadcasting)
            {
                if (broadcast.Item2.GetAwaiter().GetResult())
                    broadcasted.Add(broadcast.Item1);
            }

            Logs.Broadcasters.LogInformation($"Broadcasted {broadcasted.Count} transaction(s), monitoring {totalEntries} entries in {(long)(DateTimeOffset.UtcNow - startTime).TotalSeconds} seconds");
            return broadcasted.ToArray();
        }

        private async Task<bool> TryBroadcastCoreAsync(Record tx, int currentHeight)
        {
            return await Task.Run(() =>
            {
                bool remove = false;
                try
                {
                    remove = currentHeight >= tx.Expiration;

                    //Happens when the caller does not know the previous input yet
                    if (tx.Transaction.Inputs.Count == 0 || tx.Transaction.Inputs[0].PrevOut.Hash == uint256.Zero)
                        return false;

                    bool isFinal = tx.Transaction.IsFinal(DateTimeOffset.UtcNow, currentHeight + 1);
                    if (!isFinal || IsDoubleSpend(tx.Transaction))
                        return false;

                    try
                    {
                        this.TumblingState.WalletManager.SendTransaction(tx.Transaction.ToHex());

                        Logs.Broadcasters.LogInformation($"Broadcasted {tx.Transaction.GetHash()}");
                        return true;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Error broadcasting transaction: " + ex);

                        // TODO: As per original code, need to determine the error to decide whether to remove
                        // TODO: For a light wallet there is currently insufficient information about broadcast failure & other nodes' mempool acceptance
                        remove = false;
                    }
                    return false;
                }
                finally
                {
                    if (remove)
                        RemoveRecord(tx);
                }
            }).ConfigureAwait(false);
        }

        private bool IsDoubleSpend(Transaction tx)
        {
            var spentInputs = new HashSet<OutPoint>(tx.Inputs.Select(txin => txin.PrevOut));
            var allTransactions = Cache.FindAllTransactionsAsync().Result;
            foreach (var entry in allTransactions)
            {
                if (entry.Confirmations > 0)
                {
                    var walletTransaction = allTransactions.Where(x=>x.Transaction.GetHash() == entry.Transaction.GetHash()).FirstOrDefault();
                    if (walletTransaction != null)
                    {
                        foreach (var input in walletTransaction.Transaction.Inputs)
                        {
                            if (spentInputs.Contains(input.PrevOut))
                            {
                                return true;
                            }
                        }
                    }
                }
            }
            return false;
        }

        private void RemoveRecord(Record tx)
        {
            Console.WriteLine("Removing transaction from broadcast: " + tx.Transaction.GetHash());
            Repository.Delete<Record>("Broadcasts", tx.Transaction.GetHash().ToString());
            Repository.UpdateOrInsert<Transaction>("CachedTransactions", tx.Transaction.GetHash().ToString(), tx.Transaction, (a, b) => a);
        }

        public Task<bool> BroadcastAsync(Transaction transaction)
        {
            var record = new Record();
            record.Transaction = transaction;
            var height = TumblingState.Chain.Height;
            //3 days expiration
            record.Expiration = height + (int)(TimeSpan.FromDays(3).Ticks / Network.Main.Consensus.PowTargetSpacing.Ticks);
            Repository.UpdateOrInsert<Record>("Broadcasts", transaction.GetHash().ToString(), record, (o, n) => o);
            return TryBroadcastCoreAsync(record, height);
        }

        public Transaction GetKnownTransaction(uint256 txId)
        {
            return Repository.Get<Record>("Broadcasts", txId.ToString())?.Transaction ??
                   Repository.Get<Transaction>("CachedTransactions", txId.ToString());
        }
    }
}
