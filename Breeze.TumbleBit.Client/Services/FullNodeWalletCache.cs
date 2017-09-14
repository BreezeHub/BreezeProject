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
using System.IO;

namespace Breeze.TumbleBit.Client.Services
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
        private TumblingState TumblingState { get; }

        public FullNodeWalletCache(TumblingState tumblingState)
        {
            TumblingState = tumblingState ?? throw new ArgumentNullException(nameof(tumblingState));
        }

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

        private static readonly SemaphoreSlim SemaphoreFindTx = new SemaphoreSlim(1, 1);
        public async Task<IEnumerable<TransactionInformation>> FindAllTransactionsAsync()
        {
            await SemaphoreFindTx.WaitAsync().ConfigureAwait(false);
            try
            {
                // temporary log, if you see it delete it
                File.AppendAllText("FindAllTransactionsAsyncCallings.txt", DateTime.Now.ToString() + Environment.NewLine);

                var allTransactions = new HashSet<TransactionInformation>();

                #region AllWatchOnlyTransactions
                foreach (var transactionData in TumblingState
                    ?.WatchOnlyWalletManager
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
                        var block = TumblingState.Chain?.GetBlock(transactionData.BlockHash);
                        if (block != null)
                        {
                            confirmations = TumblingState.Chain.Height - block.Height + 1;

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
                foreach (var wallet in TumblingState
                    ?.WalletManager
                    ?.Wallets
                    ?? Enumerable.Empty<Wallet>())
                {
                    foreach (var transactionData in wallet
                    ?.GetAllTransactionsByCoinType((CoinType)TumblingState.TumblerNetwork.Consensus.CoinType)
                    ?? Enumerable.Empty<Stratis.Bitcoin.Features.Wallet.TransactionData>())
                    {
                        var transaction = GetTransactionOrNull(transactionData);
                        if (transaction == null) continue;

                        var confirmations = 0;
                        MerkleBlock proof = null;
                        if (transactionData.BlockHash != null)
                        {
                            var block = TumblingState.Chain?.GetBlock(transactionData.BlockHash);
                            if (block != null)
                            {
                                confirmations = TumblingState.Chain.Height - block.Height + 1;

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

                return allTransactions.OrderBy(x => x.Confirmations);
            }
            finally
            {
                SemaphoreFindTx.Release();
            }
        }
    }
}
