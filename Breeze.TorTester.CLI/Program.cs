using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using NBitcoin;
using NTumbleBit;
using NTumbleBit.ClassicTumbler;
using NTumbleBit.ClassicTumbler.Client;
using NTumbleBit.ClassicTumbler.Client.ConnectionSettings;
using NTumbleBit.Tor;

namespace Breeze.TorTester.CLI
{
    class Program
    {
        static void Main(string[] args)
        {
            // args[0] = server CTB URL
            // args[1] = cycle number (not too important)
            // args[2] = cookie file path for Tor
            
            var serverAddress = new TumblerUrlBuilder(args[0]);
            var cycle = int.Parse(args[1]);
            var client = new TumblerClient(Network.Main, serverAddress, cycle);

            var cookieFile = new FileInfo(args[2]);

            IPEndPoint endpoint;
            
            using (var tor = new TorClient("127.0.0.1", 9051, cookieFile))
            {
                try
                {
                    tor.ConnectAsync().GetAwaiter().GetResult();
                }
                catch
                {
                    Console.WriteLine("Error in connect");
                }

                if(!tor.AuthenticateAsync().GetAwaiter().GetResult())
                    Console.WriteLine("Error in authenticate");
                
                var endpoints = tor.GetSocksListenersAsync().GetAwaiter().GetResult();
                endpoint = endpoints.FirstOrDefault();
                if (endpoint == null)
                    throw new TorException("Tor has no socks listener", "");
            }
            
            var settings = new SocksConnectionSettings();
            settings.Proxy = endpoint;
            var handler = settings.CreateHttpHandler();
            if (handler != null)
                client.SetHttpHandler(handler);
            else
            {
                Console.WriteLine("Handler is null");
            }

            Console.WriteLine(DateTime.Now + " Starting tor connectivity test...");
            
            while (true)
            {
                Stopwatch stopwatch = Stopwatch.StartNew();
                try
                {
                    var parameters = client.GetTumblerParameters();
                    stopwatch.Stop();
                    Console.WriteLine(DateTime.Now + " Received parameter response in " + stopwatch.ElapsedMilliseconds + "ms");
                }
                catch
                {
                    stopwatch.Stop();
                    Console.WriteLine(DateTime.Now + " Timed out after " + stopwatch.ElapsedMilliseconds + "ms");
                }
            }
        }
    }
}
