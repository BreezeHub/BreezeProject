using System;
using NBitcoin;
using Stratis.Bitcoin.Signals;
using Stratis.Bitcoin.Utilities;

namespace Breeze.TumbleBit.Client
{
    /// <summary>
    /// Observer that receives notifications about the arrival of new <see cref="Block"/>s.
    /// </summary>
	public class TumbleBitBlockObserver : SignalObserver<Block>
    {
        private readonly ConcurrentChain chain;
        private readonly ITumbleBitManager tumbleBitManager;

        public TumbleBitBlockObserver(ConcurrentChain chain, ITumbleBitManager registrationManager)
        {
            this.chain = chain;
            this.tumbleBitManager = registrationManager;
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

            this.tumbleBitManager.ProcessBlock(height, block);
        }
    }
}
