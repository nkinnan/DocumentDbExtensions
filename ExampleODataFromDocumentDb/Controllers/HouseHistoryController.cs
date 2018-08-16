using System;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.OData;
using System.Web.OData.Query;
using Microsoft.Azure.Documents;
using System.Collections.Generic;
using System.Net;
using Microsoft.Azure.Documents.Client;
using System.Web.Configuration;
using Microsoft.Azure.Documents.Linq;
using System.Net.Http;

namespace ExampleODataFromDocumentDb.Controllers
{
    public class HouseHistoryController : ODataController
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

        public HouseHistoryController()
        {
        }

        /// <summary>
        /// Get: HouseHistory ($filter)
        /// </summary>
        /// <returns></returns>
        [EnableQuery(
            EnableConstantParameterization = false, // IMPORTANT: OData will do "non-standard things" with the expression tree it generates from the URL, breaking DocumentDbExtensions.InterceptQuery if this is set to true
            EnsureStableOrdering = false,           // IMPORTANT: OData will attempt in some cases to order by the primary key (the document id) which DocumentDB can't do for some reason, unless this is set to false
            AllowedQueryOptions = (AllowedQueryOptions)(AllowedQueryOptions.Supported - AllowedQueryOptions.Skip - AllowedQueryOptions.Count))] // NOTE: This just tells OData to return well-formed errors when features not supported by DocumentDB are attempted on the OData endpoint
        public async Task<IQueryable<HouseHistoryDto>> Get()
        {
            // get a standard IQueryable from the standard DocumentDB client
            await EnsureClientIsConnected();

            // configure the wrapped queryable to translate exceptions on enumeration (normally you would log this as well) 
            EnumerationExceptionHandler enumerationExceptionHandler = (FeedResponseContext context, Exception exception) =>
            {
                throw new HttpResponseException(new HttpResponseMessage()
                {
                    StatusCode = HttpStatusCode.InternalServerError,
                    Content = new StringContent(exception.ToString()),
                });
            };
            // this IQueryable will now work with DateTime/Offset types, follow result paging links, retry on error, and swallow any exceptions
            var safeWrappedQueryable = DocumentDbExtensions.InterceptQuery(
                client.CreateDocumentQuery<HouseHistoryDto>(collectionLink),
                enumerationExceptionHandler: enumerationExceptionHandler);

            // because our collection contains multiple document types, filter on that first
            safeWrappedQueryable = safeWrappedQueryable
                .Where(x => x.DocumentType == DocumentType.HouseHistory);

            // OData will apply the query from the URL to the returned IQueryable
            return safeWrappedQueryable;
        }

#if false
        /// <summary>
        /// Get: HouseHistory['key'] 
        /// 
        /// Note: returns a *list* of documents that is all history records for the original modified document ID
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        [EnableQuery(
            EnableConstantParameterization = false, // IMPORTANT: OData will do "non-standard things" with the expression tree it generates from the URL, breaking DocumentDbExtensions.InterceptQuery if this is set to true
            EnsureStableOrdering = false,           // IMPORTANT: OData will attempt in some cases to order by the primary key (the document id) which DocumentDB can't do for some reason, unless this is set to false
            AllowedQueryOptions = (AllowedQueryOptions)(AllowedQueryOptions.Supported - AllowedQueryOptions.Skip - AllowedQueryOptions.Count))] // NOTE: This just tells OData to return well-formed errors when features not supported by DocumentDB are attempted on the OData endpoint
        public async Task<IQueryable<HouseHistoryDto>> Get([FromODataUri] string key)
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
                client.CreateDocumentQuery<HouseHistoryDto>(collectionLink),
                enumerationExceptionHandler);

            // apply the clause for getting the document by key
            // because our collection contains multiple document types, filter on that as well
            // execute the query safely with continuation and retries - you could also wrap the IQueryable using InterceptQuery and simply call ".ToArray()"
            safeWrappedQueryable = safeWrappedQueryable
                .Where(x => x.DocumentType == DocumentType.HouseHistory)
                .Where(x => x.ModifiedId == key);

            // todo: bug bug
            //if (results.Count == 0)
            //    throw new HttpResponseException(HttpStatusCode.NotFound);

            return safeWrappedQueryable;
        }
#else
        /// <summary>
        /// Get: HouseHistory['key'] 
        /// 
        /// Note: returns a *list* of documents that is all history records for the original modified document ID
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        [EnableQuery(
            EnableConstantParameterization = false, // IMPORTANT: OData will do "non-standard things" with the expression tree it generates from the URL, breaking DocumentDbExtensions.InterceptQuery if this is set to true
            EnsureStableOrdering = false,           // IMPORTANT: OData will attempt in some cases to order by the primary key (the document id) which DocumentDB can't do for some reason, unless this is set to false
            AllowedQueryOptions = (AllowedQueryOptions)(AllowedQueryOptions.Supported - AllowedQueryOptions.Skip - AllowedQueryOptions.Count))] // NOTE: This just tells OData to return well-formed errors when features not supported by DocumentDB are attempted on the OData endpoint
        public async Task<IEnumerable<HouseHistoryDto>> Get([FromODataUri] string key)
        {
            // get a standard IQueryable from the standard DocumentDB client
            await EnsureClientIsConnected();

            // apply the clause for getting the document by key
            // because our collection contains multiple document types, filter on that as well
            // execute the query safely with continuation and retries - you could also wrap the IQueryable using InterceptQuery and simply call ".ToArray()"
            var results = await DocumentDbExtensions.ExecuteQueryWithContinuationAndRetryAsync(
                client.CreateDocumentQuery<HouseHistoryDto>(collectionLink)
                .Where(x => x.DocumentType == DocumentType.HouseHistory)
                .Where(x => x.ModifiedId == key));

            if (results.Count == 0)
                throw new HttpResponseException(HttpStatusCode.NotFound);

            return results.AsQueryable();
        }
#endif

        /// <summary>
        /// Post (create new): HouseHistory
        /// </summary>
        /// <param name="houseHistory"></param>
        /// <returns></returns>
        [HttpPost]
        public IHttpActionResult Post(HouseHistoryDto houseHistoryDto)
        {
            return StatusCode(System.Net.HttpStatusCode.NotImplemented);
        }

        /// <summary>
        /// Patch (update): HouseHistory
        /// </summary>
        /// <param name="key"></param>
        /// <param name="update"></param>
        /// <returns></returns>
        [HttpPatch]
        public IHttpActionResult Patch([FromODataUri] string key, Delta<HouseHistoryDto> dtoUpdate)
        {
            return StatusCode(System.Net.HttpStatusCode.NotImplemented);
        }

        /// <summary>
        /// Put (replace): HouseHistory
        /// </summary>
        /// <param name="key"></param>
        /// <param name="houseHistory"></param>
        /// <returns></returns>
        [HttpPut]
        public IHttpActionResult Put([FromODataUri] string key, HouseHistoryDto houseHistoryDto)
        {
            return StatusCode(System.Net.HttpStatusCode.NotImplemented);
        }

        /// <summary>
        /// Delete: HouseHistory
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        [HttpDelete]
        public async Task<IHttpActionResult> Delete([FromODataUri] string key)
        {
            // get a standard DocumentDB client
            await EnsureClientIsConnected();

            // execute the delete document call safely with retries
            string documentLink = string.Format(documentLinkFormat, key);
            try
            {
                await DocumentDbExtensions.ExecuteResultWithRetryAsync(() =>
                    client.DeleteDocumentAsync(documentLink));
            }
            catch (DocumentDbNonRetriableResponse e)
            {
                return StatusCode((e.InnerException as DocumentClientException).StatusCode ?? HttpStatusCode.InternalServerError);
            }

            return Ok();
        }
    }
}
