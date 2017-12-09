using System;
using System.IO;
using System.Net;
using System.Text;

using Xunit;

using BreezeCommon;
using NBitcoin;
using NTumbleBit;

namespace BreezeCommon.Tests
{
	public class RegistrationTokenTests
	{
		[Fact]
		public void CanValidateRegistrationToken()
		{
			var rsa = new RsaKey();
			var ecdsa = new Key().GetBitcoinSecret(Network.StratisMain);

			var serverAddress = ecdsa.GetAddress().ToString();
			
			var token = new RegistrationToken(255,
				serverAddress,
				IPAddress.Parse("127.0.0.1"),
				IPAddress.Parse("2001:0db8:85a3:0000:0000:8a2e:0370:7334"),
				"0123456789ABCDEF",
				"",
				37123,
				ecdsa.PubKey);

			var cryptoUtils = new CryptoUtils(rsa, ecdsa);
			token.RsaSignature = cryptoUtils.SignDataRSA(token.GetHeaderBytes().ToArray());
			token.EcdsaSignature = cryptoUtils.SignDataECDSA(token.GetHeaderBytes().ToArray());

			Assert.True(token.Validate(Network.StratisMain));
		}
	}
}