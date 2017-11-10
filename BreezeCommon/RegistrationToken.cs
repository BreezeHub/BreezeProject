using System;
using System.Collections.Generic;
using System.Net;
using System.Security.Cryptography;
using System.Text;

using NBitcoin;
using Newtonsoft.Json;
using NTumbleBit;

namespace BreezeCommon
{
	/*
        Bitstream format for Breeze TumbleBit registration token

        A registration token, once submitted to the network, remains valid indefinitely until invalidated.

        The registration token for a Breeze TumbleBit server can be invalidated by the sending of a subsequent
        token transaction at a greater block height than the original. It is the responsibility of the Breeze
        client software to scan the blockchain for the most current server registrations prior to initiating
        contact with any server.

        The registration token consists of a single transaction broadcast on the network of choice (e.g. Bitcoin's
        mainnet/testnet, or the Stratis mainnet/testnet). This transaction has any number of funding inputs, as normal.
        It has precisely one nulldata output marking the entire transaction as a Breeze TumbleBit registration.
        There can be an optional change return output, which if present MUST be at the end of the entire output list.
        
        The remainder of the transaction outputs are of near-dust value. Each output encodes 64 bytes of token data
        into a public key script. The contents and format of the encoded data is described below.

        The presumption is that the transaction outputs are not reordered by the broadcasting node.

        - OP_RETURN transaction output
        -> 26 bytes - Literal string: BREEZE_REGISTRATION_MARKER

        - Encoded public key transaction outputs
        -> 1 byte - Protocol version byte (255 = test registration to be ignored by mainnet wallets)
        -> 2 bytes - Length of registration header
        -> 34 bytes - Server ID of the tumbler (base58 representation of the collateral address, right padded with spaces)
        -> 4 bytes - IPV4 address of tumbler server; 00000000 indicates non-IPV4
        -> 16 bytes - IPV6 address of tumbler server; 00000000000000000000000000000000 indicates non-IPV6
        -> 16 bytes - Onion (Tor) address of tumbler server; 00000000000000000000000000000000 indicates non-Tor
        -> 2 bytes - IPV4/IPV6/Onion TCP port of server
        -> 2 bytes - RSA signature length
        -> n bytes - RSA signature proving ownership of the Breeze TumbleBit server's private key (to prevent spoofing)
        -> 2 bytes - ECDSA signature length
        -> n bytes - ECDSA signature proving ownership of the Breeze TumbleBit server's private key
        -> 40 bytes - Hash of the tumbler server's configuration file
        <...>
        -> Protocol does not preclude additional data being appended in future without breaking compatibility

        On connection with the Breeze TumbleBit server by a client, the public key of the server will be verified
        by the client to ensure that the server is authentic and in possession of the registered keys. The
        TumbleBit protocol is then followed as normal.
    */

	public class RegistrationToken
	{
		public int ProtocolVersion { get; set; }

        public string ServerId { get; set; }

        [JsonConverter(typeof(IPAddressConverter))]
		public IPAddress Ipv4Addr { get; set; }

        [JsonConverter(typeof(IPAddressConverter))]
		public IPAddress Ipv6Addr { get; set; }

		public string OnionAddress { get; set; }
		public int Port { get; set; }

		public byte[] RsaSignature { get; set; }
		public byte[] EcdsaSignature { get; set; }

        public string ConfigurationHash { get; set; }

        public RegistrationToken(int protocolVersion, string serverId, IPAddress ipv4Addr, IPAddress ipv6Addr, string onionAddress, string configurationHash, int port)
		{
			ProtocolVersion = protocolVersion;
            ServerId = serverId;
			Ipv4Addr = ipv4Addr;
			Ipv6Addr = ipv6Addr;
			OnionAddress = onionAddress;
			Port = port;
            ConfigurationHash = configurationHash;
		}

		public RegistrationToken()
		{
			// Constructor for when a token is being reconstituted from blockchain data
		}

		public byte[] GetRegistrationTokenBytes(RsaKey rsaKey, BitcoinSecret privateKeyEcdsa)
		{
			var token = new List<byte>();

            token.AddRange(Encoding.ASCII.GetBytes(ServerId.PadRight(34)));

			if (Ipv4Addr != null)
			{
				token.AddRange(Ipv4Addr.GetAddressBytes());
			}
			else
			{
				token.Add(0x00); token.Add(0x00); token.Add(0x00); token.Add(0x00);
			}

			if (Ipv6Addr != null)
			{
				token.AddRange(Ipv6Addr.GetAddressBytes());
			}
			else
			{
				token.Add(0x00); token.Add(0x00); token.Add(0x00); token.Add(0x00);
				token.Add(0x00); token.Add(0x00); token.Add(0x00); token.Add(0x00);
				token.Add(0x00); token.Add(0x00); token.Add(0x00); token.Add(0x00);
				token.Add(0x00); token.Add(0x00); token.Add(0x00); token.Add(0x00);
			}

			if (OnionAddress != null)
			{
				token.AddRange(Encoding.ASCII.GetBytes(OnionAddress));
			}
			else
			{
				token.Add(0x00); token.Add(0x00); token.Add(0x00); token.Add(0x00);
				token.Add(0x00); token.Add(0x00); token.Add(0x00); token.Add(0x00);
				token.Add(0x00); token.Add(0x00); token.Add(0x00); token.Add(0x00);
				token.Add(0x00); token.Add(0x00); token.Add(0x00); token.Add(0x00);
			}

			// TODO: Review the use of BitConverter for endian-ness issues
			byte[] portNumber = BitConverter.GetBytes(Port);

			token.Add(portNumber[0]);
			token.Add(portNumber[1]);

            CryptoUtils cryptoUtils = new CryptoUtils(rsaKey, privateKeyEcdsa);

			// Sign header (excluding preliminary length marker bytes) with RSA
			RsaSignature = cryptoUtils.SignDataRSA(token.ToArray());
			byte[] rsaLength = BitConverter.GetBytes(RsaSignature.Length);

			// Sign header (excluding preliminary length marker bytes) with ECDSA
			EcdsaSignature = cryptoUtils.SignDataECDSA(token.ToArray());
			byte[] ecdsaLength = BitConverter.GetBytes(EcdsaSignature.Length);

			// TODO: Check if the lengths are >2 bytes. Should not happen
			// for most conceivable signature schemes at current key lengths
			token.Add(rsaLength[0]);
			token.Add(rsaLength[1]);
			token.AddRange(RsaSignature);

			token.Add(ecdsaLength[0]);
			token.Add(ecdsaLength[1]);
			token.AddRange(EcdsaSignature);

            // Server configuration hash
            token.AddRange(Encoding.ASCII.GetBytes(ConfigurationHash));

            // Finally add protocol byte and computed length to beginning of header
            byte[] protocolVersionByte = BitConverter.GetBytes(ProtocolVersion);
			byte[] headerLength = BitConverter.GetBytes(token.Count);

			token.Insert(0, protocolVersionByte[0]);
			token.Insert(1, headerLength[0]);
			token.Insert(2, headerLength[1]);

			return token.ToArray();
		}

		public void ParseTransaction(Transaction tx, Network network)
		{
			if (tx.Outputs.Count < 2)
				throw new Exception("Transaction does not have sufficient outputs");

			// Assume the nulldata transaction marker is the first output
			// Validate that the marker bytes are present before proceeding

			if (tx.Outputs[0].ScriptPubKey.ToHex().ToLower() != "6a1a425245455a455f524547495354524154494f4e5f4d41524b4552")
				throw new Exception("Missing Breeze registration marker from first transaction output");

			// Peek at first non-nulldata address to get the length information,
			// this indicates how many outputs have been used for encoding, and
			// by extension indicates if there will be a change address output

            PubKey[] tempPubKeyArray = tx.Outputs[1].ScriptPubKey.GetDestinationPublicKeys();

			if (tempPubKeyArray.Length > 1)
				// This can't have been generated by a server registration, we don't use
				// multiple signatures for the registration transaction outputs by design
				throw new Exception("Registration transaction output has too many PubKeys");

			byte[] secondOutputData = BlockChainDataConversions.PubKeyToBytes(tempPubKeyArray[0]);

			var protocolVersion = (int)secondOutputData[0];

			var headerLength = ((int)secondOutputData[2] << 8) + ((int)secondOutputData[1]);

			// 64 = number of bytes we can store per output
			int numPubKeys = headerLength / 64;

			// Is there a partially 'full' PubKey holding the remainder of the bytes?
			if (headerLength % 64 != 0)
				numPubKeys++;

			if (tx.Outputs.Count < (numPubKeys + 1))
				throw new Exception("Too few transaction outputs, registration transaction incomplete");

			PubKey[] tempPK;
			List<PubKey> pubKeyList = new List<PubKey>();
			for (int i = 1; i < (numPubKeys + 1); i++)
			{
				tempPK = tx.Outputs[i].ScriptPubKey.GetDestinationPublicKeys();

				if (tempPK.Length > 1)
					// This can't have been generated by a server registration, we don't use
					// multiple signatures for the registration transaction outputs by design
					throw new Exception("Registration transaction output has too many PubKeys");

				pubKeyList.Add(tempPK[0]);
			}

            byte[] bitstream = BlockChainDataConversions.PubKeysToBytes(pubKeyList);

			// Need to consume X bytes at a time off the bitstream and convert them to various
			// data types, then set member variables to the retrieved values.

			// Skip over protocol version and header length bytes
			int position = 3;
			ProtocolVersion = protocolVersion;

            byte[] serverIdTemp = GetSubArray(bitstream, position, 34);

            ServerId = Encoding.ASCII.GetString(serverIdTemp);

            position += 34;

			// Either a valid IPv4 address, or all zero bytes
			bool allZeroes = true;
			byte[] ipv4temp = GetSubArray(bitstream, position, 4);

			for (int i = 0; i < ipv4temp.Length; i++)
			{
				if (ipv4temp[i] != 0)
					allZeroes = false;
			}

			if (!allZeroes)
			{
				Ipv4Addr = new IPAddress(ipv4temp);
			}
			else
			{
				Ipv4Addr = null;
			}

			position += 4;

			// Either a valid IPv6 address, or all zero bytes
			allZeroes = true;
			byte[] ipv6temp = GetSubArray(bitstream, position, 16);

			for (int i = 0; i < ipv6temp.Length; i++)
			{
				if (ipv6temp[i] != 0)
					allZeroes = false;
			}

			if (!allZeroes)
			{
				Ipv6Addr = new IPAddress(ipv6temp);
			}
			else
			{
				Ipv6Addr = null;
			}

			position += 16;

			// Either a valid onion address, or all zero bytes
			allZeroes = true;
			byte[] onionTemp = GetSubArray(bitstream, position, 16);

			for (int i = 0; i < onionTemp.Length; i++)
			{
				if (onionTemp[i] != 0)
					allZeroes = false;
			}

			if (!allZeroes)
			{
				OnionAddress = Encoding.ASCII.GetString(onionTemp);
			}
			else
			{
				OnionAddress = null;
			}

			position += 16;

			var temp = GetSubArray(bitstream, position, 2);
			Port = ((int)temp[1] << 8) + ((int)temp[0]);
			position += 2;

			temp = GetSubArray(bitstream, position, 2);
			var rsaLength = ((int)temp[1] << 8) + ((int)temp[0]);
			position += 2;

			RsaSignature = GetSubArray(bitstream, position, rsaLength);
			position += rsaLength;

			temp = GetSubArray(bitstream, position, 2);
			var ecdsaLength = ((int)temp[1] << 8) + ((int)temp[0]);
			position += 2;

			EcdsaSignature = GetSubArray(bitstream, position, ecdsaLength);
			position += ecdsaLength;

            byte[] configurationHashTemp = GetSubArray(bitstream, position, 40);
            ConfigurationHash = Encoding.ASCII.GetString(configurationHashTemp);
            position += 40;

            // TODO: Validate signatures
        }

		private byte[] GetSubArray(byte[] data, int index, int length)
		{
			byte[] result = new byte[length];
			Array.Copy(data, index, result, 0, length);
			return result;
		}
	}
}