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
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ExampleODataFromDocumentDb.Test
{
    [TestClass]
    public class UnitTestBase
    {
        protected static Uri ApiUri = new Uri("http://localhost:1085/ExampleODataFromDocumentDb");
        protected ExampleODataFromDocumentDbClient odataClient = null;

        protected Guid house1guid = Guid.Parse("7BB002A3-5026-4C0A-A969-9F39D7A2A488");

        protected DateTimeOffset firstDate = DateTimeOffset.Parse("2000-01-01 01:01:01.001Z");
        protected DateTimeOffset secondDate = DateTimeOffset.Parse("2000-02-02 02:02:02.002Z");
        protected DateTimeOffset thirdDate = DateTimeOffset.Parse("2000-03-03 03:03:03.003Z");

        private static void EnsureWebServerIsRunning()
        {
            var metadataUri = ApiUri.AbsoluteUri + "/$metadata";
            var request = (HttpWebRequest)WebRequest.Create(metadataUri);
            request.Credentials = CredentialCache.DefaultCredentials;
            try
            {
                var response = (HttpWebResponse)request.GetResponse();
            }
            catch (WebException we)
            {
                if (we.Status == WebExceptionStatus.ConnectFailure ||
                    we.Status == WebExceptionStatus.NameResolutionFailure ||
                    we.Status == WebExceptionStatus.ProtocolError ||
                    we.Status == WebExceptionStatus.ReceiveFailure ||
                    we.Status == WebExceptionStatus.Timeout)
                {
                    Assert.Fail("The ExampleODataFromDocumentDb webservice must be running on localhost.");
                }
            }
        }

        public static void ClassInitialize(TestContext testContext)
        {
            EnsureWebServerIsRunning();
        }

        private Window MakeTestSkylight(DateTimeOffset? testDate)
        {
            var testSkylight = new Window();
            testSkylight.HeightInches = 36;
            testSkylight.WidthInches = 18;
            testSkylight.InstalledDate = testDate;
            testSkylight.Panes = new ObservableCollection<GlassPane>();
            testSkylight.Panes.Add(new GlassPane());
            testSkylight.Panes[0].HeightInches = 36;
            testSkylight.Panes[0].WidthInches = 18;
            return testSkylight;
        }

        private ObservableCollection<Window> MakeTestWindows(DateTimeOffset? testDate)
        {
            var testPane1 = new GlassPane();
            testPane1.HeightInches = 12;
            testPane1.WidthInches = 12;

            var testPane2 = new GlassPane();
            testPane2.HeightInches = 12;
            testPane2.WidthInches = 12;

            var testWindows = new ObservableCollection<Window>();

            var testWindow1 = new Window();
            testWindow1.HeightInches = 72;
            testWindow1.WidthInches = 80;
            testWindow1.InstalledDate = testDate;
            testWindow1.Panes = new ObservableCollection<GlassPane>();
            testWindow1.Panes.Add(testPane1);
            testWindow1.Panes.Add(testPane2);
            testWindows.Add(testWindow1);

            var testWindow2 = new Window();
            testWindow2.HeightInches = 72;
            testWindow2.WidthInches = 80;
            testWindow2.InstalledDate = testDate;
            testWindow2.Panes = new ObservableCollection<GlassPane>();
            testWindow2.Panes.Add(testPane1);
            testWindow2.Panes.Add(testPane2);
            testWindows.Add(testWindow2);
            return testWindows;
        }

        private void ResetTestItems()
        {
            // delete all houses
            var allHouses = odataClient.Houses.ToList();
            foreach (var house in allHouses)
            {
                odataClient.DeleteObject(house);
                odataClient.SaveChanges();
                odataClient.Detach(house);
            }

            // delete all house history
            var allHouseHistory = odataClient.HouseHistory.ToList();
            foreach (var houseHistory in allHouseHistory)
            {
                odataClient.DeleteObject(houseHistory);
                odataClient.SaveChanges();
                odataClient.Detach(houseHistory);
            }

            // create default data
            var house1 = House.CreateHouse(house1guid.ToString("D"));
            house1.TestName = "house1";
            house1.TestSkylight = MakeTestSkylight(null);
            house1.TestWindows = MakeTestWindows(null);
            odataClient.AddToHouses(house1);
            odataClient.SaveChanges();
            odataClient.Detach(house1);
        }

        protected void CreateHouse1DateTimeTestHistory()
        {
            // update with first DateTime value
            var first = odataClient.Houses.ByKey(house1guid.ToString("D")).GetValue();
            first.TestDateTimeOffset = firstDate;
            first.TestDateTimeOffsetNullable = firstDate;
            first.TestSkylight.InstalledDate = firstDate;
            first.TestWindows[0].InstalledDate = firstDate;
            first.TestWindows[1].InstalledDate = firstDate;
            odataClient.UpdateObject(first);
            odataClient.SaveChanges();
            odataClient.Detach(first);

            // update with second DateTime value
            var second = odataClient.Houses.ByKey(house1guid.ToString("D")).GetValue();
            second.TestDateTimeOffset = secondDate;
            second.TestDateTimeOffsetNullable = secondDate;
            second.TestSkylight.InstalledDate = secondDate;
            second.TestWindows[0].InstalledDate = secondDate;
            second.TestWindows[1].InstalledDate = secondDate;
            odataClient.UpdateObject(second);
            odataClient.SaveChanges();
            odataClient.Detach(second);

            // update with third DateTime value
            var third = odataClient.Houses.ByKey(house1guid.ToString("D")).GetValue();
            third.TestDateTimeOffset = thirdDate;
            third.TestDateTimeOffsetNullable = thirdDate;
            third.TestSkylight.InstalledDate = thirdDate;
            third.TestWindows[0].InstalledDate = thirdDate;
            third.TestWindows[1].InstalledDate = thirdDate;
            odataClient.UpdateObject(third);
            odataClient.SaveChanges();
            odataClient.Detach(third);

            // null them back out and save again (this actually commits the "third" value to history since the pre-update record is persisted on change)
            var final = odataClient.Houses.ByKey(house1guid.ToString("D")).GetValue();
            final.TestDateTimeOffset = DateTimeOffset.MinValue;
            final.TestDateTimeOffsetNullable = null;
            final.TestSkylight.InstalledDate = null;
            final.TestWindows[0].InstalledDate = null;
            final.TestWindows[1].InstalledDate = null;
            odataClient.UpdateObject(final);
            odataClient.SaveChanges();
            odataClient.Detach(final);
        }

        public void TestInitialize()
        {
            odataClient = new ExampleODataFromDocumentDbClient(ApiUri) { Credentials = CredentialCache.DefaultCredentials };

            ResetTestItems();
        }
    }
}
