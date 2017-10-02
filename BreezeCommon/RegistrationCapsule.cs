using System;
using NBitcoin;
using NBitcoin.Protocol;

namespace BreezeCommon
{
    public class RegistrationCapsule : DiscoveryCapsule, IComparable
    {
        Transaction registrationTransaction;
        MerkleBlock registrationTransactionProof;

        public RegistrationCapsule(Block block, Transaction tx)
        {
            this.RegistrationTransaction = tx;
            this.RegistrationTransactionProof = new MerkleBlock(block, new uint256[] { tx.GetHash() });
        }

        public RegistrationCapsule(MerkleBlock merkleBlock, Transaction tx)
        {
            this.RegistrationTransaction = tx;
            this.RegistrationTransactionProof = merkleBlock;
        }

        public Transaction RegistrationTransaction
        {
            get { return registrationTransaction; }
            set { registrationTransaction = value; }
        }

        public MerkleBlock RegistrationTransactionProof
        {
            get { return registrationTransactionProof; }
            set { registrationTransactionProof = value; }
        }

        public override void ReadWrite(BitcoinStream stream)
        {
            stream.ReadWrite(ref registrationTransaction);
            stream.ReadWrite(ref registrationTransactionProof);
        }

        int IComparable.CompareTo(object obj)
        {
            RegistrationCapsule c = (RegistrationCapsule)obj;
            return String.Compare(this.registrationTransaction.GetHash().ToString(),
                c.registrationTransaction.GetHash().ToString());
        }
    }
}