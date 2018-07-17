using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using BreezeCommon;
using NBitcoin;
using Newtonsoft.Json;
using NTumbleBit;
using NTumbleBit.ClassicTumbler;
using NTumbleBit.ClassicTumbler.Client;
using NTumbleBit.ClassicTumbler.Client.ConnectionSettings;
using NTumbleBit.Tor;

namespace Breeze.RegistrationTester
{
    class Program
    {
        static void Main(string[] args)
        {
			// args[0] = registration transaction hex

	        Console.WriteLine();
			Console.WriteLine("*************************************************************");
			Console.WriteLine("Decoding registration transaction HEX");
	        Console.WriteLine("*************************************************************");
	        Console.WriteLine();
	        Console.WriteLine("Transaction HEX");
	        Console.WriteLine("*************************************************************");
			Console.WriteLine(args[0]);
	        Console.WriteLine();

			Transaction tx;

			//Set the Timestamp flag to true in order to be compatibile with Stratis transactions
			try
	        {
		        Transaction.TimeStamp = true;
		        tx = Transaction.Parse(args[0]);
	        }
	        catch (Exception ex)
	        {
		        Console.WriteLine("Cannot decode a given input into Stratis network transaction.");
		        return;
	        }

	        Console.WriteLine("Decoded registration transaction");
	        Console.WriteLine("*************************************************************");
			try
	        {
		        var registrationToken = new RegistrationToken();
		        registrationToken.ParseTransaction(tx, Network.StratisMain);

				Console.WriteLine(JsonConvert.SerializeObject(registrationToken, Formatting.Indented));
			}
	        catch (Exception ex)
	        {
		        Console.WriteLine("Given transaction HEX is not a valid registration token transaction");
			}

	        Console.ReadKey();
        }
	}
}
