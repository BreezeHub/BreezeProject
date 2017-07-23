using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Net;
using System.Text;

using BreezeCommon;

//this is temp and will be replaced with xUnit

namespace BreezeCommon.Tests
{
    [TestClass]
    public class RegStoreTests
    {
        [TestMethod]
        public void RegistrationNameTest()
        {
            Assert.IsTrue(new RegistrationStore(".").Name == "RegistrationStore");
        }

		[TestMethod]
		public void RegistrationStoreAddTest()
		{
            var token = new RegistrationToken(255,
                                              "1BvBMSEYstWetqTFn5Au4m4GFg7xJaNVN2",
                                              IPAddress.Parse("127.0.0.1"),
                                              IPAddress.Parse("2001:0db8:85a3:0000:0000:8a2e:0370:7334"),
                                              "0123456789ABCDEF",
                                              37123);
            
            token.RsaSignature = Encoding.ASCII.GetBytes("xyz");
			token.EcdsaSignature = Encoding.ASCII.GetBytes("abc");

            RegistrationRecord record = new RegistrationRecord(DateTime.Now, token);
            RegistrationStore store = new RegistrationStore(Path.GetTempFileName());

            Assert.IsTrue(store.Add(record));
        }

		[TestMethod]
		public void RegistrationStoreGetOneTest()
		{
			var token = new RegistrationToken(255,
											  "1BvBMSEYstWetqTFn5Au4m4GFg7xJaNVN2",
											  IPAddress.Parse("127.0.0.1"),
											  IPAddress.Parse("2001:0db8:85a3:0000:0000:8a2e:0370:7334"),
											  "0123456789ABCDEF",
											  37123);

			token.RsaSignature = Encoding.ASCII.GetBytes("xyz");
			token.EcdsaSignature = Encoding.ASCII.GetBytes("abc");

			RegistrationRecord record = new RegistrationRecord(DateTime.Now, token);
			RegistrationStore store = new RegistrationStore(Path.GetTempFileName());

			store.Add(record);

            var retrievedRecords = store.GetAll();

            Assert.AreEqual(retrievedRecords.Count, 1);

            var retrievedRecord = retrievedRecords[0].Record;

            Assert.AreEqual(retrievedRecord.ProtocolVersion, 255);
            Assert.AreEqual(retrievedRecord.ServerId, "1BvBMSEYstWetqTFn5Au4m4GFg7xJaNVN2");
            Assert.AreEqual(retrievedRecord.Ipv4Addr, IPAddress.Parse("127.0.0.1"));
			Assert.AreEqual(retrievedRecord.Ipv6Addr, IPAddress.Parse("2001:0db8:85a3:0000:0000:8a2e:0370:7334"));
            Assert.AreEqual(retrievedRecord.OnionAddress, "0123456789ABCDEF");
			Assert.AreEqual(retrievedRecord.Port, 37123);
        }

		[TestMethod]
		public void RegistrationStoreGetServerIdTest()
		{
			var token = new RegistrationToken(1,
											  "175tWpb8K1S7NmH4Zx6rewF9WQrcZv245W",
											  IPAddress.Parse("172.16.1.10"),
											  IPAddress.Parse("2001:0db8:85a3:0000:1234:8a2e:0370:7334"),
											  "5678901234ABCDEF",
											  16174);

			token.RsaSignature = Encoding.ASCII.GetBytes("def");
			token.EcdsaSignature = Encoding.ASCII.GetBytes("ghi");

			RegistrationRecord record = new RegistrationRecord(DateTime.Now, token);
			RegistrationStore store = new RegistrationStore(Path.GetTempFileName());

			store.Add(record);

			var token2 = new RegistrationToken(255,
									 		   "1BvBMSEYstWetqTFn5Au4m4GFg7xJaNVN2",
											   IPAddress.Parse("127.0.0.1"),
											   IPAddress.Parse("2001:0db8:85a3:0000:0000:8a2e:0370:7334"),
											   "0123456789ABCDEF",
											   37123);

			token2.RsaSignature = Encoding.ASCII.GetBytes("xyz");
			token2.EcdsaSignature = Encoding.ASCII.GetBytes("abc");

			RegistrationRecord record2 = new RegistrationRecord(DateTime.Now, token2);

			store.Add(record2);

            var retrievedRecords = store.GetByServerId("1BvBMSEYstWetqTFn5Au4m4GFg7xJaNVN2");

			Assert.AreEqual(retrievedRecords.Count, 1);

			var retrievedRecord = retrievedRecords[0].Record;

			Assert.AreEqual(retrievedRecord.ProtocolVersion, 255);
			Assert.AreEqual(retrievedRecord.ServerId, "1BvBMSEYstWetqTFn5Au4m4GFg7xJaNVN2");
			Assert.AreEqual(retrievedRecord.Ipv4Addr, IPAddress.Parse("127.0.0.1"));
			Assert.AreEqual(retrievedRecord.Ipv6Addr, IPAddress.Parse("2001:0db8:85a3:0000:0000:8a2e:0370:7334"));
			Assert.AreEqual(retrievedRecord.OnionAddress, "0123456789ABCDEF");
			Assert.AreEqual(retrievedRecord.Port, 37123);
		}
	}
}