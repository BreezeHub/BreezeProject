using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BreezeCommon;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NTumbleBit;
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

        public MasternodeManager(NodeSettings nodeSettings, MasternodeSettings masternodeSettings, ILoggerFactory loggerFactory)
        {
            this.nodeSettings = nodeSettings;
            this.masternodeSettings = masternodeSettings;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        public void Initialize()
        {
            logger.LogInformation("{Time} Pre-initialising server to obtain parameters for configuration", DateTime.Now);

            var preTumblerConfig = serviceProvider.GetService<ITumblerService>();
            preTumblerConfig.StartTumbler(config, true, torMandatory: !isRegTest, tumblerProtocol: tumblerProtocol);

            string configurationHash = preTumblerConfig.runtime.ClassicTumblerParameters.GetHash().ToString();
            string onionAddress = preTumblerConfig.runtime.TorUri.Host.Substring(0, 16);
            NTumbleBit.RsaKey tumblerKey = preTumblerConfig.runtime.TumblerKey;

            // No longer need this instance of the class
            if (config.UseTor)
                preTumblerConfig.runtime.TorConnection.Dispose();
            preTumblerConfig = null;

            string regStorePath = Path.Combine(configDir, "registrationHistory.json");

            logger.LogInformation("{Time} Registration history path {Path}", DateTime.Now, regStorePath);
            logger.LogInformation("{Time} Checking node registration", DateTime.Now);

            BreezeRegistration registration = new BreezeRegistration();

            if (forceRegistration || !registration.CheckBreezeRegistration(config, regStorePath, configurationHash, onionAddress, tumblerKey))
            {
                logger.LogInformation("{Time} Creating or updating node registration", DateTime.Now);
                var regTx = registration.PerformBreezeRegistration(config, regStorePath, configurationHash, onionAddress, tumblerKey);
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
            if (registration.VerifyCollateral(config, out collateralShortfall))
            {
                logger.LogInformation($"{{Time}} The collateral address {config.TumblerEcdsaKeyAddress} has sufficient funds.", DateTime.Now);
            }
            else
            {
                logger.LogWarning($"{{Time}} The collateral address {config.TumblerEcdsaKeyAddress} doesn't have enough funds. Collateral requirement is {RegistrationParameters.MASTERNODE_COLLATERAL_THRESHOLD} but only {collateralShortfall} is available at the collateral address. This is expected if you have only just run the masternode for the first time. Please send funds to the collateral address no later than {RegistrationParameters.WINDOW_PERIOD_BLOCK_COUNT} blocks after the registration transaction.", DateTime.Now);
            }

            logger.LogInformation("{Time} Starting Tumblebit server", DateTime.Now);

            // The TimeStamp and BlockSignature flags could be set to true when the Stratis network is instantiated.
            // We need to set it to false here to ensure compatibility with the Bitcoin protocol.
            Transaction.TimeStamp = false;
            Block.BlockSignature = false;

            var tumbler = serviceProvider.GetService<ITumblerService>();

            tumbler.StartTumbler(config, false, torMandatory: !isRegTest, tumblerProtocol: tumblerProtocol);
        }

        public void Dispose()
        {
            
        }
    }
}
