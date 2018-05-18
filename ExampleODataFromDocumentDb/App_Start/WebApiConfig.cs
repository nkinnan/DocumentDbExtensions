using Microsoft.Azure.Documents;
using Microsoft.OData.Edm;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Configuration;
using System.Web.Http;
using System.Web.OData;
using System.Web.OData.Builder;
using System.Web.OData.Extensions;

namespace ExampleODataFromDocumentDb
{
    public static class WebApiConfig
    {
        /// <summary>
        /// The standard entry point, only modified in this example to add calls to UpdateDocumentDbResources() and RegisterDocumentDbOData()
        /// </summary>
        /// <param name="config"></param>
        public static void Register(HttpConfiguration config)
        {
            // Web API configuration and services

            // Web API routes
            config.MapHttpAttributeRoutes();

            config.Routes.MapHttpRoute(
                name: "DefaultApi",
                routeTemplate: "api/{controller}/{id}",
                defaults: new { id = RouteParameter.Optional }
            );

            UpdateDocumentDbResources().Wait();
            RegisterDocumentDbOData(config);
        }

        /// <summary>
        /// This will update any DocumentDB resources like Triggers and Sprocs, which are required for this example
        /// </summary>
        private static async Task UpdateDocumentDbResources()
        {
            var connectionString = WebConfigurationManager.AppSettings["connectionString"];
            var databaseName = WebConfigurationManager.AppSettings["databaseName"];
            var collectionName = WebConfigurationManager.AppSettings["collectionName"];

            var collectionLink = string.Format("dbs/{0}/colls/{1}", databaseName, collectionName);

            var client = await DocumentDB.GetDocumentClient(connectionString, databaseName, collectionName);

            // update the sproc
            await DocumentDbExtensions.ExecuteResultWithRetryAsync(() =>
                client.UpsertTriggerAsync(collectionLink, DocumentDB.maintainHistoryAndTimestampsTrigger));
        }

        /// <summary>
        /// This will register the example OData endpoint with WebApi and set a few custom behaviors
        /// </summary>
        /// <param name="config"></param>
        private static void RegisterDocumentDbOData(HttpConfiguration config)
        {
            ////// don't require to prefix namespace on method/function invoke in the URL, this just makes your URLs easier to type/read
            ////config.EnableUnqualifiedNameCall(true);

            ////// don't require to prefix namespace on enums in the URL, this just makes your URLs easier to type/read
            ////config.EnableEnumPrefixFree(true);

            // it is not uncommon to have a nullable collection as part of your document, OData spec says this is not valid and all collections should be empty but never null,
            // fortunately there is an option to enable OData to ignore null collections on serialization instead of throwing an error
            config.SetSerializeNullDynamicProperty(false);

            // backwards compat thing
            config.Count().Filter().OrderBy().Expand().Select().MaxTop(null); 

            // setup up OData entities, functions, and actions
            ODataModelBuilder diagTrackBuilder = new ODataConventionModelBuilder();

            // you may override the namespace used in the OData $metadata, which is also the namespace any codegened client will use (see "ExampleODataFromDocumentDb.Client")
            diagTrackBuilder.Namespace = "ExampleODataFromDocumentDb";
            // you may override the container name used in the OData $metadata, which is the main "class" name that any codegened client will use (see "ExampleODataFromDocumentDb.Client")
            diagTrackBuilder.ContainerName = "ExampleODataFromDocumentDbClient";

            // set up the "Houses" collection of type HouseDto
            var housesEntitySetConfig = diagTrackBuilder.EntitySet<HouseDto>("Houses");
            // you may override the type name used in the OData $metadata, for example if you're using DTOs and you class is called "HouseDto" instead of "House"
            housesEntitySetConfig.EntityType.Name = "House";

            // set up the "HouseHistory" collection of type HouseHistoryDto
            var houseHistoryEntitySetConfig = diagTrackBuilder.EntitySet<HouseHistoryDto>("HouseHistory");
            // you may override the type name used in the OData $metadata, for example if you're using DTOs and you class is called "HouseHistoryDto" instead of "HouseHistory"
            houseHistoryEntitySetConfig.EntityType.Name = "HouseHistory";

            // you could do many other things here like add custom Actions and Functions, either bound to a collection or "unbound" at the root odata route level, alias properties, etc., OData is very flexible and powerful (though figuring out the proper syntax can sometimes be frustrating!)

            // map the OData route
            IEdmModel model = diagTrackBuilder.GetEdmModel();
            var routeName = "ExampleODataFromDocumentDb";
            var routePrefix = "ExampleODataFromDocumentDb";
            config.MapODataServiceRoute(routeName, routePrefix, model);
        }
    }
}
