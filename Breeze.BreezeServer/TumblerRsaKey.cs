using System.IO;
using NTumbleBit;

namespace Breeze.BreezeServer
{
    /// <summary>
    /// Validates or generates Tumbler.pem key file for signing registration transactions
    /// </summary>
    static class TumblerRsaKey
    {
        public static bool Exists(string tumblerRsaKeyPath)
        {
            return File.Exists(tumblerRsaKeyPath);
        }

        public static void Create(string tumblerRsaKeyPath)
        {
            File.WriteAllBytes(tumblerRsaKeyPath, new RsaKey().ToBytes());
        }
    }
}
