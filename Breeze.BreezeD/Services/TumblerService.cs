using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Server.Kestrel;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Newtonsoft.Json;

using NBitcoin;
using NBitcoin.RPC;

using NTumbleBit.Common;
using NTumbleBit.Common.Logging;
using NTumbleBit.ClassicTumbler;
using NTumbleBit.TumblerServer;
using NTumbleBit.TumblerServer.Services;
using System.Text;
using System.Reflection;
using CommandLine;

namespace Breeze.BreezeD
{
    public class TumblerService : ITumblerService
    {
		public ClassicTumblerParameters TumblerParameters
		{
			get; set;
		}
		public Network Network
		{
			get; set;
		}
		public Tracker Tracker
		{
			get; set;
		}
		public ExternalServices Services
		{
			get; set;
		}
		public CancellationTokenSource MixingToken
		{
			get; set;
		}
		public CancellationTokenSource BroadcasterToken
		{
			get; set;
		}

        public void StartTumbler(bool testnet)
        {
			Logs.Configure(new NTumbleBit.Common.Logging.FuncLoggerFactory(i => new ConsoleLogger(i, (a, b) => true, false)));
			var config = new NTumbleBit.TumblerServer.TumblerConfiguration();
			
            string[] args;
			
			if (!testnet)
				args = new string[] {};
			else
				args = new string[] {"-testnet"};

			BroadcasterToken = new CancellationTokenSource();
			MixingToken = new CancellationTokenSource();
			config.LoadArgs(args);
			try
			{
				var runtime = TumblerRuntime.FromConfiguration(config);
				Services = runtime.Services;
				Tracker = runtime.Tracker;
				IWebHost host = null;
				if(!config.OnlyMonitor)
				{
					host = new WebHostBuilder()
					.UseKestrel()
					.UseAppConfiguration(runtime)
					.UseContentRoot(Directory.GetCurrentDirectory())
					.UseIISIntegration()
					.UseStartup<Startup>()
					.UseUrls(config.GetUrls())
					.Build();
				}

				var job = new BroadcasterJob(Services, Logs.Main);
				job.Start(BroadcasterToken.Token);
				Logs.Main.LogInformation("BroadcasterJob started");

				TumblerParameters = config.ClassicTumblerParameters;
				Network = config.Network;

				if(!config.OnlyMonitor)
					new Thread(() =>
					{
						try
						{
							host.Run(MixingToken.Token);
						}
						catch(Exception ex)
						{
							if(!MixingToken.IsCancellationRequested)
								Logs.Server.LogCritical(1, ex, "Error while starting the host");
						}
						if(MixingToken.IsCancellationRequested)
							Logs.Server.LogInformation("Server stopped");
					}).Start();
				StartInteractive();
			}
			catch(ConfigException ex)
			{
				if(!string.IsNullOrEmpty(ex.Message))
					Logs.Main.LogError(ex.Message);
			}
			catch(Exception exception)
			{
				Logs.Main.LogError("Exception thrown while running the server");
				Logs.Main.LogError(exception.ToString());
			}
			finally
			{
				if(!MixingToken.IsCancellationRequested)
					MixingToken.Cancel();
				if(!BroadcasterToken.IsCancellationRequested)
					BroadcasterToken.Cancel();
			}
		}

		void StartInteractive()
		{
			Console.Write(Assembly.GetEntryAssembly().GetName().Name
						+ " " + Assembly.GetEntryAssembly().GetName().Version);
			Console.WriteLine(" -- TumbleBit Implementation in .NET Core");
			Console.WriteLine("Type \"help\" or \"help <command>\" for more information.");

			bool quit = false;
			while(!quit)
			{
				Console.Write(">>> ");
				var split = Console.ReadLine().Split(null);
				try
				{

					Parser.Default.ParseArguments<StatusOptions, StopOptions, QuitOptions>(split)
						.WithParsed<StatusOptions>(_ => GetStatus(_))
						.WithParsed<StopOptions>(_ => Stop(_))
						.WithParsed<QuitOptions>(_ => quit = true);
				}
				catch(FormatException)
				{
					Console.WriteLine("Invalid format");
				}
			}
			MixingToken.Cancel();
			BroadcasterToken.Cancel();
		}
		private void Stop(StopOptions opt)
		{
			opt.Target = opt.Target ?? "";
			var stopMixer = opt.Target.Equals("mixer", StringComparison.OrdinalIgnoreCase);
			var stopBroadcasted = opt.Target.Equals("broadcaster", StringComparison.OrdinalIgnoreCase);
			var both = opt.Target.Equals("both", StringComparison.OrdinalIgnoreCase);
			if(both)
				stopMixer = stopBroadcasted = true;
			if(stopMixer)
			{
				MixingToken.Cancel();
			}
			if(stopBroadcasted)
			{
				BroadcasterToken.Cancel();
			}
			if(!stopMixer && !stopBroadcasted)
				throw new FormatException();
		}

		void GetStatus(StatusOptions options)
		{
			options.Query = options.Query.Trim();
			if(!string.IsNullOrWhiteSpace(options.Query))
			{
				bool parsed = false;
				try
				{
					options.CycleId = int.Parse(options.Query);
					parsed = true;
				}
				catch { }
				try
				{
					options.TxId = new uint256(options.Query).ToString();
					parsed = true;
				}
				catch { }
				try
				{
					options.Address = BitcoinAddress.Create(options.Query, Network).ToString();
					parsed = true;
				}
				catch { }
				if(!parsed)
					throw new FormatException();
			}

			if(options.CycleId != null)
			{
				CycleParameters cycle = null;

				try
				{
					cycle = TumblerParameters?.CycleGenerator?.GetCycle(options.CycleId.Value);
				}
				catch
				{
					Console.WriteLine("Invalid cycle");
					return;
				}
				var records = Tracker.GetRecords(options.CycleId.Value);
				var currentHeight = Services.BlockExplorerService.GetCurrentHeight();

				StringBuilder builder = new StringBuilder();
				var phases = new[]
				{
					CyclePhase.Registration,
					CyclePhase.ClientChannelEstablishment,
					CyclePhase.TumblerChannelEstablishment,
					CyclePhase.PaymentPhase,
					CyclePhase.TumblerCashoutPhase,
					CyclePhase.ClientCashoutPhase
				};

				if(cycle != null)
				{
					Console.WriteLine("Phases:");
					var periods = cycle.GetPeriods();
					foreach(var phase in phases)
					{
						var period = periods.GetPeriod(phase);
						if(period.IsInPeriod(currentHeight))
							builder.Append("(");
						builder.Append(phase.ToString());
						if(period.IsInPeriod(currentHeight))
							builder.Append($" {(period.End - currentHeight)} blocks left)");

						if(phase != CyclePhase.ClientCashoutPhase)
							builder.Append("-");
					}
					Console.WriteLine(builder.ToString());
					Console.WriteLine();
				}

				Console.WriteLine("Records:");
				foreach(var correlationGroup in records.GroupBy(r => r.Correlation).OrderBy(o => (int)o.Key))
				{
					Console.WriteLine("========");
					foreach(var group in correlationGroup.GroupBy(r => r.TransactionType).OrderBy(o => (int)o.Key))
					{
						builder = new StringBuilder();
						builder.AppendLine(group.Key.ToString());
						foreach(var data in group.OrderBy(g => g.RecordType))
						{
							builder.Append("\t" + data.RecordType.ToString());
							if(data.ScriptPubKey != null)
								builder.AppendLine(" " + data.ScriptPubKey.GetDestinationAddress(Network));
							if(data.TransactionId != null)
								builder.AppendLine(" " + data.TransactionId);
						}
						Console.WriteLine(builder.ToString());
					}
					Console.WriteLine("========");
				}
			}

			if(options.TxId != null)
			{
				var txId = new uint256(options.TxId);
				var result = Tracker.Search(txId);
				foreach(var record in result)
				{
					Console.WriteLine("Cycle " + record.Cycle);
					Console.WriteLine("Type " + record.TransactionType);
				}

				var knownTransaction = Services.TrustedBroadcastService.GetKnownTransaction(txId);
				Transaction tx = knownTransaction?.Transaction;
				if(knownTransaction != null)
				{
					if(knownTransaction.BroadcastAt != 0)
						Console.WriteLine("Planned for " + knownTransaction.BroadcastAt.ToString());
				}
				if(tx == null)
				{
					tx = Services.BroadcastService.GetKnownTransaction(txId);
				}
				var txInfo = Services.BlockExplorerService.GetTransaction(txId);
				if(tx == null)
					tx = txInfo?.Transaction;
				if(txInfo != null)
				{
					if(txInfo.Confirmations != 0)
						Console.WriteLine(txInfo.Confirmations + " Confirmations");
					else
						Console.WriteLine("Unconfirmed");
				}

				if(tx != null)
				{
					Console.WriteLine("Timelock " + tx.LockTime.ToString());
					Console.WriteLine("Hex " + tx.ToHex());
				}
				//TODO ask to other objects for more info
			}

			if(options.Address != null)
			{
				var address = BitcoinAddress.Create(options.Address, TumblerParameters.Network);
				var result = Tracker.Search(address.ScriptPubKey);
				foreach(var record in result)
				{
					Console.WriteLine("Cycle " + record.Cycle);
					Console.WriteLine("Type " + record.TransactionType);
				}
			}
		}
    }

	[Verb("status", HelpText = "Shows the current status.")]
	internal class StatusOptions
	{
		[Value(0, HelpText = "Search information about the specifed, cycle/transaction/address.")]
		public string Query
		{
			get; set;
		}
		
		public int? CycleId
		{
			get; set;
		}

		public string TxId
		{
			get; set;
		}
		
		public string Address
		{
			get; set;
		}
	}

	[Verb("stop", HelpText = "Stop a service.")]
	internal class StopOptions
	{
		[Value(0, HelpText = "\"stop mixer\" to stop the mixer, \"stop broadcaster\" to stop the broadcaster, \"stop both\" to stop both.")]
		public string Target
		{
			get; set;
		}
	}

	[Verb("exit", HelpText = "Quit.")]
	internal class QuitOptions
	{
		//normal options here
	}

	public class Startup
	{
		// This method gets called by the runtime. Use this method to add services to the container.
		// For more information on how to configure your application, visit http://go.microsoft.com/fwlink/?LinkID=398940
		public void ConfigureServices(IServiceCollection services)
		{
			services.AddSingleton<IObjectModelValidator, NoObjectModelValidator>();
			services.AddMvcCore(o => o.Filters.Add(new ActionResultExceptionFilter()))
				.AddJsonFormatters()
				.AddFormatterMappings();
		}

		// This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
		public void Configure(IApplicationBuilder app, IHostingEnvironment env,
			ILoggerFactory loggerFactory,
			IServiceProvider serviceProvider)
		{
			var logging = new FilterLoggerSettings();
			logging.Add("Microsoft.AspNetCore.Hosting.Internal.WebHost", LogLevel.Error);
			logging.Add("Microsoft.AspNetCore.Mvc", LogLevel.Error);
			logging.Add("Microsoft.AspNetCore.Server.Kestrel", LogLevel.Error);
			loggerFactory
				.WithFilter(logging)
				.AddConsole();

			if(env.IsDevelopment())
			{
				app.UseDeveloperExceptionPage();
			}

			app.UseMvc();

			var builder = serviceProvider.GetService<ConfigurationBuilder>() ?? new ConfigurationBuilder();
			Configuration = builder.Build();

			var config = serviceProvider.GetService<TumblerRuntime>();
			var options = GetMVCOptions(serviceProvider);
			NTumbleBit.Serializer.RegisterFrontConverters(options.SerializerSettings, config.Network);
		}

		public IConfiguration Configuration
		{
			get; set;
		}

		private static MvcJsonOptions GetMVCOptions(IServiceProvider serviceProvider)
		{
			return serviceProvider.GetRequiredService<IOptions<MvcJsonOptions>>().Value;
		}
	}

	internal class NoObjectModelValidator : IObjectModelValidator
	{
		public void Validate(ActionContext actionContext, ValidationStateDictionary validationState, string prefix, object model)
		{

		}
	}
}