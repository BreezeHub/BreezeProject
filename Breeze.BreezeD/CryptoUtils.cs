using System;
using System.IO;
using System.Security;
using System.Security.Cryptography;
using System.Text;

using NBitcoin;
using NTumbleBit;

namespace Breeze.BreezeD
{
    public class CryptoUtils
    {
        NTumbleBit.RsaKey TumblerRsaKey;
        BitcoinSecret EcdsaKey;

        public CryptoUtils(string rsaKeyPath, BitcoinSecret privateKeyEcdsa)
        {
			TumblerRsaKey = new NTumbleBit.RsaKey(File.ReadAllBytes(rsaKeyPath));
            EcdsaKey = privateKeyEcdsa;
        }

        public byte[] SignDataRSA(byte[] message)
        {
            byte[] signedBytes;
            NBitcoin.uint160 temp1;

            signedBytes = TumblerRsaKey.Sign(message, out temp1);

            return signedBytes;
        }

        public byte[] SignDataECDSA(byte[] message)
        {
            var signature = EcdsaKey.PrivateKey.SignMessage(message);
            var signedBytes = Encoding.ASCII.GetBytes(signature);
            
            return signedBytes;
        }
    }
}