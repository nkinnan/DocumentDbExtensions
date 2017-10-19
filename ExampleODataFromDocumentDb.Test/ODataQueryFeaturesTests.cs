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
using System.Collections.Generic;

namespace ExampleODataFromDocumentDb.Test
{
    /// <summary>
    /// Query by date time is enabled by the use of DocumentDbExtensions query interception on the server and 
    /// decoration of DateTime fields in the document with DateTimeDocumentDbJsonConverter
    /// </summary>
    [TestClass]
    public class ODataQueryFeaturesTests : UnitTestBase
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

            base.CreateHouse1DateTimeTestHistory();
        }

        /// <summary>
        /// skip is not supported by DocumentDB
        /// </summary>
        [TestMethod]
        public void SkipFailsWithIntelligentError()
        {
            bool threw = false;
            try
            {
                odataClient.HouseHistory.Skip(1).ToArray();
            }
            catch (DataServiceQueryException e)
            {
                threw = true;
                Assert.AreEqual((int)HttpStatusCode.BadRequest, (e.InnerException as DataServiceClientException).StatusCode);
                Assert.IsTrue(e.InnerException.Message.Contains("Query option 'Skip' is not allowed"));
            }
            Assert.IsTrue(threw);
        }

        /// <summary>
        /// count is not supported by DocumentDB
        /// </summary>
        [TestMethod]
        public void CountFailsWithIntelligentError()
        {
            bool threw = false;
            try
            {
                int countAll = odataClient.HouseHistory.Count();
            }
            catch (DataServiceQueryException e)
            {
                threw = true;
                Assert.AreEqual((int)HttpStatusCode.BadRequest, (e.InnerException as DataServiceClientException).StatusCode);
                Assert.IsTrue(e.InnerException.Message.Contains("Query option 'Count' is not allowed"));
            }
            Assert.IsTrue(threw);
        }

        /// <summary>
        /// distinct is not supported by DocumentDB _OR_ OData :)
        /// </summary>
        [TestMethod]
        public void DistinctFailsWithIntelligentError()
        {
            bool threw = false;
            try
            {
                var distinct = odataClient.HouseHistory.Distinct().ToArray();
            }
            catch (NotSupportedException e)
            {
                threw = true;
                Assert.AreEqual("The method 'Distinct' is not supported.", e.Message);
            }
            Assert.IsTrue(threw);

            //Assert.AreEqual((int)HttpStatusCode.BadRequest, (e.InnerException as DataServiceClientException).StatusCode);
            //Assert.IsTrue(e.InnerException.Message.Contains("Query option 'Count' is not allowed"));
        }

        [TestMethod]
        public void LastFailsWithIntelligentError()
        {
            bool threw = false;
            try
            {
                var result = odataClient.HouseHistory.Last();
            }
            catch (NotSupportedException e)
            {
                threw = true;
                Assert.AreEqual("The method 'Last' is not supported.", e.Message);
            }
            Assert.IsTrue(threw);
        }

        [TestMethod]
        public void LastOrDefaultFailsWithIntelligentError()
        {
            bool threw = false;
            try
            {
                var result = odataClient.HouseHistory.LastOrDefault();
            }
            catch (NotSupportedException e)
            {
                threw = true;
                Assert.AreEqual("The method 'LastOrDefault' is not supported.", e.Message);
            }
            Assert.IsTrue(threw);
        }

        [TestMethod]
        public void AverageFailsWithIntelligentError()
        {
            bool threw = false;
            try
            {
                var result = odataClient.HouseHistory.Average(h => h.TestSkylight.WidthInches);
            }
            catch (NotSupportedException e)
            {
                threw = true;
                Assert.AreEqual("The method 'Average' is not supported.", e.Message);
            }
            Assert.IsTrue(threw);
        }

        /// <summary>
        /// Ignore: seems to be a bug in the OData client, it can't generate a URI from this valid Linq query
        /// </summary>
        [TestMethod]
        public void ContainsFailsWithIntelligentError()
        {
            bool threw = false;
            try
            {
                var values = new string[] { "a", "b" };
                var result = odataClient.HouseHistory.Where(s => values.Contains(s.Id)).ToArray();
            }
            catch (NotSupportedException e)
            {
                threw = true;
                Assert.AreEqual("The method 'Contains' is not supported.", e.Message);
            }
            Assert.IsTrue(threw);
        }

        /// <summary>
        /// Ignore: contains is not supported, but the odata client can't emit a proper exception if calling contains on a list
        /// https://github.com/OData/odata.net/issues/608
        /// </summary>
        [Ignore]
        [TestMethod]
        public void ContainsOnGenericListFailsWithIntelligentError()
        {
            List<string> values = new List<string>();
            values.Add("a");
            values.Add("b");

            // but we know there is always only one filter in these kinds of scenarios
            var result = odataClient.HouseHistory.Where(s => values.Contains(s.TestName)).ToArray();
        }

        /// <summary>
        /// Ignored until I can implement sub-select query translation support (once DocDB makes it available!)
        /// </summary>
        [Ignore]
        [TestMethod]
        public void QueryOnComplexArrayNestedValue()
        {
            var equalsSecondDate = odataClient.HouseHistory
               .Where(h => h.ModifiedId == house1guid.ToString("D"))
               .Where(h => h.TestWindows.Any(w => w.HeightInches == 72))
               .ToList();
            Assert.AreEqual(1, equalsSecondDate.Count);
            Assert.AreEqual(secondDate, equalsSecondDate.Single().TestSkylight.InstalledDate);
        }

        [TestMethod]
        public void First()
        {
            var result = odataClient.HouseHistory.First();
            Assert.IsNotNull(result);
        }

        [TestMethod]
        public void FirstOrDefault()
        {
            var result = odataClient.HouseHistory.FirstOrDefault();
            Assert.IsNotNull(result);
        }

        [TestMethod]
        public void Top()
        {
            var result = odataClient.HouseHistory.Take(2).ToArray();
            Assert.IsNotNull(result);
            Assert.AreEqual(2, result.Count());
        }
    }
}
