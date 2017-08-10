using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

using Newtonsoft.Json.Linq;

using NBitcoin;
using NBitcoin.DataEncoders;
using NBitcoin.RPC;

using BreezeCommon;

namespace Breeze.BreezeServer
{
    public class BreezeRegistration
    {
        public bool CheckBreezeRegistration(BreezeConfiguration config, string regStorePath)
        {
			Network network = Network.StratisMain;
			if (config.IsTestNet)
			{
                network = Network.StratisTest;
			}

            // In order to determine if the registration sequence has been performed
            // before, and to see if a previous performance is still valid, interrogate
            // the database to see if any transactions have been recorded.

            RegistrationStore regStore = new RegistrationStore(regStorePath);

            List<RegistrationRecord> transactions = regStore.GetByServerId(config.TumblerEcdsaKeyAddress);

            // If no transactions exist, the registration definitely needs to be done
            if (transactions == null || transactions.Count == 0) { return false; }

            RegistrationRecord mostRecent = null;

            foreach (RegistrationRecord record in transactions)
            {
                // Find most recent transaction
                if (mostRecent == null)
                {
                    mostRecent = record;
                }

                if (record.RecordTimestamp > mostRecent.RecordTimestamp)
                    mostRecent = record;
            }

            // Check if the stored record matches the current configuration

            RegistrationToken registrationToken = mostRecent.Record;

            if (!config.Ipv4Address.Equals(registrationToken.Ipv4Addr))
                return false;

            if (!config.Ipv6Address.Equals(registrationToken.Ipv6Addr))
                return false;

            if (config.OnionAddress != registrationToken.OnionAddress)
                return false;

            if (config.Port != registrationToken.Port)
                return false;
            
            // TODO: Check if transaction is actually confirmed on the blockchain?

            return true;
        }

        public Transaction PerformBreezeRegistration(BreezeConfiguration config, string regStorePath)
        {
			Network network = Network.StratisMain;
			if (config.IsTestNet)
			{
				network = Network.StratisTest;
			}

            RPCHelper stratisHelper = null;
            RPCClient stratisRpc = null;
            BitcoinSecret privateKeyEcdsa = null;

            try {
                stratisHelper = new RPCHelper(network);
                stratisRpc = stratisHelper.GetClient(config.RpcUser, config.RpcPassword, config.RpcUrl);
                privateKeyEcdsa = stratisRpc.DumpPrivKey(BitcoinAddress.Create(config.TumblerEcdsaKeyAddress));
            }
            catch (Exception e) {
                Console.WriteLine("ERROR: Unable to retrieve private key to fund registration transaction");
				Console.WriteLine("Is the wallet unlocked?");
                Console.WriteLine(e);
                Environment.Exit(0);
            }

            var registrationToken = new RegistrationToken(255, config.TumblerEcdsaKeyAddress, config.Ipv4Address, config.Ipv6Address, config.OnionAddress, config.Port);
            byte[] msgBytes = registrationToken.GetRegistrationTokenBytes(config.TumblerRsaKeyFile, privateKeyEcdsa);

            // Create the registration transaction using the bytes generated above
            Transaction rawTx = CreateBreezeRegistrationTx(network, msgBytes, config.TxOutputValueSetting);

            TransactionUtils txUtils = new TransactionUtils();
			RegistrationStore regStore = new RegistrationStore(regStorePath);

            try {
                // Replace fundrawtransaction with C# implementation. The legacy wallet
                // software does not support the RPC call.     
                Transaction fundedTx = txUtils.FundRawTx(stratisRpc, rawTx, config.TxFeeValueSetting, BitcoinAddress.Create(config.TumblerEcdsaKeyAddress));
                RPCResponse signedTx = stratisRpc.SendCommand("signrawtransaction", fundedTx.ToHex());
                Transaction txToSend = new Transaction(((JObject)signedTx.Result)["hex"].Value<string>());

                RegistrationRecord regRecord = new RegistrationRecord(DateTime.Now,
                                                                      Guid.NewGuid(),
                                                                      txToSend.GetHash().ToString(),
                                                                      txToSend.ToHex(),
                                                                      registrationToken);

                regStore.Add(regRecord);

                stratisRpc.SendRawTransaction(txToSend);

                return txToSend;
            }
            catch (Exception e)
            {
                Console.WriteLine("ERROR: Unable to broadcast registration transaction");
                Console.WriteLine(e);
            }

            return null;
        }

        public Transaction CreateBreezeRegistrationTx(Network network, byte[] data, Money outputValue)
        {
            // Funding of the transaction is handled by the 'fundrawtransaction' RPC
            // call or its equivalent reimplementation.

            // Only construct the transaction outputs; the change address is handled
            // automatically by the funding logic

            // You need to control *where* the change address output appears inside the
            // transaction to prevent decoding errors with the addresses. Note that if
            // the fundrawtransaction RPC call is used there is an option that can be
            // passed to specify the position of the change output (it is randomly
            // positioned otherwise)

            Transaction sendTx = new Transaction();

            // Recognisable string used to tag the transaction within the blockchain
            byte[] bytes = Encoding.UTF8.GetBytes("BREEZE_REGISTRATION_MARKER");
            sendTx.Outputs.Add(new TxOut()
            {
                Value = outputValue,
                ScriptPubKey = TxNullDataTemplate.Instance.GenerateScriptPubKey(bytes)
            });

            // Add each data-encoding PubKey as a TxOut
            foreach (PubKey pubKey in BlockChainDataConversions.BytesToPubKeys(data))
            {
                TxOut destTxOut = new TxOut()
                {
                    Value = outputValue,
                    ScriptPubKey = pubKey.ScriptPubKey
                };

                sendTx.Outputs.Add(destTxOut);
            }

            if (sendTx.Outputs.Count == 0)
                throw new Exception("ERROR: No outputs in registration transaction, cannot proceed");

            return sendTx;
        }
    }
}
