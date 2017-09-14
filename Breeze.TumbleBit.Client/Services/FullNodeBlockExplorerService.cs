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
        FullNodeWalletCache _Cache;
        IRepository _Repo;
        private TumblingState tumblingState;

        public FullNodeBlockExplorerService(FullNodeWalletCache cache, IRepository repo, TumblingState tumblingState)
        {
            if (cache == null)
                throw new ArgumentNullException("cache");
            if (repo == null)
                throw new ArgumentNullException("repo");
            if (tumblingState == null)
                throw new ArgumentNullException("tumblingState");

            _Cache = cache;
            _Repo = repo;
            this.tumblingState = tumblingState;
        }

        public int GetCurrentHeight()
        {
            return _Cache.BlockCount;
        }

        public uint256 WaitBlock(uint256 currentBlock, CancellationToken cancellation = default(CancellationToken))
        {
            while (true)
            {
                cancellation.ThrowIfCancellationRequested();
                var h = this.tumblingState.chain.Tip.Header.GetHash();

                if (h != currentBlock)
                {
                    _Cache.Refresh(h);
                    return h;
                }
                cancellation.WaitHandle.WaitOne(5000);
            }
        }

        public async Task<ICollection<TransactionInformation>> GetTransactionsAsync(Script scriptPubKey, bool withProof)
        {
            if (scriptPubKey == null)
                throw new ArgumentNullException(nameof(scriptPubKey));


            var results = _Cache
                                        .GetEntriesFromScript(scriptPubKey)
                                        .Select(entry => new TransactionInformation()
                                        {
                                            Confirmations = entry.Confirmations,
                                            Transaction = entry.Transaction
                                        }).ToList();

            if (withProof)
            {
                foreach (var tx in results.ToList())
                {
                    bool found = false;
                    var completion = new TaskCompletionSource<MerkleBlock>();
                    bool isRequester = true;
                    var txid = tx.Transaction.GetHash();
                    _GettingProof.AddOrUpdate(txid, completion, (k, o) =>
                    {
                        isRequester = false;
                        completion = o;
                        return o;
                    });
                    if (isRequester)
                    {
                        try
                        {
                            MerkleBlock proof = null;

                            foreach (var account in this.tumblingState.OriginWallet.GetAccountsByCoinType(this.tumblingState.coinType))
                            {
                                var txData = account.GetTransactionsById(tx.Transaction.GetHash());

                                if (txData != null)
                                {
                                    // TODO: Is it possible for GetTransactionsById to return multiple results?
                                    var trx = txData.First<Stratis.Bitcoin.Features.Wallet.TransactionData>();

                                    Console.WriteLine("Transaction " + trx.Id + " confirmation status: " + trx.IsConfirmed());
                                    Console.WriteLine("Transaction " + trx.Id + " block hash: " + trx.BlockHash);

                                    // Transaction is not confirmed yet - do not yet have a Merkle proof for it as there is no block
                                    if (trx.BlockHash == null)
                                    {
                                        completion.TrySetResult(null);
                                        break;
                                    }

                                    found = true;

                                    try
                                    {
                                        proof = new MerkleBlock()
                                        {
                                            Header = this.tumblingState.chain.GetBlock(trx.BlockHash).Header,
                                            PartialMerkleTree = trx.MerkleProof
                                        };
                                    }
                                    catch (Exception e)
                                    {
                                        Console.WriteLine("Could not create Merkle block for transaction " + tx.Transaction.GetHash() + " in block " + trx.BlockHash);
                                    }

                                    tx.MerkleProof = proof;
                                    completion.TrySetResult(proof);

                                    break;
                                }
                            }

                            // The transaction should not be in both wallets normally
                            if (!found)
                            {
                                foreach (WatchedAddress addr in this.tumblingState.watchOnlyWalletManager.GetWatchOnlyWallet().WatchedAddresses.Values)
                                {
                                    addr.Transactions.TryGetValue(tx.Transaction.GetHash().ToString(), out Stratis.Bitcoin.Features.WatchOnlyWallet.TransactionData woTx);

                                    if (woTx != null)
                                    {
                                        Console.WriteLine("Watch-only transaction " + woTx.Id + " block hash: " + woTx.BlockHash);

                                        if (woTx.BlockHash == null)
                                        {
                                            Console.WriteLine("Watch-only transaction is not confirmed yet - do not yet have a Merkle proof for it as there is no block");
                                            completion.TrySetResult(null);
                                            break;
                                        }

                                        if (woTx.MerkleProof == null)
                                        {
                                            Console.WriteLine("Watch-only transaction has no Merkle proof recorded");
                                            completion.TrySetResult(null);
                                            break;
                                        }

                                        found = true;

                                        try
                                        {
                                            proof = new MerkleBlock()
                                            {
                                                Header = this.tumblingState.chain.GetBlock(woTx.BlockHash).Header,
                                                PartialMerkleTree = woTx.MerkleProof
                                            };
                                        }
                                        catch (Exception e)
                                        {
                                            Console.WriteLine("Could not create Merkle block for transaction " + tx.Transaction.GetHash() + " in block " + woTx.BlockHash);
                                        }

                                        tx.MerkleProof = proof;
                                        completion.TrySetResult(proof);

                                        break;
                                    }
                                }
                            }

                            if (!found)
                            {
                                completion.TrySetResult(null);
                                continue;
                            }
                        }
                        catch (Exception ex) { completion.TrySetException(ex); }
                        finally { _GettingProof.TryRemove(txid, out completion); }
                    }

                    var merkleBlock = await completion.Task.ConfigureAwait(false);
                    if (merkleBlock == null)
                        results.Remove(tx);
                }
            }
            return results;
        }

        ConcurrentDictionary<uint256, TaskCompletionSource<MerkleBlock>> _GettingProof = new ConcurrentDictionary<uint256, TaskCompletionSource<MerkleBlock>>();

        private List<TransactionInformation> QueryWithListReceivedByAddress(bool withProof, BitcoinAddress address)
        {
            // List all transactions involving a particular address, including those in watch-only wallet
            // (zero confirmations are acceptable)

            // Original RPC call:
            //var result = RPCClient.SendCommand("listreceivedbyaddress", 0, false, true, address.ToString());

            List<uint256> txIdList = new List<uint256>();

            // First examine watch-only wallet
            var watchOnlyWallet = this.tumblingState.watchOnlyWalletManager.GetWatchOnlyWallet();

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
                        if (address == vout.ScriptPubKey.GetDestinationAddress(this.tumblingState.TumblerNetwork))
                        {
                             txIdList.Add(watchOnlyTx.Value.Transaction.GetHash());
                        }
                    }
                }
            }

            // Search transactions in regular wallet for matching address criteria

            foreach (var walletTx in this.tumblingState.OriginWallet.GetAllTransactionsByCoinType(this.tumblingState.coinType))
            {
                if (address == walletTx.ScriptPubKey.GetDestinationAddress(this.tumblingState.TumblerNetwork))
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
                    var tx = _Cache.GetTransaction(obj.TransactionId);

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
                foreach (WatchedAddress addr in this.tumblingState.watchOnlyWalletManager.GetWatchOnlyWallet().WatchedAddresses.Values)
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
                foreach (var walletTx in this.tumblingState.OriginWallet.GetAllTransactionsByCoinType(this.tumblingState.coinType))
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
            await Task.Run(() => this.tumblingState.watchOnlyWalletManager.WatchAddress(scriptPubkey.GetDestinationAddress(this.tumblingState.TumblerNetwork).ToString())).ConfigureAwait(false);
        }

        public int GetBlockConfirmations(uint256 blockId)
        {
            if (blockId == null)
                return 0;

            ChainedBlock block = this.tumblingState.chain.GetBlock(blockId);

            if (block == null)
                return 0;

            int tipHeight = this.tumblingState.chain.Tip.Height;
            int confirmations = tipHeight - block.Height + 1;
            int confCount = Math.Max(0, confirmations);

            return confCount;
        }

        public async Task<bool> TrackPrunedTransactionAsync(Transaction transaction, MerkleBlock merkleProof)
        {
            bool success = false;

            ChainedBlock chainBlock = this.tumblingState.chain.GetBlock(merkleProof.Header.GetHash());

            if (chainBlock == null)
                return false;

            await Task.Run(() =>
            {
                // TODO: We cannot obtain the complete block to pass to ProcessTransaction. Is this going to be a problem?
                this.tumblingState.walletManager.ProcessTransaction(transaction, chainBlock.Height, null);

                // TODO: Track via Watch Only wallet instead?

                // We don't really have the same error conditions available that the original code used to determine success
                success = true;

                if (success)
                {
                    _Cache.ImportTransaction(transaction, GetBlockConfirmations(merkleProof.Header.GetHash()));
                }
            }).ConfigureAwait(false);

            return success;
        }
    }
}
