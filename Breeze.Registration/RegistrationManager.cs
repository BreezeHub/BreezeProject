using System;
using System.Threading.Tasks;
using NBitcoin;
using BreezeCommon;

namespace Breeze.Registration
{
    public class RegistrationManager : IRegistrationManager
    {
        private RegistrationStore registrationStore;
        private Network network;

        public RegistrationManager()
        {

        }

        public void Initialize(RegistrationStore registrationStore, bool isBitcoin, Network network)
        {
            this.registrationStore = registrationStore;
            this.network = network;
            Console.WriteLine("Initialized RegistrationFeature");
            foreach (var item in this.registrationStore.GetAll())
            {
                Console.WriteLine(item.RecordTxHex);
            }
        }

        /// <inheritdoc />
        public void ProcessBlock(int height, Block block)
        {
            // Check for any server registration transactions
            if (block.Transactions != null)
            {
                foreach (Transaction tx in block.Transactions)
                {
                    // Check if the transaction has the Breeze registration marker output
                    if (tx.Outputs[0].ScriptPubKey.ToHex().ToLower() == "6a1a425245455a455f524547495354524154494f4e5f4d41524b4552")
                    {
                        try
                        {
                            RegistrationToken registrationToken = new RegistrationToken();
                            registrationToken.ParseTransaction(tx, this.network);
                            MerkleBlock merkleBlock = new MerkleBlock(block, new uint256[] { tx.GetHash() });
                            RegistrationRecord registrationRecord = new RegistrationRecord(DateTime.Now, Guid.NewGuid(), tx.GetHash().ToString(), tx.ToHex(), registrationToken, merkleBlock.PartialMerkleTree);
                            this.registrationStore.Add(registrationRecord);
                        }
                        catch (Exception e)
                        {
                            //this.logger.LogDebug("Failed to parse registration transaction: " + tx.GetHash());
                        }
                    }
                }
            }
        }

        public void Dispose()
        {
        }
    }
}
