using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.Documents.Linq;
using System.Linq;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Diagnostics;

namespace DocumentDbExtensionsTest
{
    [TestClass]
    public class UnitTests
    {
        [ClassInitialize]
        public static void ClassInitialize(TestContext testContext)
        {

        }

        //[TestMethod]
        //public void BasicQueryInterception()
        //{
        //}

        //[TestMethod]
        //public void CustomShouldRetryWithNonContinuableSproc()
        //{
        //}

        private class TestSerialization
        {
            [JsonProperty]
            [JsonConverter(typeof(DateTimeDocumentDbJsonConverter))]
            public DateTime TestDateTime { get; set; }

            [JsonProperty]
            [JsonConverter(typeof(DateTimeDocumentDbJsonConverter))]
            public DateTimeOffset TestDateTimeOffset { get; set; }
        }

        [TestMethod]
        public void DateTimeSerializationFormatIsCorrect()
        {
            var test = new TestSerialization()
            {
                TestDateTime = new DateTime(2000, 01, 01, 00, 00, 00, DateTimeKind.Utc),
                TestDateTimeOffset = new DateTimeOffset(2000, 01, 01, 00, 00, 00, TimeSpan.Zero),
            };

            var result = JsonConvert.SerializeObject(test, new JsonSerializerSettings() { Formatting = Newtonsoft.Json.Formatting.Indented });

            var expected = @"{
  ""TestDateTime"": ""2000-01-01T00:00:00.000Z"",
  ""TestDateTimeOffset"": ""2000-01-01T00:00:00.000Z""
}";

            Assert.AreEqual(expected, result);
        }
    }
}
