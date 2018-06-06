﻿using NTumbleBit.Configuration;
using NTumbleBit.Tor;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using TCPServer.Client;

namespace NTumbleBit.ClassicTumbler.Client.ConnectionSettings
{
	public class ConnectionSettingsBase
	{
		//Default to socks connection is safe if the cycle parameters make it impossible to have Alice and Bob both connected on a 10 minutes span
		public static ConnectionSettingsBase ParseConnectionSettings(string prefix, TextFileConfiguration config, string defaultType = "socks")
		{
			var type = config.GetOrDefault<string>(prefix + ".proxy.type", defaultType);
			if(type.Equals("none", StringComparison.OrdinalIgnoreCase))
			{
				return new ConnectionSettingsBase();
			}
			else if(type.Equals("socks", StringComparison.OrdinalIgnoreCase))
			{
                SocksConnectionSettings settings = new SocksConnectionSettings();
				var server = config.GetOrDefault<IPEndPoint>(prefix + ".proxy.server", new IPEndPoint(IPAddress.Parse("127.0.0.1"), 9050));
				settings.Proxy = server;
				return settings;
			}
			else
				throw new ConfigException(prefix + ".proxy.type is not supported, should be socks or http");
		}
		public virtual HttpMessageHandler CreateHttpHandler(TimeSpan? connectTimeout)
		{
			var handler = new TCPHttpMessageHandler(new ClientOptions() { IncludeHeaders = false });

            if (connectTimeout != null)
                handler.Options.ConnectTimeout = connectTimeout.Value;

            return handler;
		}
	}
}
