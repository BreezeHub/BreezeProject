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
        private TumblingState tumblingState;
        public FullNodeTumblerClientConfiguration(TumblingState tumblingState, bool onlyMonitor, bool connectionTest = false)
        {
            this.tumblingState = tumblingState ?? throw new ArgumentNullException(nameof(tumblingState));
            Network = tumblingState.TumblerNetwork ?? throw new ArgumentNullException(nameof(tumblingState.TumblerNetwork));

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

                AliceConnectionSettings = new SocksConnectionSettings()
                {
                    Proxy = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 9050)
                };

                // TODO: Need to check what recommended configuration is to prevent Alice/Bob linkage
                BobConnectionSettings = new SocksConnectionSettings()
                {
                    Proxy = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 9050)
                };

                if (connectionTest)
                {
                    return;
                }
            }

            OnlyMonitor = onlyMonitor;
            Logs.Configuration.LogInformation("Network: " + Network);
            DataDir = Path.Combine(this.tumblingState.NodeSettings.DataDir, "TumbleBit");
            Logs.Configuration.LogInformation("Data directory set to " + DataDir);

            DBreezeRepository = new DBreezeRepository(Path.Combine(DataDir, "db2"));
            Tracker = new Tracker(DBreezeRepository, Network);

            // Need to use our own ExternalServices implementations to remove RPC dependency
            Services = ExternalServices.CreateFromFullNode(DBreezeRepository, Tracker, this.tumblingState);
        }
    }
}