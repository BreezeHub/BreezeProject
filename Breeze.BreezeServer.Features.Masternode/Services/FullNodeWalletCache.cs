using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using NTumbleBit.Services;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Features.WatchOnlyWallet;
using Stratis.Bitcoin.Utilities.Extensions;

namespace Breeze.BreezeServer.Features.Masternode.Services
{
    public class FullNodeWalletEntry
    {
        public uint256 TransactionId { get; set; }
        public int Confirmations { get; set; }
        public Transaction Transaction { get; set; }
    }

    /// <summary>
    /// We are refreshing the list of received transactions once per block.
    /// </summary>
    public class FullNodeWalletCache
    {
        private ConcurrentChain Chain { get; set; }
        private IWatchOnlyWalletManager WatchOnlyWalletManager { get; set; }
        private Network Network { get; set; }
        private IWalletManager WalletManager { get; set; }

        public FullNodeWalletCache(ConcurrentChain chain, IWalletManager walletManager, IWatchOnlyWalletManager watchOnlyWalletManager, Network network)
        {
            this.Chain = chain ?? throw new ArgumentNullException(nameof(chain));
            this.WatchOnlyWalletManager = watchOnlyWalletManager ?? throw new ArgumentNullException(nameof(WatchOnlyWalletManager));
            this.Network = network ?? throw new ArgumentNullException(nameof(network));
            this.WalletManager = walletManager ?? throw new ArgumentNullException(nameof(walletManager));
        }

        public int BlockCount => Chain.Height;

        private Transaction GetTransactionOrNull(Stratis.Bitcoin.Features.Wallet.TransactionData transactionData)
        {
            try
            {
                var transaction = transactionData?.Transaction;
                return transaction;
            }
            catch
            {
                return null;
            }
        }
        private Transaction GetTransactionOrNull(Stratis.Bitcoin.Features.WatchOnlyWallet.TransactionData transactionData)
        {
            try
            {
                var transaction = transactionData?.Transaction;
                return transaction;
            }
            catch
            {
                return null;
            }
        }

        private static readonly SemaphoreSlim SemFindTx = new SemaphoreSlim(1, 1);
        public async Task<IEnumerable<TransactionInformation>> FindAllTransactionsAsync()
        {
            await SemFindTx.WaitAsync().ConfigureAwait(false);
            try
            {
                var allTransactions = new HashSet<TransactionInformation>();

                #region AllWatchOnlyTransactions
                foreach (var transactionData in WatchOnlyWalletManager
                    ?.GetWatchOnlyWallet()
                    ?.WatchedAddresses
                    ?.Values
                    ?.SelectMany(x => x.Transactions)
                    ?.Select(x => x.Value)
                    ?? Enumerable.Empty<Stratis.Bitcoin.Features.WatchOnlyWallet.TransactionData>())
                {
                    var transaction = GetTransactionOrNull(transactionData);
                    if (transaction == null) continue;

                    var confirmations = 0;
                    MerkleBlock proof = null;
                    if (transactionData.BlockHash != null)
                    {
                        var block = Chain?.GetBlock(transactionData.BlockHash);
                        if (block != null)
                        {
                            confirmations = Chain.Height - block.Height + 1;

                            if (transactionData.MerkleProof != null)
                            {
                                proof = new MerkleBlock()
                                {
                                    Header = block.Header,
                                    PartialMerkleTree = transactionData.MerkleProof
                                };
                            }
                        }
                    }

                    var transactionInformation = new TransactionInformation
                    {
                        Transaction = transaction,
                        Confirmations = confirmations,
                        MerkleProof = proof
                    };

                    allTransactions.Add(transactionInformation);
                }
                #endregion

                #region AllWalletTransactions
                foreach (var wallet in ((WalletManager)WalletManager)
                    ?.Wallets
                    ?? Enumerable.Empty<Wallet>())
                {
                    foreach (var transactionData in wallet
                    ?.GetAllTransactionsByCoinType((CoinType)Network.Consensus.CoinType)
                    ?? Enumerable.Empty<Stratis.Bitcoin.Features.Wallet.TransactionData>())
                    {
                        var transaction = GetTransactionOrNull(transactionData);
                        if (transaction == null) continue;

                        var confirmations = 0;
                        MerkleBlock proof = null;
                        if (transactionData.BlockHash != null)
                        {
                            var block = Chain?.GetBlock(transactionData.BlockHash);
                            if (block != null)
                            {
                                confirmations = Chain.Height - block.Height + 1;

                                if (transactionData.MerkleProof != null)
                                {
                                    proof = new MerkleBlock()
                                    {
                                        Header = block.Header,
                                        PartialMerkleTree = transactionData.MerkleProof
                                    };
                                }
                            }
                        }

                        var transactionInformation = new TransactionInformation
                        {
                            Transaction = transaction,
                            Confirmations = confirmations,
                            MerkleProof = proof
                        };

                        allTransactions.Add(transactionInformation);
                    }
                }
                #endregion

                foreach (var transactionData in WatchOnlyWalletManager
                ?.GetWatchOnlyWallet()
                ?.WatchedTransactions
                ?.Values
                ?? Enumerable.Empty<Stratis.Bitcoin.Features.WatchOnlyWallet.TransactionData>())
                {
                    var transaction = GetTransactionOrNull(transactionData);
                    if (transaction == null) continue;

                    var confirmations = 0;
                    MerkleBlock proof = null;
                    if (transactionData.BlockHash != null)
                    {
                        var block = Chain?.GetBlock(transactionData.BlockHash);
                        if (block != null)
                        {
                            confirmations = Chain.Height - block.Height + 1;

                            if (transactionData.MerkleProof != null)
                            {
                                proof = new MerkleBlock()
                                {
                                    Header = block.Header,
                                    PartialMerkleTree = transactionData.MerkleProof
                                };
                            }
                        }
                    }

                    var transactionInformation = new TransactionInformation
                    {
                        Transaction = transaction,
                        Confirmations = confirmations,
                        MerkleProof = proof
                    };

                    allTransactions.Add(transactionInformation);
                }

                await SemImpUncTxs.WaitAsync().ConfigureAwait(false);
                try
                {
                    var toRemove = new HashSet<uint256>();
                    foreach (var transaction in importedUnconfirmedTransactions)
                    {
                        if (allTransactions.Select(x => x.Transaction.GetHash()).Contains(transaction.Transaction.GetHash()))
                        {
                            toRemove.Add(transaction.Transaction.GetHash());
                        }
                        else
                        {
                            allTransactions.Add(transaction);
                        }
                    }
                    foreach (var txid in toRemove)
                    {
                        importedUnconfirmedTransactions.RemoveWhere(x => x.Transaction.GetHash() == txid);
                    }
                }
                finally
                {
                    SemImpUncTxs.SafeRelease();
                }

                return allTransactions.OrderBy(x => x.Confirmations);
            }
            finally
            {
                SemFindTx.SafeRelease();
            }
        }

        private HashSet<TransactionInformation> importedUnconfirmedTransactions = new HashSet<TransactionInformation>();
        private static readonly SemaphoreSlim SemImpUncTxs = new SemaphoreSlim(1, 1);
        public async Task ImportUnconfirmedTransaction(Transaction transaction)
        {
            await SemImpUncTxs.WaitAsync().ConfigureAwait(false);
            try
            {
                importedUnconfirmedTransactions.Add(new TransactionInformation
                {
                    Confirmations = 0,
                    MerkleProof = null,
                    Transaction = transaction
                });
            }
            finally
            {
                SemImpUncTxs.SafeRelease();
            }
        }
    }
}
