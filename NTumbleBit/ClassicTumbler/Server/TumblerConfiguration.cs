﻿using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.RPC;
using NTumbleBit.ClassicTumbler.Client.ConnectionSettings;
using NTumbleBit.Configuration;
using NTumbleBit.Logging;
using NTumbleBit.Services;

namespace NTumbleBit.ClassicTumbler.Server
{

    public class TumblerConfiguration
	{
		public TumblerConfiguration()
		{
			ClassicTumblerParameters = new ClassicTumblerParameters();
		}

		public string DataDir
		{
			get; set;
		}

		public Network Network
		{
			get
			{
				return ClassicTumblerParameters.Network;
			}
			set
			{
				ClassicTumblerParameters.Network = value;
			}
		}

		public ClassicTumblerParameters ClassicTumblerParameters
		{
			get; set;
		}
		public string ConfigurationFile
		{
			get;
			set;
		}

		public RPCArgs RPC
		{
			get; set;
		} = new RPCArgs();

		public IPEndPoint Listen
		{
			get;
			set;
		}
		public bool OnlyMonitor
		{
			get;
			set;
		}
		public bool Cooperative
		{
			get;
			set;
		}

		public TorConnectionSettings TorSettings
		{
			get;
			set;
		}
		public string TorPath
		{
			get;
			set;
		}

		public Tracker Tracker
		{
			get;
			set;
		}

		public ExternalServices Services
		{
			get;
			set;
		}

		public DBreezeRepository DBreezeRepository
		{
			get;
			set;
		}

		public TumblerConfiguration LoadArgs(String[] args)
		{
			ConfigurationFile = args.Where(a => a.StartsWith("-conf=", StringComparison.Ordinal)).Select(a => a.Substring("-conf=".Length).Replace("\"", "")).FirstOrDefault();
			DataDir = args.Where(a => a.StartsWith("-datadir=", StringComparison.Ordinal)).Select(a => a.Substring("-datadir=".Length).Replace("\"", "")).FirstOrDefault();
			if(DataDir != null && ConfigurationFile != null)
			{
				var isRelativePath = Path.GetFullPath(ConfigurationFile).Length > ConfigurationFile.Length;
				if(isRelativePath)
				{
					ConfigurationFile = Path.Combine(DataDir, ConfigurationFile);
				}
			}

			Network = args.Contains("-testnet", StringComparer.OrdinalIgnoreCase) ? Network.TestNet :
				args.Contains("-regtest", StringComparer.OrdinalIgnoreCase) ? Network.RegTest :
                args.Contains("-stratismain", StringComparer.OrdinalIgnoreCase) ? Network.StratisMain :
                args.Contains("-stratistest", StringComparer.OrdinalIgnoreCase) ? Network.StratisTest :
                args.Contains("-stratisregtest", StringComparer.OrdinalIgnoreCase) ? Network.StratisRegTest :
				Network.Main;

			if(ConfigurationFile != null)
			{
				AssetConfigFileExists();
				var configTemp = TextFileConfiguration.Parse(File.ReadAllText(ConfigurationFile));
				
				Network = configTemp.GetOrDefault<bool>("testnet", false) ? Network.TestNet :
						  configTemp.GetOrDefault<bool>("regtest", false) ? Network.RegTest :
                          configTemp.GetOrDefault<bool>("stratismain", false) ? Network.StratisMain :
                          configTemp.GetOrDefault<bool>("stratistest", false) ? Network.StratisTest :
                          configTemp.GetOrDefault<bool>("stratisregtest", false) ? Network.StratisRegTest :
						  Network.Main;
			}
			if(DataDir == null)
			{
				DataDir = DefaultDataDirectory.GetDefaultDirectory("NTumbleBitServer", Network);
			}

			if(ConfigurationFile == null)
			{
				ConfigurationFile = GetDefaultConfigurationFile(Network);
			}
			Logs.Configuration.LogInformation("Network: " + Network);

			Logs.Configuration.LogInformation("Data directory set to " + DataDir);
			Logs.Configuration.LogInformation("Configuration file set to " + ConfigurationFile);

			if(!Directory.Exists(DataDir))
				throw new ConfigurationException("Data directory does not exists");

			var consoleConfig = new TextFileConfiguration(args);
			var config = TextFileConfiguration.Parse(File.ReadAllText(ConfigurationFile));
			consoleConfig.MergeInto(config, true);

			if(config.Contains("help"))
			{
				Console.WriteLine("Details on the wiki page :  https://github.com/NTumbleBit/NTumbleBit/wiki/Server-Config");
				OpenBrowser("https://github.com/NTumbleBit/NTumbleBit/wiki/Server-Config");
				Environment.Exit(0);
			}

			var standardCycles = new StandardCycles(Network);
			var cycleName = config.GetOrDefault<string>("cycle", standardCycles.Debug ? "shorty2x" : "shorty2x");

			Logs.Configuration.LogInformation($"Using cycle {cycleName}");
			
			var standardCycle = standardCycles.GetStandardCycle(cycleName);
			if(standardCycle == null)
				throw new ConfigException($"Invalid cycle name, choose among {String.Join(",", standardCycles.ToEnumerable().Select(c => c.FriendlyName).ToArray())}");

			ClassicTumblerParameters.CycleGenerator = standardCycle.Generator;
			ClassicTumblerParameters.Denomination = standardCycle.Denomination;
			var torEnabled = config.GetOrDefault<bool>("tor.enabled", true);
			if(torEnabled)
			{
				TorSettings = TorConnectionSettings.ParseConnectionSettings("tor", config);
			}

			Cooperative = config.GetOrDefault<bool>("cooperative", true);

			var defaultPort = config.GetOrDefault<int>("port", 37123);

			OnlyMonitor = config.GetOrDefault<bool>("onlymonitor", false);
			Listen = new IPEndPoint(IPAddress.Parse("127.0.0.1"), defaultPort);

			RPC = RPCArgs.Parse(config, Network);
			TorPath = config.GetOrDefault<string>("torpath", "tor");		    
			DBreezeRepository = new DBreezeRepository(Path.Combine(DataDir, "db2"));
			Tracker = new Tracker(DBreezeRepository, Network);

		    // 1% fee by default (desired value should be set in configuration anyway)
		    var defaultFee = Money.Satoshis((decimal)ClassicTumblerParameters.Denomination.Satoshi * 0.01m);
		    ClassicTumblerParameters.Fee = config.GetOrDefault<Money>("tumbler.fee", defaultFee);

			RPCClient rpc = null;
			try
			{
				rpc = RPC.ConfigureRPCClient(Network);
			}
			catch
			{
				throw new ConfigException("Please, fix rpc settings in " + ConfigurationFile);
			}

			Services = ExternalServices.CreateFromRPCClient(rpc, DBreezeRepository, Tracker, true);
			return this;
		}

		public string CycleName
		{
			get; set;
		}
		public bool AllowInsecure
		{
			get;
			internal set;
		}
		public bool NoRSAProof
		{
			get;
			set;
		} = false;
		public bool TorMandatory
		{
			get;
			set;
		} = true;

		public string GetDefaultConfigurationFile(Network network)
		{
			var config = Path.Combine(DataDir, "server.config");
			Logs.Configuration.LogInformation("Configuration file set to " + config);
			if(!File.Exists(config))
			{
				Logs.Configuration.LogInformation("Creating configuration file");
				StringBuilder builder = new StringBuilder();
				builder.AppendLine("####Common Commands####");
				builder.AppendLine("#Connection to the input Bitcoin wallet.");
				builder.AppendLine("#rpc.url=http://localhost:" + Network.RPCPort + "/");
				builder.AppendLine("#rpc.user=bitcoinuser");
				builder.AppendLine("#rpc.password=bitcoinpassword");
				builder.AppendLine("#rpc.cookiefile=yourbitcoinfolder/.cookie");

				builder.AppendLine();
				builder.AppendLine();

				builder.AppendLine("####Tumbler settings####");
				builder.AppendLine("## The fees in BTC");
				builder.AppendLine("#tumbler.fee=0.001");
				builder.AppendLine("## The cycle used can be one of: " + string.Join(",", new StandardCycles(Network).ToEnumerable().Select(c => c.FriendlyName)));
				builder.AppendLine("#cycle=shorty2x");

				builder.AppendLine();
				builder.AppendLine();

				builder.AppendLine("####Server Commands####");
				builder.AppendLine("#port=37123");
				builder.AppendLine("#listen=0.0.0.0");

				builder.AppendLine();
				builder.AppendLine();

				builder.AppendLine("####Tor configuration (default is enabled, using cookie auth or no auth on Tor control port 9051)####");
				builder.AppendLine("#tor.enabled=true");
				builder.AppendLine("#tor.server=127.0.0.1:9051");
				builder.AppendLine("#tor.password=mypassword");
				builder.AppendLine("#tor.cookiefile=/path/to/my/cookie/file");
				builder.AppendLine("#tor.virtualport=80");

				builder.AppendLine();
				builder.AppendLine();

				File.WriteAllText(config, builder.ToString());
			}
			return config;
		}

		private void AssetConfigFileExists()
		{
			if(!File.Exists(ConfigurationFile))
				throw new ConfigurationException("Configuration file does not exist");
		}

		public void OpenBrowser(string url)
		{
			try
			{
				if(RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
				{
					Process.Start(new ProcessStartInfo("cmd", $"/c start {url}")); // Works ok on windows
				}
				else if(RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
				{
					Process.Start("xdg-open", url);  // Works ok on linux
				}
				else if(RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
				{
					Process.Start("open", url); // Not tested
				}
			}
			catch(Exception)
			{
				// nothing happens
			}
		}
	}
}

