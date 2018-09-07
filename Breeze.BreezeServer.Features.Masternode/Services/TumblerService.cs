using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NTumbleBit;
using NTumbleBit.ClassicTumbler.CLI;
using NTumbleBit.ClassicTumbler.Server;
using NTumbleBit.Configuration;
using NTumbleBit.Logging;
using NTumbleBit.Services;
using Stratis.Bitcoin.Configuration;
using ITumblerService = Breeze.BreezeServer.Features.Masternode.Services.ITumblerService;
using TextFileConfiguration = Stratis.Bitcoin.Configuration.TextFileConfiguration;


namespace Breeze.BreezeServer.Features.Masternode.Services
{
    public class TumblerService : ITumblerService
    {
        public TumblerConfiguration config { get; set; }
        public TumblerRuntime runtime { get; set; }

        /// <summary>Settings relevant to node.</summary>
        private readonly NodeSettings nodeSettings;

        /// <summary>Settings relevant to masternode.</summary>
        private readonly MasternodeSettings masternodeSettings;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        public TumblerService(NodeSettings nodeSettings, MasternodeSettings masternodeSettings, ILoggerFactory loggerFactory)
        {
            this.nodeSettings = nodeSettings;
            this.masternodeSettings = masternodeSettings;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        public void StartTumbler(bool getConfigOnly)
        {
            var argsTemp = new List<string>();
            
            if (nodeSettings.DataDir != null)
                argsTemp.Add("-datadir=" + nodeSettings.DataDir);

			argsTemp.Add($"-tumblerProtocol={masternodeSettings.TumblerProtocol}");

            string[] args = argsTemp.ToArray();
            var argsConf = new TextFileConfiguration(args);
            nodeSettings.ConfigReader.MergeInto(argsConf);
            
            var debug = argsConf.GetOrDefault<bool>("debug", false);

            ConsoleLoggerProcessor loggerProcessor = new ConsoleLoggerProcessor();

            Logs.Configure(new FuncLoggerFactory(i => new CustomerConsoleLogger(i, Logs.SupportDebug(debug), false, loggerProcessor)), nodeSettings.DataFolder.LogPath);

            if (getConfigOnly)
            {
                config = new TumblerConfiguration();
				config.LoadArgs(args);
                config.Services = ExternalServices.CreateFromFullNode(config.DBreezeRepository, config.Tracker, true);

                config.TorMandatory = !masternodeSettings.IsRegTest;

                runtime = TumblerRuntime.FromConfiguration(config, new AcceptAllClientInteraction());
                return;
            }

            using (var interactive = new Interactive())
            {
                config = new TumblerConfiguration();
                config.LoadArgs(args);
                config.Services = ExternalServices.CreateFromFullNode(config.DBreezeRepository, config.Tracker, true);
                config.TorMandatory = !masternodeSettings.IsRegTest;

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

                    if (masternodeSettings.TorEnabled)
						baseUri = runtime.TorUri.ToString().TrimEnd('/'); 
                    else
	                    baseUri = runtime.LocalEndpoint.ToString();

					if (!baseUri.StartsWith("http://") && (!baseUri.StartsWith("ctb://")))
                        baseUri = "http://" + baseUri;
                    
                    var tempUri = (baseUri + "?h=" + runtime.ClassicTumblerParametersHash).Replace("http:", "ctb:");

					//The uri.txt is only used in the integration tests as there is no registration service running (no Stratis daemon)
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