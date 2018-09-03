using System;
using System.Collections.Generic;
using System.Net;
using Microsoft.AspNetCore.Mvc;
using Stratis.Bitcoin.Utilities.JsonErrors;

namespace Breeze.BreezeServer.Features.Masternode.Controllers
{
    /// <summary>
    /// Controller providing operations on a watch-only wallet.
    /// </summary>
    [Route("api/[controller]")]
    public class MasternodeController : Controller
    {
        /// <summary> The masternode manager. </summary>
        private readonly IMasternodeManager masternodeManager;

        /// <summary>
        /// Initializes a new instance of the <see cref="MasternodeController"/> class.
        /// </summary>
        /// <param name="watchOnlyWalletManager">The masternode manager.</param>
        public MasternodeController(IMasternodeManager masternodeManager)
        {
            this.masternodeManager = masternodeManager;
        }

    }
}
