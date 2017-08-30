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
        public FullNodeTumblerClientConfiguration(TumblingState tumblingState)
        {
            this.tumblingState = tumblingState;
        }

        public FullNodeTumblerClientConfiguration LoadArgs(String[] args)
        {
            Network = this.tumblingState.TumblerNetwork;

            // TODO: Does this change for the actual Breeze wallet
            DataDir = DefaultDataDirectory.GetDefaultDirectory("StratisNode", Network);

            Logs.Configuration.LogInformation("Network: " + Network);
            Logs.Configuration.LogInformation("Data directory set to " + DataDir);

            if (!Directory.Exists(DataDir))
                throw new ConfigurationException("Data directory does not exist");

            // TODO: May want option to run client in monitoring mode to recover locked up coins
            OnlyMonitor = false;
            Cooperative = true;

            TumblerServer = new TumblerUrlBuilder(this.tumblingState.TumblerUri);
            TorPath = "tor";

            if (!OnlyMonitor && TumblerServer == null)
                throw new ConfigException("tumbler.server not configured");

            // Move this to Breeze TB client 'tumble' method
            /*
            try
            {
                var accounts = this.tumblingState.DestinationWallet.GetAccountsByCoinType(this.tumblingState.coinType);
                // TODO: Possibly need to preserve destination account name in tumbling state. Default to first account for now
                string accountName = null;
                foreach (var account in accounts)
                {
                    if (account.Index == 0)
                        accountName = account.Name;
                }
                var destAccount = this.tumblingState.DestinationWallet.GetAccountByCoinType(accountName, this.tumblingState.coinType);

                var key = destAccount.ExtendedPubKey;
                if (key != null)
                    OutputWallet.RootKey = new BitcoinExtPubKey(key, Network);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                throw new ConfigException("outputwallet.extpubkey is not configured correctly");
            }
            */

            OutputWallet.KeyPath = new KeyPath("0");

            AliceConnectionSettings = new SocksConnectionSettings()
            {
                Proxy = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 9050)
            };

            // TODO: Need to check what recommended configuration is to prevent Alice/Bob linkage
            BobConnectionSettings = new SocksConnectionSettings()
            {
                Proxy = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 9050)
            };

            AllowInsecure = true;

            DBreezeRepository = new DBreezeRepository(Path.Combine(DataDir, "db2"));
            Tracker = new Tracker(DBreezeRepository, Network);

            // Need to use our own ExternalServices implementations to remove RPC dependency
            Services = Breeze.TumbleBit.Client.ExternalServices.CreateUsingFullNode(DBreezeRepository, Tracker, this.tumblingState);

            //if (OutputWallet.RootKey != null && OutputWallet.KeyPath != null)
            //    DestinationWallet = new ClientDestinationWallet(OutputWallet.RootKey, OutputWallet.KeyPath, DBreezeRepository, Network);
            //else
            //    throw new ConfigException("Missing configuration for outputwallet");

            return this;
        }
    }
}