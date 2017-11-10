using System;
using System.IO;
using System.Security;
using System.Security.Cryptography;
using System.Text;

using NBitcoin;
using NTumbleBit;

namespace BreezeCommon
{
	public class CryptoUtils
	{
		NTumbleBit.RsaKey TumblerRsaKey;
		BitcoinSecret EcdsaKey;

		public CryptoUtils(RsaKey rsaKey, BitcoinSecret privateKeyEcdsa)
		{
			TumblerRsaKey = rsaKey;
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