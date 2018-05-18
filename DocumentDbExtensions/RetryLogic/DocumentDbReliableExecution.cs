using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.Documents.Linq;
using Microsoft.Azure.Documents.Client;
using System.Diagnostics;

namespace Microsoft.Azure.Documents
{
    internal static class DocumentDbReliableExecution
    {
        internal const string BadQueryableMessage = "Queryable does not appear to be from DocumentDB, did you perhaps pass a queryable that was already intercepted by this library to an execute call which is meant for unwrapped DocumentDB queryables?  If so, you can simply call .ToArray() or equivalent since you already have a 'safe' wrapped queryable.  The main reason to use an un-wrapped DocumentDB queryable and then call one of the execute methods meant for bare DocumentDB queryables is to be able to get the results using an async Task<> since queryables do not support asyncronous execution.  Note however that this means results won't be paged/streamed in but fully streamed into memory before the async execute method returns.";

        internal static async Task<DocumentsPage<R>> BeginPagingWithRetry<R>(IDocumentQuery<R> query, QueryExecutionHandler queryExecutionHandler, EnumerationExceptionHandler enumerationExceptionHandler, FeedResponseHandler feedResponseHandler, int maxRetries, TimeSpan maxTime, ShouldRetry shouldRetry)
        {
            queryExecutionHandler(query.ToString());

            feedResponseHandler(FeedResponseType.BeforeEnumeration, null);

            FeedResponse<R> firstPageResults = null;
            while (firstPageResults == null)
            {
                try
                {
                    firstPageResults = await DocumentDbReliableExecution.ExecuteResultWithRetry(() =>
                    query.ExecuteNextAsync<R>(),
                    null,
                    maxRetries,
                    maxTime,
                    shouldRetry);
                }
                catch (Exception ex)
                {
                    bool handled = enumerationExceptionHandler(ex);
                    if (!handled)
                    {
                        feedResponseHandler(FeedResponseType.EnumerationAborted, null);
                        throw;
                    }
                }
            }

            // lots of interesting info in intermediateResults such as RU usage, etc.
            feedResponseHandler(FeedResponseType.PageReceived, new FeedResponseWrapper<R>(firstPageResults));

            return new DocumentsPage<R>(firstPageResults.ToList(), firstPageResults.ResponseContinuation);
        }

        internal static async Task<DocumentsPage<R>> GetNextPageWithRetry<R>(IDocumentQuery<R> query, QueryExecutionHandler queryExecutionHandler, EnumerationExceptionHandler enumerationExceptionHandler, FeedResponseHandler feedResponseHandler, int maxRetries, TimeSpan maxTime, ShouldRetry shouldRetry)
        {
            FeedResponse<R> nextPageResults = null;
            while (nextPageResults == null)
            {
                try
                {
                    nextPageResults = await DocumentDbReliableExecution.ExecuteResultWithRetry(() =>
                    query.ExecuteNextAsync<R>(),
                    null,
                    maxRetries,
                    maxTime,
                    shouldRetry);
                }
                catch (Exception ex)
                {
                    bool handled = enumerationExceptionHandler(ex);
                    if (!handled)
                    {
                        feedResponseHandler(FeedResponseType.EnumerationAborted, null);
                        throw;
                    }
                }
            }

            // lots of interesting info in intermediateResults such as RU usage, etc.
            feedResponseHandler(FeedResponseType.PageReceived, new FeedResponseWrapper<R>(nextPageResults));

            if (!query.HasMoreResults)
            {
                feedResponseHandler(FeedResponseType.AfterEnumeration, null);
            }

            return new DocumentsPage<R>(nextPageResults.ToList(), query.HasMoreResults ? nextPageResults.ResponseContinuation : null);
        }

        /// <summary>
        /// This will execute a DocumentDB query in the form of an IQueryable (Linq form) and return the results.
        /// 
        /// It handles paging, continuation tokens, and retriable errors such as "too many requests" for you,
        /// while aggregating all query results in-memory before returning.
        /// </summary>
        /// <typeparam name="R"></typeparam>
        /// <param name="queryable"></param>
        /// <param name="queryExecutionHandler"></param>
        /// <param name="enumerationExceptionHandler"></param>
        /// <param name="feedResponseHandler"></param>
        /// <param name="maxRetries"></param>
        /// <param name="maxTime"></param>
        /// <param name="shouldRetry"></param>
        /// <returns></returns>
        public static async Task<IList<R>> ExecuteQueryWithContinuationAndRetry<R>(IQueryable<R> queryable, QueryExecutionHandler queryExecutionHandler, EnumerationExceptionHandler enumerationExceptionHandler, FeedResponseHandler feedResponseHandler, int maxRetries, TimeSpan maxTime, ShouldRetry shouldRetry)
        {
            IDocumentQuery<R> query = null;
            try
            {
                query = queryable.AsDocumentQuery();
            }
            catch (Exception e)
            {
                throw new ArgumentException(BadQueryableMessage, e);
            }

            queryExecutionHandler(query.ToString());

            feedResponseHandler(FeedResponseType.BeforeEnumeration, null);

            var allResults = new List<R>();

            while (query.HasMoreResults)
            {
                FeedResponse<R> intermediateResults = null;
                try
                {
                    intermediateResults = await DocumentDbReliableExecution.ExecuteResultWithRetry(() =>
                    query.ExecuteNextAsync<R>(),
                    null,
                    maxRetries,
                    maxTime,
                    shouldRetry);
                }
                catch (Exception ex)
                {
                    bool handled = enumerationExceptionHandler(ex);
                    if (!handled)
                    {
                        feedResponseHandler(FeedResponseType.EnumerationAborted, null);
                        throw;
                    }
                    else
                        break;
                }

                // lots of interesting info in intermediateResults such as RU usage, etc.
                feedResponseHandler(FeedResponseType.PageReceived, new FeedResponseWrapper<R>(intermediateResults));

                allResults.AddRange(intermediateResults);
            }

            feedResponseHandler(FeedResponseType.AfterEnumeration, null);

            return allResults;
        }

        /// <summary>
        /// This will execute a DocumentDB query in the form of an IQueryable (Linq form) and return the results.
        /// 
        /// It handles paging, continuation tokens, and retriable errors such as "too many requests" for you,
        /// while streaming query results out in chunks via IEnumerable / yield.
        /// </summary>
        /// <typeparam name="R"></typeparam>
        /// <param name="queryable"></param>
        /// <param name="queryExecutionHandler"></param>
        /// <param name="enumerationExceptionHandler"></param>
        /// <param name="feedResponseHandler"></param>
        /// <param name="maxRetries"></param>
        /// <param name="maxTime"></param>
        /// <param name="shouldRetry"></param>
        /// <returns></returns>
        public static IEnumerable<R> StreamQueryWithContinuationAndRetry<R>(IQueryable<R> queryable, QueryExecutionHandler queryExecutionHandler, EnumerationExceptionHandler enumerationExceptionHandler, FeedResponseHandler feedResponseHandler, int maxRetries, TimeSpan maxTime, ShouldRetry shouldRetry)
        {
            IDocumentQuery<R> query = null;
            try
            {
                query = queryable.AsDocumentQuery();
            }
            catch (Exception e)
            {
                throw new ArgumentException(BadQueryableMessage, e);
            }

            queryExecutionHandler(query.ToString());

            feedResponseHandler(FeedResponseType.BeforeEnumeration, null);

            while (query.HasMoreResults)
            {
                Task<FeedResponse<R>> t;
                try
                {
                    t = Task.Run(async () => await ExecuteResultWithRetry(() =>
                        query.ExecuteNextAsync<R>(),
                        null,
                        maxRetries,
                        maxTime,
                        shouldRetry));

                    t.Wait();
                }
                catch (Exception ex)
                {
                    // note: if an IQueryable is returned to OData, throwing an exception here will cause it to take down w3wp.exe 

                    bool handled = enumerationExceptionHandler(ex);
                    if (!handled)
                    {
                        feedResponseHandler(FeedResponseType.EnumerationAborted, null);
                        throw;
                    }
                    else
                        break;
                }

                var intermediateResults = t.Result;

                // lots of interesting info in intermediateResults such as RU usage, etc.
                feedResponseHandler(FeedResponseType.PageReceived, new FeedResponseWrapper<R>(intermediateResults));

                foreach (var result in intermediateResults)
                {
                    yield return result;
                }
            }

            feedResponseHandler(FeedResponseType.AfterEnumeration, null);
        }

        /// <summary>
        /// This will execute a DocumentDB FeedResponse method return the results.
        /// 
        /// It handles paging, continuation tokens, and retriable errors such as "too many requests" for you,
        /// while aggregating all query results in-memory before returning.
        /// </summary>
        /// <typeparam name="R"></typeparam>
        /// <param name="feedTakingContinuation"></param>
        /// <param name="enumerationExceptionHandler"></param>
        /// <param name="feedResponseHandler"></param>
        /// <param name="maxRetries"></param>
        /// <param name="maxTime"></param>
        /// <param name="shouldRetry"></param>
        /// <returns></returns>
        public static async Task<IList<R>> ExecuteFeedWithContinuationAndRetry<R>(Func<string, FeedResponse<R>> feedTakingContinuation, EnumerationExceptionHandler enumerationExceptionHandler, FeedResponseHandler feedResponseHandler, int maxRetries, TimeSpan maxTime, ShouldRetry shouldRetry)
        {
            return await ExecuteFeedWithContinuationAndRetry<R>((continuation) => Task<R>.Run(() => feedTakingContinuation(continuation)), enumerationExceptionHandler, feedResponseHandler, maxRetries, maxTime, shouldRetry);
        }

        /// <summary>
        /// This will execute a DocumentDB FeedResponse method return the results.
        /// 
        /// It handles paging, continuation tokens, and retriable errors such as "too many requests" for you,
        /// while aggregating all query results in-memory before returning.
        /// </summary>
        /// <typeparam name="R"></typeparam>
        /// <param name="feedTakingContinuation"></param>
        /// <param name="enumerationExceptionHandler"></param>
        /// <param name="feedResponseHandler"></param>
        /// <param name="maxRetries"></param>
        /// <param name="maxTime"></param>
        /// <param name="shouldRetry"></param>
        /// <returns></returns>
        public static async Task<IList<R>> ExecuteFeedWithContinuationAndRetry<R>(Func<string, Task<FeedResponse<R>>> feedTakingContinuation, EnumerationExceptionHandler enumerationExceptionHandler, FeedResponseHandler feedResponseHandler, int maxRetries, TimeSpan maxTime, ShouldRetry shouldRetry)
        {
            feedResponseHandler(FeedResponseType.BeforeEnumeration, null);

            var allResults = new List<R>();

            FeedResponse<R> intermediateResults = null;
            do
            {
                try
                {
                    intermediateResults = await DocumentDbReliableExecution.ExecuteResultWithRetry(() =>
                    feedTakingContinuation(intermediateResults?.ResponseContinuation),
                    null,
                    maxRetries,
                    maxTime,
                    shouldRetry);
                }
                catch (Exception ex)
                {
                    bool handled = enumerationExceptionHandler(ex);
                    if (!handled)
                    {
                        feedResponseHandler(FeedResponseType.EnumerationAborted, null);
                        throw;
                    }
                    else
                        break;
                }

                // lots of interesting info in intermediateResults such as RU usage, etc.
                feedResponseHandler(FeedResponseType.PageReceived, new FeedResponseWrapper<R>(intermediateResults));

                allResults.AddRange(intermediateResults);

            } while (!string.IsNullOrEmpty(intermediateResults.ResponseContinuation));

            feedResponseHandler(FeedResponseType.AfterEnumeration, null);

            return allResults;
        }

        /// <summary>
        /// This will execute a DocumentDB FeedResponse method and return the results.
        /// 
        /// It handles paging, continuation tokens, and retriable errors such as "too many requests" for you,
        /// while streaming query results out in chunks via IEnumerable / yield.
        /// </summary>
        /// <typeparam name="R"></typeparam>
        /// <param name="feedTakingContinuation"></param>
        /// <param name="enumerationExceptionHandler"></param>
        /// <param name="feedResponseHandler"></param>
        /// <param name="maxRetries"></param>
        /// <param name="maxTime"></param>
        /// <param name="shouldRetry"></param>
        /// <returns></returns>
        public static IEnumerable<R> StreamFeedWithContinuationAndRetry<R>(Func<string, FeedResponse<R>> feedTakingContinuation, EnumerationExceptionHandler enumerationExceptionHandler, FeedResponseHandler feedResponseHandler, int maxRetries, TimeSpan maxTime, ShouldRetry shouldRetry)
        {
            return StreamFeedWithContinuationAndRetry<R>((continuation) => Task<R>.Run(() => feedTakingContinuation(continuation)), enumerationExceptionHandler, feedResponseHandler, maxRetries, maxTime, shouldRetry);
        }

        /// <summary>
        /// This will execute a DocumentDB FeedResponse method and return the results.
        /// 
        /// It handles paging, continuation tokens, and retriable errors such as "too many requests" for you,
        /// while streaming query results out in chunks via IEnumerable / yield.
        /// </summary>
        /// <typeparam name="R"></typeparam>
        /// <param name="feedTakingContinuation"></param>
        /// <param name="enumerationExceptionHandler"></param>
        /// <param name="feedResponseHandler"></param>
        /// <param name="maxRetries"></param>
        /// <param name="maxTime"></param>
        /// <param name="shouldRetry"></param>
        /// <returns></returns>
        public static IEnumerable<R> StreamFeedWithContinuationAndRetry<R>(Func<string, Task<FeedResponse<R>>> feedTakingContinuation, EnumerationExceptionHandler enumerationExceptionHandler, FeedResponseHandler feedResponseHandler, int maxRetries, TimeSpan maxTime, ShouldRetry shouldRetry)
        {
            feedResponseHandler(FeedResponseType.BeforeEnumeration, null);

            FeedResponse<R> intermediateResults = null;
            do
            {
                Task<FeedResponse<R>> t;
                try
                {
                    t = Task.Run(async () => await ExecuteResultWithRetry(() =>
                        feedTakingContinuation(intermediateResults?.ResponseContinuation),
                        null,
                        maxRetries,
                        maxTime,
                        shouldRetry));

                    t.Wait();
                }
                catch (Exception ex)
                {
                    // note: if an IQueryable is returned to OData, throwing an exception here will cause it to take down w3wp.exe 

                    bool handled = enumerationExceptionHandler(ex);
                    if (!handled)
                    {
                        feedResponseHandler(FeedResponseType.EnumerationAborted, null);
                        throw;
                    }
                    else
                        break;
                }

                intermediateResults = t.Result;

                // lots of interesting info in intermediateResults such as RU usage, etc.
                feedResponseHandler(FeedResponseType.PageReceived, new FeedResponseWrapper<R>(intermediateResults));

                foreach (var result in intermediateResults)
                {
                    yield return result;
                }

            } while (!string.IsNullOrEmpty(intermediateResults.ResponseContinuation));

            feedResponseHandler(FeedResponseType.AfterEnumeration, null);
        }

        /// <summary>
        /// This will execute a DocumentDB client method for you while handling retriable errors such as "too many requests".
        /// 
        /// The caller must explicitly wrap the call they want to make in a lambda.  This is so that WithRetry can 
        /// execute the lambda in order to ask for the task multiple times instead of getting an instance created at 
        /// WithRetry method invocation time.
        /// 
        /// Example: "ExecuteResultWithRetry(() => YourCallHere(arguments, will, be, closured));"
        /// </summary>
        /// <param name="action"></param>
        /// <param name="resourceResponseHandler"></param>
        /// <param name="maxRetries"></param>
        /// <param name="maxTime"></param>
        /// <param name="shouldRetry"></param>
        /// <returns></returns>
        public static async Task ExecuteResultWithRetry(Action action, ResourceResponseHandler resourceResponseHandler, int maxRetries, TimeSpan maxTime, ShouldRetry shouldRetry)
        {
            // just wrap it in a task and call the main WithRetry method
            await ExecuteResultWithRetry<int>(() => { action(); return 0; }, resourceResponseHandler, maxRetries, maxTime, shouldRetry);
        }

        /// <summary>
        /// This will execute a DocumentDB client method for you while handling retriable errors such as "too many requests".
        /// 
        /// The caller must explicitly wrap the call they want to make in a lambda.  This is so that WithRetry can 
        /// execute the lambda in order to ask for the task multiple times instead of getting an instance created at 
        /// WithRetry method invocation time.
        /// 
        /// Example: "ExecuteResultWithRetry(() => YourCallHere(arguments, will, be, closured));"
        /// </summary>
        /// <typeparam name="R"></typeparam>
        /// <param name="action"></param>
        /// <param name="resourceResponseHandler"></param>
        /// <param name="maxRetries"></param>
        /// <param name="maxTime"></param>
        /// <param name="shouldRetry"></param>
        /// <returns></returns>
        public static async Task<R> ExecuteResultWithRetry<R>(Func<R> action, ResourceResponseHandler resourceResponseHandler, int maxRetries, TimeSpan maxTime, ShouldRetry shouldRetry)
        {
            // just wrap it in a task and call the main WithRetry method
            // the call to Task.Run must itself be closured because it takes a param 
            return await ExecuteResultWithRetry<R>(() => Task<R>.Run(action), resourceResponseHandler, maxRetries, maxTime, shouldRetry);
        }

        /// <summary>
        /// This will execute a DocumentDB client method for you while handling retriable errors such as "too many requests".
        /// 
        /// The caller must explicitly wrap the call they want to make in a lambda.  This is so that WithRetry can 
        /// execute the lambda in order to ask for the task multiple times instead of getting an instance created at 
        /// WithRetry method invocation time.
        /// 
        /// Example: "ExecuteResultWithRetry(() => YourCallHere(arguments, will, be, closured));"
        /// </summary>
        /// <param name="action"></param>
        /// <param name="resourceResponseHandler"></param>
        /// <param name="maxRetries"></param>
        /// <param name="maxTime"></param>
        /// <param name="shouldRetry"></param>
        /// <returns></returns>
        public static async Task ExecuteResultWithRetry(Func<Task> action, ResourceResponseHandler resourceResponseHandler, int maxRetries, TimeSpan maxTime, ShouldRetry shouldRetry)
        {
            // just wrap it in a task and call the main WithRetry method
            await ExecuteResultWithRetry<int>(async () => { await action(); return 0; }, resourceResponseHandler, maxRetries, maxTime, shouldRetry);
        }

        /// <summary>
        /// This will execute a DocumentDB client method for you while handling retriable errors such as "too many requests".
        /// 
        /// The caller must explicitly wrap the async call they want to make in a lambda.  This is so that WithRetry can 
        /// execute the lambda in order to ask for the task multiple times instead of getting an instance created at 
        /// WithRetry method invocation time.
        /// 
        /// Example: "ExecuteResultWithRetry(() => YourCallHere(arguments, will, be, closured));"
        /// </summary>
        /// <typeparam name="R"></typeparam>
        /// <param name="action"></param>
        /// <param name="resourceResponseHandler"></param>
        /// <param name="maxRetries"></param>
        /// <param name="maxTime"></param>
        /// <param name="shouldRetry"></param>
        /// <returns></returns>
        public static async Task<R> ExecuteResultWithRetry<R>(Func<Task<R>> action, ResourceResponseHandler resourceResponseHandler, int maxRetries, TimeSpan maxTime, ShouldRetry shouldRetry)
        {
            // time the execution
            Stopwatch sw = new Stopwatch();
            sw.Start();

            // count the retries
            int retries = 0;

            // remember the last exception
            Exception lastEx;

            while (true)
            {
                TimeSpan retryDelay = TimeSpan.Zero;

                try
                {
                    var result = await action();

                    if(result is IResourceResponseBase)
                        resourceResponseHandler(result as IResourceResponseBase);

                    return result;
                }
                catch (Exception clientException)
                {
                    lastEx = clientException;

                    try
                    {
                        retryDelay = shouldRetry(clientException);
                    }
                    catch (DocumentDbConflictResponse)
                    {
                        // conflict response was already handled properly internally to the ShouldRetry handler
                        throw;
                    }
                    catch (DocumentDbUnexpectedResponse)
                    {
                        // unexpected response was already handled properly internally to the ShouldRetry handler
                        throw;
                    }
                    catch (DocumentDbNonRetriableResponse)
                    {
                        // non-retriable response was already handled properly internally to the ShouldRetry handler
                        throw;
                    }
                    catch (Exception ex)
                    {
                        // someone gave us a bad ShouldRetry handler
                        throw new DocumentDbRetryHandlerError("The ShouldRetry handler threw an unexpected exception.", ex);
                    }

                    if (null == retryDelay)
                    {
                        // someone gave us a bad ShouldRetry handler
                        throw new DocumentDbRetryHandlerError("The ShouldRetry handler returned a null delay.", clientException);
                    }
                }

                if (sw.Elapsed > maxTime || retries > maxRetries)
                {
                    throw new DocumentDbRetriesExceeded("Exceeded retry count (" + retries + " of " + maxRetries + ") or time (" + sw.Elapsed + " of " + maxTime + ") limit while retrying operation.", lastEx);
                }

                await Task.Delay(retryDelay);

                retries++;
            }
        }
    }
}
