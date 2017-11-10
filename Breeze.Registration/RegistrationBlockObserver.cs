using System;
using NBitcoin;
using Stratis.Bitcoin.Signals;
using Stratis.Bitcoin.Utilities;

namespace Breeze.Registration
{
    /// <summary>
    /// Observer that receives notifications about the arrival of new <see cref="Block"/>s.
    /// </summary>
	public class RegistrationBlockObserver : SignalObserver<Block>
    {
        private readonly ConcurrentChain chain;
        private readonly IRegistrationManager registrationManager;

        public RegistrationBlockObserver(ConcurrentChain chain, IRegistrationManager registrationManager)
        {
            this.chain = chain;
            this.registrationManager = registrationManager;
        }

        protected override void OnErrorCore(Exception error)
        {
            Guard.NotNull(error, nameof(error));
            // Nothing to do.
        }

        /// <summary>
        /// Manages what happens when a new block is received.
        /// </summary>
        /// <param name="block">The new block</param>
        protected override void OnNextCore(Block block)
        {
            var hash = block.Header.GetHash();
            var height = this.chain.GetBlock(hash).Height;

            this.registrationManager.ProcessBlock(height, block);
        }
    }
}
