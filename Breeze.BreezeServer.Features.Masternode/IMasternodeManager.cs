using System;
using System.Collections.Generic;
using System.Text;

namespace Breeze.BreezeServer.Features.Masternode
{
    /// <summary>
    /// An interface representing a manager providing operations on Masternode.
    /// </summary>
    public interface IMasternodeManager
    {
        /// <summary>
        /// Initializes this watch-only wallet manager.
        /// </summary>
        void Initialize();

        void Dispose();
    }
}
