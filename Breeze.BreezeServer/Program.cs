using System;
using System.IO;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using BreezeCommon;
using NBitcoin;
using NTumbleBit.ClassicTumbler.Server;
using Breeze.BreezeServer.Services;

namespace Breeze.BreezeServer
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var comparer = new CommandlineArgumentComparer();
            var isRegTest = args.Contains("regtest", comparer);
            var isTestNet = args.Contains("testnet", comparer);
            var forceRegistration = args.Contains("forceRegistration", comparer);

            var useTor = !args.Contains("noTor", comparer);

            TumblerProtocolType? tumblerProtocol = null;
            try
            {
                string tumblerProtocolString = args.Where(a => a.StartsWith("-tumblerProtocol=")).Select(a => a.Substring("-tumblerProtocol=".Length).Replace("\"", "")).FirstOrDefault();
                if (!isRegTest && (tumblerProtocolString != null || !useTor))
                {
                    Console.WriteLine("Options -TumblerProtocol and -NoTor can only be used in combination with -RegTest switch.");
                    return;
                }

                if (tumblerProtocolString != null)
                    tumblerProtocol = Enum.Parse<TumblerProtocolType>(tumblerProtocolString, true);

                if (useTor && tumblerProtocol.HasValue && tumblerProtocol.Value == TumblerProtocolType.Http)
                {
                    Console.WriteLine("TumblerProtocol can only be changed to Http when Tor is disabled. Please use -NoTor switch to disable Tor.");
                    return;
                }
            }
            catch
            {
                Console.WriteLine($"Incorrect tumbling prococol specified; the valid values are {TumblerProtocolType.Tcp} and {TumblerProtocolType.Http}");
                return;
            }

            var serviceProvider = new ServiceCollection()
                .AddLogging()
                .AddSingleton<ITumblerService, TumblerService>()
                .BuildServiceProvider();

            serviceProvider
                .GetService<ILoggerFactory>()
                .AddConsole(LogLevel.Debug);

            // TODO: It is messy having both a BreezeServer logger and an NTumbleBit logger
            var logger = serviceProvider.GetService<ILoggerFactory>()
                .CreateLogger<Program>();
            
            logger.LogInformation("{Time} Reading Breeze server configuration", DateTime.Now);

            // Check OS-specific default config path for the config file. Create default file if it does not exist
            string configDir = BreezeConfiguration.GetDefaultDataDir("BreezeServer");
            if (isRegTest)
                configDir = Path.Combine(configDir, "StratisRegTest");
            else if (isTestNet)
                configDir = Path.Combine(configDir, "StratisTest");
            else
                configDir = Path.Combine(configDir, "StratisMain");

            string configPath = Path.Combine(configDir, "breeze.conf");

            logger.LogInformation("{Time} Configuration file path {Path}", DateTime.Now, configPath);

            BreezeConfiguration config = new BreezeConfiguration(configPath);
            if (!useTor)
                config.UseTor = false;

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

            if (forceRegistration || !registration.CheckBreezeRegistration(config, regStorePath, configurationHash, onionAddress, tumblerKey)) {
                logger.LogInformation("{Time} Creating or updating node registration", DateTime.Now);
                var regTx = registration.PerformBreezeRegistration(config, regStorePath, configurationHash, onionAddress, tumblerKey);
                if (regTx != null) {
                    logger.LogInformation("{Time} Submitted transaction {TxId} via RPC for broadcast", DateTime.Now, regTx.GetHash().ToString());
                }
                else {
                    logger.LogInformation("{Time} Unable to broadcast transaction via RPC", DateTime.Now);
                    Environment.Exit(0);
                }
            }
            else {
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
    }
}
