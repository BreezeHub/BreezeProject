using System;
using System.IO;
using System.Threading;
using CommandLine;

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

using NBitcoin;

using NTumbleBit;
using NTumbleBit.Logging;
using NTumbleBit.ClassicTumbler;
using NTumbleBit.ClassicTumbler.Server.CLI;
using NTumbleBit.ClassicTumbler.Server;
using NTumbleBit.Services;
using NTumbleBit.Configuration;
using NTumbleBit.ClassicTumbler.CLI;

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
			Logs.Configure(new NTumbleBit.Logging.FuncLoggerFactory(i => new ConsoleLogger(i, (a, b) => true, false)));
			
            string[] args;
			
			if (!testnet)
				// TODO: Tumbler is locked to testnet for testing
				args = new string[] {"-testnet"};
			else
				args = new string[] {"-testnet"};

			Logs.Configure(new FuncLoggerFactory(i => new CustomerConsoleLogger(i, (a, b) => true, false)));

			using(var interactive = new Interactive())
			{
				var config = new TumblerConfiguration();
				config.LoadArgs(args);
				try
				{
					var runtime = TumblerRuntime.FromConfiguration(config, new TextWriterClientInteraction(Console.Out, Console.In));
					interactive.Runtime = new ServerInteractiveRuntime(runtime);
					IWebHost host = null;
					if(!config.OnlyMonitor)
					{
						host = new WebHostBuilder()
						.UseKestrel()
						.UseAppConfiguration(runtime)
						.UseContentRoot(Directory.GetCurrentDirectory())
						.UseStartup<Startup>()
						.UseUrls(config.GetUrls())
						.Build();
					}

					var job = new BroadcasterJob(interactive.Runtime.Services);
					job.Start(interactive.BroadcasterCancellationToken);

					if(!config.OnlyMonitor)
						new Thread(() =>
						{
							try
							{
								host.Run(interactive.MixingCancellationToken);
							}
							catch(Exception ex)
							{
								if(!interactive.MixingCancellationToken.IsCancellationRequested)
									Logs.Tumbler.LogCritical(1, ex, "Error while starting the host");
							}
							if(interactive.MixingCancellationToken.IsCancellationRequested)
								Logs.Tumbler.LogInformation("Server stopped");
						}).Start();
					interactive.StartInteractive();
				}
				catch(ConfigException ex)
				{
					if(!string.IsNullOrEmpty(ex.Message))
						Logs.Configuration.LogError(ex.Message);
				}
				catch(Exception exception)
				{
					Logs.Tumbler.LogError("Exception thrown while running the server");
					Logs.Tumbler.LogError(exception.ToString());
				}
			}
		}
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