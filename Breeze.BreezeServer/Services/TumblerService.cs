using System;
using System.IO;
using CommandLine;

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;

using NTumbleBit;
using NTumbleBit.Logging;
using NTumbleBit.ClassicTumbler;
using NTumbleBit.ClassicTumbler.Server.CLI;
using NTumbleBit.ClassicTumbler.Server;
using NTumbleBit.Services;
using NTumbleBit.Configuration;
using NTumbleBit.ClassicTumbler.CLI;

namespace Breeze.BreezeServer
{
    public class TumblerService : ITumblerService
    {
        public void StartTumbler(bool testnet)
        {
            string[] args;
			
			if (!testnet)
				// TODO: Tumbler is locked to testnet for testing
				args = new string[] {"-testnet"};
			else
				args = new string[] {"-testnet"};

			Logs.Configure(new FuncLoggerFactory(i => new CustomerConsoleLogger(i, (a, b) => true, false)));

			using (var interactive = new Interactive())
			{
				var config = new TumblerConfiguration();
				config.LoadArgs(args);
				try
				{
					var runtime = TumblerRuntime.FromConfiguration(config, new TextWriterClientInteraction(Console.Out, Console.In));
					interactive.Runtime = new ServerInteractiveRuntime(runtime);
					StoppableWebHost host = null;
					if (!config.OnlyMonitor)
					{
						host = new StoppableWebHost(() => new WebHostBuilder()
						.UseAppConfiguration(runtime)
						.UseContentRoot(Directory.GetCurrentDirectory())
						.UseStartup<Startup>()
						.Build());
					}

					var job = new BroadcasterJob(interactive.Runtime.Services);
					job.Start();
					interactive.Services.Add(job);

					if (!config.OnlyMonitor)
					{
						host.Start();
						interactive.Services.Add(host);
					}

					interactive.StartInteractive();
				}
				catch (ConfigException ex)
				{
					if (!string.IsNullOrEmpty(ex.Message))
						Logs.Configuration.LogError(ex.Message);
				}
				catch (InterruptedConsoleException) { }
				catch (Exception exception)
				{
					Logs.Tumbler.LogError("Exception thrown while running the server");
					Logs.Tumbler.LogError(exception.ToString());
				}
			}
		}
	}
}