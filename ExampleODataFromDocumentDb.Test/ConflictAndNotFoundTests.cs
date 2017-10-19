using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ExampleODataFromDocumentDb.Client;
using System.Net;
using System.Linq;
using Microsoft.OData.Client;
using System.Net.Http;
using Newtonsoft.Json.Linq;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace ExampleODataFromDocumentDb.Test
{
    [TestClass]
    public class ConflictAndNotFoundTests : UnitTestBase
    {
        [ClassInitialize]
        public static new void ClassInitialize(TestContext testContext)
        {
            UnitTestBase.ClassInitialize(testContext);
        }

        [TestInitialize]
        public new void TestInitialize()
        {
            base.TestInitialize();
        }

        [TestMethod]
        public void DoublePostReturnsConflict()
        {
            var house2 = House.CreateHouse(house1guid.ToString("D"));
            odataClient.AddToHouses(house2);

            bool threw = false;
            try
            {
                odataClient.SaveChanges();
            }
            catch (DataServiceRequestException e)
            {
                threw = true;
                Assert.AreEqual((int)HttpStatusCode.Conflict, (e.InnerException as DataServiceClientException).StatusCode);
            }
            Assert.IsTrue(threw);
        }

        [TestMethod]
        public void GetByKeyOnMissingReturnsNotFound()
        {
            bool threw = false;
            try
            {
                var house = odataClient.Houses.ByKey(Guid.NewGuid().ToString("D")).GetValue();
            }
            catch (DataServiceQueryException e)
            {
                threw = true;
                Assert.AreEqual((int)HttpStatusCode.NotFound, (e.InnerException as DataServiceClientException).StatusCode);
            }
            Assert.IsTrue(threw);
        }

        [TestMethod]
        public void MergeOnMissingReturnsNotFound()
        {
            // create and attach a non-existant entity
            var house2 = House.CreateHouse(Guid.NewGuid().ToString("D"));
            odataClient.AttachTo("Houses", house2);

            // update it
            house2.TestName = Guid.NewGuid().ToString();
            odataClient.UpdateObject(house2);

            bool threw = false;
            try
            {
                odataClient.SaveChanges();
            }
            catch (DataServiceRequestException e)
            {
                threw = true;
                Assert.AreEqual((int)HttpStatusCode.NotFound, (e.InnerException as DataServiceClientException).StatusCode);
            }
            Assert.IsTrue(threw);
        }

        /// <summary>
        /// Note: we pass SaveChangesOptions.ReplaceOnUpdate to cause a PUT instead of a MERGE
        /// </summary>
        [TestMethod]
        public void PutOnMissingReturnsNotFound()
        {
            // create and attach a non-existant entity
            var house2 = House.CreateHouse(Guid.NewGuid().ToString("D"));
            odataClient.AttachTo("Houses", house2);

            // update it
            house2.TestName = Guid.NewGuid().ToString();
            odataClient.UpdateObject(house2);

            bool threw = false;
            try
            {
                odataClient.SaveChanges(SaveChangesOptions.ReplaceOnUpdate);
            }
            catch (DataServiceRequestException e)
            {
                threw = true;
                Assert.AreEqual((int)HttpStatusCode.NotFound, (e.InnerException as DataServiceClientException).StatusCode);
            }
            Assert.IsTrue(threw);
        }

        [TestMethod]
        public void DeleteOnMissingReturnsNotFound()
        {
            // create and attach a non-existant entity
            var house2 = House.CreateHouse(Guid.NewGuid().ToString("D"));
            odataClient.AttachTo("Houses", house2);

            // delete it
            odataClient.DeleteObject(house2);

            bool threw = false;
            try
            {
                odataClient.SaveChanges();
            }
            catch (DataServiceRequestException e)
            {
                threw = true;
                Assert.AreEqual((int)HttpStatusCode.NotFound, (e.InnerException as DataServiceClientException).StatusCode);
            }
            Assert.IsTrue(threw);
        }
    }
}
