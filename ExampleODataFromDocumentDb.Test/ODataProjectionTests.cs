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
    public class ODataProjectionTests : UnitTestBase
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

        // THESE ARE ALL DISABLED because projecting a bare property requires a custom get method on the server-side 
        // on a PER PROPERTY basis!  I didn't bother to implement that (I'm sure there are good reasons for this 
        // design decision, I just am not familiar with them).
        #region by key, project bare
        [Ignore]
        [TestMethod]
        public void ByKeyProjectBareKey()
        {
            var res = odataClient.Houses.ByKey(house1guid.ToString("D"))
                .Select(h => h.Id)
                .GetValue();
            Assert.AreEqual(house1guid.ToString("D"), res);
        }

        [Ignore]
        [TestMethod]
        public void ByKeyProjectBareProperty()
        {
            var res = odataClient.Houses.ByKey(house1guid.ToString("D"))
                .Select(h => h.TestName)
                .GetValue();
            Assert.AreEqual("house1", res);
        }

        [Ignore]
        [TestMethod]
        public void ByKeyProjectBareNestedProperty()
        {
            var res = odataClient.Houses.ByKey(house1guid.ToString("D"))
                .Select(h => h.TestSkylight.WidthInches)
                .GetValue();
            Assert.AreEqual(18, res);
        }

        [Ignore]
        [TestMethod]
        public void ByKeyProjectBareNestedArray()
        {
            var res = odataClient.Houses.ByKey(house1guid.ToString("D"))
                .Select(h => h.TestSkylight.Panes)
                .GetValue();
            Assert.AreEqual(18, res[0].WidthInches);
        }

        [Ignore]
        [TestMethod]
        public void ByKeyProjectBareTraverseArrayByIndex()
        {
            var res = odataClient.Houses.ByKey(house1guid.ToString("D"))
                .Select(h => h.TestWindows[0].Panes)
                .GetValue();
            Assert.AreEqual(12, res[0].WidthInches);
        }

        [Ignore]
        [TestMethod]
        public void ByKeyProjectBareTraverseArrayByQuery()
        {
            var res = odataClient.Houses.ByKey(house1guid.ToString("D"))
                .Select(h => h.TestWindows.Where(w => w.WidthInches == 80).First().Panes)
                .GetValue();
            Assert.AreEqual(12, res[0].WidthInches);
        }
        #endregion

        #region by key, project anonymous
        [TestMethod]
        public void ByKeyProjectAnonymousKey()
        {
            var res = odataClient.Houses.ByKey(house1guid.ToString("D"))
                .Select(h => new { h.Id })
                .GetValue();
            Assert.AreEqual(house1guid.ToString("D"), res.Id);
        }

        [TestMethod]
        public void ByKeyProjectAnonymousProperty()
        {
            var res = odataClient.Houses.ByKey(house1guid.ToString("D"))
                .Select(h => new { h.TestName })
                .GetValue();
            Assert.AreEqual("house1", res.TestName);
        }

        [TestMethod]
        public void ByKeyProjectAnonymousNestedProperty()
        {
            var res = odataClient.Houses.ByKey(house1guid.ToString("D"))
                .Select(h => new { h.TestSkylight.WidthInches })
                .GetValue();
            Assert.AreEqual(18, res.WidthInches);
        }

        [TestMethod]
        public void ByKeyProjectAnonymousNestedArray()
        {
            var res = odataClient.Houses.ByKey(house1guid.ToString("D"))
                .Select(h => new { h.TestSkylight.Panes })
                .GetValue();
            Assert.AreEqual(18, res.Panes[0].WidthInches);
        }

        [TestMethod]
        public void ByKeyProjectAnonymousTraverseArrayByIndex()
        {
            var res = odataClient.Houses.ByKey(house1guid.ToString("D"))
                .Select(h => new { h.TestWindows[0].Panes })
                .GetValue();
            Assert.AreEqual(12, res.Panes[0].WidthInches);
        }

        [TestMethod]
        public void ByKeyProjectAnonymousTraverseArrayByQuery()
        {
            var res = odataClient.Houses.ByKey(house1guid.ToString("D"))
                .Select(h => new { h.TestWindows.Where(w => w.WidthInches == 80).First().Panes })
                .GetValue();
            Assert.AreEqual(12, res.Panes[0].WidthInches);
        }
        #endregion

        #region by query, project bare
        [TestMethod]
        public void ByQueryProjectBareFailsWithIntelligentError()
        {
            bool threw = false;
            try
            {
                var res = odataClient.Houses.Where(h => h.TestName == "house1" && h.DocumentType == Client.DocumentType.House)
                    .Select(h => h.Id)
                    .ToArray().First();
            }
            catch (NotSupportedException e)
            {
                threw = true;

                // ...WEEEEeeeeeelllll I guess i'll let that pass as "intelligent error message"
                Assert.AreEqual("Can only specify query options (orderby, where, take, skip) after last navigation.", e.Message);
            }
            Assert.IsTrue(threw);
        }
        #endregion

        // bug in docdb vs odata IQueryable
        #region by query, project anonymous
        [TestMethod]
        public void ByQueryProjectAnonymousKey()
        {
            var res = odataClient.Houses.Where(h => h.TestName == "house1" && h.DocumentType == Client.DocumentType.House)
                .Select(h => new { h.Id })
                .ToArray().First();
            Assert.AreEqual(house1guid.ToString("D"), res.Id);
        }

        [TestMethod]
        public void ByQueryProjectAnonymousProperty()
        {
            var res = odataClient.Houses.Where(h => h.TestName == "house1" && h.DocumentType == Client.DocumentType.House)
                .Select(h => new { h.TestName })
                .ToArray().First();
            Assert.AreEqual("house1", res.TestName);
        }

        [TestMethod]
        public void ByQueryProjectAnonymousNestedProperty()
        {
            var res = odataClient.Houses.Where(h => h.TestName == "house1" && h.DocumentType == Client.DocumentType.House)
                .Select(h => new { h.TestSkylight.WidthInches })
                .ToArray().First();
            Assert.AreEqual(18, res.WidthInches);
        }

        [TestMethod]
        public void ByQueryProjectAnonymousNestedArray()
        {
            var res = odataClient.Houses.Where(h => h.TestName == "house1" && h.DocumentType == Client.DocumentType.House)
                .Select(h => new { h.TestSkylight.Panes })
                .ToArray().First();
            Assert.AreEqual(18, res.Panes[0].WidthInches);
        }

        [TestMethod]
        public void ByQueryProjectAnonymousTraverseArrayByIndex()
        {
            var res = odataClient.Houses.Where(h => h.TestName == "house1" && h.DocumentType == Client.DocumentType.House)
                .Select(h => new { h.TestWindows[0].Panes })
                .ToArray().First();
            Assert.AreEqual(12, res.Panes[0].WidthInches);
        }

        [TestMethod]
        public void ByQueryProjectAnonymousTraverseArrayByQuery()
        {
            var res = odataClient.Houses.Where(h => h.TestName == "house1" && h.DocumentType == Client.DocumentType.House)
                .Select(h => new { h.TestWindows.Where(w => w.WidthInches == 80).First().Panes })
                .ToArray().First();
            Assert.AreEqual(12, res.Panes[0].WidthInches);
        }
        #endregion

        // combo of above issues, revisit
        #region by query, project anonymous, rename
        [TestMethod]
        public void ByQueryProjectAnonymousKeyRenamed()
        {
            var res = odataClient.Houses.Where(h => h.TestName == "house1" && h.DocumentType == Client.DocumentType.House)
                .Select(h => new { xyz = h.Id })
                .ToArray().First();
            Assert.AreEqual(house1guid.ToString("D"), res.xyz);
        }

        [TestMethod]
        public void ByQueryProjectAnonymousPropertyRenamed()
        {
            var res = odataClient.Houses.Where(h => h.TestName == "house1" && h.DocumentType == Client.DocumentType.House)
                .Select(h => new { xyz = h.TestName })
                .ToArray().First();
            Assert.AreEqual("house1", res.xyz);
        }

        [TestMethod]
        public void ByQueryProjectAnonymousNestedPropertyRenamed()
        {
            var res = odataClient.Houses.Where(h => h.TestName == "house1" && h.DocumentType == Client.DocumentType.House)
                .Select(h => new { xyz = h.TestSkylight.WidthInches })
                .ToArray().First();
            Assert.AreEqual(18, res.xyz);
        }

        [TestMethod]
        public void ByQueryProjectAnonymousNestedArrayRenamed()
        {
            var res = odataClient.Houses.Where(h => h.TestName == "house1" && h.DocumentType == Client.DocumentType.House)
                .Select(h => new { xyz = h.TestSkylight.Panes })
                .ToArray().First();
            Assert.AreEqual(18, res.xyz[0].WidthInches);
        }

        [TestMethod]
        public void ByQueryProjectAnonymousTraverseArrayByIndexRenamed()
        {
            var res = odataClient.Houses.Where(h => h.TestName == "house1" && h.DocumentType == Client.DocumentType.House)
                .Select(h => new { xyz = h.TestWindows[0].Panes })
                .ToArray().First();
            Assert.AreEqual(12, res.xyz[0].WidthInches);
        }

        [TestMethod]
        public void ByQueryProjectAnonymousTraverseArrayByQueryRenamed()
        {
            var res = odataClient.Houses.Where(h => h.TestName == "house1" && h.DocumentType == Client.DocumentType.House)
                .Select(h => new { xyz = h.TestWindows.Where(w => w.WidthInches == 80).First().Panes })
                .ToArray().First();
            Assert.AreEqual(12, res.xyz[0].WidthInches);
        }
        #endregion

        // combo of above issues, revisit
        #region by query, project anonymous, with top
        [TestMethod]
        public void ByQueryProjectAnonymousTopKey()
        {
            var res = odataClient.Houses.Where(h => h.TestName == "house1" && h.DocumentType == Client.DocumentType.House)
                .Select(h => new { h.Id })
                .First();
            Assert.AreEqual(house1guid.ToString("D"), res.Id);
        }

        [TestMethod]
        public void ByQueryProjectAnonymousTopProperty()
        {
            var res = odataClient.Houses.Where(h => h.TestName == "house1" && h.DocumentType == Client.DocumentType.House)
                .Select(h => new { h.TestName })
                .First();
            Assert.AreEqual("house1", res.TestName);
        }

        [TestMethod]
        public void ByQueryProjectAnonymousTopNestedProperty()
        {
            var res = odataClient.Houses.Where(h => h.TestName == "house1" && h.DocumentType == Client.DocumentType.House)
                .Select(h => new { h.TestSkylight.WidthInches })
                .First();
            Assert.AreEqual(18, res.WidthInches);
        }

        [TestMethod]
        public void ByQueryProjectAnonymousTopNestedArray()
        {
            var res = odataClient.Houses.Where(h => h.TestName == "house1" && h.DocumentType == Client.DocumentType.House)
                .Select(h => new { h.TestSkylight.Panes })
                .First();
            Assert.AreEqual(18, res.Panes[0].WidthInches);
        }

        [TestMethod]
        public void ByQueryProjectAnonymousTopTraverseArrayByIndex()
        {
            var res = odataClient.Houses.Where(h => h.TestName == "house1" && h.DocumentType == Client.DocumentType.House)
                .Select(h => new { h.TestWindows[0].Panes })
                .First();
            Assert.AreEqual(12, res.Panes[0].WidthInches);
        }

        [TestMethod]
        public void ByQueryProjectAnonymousTopTraverseArrayByQuery()
        {
            var res = odataClient.Houses.Where(h => h.TestName == "house1" && h.DocumentType == Client.DocumentType.House)
                .Select(h => new { h.TestWindows.Where(w => w.WidthInches == 80).First().Panes })
                .First();
            Assert.AreEqual(12, res.Panes[0].WidthInches);
        }
        #endregion

        ////#if NEEDED_SERVER_SIDE_PER_PROPERTY_SUPPORT_LOW_PRIORITY_FEATURE_IMPLEMENTED
        ////        /// <summary>
        ////        /// This will generate a URI like: http://localhost/controltower/odata/diagtrack/Scenarios('06033e1c-3e1c-43af-9d81-1756b6a9d8a8')/ScenarioStatusId
        ////        /// which requires explicit support server-side for each property you can name in this way (otherwise, you have to project into an anonymous type)
        ////        /// 
        ////        /// We can of course add that support, but I couldn't be bothered right now
        ////        /// </summary>
        ////        [TestMethod]
        ////        public void ScenarioProjectSinglePropertyByPrimaryKey()
        ////        {


        ////            var project = odataClient.HouseHistory
        ////                .Where(s => s.Id == validId)
        ////                .Select(s => s.ScenarioStatusId)
        ////                .First();

        ////            Assert.IsTrue(project != null);
        ////        }
        ////#endif

        ////        // no idea why this projection broke, OData can be very fussy, but no one is using projection anyway
        ////        [TestMethod]
        ////        public void ScenarioProjectAnonymousFirstLevelByPrimaryKey()
        ////        {


        ////            var project = odataClient.HouseHistory
        ////                .Where(s => s.Id == validId)
        ////                .Select(s => new { s.ScenarioUpdateDate, s.ScenarioStatusId })
        ////                .First();

        ////            Assert.IsTrue(project != null);
        ////        }

        ////#if DOCUMENTDB_THINKS_NULLABLES_ARE_NOT_VALUE_TYPES_BUG_HAS_BEEN_FIXED
        ////        // What's happening here is that DocumentDB considers DateTimeOffset? (nullable) to not be a value type, so it fails the projection ... 
        ////        // this works above since its hitting the Scenarios[Key=''] endpoint which retrieves the entire doc, and odata then projects out after the DocumentDB query has completed
        ////        //     http://localhost/controltower/odata/diagtrack/Scenarios?$filter=Id%20eq%20'06033e1c-3e1c-43af-9d81-1756b6a9d8a8'%20or%20Id%20eq%20'031a76e9-e6bd-4763-8f36-bf54da6c404b'&$select=ScenarioUpdateDate,ScenarioStatusId
        ////        //     "Instantiation of only value types, anonymous types and spatial types are supported.","type":"Microsoft.Azure.Documents.Linq.DocumentQueryException","stacktrace":"   at Microsoft.Azure.Documents.Linq.ExpressionToSql.VisitNew(NewExpression inputExpression, TranslationContext context)
        ////        // todo: follow up with DocumentDB client team to figure out why this is broken, just because something is nullable doesn't mean its not a value type!
        ////        [TestMethod]
        ////        public void ScenarioProjectAnonymousFirstLevelByMultiplePrimaryKey()
        ////        {
        ////            var validIds = odataClient.HouseHistory.Where(s => s.IsValid == true).Take(2).ToArray();

        ////            var project = odataClient.HouseHistory
        ////                .Where(s => s.Id == validIds[0].Id || s.Id == validIds[1].Id)
        ////                .Select(s => new { s.ScenarioUpdateDate, s.ScenarioStatusId })
        ////                .ToArray();

        ////            Assert.IsTrue(project.Count() == 2);
        ////        }
        ////#endif

        ////#if DOCUMENTDB_THINKS_NULLABLES_ARE_NOT_VALUE_TYPES_BUG_HAS_BEEN_FIXED
        ////        // What's happening here is that DocumentDB considers DateTimeOffset? (nullable) to not be a value type, so it fails the projection ... 
        ////        // this works above since its hitting the Scenarios[Key=''] endpoint which retrieves the entire doc, and odata then projects out after the DocumentDB query has completed
        ////        //     http://localhost/controltower/odata/diagtrack/Scenarios?$filter=ScenarioData/scenario/scenarioid%20eq%20'06033e1c-3e1c-43af-9d81-1756b6a9d8a8'&$top=1&$select=ScenarioUpdateDate,ScenarioStatusId
        ////        //     "Instantiation of only value types, anonymous types and spatial types are supported.","type":"Microsoft.Azure.Documents.Linq.DocumentQueryException","stacktrace":"   at Microsoft.Azure.Documents.Linq.ExpressionToSql.VisitNew(NewExpression inputExpression, TranslationContext context)
        ////        // todo: follow up with DocumentDB client team to figure out why this is broken, just because something is nullable doesn't mean its not a value type!
        ////        [TestMethod]
        ////        public void ScenarioProjectAnonymousFirstLevelByDeepWhereClause()
        ////        {


        ////            var project = odataClient.HouseHistory
        ////                .Where(s => s.ScenarioData.scenario.scenarioid == validId)
        ////                .Select(s => new { s.ScenarioUpdateDate, s.ScenarioStatusId })
        ////                .First();

        ////            Assert.IsTrue(project != null);
        ////        }
        ////#endif

        ////        // no idea why this projection broke, OData can be very fussy, but no one is using projection anyway
        ////        [TestMethod]
        ////        public void ScenarioProjectAnonymousDeepByPrimaryKey()
        ////        {


        ////            var project = odataClient.HouseHistory
        ////                .Where(s => s.Id == validId)
        ////                .Select(s => new { s.ScenarioData.scenario.scenarioname, s.ScenarioData.scenario.scenarioid, s.ScenarioStatusId })
        ////                .First();

        ////            Assert.IsTrue(project != null);
        ////        }

        ////#if ODATA_THINKS_DEEP_SELECT_IS_NOT_ALLOWED_BUG_HAS_BEEN_FIXED
        ////        // It seems the odata client is generating an invalid request, its only sending a partial path for the deep projections 
        ////        //     http://localhost/controltower/odata/diagtrack/Scenarios?$filter=Id%20eq%20'06033e1c-3e1c-43af-9d81-1756b6a9d8a8'%20or%20Id%20eq%20'031a76e9-e6bd-4763-8f36-bf54da6c404b'&$select=ScenarioData,ScenarioData,ScenarioStatusId
        ////        //     and this causes DocumentDB to reject the projection: "Instantiation of only value types, anonymous types and spatial types are supported.","type":"Microsoft.Azure.Documents.Linq.DocumentQueryException","stacktrace":"   at Microsoft.Azure.Documents.Linq.ExpressionToSql.VisitNew(NewExpression inputExpression, TranslationContext context)
        ////        // However, this also does not work:
        ////        //     http://localhost/controltower/odata/diagtrack/Scenarios?$filter=Id%20eq%20'06033e1c-3e1c-43af-9d81-1756b6a9d8a8'%20or%20Id%20eq%20'031a76e9-e6bd-4763-8f36-bf54da6c404b'&$select=ScenarioData/scenario/scenarioname,ScenarioData/scenario/scenarioid,ScenarioStatusId
        ////        //     with odata complaining: "A path within the select or expand query option is not supported.","type":"Microsoft.OData.Core.ODataException","stacktrace":"   at System.Web.OData.Formatter.Serialization.SelectExpandNode.ValidatePathIsSupported(ODataPath path)
        ////        // todo: follow up with OData client team to figure out why this is broken, is projecting a nested property not in spec?
        ////        [TestMethod]
        ////        public void ScenarioProjectAnonymousDeepByMultiplePrimaryKey()
        ////        {
        ////            var validIds = odataClient.HouseHistory.Where(s => s.IsValid == true).Take(2).ToArray();

        ////            var project = odataClient.HouseHistory
        ////                .Where(s => s.Id == validIds[0].Id || s.Id == validIds[1].Id)
        ////                .Select(s => new { s.ScenarioData.scenario.scenarioname, s.ScenarioData.scenario.scenarioid, s.ScenarioStatusId })
        ////                .ToArray();

        ////            Assert.IsTrue(project.Count() == 2);
        ////        }
        ////#endif

        ////#if ODATA_THINKS_DEEP_SELECT_IS_NOT_ALLOWED_BUG_HAS_BEEN_FIXED
        ////        // It seems the odata client is generating an invalid request, its only sending a partial path for the deep projections 
        ////        //     http://localhost/controltower/odata/diagtrack/Scenarios?$filter=ScenarioData/scenario/scenarioid%20eq%20'06033e1c-3e1c-43af-9d81-1756b6a9d8a8'&$top=1&$select=ScenarioData,ScenarioData,ScenarioStatusId
        ////        //     and this causes DocumentDB to reject the projection: "Instantiation of only value types, anonymous types and spatial types are supported.","type":"Microsoft.Azure.Documents.Linq.DocumentQueryException","stacktrace":"   at Microsoft.Azure.Documents.Linq.ExpressionToSql.VisitNew(NewExpression inputExpression, TranslationContext context)
        ////        // However, this also does not work:
        ////        //     http://localhost/controltower/odata/diagtrack/Scenarios?$filter=ScenarioData/scenario/scenarioid%20eq%20'06033e1c-3e1c-43af-9d81-1756b6a9d8a8'&$top=1&$select=ScenarioData/scenario/scenarioname,ScenarioData/scenario/scenarioid,ScenarioStatusId
        ////        //     with odata complaining: "A path within the select or expand query option is not supported.","type":"Microsoft.OData.Core.ODataException","stacktrace":"   at System.Web.OData.Formatter.Serialization.SelectExpandNode.ValidatePathIsSupported(ODataPath path)
        ////        // todo: follow up with OData client team to figure out why this is broken, is projecting a nested property not in spec?
        ////        [TestMethod]
        ////        public void ScenarioProjectAnonymousDeepByDeepWhereClause()
        ////        {


        ////            var project = odataClient.HouseHistory
        ////                .Where(s => s.ScenarioData.scenario.scenarioid == validId)
        ////                .Select(s => new { s.ScenarioData.scenario.scenarioname, s.ScenarioData.scenario.scenarioid, s.ScenarioStatusId })
        ////                .First();

        ////            Assert.IsTrue(project != null);
        ////        }
        ////#endif

        ////        // no idea why this projection broke, OData can be very fussy, but no one is using projection anyway
        ////        [TestMethod]
        ////        public void ScenarioProjectAnonymousRenamedDeepByPrimaryKey()
        ////        {


        ////            var project = odataClient.HouseHistory
        ////                .Where(s => s.Id == validId)
        ////                .Select(s => new { a = s.ScenarioData.scenario.scenarioname, b = s.ScenarioData.scenario.scenarioid, c = s.ScenarioStatusId })
        ////                .First();

        ////            Assert.IsTrue(project != null);
        ////        }

        ////#if ODATA_THINKS_DEEP_SELECT_IS_NOT_ALLOWED_BUG_HAS_BEEN_FIXED
        ////        // It seems the odata client is generating an invalid request, its only sending a partial path for the deep projections 
        ////        //     http://localhost/controltower/odata/diagtrack/Scenarios?$filter=Id%20eq%20'06033e1c-3e1c-43af-9d81-1756b6a9d8a8'%20or%20Id%20eq%20'031a76e9-e6bd-4763-8f36-bf54da6c404b'&$select=ScenarioData,ScenarioData,ScenarioStatusId
        ////        //     and this causes DocumentDB to reject the projection: "Instantiation of only value types, anonymous types and spatial types are supported.","type":"Microsoft.Azure.Documents.Linq.DocumentQueryException","stacktrace":"   at Microsoft.Azure.Documents.Linq.ExpressionToSql.VisitNew(NewExpression inputExpression, TranslationContext context)
        ////        // However, this also does not work:
        ////        //     http://localhost/controltower/odata/diagtrack/Scenarios?$filter=Id%20eq%20'06033e1c-3e1c-43af-9d81-1756b6a9d8a8'%20or%20Id%20eq%20'031a76e9-e6bd-4763-8f36-bf54da6c404b'&$select=ScenarioData/scenario/scenarioname,ScenarioData/scenario/scenarioid,ScenarioStatusId
        ////        //     with odata complaining: "A path within the select or expand query option is not supported.","type":"Microsoft.OData.Core.ODataException","stacktrace":"   at System.Web.OData.Formatter.Serialization.SelectExpandNode.ValidatePathIsSupported(ODataPath path)
        ////        // todo: follow up with OData client team to figure out why this is broken, is projecting a nested property not in spec?
        ////        [TestMethod]
        ////        public void ScenarioProjectAnonymousRenamedDeepByMultiplePrimaryKey()
        ////        {
        ////            var validIds = odataClient.HouseHistory.Where(s => s.IsValid == true).Take(2).ToArray();

        ////            var project = odataClient.HouseHistory
        ////                .Where(s => s.Id == validIds[0].Id || s.Id == validIds[1].Id)
        ////                .Select(s => new { a = s.ScenarioData.scenario.scenarioname, b = s.ScenarioData.scenario.scenarioid, c = s.ScenarioStatusId })
        ////                .ToArray();

        ////            Assert.IsTrue(project.Count() == 2);
        ////        }
        ////#endif

        ////#if ODATA_THINKS_DEEP_SELECT_IS_NOT_ALLOWED_BUG_HAS_BEEN_FIXED
        ////        // It seems the odata client is generating an invalid request, its only sending a partial path for the deep projections 
        ////        //     http://localhost/controltower/odata/diagtrack/Scenarios?$filter=ScenarioData/scenario/scenarioid%20eq%20'06033e1c-3e1c-43af-9d81-1756b6a9d8a8'&$top=1&$select=ScenarioData,ScenarioData,ScenarioStatusId
        ////        //     and this causes DocumentDB to reject the projection: "Instantiation of only value types, anonymous types and spatial types are supported.","type":"Microsoft.Azure.Documents.Linq.DocumentQueryException","stacktrace":"   at Microsoft.Azure.Documents.Linq.ExpressionToSql.VisitNew(NewExpression inputExpression, TranslationContext context)
        ////        // However, this also does not work:
        ////        //     http://localhost/controltower/odata/diagtrack/Scenarios?$filter=ScenarioData/scenario/scenarioid%20eq%20%2706033e1c-3e1c-43af-9d81-1756b6a9d8a8%27&$top=1&$select=ScenarioData/scenario/scenarioname,ScenarioData/scenario/scenarioid,ScenarioStatusId
        ////        //     with odata complaining: "A path within the select or expand query option is not supported.","type":"Microsoft.OData.Core.ODataException","stacktrace":"   at System.Web.OData.Formatter.Serialization.SelectExpandNode.ValidatePathIsSupported(ODataPath path)
        ////        // todo: follow up with OData client team to figure out why this is broken, is projecting a nested property not in spec?
        ////        [TestMethod]
        ////        public void ScenarioProjectAnonymousRenamedDeepByDeepWhereClause()
        ////        {


        ////            var project = odataClient.HouseHistory
        ////                .Where(s => s.ScenarioData.scenario.scenarioid == validId)
        ////                .Select(s => new { a = s.ScenarioData.scenario.scenarioname, b = s.ScenarioData.scenario.scenarioid, c = s.ScenarioStatusId })
        ////                .First();

        ////            Assert.IsTrue(project != null);
        ////        }
        ////#endif
    }
}
