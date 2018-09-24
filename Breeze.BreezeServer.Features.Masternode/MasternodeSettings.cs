using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security;
using System.Text;
using BreezeCommon;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NTumbleBit.ClassicTumbler.Server;
using Stratis.Bitcoin.Configuration;

namespace Breeze.BreezeServer.Features.Masternode
{
    /// <summary>
    /// Configuration related to Masternode setup.
    /// </summary>
    public class MasternodeSettings
    {
        /// <summary>Network name</summary>
        public Network Network { get; private set; }

        public bool IsRegTest => Network == Network.RegTest || Network == Network.StratisRegTest;

        /// <summary>Masternode port</summary>
        public int TumblerPort { get; private set; }

        /// <summary>Name of the tumbling cycle</summary>
        public string CycleType { get; private set; }

        /// <summary>Whether the TOR should be using for communication between Masternode and its clients</summary>
        public bool TorEnabled { get; private set; }

        /// <summary>Protocol whioch should be used to communicate between Masternode and its clients</summary>
        public TumblerProtocolType TumblerProtocol { get; private set; }

        /// <summary>Fee charged by the Masternode per tumbling cycle</summary>
        public decimal TumblerFee { get; private set; }

        /// <summary>Collateral address</summary>
        public string TumblerEcdsaKeyAddress { get; private set; }

        /// <summary>Forces the registration of the Masternode</summary>
        public bool ForceRegistration { get; private set; }

        /// <summary>Masternode IPv4 address</summary>
        public IPAddress Ipv4Address { get; set; }

        /// <summary>Masternode IPv6 address</summary>
        public IPAddress Ipv6Address { get; set; }

        /// <summary>Masternode Onion address</summary>
        public string OnionAddress { get; set; }

        /// <summary>Masternode API address</summary>
        public string TumblerApiBaseUrl { get; set; }

        public string TumblerRsaKeyFile { get; set; }

        public Money TxOutputValueSetting { get; set; }

        public Money TxFeeValueSetting { get; set; }

        public string TumblerWalletName { get; set; }

        public SecureString TumblerWalletPassword { get; set; }

        private static decimal DefaultMasternodeFee = 0.00075m;

        /// <summary>
        /// Initializes an instance of the object.
        /// </summary>
        public MasternodeSettings()
        {
        }

        

        /// <summary>
        /// Loads the masternode settings from the application configuration.
        /// </summary>
        /// <param name="nodeSettings">Application configuration.</param>
        private void LoadSettingsFromConfig(NodeSettings nodeSettings)
        {
            var config = nodeSettings.ConfigReader;
            this.Network = nodeSettings.Network;

            this.ForceRegistration = config.GetOrDefault<bool>("ForceRegistration", false);
            this.TumblerWalletName = config.GetOrDefault<string>("TumblerWalletName", null);
            string tumblerWalletPassword = config.GetOrDefault<string>("TumblerWalletPassword", null);
            SecureString tumblerWalletPasswordSecure = new SecureString();
            foreach (char c in tumblerWalletPassword)
                tumblerWalletPasswordSecure.AppendChar(c);
            tumblerWalletPassword = null;

            this.TumblerWalletPassword = tumblerWalletPasswordSecure;

            this.TumblerPort = config.GetOrDefault<int>("MasternodePort", TumblerConfiguration.DefaultTumblerPort);
            this.CycleType = config.GetOrDefault<string>("MasternodeCycle", "Kotori");
            this.TumblerEcdsaKeyAddress = config.GetOrDefault<string>("TumblerEcdsaKeyAddress", null);
            string tumblerFeeString = config.GetOrDefault<string>("MasternodeFee", DefaultMasternodeFee.ToString());
            this.TumblerFee = decimal.TryParse(tumblerFeeString, out decimal tumblerFee) ? tumblerFee : DefaultMasternodeFee;

            this.TorEnabled = config.GetOrDefault<bool>("MasternodeUseTor", true);
            string tumblerProtocolString = config.GetOrDefault<string>("MasternodeProtocol", TumblerProtocolType.Tcp.ToString());

            TumblerProtocolType tumblerProtocolType;
            this.TumblerProtocol = TumblerProtocolType.TryParse(tumblerProtocolString, out tumblerProtocolType)
                ? tumblerProtocolType
                : throw new ConfigurationException($"Incorrect tumbling prococol specified; the valid values are {TumblerProtocolType.Tcp} and {TumblerProtocolType.Http}");

            try
            {
                // Assume that if it parses it's valid
                string defaultAddress = this.TorEnabled ? null : NTumbleBit.Utils.GetInternetConnectedAddress().ToString();
                Ipv4Address = IPAddress.Parse(config.GetOrDefault<string>("MasternodeIPv4", defaultAddress));
            }
            catch (Exception)
            {
                Ipv4Address = null;
            }

            try
            {
                // Assume that if it parses it's valid
                Ipv6Address = IPAddress.Parse(config.GetOrDefault<string>("MasternodeIPv6", null));
            }
            catch (Exception)
            {
                Ipv6Address = null;
            }

            try
            {
                OnionAddress = config.GetOrDefault<string>("MasternodeOnion", null);
            }
            catch (Exception)
            {
                OnionAddress = null;
            }

            TumblerApiBaseUrl = config.GetOrDefault<string>("MasternodeTumblerUrl", null);
            TumblerRsaKeyFile = config.GetOrDefault<string>("MasternodeRsakeyfile", Path.Combine(nodeSettings.DataDir, "Tumbler.pem"));
            TxOutputValueSetting = new Money(config.GetOrDefault<int>("MasternodeRegtxoutputvalue", 1000), MoneyUnit.Satoshi);
            TxFeeValueSetting = new Money(config.GetOrDefault<int>("MasternodeRegtxfeevalue", 10000), MoneyUnit.Satoshi);
        }

        /// <summary>
        /// Checks the validity of the RPC settings or forces them to be valid.
        /// </summary>
        /// <param name="logger">Logger to use.</param>
        private void CheckConfigurationValidity(ILogger logger)
        {
            if ((this.TumblerPort < 0 || this.TumblerPort > 65535))
                throw new ConfigurationException("masternodeport is invalid");

            if (!IsRegTest && (this.TumblerProtocol != TumblerProtocolType.Tcp || !this.TorEnabled))
                throw new ConfigurationException("Options MasternodeProtocol and MasternodeUseTor can only be used in combination with -RegTest switch.");

            if (this.TorEnabled && this.TumblerProtocol == TumblerProtocolType.Http)
                throw new ConfigurationException("TumblerProtocol can only be changed to Http when Tor is disabled. Please use -NoTor switch to disable Tor.");

            if (!string.IsNullOrEmpty(OnionAddress) && OnionAddress.Length > 16)
            {
                throw new ConfigurationException($"Invalid Onion address {OnionAddress}. The address needs to be less than 16 characters.");
            }

            if (string.IsNullOrEmpty(this.TumblerWalletName))
            {
                throw new ConfigurationException("TumblerWalletName cannot be empty");
            }

            while (this.TumblerWalletPassword == null || this.TumblerWalletPassword.Length < 1)
            {
                Console.Write("Please enter password for the Masternode wallet: ");
                this.TumblerWalletPassword = GetPassword();
                Console.WriteLine();
            }

            TumblerRsaKeyFile = BreezeConfigurationValidator.ValidateTumblerRsaKeyFile(TumblerRsaKeyFile, TumblerRsaKeyFile);
        }

        /// <summary>
        /// Loads the masternode settings from the application configuration.
        /// </summary>
        /// <param name="nodeSettings">Application configuration.</param>
        public void Load(NodeSettings nodeSettings)
        {
            // Get values from config
            this.LoadSettingsFromConfig(nodeSettings);

            // Check validity of settings
            try
            {
                this.CheckConfigurationValidity(nodeSettings.Logger);
            }
            catch
            {
                Console.WriteLine("If you are facing issues with starting up the Masternode please compare your configuration with the recommended setings.");
                Console.WriteLine(DefaultConfiguration());
                throw;
            }
            
        }

        /// <summary> Prints the help information on how to configure the masternode settings to the logger.</summary>
        /// <param name="network">The network to use.</param>
        public static void PrintHelp(Network network)
        {
            var defaults = NodeSettings.Default();
            var builder = new StringBuilder();

            builder.AppendLine($"-MasternodePort=<0-65535>              Port on which the Masternode should be run");
            builder.AppendLine($"-ForceRegistration=<0 or 1>            Forces Masternode registration");
            builder.AppendLine($"-MasternodeCycle=<string>              Name of the tumbling cycle");
            builder.AppendLine($"-TumblerWalletName=<string>            Name of the wallet used by the masternode during tumbling");
            builder.AppendLine($"-TumblerWalletPassword=<string>        Password of the wallet used by the masternode during tumbling");
            builder.AppendLine($"-MasternodeFee=<decimal>               Fee charged by the Masternode per tumbling cycle");
            builder.AppendLine($"-TumblerEcdsaKeyAddress=<address>      Collateral address");
            builder.AppendLine($"-MasternodeIPv4=<IPv4>                 MasternodeIPv4");
            builder.AppendLine($"-MasternodeIPv6=<IPv6>                 MasternodeIPv6");
            builder.AppendLine($"-MasternodeOnion=<string>              MasternodeOnion");
            builder.AppendLine($"-MasternodeTumblerUrl=<string>         MasternodeTumblerUrl");
            builder.AppendLine($"-MasternodeRsakeyfile=<string>         MasternodeRsakeyfile");
            builder.AppendLine($"-MasternodeRegtxoutputvalue=<decimal>  MasternodeRegtxoutputvalue");
            builder.AppendLine($"-MasternodeRegtxfeevalue=<decimal>     MasternodeRegtxfeevalue");
            builder.AppendLine($"-MasternodeUseTor=<0 or 1>             Whether the TOR should be using for communication between Masternode and its clients (test only)");
            builder.AppendLine($"-MasternodeProtocol=<http or tcp>      Protocol which should be used to communicate between Masternode and its clients (test only)");

            defaults.Logger.LogInformation(builder.ToString());
        }

        public static string DefaultConfiguration()
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("####Masternode####");
            
            builder.AppendLine("#Port on which the Masternode should be run (default: 37123)");
            builder.AppendLine("MasternodePort=37123");
            builder.AppendLine("#Forces Masternode registration (default: 0)");
            builder.AppendLine("#ForceRegistration=0");
            builder.AppendLine("#Name of the tumbling cycle (default kotori)");
            builder.AppendLine("#MasternodeCycle=kotori");
            builder.AppendLine("#Name of the wallet used by the masternode during tumbling (default: Masternode)");
            builder.AppendLine("TumblerWalletName=Masternode");
            builder.AppendLine("#Password of the wallet used by the masternode during tumbling");
            builder.AppendLine("TumblerWalletPassword=<please make this a secure password>");
            builder.AppendLine($"#Fee charged by the Masternode per tumbling cycle (default: {DefaultMasternodeFee})");
            builder.AppendLine($"MasternodeFee={DefaultMasternodeFee}");
            builder.AppendLine("#Collateral address");
            builder.AppendLine("TumblerEcdsaKeyAddress=");
            builder.AppendLine("#Masternode IPv4 address used during registration");
            builder.AppendLine("#MasternodeIPv4=");
            builder.AppendLine("#Masternode IPv6 address used during registration");
            builder.AppendLine("#MasternodeIPv6=");
            builder.AppendLine("#Masternode Onion address");
            builder.AppendLine("#MasternodeOnion=");
            builder.AppendLine("#Masternode Tumbler Url address");
            builder.AppendLine("#MasternodeTumblerUrl=");
            builder.AppendLine("#Masternode Rsakey file (default: set automatically)");
            builder.AppendLine("#MasternodeRsakeyfile=");
            builder.AppendLine("#Masternode Reg tx output value");
            builder.AppendLine("#MasternodeRegtxoutputvalue=");
            builder.AppendLine("#Masternode Reg tx fee value");
            builder.AppendLine("#MasternodeRegtxfeevalue=");

            builder.AppendLine("#Whether the TOR should be using for communication between Masternode and its clients (test only) (default: 1)");
            builder.AppendLine("#MasternodeUseTor=1");
            builder.AppendLine("#Protocol which should be used to communicate between Masternode and its clients (test only) (default: TCP)");
            builder.AppendLine("#MasternodeProtocol=TCP");

            return builder.ToString();
        }

        public SecureString GetPassword()
        {
            SecureString password = new SecureString();
            ConsoleKeyInfo consoleKeyInfo;

            while ((consoleKeyInfo = Console.ReadKey(true)).Key != ConsoleKey.Enter)
            {
                if (consoleKeyInfo.Key == ConsoleKey.Backspace)
                {
                    if (password.Length > 0)
                    {
                        password.RemoveAt(password.Length - 1);
                        Console.Write("\b \b");
                    }
                }
                else if (consoleKeyInfo.KeyChar != '\u0000') // KeyChar == '\u0000' if the key pressed does not correspond to a printable character, e.g. F1, Pause-Break, etc
                {
                    password.AppendChar(consoleKeyInfo.KeyChar);
                    Console.Write("*");
                }
            }
            return password;
        }
    }
}
