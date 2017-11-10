using NBitcoin.RPC;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin;
using Newtonsoft.Json.Linq;
using NBitcoin.DataEncoders;
using System.Threading;
using NTumbleBit.Services;
using Stratis.Bitcoin;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.WatchOnlyWallet;
using System.Collections.Concurrent;

namespace Breeze.TumbleBit.Client.Services
{
    public class FullNodeBlockExplorerService : IBlockExplorerService
    {
        private FullNodeWalletCache Cache { get; }
        private TumblingState TumblingState { get; }

        public FullNodeBlockExplorerService(FullNodeWalletCache cache, TumblingState tumblingState)
        {
            Cache = cache ?? throw new ArgumentNullException(nameof(cache));
            TumblingState = tumblingState ?? throw new ArgumentNullException(nameof(tumblingState));
        }

        public int GetCurrentHeight() => TumblingState.Chain.Height;

        public uint256 WaitBlock(uint256 currentBlock, CancellationToken cancellation = default(CancellationToken))
        {
            while (true)
            {
                cancellation.ThrowIfCancellationRequested();
                var h = TumblingState.Chain.Tip.HashBlock;

                if (h != currentBlock)
                {
                    return h;
                }
                cancellation.WaitHandle.WaitOne(5000);
            }
        }

        public async Task<ICollection<TransactionInformation>> GetTransactionsAsync(Script scriptPubKey, bool withProof)
        {
            var foundTransactions = new HashSet<TransactionInformation>();
            foreach(var transaction in await Cache.FindAllTransactionsAsync().ConfigureAwait(false))
            {
                foreach(var output in transaction.Transaction.Outputs)
                {
                    if(output.ScriptPubKey.Hash == scriptPubKey.Hash)
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
                foreach(var tx in Cache.FindAllTransactionsAsync().Result)
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
                var block = TumblingState.Chain.GetBlock(blockId);

                if (block == null)
                    return 0;

                return TumblingState.Chain.Tip.Height - block.Height + 1;
            }
            catch
            {
                return 0;
            }
        }

        public async Task TrackAsync(Script scriptPubkey)
        {
            await Task.Run(() =>
                TumblingState.WatchOnlyWalletManager.WatchScriptPubKey(scriptPubkey)
            ).ConfigureAwait(false);
        }

        public async Task<bool> TrackPrunedTransactionAsync(Transaction transaction, MerkleBlock merkleProof)
        {
            await Task.Run(() =>
            {
                TumblingState.WatchOnlyWalletManager.StoreTransaction(
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
    }
}
