using System;
using System.IO;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Breeze.BreezeD
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

			// TODO: It is messy having both a BreezeD logger and an NTumbleBit logger
			var logger = serviceProvider.GetService<ILoggerFactory>()
				.CreateLogger<Program>();

			logger.LogInformation("{Time} Reading Breeze server configuration", DateTime.Now);

			// Check OS-specific default config path for the config file. Create default file if it does not exist
			var configDir = BreezeConfiguration.GetDefaultDataDir("BreezeD");
			var configPath = Path.Combine(configDir, "breeze.conf");

			logger.LogInformation("{Time} Configuration file path {Path}", DateTime.Now, configPath);

			var config = new BreezeConfiguration(configPath);

			var dbPath = Path.Combine(configDir, "db");

			logger.LogInformation("{Time} Database path {Path}", DateTime.Now, dbPath);

			var db = new DBUtils(dbPath);

			logger.LogInformation("{Time} Checking node registration on the blockchain", DateTime.Now);

			var registration = new BreezeRegistration();

			if (!registration.CheckBreezeRegistration(config, db)) {
				logger.LogInformation("{Time} Creating or updating node registration", DateTime.Now);
				var regTx = registration.PerformBreezeRegistration(config, db);
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

            db.UpdateOrInsert<string>("TumblerStartupLog", DateTime.Now.ToString("yyyyMMddHHmmss"), "Tumbler starting", (o, n) => n);

			var tumbler = serviceProvider.GetService<ITumblerService>();
			
			tumbler.StartTumbler(config.IsTestNet);
		}
	}
}
