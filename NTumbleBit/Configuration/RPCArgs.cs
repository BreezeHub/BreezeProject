﻿using NBitcoin;
using NBitcoin.RPC;
using NTumbleBit.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System.Net;

namespace NTumbleBit.Configuration
{
	public class RPCArgs
	{
		public Uri Url
		{
			get; set;
		}
		public string User
		{
			get; set;
		}
		public string Password
		{
			get; set;
		}
		public string CookieFile
		{
			get; set;
		}
		public string AuthenticationString
		{
			get;
			set;
		}

		public RPCClient ConfigureRPCClient(Network network)
		{
			RPCClient rpcClient = null;
			var url = Url;
			var usr = User;
			var pass = Password;
			if(url != null && usr != null && pass != null)
				rpcClient = new RPCClient(new System.Net.NetworkCredential(usr, pass), url, network);
			if(rpcClient == null)
			{
				if(url != null && CookieFile != null)
				{
					try
					{

						rpcClient = new RPCClient(new RPCCredentialString() { CookieFile = CookieFile }, url, network);
					}
					catch(IOException)
					{
						Logs.Configuration.LogWarning("RPC Cookie file not found at " + CookieFile);
					}
				}

				if(AuthenticationString != null)
				{
					rpcClient = new RPCClient(RPCCredentialString.Parse(AuthenticationString), url, network);
				}

				if(rpcClient == null)
				{
					try
					{
						rpcClient = new RPCClient(null as NetworkCredential, url, network);
					}
					catch { }
					if(rpcClient == null)
					{
						Logs.Configuration.LogError("RPC connection settings not configured");
						throw new ConfigException();
					}
				}
			}

			Logs.Configuration.LogInformation("Testing RPC connection to " + rpcClient.Address.AbsoluteUri);
			try
			{
				var address = new Key().PubKey.GetAddress(network);
				var isValid = rpcClient.ValidateAddress(address).IsValid;
				if(!isValid)
				{
					Logs.Configuration.LogError("The RPC Server is on a different blockchain than the one configured for tumbling");
					throw new ConfigException();
				}
			}
			catch(ConfigException)
			{
				throw;
			}
			catch(RPCException ex)
			{
				Logs.Configuration.LogError("Invalid response from RPC server " + ex.Message);
				throw new ConfigException();
			}
			catch(Exception ex)
			{
				Logs.Configuration.LogError("Error connecting to RPC server " + ex.Message);
				throw new ConfigException();
			}
			Logs.Configuration.LogInformation("RPC connection successfull");

			if(rpcClient.GetBlockHash(0) != network.GenesisHash)
			{
				Logs.Configuration.LogError("The RPC server is not using the chain " + network.Name);
				throw new ConfigException();
			}
			var getInfo = rpcClient.SendCommand(RPCOperations.getinfo);
			var version = ((JObject)getInfo.Result)["version"].Value<int>();
			if(version < MIN_CORE_VERSION)
			{
				Logs.Configuration.LogError($"The minimum Bitcoin Core version required is {MIN_CORE_VERSION} (detected: {version})");
				throw new ConfigException();
			}
			Logs.Configuration.LogInformation($"Bitcoin Core version detected: {version}");
			return rpcClient;
		}

		const int MIN_CORE_VERSION = 140100;
		public static RPCClient ConfigureRPCClient(TextFileConfiguration confArgs, Network network, string prefix = null)
		{
			RPCArgs args = Parse(confArgs, network, prefix);
			return args.ConfigureRPCClient(network);
		}

		public static RPCArgs Parse(TextFileConfiguration confArgs, Network network, string prefix = null)
		{
			prefix = prefix ?? "";
			if(prefix != "")
			{
				if(!prefix.EndsWith("."))
					prefix += ".";
			}
			try
			{
				var url = confArgs.GetOrDefault<string>(prefix + "rpc.url", network == null ? null : "http://localhost:" + network.RPCPort + "/");
				return new RPCArgs()
				{
					User = confArgs.GetOrDefault<string>(prefix + "rpc.user", null),
					Password = confArgs.GetOrDefault<string>(prefix + "rpc.password", null),
					CookieFile = confArgs.GetOrDefault<string>(prefix + "rpc.cookiefile", null),
					Url = url == null ? null : new Uri(url),
					AuthenticationString = confArgs.GetOrDefault<string>(prefix + "rpc.auth", null)
				};
			}
			catch(FormatException)
			{
				throw new ConfigException("rpc.url is not an url");
			}
		}
	}
}
