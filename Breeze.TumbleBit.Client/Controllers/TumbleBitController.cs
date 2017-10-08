using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Breeze.TumbleBit.Client;
using Breeze.TumbleBit.Models;
using NBitcoin;
using NTumbleBit.ClassicTumbler;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Models;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Utilities.JsonErrors;

namespace Breeze.TumbleBit.Controllers
{
    /// <summary>
    /// Controller providing TumbleBit operations.
    /// </summary>
    [Route("api/[controller]")]
    public class TumbleBitController : Controller
    {
        private readonly IWalletManager walletManager;
        private readonly ITumbleBitManager tumbleBitManager;

        public TumbleBitController(ITumbleBitManager tumbleBitManager, IWalletManager walletManager)
        {
            this.tumbleBitManager = tumbleBitManager;
            this.walletManager = walletManager;
        }

        /// <summary>
        /// Connect to a tumbler.
        /// </summary>
        [Route("connect")]
        [HttpGet]
        public async Task<IActionResult> ConnectAsync()
        {
            // checks the request is valid
            if (!this.ModelState.IsValid)
            {
                var errors = this.ModelState.Values.SelectMany(e => e.Errors.Select(m => m.ErrorMessage));
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, "Formatting error", string.Join(Environment.NewLine, errors));
            }

            try
            {
                var tumblerParameters = await this.tumbleBitManager.ConnectToTumblerAsync().ConfigureAwait(false);

                if (tumblerParameters == null)
                    return ErrorHelpers.BuildErrorResponse(HttpStatusCode.InternalServerError, "Unable to connect", "Unable to connect");

                var periods = tumblerParameters.CycleGenerator.FirstCycle.GetPeriods();
                var lengthBlocks = periods.Total.End - periods.Total.Start;
                var cycleLengthSeconds = lengthBlocks * 10 * 60;

                var parameterDictionary = new Dictionary<string, string>()
                {
                    ["tumbler"] = this.tumbleBitManager.TumblerAddress,
                    ["denomination"] = tumblerParameters.Denomination.ToString(),
                    ["fee"] = tumblerParameters.Fee.ToString(),
                    ["network"] = tumblerParameters.Network.Name,
                    ["estimate"] = cycleLengthSeconds.ToString()
                };

                return this.Json(parameterDictionary);
            }
            catch (Exception e)
            {
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, $"An error occured connecting to the tumbler with uri {this.tumbleBitManager.TumblerAddress}.", e.ToString());
            }
        }

        /// <summary>
        /// Connect to a tumbler.
        /// </summary>
        [Route("tumble")]
        [HttpPost]
        public async Task<IActionResult> TumbleAsync([FromBody] TumbleRequest request)
        {
            // checks the request is valid
            if (!this.ModelState.IsValid)
            {
                var errors = this.ModelState.Values.SelectMany(e => e.Errors.Select(m => m.ErrorMessage));
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, "Formatting error", string.Join(Environment.NewLine, errors));
            }

            if (this.tumbleBitManager.State == TumbleBitManager.TumbleState.Tumbling)
            {
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, "Already started tumbling", "");
            }

            try
            {
                await this.tumbleBitManager.TumbleAsync(request.OriginWalletName, request.DestinationWalletName, request.OriginWalletPassword);
                return this.Ok();
            }
            catch (Exception e)
            {
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, "An error occured starting tumbling session.", e.ToString());
            }
        }

        /// <summary>
        /// Is tumbler tumbling.
        /// </summary>
        [Route("tumbling-state")]
        [HttpGet]
        public async Task<IActionResult> GetTumblingStateAsync()
        {
            try
            {
                var parameterDictionary = new Dictionary<string, string>()
                {
                    ["tumbler"] = this.tumbleBitManager.TumblerAddress,
                    ["state"] = this.tumbleBitManager.State.ToString(),
                    ["originWallet"] = this.tumbleBitManager.tumblingState.OriginWalletName,
                    ["destinationWallet"] = this.tumbleBitManager.tumblingState.DestinationWalletName
                };

                return this.Json(parameterDictionary);
            }
            catch (Exception e)
            {
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, "An error occured during tumbling-state request.", e.ToString());
            }
        }

        /// <summary>
        /// Flip the tumbler to onlymonitor mode.
        /// </summary>
        [Route("onlymonitor")]
        [HttpGet]
        public async Task<IActionResult> OnlyMonitorAsync()
        {
            try
            {
                await this.tumbleBitManager.OnlyMonitorAsync().ConfigureAwait(false);
                return this.Ok();
            }
            catch (Exception e)
            {
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, "An error occured during onlymonitor request.", e.ToString());
            }
        }

        /// <summary>
        /// Gets the balance of the destination wallet.
        /// </summary>
        /// <param name="request">The request parameters.</param>        
        /// <returns></returns>
        [Route("destination-balance")]
        [HttpGet]
        public IActionResult GetDestinationBalance([FromQuery] WalletBalanceRequest request)
        {
            Guard.NotNull(request, nameof(request));

            // checks the request is valid
            if (!this.ModelState.IsValid)
            {
                var errors = this.ModelState.Values.SelectMany(e => e.Errors.Select(m => m.ErrorMessage));
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, "Formatting error", string.Join(Environment.NewLine, errors));
            }

            try
            {
                WalletBalanceModel model = new WalletBalanceModel();

                var accounts = this.walletManager.GetAccounts(request.WalletName).ToList();
                foreach (var account in accounts)
                {
                    var result = account.GetSpendableAmount();

                    AccountBalance balance = new AccountBalance
                    {
                        //TODO:update this for mainnet
                        CoinType = CoinType.Testnet,
                        Name = account.Name,
                        HdPath = account.HdPath,
                        AmountConfirmed = result.ConfirmedAmount,
                        AmountUnconfirmed = result.UnConfirmedAmount,
                    };

                    model.AccountsBalances.Add(balance);
                }

                return this.Json(model);
            }
            catch (Exception e)
            {
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        [Route("dummy-registration")]
        [HttpGet]
        public IActionResult DummyRegistration([FromQuery] DummyRegistrationRequest request)
        {
            this.tumbleBitManager.DummyRegistration(request.OriginWallet, request.OriginWalletPassword);
            return this.Ok();
        }

        [Route("block-generate")]
        [HttpPost]
        public IActionResult BlockGenerate([FromQuery] BlockGenerateRequest request)
        {
            this.tumbleBitManager.BlockGenerate(request.NumberOfBlocks);
            return this.Ok();
        }
    }
}
