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
using static NTumbleBit.Services.RPC.RPCBroadcastService;
using System.Net.Http;
using Newtonsoft.Json.Linq;
using System.Text;
using System.Threading;

namespace Breeze.TumbleBit.Client.Services
{
    public class FullNodeBroadcastService : IBroadcastService
    {
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
            bool remove = false;
            try
            {
                remove = currentHeight >= tx.Expiration;

                // Happens when the caller does not know the previous input yet
                if (tx.Transaction.Inputs.Count == 0 || tx.Transaction.Inputs[0].PrevOut.Hash == uint256.Zero)
                    return false;

                bool isFinal = tx.Transaction.IsFinal(DateTimeOffset.UtcNow, currentHeight + 1);
                if (!isFinal || IsDoubleSpend(tx.Transaction))
                    return false;

                if (this.TumblingState.TumblerNetwork != Network.RegTest)
                {
                    // Check number of attached peers - if there aren't enough there is
                    // a significantly higher risk the transaction will not get adequately
                    // propagated on the network.
                    // TODO: Revisit this when SBFN connection manager reliably maintains
                    // a peer count of ~8 continuously
                    if (this.TumblingState.ConnectionManager.ConnectedPeers.Count() < 1)
                    {
                        Logs.Broadcasters.LogDebug($"Insufficient peers for reliable transaction ({tx.Transaction.GetHash()}) propagation: {this.TumblingState.ConnectionManager.ConnectedPeers.Count()}");
                        return false;
                    }
                }

                // Use the node's broadcast manager for all networks
                Logs.Broadcasters.LogDebug($"Trying to broadcast transaction: {tx.Transaction.GetHash()}");

                await this.TumblingState.BroadcasterManager.BroadcastTransactionAsync(tx.Transaction).ConfigureAwait(false);
                var bcResult = TumblingState.BroadcasterManager.GetTransaction(tx.Transaction.GetHash()).State;
                switch (bcResult)
                {
                    case Stratis.Bitcoin.Broadcasting.State.Broadcasted:
                    case Stratis.Bitcoin.Broadcasting.State.Propagated:
                        await Cache.ImportUnconfirmedTransaction(tx.Transaction);
                        foreach (var output in tx.Transaction.Outputs)
                        {
                            TumblingState.WatchOnlyWalletManager.WatchScriptPubKey(output.ScriptPubKey);
                        }
                        Logs.Broadcasters.LogDebug($"Broadcasted transaction: {tx.Transaction.GetHash()}");
                        return true;
                        break;
                    case Stratis.Bitcoin.Broadcasting.State.ToBroadcast:
                        // Wait for propagation
                        var waited = TimeSpan.Zero;
                        var period = TimeSpan.FromSeconds(1);
                        while (TimeSpan.FromSeconds(21) > waited)
                        {
                            // Check BroadcasterManager for broadcast success
                            var transactionEntry = this.TumblingState.BroadcasterManager.GetTransaction(tx.Transaction.GetHash());
                            if (transactionEntry != null && transactionEntry.State == Stratis.Bitcoin.Broadcasting.State.Propagated)
                            {
                                Logs.Broadcasters.LogDebug($"Propagated transaction: {tx.Transaction.GetHash()}");
                                // Have to presume propagated = broadcasted when we are operating as a light wallet (or on regtest)
                                return true;
                            }
                            await Task.Delay(period).ConfigureAwait(false);
                            waited += period;
                        }
                        break;
                    case Stratis.Bitcoin.Broadcasting.State.CantBroadcast:
                        Logs.Broadcasters.LogDebug($"Could not broadcast transaction: {tx.Transaction.GetHash()}");
                        // Do nothing
                        break;
                }

                Logs.Broadcasters.LogDebug($"Broadcast status uncertain for transaction: {tx.Transaction.GetHash()}");

                return false;
            }
            finally
            {
                if (remove)
                    RemoveRecord(tx);
            }
        }

        private bool IsDoubleSpend(Transaction tx)
        {
            Logs.Broadcasters.LogDebug("Checking double spends for transaction: " + tx.GetHash());
            var spentInputs = new HashSet<OutPoint>(tx.Inputs.Select(txin => txin.PrevOut));
            var allTransactions = Cache.FindAllTransactionsAsync().Result;
            foreach (var entry in allTransactions)
            {
                if (entry.Confirmations > 0)
                {
                    // In the case where a transaction has already appeared in the wallet (and has confirmed), it has been broadcast before.
                    // Therefore this is regarded as a double spend and it is not broadcast again.

                    var walletTransaction = allTransactions.Where(x => x.Transaction.GetHash() == entry.Transaction.GetHash()).FirstOrDefault();

                    if (walletTransaction != null)
                    {
                        foreach (TxIn input in walletTransaction.Transaction.Inputs)
                        {
                            foreach (OutPoint spentInput in spentInputs)
                            {
                                if (spentInput == input.PrevOut)
                                {
                                    // TODO: Maybe suppress these log entries when tx.GetHash() == walletTransaction.GetHash()?

                                    Logs.Broadcasters.LogDebug("FOUND in transaction: " + walletTransaction.Transaction.GetHash());
                                    Logs.Broadcasters.LogDebug("-- Hex for " + tx.GetHash() + "--");
                                    Logs.Broadcasters.LogDebug(tx.ToHex());
                                    Logs.Broadcasters.LogDebug("-- Hex for " + walletTransaction.Transaction.GetHash() + "--");
                                    Logs.Broadcasters.LogDebug(walletTransaction.Transaction.ToHex());
                                    Logs.Broadcasters.LogDebug("---");
                                    return true;
                                }
                            }
                        }
                    }
                }
            }

            Logs.Broadcasters.LogDebug("Double spend NOT found for transaction: " + tx.GetHash());
            return false;
        }

        private void RemoveRecord(Record tx)
        {
            Repository.Delete<Record>("Broadcasts", tx.Transaction.GetHash().ToString());
            Repository.UpdateOrInsert<Transaction>("CachedTransactions", tx.Transaction.GetHash().ToString(), tx.Transaction, (a, b) => a);
        }

        public Task<bool> BroadcastAsync(Transaction transaction)
        {
            var record = new Record
            {
                Transaction = transaction
            };
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
