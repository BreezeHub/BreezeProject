using System;

namespace Breeze.BreezeServer
{
    internal static class BreezeConfigurationValidator
    {
        public static string ValidateTumblerRsaKeyFile(string tumblerRsaKeyFile, string defaultFile)
        {
            if (tumblerRsaKeyFile != "" && !TumblerRsaKey.Exists(tumblerRsaKeyFile))
            {
                Console.WriteLine("RSA private key not found at the configured filepath");
                return tumblerRsaKeyFile;
            }
            else if (tumblerRsaKeyFile == "")
            {
                if (!TumblerRsaKey.Exists(defaultFile))
                {
                    Console.WriteLine("Generating new RSA key...");
                    TumblerRsaKey.Create(defaultFile);
                    Console.WriteLine("RSA key saved (" + defaultFile + ")");
                    return defaultFile;
                }
                else
                {
                    Console.WriteLine("RSA private key found (" + defaultFile + ")");
                    return defaultFile;
                }
            }
            else
            {
                Console.WriteLine("RSA private key found (" + tumblerRsaKeyFile + ")");
                return tumblerRsaKeyFile;
            }
        }
       
    }
}
