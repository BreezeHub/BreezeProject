using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NBitcoin;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace NTumbleBit.Logging
{
	public class Logs
	{
        public static string LogDir { get; set; }
        static Logs()
		{
			Configure(new FuncLoggerFactory(n => NullLogger.Instance), null);
		}

		public static void Configure(ILoggerFactory factory, string logDir = null)
		{
            LogDir = logDir;

            Configuration = factory.CreateLogger("Configuration");
			Tumbler = factory.CreateLogger("Tumbler");
			Client = factory.CreateLogger("Client");
			Broadcasters = factory.CreateLogger("Broadcasters");
			Tracker = factory.CreateLogger("Tracker");
			Wallet = factory.CreateLogger("Wallet");
			TOR = factory.CreateLogger("TOR");
		}
		public static ILogger Tumbler
		{
			get; set;
		}
		public static ILogger Client
		{
			get; set;
		}
		public static ILogger TOR
		{
			get; set;
		}
		public static ILogger Tracker
		{
			get; set;
		}
		public static ILogger Broadcasters
		{
			get; set;
		}
		public static ILogger Wallet
		{
			get; set;
		}

		public static Func<string, LogLevel, bool> SupportDebug(bool debug)
		{
			return (a, filter) =>
			{
				return (debug && filter == LogLevel.Debug) || filter > LogLevel.Debug;
			};
		}

		public static ILogger Configuration
		{
			get; set;
		}
		public const int ColumnLength = 16;
    }
}
