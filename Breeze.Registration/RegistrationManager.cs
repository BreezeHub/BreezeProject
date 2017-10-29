using System;
using System.Threading.Tasks;
using NBitcoin;
using BreezeCommon;
using Stratis.Bitcoin.Features.WatchOnlyWallet;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;

namespace Breeze.Registration
{
    public class RegistrationManager : IRegistrationManager
    {
        public Money MASTERNODE_COLLATERAL_THRESHOLD = new Money(250000, MoneyUnit.BTC);

        private ILoggerFactory loggerFactory;
        private RegistrationStore registrationStore;
        private Network network;
        private IWatchOnlyWalletManager watchOnlyWalletManager;

        private ILogger logger;

        public RegistrationManager()
        {

        }

        public void Initialize(ILoggerFactory loggerFactory, RegistrationStore registrationStore, bool isBitcoin, Network network, IWatchOnlyWalletManager watchOnlyWalletManager)
        {
            this.loggerFactory = loggerFactory;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.registrationStore = registrationStore;
            this.network = network;
            this.watchOnlyWalletManager = watchOnlyWalletManager;

            logger.LogInformation("Initialized RegistrationFeature");
        }

        /// <inheritdoc />
        public void ProcessBlock(int height, Block block)
        {
            // Check for any server registration transactions
            if (block.Transactions != null)
            {
                foreach (Transaction tx in block.Transactions)
                {
                    // Check if the transaction has the Breeze registration marker output (literal text BREEZE_REGISTRATION_MARKER)
                    if (tx.Outputs[0].ScriptPubKey.ToHex().ToLower() == "6a1a425245455a455f524547495354524154494f4e5f4d41524b4552")
                    {
                        this.logger.LogDebug("Received a new registration transaction: " + tx.GetHash());

                        try
                        {
                            RegistrationToken registrationToken = new RegistrationToken();
                            registrationToken.ParseTransaction(tx, this.network);
                            MerkleBlock merkleBlock = new MerkleBlock(block, new uint256[] { tx.GetHash() });
                            RegistrationRecord registrationRecord = new RegistrationRecord(DateTime.Now, Guid.NewGuid(), tx.GetHash().ToString(), tx.ToHex(), registrationToken, merkleBlock.PartialMerkleTree);
                            this.registrationStore.Add(registrationRecord);

                            this.logger.LogDebug("Registration transaction for server collateral address: " + registrationRecord.Record.ServerId);
                            this.logger.LogDebug("Server Onion address: " + registrationRecord.Record.OnionAddress);
                            this.logger.LogDebug("Server configuration hash: " + registrationRecord.Record.ConfigurationHash);

                            this.watchOnlyWalletManager.WatchAddress(registrationRecord.Record.ServerId);
                        }
                        catch (Exception e)
                        {
                            this.logger.LogDebug("Failed to parse registration transaction: " + tx.GetHash());
                        }
                    }
                }

                WatchOnlyWallet watchOnlyWallet = this.watchOnlyWalletManager.GetWatchOnlyWallet();

                // TODO: Need to have 'current height' field in watch-only wallet so that we don't start rebalancing collateral balances before the latest block has been processed & incorporated
                // Perform watch-only wallet housekeeping - iterate through known servers
                HashSet<string> knownServers = new HashSet<string>();

                foreach (RegistrationRecord record in this.registrationStore.GetAll())
                {
                    if (knownServers.Add(record.Record.ServerId))
                    {
                        this.logger.LogDebug("Calculating collateral balance for server: " + record.Record.ServerId);

                        //var addrToCheck = new WatchedAddress
                        //{
                        //    Script = BitcoinAddress.Create(record.Record.ServerId, this.network).ScriptPubKey,
                        //    Address = record.Record.ServerId
                        //};

                        var scriptToCheck = BitcoinAddress.Create(record.Record.ServerId, this.network).ScriptPubKey;

                        if (!watchOnlyWallet.WatchedAddresses.ContainsKey(scriptToCheck.ToString()))
                        {
                            this.logger.LogDebug("Server address missing from watch-only wallet. Deleting stored registrations for server: " + record.Record.ServerId);
                            this.registrationStore.DeleteAllForServer(record.Record.ServerId);
                            continue;
                        }

                        // Initially the server's balance is zero
                        Money serverCollateralBalance = new Money(0);

                        // Need this for looking up other transactions in the watch-only wallet
                        TransactionData prevTransaction;

                        // TODO: Move balance evaluation logic into helper methods in watch-only wallet itself
                        // Now evaluate the watch-only balance for this server
                        foreach (string txId in watchOnlyWallet.WatchedAddresses[scriptToCheck.ToString()].Transactions.Keys)
                        {
                            TransactionData transaction = watchOnlyWallet.WatchedAddresses[scriptToCheck.ToString()].Transactions[txId];

                            // First check if the inputs contain the watched address
                            foreach (TxIn input in transaction.Transaction.Inputs)
                            {
                                // See if we have the previous transaction in our watch-only wallet.
                                watchOnlyWallet.WatchedAddresses[scriptToCheck.ToString()].Transactions.TryGetValue(input.PrevOut.Hash.ToString(), out prevTransaction);

                                // If it is null, it can't be related to one of the watched addresses (or it is the very first watched transaction)
                                if (prevTransaction == null)
                                    continue;

                                if (prevTransaction.Transaction.Outputs[input.PrevOut.N].ScriptPubKey == scriptToCheck)
                                {
                                    // Input = funds are being paid out of the address in question

                                    // Computing the input value is a bit more complex than it looks, as the value is not directly stored
                                    // in a TxIn object. We need to check the output being spent by the input to get this information.
                                    // But even an OutPoint does not contain the Value - we need to check the other transactions in the
                                    // watch-only wallet to see if we have the prior transaction being referenced.

                                    // This does imply that the earliest transaction in the watch-only wallet (for this address) will not
                                    // have full previous transaction information stored. Therefore we can only reason about the address
                                    // balance after a given block height; any prior transactions are ignored.

                                    serverCollateralBalance -= prevTransaction.Transaction.Outputs[input.PrevOut.N].Value;
                                }
                            }

                            // Check if the outputs contain the watched address
                            foreach (var output in transaction.Transaction.Outputs)
                            {
                                if (output.ScriptPubKey.GetScriptAddress(this.network).ToString() == record.Record.ServerId)
                                {
                                    // Output = funds are being paid into the address in question
                                    serverCollateralBalance += output.Value;
                                }
                            }
                        }

                        this.logger.LogDebug("Collateral balance for server " + record.Record.ServerId + " is " + serverCollateralBalance.ToString());

                        if (serverCollateralBalance < MASTERNODE_COLLATERAL_THRESHOLD)
                        {
                            // Remove server registrations
                            //this.logger.LogDebug("Deleting stored registrations for server: " + record.Record.ServerId);
                            //this.registrationStore.DeleteAllForServer(record.Record.ServerId);

                            // TODO: Remove unneeded transactions from the watch-only wallet?

                            // TODO: Need to make the TumbleBitFeature change its server address if this is the address it was using
                        }
                    }
                }

                this.watchOnlyWalletManager.SaveWatchOnlyWallet();
            }
        }

        public void Dispose()
        {
        }
    }
}
