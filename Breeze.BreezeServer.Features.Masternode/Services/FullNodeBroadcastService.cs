using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NTumbleBit;
using NTumbleBit.Logging;
using NTumbleBit.Services;
using NTumbleBit.Services.RPC;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Features.Wallet.Broadcasting;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Features.WatchOnlyWallet;
using Stratis.Bitcoin.Interfaces;

namespace Breeze.BreezeServer.Features.Masternode.Services
{
    public class FullNodeBroadcastService : IBroadcastService
    {
        private FullNodeWalletCache Cache;
        private IRepository repository;
        private ConcurrentChain chain;
        private Network network;
        private IWatchOnlyWalletManager watchOnlyWalletManager;
        private IWalletManager walletManager;
        private IBroadcasterManager broadcasterManager;
        private IConnectionManager connectionManager;

        public FullNodeBlockExplorerService BlockExplorerService { get; }

        public FullNodeBroadcastService(FullNodeWalletCache cache, IRepository repository, ConcurrentChain chain, Network network, IWalletManager walletManager, IWatchOnlyWalletManager watchOnlyWalletManager, IBroadcasterManager broadcasterManager, IConnectionManager connectionManager)
        {
            Cache = cache ?? throw new ArgumentNullException(nameof(cache));
            this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
            this.chain = chain ?? throw new ArgumentNullException(nameof(chain));
            this.network = network ?? throw new ArgumentNullException(nameof(network));
            this.watchOnlyWalletManager = watchOnlyWalletManager ?? throw new ArgumentNullException(nameof(watchOnlyWalletManager));
            this.walletManager = walletManager ?? throw new ArgumentNullException(nameof(walletManager));
            this.broadcasterManager = broadcasterManager ?? throw new ArgumentNullException(nameof(broadcasterManager));
            this.connectionManager = connectionManager ?? throw new ArgumentNullException(nameof(connectionManager));
            BlockExplorerService = new FullNodeBlockExplorerService(cache, chain, network, walletManager, watchOnlyWalletManager, connectionManager);
        }

        public RPCBroadcastService.Record[] GetTransactions()
        {
            var transactions = repository.List<RPCBroadcastService.Record>("Broadcasts");
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
            int height = chain.Height;
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

        private async Task<bool> TryBroadcastCoreAsync(RPCBroadcastService.Record tx, int currentHeight)
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

                if (this.network != Network.RegTest)
                {
                    // Check number of attached peers - if there aren't enough there is
                    // a significantly higher risk the transaction will not get adequately
                    // propagated on the network.
                    // TODO: Revisit this when SBFN connection manager reliably maintains
                    // a peer count of ~8 continuously
                    if (this.connectionManager.ConnectedPeers.Count() < 1)
                    {
                        Logs.Broadcasters.LogDebug($"Insufficient peers for reliable transaction ({tx.Transaction.GetHash()}) propagation: {this.connectionManager.ConnectedPeers.Count()}");
                        return false;
                    }
                }

                // Use the node's broadcast manager for all networks
                Logs.Broadcasters.LogDebug($"Trying to broadcast transaction: {tx.Transaction.GetHash()}");

                await this.broadcasterManager.BroadcastTransactionAsync(tx.Transaction).ConfigureAwait(false);
                var bcResult = broadcasterManager.GetTransaction(tx.Transaction.GetHash()).State;
                switch (bcResult)
                {
                    case Stratis.Bitcoin.Broadcasting.State.Broadcasted:
                    case Stratis.Bitcoin.Broadcasting.State.Propagated:
                        await Cache.ImportUnconfirmedTransaction(tx.Transaction);
                        foreach (var output in tx.Transaction.Outputs)
                        {
                            this.watchOnlyWalletManager.WatchScriptPubKey(output.ScriptPubKey);
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
                            var transactionEntry = this.broadcasterManager.GetTransaction(tx.Transaction.GetHash());
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

        private void RemoveRecord(RPCBroadcastService.Record tx)
        {
            repository.Delete<RPCBroadcastService.Record>("Broadcasts", tx.Transaction.GetHash().ToString());
            repository.UpdateOrInsert<Transaction>("CachedTransactions", tx.Transaction.GetHash().ToString(), tx.Transaction, (a, b) => a);
        }

        public Task<bool> BroadcastAsync(Transaction transaction)
        {
            var record = new RPCBroadcastService.Record
            {
                Transaction = transaction
            };
            var height = chain.Height;
            //3 days expiration
            record.Expiration = height + (int)(TimeSpan.FromDays(3).Ticks / Network.Main.Consensus.PowTargetSpacing.Ticks);
            repository.UpdateOrInsert<RPCBroadcastService.Record>("Broadcasts", transaction.GetHash().ToString(), record, (o, n) => o);
            return TryBroadcastCoreAsync(record, height);
        }

        public Transaction GetKnownTransaction(uint256 txId)
        {
            return repository.Get<RPCBroadcastService.Record>("Broadcasts", txId.ToString())?.Transaction ??
                   repository.Get<Transaction>("CachedTransactions", txId.ToString());
        }
    }
}
