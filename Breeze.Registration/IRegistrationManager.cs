using System;
using System.Threading.Tasks;
using NBitcoin;
using BreezeCommon;

namespace Breeze.Registration
{
    public interface IRegistrationManager : IDisposable
    {
        void Initialize(RegistrationStore registrationStore, bool isBitcoin, Network network);

        /// <summary>
        /// Processes a block received from the network.
        /// </summary>
        /// <param name="height">The height of the block in the blockchain.</param>
        /// <param name="block">The block.</param>
        void ProcessBlock(int height, Block block);
    }
}
