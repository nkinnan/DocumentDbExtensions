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
using System.Configuration;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Linq;
using Microsoft.Azure.Documents.Client;
using Newtonsoft.Json;

namespace ExampleODataFromDocumentDb.Test
{
    [TestClass]
    public class MiscellaneousTests : UnitTestBase
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
        public void GuidIdCannonicalization()
        {
            var house = odataClient.Houses.ByKey(house1guid.ToString("D")).GetValue();
            Assert.IsNotNull(house);

            house = odataClient.Houses.ByKey(house1guid.ToString()).GetValue();
            Assert.IsNotNull(house);
        }

        /// <summary>
        /// Ignored until this feature can be added to the odata client, as I added it for the odata service libs
        /// /// </summary>
        [Ignore]
        [TestMethod]
        public void CanCreateNullCollectionProperty()
        {
            var create = House.CreateHouse(Guid.NewGuid().ToString("D"));
            create.TestWindows = null;
            odataClient.AddToHouses(create);
            odataClient.SaveChanges();
            odataClient.Detach(create);

            var house = odataClient.Houses.ByKey(create.Id).GetValue();

            Assert.IsNull(house.TestWindows);
        }

        [TestMethod]
        public void CanRetrieveNullCollectionProperty()
        {
            /* todo: The service can return a null collection, but apparently the client needs that feature added as well
                     I suppose it's not that big a deal though, since you have control over the object at this point
            var create = House.CreateHouse(Guid.NewGuid().ToString("D"));
            create.TestWindows = null;
            client.AddToHouses(create);
            client.SaveChanges();
            client.Detach(create);*/

            // because of the above bug, we must create the document directly, but this is the more important path anyway
            var connectionString = ConfigurationManager.AppSettings["connectionString"];
            var databaseName = ConfigurationManager.AppSettings["databaseName"];
            var collectionName = ConfigurationManager.AppSettings["collectionName"];
            var collectionLink = string.Format("dbs/{0}/colls/{1}", databaseName, collectionName);
            var documentLinkFormat = collectionLink + "/docs/{0}";
            var task = DocumentDB.GetDocumentClient(connectionString, databaseName, collectionName);
            task.Wait();
            var docClient = task.Result;

            // make test document
            HouseDocument houseDocument = new HouseDocument();
            houseDocument.Id = Guid.NewGuid().ToString("D");
            houseDocument.TestWindows = null;

            // insert it directly to DocumentDB
            DocumentDbExtensions.ExecuteMethodWithRetry(() =>
                docClient.UpsertDocumentAsync(collectionLink, houseDocument));

            // retrieve it directly from DocumentDB
            var retrievedDocument = DocumentDbExtensions.ExecuteQueryWithContinuationAndRetry(
                docClient.CreateDocumentQuery<HouseDocument>(collectionLink)
                .Where(x => x.Id == houseDocument.Id))
                .Single();

            // is the test set up properly?
            Assert.IsNull(retrievedDocument.TestWindows);

            // finally, test retrieval through OData
            var house = odataClient.Houses.ByKey(houseDocument.Id).GetValue();
            Assert.IsNotNull(house);
            // note: house.TestWindows will actually be deserialized as an empty collection here, but the test is 
            //       that the OData service didn't throw an error when encountering that in the source document
        }
    }
}
