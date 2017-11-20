using System.IO;
using NTumbleBit;

namespace Breeze.BreezeServer
{
    /// <summary>
    /// Validates or generates Tumbler.pem key file for signing registration transactions
    /// </summary>
    internal static class TumblerRsaKey
    {
        public static bool Exists(string tumblerRsaKeyFullPath)
        {
            return File.Exists(tumblerRsaKeyFullPath);
        }

        public static void Create(string tumblerRsaKeyFullPath)
        {
            File.WriteAllBytes(tumblerRsaKeyFullPath, new RsaKey().ToBytes());
        }
    }
}
