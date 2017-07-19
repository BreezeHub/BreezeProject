using Microsoft.VisualStudio.TestTools.UnitTesting;

using BreezeCommon;

//this is temp and will be replaced with xUnit

namespace BreezeCommon.Tests
{
    [TestClass]
    public class RegStoreNameTest
    {
        [TestMethod]
        public void RegistrationNameTest()
        {
			Assert.IsTrue(new RegistrationStore().Name == "RegistrationStore");
        }
    }
}