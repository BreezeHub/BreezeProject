using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CommandLine;

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;

using NTumbleBit;
using NTumbleBit.Logging;
using NTumbleBit.ClassicTumbler.Server;
using NTumbleBit.Services;
using NTumbleBit.Configuration;
using NTumbleBit.ClassicTumbler.CLI;
using NBitcoin;
using Breeze.BreezeServer;

namespace Breeze.BreezeServer.Services
{
    public class TumblerService : ITumblerService
    {
        public TumblerConfiguration config { get; set; }
        public TumblerRuntime runtime { get; set; }
        
        public void StartTumbler(BreezeConfiguration breezeConfig, bool getConfigOnly, string ntumblebitServerConf = null, string datadir = null)
        {
            var argsTemp = new List<string>();
            argsTemp.Add("-debug");
			
			if (breezeConfig.TumblerNetwork == Network.TestNet)
				argsTemp.Add("-testnet");
			else if (breezeConfig.TumblerNetwork == Network.RegTest)
			    argsTemp.Add("-regtest");
            // No else needed, mainnet is defaulted
            
            if (ntumblebitServerConf != null)
                argsTemp.Add("-conf=" + ntumblebitServerConf);

            if (datadir != null)
                argsTemp.Add("-datadir=" + datadir);

            string[] args = argsTemp.ToArray();
            var argsConf = new TextFileConfiguration(args);
            var debug = argsConf.GetOrDefault<bool>("debug", false);

            ConsoleLoggerProcessor loggerProcessor = new ConsoleLoggerProcessor();
            Logs.Configure(new FuncLoggerFactory(i => new CustomerConsoleLogger(i, Logs.SupportDebug(debug), false, loggerProcessor)));

            if (getConfigOnly)
            {
                config = new TumblerConfiguration();
                config.LoadArgs(args);                
                runtime = TumblerRuntime.FromConfiguration(config, new AcceptAllClientInteraction());
                return;
            }

            using (var interactive = new Interactive())
            {
                config = new TumblerConfiguration();
                config.LoadArgs(args);
                try
                {
                    runtime = TumblerRuntime.FromConfiguration(config, new TextWriterClientInteraction(Console.Out, Console.In));
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

                    var tor = new TorRegisterJob(config, runtime);
                    tor.Start();
                    interactive.Services.Add(tor);

                    if (!config.OnlyMonitor)
                    {
                        host.Start();
                        interactive.Services.Add(host);
                    }

                    string baseUri;
                    if (runtime.TorUri.ToString().EndsWith("/"))
                        baseUri = runtime.TorUri.ToString().Substring(0, runtime.TorUri.ToString().Length - 1);
                    else
                        baseUri = runtime.TorUri.ToString();

                    var tempUri = (baseUri + "?h=" + runtime.ClassicTumblerParametersHash).Replace("http:", "ctb:");
                    File.WriteAllText(Path.Combine(config.DataDir, "uri.txt"), tempUri);

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