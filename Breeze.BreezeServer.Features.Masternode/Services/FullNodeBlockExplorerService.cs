using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using NTumbleBit.Services;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Features.WatchOnlyWallet;
using Stratis.Bitcoin.Interfaces;

namespace Breeze.BreezeServer.Features.Masternode.Services
{
    public class FullNodeBlockExplorerService : IBlockExplorerService
    {
        private FullNodeWalletCache cache;
        private ConcurrentChain chain;
        private Network network;
        private IWalletManager walletManager { get; set; }
        private IWatchOnlyWalletManager watchOnlyWalletManager;
        private IConnectionManager connectionManager;

        public FullNodeBlockExplorerService(FullNodeWalletCache cache, ConcurrentChain chain, Network network, IWalletManager walletManager, IWatchOnlyWalletManager watchOnlyWalletManager, IConnectionManager connectionManager)
        {
            this.cache = cache ?? throw new ArgumentNullException(nameof(cache));
            this.chain = chain ?? throw new ArgumentNullException(nameof(chain));
            this.network = network ?? throw new ArgumentNullException(nameof(network));
            this.walletManager = walletManager ?? throw new ArgumentNullException(nameof(walletManager));
            this.watchOnlyWalletManager = watchOnlyWalletManager;
            this.connectionManager = connectionManager ?? throw new ArgumentNullException(nameof(connectionManager));
        }

        public int GetCurrentHeight()
        {
            return this.walletManager.LastBlockHeight();
        }

        public uint256 WaitBlock(uint256 currentBlock, CancellationToken cancellation = default(CancellationToken))
        {
            while (true)
            {
                cancellation.ThrowIfCancellationRequested();
                uint256 h = ((WalletManager)this.walletManager).LastReceivedBlockHash();

                if (h != currentBlock)
                {
                    return h;
                }
                cancellation.WaitHandle.WaitOne(5000);
            }
        }

        public TransactionInformation GetTransaction(uint256 txId, bool withProof)
        {
            if (txId == null)
                throw new ArgumentNullException(nameof(txId));

            // Perform the search synchronously
            foreach (var output in Task.Run(cache.FindAllTransactionsAsync).Result)
            {
                if (output.Transaction.GetHash() == txId)
                {
                    if (withProof && output.MerkleProof == null)
                        return null;

                    return output;
                }
            }

            // In the original code null gets returned if the transaction isn't found
            return null;
        }

        public async Task<ICollection<TransactionInformation>> GetTransactionsAsync(Script scriptPubKey, bool withProof)
        {
            var foundTransactions = new HashSet<TransactionInformation>();
            foreach (var transaction in await cache.FindAllTransactionsAsync().ConfigureAwait(false))
            {
                foreach (var output in transaction.Transaction.Outputs)
                {
                    if (output.ScriptPubKey.Hash == scriptPubKey.Hash)
                    {
                        foundTransactions.Add(transaction);
                    }
                }
            }

            if (withProof)
            {
                foundTransactions.RemoveWhere(x => x.Confirmations == 0 || x.MerkleProof == null);
            }

            return foundTransactions.OrderBy(x => x.Confirmations).ToArray();
        }

        public TransactionInformation GetTransaction(uint256 txId)
        {
            try
            {
                foreach (var tx in cache.FindAllTransactionsAsync().Result)
                {
                    var hash = tx?.Transaction?.GetHash();
                    if (hash == null) continue;
                    if (hash == txId) return tx;
                }
                return null;
            }
            catch
            {
                return null;
            }
        }

        public int GetBlockConfirmations(uint256 blockId)
        {
            try
            {
                var block = this.chain.GetBlock(blockId);

                if (block == null)
                    return 0;

                return this.chain.Tip.Height - block.Height + 1;
            }
            catch
            {
                return 0;
            }
        }

        public async Task TrackAsync(Script scriptPubkey)
        {
            await Task.Run(() =>
                this.watchOnlyWalletManager.WatchScriptPubKey(scriptPubkey)
            ).ConfigureAwait(false);
        }

        public async Task<bool> TrackPrunedTransactionAsync(Transaction transaction, MerkleBlock merkleProof)
        {
            await Task.Run(() =>
            {
                this.watchOnlyWalletManager.StoreTransaction(
                    new Stratis.Bitcoin.Features.WatchOnlyWallet.TransactionData
                    {
                        BlockHash = merkleProof.Header.GetHash(),
                        Hex = transaction.ToHex(),
                        Id = transaction.GetHash(),
                        MerkleProof = merkleProof.PartialMerkleTree
                    });
            }).ConfigureAwait(false);

            return true;
        }

        public int GetConnectionCount()
        {
            return this.connectionManager.ConnectedPeers.Count();
        }
    }
}
