using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using NBitcoin;
using NBitcoin.DataEncoders;

namespace BreezeCommon
{
    public class BlockChainDataConversions
    {
		public static List<BitcoinAddress> BytesToAddresses(Network network, byte[] dataToEncode)
		{
			/* Converts a byte array into a list of Stratis or Bitcoin addresses.
            Each address can store 20 bytes of arbitrary data. The first byte of
            a (typically) 25 byte address is the network byte and is therefore
            unusable. The last 4 bytes are the checksum and are similarly
            unusable. */

			List<BitcoinAddress> addressList = new List<BitcoinAddress>();

			if (dataToEncode.Length == 0)
			{
				return addressList;
			}

			int startPos = 0;
			string base58address;

			while (true)
			{
				IEnumerable<byte> subSet = dataToEncode.Skip(startPos).Take(20);

				if (subSet.Count() == 20)
				{
					IEnumerable<byte> augmentedSubSet = subSet;

					KeyId key = new KeyId(augmentedSubSet.ToArray());
					BitcoinPubKeyAddress address = new BitcoinPubKeyAddress(key, network);
					base58address = address.ToString();
					addressList.Add(BitcoinAddress.Create(base58address));
				}
				else
				{
					// Fill up remainder of 20 bytes with zeroes
					var arr = new byte[20 - subSet.Count()];
					for (int i = 0; i < arr.Length; i++)
					{
						arr[i] = 0x00;
					}

					IEnumerable<byte> tempSuffix = arr;
					IEnumerable<byte> augmentedSubSet = subSet.Concat(tempSuffix);

					KeyId key = new KeyId(augmentedSubSet.ToArray());
					BitcoinPubKeyAddress address = new BitcoinPubKeyAddress(key, network);
					base58address = address.ToString();
					addressList.Add(BitcoinAddress.Create(base58address));

					break;
				}

				startPos += 20;
			}

			return addressList;
		}

		public static byte[] AddressesToBytes(List<BitcoinAddress> addresses)
		{
			/* Convert a list of Bitcoin or Stratis addresses back into
             a byte array. Note that no assumptions are made about
             padding schemes, and the data storage protocol therefore
             needs to be robust enough to handle trailing empty or
             garbage bytes.
             */

			var decoded = new MemoryStream();
			byte[] temp;

			foreach (var addr in addresses)
			{
				temp = Encoders.Base58.DecodeData(addr.ToString());

				// Use an offset to trim off the 1 network byte and 4 checksum bytes
				if (temp.Length < 21)
				{
					throw new Exception("Decoded address too short, data corrupted?");
				}
				decoded.Write(temp, 1, 20);
			}

			return decoded.ToArray();
		}

		public static byte[] AddressToBytes(BitcoinAddress address)
		{
			/* Convert a Bitcoin or Stratis address back into
             a byte array. Note that no assumptions are made about
             padding schemes, and the data storage protocol therefore
             needs to be robust enough to handle trailing empty or
             garbage bytes.
             */

			var decoded = new MemoryStream();
			byte[] temp;

			temp = Encoders.Base58.DecodeData(address.ToString());

			// Use an offset to trim off the 1 network byte and 4 checksum bytes
			if (temp.Length < 21)
			{
				throw new Exception("Decoded address too short, data corrupted?");
			}
			decoded.Write(temp, 1, 20);

			return decoded.ToArray();
		}

	}
}