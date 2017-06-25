using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Xunit;

using NBitcoin;
using NBitcoin.DataEncoders;

using Breeze.BreezeD;

namespace Breeze.BreezeD.Tests
{
    public class AddressEncodingTests
    {
        private BreezeRegistration breezeReg;

        public AddressEncodingTests()
        {
            breezeReg = new BreezeRegistration();
        }

        [Fact]
        public void BytesToAddresses_ShortMessage()
        {            
            // a - mpMqqfKnF9M2rwk9Ai4RymBqADx6TssFuM <- correct version on testnet
            //     mpMqqfKnF9M2rwk9Ai4RymBqADx6TUnBkb <- incorrect version with 00000000 checksum bytes

            var inputMessage = "a"; 
            var inputMessageBytes = Encoding.ASCII.GetBytes(inputMessage);
            var output = breezeReg.BytesToAddresses(Network.TestNet, inputMessageBytes);
            List<BitcoinAddress> expectedOutput = new List<BitcoinAddress>();
            expectedOutput.Add(BitcoinAddress.Create("mpMqqfKnF9M2rwk9Ai4RymBqADx6TssFuM"));

            /* Worked example
            // 0x00 - Mainnet
            // 0x6F - Testnet
            // 0x?? - Stratis mainnet

            // Literal 'a' is 0x61 hex

            var keyBytes = new byte[] {0x6F, 0x61, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00};
            var algorithm = SHA256.Create();
            var hash = algorithm.ComputeHash(algorithm.ComputeHash(keyBytes));

            First 4 bytes of double SHA256: 15, 146, 165, 196
            Need to concatenate them to keyBytes

            var keyBytes2 = new byte[] {0x6F,
                0x61, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                15, 146, 165, 196};

            var finalEncodedAddress = Encoders.Base58.EncodeData(keyBytes2);

            Result should be "mpMqqfKnF9M2rwk9Ai4RymBqADx6TssFuM"
            */
            
            Assert.Equal(expectedOutput, output);
        }

        [Fact]
        public void AddressesToBytes_ShortMessage()
        {
            var expectedBytes = new byte[] {
                0x61, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            };

            List<BitcoinAddress> inputAddresses = new List<BitcoinAddress>();
            inputAddresses.Add(BitcoinAddress.Create("mpMqqfKnF9M2rwk9Ai4RymBqADx6TssFuM"));

            var output = breezeReg.AddressesToBytes(inputAddresses);

            Assert.Equal(expectedBytes, output);
        }
    }
}
