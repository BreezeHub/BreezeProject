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

namespace Breeze.TumbleBit.Client.Services
{
    public class FullNodeBlockExplorerService : IBlockExplorerService
    {
        FullNodeWalletCache _Cache;
        private TumblingState tumblingState;

        public FullNodeBlockExplorerService(FullNodeWalletCache cache, IRepository repo, TumblingState tumblingState)
        {
            if (repo == null)
                throw new ArgumentNullException("repo");
            if (cache == null)
                throw new ArgumentNullException("cache");
            if (tumblingState == null)
                throw new ArgumentNullException("tumblingState");

            _Repo = repo;
            _Cache = cache;
            this.tumblingState = tumblingState;
        }

        IRepository _Repo;

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

        public TransactionInformation[] GetTransactions(Script scriptPubKey, bool withProof)
        {
            if (scriptPubKey == null)
                throw new ArgumentNullException(nameof(scriptPubKey));
            
            var address = scriptPubKey.GetDestinationAddress(this.tumblingState.TumblerNetwork);
            if (address == null)
                return new TransactionInformation[0];

            var walletTransactions = _Cache.GetEntries();
            List<TransactionInformation> results = Filter(walletTransactions, !withProof, address);

            if (withProof)
            {
                bool found;
                foreach (var tx in results.ToList())
                {
                    found = false;
                    MerkleBlock proof = null;

                    // TODO: Not efficient

                    foreach (var wallet in this.tumblingState.walletManager.Wallets)
                    {
                        if (found)
                            break;

                        foreach (var account in wallet.GetAccountsByCoinType(this.tumblingState.coinType))
                        {
                            var txData = account.GetTransactionsById(tx.Transaction.GetHash());
                            if (txData != null)
                            {
                                found = true;

                                // TODO: Is it possible for GetTransactionsById to return multiple results?
                                var trx = txData.First<Stratis.Bitcoin.Features.Wallet.TransactionData>();

                                proof = new MerkleBlock();
                                proof.ReadWrite(Encoders.Hex.DecodeData(trx.MerkleProof.ToHex()));

                                tx.MerkleProof = proof;

                                break;
                            }
                        }
                    }

                    if (!found)
                    {
                        results.Remove(tx);
                        continue;
                    }
                }
            }
            return results.ToArray();
        }

        private List<TransactionInformation> QueryWithListReceivedByAddress(bool withProof, BitcoinAddress address)
        {
            // List all transactions involving a particular address, including those in watch-only wallet
            // (zero confirmations are acceptable)

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

            foreach (var wallet in this.tumblingState.walletManager.Wallets)
            {
                //var wallet = this.tumblingState.walletManager.GetWallet(walletName);
                foreach (var walletTx in wallet.GetAllTransactionsByCoinType(this.tumblingState.coinType))
                {
                    if (address == walletTx.ScriptPubKey.GetDestinationAddress(this.tumblingState.TumblerNetwork))
                    {
                        txIdList.Add(walletTx.Id);
                    }
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

        private List<TransactionInformation> Filter(FullNodeWalletEntry[] entries, bool includeUnconf, BitcoinAddress address)
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

                    if (tx.Outputs.Any(o => o.ScriptPubKey == address.ScriptPubKey) ||
                       tx.Inputs.Any(o => o.ScriptSig.GetSigner().ScriptPubKey == address.ScriptPubKey))
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
                foreach (WatchedAddress addr in this.tumblingState.watchOnlyWalletManager.GetWatchOnlyWallet()
                    .WatchedAddresses.Values)
                {
                    foreach (Stratis.Bitcoin.Features.WatchOnlyWallet.TransactionData trans in addr.Transactions.Values)
                    {
                        if (trans.Transaction.GetHash() == txId)
                        {
                            // Need number of confirmations as well
                            var watchBlock = this.tumblingState.chain.GetBlock(trans.BlockHash);
                            var watchConfCount = Math.Max(0, (this.tumblingState.chain.Tip.Height - watchBlock.Height));

                            return new TransactionInformation
                            {
                                Confirmations = watchConfCount,
                                Transaction = trans.Transaction
                            };
                        }
                    }
                }
                
                Console.WriteLine("*** Transaction not found in watch-only wallet: " + txId);
                return null;

                // Transaction was not in watch-only wallet
                /*
                foreach (var walletTx in this.tumblingState.OriginWallet.GetAllTransactionsByCoinType(this.tumblingState.coinType))
                {
                    if (walletTx.Id != txId)
                        continue;

                    var confCount = this.tumblingState.chain.Tip.Height - walletTx.BlockHeight;

                    if (confCount == null)
                        confCount = 0;

                    return new TransactionInformation
                    {
                        Confirmations = confCount,
                        Transaction = trx
                    };
                }
                */
            }
            // TODO: Replace this with better exception type
            catch (Exception)
            {
                Console.WriteLine("Error looking up transaction: " + txId.ToString());
                return null;
            }
        }

        public void Track(Script scriptPubkey)
        {
            this.tumblingState.watchOnlyWalletManager.WatchAddress(scriptPubkey.GetDestinationAddress(this.tumblingState.TumblerNetwork).ToString());
        }

        public int GetBlockConfirmations(uint256 blockId)
        {
            var block = this.tumblingState.chain.GetBlock(blockId);
            var tipHeight = this.tumblingState.chain.Tip.Height;
            var confirmations = tipHeight - block.Height;
            var confCount = Math.Max(0, confirmations);

            return confCount;
        }

        public bool TrackPrunedTransaction(Transaction transaction, MerkleBlock merkleProof)
        {
            var chainBlock = this.tumblingState.chain.GetBlock(merkleProof.Header.GetHash());

            // TODO: We cannot obtain the block to pass to ProcessTransaction. Is this going to be a problem?
            this.tumblingState.walletManager.ProcessTransaction(transaction, chainBlock.Height, null);

            _Cache.ImportTransaction(transaction, GetBlockConfirmations(merkleProof.Header.GetHash()));

            return true;
        }
    }
}
