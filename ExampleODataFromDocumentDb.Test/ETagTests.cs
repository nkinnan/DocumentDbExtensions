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
    public class ETagTests : UnitTestBase
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
        public void EtagIsPresent()
        {
            // by key
            var house1a = odataClient.Houses.ByKey(house1guid.ToString("D")).GetValue();
            var etag1 = odataClient.GetEntityDescriptor(house1a).ETag;

            // by key implicit
            var house1b = odataClient.Houses
                .Where(house => house.Id == house1guid.ToString("D"))
                .AsEnumerable().FirstOrDefault();
            var etag2 = odataClient.GetEntityDescriptor(house1b).ETag;

            // by query ($filter)
            var house1c = odataClient.Houses
                .Where(house => house.TestName == "house1")
                .AsEnumerable().FirstOrDefault();
            var etag3 = odataClient.GetEntityDescriptor(house1c).ETag;

            Assert.IsNotNull(etag1);
            Assert.IsNotNull(etag2);
            Assert.IsNotNull(etag3);
            Assert.IsFalse(string.IsNullOrWhiteSpace(etag1));
            Assert.IsFalse(string.IsNullOrWhiteSpace(etag2));
            Assert.IsFalse(string.IsNullOrWhiteSpace(etag3));
            Assert.AreEqual(etag1, etag2);
            Assert.AreEqual(etag1, etag3);
        }

        /// <summary>
        /// Can't figure out how to do this with the DataServiceContext, so have to make a manual http request
        /// </summary>
        [TestMethod]
        public void EtagWorksOnGet()
        {
            HttpClient client = new HttpClient();
            HttpRequestMessage request;
            HttpResponseMessage response;

            // Retrieving an object for the first time. Observe that the ETag is NOT in the response headers and 
            // the returned payload contains the annotation @odata.etag indicating the ETag associated with that customer.
            request = new HttpRequestMessage(HttpMethod.Get, ApiUri.ToString() + "/Houses('" + house1guid.ToString("D") + "')");
            response = client.SendAsync(request).Result;
            response.EnsureSuccessStatusCode();
            dynamic house = JObject.Parse(response.Content.ReadAsStringAsync().Result);
            string odataEtag = house["@odata.etag"];

            // Retrieving the same object as in the previous request but only if the ETag doesn't match the one
            // specified in the If-None-Match header. We are sending the ETag value that we obtained from the previous
            // request, so we expect to see a 304 (Not Modified) response.
            request = new HttpRequestMessage(HttpMethod.Get, ApiUri.ToString() + "/Houses('" + house1guid.ToString("D") + "')");
            request.Headers.IfNoneMatch.Add(EntityTagHeaderValue.Parse(odataEtag));
            response = client.SendAsync(request).Result;
            Assert.AreEqual(HttpStatusCode.NotModified, response.StatusCode);
        }

        [TestMethod]
        public void EtagWorksOnMerge()
        {
            // get the entity and remember old etag
            House house = odataClient.Houses.ByKey(house1guid.ToString("D")).GetValue();
            var originalEtag = odataClient.GetEntityDescriptor(house).ETag;

            // modify the entity
            house.TestName = Guid.NewGuid().ToString();

            // save the change
            odataClient.UpdateObject(house);
            odataClient.SaveChanges();
            odataClient.Detach(house); // if we don't do this on update, it breaks etag for some reason, but this is a client issue only!

            // get the entity and remember new etag
            house = odataClient.Houses.ByKey(house1guid.ToString("D")).GetValue();
            var newEtag = odataClient.GetEntityDescriptor(house).ETag;

            // modify the entity
            house.TestName = Guid.NewGuid().ToString();

            // set the entity to use the OLD etag
            odataClient.Detach(house);
            odataClient.AttachTo("Houses", house, originalEtag);

            // save should now fail
            odataClient.UpdateObject(house);
            bool threw = false;
            try
            {
                odataClient.SaveChanges();
            }
            catch (DataServiceRequestException e)
            {
                threw = true;
                Assert.AreEqual((int)HttpStatusCode.PreconditionFailed, (e.InnerException as DataServiceClientException).StatusCode);
            }
            Assert.IsTrue(threw);

            // set the entity to use the NEW etag
            odataClient.Detach(house);
            odataClient.AttachTo("Houses", house, newEtag);

            // save should now succeed
            odataClient.UpdateObject(house);
            odataClient.SaveChanges();
        }

        /// <summary>
        /// Note: we pass SaveChangesOptions.ReplaceOnUpdate to cause a PUT instead of a MERGE
        /// </summary>
        [TestMethod]
        public void EtagWorksOnPut()
        {
            // get the entity and remember old etag
            House house = odataClient.Houses.ByKey(house1guid.ToString("D")).GetValue();
            var originalEtag = odataClient.GetEntityDescriptor(house).ETag;

            // modify the entity
            house.TestName = Guid.NewGuid().ToString();

            // save the change
            odataClient.UpdateObject(house);
            odataClient.SaveChanges(SaveChangesOptions.ReplaceOnUpdate);
            odataClient.Detach(house); // if we don't do this on update, it breaks etag for some reason, but this is a client issue only!

            // get the entity and remember new etag
            house = odataClient.Houses.ByKey(house1guid.ToString("D")).GetValue();
            var newEtag = odataClient.GetEntityDescriptor(house).ETag;

            // modify the entity
            house.TestName = Guid.NewGuid().ToString();

            // set the entity to use the OLD etag
            odataClient.Detach(house);
            odataClient.AttachTo("Houses", house, originalEtag);

            // save should now fail
            odataClient.UpdateObject(house);
            bool threw = false;
            try
            {
                odataClient.SaveChanges(SaveChangesOptions.ReplaceOnUpdate);
            }
            catch (DataServiceRequestException e)
            {
                threw = true;
                Assert.AreEqual((int)HttpStatusCode.PreconditionFailed, (e.InnerException as DataServiceClientException).StatusCode);
            }
            Assert.IsTrue(threw);

            // set the entity to use the NEW etag
            odataClient.Detach(house);
            odataClient.AttachTo("Houses", house, newEtag);

            // save should now succeed
            odataClient.UpdateObject(house);
            odataClient.SaveChanges(SaveChangesOptions.ReplaceOnUpdate);
        }

        [TestMethod]
        public void EtagWorksOnDelete()
        {
            // get the entity and remember old etag
            House house = odataClient.Houses.ByKey(house1guid.ToString("D")).GetValue();
            var originalEtag = odataClient.GetEntityDescriptor(house).ETag;

            // modify the entity
            house.TestName = Guid.NewGuid().ToString();

            // save the change
            odataClient.UpdateObject(house);
            odataClient.SaveChanges();
            odataClient.Detach(house); // if we don't do this on update, it breaks etag for some reason, but this is a client issue only!

            // get the entity and remember new etag
            house = odataClient.Houses.ByKey(house1guid.ToString("D")).GetValue();
            var newEtag = odataClient.GetEntityDescriptor(house).ETag;

            // set the entity to use the OLD etag
            odataClient.Detach(house);
            odataClient.AttachTo("Houses", house, originalEtag);

            // delete should now fail
            odataClient.DeleteObject(house);
            bool threw = false;
            try
            {
                odataClient.SaveChanges();
            }
            catch (DataServiceRequestException e)
            {
                threw = true;
                Assert.AreEqual((int)HttpStatusCode.PreconditionFailed, (e.InnerException as DataServiceClientException).StatusCode);
            }
            Assert.IsTrue(threw);

            // set the entity to use the NEW etag
            odataClient.Detach(house);
            odataClient.AttachTo("Houses", house, newEtag);

            // delete should now succeed
            odataClient.DeleteObject(house);
            odataClient.SaveChanges();
        }
    }
}
