using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
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

        /// <summary>Masternode port</summary>
        public int MasternodePort { get; private set; }

        /// <summary>Name of the tumbling cycle</summary>
        public string CycleType { get; private set; }

        /// <summary>Whether the TOR should be using for communication between Masternode and its clients</summary>
        public bool TorEnabled { get; private set; }

        /// <summary>Protocol whioch should be used to communicate between Masternode and its clients</summary>
        public TumblerProtocolType TumblerProtocol { get; private set; }

        /// <summary>Fee charged by the Masternode per tumbling cycle</summary>
        public decimal TumblerFee { get; private set; }

        /// <summary>Forces the registration of the Masternode</summary>
        public bool ForceRegistration { get; private set; }

        /// <summary>
        /// Initializes an instance of the object.
        /// </summary>
        public MasternodeSettings(bool forceRegistration)
        {
            this.ForceRegistration = forceRegistration;
        }

        /// <summary>
        /// Loads the masternode settings from the application configuration.
        /// </summary>
        /// <param name="nodeSettings">Application configuration.</param>
        private void LoadSettingsFromConfig(NodeSettings nodeSettings)
        {
            var config = nodeSettings.ConfigReader;
            this.Network = nodeSettings.Network;

            this.MasternodePort = config.GetOrDefault<int>("MasternodePort", TumblerConfiguration.DefaultTumblerPort);
            this.CycleType = config.GetOrDefault<string>("MasternodeCycle", "Kotori");
            this.TumblerFee = config.GetOrDefault<decimal>("MasternodeFee", 0.00075m);
            this.TorEnabled = config.GetOrDefault<bool>("MasternodeUseTor", true);
            string tumblerProtocolString = config.GetOrDefault<string>("MasternodeProtocol", TumblerProtocolType.Tcp.ToString());

            TumblerProtocolType tumblerProtocolType;
            this.TumblerProtocol = TumblerProtocolType.TryParse(tumblerProtocolString, out tumblerProtocolType)
                ? tumblerProtocolType
                : throw new ConfigurationException($"Incorrect tumbling prococol specified; the valid values are {TumblerProtocolType.Tcp} and {TumblerProtocolType.Http}");
        }

        /// <summary>
        /// Checks the validity of the RPC settings or forces them to be valid.
        /// </summary>
        /// <param name="logger">Logger to use.</param>
        private void CheckConfigurationValidity(ILogger logger)
        {
            if ((this.MasternodePort < 0 || this.MasternodePort > 65535))
                throw new ConfigurationException("masternodeport is invalid");

            bool isRegTest = Network == Network.RegTest || Network == Network.StratisRegTest;
            if (!isRegTest && (this.TumblerProtocol != TumblerProtocolType.Tcp || !this.TorEnabled))
                throw new ConfigurationException("Options MasternodeProtocol and MasternodeUseTor can only be used in combination with -RegTest switch.");

            if (this.TorEnabled && this.TumblerProtocol == TumblerProtocolType.Http)
                throw new ConfigurationException("TumblerProtocol can only be changed to Http when Tor is disabled. Please use -NoTor switch to disable Tor.");
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
            this.CheckConfigurationValidity(nodeSettings.Logger);
        }

        /// <summary> Prints the help information on how to configure the masternode settings to the logger.</summary>
        /// <param name="network">The network to use.</param>
        public static void PrintHelp(Network network)
        {
            var defaults = NodeSettings.Default();
            var builder = new StringBuilder();

            builder.AppendLine($"-MasternodePort=<0-65535>          Masternode port ");
            builder.AppendLine($"-MasternodeCycle=<string>          Name of the tumbling cycle");
            builder.AppendLine($"-MasternodeFee=<decimal>           Fee charged by the Masternode per tumbling cycle");
            builder.AppendLine($"-MasternodeUseTor=<0 or 1>         Whether the TOR should be using for communication between Masternode and its clients (test only)");
            builder.AppendLine($"-MasternodeProtocol=<http or tcp>  Protocol which should be used to communicate between Masternode and its clients (test only)");

            defaults.Logger.LogInformation(builder.ToString());
        }
    }
}
