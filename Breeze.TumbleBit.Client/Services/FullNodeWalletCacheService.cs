using NBitcoin;
using NTumbleBit.Services;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Stratis.Bitcoin;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.WatchOnlyWallet;
using Stratis.Bitcoin.Features.Wallet;

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
    }

    /// <summary>
    /// Workaround around slow Bitcoin Core RPC. 
    /// We are refreshing the list of received transaction once per block.
    /// </summary>
    public class FullNodeWalletCache
    {
        private readonly IRepository _Repo;
        private TumblingState tumblingState;

        public FullNodeWalletCache(IRepository repository, TumblingState tumblingState)
        {
            if(repository == null)
                throw new ArgumentNullException("repository");
            if (tumblingState == null)
                throw new ArgumentNullException("tumblingState");

            _Repo = repository;
            this.tumblingState = tumblingState;
        }

        volatile uint256 _RefreshedAtBlock;

        public void Refresh(uint256 currentBlock)
        {
            var refreshedAt = _RefreshedAtBlock;
            if(refreshedAt != currentBlock)
            {
                lock(_Transactions)
                {
                    if(refreshedAt != currentBlock)
                    {
                        RefreshBlockCount();
                        _Transactions = ListTransactions(ref _KnownTransactions);
                        _RefreshedAtBlock = currentBlock;
                    }
                }
            }
        }

        int _BlockCount;
        public int BlockCount
        {
            get
            {
                if(_BlockCount == 0)
                {
                    RefreshBlockCount();
                }
                return _BlockCount;
            }
        }

        private void RefreshBlockCount()
        {
                Interlocked.Exchange(ref _BlockCount, this.tumblingState.walletManager.LastBlockHeight());
        }

        public Transaction GetTransaction(uint256 txId)
        {
            var cached = GetCachedTransaction(txId);
            if(cached != null)
                return cached;
            var tx = FetchTransaction(txId);
            if(tx == null)
                return null;
            PutCached(tx);
            return tx;
        }

        ConcurrentDictionary<uint256, Transaction> _TransactionsByTxId = new ConcurrentDictionary<uint256, Transaction>();

        private Transaction FetchTransaction(uint256 txId)
        {
            try
            {
                Transaction trx = null;

                foreach (WatchedAddress addr in this.tumblingState.watchOnlyWalletManager.GetWatchOnlyWallet()
                    .WatchedAddresses.Values)
                {
                    foreach (Stratis.Bitcoin.Features.WatchOnlyWallet.TransactionData trans in addr.Transactions.Values)
                    {
                        if (trans.Transaction.GetHash() == txId)
                        {
                            return trans.Transaction;
                        }
                    }
                }

                foreach (var tx in this.tumblingState.OriginWallet.GetAllTransactionsByCoinType(this.tumblingState.coinType))
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

        public FullNodeWalletEntry[] GetEntries()
        {
            lock(_Transactions)
            {
                return _Transactions.ToArray();
            }
        }

        private void PutCached(Transaction tx)
        {
            tx.CacheHashes();
            _Repo.UpdateOrInsert("CachedTransactions", tx.GetHash().ToString(), tx, (a, b) => b);
            lock(_TransactionsByTxId)
            {
                _TransactionsByTxId.TryAdd(tx.GetHash(), tx);
            }
        }

        private Transaction GetCachedTransaction(uint256 txId)
        {
            Transaction tx = null;
            if(_TransactionsByTxId.TryGetValue(txId, out tx))
            {
                return tx;
            }
            var cached = _Repo.Get<Transaction>("CachedTransactions", txId.ToString());
            if(cached != null)
                _TransactionsByTxId.TryAdd(txId, cached);
            return cached;
        }


        List<FullNodeWalletEntry> _Transactions = new List<FullNodeWalletEntry>();
        HashSet<uint256> _KnownTransactions = new HashSet<uint256>();
        List<FullNodeWalletEntry> ListTransactions(ref HashSet<uint256> knownTransactions)
        {
            List<FullNodeWalletEntry> array = new List<FullNodeWalletEntry>();
            knownTransactions = new HashSet<uint256>();
            var removeFromCache = new HashSet<uint256>(_TransactionsByTxId.Values.Select(tx => tx.GetHash()));

            // List all transactions, including those in watch-only wallet
            // (zero confirmations are acceptable)

            // First examine watch-only wallet
            var watchOnlyWallet = this.tumblingState.watchOnlyWalletManager.GetWatchOnlyWallet();

            foreach (var watchedAddress in watchOnlyWallet.WatchedAddresses)
            {
                foreach (var watchOnlyTx in watchedAddress.Value.Transactions)
                {
                    var block = this.tumblingState.chain.GetBlock(watchOnlyTx.Value.BlockHash);
                    var confCount = this.tumblingState.chain.Tip.Height - block.Height;

                    if (confCount == null)
                        confCount = 0;

                    var entry = new FullNodeWalletEntry()
                    {
                        TransactionId = watchOnlyTx.Value.Transaction.GetHash(),
                        Confirmations = (int)confCount
                    };

                    removeFromCache.Remove(watchOnlyTx.Value.Transaction.GetHash());
                    if (knownTransactions.Add(entry.TransactionId))
                    {
                        array.Add(entry);
                    }
                }
            }

            // List transactions in regular source wallet
            var wallet = this.tumblingState.OriginWallet;
            foreach (var walletTx in wallet.GetAllTransactionsByCoinType(this.tumblingState.coinType))
            {
                var confCount = this.tumblingState.chain.Tip.Height - walletTx.BlockHeight;

                if (confCount == null)
                    confCount = 0;

                var entry = new FullNodeWalletEntry()
                {
                    TransactionId = walletTx.Id,
                    Confirmations = (int)confCount
                };

                removeFromCache.Remove(walletTx.Id);
                if (knownTransactions.Add(entry.TransactionId))
                {
                    array.Add(entry);
                }
            }

            // TODO: Filter out high confirmation transactions upfront as in original code
            
            foreach (var remove in removeFromCache)
            {
                Transaction opt;
                _TransactionsByTxId.TryRemove(remove, out opt);
            }
            return array;
        }

        public void ImportTransaction(Transaction transaction, int confirmations)
        {
            PutCached(transaction);
            lock(_Transactions)
            {
                if(_KnownTransactions.Add(transaction.GetHash()))
                {
                    _Transactions.Insert(0,
                        new FullNodeWalletEntry()
                        {
                            Confirmations = confirmations,
                            TransactionId = transaction.GetHash()
                        });
                }
            }
        }
    }
}
