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

        public int GetCurrentHeight()
        {
            return TumblingState.Chain.Height;
        }

        public uint256 WaitBlock(uint256 currentBlock, CancellationToken cancellation = default(CancellationToken))
        {
            while (true)
            {
                cancellation.ThrowIfCancellationRequested();
                var h = this.TumblingState.Chain.Tip.Header.GetHash();

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

        private List<TransactionInformation> QueryWithListReceivedByAddress(bool withProof, BitcoinAddress address)
        {
            // List all transactions involving a particular address, including those in watch-only wallet
            // (zero confirmations are acceptable)

            // Original RPC call:
            //var result = RPCClient.SendCommand("listreceivedbyaddress", 0, false, true, address.ToString());

            List<uint256> txIdList = new List<uint256>();

            // First examine watch-only wallet
            var watchOnlyWallet = this.TumblingState.WatchOnlyWalletManager.GetWatchOnlyWallet();

            // TODO: This seems highly inefficient, maybe we need a cache or quicker lookup mechanism
            foreach (var watchedAddressKeyValue in watchOnlyWallet.WatchedAddresses)
            {
                if (watchedAddressKeyValue.Value.Script != address.ScriptPubKey)
                    continue;

                var watchedAddress = watchedAddressKeyValue.Value;

                foreach (var watchOnlyTx in watchedAddress.Transactions)
                {
                    // Looking for funds received by address only, so only examine transaction outputs
                    foreach (var vout in watchOnlyTx.Value.Transaction.Outputs)
                    {
                        // Look at each of the addresses contained in the scriptPubKey to see if they match
                        if (address == vout.ScriptPubKey.GetDestinationAddress(this.TumblingState.TumblerNetwork))
                        {
                             txIdList.Add(watchOnlyTx.Value.Transaction.GetHash());
                        }
                    }
                }
            }

            // Search transactions in regular wallet for matching address criteria

            foreach (var walletTx in this.TumblingState.OriginWallet.GetAllTransactionsByCoinType(this.TumblingState.CoinType))
            {
                if (address == walletTx.ScriptPubKey.GetDestinationAddress(this.TumblingState.TumblerNetwork))
                {
                    txIdList.Add(walletTx.Id);
                }
            }

            if (txIdList.Count == 0)
                return null;

            HashSet<uint256> resultsSet = new HashSet<uint256>();
            List<TransactionInformation> results = new List<TransactionInformation>();
            foreach (var txId in txIdList)
            {
                // May have duplicates
                if (!resultsSet.Contains(txId))
                {
                    var tx = GetTransaction(txId);
                    if (tx == null || (withProof && tx.Confirmations == 0))
                        continue;
                    resultsSet.Add(txId);
                    results.Add(tx);
                }
            }
            return results;
        }

        private List<TransactionInformation> Filter(ICollection<FullNodeWalletEntry> entries, bool includeUnconf, Script scriptPubKey)
        {
            List<TransactionInformation> results = new List<TransactionInformation>();
            HashSet<uint256> resultsSet = new HashSet<uint256>();
            foreach (var obj in entries)
            {
                //May have duplicates
                if (!resultsSet.Contains(obj.TransactionId))
                {
                    var confirmations = obj.Confirmations;
                    var tx = Cache.FindAllTransactionsAsync().Result.Where(x=>x.Transaction.GetHash() == obj.TransactionId)?.FirstOrDefault()?.Transaction;

                    if (tx == null || (!includeUnconf && confirmations == 0))
                        continue;

                    if (tx.Outputs.Any(o => o.ScriptPubKey == scriptPubKey) ||
                       tx.Inputs.Any(o => o.ScriptSig.GetSigner().ScriptPubKey == scriptPubKey))
                    {

                        resultsSet.Add(obj.TransactionId);
                        results.Add(new TransactionInformation()
                        {
                            Transaction = tx,
                            Confirmations = confirmations
                        });
                    }
                }
            }
            return results;
        }

        public TransactionInformation GetTransaction(uint256 txId)
        {
            try
            {
                foreach (WatchedAddress addr in this.TumblingState.WatchOnlyWalletManager.GetWatchOnlyWallet().WatchedAddresses.Values)
                {
                    addr.Transactions.TryGetValue(txId.ToString(), out Stratis.Bitcoin.Features.WatchOnlyWallet.TransactionData trans);

                    if (trans != null)
                    {
                        return new TransactionInformation
                        {
                            Confirmations = GetBlockConfirmations(trans.BlockHash),
                            Transaction = trans.Transaction
                        };
                    }
                }
                
                // Transaction was not in watch-only wallet
                foreach (var walletTx in this.TumblingState.OriginWallet.GetAllTransactionsByCoinType(this.TumblingState.CoinType))
                {
                    if (walletTx.Id != txId)
                        continue;

                    return new TransactionInformation
                    {
                        Confirmations = GetBlockConfirmations(walletTx.BlockHash),
                        Transaction = walletTx.Transaction
                    };
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error looking up transaction " + txId + ": " + ex);
            }

            return null;
        }

        public async Task TrackAsync(Script scriptPubkey)
        {
            await Task.Run(() => this.TumblingState.WatchOnlyWalletManager.WatchAddress(scriptPubkey.GetDestinationAddress(this.TumblingState.TumblerNetwork).ToString())).ConfigureAwait(false);
        }

        public int GetBlockConfirmations(uint256 blockId)
        {
            if (blockId == null)
                return 0;

            ChainedBlock block = this.TumblingState.Chain.GetBlock(blockId);

            if (block == null)
                return 0;

            int tipHeight = this.TumblingState.Chain.Tip.Height;
            int confirmations = tipHeight - block.Height + 1;
            int confCount = Math.Max(0, confirmations);

            return confCount;
        }

        public async Task<bool> TrackPrunedTransactionAsync(Transaction transaction, MerkleBlock merkleProof)
        {
            bool success = false;

            ChainedBlock chainBlock = this.TumblingState.Chain.GetBlock(merkleProof.Header.GetHash());

            if (chainBlock == null)
                return false;

            await Task.Run(() =>
            {
                // TODO: We cannot obtain the complete block to pass to ProcessTransaction. Is this going to be a problem?
                this.TumblingState.WalletManager.ProcessTransaction(transaction, chainBlock.Height, null);

                // TODO: Track via Watch Only wallet instead?

                // We don't really have the same error conditions available that the original code used to determine success
                success = true;
            }).ConfigureAwait(false);

            return success;
        }
    }
}
