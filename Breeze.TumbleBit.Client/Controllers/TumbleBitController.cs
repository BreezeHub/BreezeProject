﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Breeze.TumbleBit.Client;
using Breeze.TumbleBit.Models;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Models;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Features.Wallet.Interfaces;

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
        /// Connect to a masternode running the Breeze Privacy Protocol.
        /// </summary>
        [Route("connect")]
        [HttpGet]
        public async Task<IActionResult> ConnectAsync()
        {
            // Checks the request is valid
            if (!this.ModelState.IsValid)
            {
                var errors = this.ModelState.Values.SelectMany(e => e.Errors.Select(m => m.ErrorMessage));
                return Client.ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, "Formatting error",
                    string.Join(Environment.NewLine, errors));
            }

            try
            {
                var tumblerParameters = await this.tumbleBitManager.ConnectToTumblerAsync().ConfigureAwait(false);

                if (tumblerParameters.Failure)
                    return Client.ErrorHelpers.BuildErrorResponse(HttpStatusCode.InternalServerError,
                        tumblerParameters.Message, tumblerParameters.Message);

                var periods = tumblerParameters.Value.CycleGenerator.FirstCycle.GetPeriods();
                var lengthBlocks = periods.Total.End - periods.Total.Start;
                var cycleLengthSeconds = lengthBlocks * 10 * 60;

                var parameterDictionary = new Dictionary<string, string>()
                {
                    ["tumbler"] = this.tumbleBitManager.TumblerAddress,
                    ["denomination"] = tumblerParameters.Value.Denomination.ToString(),
                    ["fee"] = tumblerParameters.Value.Fee.ToString(),
                    ["network"] = tumblerParameters.Value.Network.Name,
                    ["estimate"] = cycleLengthSeconds.ToString(),
                    ["parameters_are_standard"] = tumblerParameters.Value.IsStandard().ToString()
                };

                return this.Json(parameterDictionary);
            }
            catch (Exception e)
            {
                return Client.ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest,
                    $"An error occured connecting to the tumbler with uri {this.tumbleBitManager.TumblerAddress}.",
                    e.ToString());
            }
        }

        /// <summary>
        /// Initiate the Breeze Privacy Protocol.
        /// </summary>
        [Route("tumble")]
        [HttpPost]
        public async Task<IActionResult> TumbleAsync([FromBody] TumbleRequest request)
        {
            // Checks the request is valid
            if (!this.ModelState.IsValid)
            {
                var errors = this.ModelState.Values.SelectMany(e => e.Errors.Select(m => m.ErrorMessage));
                return Client.ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, "Formatting error",
                    string.Join(Environment.NewLine, errors));
            }

            if (this.tumbleBitManager.State == TumbleBitManager.TumbleState.Tumbling)
            {
                return Client.ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, "Already started tumbling",
                    "");
            }

            try
            {
                await this.tumbleBitManager.TumbleAsync(request.OriginWalletName, request.DestinationWalletName,
                    request.OriginWalletPassword);
                return this.Ok();
            }
            catch (Exception e)
            {
                return Client.ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest,
                    "An error occured starting tumbling session.", e.Message);
            }
        }

        /// <summary>
        /// Disconnects from the currently connected masternode and attempts to connect to a new one.
        /// </summary>
        [Route("changeserver")]
        [HttpGet]
        public async Task<IActionResult> ChangeServerAsync()
        {
            // Checks the request is valid
            if (!this.ModelState.IsValid)
            {
                var errors = this.ModelState.Values.SelectMany(e => e.Errors.Select(m => m.ErrorMessage));
                return Client.ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, "Formatting error",
                    string.Join(Environment.NewLine, errors));
            }

            try
            {
                var tumblerParameters = await this.tumbleBitManager.ChangeServerAsync().ConfigureAwait(false);

                if (tumblerParameters.Failure)
                    return Client.ErrorHelpers.BuildErrorResponse(HttpStatusCode.InternalServerError,
                        tumblerParameters.Message, tumblerParameters.Message);

                var periods = tumblerParameters.Value.CycleGenerator.FirstCycle.GetPeriods();
                var lengthBlocks = periods.Total.End - periods.Total.Start;
                var cycleLengthSeconds = lengthBlocks * 10 * 60;

                var parameterDictionary = new Dictionary<string, string>()
                {
                    ["tumbler"] = this.tumbleBitManager.TumblerAddress,
                    ["denomination"] = tumblerParameters.Value.Denomination.ToString(),
                    ["fee"] = tumblerParameters.Value.Fee.ToString(),
                    ["network"] = tumblerParameters.Value.Network.Name,
                    ["estimate"] = cycleLengthSeconds.ToString(),
                    ["parameters_are_standard"] = tumblerParameters.Value.IsStandard().ToString()
                };

                return this.Json(parameterDictionary);
            }
            catch (Exception e)
            {
                return Client.ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest,
                    $"An error occured connecting to the tumbler with uri {this.tumbleBitManager.TumblerAddress}.",
                    e.ToString());
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
                    ["destinationWallet"] = this.tumbleBitManager.tumblingState.DestinationWalletName,
                    ["registrations"] = this.tumbleBitManager.RegistrationCount().ToString(),
                    ["minRegistrations"] = "1"
                };

                return this.Json(parameterDictionary);
            }
            catch (Exception e)
            {
                return Client.ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest,
                    "An error occured during tumbling-state request.", e.ToString());
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
                return Client.ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest,
                    "An error occured during onlymonitor request.", e.ToString());
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
                return Client.ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, "Formatting error",
                    string.Join(Environment.NewLine, errors));
            }

            try
            {
                WalletBalanceModel model = new WalletBalanceModel();

                var accounts = this.walletManager.GetAccounts(request.WalletName).ToList();
                foreach (var account in accounts)
                {
                    var result = account.GetSpendableAmount();

                    model.AccountsBalances.Add(new AccountBalanceModel
                    {
                        //TODO:update this for mainnet
                        CoinType = CoinType.Testnet,
                        Name = account.Name,
                        HdPath = account.HdPath,
                        AmountConfirmed = result.ConfirmedAmount,
                        AmountUnconfirmed = result.UnConfirmedAmount,
                    });
                }

                return this.Json(model);
            }
            catch (Exception e)
            {
                return Client.ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        [Route("dummy-registration")]
        [HttpGet]
        public IActionResult DummyRegistration([FromQuery] DummyRegistrationRequest request)
        {
            this.tumbleBitManager.DummyRegistration(request.OriginWallet, request.OriginWalletPassword);
            return this.Ok();
        }

        /// <summary>
        /// Tumbling Progress expressed as json.
        /// </summary>
        [Route("progress")]
        [HttpGet]
        public async Task<IActionResult> ProgressAsync()
        {
            try
            {
                string folder;
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        "StratisNode\\bitcoin\\TumbleBit");
                else
                    folder = Path.Combine(Environment.GetEnvironmentVariable("HOME"), ".stratisnode", "bitcoin",
                        "TumbleBit");

                if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
                string filename = Path.Combine(folder, "tb_progress.json");
                if (System.IO.File.Exists(filename) == false)
                    return this.Json(string.Empty);
                else
                {
                    string progress = await System.IO.File.ReadAllTextAsync(filename).ConfigureAwait(false);
                    return this.Json(progress);
                }
            }
            catch (Exception e)
            {
                return Client.ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, $"Could not get progress.",
                    e.ToString());
            }
        }

        ///<inheritdoc/>
        [Route("last-block-mins")]
        [HttpGet]
        public IActionResult LastBlockMinsAsync()
        {
            try
            {
                var parameterDictionary = new Dictionary<string, string>()
                {
                    ["mins"] = this.tumbleBitManager.LastBlockTime.ToString()
                };
                return this.Json(parameterDictionary);
            }
            catch (Exception e)
            {
                return Client.ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest,
                    $"Could not get last block mins.", e.ToString());
            }
        }
    }
}