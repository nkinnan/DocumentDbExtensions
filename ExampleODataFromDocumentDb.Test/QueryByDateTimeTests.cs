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
    /// <summary>
    /// All of this functionality is enabled by the use of DocumentDbExtensions query interception on the server and 
    /// decoration of DateTime fields in the document with DateTimeDocumentDbJsonConverter
    /// </summary>
    [TestClass]
    public class QueryByDateTimeTests : UnitTestBase
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

        [TestMethod]
        public void DateTimeOffsetLessThanGreaterThan()
        {
            var history = odataClient.HouseHistory
                .Where(h => h.ModifiedId == house1guid.ToString("D"))
                .ToList();
            // the four updates
            Assert.AreEqual(4, history.Count);

            // TestDateTimeOffset < 
            var lessThanSecondDate = odataClient.HouseHistory
                .Where(h => h.ModifiedId == house1guid.ToString("D"))
                .Where(h => h.TestDateTimeOffset < secondDate)
                .Where(h => h.TestDateTimeOffset != DateTimeOffset.MinValue) // (since its not nullable)
                .ToList();
            Assert.AreEqual(1, lessThanSecondDate.Count);
            Assert.AreEqual(firstDate, lessThanSecondDate.Single().TestDateTimeOffset);

            // TestDateTimeOffset >
            var greaterThanSecondDate = odataClient.HouseHistory
                .Where(h => h.ModifiedId == house1guid.ToString("D"))
                .Where(h => h.TestDateTimeOffset > secondDate)
                .ToList();
            Assert.AreEqual(1, greaterThanSecondDate.Count);
            Assert.AreEqual(thirdDate, greaterThanSecondDate.Single().TestDateTimeOffset);

            // TestDateTimeOffsetNullable < 
            lessThanSecondDate = odataClient.HouseHistory
                .Where(h => h.ModifiedId == house1guid.ToString("D"))
                .Where(h => h.TestDateTimeOffsetNullable < secondDate)
                .ToList();
            Assert.AreEqual(1, lessThanSecondDate.Count);
            Assert.AreEqual(firstDate, lessThanSecondDate.Single().TestDateTimeOffsetNullable);

            // TestDateTimeOffsetNullable >
            greaterThanSecondDate = odataClient.HouseHistory
                .Where(h => h.ModifiedId == house1guid.ToString("D"))
                .Where(h => h.TestDateTimeOffsetNullable > secondDate)
                .ToList();
            Assert.AreEqual(1, greaterThanSecondDate.Count);
            Assert.AreEqual(thirdDate, greaterThanSecondDate.Single().TestDateTimeOffsetNullable);
        }

        [TestMethod]
        public void DateTimeOffsetLessThanGreaterThanOrEquals()
        {
            var history = odataClient.HouseHistory
                .Where(h => h.ModifiedId == house1guid.ToString("D"))
                .ToList();
            // the four updates
            Assert.AreEqual(4, history.Count);

            // TestDateTimeOffset <=
            var lessThanOrEqualSecondDate = odataClient.HouseHistory
                .Where(h => h.ModifiedId == house1guid.ToString("D"))
                .Where(h => h.TestDateTimeOffset <= secondDate)
                .Where(h => h.TestDateTimeOffset != DateTimeOffset.MinValue) // (since its not nullable)
                .ToList();
            Assert.AreEqual(2, lessThanOrEqualSecondDate.Count);
            Assert.AreEqual(1, lessThanOrEqualSecondDate.Where(x => x.TestDateTimeOffset == firstDate).Count());
            Assert.AreEqual(1, lessThanOrEqualSecondDate.Where(x => x.TestDateTimeOffset == secondDate).Count());

            // TestDateTimeOffset >=
            var greaterThanOrEqualSecondDate = odataClient.HouseHistory
                .Where(h => h.ModifiedId == house1guid.ToString("D"))
                .Where(h => h.TestDateTimeOffset >= secondDate)
                .ToList();
            Assert.AreEqual(2, greaterThanOrEqualSecondDate.Count);
            Assert.AreEqual(1, greaterThanOrEqualSecondDate.Where(x => x.TestDateTimeOffset == secondDate).Count());
            Assert.AreEqual(1, greaterThanOrEqualSecondDate.Where(x => x.TestDateTimeOffset == thirdDate).Count());

            // TestDateTimeOffsetNullable <= 
            lessThanOrEqualSecondDate = odataClient.HouseHistory
                .Where(h => h.ModifiedId == house1guid.ToString("D"))
                .Where(h => h.TestDateTimeOffsetNullable <= secondDate)
                .ToList();
            Assert.AreEqual(2, lessThanOrEqualSecondDate.Count);
            Assert.AreEqual(1, lessThanOrEqualSecondDate.Where(x => x.TestDateTimeOffset == firstDate).Count());
            Assert.AreEqual(1, lessThanOrEqualSecondDate.Where(x => x.TestDateTimeOffset == secondDate).Count());

            // TestDateTimeOffsetNullable >=
            greaterThanOrEqualSecondDate = odataClient.HouseHistory
                .Where(h => h.ModifiedId == house1guid.ToString("D"))
                .Where(h => h.TestDateTimeOffsetNullable >= secondDate)
                .ToList();
            Assert.AreEqual(2, greaterThanOrEqualSecondDate.Count);
            Assert.AreEqual(1, greaterThanOrEqualSecondDate.Where(x => x.TestDateTimeOffset == secondDate).Count());
            Assert.AreEqual(1, greaterThanOrEqualSecondDate.Where(x => x.TestDateTimeOffset == thirdDate).Count());
        }

        [TestMethod]
        public void DateTimeOffsetEqualsAndNotEquals()
        {
            var history = odataClient.HouseHistory
                .Where(h => h.ModifiedId == house1guid.ToString("D"))
                .ToList();
            // the four updates
            Assert.AreEqual(4, history.Count);

            // TestDateTimeOffset == 
            var lessThanSecondDate = odataClient.HouseHistory
                .Where(h => h.ModifiedId == house1guid.ToString("D"))
                .Where(h => h.TestDateTimeOffset == secondDate)
                .ToList();
            Assert.AreEqual(1, lessThanSecondDate.Count);
            Assert.AreEqual(secondDate, lessThanSecondDate.Single().TestDateTimeOffset);

            // TestDateTimeOffset !=
            var greaterThanSecondDate = odataClient.HouseHistory
                .Where(h => h.ModifiedId == house1guid.ToString("D"))
                .Where(h => h.TestDateTimeOffset != firstDate && h.TestDateTimeOffset != thirdDate)
                .Where(h => h.TestDateTimeOffset != DateTimeOffset.MinValue) // (since its not nullable)
                .ToList();
            Assert.AreEqual(1, greaterThanSecondDate.Count);
            Assert.AreEqual(secondDate, greaterThanSecondDate.Single().TestDateTimeOffset);

            // TestDateTimeOffsetNullable == 
            var lessThanSecondDateNullable = odataClient.HouseHistory
                .Where(h => h.ModifiedId == house1guid.ToString("D"))
                .Where(h => h.TestDateTimeOffsetNullable == secondDate)
                .ToList();
            Assert.AreEqual(1, lessThanSecondDateNullable.Count);
            Assert.AreEqual(secondDate, lessThanSecondDateNullable.Single().TestDateTimeOffsetNullable);

            // TestDateTimeOffsetNullable !=
            var greaterThanSecondDateNullable = odataClient.HouseHistory
                .Where(h => h.ModifiedId == house1guid.ToString("D"))
                .Where(h => h.TestDateTimeOffsetNullable != firstDate && h.TestDateTimeOffsetNullable != thirdDate)
                .Where(h => h.TestDateTimeOffsetNullable != null) // (since its nullable)
                .ToList();
            Assert.AreEqual(1, greaterThanSecondDateNullable.Count);
            Assert.AreEqual(secondDate, greaterThanSecondDateNullable.Single().TestDateTimeOffsetNullable);
        }

        [TestMethod]
        public void QueryNestedDateTimeOffset()
        {
            var equalsSecondDate = odataClient.HouseHistory
               .Where(h => h.ModifiedId == house1guid.ToString("D"))
               .Where(h => h.TestSkylight.InstalledDate == secondDate)
               .ToList();
            Assert.AreEqual(1, equalsSecondDate.Count);
            Assert.AreEqual(secondDate, equalsSecondDate.Single().TestSkylight.InstalledDate);
        }

        /// <summary>
        /// Ignored until I can implement sub-select query translation support (once DocDB makes it available!)
        /// </summary>
        [Ignore]
        [TestMethod]
        public void QueryComplexArrayNestedDateTimeOffset()
        {
            var equalsSecondDate = odataClient.HouseHistory
               .Where(h => h.ModifiedId == house1guid.ToString("D"))
               .Where(h => h.TestWindows.Any(w => w.InstalledDate == secondDate))
               .ToList();
            Assert.AreEqual(1, equalsSecondDate.Count);
            Assert.AreEqual(secondDate, equalsSecondDate.Single().TestSkylight.InstalledDate);
        }

        [TestMethod]
        public void DateTimeOffsetOrderBy()
        {
            // sort ascending on DateTimeOffset
            var ascending = odataClient.HouseHistory
                .Where(h => h.TestDateTimeOffset != DateTimeOffset.MinValue) // (since its not nullable)
                .OrderBy(s => s.TestDateTimeOffset)
                .ToArray();
            var first = ascending.First().TestDateTimeOffset.Value; // odata presents these as nullable, but they aren't in the DB
            Assert.AreEqual(firstDate, first);
            var last = ascending.Last().TestDateTimeOffset.Value; // odata presents these as nullable, but they aren't in the DB
            Assert.AreEqual(thirdDate, last);

            // sort descending on DateTimeOffset
            var descending = odataClient.HouseHistory
                .Where(h => h.TestDateTimeOffset != DateTimeOffset.MinValue) // (since its not nullable)
                .OrderByDescending(s => s.TestDateTimeOffset)
                .ToArray();
            first = descending.First().TestDateTimeOffset.Value; // odata presents these as nullable, but they aren't in the DB
            Assert.AreEqual(thirdDate, first);
            last = descending.Last().TestDateTimeOffset.Value; // odata presents these as nullable, but they aren't in the DB
            Assert.AreEqual(firstDate, last);

            // sort ascending on DateTimeOffset (nullable)
            var ascendingNullable = odataClient.HouseHistory
                .Where(s => s.TestDateTimeOffsetNullable != null) // (since its nullable)
                .OrderBy(s => s.TestDateTimeOffsetNullable)
                .ToArray();
            first = ascendingNullable.First().TestDateTimeOffset.Value;
            Assert.AreEqual(firstDate, first);
            last = ascendingNullable.Last().TestDateTimeOffset.Value; 
            Assert.AreEqual(thirdDate, last);

            // sort descending on DateTimeOffset (nullable)
            var descendingNullable = odataClient.HouseHistory
                .Where(s => s.TestDateTimeOffsetNullable != null) // (since its nullable)
                .OrderByDescending(s => s.TestDateTimeOffsetNullable)
                .ToArray();
            first = descendingNullable.First().TestDateTimeOffset.Value;
            Assert.AreEqual(thirdDate, first);
            last = descendingNullable.Last().TestDateTimeOffset.Value; 
            Assert.AreEqual(firstDate, last);
        }
    }
}
