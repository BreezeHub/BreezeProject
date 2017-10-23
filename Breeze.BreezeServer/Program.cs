using System;
using System.IO;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using BreezeCommon;
using NBitcoin;
using NTumbleBit.ClassicTumbler.Server;

namespace Breeze.BreezeServer
{
	public class Program
	{
        public static void Main(string[] args)
		{
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
			string configPath = Path.Combine(configDir, "breeze.conf");

			logger.LogInformation("{Time} Configuration file path {Path}", DateTime.Now, configPath);

            BreezeConfiguration config = new BreezeConfiguration(configPath);

			logger.LogInformation("{Time} Pre-initialising server to obtain parameters for configuration", DateTime.Now);
			
			var preTumblerConfig = serviceProvider.GetService<ITumblerService>();
			preTumblerConfig.StartTumbler(config.IsTestNet, true);

			string configurationHash = preTumblerConfig.config.ClassicTumblerParameters.GetHash().ToString();
			string onionAddress = preTumblerConfig.runtime.TorUri.Host.Substring(0, 16);
			NTumbleBit.RsaKey tumblerKey = preTumblerConfig.runtime.TumblerKey;
			
			// Mustn't be occupying hidden service URL when the TumblerService is reinitialised
			preTumblerConfig.runtime.TorConnection.Dispose();
			
			// No longer need this instance of the class
			preTumblerConfig = null;
			
			string regStorePath = Path.Combine(configDir, "registrationHistory.json");

            logger.LogInformation("{Time} Registration history path {Path}", DateTime.Now, regStorePath);
			logger.LogInformation("{Time} Checking node registration", DateTime.Now);

            BreezeRegistration registration = new BreezeRegistration();

            if (!registration.CheckBreezeRegistration(config, regStorePath, configurationHash, onionAddress, tumblerKey)) {
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

			logger.LogInformation("{Time} Starting Tumblebit server", DateTime.Now);

			var tumbler = serviceProvider.GetService<ITumblerService>();
			
			tumbler.StartTumbler(config.IsTestNet, false);
		}
	}
}
