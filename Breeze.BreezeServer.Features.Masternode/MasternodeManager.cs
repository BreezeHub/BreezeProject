using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Breeze.BreezeServer.Features.Masternode.Services;
using BreezeCommon;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Configuration;

namespace Breeze.BreezeServer.Features.Masternode
{
    public class MasternodeManager : IMasternodeManager
    {
        /// <summary>Settings relevant to node.</summary>
        private readonly NodeSettings nodeSettings;
        
        /// <summary>Settings relevant to masternode.</summary>
        private readonly MasternodeSettings masternodeSettings;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        private TumblerService tumblerService;

        public MasternodeManager(NodeSettings nodeSettings, MasternodeSettings masternodeSettings, ILoggerFactory loggerFactory, ITumblerService tumblerService)
        {
            this.nodeSettings = nodeSettings;
            this.masternodeSettings = masternodeSettings;
            this.tumblerService = tumblerService as TumblerService;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        public void Initialize()
        {
            logger.LogInformation("{Time} Pre-initialising server to obtain parameters for configuration", DateTime.Now);

            tumblerService.StartTumbler(true);

            string configurationHash = tumblerService.runtime.ClassicTumblerParameters.GetHash().ToString();
            string onionAddress = tumblerService.runtime.TorUri.Host.Substring(0, 16);
            NTumbleBit.RsaKey tumblerKey = tumblerService.runtime.TumblerKey;

            // Close Tor connection if it was opened
            if (masternodeSettings.TorEnabled)
                tumblerService.runtime.TorConnection.Dispose();

            string regStorePath = Path.Combine(nodeSettings.DataDir, "registrationHistory.json");

            logger.LogInformation("{Time} Registration history path {Path}", DateTime.Now, regStorePath);
            logger.LogInformation("{Time} Checking node registration", DateTime.Now);

            BreezeRegistration registration = new BreezeRegistration();

            if (masternodeSettings.ForceRegistration || !registration.CheckBreezeRegistration(nodeSettings, masternodeSettings, regStorePath, configurationHash, onionAddress, tumblerKey))
            {
                logger.LogInformation("{Time} Creating or updating node registration", DateTime.Now);
                var regTx = registration.PerformBreezeRegistration(nodeSettings, masternodeSettings, regStorePath, configurationHash, onionAddress, tumblerKey);
                if (regTx != null)
                {
                    logger.LogInformation("{Time} Submitted transaction {TxId} via RPC for broadcast", DateTime.Now, regTx.GetHash().ToString());
                }
                else
                {
                    logger.LogInformation("{Time} Unable to broadcast transaction via RPC", DateTime.Now);
                    Environment.Exit(0);
                }
            }
            else
            {
                logger.LogInformation("{Time} Node registration has already been performed", DateTime.Now);
            }

            // Perform collateral balance check and report the result
            Money collateralShortfall;
            if (registration.VerifyCollateral(nodeSettings, masternodeSettings, out collateralShortfall))
            {
                logger.LogInformation($"{{Time}} The collateral address {masternodeSettings.TumblerEcdsaKeyAddress} has sufficient funds.", DateTime.Now);
            }
            else
            {
                logger.LogWarning($"{{Time}} The collateral address {masternodeSettings.TumblerEcdsaKeyAddress} doesn't have enough funds. Collateral requirement is {RegistrationParameters.MASTERNODE_COLLATERAL_THRESHOLD} but only {collateralShortfall} is available at the collateral address. This is expected if you have only just run the masternode for the first time. Please send funds to the collateral address no later than {RegistrationParameters.WINDOW_PERIOD_BLOCK_COUNT} blocks after the registration transaction.", DateTime.Now);
            }

            logger.LogInformation("{Time} Starting Tumblebit server", DateTime.Now);

            // The TimeStamp and BlockSignature flags could be set to true when the Stratis network is instantiated.
            // We need to set it to false here to ensure compatibility with the Bitcoin protocol.
            Transaction.TimeStamp = false;
            Block.BlockSignature = false;

            tumblerService.StartTumbler(false);
        }

        public void Dispose()
        {
            
        }
    }
}
