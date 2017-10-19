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
    public class HistoryTriggerTests : UnitTestBase
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
        public void PostDoesNotCreateHistory()
        {
            var create = House.CreateHouse(Guid.NewGuid().ToString("D"));
            create.TestName = "create";
            odataClient.AddToHouses(create);
            odataClient.SaveChanges();
            odataClient.Detach(create);

            var house = odataClient.Houses.ByKey(create.Id).GetValue();
            var history = odataClient.HouseHistory.Where(h => h.ModifiedId == create.Id).ToList();

            Assert.IsNotNull(house);
            Assert.AreEqual(0, history.Count);
        }

        [TestMethod]
        public void MergeCreatesHistory()
        {
            // get the entity
            House house = odataClient.Houses.ByKey(house1guid.ToString("D")).GetValue();

            // modify the entity
            house.TestName = Guid.NewGuid().ToString();

            // save the change
            odataClient.UpdateObject(house);
            odataClient.SaveChanges();
            odataClient.Detach(house);

            var history = odataClient.HouseHistory.Where(h => h.ModifiedId == house.Id).ToList();

            Assert.AreEqual(1, history.Count);

            // check update history
            var replaceHistory = history.Where(h => h.ModifyAction == "Replace").ToList();
            Assert.AreEqual(1, replaceHistory.Count);
            // allow 30 second clock skew
            Assert.IsTrue(TimeSpan.FromSeconds(30).Ticks > Math.Abs(replaceHistory.Single().ModifyTimestamp.Value.Ticks - DateTimeOffset.UtcNow.Ticks));
        }

        /// <summary>
        /// Note: we pass SaveChangesOptions.ReplaceOnUpdate to cause a PUT instead of a MERGE
        /// </summary>
        [TestMethod]
        public void PutCreatesHistory()
        {
            // get the entity
            House house = odataClient.Houses.ByKey(house1guid.ToString("D")).GetValue();

            // modify the entity
            house.TestName = Guid.NewGuid().ToString();

            // save the change
            odataClient.UpdateObject(house);
            odataClient.SaveChanges(SaveChangesOptions.ReplaceOnUpdate);
            odataClient.Detach(house);

            var history = odataClient.HouseHistory.Where(h => h.ModifiedId == house.Id).ToList();

            Assert.AreEqual(1, history.Count);

            // check update history
            var replaceHistory = history.Where(h => h.ModifyAction == "Replace").ToList();
            Assert.AreEqual(1, replaceHistory.Count);
            // allow 30 second clock skew
            Assert.IsTrue(TimeSpan.FromSeconds(30).Ticks > Math.Abs(replaceHistory.Single().ModifyTimestamp.Value.Ticks - DateTimeOffset.UtcNow.Ticks));
        }

        [TestMethod]
        public void DeleteCreatesHistory()
        {
            // get the entity
            House house = odataClient.Houses.ByKey(house1guid.ToString("D")).GetValue();

            // delete it
            odataClient.DeleteObject(house);
            odataClient.SaveChanges();
            odataClient.Detach(house);

            var history = odataClient.HouseHistory.Where(h => h.ModifiedId == house.Id).ToList();

            Assert.AreEqual(1, history.Count);

            // check update history
            var replaceHistory = history.Where(h => h.ModifyAction == "Delete").ToList();
            Assert.AreEqual(1, replaceHistory.Count);
            // allow 30 second clock skew
            Assert.IsTrue(TimeSpan.FromSeconds(30).Ticks > Math.Abs(replaceHistory.Single().ModifyTimestamp.Value.Ticks - DateTimeOffset.UtcNow.Ticks));
        }
    }
}
