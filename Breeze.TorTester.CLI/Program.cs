using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using NBitcoin;
using Newtonsoft.Json;
using NTumbleBit;
using NTumbleBit.ClassicTumbler;
using NTumbleBit.ClassicTumbler.Client;
using NTumbleBit.ClassicTumbler.Client.ConnectionSettings;
using NTumbleBit.ClassicTumbler.Server;
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
            // args[3] & args[4] = -noTor and/or -tumblerProtocol=http |-tumblerProtocol=tcp
            var useTor = !args.Contains("-noTor");
            TumblerProtocolType? tumblerProtocol = null;
            try
            {
                string tumblerProtocolString = args.Where(a => a.StartsWith("-tumblerProtocol=")).Select(a => a.Substring("-tumblerProtocol=".Length).Replace("\"", "")).FirstOrDefault();
                if (tumblerProtocolString != null)
                {
                    tumblerProtocol = Enum.Parse<TumblerProtocolType>(tumblerProtocolString, true);
                }

                if (useTor && tumblerProtocol.HasValue && tumblerProtocol.Value == TumblerProtocolType.Http)
                {
                    Console.WriteLine("TumblerProtocol can only be changed to Http when Tor is disabled. Please use -NoTor switch to disable Tor.");
                    return;
                }
            }
            catch
            {
                Console.WriteLine($"Incorrect tumbling prococol specified; the valid values are {TumblerProtocolType.Tcp} and {TumblerProtocolType.Http}");
                return;
            }

            var serverAddress = new TumblerUrlBuilder(args[0]);
            var cycle = int.Parse(args[1]);
            var client = new TumblerClient(Network.Main, serverAddress, cycle);

            var cookieFile = new FileInfo(args[2]);

            IPEndPoint endpoint;

            var settings = new SocksConnectionSettings();
            if (useTor)
            {
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

                    if (!tor.AuthenticateAsync().GetAwaiter().GetResult())
                        Console.WriteLine("Error in authenticate");

                    var endpoints = tor.GetSocksListenersAsync().GetAwaiter().GetResult();
                    endpoint = endpoints.FirstOrDefault();
                    if (endpoint == null)
                        throw new TorException("Tor has no socks listener", "");
                }
                settings.Proxy = endpoint;
            }

            var handler = settings.CreateHttpHandler(tumblerProtocol ?? TumblerProtocolType.Tcp);
            if (handler != null)
                client.SetHttpHandler(handler);
            else
            {
                Console.WriteLine("Handler is null");
            }

            Console.WriteLine(DateTime.Now + " Obtaining MasterNode parameters");
            try
            {
                var parameters = client.GetTumblerParameters();
                Console.WriteLine("Core settings");
                Console.WriteLine($"Network: {parameters.Network}");
                Console.WriteLine($"ExpectedAddress: {parameters.ExpectedAddress}");
                Console.WriteLine($"Denomination: {parameters.Denomination}");
                Console.WriteLine($"Fee: {parameters.Fee}");
                Console.WriteLine();

                Console.WriteLine("Standard Server Check");
                Console.WriteLine($" - IsStandard (MasterNode value =? ExpectedValue): {parameters.IsStandard()}");
                Console.WriteLine($"   - Version ({parameters.Version} =? {ClassicTumblerParameters.LAST_VERSION}): {parameters.Version == ClassicTumblerParameters.LAST_VERSION}");
                Console.WriteLine($"   - VoucherKey: {parameters.VoucherKey.CheckKey()}");
                Console.WriteLine($"   - ServerKey: {parameters.ServerKey.CheckKey()}");
                Console.WriteLine($"   - FakePuzzleCount ({parameters.FakePuzzleCount} =? {285}): {parameters.FakePuzzleCount == 285}");
                Console.WriteLine($"   - RealPuzzleCount ({parameters.RealPuzzleCount} =? {15}): {parameters.RealPuzzleCount == 15}");
                Console.WriteLine($"   - RealTransactionCount ({parameters.RealTransactionCount} =? {42}): {parameters.RealTransactionCount == 42}");
                Console.WriteLine($"   - FakeTransactionCount ({parameters.FakeTransactionCount} =? {42}): {parameters.FakeTransactionCount == 42}");
                Console.WriteLine($"   - Denomination ({parameters.Denomination} =? {new Money(0.1m, MoneyUnit.BTC)}): {parameters.Denomination == new Money(0.1m, MoneyUnit.BTC)}");
                Console.WriteLine($"   - Fee ({parameters.Fee} =? {new Money(0.00075m, MoneyUnit.BTC)}): {parameters.Fee == new Money(0.00075m, MoneyUnit.BTC)}");
                Console.WriteLine($"   - FakeFormat ({parameters.FakeFormat} =? {new uint256(Enumerable.Range(0, 32).Select(o => o == 0 ? (byte)0 : (byte)1).ToArray())}): {parameters.FakeFormat == new uint256(Enumerable.Range(0, 32).Select(o => o == 0 ? (byte)0 : (byte)1).ToArray())}");
                Console.WriteLine();

                Console.WriteLine($"Cycles");
                Console.WriteLine($" - CycleGenerator: {parameters.CycleGenerator.FirstCycle.ToString()}");
                Console.WriteLine($" - RegistrationOverlap: {parameters.CycleGenerator.RegistrationOverlap}");
                Console.WriteLine();

                Console.WriteLine("Cryptography");
                Console.WriteLine($" - FakeFormat: {parameters.FakeFormat}");
                Console.WriteLine($" - FakeTransactionCount: {parameters.FakeTransactionCount}");
                Console.WriteLine($" - RealPuzzleCount: {parameters.RealPuzzleCount}");
                Console.WriteLine($" - FakePuzzleCount: {parameters.FakePuzzleCount}");
                Console.WriteLine($" - RealTransactionCount: {parameters.RealTransactionCount}");
                Console.WriteLine($" - Version: {parameters.Version}");
                Console.WriteLine($" - FakeFormat: {parameters.Version}");
                Console.WriteLine();
            }
            catch (Exception ex) { }

            Console.WriteLine();

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
                Thread.Sleep(1000);
            }
        }
    }
}
