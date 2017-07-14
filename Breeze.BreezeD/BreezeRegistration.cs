using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

using Newtonsoft.Json.Linq;

using NBitcoin;
using NBitcoin.DataEncoders;
using NBitcoin.RPC;

namespace Breeze.BreezeD
{
    public class BreezeRegistration
    {
        public bool CheckBreezeRegistration(BreezeConfiguration config, DBUtils db)
        {
			var network = Network.StratisMain;
			if (config.IsTestNet)
			{
                // TODO: Change to StratisTest when it is added to NStratis
				network = Network.TestNet;
			}

            // In order to determine if the registration sequence has been performed
            // before, and to see if a previous performance is still valid, interrogate
            // the database to see if any transactions have been recorded.

            var transactions = db.GetDictionary<string, string>("RegistrationTransactions");

            // If no transactions exist, the registration definitely needs to be done
            if (transactions == null || transactions.Count == 0) { return false; }

            string highestKey = null;
            foreach (var txn in transactions)
            {
                // Find most recent transaction. Assume that the rowKeys are ordered
                // lexicographically.
                if (highestKey == null)
                {
                    highestKey = txn.Key;
                }

                if (String.Compare(txn.Key, highestKey) == 1)
                    highestKey = txn.Key;
            }

            var mostRecentTxn = new Transaction(transactions[highestKey]);

            // Decode transaction and check if the decoded bitstream matches the
            // current configuration

            // TODO: Check if transaction is actually confirmed on the blockchain?
			var registrationToken = new RegistrationToken();
            registrationToken.ParseTransaction(mostRecentTxn, network);

            if (!config.Ipv4Address.Equals(registrationToken.Ipv4Addr))
                return false;

            if (!config.Ipv6Address.Equals(registrationToken.Ipv6Addr))
                return false;

            if (config.OnionAddress != registrationToken.OnionAddress)
                return false;

            if (config.Port != registrationToken.Port)
                return false;

            return true;
        }

        public Transaction PerformBreezeRegistration(BreezeConfiguration config, DBUtils db)
        {
			var network = Network.StratisMain;
			if (config.IsTestNet)
			{
                // TODO: Change to StratisTest when support is added to NStratis
				network = Network.TestNet;
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

            // Retrieve tumbler's parameters so that the registration details can be constructed
            //var tumblerApi = new TumblerApiAccess(config.TumblerApiBaseUrl);
            //string json = tumblerApi.GetParameters().Result;
            //var tumblerParameters = JsonConvert.DeserializeObject<TumblerParameters>(json);
            var registrationToken = new RegistrationToken(255, config.Ipv4Address, config.Ipv6Address, config.OnionAddress, config.Port, config.TumblerRsaKeyPath);
            var msgBytes = registrationToken.GetRegistrationTokenBytes(privateKeyEcdsa);

            // Create the registration transaction using the bytes generated above
            var rawTx = CreateBreezeRegistrationTx(network, msgBytes, config.TxOutputValueSetting);

            var txUtils = new TransactionUtils();

            try {
                // Replace fundrawtransaction with C# implementation. The legacy wallet
                // software does not support the RPC call.     
                var fundedTx = txUtils.FundRawTx(stratisRpc, rawTx, config.TxFeeValueSetting, BitcoinAddress.Create(config.TumblerEcdsaKeyAddress));
                var signedTx = stratisRpc.SendCommand("signrawtransaction", fundedTx.ToHex());
                var txToSend = new Transaction(((JObject)signedTx.Result)["hex"].Value<string>());

                db.UpdateOrInsert<string>("RegistrationTransactions", DateTime.Now.ToString("yyyyMMddHHmmss"), txToSend.ToHex(), (o, n) => n);
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

            var sendTx = new Transaction();

            // Recognisable string used to tag the transaction within the blockchain
            byte[] bytes = Encoding.UTF8.GetBytes("BREEZE_REGISTRATION_MARKER");
            sendTx.Outputs.Add(new TxOut()
            {
                Value = outputValue,
                ScriptPubKey = TxNullDataTemplate.Instance.GenerateScriptPubKey(bytes)
            });

            // Add each data-encoding address as a TxOut
            foreach (BitcoinAddress address in BytesToAddresses(network, data))
            {
                TxOut destTxOut = new TxOut()
                {
                    Value = outputValue,
                    ScriptPubKey = address.ScriptPubKey
                };

                sendTx.Outputs.Add(destTxOut);
            }

            return sendTx;
        }

        public List<BitcoinAddress> BytesToAddresses(Network network, byte[] dataToEncode)
        {
            /* Converts a byte array into a list of Stratis or Bitcoin addresses.
            Each address can store 20 bytes of arbitrary data. The first byte of
            a (typically) 25 byte address is the network byte and is therefore
            unusable. The last 4 bytes are the checksum and are similarly
            unusable. */

            List<BitcoinAddress> addressList = new List<BitcoinAddress>();

            if (dataToEncode.Length == 0)
            {
                return addressList;
            }

            int startPos = 0;
            string base58address;

            while (true)
            {
                IEnumerable<byte> subSet = dataToEncode.Skip(startPos).Take(20);

                if (subSet.Count() == 20)
                {
                    IEnumerable<byte> augmentedSubSet = subSet;

                    KeyId key = new KeyId(augmentedSubSet.ToArray());
                    BitcoinPubKeyAddress address = new BitcoinPubKeyAddress(key, network);
                    base58address = address.ToString();
                    addressList.Add(BitcoinAddress.Create(base58address));
                }
                else
                {
                    // Fill up remainder of 20 bytes with zeroes
                    var arr = new byte[20 - subSet.Count()];
                    for (int i = 0; i < arr.Length; i++)
                    {
                        arr[i] = 0x00;
                    }

                    IEnumerable<byte> tempSuffix = arr;
                    IEnumerable<byte> augmentedSubSet = subSet.Concat(tempSuffix);

                    KeyId key = new KeyId(augmentedSubSet.ToArray());
                    BitcoinPubKeyAddress address = new BitcoinPubKeyAddress(key, network);
                    base58address = address.ToString();
                    addressList.Add(BitcoinAddress.Create(base58address));

                    break;
                }

                startPos += 20;
            }

            return addressList;
        }

        public byte[] AddressesToBytes(List<BitcoinAddress> addresses)
        {
            /* Convert a list of Bitcoin or Stratis addresses back into
             a byte array. Note that no assumptions are made about
             padding schemes, and the data storage protocol therefore
             needs to be robust enough to handle trailing empty or
             garbage bytes.
             */
            
            var decoded = new MemoryStream();
            byte[] temp;

            foreach (var addr in addresses)
            {
                temp = Encoders.Base58.DecodeData(addr.ToString());
                
                // Use an offset to trim off the 1 network byte and 4 checksum bytes
                if (temp.Length < 21)
                {
                    throw new Exception("Decoded address too short, data corrupted?");
                }
                decoded.Write(temp, 1, 20);
            }

            return decoded.ToArray();
        }

        public byte[] AddressToBytes(BitcoinAddress address)
        {
            /* Convert a Bitcoin or Stratis address back into
             a byte array. Note that no assumptions are made about
             padding schemes, and the data storage protocol therefore
             needs to be robust enough to handle trailing empty or
             garbage bytes.
             */
            
            var decoded = new MemoryStream();
            byte[] temp;

            temp = Encoders.Base58.DecodeData(address.ToString());
            
            // Use an offset to trim off the 1 network byte and 4 checksum bytes
            if (temp.Length < 21)
            {
                throw new Exception("Decoded address too short, data corrupted?");
            }
            decoded.Write(temp, 1, 20);

            return decoded.ToArray();
        }
    }
}