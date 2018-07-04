using System;
using System.Text;
using System.Linq;
using System.IO;
using NBitcoin;
using Microsoft.Extensions.Logging;
using NTumbleBit.Logging;
using NTumbleBit.Configuration;
using NTumbleBit.ClassicTumbler.Client;
using NTumbleBit.ClassicTumbler.Client.ConnectionSettings;
using NTumbleBit.Services;
using NTumbleBit.ClassicTumbler;
using System.Net;

namespace Breeze.TumbleBit.Client
{
    public class FullNodeTumblerClientConfiguration : TumblerClientConfigurationBase
    {
        public static readonly string TumbleBitFolderName = "TumbleBit";
        private TumblingState tumblingState;
        public FullNodeTumblerClientConfiguration(TumblingState tumblingState, bool onlyMonitor, bool connectionTest = false, bool useProxy = true)
        {
            this.tumblingState = tumblingState ?? throw new ArgumentNullException(nameof(tumblingState));
            Network = tumblingState.TumblerNetwork ?? throw new ArgumentNullException(nameof(tumblingState.TumblerNetwork));

            Logs.LogDir = this.tumblingState.NodeSettings.DataDir;

            if (!onlyMonitor || connectionTest)
            {
                TorPath = "tor";

                Cooperative = true;
                AllowInsecure = true;

                if (tumblingState.TumblerUri != null)
                {
                    TumblerServer = new TumblerUrlBuilder(this.tumblingState.TumblerUri);
                    if (TumblerServer == null) throw new ConfigException("Tumbler server is not configured");
                }

                if (useProxy)
                {
                    AliceConnectionSettings = new SocksConnectionSettings()
                    {
                        Proxy = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 9050)
                    };

                    BobConnectionSettings = new SocksConnectionSettings()
                    {
                        Proxy = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 9050)
                    };
                }
                else
                {
                    // This mode is only for unit/integration tests, as it allows testing with latency introduced by Tor
                    AliceConnectionSettings = new ConnectionSettingsBase();
                    BobConnectionSettings = new ConnectionSettingsBase();                    
                }
                
                if (connectionTest)
                {
                    return;
                }
            }

            OnlyMonitor = onlyMonitor;
            Logs.Configuration.LogInformation("Network: " + Network);
            DataDir = GetTumbleBitDataDir(this.tumblingState.NodeSettings.DataDir);
            Logs.Configuration.LogInformation("Data directory set to " + DataDir);

            DBreezeRepository = new DBreezeRepository(Path.Combine(DataDir, "db2"));
            Tracker = new Tracker(DBreezeRepository, Network);

            // Need to use our own ExternalServices implementations to remove RPC dependency
            Services = ExternalServices.CreateFromFullNode(DBreezeRepository, Tracker, this.tumblingState);
        }

        public static string GetTumbleBitDataDir(string dataDir)
        {
            string tumbleBitDataDir = Path.Combine(dataDir, TumbleBitFolderName);
            if (!Directory.Exists(tumbleBitDataDir)) Directory.CreateDirectory(tumbleBitDataDir);
            return tumbleBitDataDir;
        }
    }
}