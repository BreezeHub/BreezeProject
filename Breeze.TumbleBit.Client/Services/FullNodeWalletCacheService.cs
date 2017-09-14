using NBitcoin;
using NTumbleBit.Logging;
using NTumbleBit.Services;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Stratis.Bitcoin.Features.WatchOnlyWallet;
using Stratis.Bitcoin.Features.Wallet;
using NTumbleBit;
using System.Threading.Tasks;

namespace Breeze.TumbleBit.Client.Services
{
    public class FullNodeWalletEntry
    {
        public uint256 TransactionId
        {
            get; set;
        }
        public int Confirmations
        {
            get; set;
        }

        public Transaction Transaction
        {
            get; set;
        }
    }

    /// <summary>
    /// We are refreshing the list of received transactions once per block.
    /// </summary>
    public class FullNodeWalletCache
    {
        private readonly IRepository repo;
        private TumblingState tumblingState;

        MultiValueDictionary<Script, FullNodeWalletEntry> _TxByScriptId = new MultiValueDictionary<Script, FullNodeWalletEntry>();
        ConcurrentDictionary<uint256, FullNodeWalletEntry> _WalletEntries = new ConcurrentDictionary<uint256, FullNodeWalletEntry>();
        const int MaxConfirmations = 1400;

        public FullNodeWalletCache(IRepository repository, TumblingState tumblingState)
        {
            this.repo = repository ?? throw new ArgumentNullException("repository");
            this.tumblingState = tumblingState ?? throw new ArgumentNullException("tumblingState");
        }

        volatile uint256 _RefreshedAtBlock;

        public void Refresh(uint256 currentBlock)
        {
            if (_RefreshedAtBlock != currentBlock)
            {
                var newBlockCount = this.tumblingState.Chain.Tip.Height;
                //If we just udpated the value...
                if (Interlocked.Exchange(ref _BlockCount, newBlockCount) != newBlockCount)
                {
                    _RefreshedAtBlock = currentBlock;
                    var startTime = DateTimeOffset.UtcNow;
                    ListTransactions();
                    //Logs.Wallet.LogInformation($"Updated {_WalletEntries.Count} cached transactions in {(long)(DateTimeOffset.UtcNow - startTime).TotalSeconds} seconds");
                    Console.WriteLine($"Updated {_WalletEntries.Count} cached transactions in {(long)(DateTimeOffset.UtcNow - startTime).TotalSeconds} seconds");
                }
            }
        }

        int _BlockCount;
        public int BlockCount
        {
            get
            {
                if (_BlockCount == 0)
                {
                    _BlockCount = this.tumblingState.Chain.Tip.Height;
                }
                return _BlockCount;
            }
        }

        public Transaction GetTransaction(uint256 txId)
        {
            if (_WalletEntries.TryGetValue(txId, out FullNodeWalletEntry entry))
            {
                return entry.Transaction;
            }
            return null;
        }

        //ConcurrentDictionary<uint256, Transaction> _TransactionsByTxId = new ConcurrentDictionary<uint256, Transaction>();

        // Made this non-static since you can't keep this static & search for transactions in the wallet
        private async Task<Transaction> FetchTransactionAsync(uint256 txId)
        {
            try
            {
                foreach (WatchedAddress addr in this.tumblingState.WatchOnlyWalletManager.GetWatchOnlyWallet().WatchedAddresses.Values)
                {
                    foreach (Stratis.Bitcoin.Features.WatchOnlyWallet.TransactionData trans in addr.Transactions.Values)
                    {
                        if (trans.Transaction.GetHash() == txId)
                        {
                             return trans.Transaction;
                        }
                    }
                }

                foreach (var tx in this.tumblingState.OriginWallet.GetAllTransactionsByCoinType(this.tumblingState.CoinType))
                {
                    if (tx.Transaction.GetHash() == txId)
                    {
                        return tx.Transaction;
                    }
                }

                Console.WriteLine("Unable to locate transaction in wallet or watch-only wallet: " + txId);
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception searching for transaction " + txId + ": " + ex);
                return null;
            }
        }

        public ICollection<FullNodeWalletEntry> GetEntries()
        {
            return _WalletEntries.Values;
        }

        public IEnumerable<FullNodeWalletEntry> GetEntriesFromScript(Script script)
        {
            lock (_TxByScriptId)
            {
                IReadOnlyCollection<FullNodeWalletEntry> transactions = null;
                if (_TxByScriptId.TryGetValue(script, out transactions))
                    return transactions.ToArray();
                return new FullNodeWalletEntry[0];
            }
        }

        private void AddTxByScriptId(uint256 txId, FullNodeWalletEntry entry)
        {
            IEnumerable<Script> scripts = GetScriptsOf(entry.Transaction);
            lock (_TxByScriptId)
            {
                foreach (var s in scripts)
                {
                    _TxByScriptId.Add(s, entry);
                }
            }
        }
        private void RemoveTxByScriptId(FullNodeWalletEntry entry)
        {
            IEnumerable<Script> scripts = GetScriptsOf(entry.Transaction);
            lock (_TxByScriptId)
            {
                foreach (var s in scripts)
                {
                    _TxByScriptId.Remove(s, entry);
                }
            }
        }

        private static IEnumerable<Script> GetScriptsOf(Transaction tx)
        {
            return tx.Outputs.Select(o => o.ScriptPubKey)
                                    .Concat(tx.Inputs.Select(o => o.GetSigner()?.ScriptPubKey))
                                    .Where(script => script != null);
        }

        void ListTransactions()
        {
            // Dropped the batching from the RPC version to make the code simpler?

            var removeFromWalletEntries = new HashSet<uint256>(_WalletEntries.Keys);

            HashSet<uint256> processedTransacions = new HashSet<uint256>();

            // List all transactions, including those in watch-only wallet
            // (zero confirmations are acceptable)

            // Original RPC command with parameters:
            //var result = _RPCClient.SendCommand("listtransactions", "*", count, skip, true);

            // First examine watch-only wallet
            var watchOnlyWallet = this.tumblingState.WatchOnlyWalletManager.GetWatchOnlyWallet();

            foreach (var watchedAddress in watchOnlyWallet.WatchedAddresses)
            {
                foreach (var watchOnlyTx in watchedAddress.Value.Transactions)
                {
                    var block = this.tumblingState.Chain.GetBlock(watchOnlyTx.Value.BlockHash);
                    var confCount = this.tumblingState.Chain.Tip.Height - block.Height;

                    // Ignore very old transactions
                    if (confCount > MaxConfirmations)
                        continue;

                    var entry = new FullNodeWalletEntry()
                    {
                        TransactionId = watchOnlyTx.Value.Transaction.GetHash(),
                        Confirmations = (int)confCount,
                        Transaction = watchOnlyTx.Value.Transaction
                    };

                    if (_WalletEntries.TryAdd(entry.TransactionId, entry))
                        AddTxByScriptId(entry.TransactionId, entry);
                }
            }

            // List transactions in regular source wallet
            foreach (var wallet in this.tumblingState.WalletManager.Wallets)
            {
                foreach (var walletTx in wallet.GetAllTransactionsByCoinType(this.tumblingState.CoinType))
                {
                    var confCount = this.tumblingState.Chain.Tip.Height - walletTx.BlockHeight;

                    if (confCount == null)
                        confCount = 0;

                    // Ignore very old transactions
                    if (confCount > MaxConfirmations)
                        continue;

                    var entry = new FullNodeWalletEntry()
                    {
                        TransactionId = walletTx.Id,
                        Confirmations = (int)confCount,
                        Transaction = walletTx.Transaction
                    };

                    if (_WalletEntries.TryAdd(entry.TransactionId, entry))
                        AddTxByScriptId(entry.TransactionId, entry);
                }
            }

            // TODO: The original code is somewhat unclear - removeFromWalletEntries is never added to in ListTransactions
            foreach (var remove in removeFromWalletEntries)
            {
                FullNodeWalletEntry opt;
                if (_WalletEntries.TryRemove(remove, out opt))
                {
                    RemoveTxByScriptId(opt);
                }
            }        
        }

        public void ImportTransaction(Transaction transaction, int confirmations)
        {
            var txId = transaction.GetHash();
            var entry = new FullNodeWalletEntry()
            {
                Confirmations = confirmations,
                TransactionId = transaction.GetHash(),
                Transaction = transaction
            };
            if (_WalletEntries.TryAdd(txId, entry))
                AddTxByScriptId(txId, entry);
        }
    }
}
