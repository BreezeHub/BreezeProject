// Based on StratisBitcoinFullNode configuration code

using System;

using System.Net;
using System.Text;
using System.IO;

using NBitcoin;

namespace Breeze.BreezeD
{
    public class BreezeConfiguration
    {
        public bool IsTestNet { get; set; }
        public string RpcUser { get; set; }
        public string RpcPassword { get; set; }
        public string RpcUrl { get; set; }

        public string TumblerUrl { get; set; }

        public IPAddress Ipv4Address { get; set; }
        public IPAddress Ipv6Address { get; set; }
        public string OnionAddress { get; set; }
        public int Port { get; set; }

        public string TumblerApiBaseUrl { get; set; }
        public string TumblerRsaKeyPath { get; set; }
        public string TumblerEcdsaKeyAddress { get; set; }

        public Money TxOutputValueSetting { get; set; }
        public Money TxFeeValueSetting { get; set; }

        public BreezeConfiguration(string configPath)
        {
            try
            {
                if (!File.Exists(configPath))
                {
                    StringBuilder builder = new StringBuilder();
                    builder.AppendLine("# Stratis TumbleBit daemon settings");
                    builder.AppendLine("#rpc.user=");
                    builder.AppendLine("#rpc.password=");
                    builder.AppendLine("#rpc.url=http://127.0.0.1:16174");
                    builder.AppendLine("#breeze.ipv4=");
                    builder.AppendLine("#breeze.ipv6=");
                    builder.AppendLine("#breeze.onion=");
                    builder.AppendLine("#breeze.port=");
                    builder.AppendLine("# Value of each registration transaction output (in satoshi)");
                    builder.AppendLine("#breeze.regtxoutputvalue=");
                    builder.AppendLine("# Value of registration transaction fee (in satoshi)");
                    builder.AppendLine("#breeze.regtxfeevalue=");
                    builder.AppendLine("#tumbler.url=");
                    builder.AppendLine("#tumbler.rsakeypath=");
                    builder.AppendLine("#tumbler.ecdsakeyaddress=");

                    File.WriteAllText(configPath, builder.ToString());

                    Console.WriteLine("*** Default blank configuration file created, please set configuration values and restart ***");
                    Environment.Exit(0);
                }

                var configFile = TextFileConfiguration.Parse(File.ReadAllText(configPath));

                IsTestNet = configFile.GetOrDefault<bool>("testnet", false);

                RpcUser = configFile.GetOrDefault<string>("rpc.user", null);
                RpcPassword = configFile.GetOrDefault<string>("rpc.password", null);
                RpcUrl = configFile.GetOrDefault<string>("rpc.url", null);

                if (RpcUser == null || RpcPassword == null || RpcUrl == null)
                {
                    throw new Exception("ERROR: RPC information in config file is invalid");
                }

                try
                {
                    // Assume that if it parses it's valid
                    Ipv4Address = IPAddress.Parse(configFile.GetOrDefault<string>("breeze.ipv4", null));
                }
                catch (Exception)
                {
                    Ipv4Address = null;
                }

                try
                {
                    // Assume that if it parses it's valid
                    Ipv6Address = IPAddress.Parse(configFile.GetOrDefault<string>("breeze.ipv6", null));
                }
                catch (Exception)
                {
                    Ipv6Address = null;
                }

                try
                {
                    OnionAddress = configFile.GetOrDefault<string>("breeze.onion", null);

                    if (OnionAddress.Length > 16)
                    {
                        // Regard as invalid, do not try to truncate etc.
                        OnionAddress = null;
                    }
                }
                catch (Exception)
                {
                    OnionAddress = null;
                }

                if (Ipv4Address == null && Ipv6Address == null && OnionAddress == null)
                {
                    throw new Exception("ERROR: No valid IP/onion addresses in configuration");
                }

                Port = configFile.GetOrDefault<int>("breeze.port", 37123);

                TumblerApiBaseUrl = configFile.GetOrDefault<string>("tumbler.url", null);
                TumblerRsaKeyPath = configFile.GetOrDefault<string>("tumbler.rsakeypath", null);
                TumblerEcdsaKeyAddress = configFile.GetOrDefault<string>("tumbler.ecdsakeyaddress", null);

                TxOutputValueSetting = new Money(configFile.GetOrDefault<int>("breeze.regtxoutputvalue", 1000), MoneyUnit.Satoshi);
                TxFeeValueSetting = new Money(configFile.GetOrDefault<int>("breeze.regtxfeevalue", 10000), MoneyUnit.Satoshi);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw new Exception("ERROR: Unable to read configuration");
            }
        }

		public static string GetDefaultDataDir(string appName)
		{
			string directory = null;
			var home = Environment.GetEnvironmentVariable("HOME");
			if (!string.IsNullOrEmpty(home))
			{
				directory = home;
				directory = Path.Combine(directory, "." + appName.ToLowerInvariant());
			}
			else
			{
				var localAppData = Environment.GetEnvironmentVariable("APPDATA");
				if (!string.IsNullOrEmpty(localAppData))
				{
					directory = localAppData;
					directory = Path.Combine(directory, appName);
				}
				else
				{
					throw new DirectoryNotFoundException("Could not find suitable datadir");
				}
			}
			if (!Directory.Exists(directory))
			{
				Directory.CreateDirectory(directory);
			}
			//directory = Path.Combine(directory, network.Name);
			if (!Directory.Exists(directory))
			{
				Directory.CreateDirectory(directory);
			}
			return directory;
		}
    }
}