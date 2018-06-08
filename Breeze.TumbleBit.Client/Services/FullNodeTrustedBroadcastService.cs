using NBitcoin;
using NTumbleBit.Logging;
using NTumbleBit.PuzzleSolver;
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using NTumbleBit;
using NTumbleBit.Services;
using Stratis.Bitcoin;
using Stratis.Bitcoin.Features.WatchOnlyWallet;
using static NTumbleBit.Services.RPC.RPCTrustedBroadcastService;

namespace Breeze.TumbleBit.Client.Services
{
    public class FullNodeTrustedBroadcastService : ITrustedBroadcastService
    {
        private TumblingState TumblingState { get; }
        private Tracker Tracker { get; }
        private IBroadcastService Broadcaster { get; }
        private FullNodeWalletCache Cache { get; }

        /// <summary>Specification of the network the node runs on - regtest/testnet/mainnet.</summary>
        private readonly Network network;

        public IBlockExplorerService BlockExplorer { get; }
        public IRepository Repository { get; }

        public bool TrackPreviousScriptPubKey { get; set; }

        public FullNodeTrustedBroadcastService(
            IBroadcastService innerBroadcaster,
            IBlockExplorerService explorer,
            IRepository repository,
            FullNodeWalletCache cache,
            Tracker tracker,
            TumblingState tumblingState,
            Network network)
        {
            Broadcaster = innerBroadcaster ?? throw new ArgumentNullException(nameof(innerBroadcaster));
            BlockExplorer = explorer ?? throw new ArgumentNullException(nameof(explorer));
            Repository = repository ?? throw new ArgumentNullException(nameof(repository));
            Cache = cache ?? throw new ArgumentNullException(nameof(cache));
            Tracker = tracker ?? throw new ArgumentNullException(nameof(tracker));
            TumblingState = tumblingState ?? throw new ArgumentNullException(nameof(tumblingState));
            TrackPreviousScriptPubKey = true;
            this.network = network ?? throw new ArgumentNullException(nameof(network));
        }

        public void Broadcast(int cycleStart, TransactionType transactionType, CorrelationId correlation, TrustedBroadcastRequest broadcast)
        {
            if (broadcast == null)
                throw new ArgumentNullException(nameof(broadcast));
            if (broadcast.Key != null && !broadcast.Transaction.Inputs.Any(i => i.PrevOut.IsNull))
                throw new InvalidOperationException("One of the input should be null");

            var address = broadcast.PreviousScriptPubKey?.GetDestinationAddress(TumblingState.TumblerNetwork);
            if (address != null && TrackPreviousScriptPubKey)
                TumblingState.WatchOnlyWalletManager.WatchAddress(address.ToString());

            var height = TumblingState.Chain.Height;
            var record = new Record();
            //3 days expiration after now or broadcast date
            var expirationBase = Math.Max(height, broadcast.BroadcastableHeight);
            record.Expiration = expirationBase + (int)(TimeSpan.FromDays(3).Ticks / TumblingState.TumblerNetwork.Consensus.PowTargetSpacing.Ticks);

            record.Request = broadcast;
            record.TransactionType = transactionType;
            record.Cycle = cycleStart;
            record.Correlation = correlation;
            Logs.Broadcasters.LogInformation($"Planning to broadcast {record.TransactionType} of cycle {record.Cycle} on block {record.Request.BroadcastableHeight}");
            AddBroadcast(record);
        }


        private void AddBroadcast(Record broadcast)
        {
            Repository.UpdateOrInsert("TrustedBroadcasts", broadcast.Request.Transaction.GetHash().ToString(), broadcast, (o, n) => n);
        }

        public Record[] GetRequests()
        {
            var requests = Repository.List<Record>("TrustedBroadcasts");
            return requests.TopologicalSort(tx => requests.Where(tx2 => tx2.Request.Transaction.Outputs.Any(o => o.ScriptPubKey == tx.Request.PreviousScriptPubKey))).ToArray();
        }

        public Transaction[] TryBroadcast()
        {
            uint256[] b = null;
            return TryBroadcast(ref b);
        }
        public Transaction[] TryBroadcast(ref uint256[] knownBroadcasted)
        {
            var height = TumblingState.Chain.Height;

            DateTimeOffset startTime = DateTimeOffset.UtcNow;
            int totalEntries = 0;

            HashSet<uint256> knownBroadcastedSet = new HashSet<uint256>(knownBroadcasted ?? new uint256[0]);
            foreach (var confirmedTx in Cache.FindAllTransactionsAsync().Result.Where(e => e.Confirmations > 6).Select(t => t.Transaction.GetHash()))
            {
                knownBroadcastedSet.Add(confirmedTx);
            }

            List<Transaction> broadcasted = new List<Transaction>();
            var broadcasting = new List<Tuple<Record, Transaction, Task<bool>>>();

            foreach (var broadcast in GetRequests())
            {
                totalEntries++;
                if (broadcast.Request.PreviousScriptPubKey == null)
                {
                    var transaction = broadcast.Request.Transaction;
                    var txHash = transaction.GetHash();
                    if (!broadcast.Tracked)
                    {
                        broadcast.Tracked = true;
                        Tracker.TransactionCreated(broadcast.Cycle, broadcast.TransactionType, txHash, broadcast.Correlation);
                        RecordMaping(broadcast, transaction, txHash);
                        AddBroadcast(broadcast);
                    }

                    if (!knownBroadcastedSet.Contains(txHash)
                        && broadcast.Request.IsBroadcastableAt(height))
                    {
                        broadcasting.Add(Tuple.Create(broadcast, transaction, Broadcaster.BroadcastAsync(transaction)));
                    }
                    knownBroadcastedSet.Add(txHash);
                }
                else
                {
                    foreach (var coin in GetReceivedTransactions(broadcast.Request.PreviousScriptPubKey)
                        //Currently broadcasting transaction might have received transactions for PreviousScriptPubKey
                        .Concat(broadcasting.ToArray().Select(b => b.Item2))
                        .SelectMany(tx => tx.Outputs.AsCoins())
                        .Concat(broadcast.Request.KnownPrevious ?? new Coin[0])
                        .Where(c => c.ScriptPubKey == broadcast.Request.PreviousScriptPubKey))
                    {

                        var transaction = broadcast.Request.ReSign(coin, this.network, out bool cached);
                        var txHash = transaction.GetHash();
                        if (!cached || !broadcast.Tracked)
                        {
                            broadcast.Tracked = true;
                            Tracker.TransactionCreated(broadcast.Cycle, broadcast.TransactionType, txHash, broadcast.Correlation);
                            RecordMaping(broadcast, transaction, txHash);
                            AddBroadcast(broadcast);
                        }

                        if (!knownBroadcastedSet.Contains(txHash)
                            && broadcast.Request.IsBroadcastableAt(height))
                        {
                            broadcasting.Add(Tuple.Create(broadcast, transaction, Broadcaster.BroadcastAsync(transaction)));
                        }
                        knownBroadcastedSet.Add(txHash);
                    }
                }

                var isExpired = height >= broadcast.Expiration;
                var needTracking = broadcast.TransactionType == TransactionType.ClientRedeem;
                if (isExpired && (!needTracking || broadcast.Tracked))
                    Repository.Delete<Record>("TrustedBroadcasts", broadcast.Request.Transaction.GetHash().ToString());
            }

            knownBroadcasted = knownBroadcastedSet.ToArray();

            foreach (var b in broadcasting)
            {
                if (b.Item3.GetAwaiter().GetResult())
                {
                    LogBroadcasted(b.Item1);
                    broadcasted.Add(b.Item2);
                }
            }

            Logs.Broadcasters.LogInformation($"Trusted Broadcaster is monitoring {totalEntries} entries in {(long)(DateTimeOffset.UtcNow - startTime).TotalSeconds} seconds");
            return broadcasted.ToArray();
        }

        private void LogBroadcasted(Record broadcast)
        {
            Logs.Broadcasters.LogInformation($"Broadcasted {broadcast.TransactionType} of cycle {broadcast.Cycle} planned on block {broadcast.Request.BroadcastableHeight}");
        }

        private void RecordMaping(Record broadcast, Transaction transaction, uint256 txHash)
        {
            var txToRecord = new TxToRecord()
            {
                RecordHash = broadcast.Request.Transaction.GetHash(),
                Transaction = transaction
            };
            Repository.UpdateOrInsert<TxToRecord>("TxToRecord", txHash.ToString(), txToRecord, (a, b) => a);
        }

        public TrustedBroadcastRequest GetKnownTransaction(uint256 txId)
        {
            var mapping = Repository.Get<TxToRecord>("TxToRecord", txId.ToString());
            if (mapping == null)
                return null;
            var record = Repository.Get<Record>("TrustedBroadcasts", mapping.RecordHash.ToString())?.Request;
            if (record == null)
                return null;
            record.Transaction = mapping.Transaction;
            return record;
        }

        public Transaction[] GetReceivedTransactions(Script scriptPubKey)
        {
            if (scriptPubKey == null)
                throw new ArgumentNullException(nameof(scriptPubKey));
            return
                BlockExplorer.GetTransactionsAsync(scriptPubKey, false).GetAwaiter().GetResult()
                .Where(t => t.Transaction.Outputs.Any(o => o.ScriptPubKey == scriptPubKey))
                .Select(t => t.Transaction)
                .ToArray();
        }
    }
}