using System;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.OData;
using System.Web.OData.Query;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using System.Collections.Generic;
using System.Net;
using System.Web.Configuration;
using Newtonsoft.Json;
using System.Net.Http;
using Microsoft.Azure.Documents.Linq;

namespace ExampleODataFromDocumentDb.Controllers
{
    public class HousesController : ODataController
    {
        private static string collectionLink = null;
        private static string documentLinkFormat = null;
        private static DocumentClient client;

        private static async Task EnsureClientIsConnected()
        {
            if (client == null)
            {
                var connectionString = WebConfigurationManager.AppSettings["connectionString"];
                var databaseName = WebConfigurationManager.AppSettings["databaseName"];
                var collectionName = WebConfigurationManager.AppSettings["collectionName"];

                collectionLink = string.Format("dbs/{0}/colls/{1}", databaseName, collectionName);
                documentLinkFormat = collectionLink + "/docs/{0}";

                client = await DocumentDB.GetDocumentClient(connectionString, databaseName, collectionName);
            }
        }

        public HousesController()
        {
        }

        /// <summary>
        /// Get: Houses ($filter)
        /// </summary>
        /// <returns></returns>
        [EnableQuery(
            EnableConstantParameterization = false, // IMPORTANT: OData will do "non-standard things" with the expression tree it generates from the URL, breaking DocumentDbExtensions.InterceptQuery if this is set to true
            EnsureStableOrdering = false,           // IMPORTANT: OData will attempt in some cases to order by the primary key (the document id) which DocumentDB can't do for some reason, unless this is set to false
            AllowedQueryOptions = (AllowedQueryOptions)(AllowedQueryOptions.Supported - AllowedQueryOptions.Skip - AllowedQueryOptions.Count))] // NOTE: This just tells OData to return well-formed errors when features not supported by DocumentDB are attempted on the OData endpoint
        public async Task<IQueryable<HouseDto>> Get()
        {
            // get a standard IQueryable from the standard DocumentDB client
            await EnsureClientIsConnected();

            // configure the wrapped queryable to translate exceptions on enumeration (normally you would log this as well) 
            EnumerationExceptionHandler enumerationExceptionHandler = (Exception exception) =>
            {
                throw new HttpResponseException(new HttpResponseMessage()
                {
                    StatusCode = HttpStatusCode.InternalServerError,
                    Content = new StringContent(exception.ToString()),
                });
            };
            // this IQueryable will now work with DateTime/Offset types, follow result paging links, retry on error, and swallow any exceptions
            var safeWrappedQueryable = DocumentDbExtensions.InterceptQuery(
                client.CreateDocumentQuery<HouseDto>(collectionLink), 
                enumerationExceptionHandler);

            // because our collection contains multiple document types, filter on that first
            safeWrappedQueryable = safeWrappedQueryable
                .Where(x => x.DocumentType == DocumentType.House);

            // OData will apply the query from the URL to the returned IQueryable
            return safeWrappedQueryable;
        }

#if false
        /// <summary>
        /// Get: Houses['key'] 
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        [EnableQuery(
            EnableConstantParameterization = false, // IMPORTANT: OData will do "non-standard things" with the expression tree it generates from the URL, breaking DocumentDbExtensions.InterceptQuery if this is set to true
            EnsureStableOrdering = false,           // IMPORTANT: OData will attempt in some cases to order by the primary key (the document id) which DocumentDB can't do for some reason, unless this is set to false
            AllowedQueryOptions = (AllowedQueryOptions)(AllowedQueryOptions.Supported - AllowedQueryOptions.Skip - AllowedQueryOptions.Count))] // NOTE: This just tells OData to return well-formed errors when features not supported by DocumentDB are attempted on the OData endpoint
        public async Task<IQueryable<HouseDto>> Get([FromODataUri] string key, ODataQueryOptions<HouseDto> odataOptions)
        {
            // get a standard IQueryable from the standard DocumentDB client
            await EnsureClientIsConnected();

            // configure the wrapped queryable to translate exceptions on enumeration (normally you would log this as well) 
            EnumerationExceptionHandler enumerationExceptionHandler = (Exception exception) =>
            {
                throw new HttpResponseException(new HttpResponseMessage()
                {
                    StatusCode = HttpStatusCode.InternalServerError,
                    Content = new StringContent(exception.ToString()),
                });
            };
            // this IQueryable will now work with DateTime/Offset types, follow result paging links, retry on error, and swallow any exceptions
            var safeWrappedQueryable = DocumentDbExtensions.InterceptQuery(
                client.CreateDocumentQuery<HouseDto>(collectionLink),
                enumerationExceptionHandler);

            // apply the clause for getting the document by key
            // because our collection contains multiple document types, filter on that as well
            // execute the query safely with continuation and retries - you could also wrap the IQueryable using InterceptQuery and simply call ".ToArray()"
            safeWrappedQueryable = safeWrappedQueryable
                .Where(x => x.DocumentType == DocumentType.House)
                .Where(x => x.Id == key);

            // todo: bug bug
            //if (results.Count == 0)
            //    throw new HttpResponseException(HttpStatusCode.NotFound);

            // todo: bug bug
            //// return "not modified" if supplied etag matches
            //if (odataOptions.IfNoneMatch != null)
            //{
            //    if (results.Single().ETag == (string)odataOptions.IfNoneMatch["ETag"])
            //        throw new HttpResponseException(HttpStatusCode.NotModified);
            //}

            return safeWrappedQueryable;
        }
#else
        /// <summary>
        /// Get: Houses['key'] 
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        [EnableQuery(
            EnableConstantParameterization = false, // IMPORTANT: OData will do "non-standard things" with the expression tree it generates from the URL, breaking DocumentDbExtensions.InterceptQuery if this is set to true
            EnsureStableOrdering = false,           // IMPORTANT: OData will attempt in some cases to order by the primary key (the document id) which DocumentDB can't do for some reason, unless this is set to false
            AllowedQueryOptions = (AllowedQueryOptions)(AllowedQueryOptions.Supported - AllowedQueryOptions.Skip - AllowedQueryOptions.Count))] // NOTE: This just tells OData to return well-formed errors when features not supported by DocumentDB are attempted on the OData endpoint
        public async Task<SingleResult<HouseDto>> Get([FromODataUri] string key, ODataQueryOptions<HouseDto> odataOptions)
        {
            // get a standard IQueryable from the standard DocumentDB client
            await EnsureClientIsConnected();

            // apply the clause for getting the document by key
            // because our collection contains multiple document types, filter on that as well
            // execute the query safely with continuation and retries - you could also wrap the IQueryable using InterceptQuery and simply call ".ToArray()"
            var results = await DocumentDbExtensions.ExecuteQueryWithContinuationAndRetryAsync(
                client.CreateDocumentQuery<HouseDto>(collectionLink)
                .Where(x => x.DocumentType == DocumentType.House)
                .Where(x => x.Id == key));

            if (results.Count == 0)
                throw new HttpResponseException(HttpStatusCode.NotFound);

            // return "not modified" if supplied etag matches
            if (odataOptions.IfNoneMatch != null)
            {
                if (results.Single().ETag == (string)odataOptions.IfNoneMatch["ETag"])
                    throw new HttpResponseException(HttpStatusCode.NotModified);
            }

            return SingleResult.Create(results.AsQueryable());
        }
#endif

        /// <summary>
        /// Post (create new): House
        /// </summary>
        /// <param name="house"></param>
        /// <returns></returns>
        [HttpPost]
        public async Task<IHttpActionResult> Post(HouseDto houseDto)
        {
            // get a standard DocumentDB client
            await EnsureClientIsConnected();

            // convert the DTO into the Document type
            var houseDoc = JsonConvert.DeserializeObject<HouseDocument>(JsonConvert.SerializeObject(houseDto));

            // DocumentDB Triggers are "opt-in", so opt-in
            var docdbOptions = new RequestOptions();
            docdbOptions.PreTriggerInclude = new List<string> { "MaintainHistoryAndTimestamps" };

            // execute the create document call safely with retries
            ResourceResponse<Document> created = null;
            try
            {
                created = await DocumentDbExtensions.ExecuteMethodWithRetryAsync(() =>
                client.CreateDocumentAsync(collectionLink, houseDoc, docdbOptions));
            }
            catch(DocumentDbNonRetriableResponse e)
            {
                return StatusCode((e.InnerException as DocumentClientException).StatusCode ?? HttpStatusCode.InternalServerError);
            }

            // get the resulting document (it is returned above, but in non-typed form - probably should really be using AutoMapper or something like that here)
            var result = await DocumentDbExtensions.ExecuteQueryWithContinuationAndRetryAsync(
                client.CreateDocumentQuery<HouseDto>(collectionLink)
                .Where(x => x.DocumentType == DocumentType.House)
                .Where(x => x.Id == created.Resource.Id));

            return Created(result.Single());
        }

        /// <summary>
        /// Patch (update): House
        /// 
        /// IMPORTANT: OData does not yet support "deep updates", this will only work on first-level properties
        /// </summary>
        /// <param name="key"></param>
        /// <param name="update"></param>
        /// <returns></returns>
        [HttpPatch]
        public async Task<IHttpActionResult> Patch([FromODataUri] string key, Delta<HouseDto> dtoUpdate, ODataQueryOptions<HouseDto> odataOptions)
        {
            // get a standard IQueryable from the standard DocumentDB client
            await EnsureClientIsConnected();

            // apply the clause for getting the document by key
            // because our collection contains multiple document types, filter on that as well
            // execute the query safely with continuation and retries
            var results = await DocumentDbExtensions.ExecuteQueryWithContinuationAndRetryAsync(
                client.CreateDocumentQuery<HouseDto>(collectionLink)
                .Where(x => x.DocumentType == DocumentType.House)
                .Where(x => x.Id == key));

            if (results.Count == 0)
                return NotFound();

            // patch the original with the delta
            var original = results.Single();
            dtoUpdate.Patch(original);

            // convert the updated DTO into the Document type
            var updatedDoc = JsonConvert.DeserializeObject<HouseDocument>(JsonConvert.SerializeObject(original));

            // DocumentDB Triggers are "opt-in", so opt-in
            var docdbOptions = new RequestOptions();
            docdbOptions.PreTriggerInclude = new List<string> { "MaintainHistoryAndTimestamps" };

            // DocumentDB ETag checks are "opt-in", so opt-in if requested
            if (odataOptions.IfMatch != null)
            {
                docdbOptions.AccessCondition = new AccessCondition()
                {
                    Type = AccessConditionType.IfMatch,
                    Condition = (string)odataOptions.IfMatch["ETag"],
                };
            }

            ResourceResponse<Document> updated = null;
            try
            {
                // execute the replace document call safely with retries
                string documentLink = string.Format(documentLinkFormat, key);
                updated = await DocumentDbExtensions.ExecuteMethodWithRetryAsync(() =>
                    client.ReplaceDocumentAsync(documentLink, updatedDoc, docdbOptions));
            }
            catch(DocumentDbNonRetriableResponse e)
            {
                return StatusCode((e.InnerException as DocumentClientException).StatusCode ?? HttpStatusCode.InternalServerError);
            }

            // get the resulting document (it is returned above, but in non-typed form - probably should really be using AutoMapper or something like that here)
            var result = await DocumentDbExtensions.ExecuteQueryWithContinuationAndRetryAsync(
                client.CreateDocumentQuery<HouseDto>(collectionLink)
                .Where(x => x.DocumentType == DocumentType.House)
                .Where(x => x.Id == updated.Resource.Id));

            return Updated(result.Single());
        }

        /// <summary>
        /// Put (replace): House
        /// </summary>
        /// <param name="key"></param>
        /// <param name="house"></param>
        /// <returns></returns>
        [HttpPut]
        public async Task<IHttpActionResult> Put([FromODataUri] string key, HouseDto houseDto, ODataQueryOptions<HouseDto> odataOptions)
        {
            // get a standard DocumentDB client
            await EnsureClientIsConnected();

            // convert the DTO into the Document type
            var houseDoc = JsonConvert.DeserializeObject<HouseDocument>(JsonConvert.SerializeObject(houseDto));

            // DocumentDB Triggers are "opt-in", so opt-in
            var docdbOptions = new RequestOptions();
            docdbOptions.PreTriggerInclude = new List<string> { "MaintainHistoryAndTimestamps" };

            // DocumentDB ETag checks are "opt-in", so opt-in if requested
            if (odataOptions.IfMatch != null)
            {
                docdbOptions.AccessCondition = new AccessCondition()
                {
                    Type = AccessConditionType.IfMatch,
                    Condition = (string)odataOptions.IfMatch["ETag"],
                };
            }

            // execute the replace document call safely with retries
            string documentLink = string.Format(documentLinkFormat, key);
            ResourceResponse<Document> replaced = null;
            try
            {
                replaced = await DocumentDbExtensions.ExecuteMethodWithRetryAsync(() =>
                    client.ReplaceDocumentAsync(documentLink, houseDoc, docdbOptions));
            }
            catch(DocumentDbNonRetriableResponse e)
            {
                return StatusCode((e.InnerException as DocumentClientException).StatusCode ?? HttpStatusCode.InternalServerError);
            }

            // get the resulting document (it is returned above, but in non-typed form - probably should really be using AutoMapper or something like that here)
            var result = await DocumentDbExtensions.ExecuteQueryWithContinuationAndRetryAsync(
                client.CreateDocumentQuery<HouseDto>(collectionLink)
                .Where(x => x.DocumentType == DocumentType.House)
                .Where(x => x.Id == replaced.Resource.Id));

            return Updated(result.Single());
        }

        /// <summary>
        /// Delete: House
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        [HttpDelete]
        public async Task<IHttpActionResult> Delete([FromODataUri] string key, ODataQueryOptions<HouseDto> odataOptions)
        {
            // get a standard DocumentDB client
            await EnsureClientIsConnected();

            // DocumentDB Triggers are "opt-in", so opt-in
            var docdbOptions = new RequestOptions();
            docdbOptions.PreTriggerInclude = new List<string> { "MaintainHistoryAndTimestamps" };

            // DocumentDB ETag checks are "opt-in", so opt-in if requested
            if (odataOptions.IfMatch != null)
            {
                docdbOptions.AccessCondition = new AccessCondition()
                {
                    Type = AccessConditionType.IfMatch,
                    Condition = (string)odataOptions.IfMatch["ETag"],
                };
            }

            // execute the delete document call safely with retries
            string documentLink = string.Format(documentLinkFormat, key);
            try
            {
                await DocumentDbExtensions.ExecuteMethodWithRetryAsync(() =>
                    client.DeleteDocumentAsync(documentLink, docdbOptions));
            }
            catch(DocumentDbNonRetriableResponse e)
            {
                return StatusCode((e.InnerException as DocumentClientException).StatusCode ?? HttpStatusCode.InternalServerError);
            }

            return Ok();
        }
    }
}
