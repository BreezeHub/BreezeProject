// Based on StratisBitcoinFullNode configuration code

using System;

using System.Net;
using System.Text;
using System.IO;
using BreezeCommon;
using NBitcoin;
using NTumbleBit;

namespace Breeze.BreezeServer
{
    /// <summary>
    /// BreezeConfiguration is an instantiation of the user defined config
    /// </summary>
    public class BreezeConfiguration
    {
        public Network TumblerNetwork { get; set; }

        public bool UseTor { get; set; } = false;

        public string RpcUser { get; set; }
        public string RpcPassword { get; set; }
        public string RpcUrl { get; set; }

        public string TumblerUrl { get; set; }

        public IPAddress Ipv4Address { get; set; }
        public IPAddress Ipv6Address { get; set; }
        public string OnionAddress { get; set; }
        public int Port { get; set; }

        public string TumblerApiBaseUrl { get; set; }
        public string TumblerRsaKeyFile { get; set; }
        public string TumblerEcdsaKeyAddress { get; set; }

        public Money TxOutputValueSetting { get; set; }
        public Money TxFeeValueSetting { get; set; }

        public BreezeConfiguration(string configPath, string datadir = null)
        {
            try
            {
                if (!File.Exists(configPath))
                {
                    StringBuilder builder = new StringBuilder();
                    builder.AppendLine("# Breeze TumbleBit daemon settings");
                    builder.AppendLine("#network=testnet");
                    builder.AppendLine("#tor.enabled=");
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
                    builder.AppendLine("#tumbler.rsakeyfile=");
                    builder.AppendLine("#tumbler.ecdsakeyaddress=");

                    File.WriteAllText(configPath, builder.ToString());

                    Console.WriteLine("*** Default blank configuration file created, please set configuration values and restart ***");
                    Environment.Exit(0);
                }

                var configFile = TextFileConfiguration.Parse(File.ReadAllText(configPath));

                if (configFile.GetOrDefault<string>("network", "testnet").Equals("testnet"))
                {
                    TumblerNetwork = Network.TestNet;
                }

                if (configFile.GetOrDefault<string>("tor.enabled", "true").Equals("true"))
                {
                    UseTor = true;
                }

                if (configFile.GetOrDefault<string>("network", "testnet").Equals("regtest"))
                {
                    TumblerNetwork = Network.RegTest;
                }

                if (configFile.GetOrDefault<string>("network", "testnet").Equals("main"))
                {
                    TumblerNetwork = Network.Main;
                }
                
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
                    string defaultAddress = this.UseTor ? null : NTumbleBit.Utils.GetInternetConnectedAddress().ToString();
                    Ipv4Address = IPAddress.Parse(configFile.GetOrDefault<string>("breeze.ipv4", defaultAddress));
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

                //if (Ipv4Address == null && Ipv6Address == null && OnionAddress == null)
                //{
                //    throw new Exception("ERROR: No valid IP/onion addresses in configuration");
                //}

                Port = configFile.GetOrDefault<int>("breeze.port", 37123);

                TumblerApiBaseUrl = configFile.GetOrDefault<string>("tumbler.url", null);

                // Use user keyfile; default new key if invalid

                string bitcoinNetwork;
                
                if (TumblerNetwork == Network.Main)
                    bitcoinNetwork = "MainNet";
                else if (TumblerNetwork == Network.RegTest)
                    bitcoinNetwork = "RegTest";
                else // TumblerNetwork == Network.TestNet
                    bitcoinNetwork = "TestNet";

                if (datadir == null)
                {
                    // Create default directory for key files if it does not already exist
                    Directory.CreateDirectory(Path.Combine(GetDefaultDataDir("NTumbleBitServer"), bitcoinNetwork));

                    TumblerRsaKeyFile = configFile.GetOrDefault<string>("tumbler.rsakeyfile",
                        Path.Combine(GetDefaultDataDir("NTumbleBitServer"), bitcoinNetwork, "Tumbler.pem"));
                }
                else
                {
                    Directory.CreateDirectory(Path.Combine(datadir, bitcoinNetwork));
                    
                    TumblerRsaKeyFile = configFile.GetOrDefault<string>("tumbler.rsakeyfile",
                        Path.Combine(datadir, bitcoinNetwork, "Tumbler.pem"));                    
                }
                
                TumblerRsaKeyFile = BreezeConfigurationValidator.ValidateTumblerRsaKeyFile(
                    TumblerRsaKeyFile,
                    TumblerRsaKeyFile
                );
                
                TumblerEcdsaKeyAddress = configFile.GetOrDefault<string>("tumbler.ecdsakeyaddress", null);

                TxOutputValueSetting = new Money(configFile.GetOrDefault<int>("breeze.regtxoutputvalue", 1000), MoneyUnit.Satoshi);
                TxFeeValueSetting = new Money(configFile.GetOrDefault<int>("breeze.regtxfeevalue", 10000), MoneyUnit.Satoshi);
            }
            catch (Exception e)
            {
                throw new Exception("ERROR: Unable to read configuration. " + e);
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