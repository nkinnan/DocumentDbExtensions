using Microsoft.Azure.Documents.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;

namespace Microsoft.Azure.Documents
{
    /// <summary>
    /// If you wish to override the default retry logic, implement this prototype and pass it into the method call, or
    /// set DocumentDbExtensions.DefaultShouldRetryLogic
    /// 
    /// You may wish to use this functionality, for example, if you have a sproc which is not "continuable" but instead
    /// throws a specific error that you can recognize as "entire transaction rolled back, please retry executing the entire
    /// call".  In that case, your ShouldRetry logic can pick up on that and retry the call in its entirety.  (Its 
    /// recommended that your sprocs should instead return "how far they got" and be called a second time with those 
    /// inputs removed in "continuation mode" course.)
    /// 
    /// If your custom ShouldRetry logic can't understand the response, you should throw DocumentDbUnexpectedResponse.
    /// If your custom ShouldRetry logic can understand the response but it is not retriable, you should throw DocumentDbNonRetriableResponse or DocumentDbConflictResponse which is a special case of NonRetriable.
    /// If you throw any other exception type, that exception will be wrapped in DocumentDbRetryHandlerError.
    /// </summary>
    /// <param name="context">This instance will be constant across all invocations for a particular query.</param>
    /// <param name="exception">The DocumentDB client exception to interpret and decide if you want to retry.</param>
    /// <returns>
    /// NULL for "don't retry" in which case a DocumentDbRetriesExceeded exception will be thrown, wrapping the original exception,
    /// otherwise the TimeSpan to wait for before making the next attempt, normally retrieved from the DocumentDB response.
    /// </returns>
    public delegate TimeSpan ShouldRetry(FeedResponseContext context, Exception exception);

    /// <summary>
    /// When intercepting or paging a query, this handler will be called before execution with the full Linq expression / DocumentDB SQL 
    /// expression that is about to be executed.
    /// </summary>
    /// <param name="context">This instance will be constant across all invocations for a particular query.</param>
    /// <param name="query"></param>
    public delegate void QueryExecutionHandler(FeedResponseContext context, string query);

    /// <summary>
    /// When intercepting a query, or using one of the reliable query execution methods, results are retrieved in "pages".  It is 
    /// sometimes not desirable to propagate exceptions back out to the caller who is enumerating results, if a failure happens on
    /// "get next page".
    /// 
    /// For example if you pass an intercepted IQueryable back to OData and propagate an exception out, it will take down the entire 
    /// w3wp.exe process :D
    /// 
    /// So, by passing an implementation of this delegate you may ignore, log, or propagate the exception as you choose.
    /// 
    /// If you return true, the exception will be re-thrown from its original context, and if you return false then a partial result 
    /// set will be returned.
    /// </summary>
    /// <param name="context">This instance will be constant across all invocations for a particular query.</param>
    /// <param name="exception"></param>
    /// <returns>
    /// Whether the exception was handled.  If false, the original exception will be rethrown from its original context.  If true, the exception 
    /// will be swallowed on the assumption that you have logged or otherwise handled it and a partial (or empty) result set will be returned.
    /// </returns>
    public delegate bool EnumerationExceptionHandler(FeedResponseContext context, Exception exception);

    /// <summary>
    /// When intercepting a query, or using one of the reliable query execution methods, results are retrieved in "pages".  While
    /// all paging and enumeration is handled internally, you may wish to have access to certain interesting bits of information from 
    /// each DocumentDB FeedResponse as it comes through "behind the scenes".  Things like resource usage for example may be useful 
    /// to log.
    /// </summary>
    /// <param name="context">This instance will be constant across all invocations for a particular query.</param>
    /// <param name="type"></param>
    /// <param name="feedResponse"></param>
    public delegate void FeedResponseHandler(FeedResponseContext context, FeedResponseType type, IFeedResponse feedResponse);

    /// <summary>
    /// When executing with retry, a document db client method, the resource response may be passed back to you.  Things like resource 
    /// usage for example may be useful to log.
    /// </summary>
    /// <param name="resourceResponse"></param>
    public delegate void ResourceResponseHandler(IResourceResponseBase resourceResponse);

    /// <summary>
    /// Extensions for the DocumentDB Client which provide:
    /// * DocumentDB IQueryable interception / translation in order to allow use of DateTime/Offset types in where clauses.
    ///     - Don't forget to mark all of your DateTime or DateTimeOffset properties with the DocumentDbDateTimeJsonConverter attribute!
    /// * Reliable execution with retries and automatic paging/streaming for DocumentDB IQueryables.
    /// * Reliable execution with retries for any DocumentDB Client method.
    /// * Both syncronous and asyncronous implementations of all methods.
    /// </summary>
    public static class DocumentDbExtensions
    {
        #region default values
        /// <summary>
        /// The default maximum number of DocumentDB Client retries to execute before giving up if not overridden in the method call.
        /// 
        /// If you set this, it will apply to all future calls into DocumentDbExtensions which do not override the value.
        /// </summary>
        public static int DefaultMaxRetryCount = 10 * 60;

        /// <summary>
        /// The default maximum amount of time to use for of DocumentDB Client retries before giving up, if not overridden in the method call.
        /// 
        /// If you set this, it will apply to all future calls into DocumentDbExtensions which do not override the value.
        /// </summary>
        public static TimeSpan DefaultMaxRetryTime = TimeSpan.FromMinutes(10);

        /// <summary>
        /// This implements the default retry logic to use if not overridden in the method call.  
        /// 
        /// If you set this, it will apply to all future calls into DocumentDbExtensions which do not override the value.
        /// </summary>
        public static ShouldRetry DefaultShouldRetryLogic = DefaultShouldRetryLogicImplementation;

        /// <summary>
        /// This implements the default query execution handling logic when executing an IQueryable, if not overridden in the method call.
        /// 
        /// If you set this, it will apply to all future calls into DocumentDbExtensions which do not override the value.
        /// </summary>
        public static QueryExecutionHandler QueryExecutionHandler = DefaultQueryExecutionHandlerImplementation;

        /// <summary>
        /// This implements the default exception handling logic on IQueryable enumeration/paging errors to use, if not overridden in the method call.  
        /// 
        /// If you set this, it will apply to all future calls into DocumentDbExtensions which do not override the value.
        /// </summary>
        public static EnumerationExceptionHandler DefaultEnumerationExceptionHandler = DefaultEnumerationExceptionHandlerImplementation;

        /// <summary>
        /// This implements the default feed response handling logic on IQueryable result paging, if not overridden in the method call.  
        /// 
        /// If you set this, it will apply to all future calls into DocumentDbExtensions which do not override the value.
        /// </summary>
        public static FeedResponseHandler DefaultFeedResponseHandler = DefaultFeedResponseHandlerImplementation;

        /// <summary>
        /// This implements the default resource response handling logic on non-IQueryable result call results, if not overridden in the method call.  
        /// 
        /// If you set this, it will apply to all future calls into DocumentDbExtensions which do not override the value.
        /// </summary>
        public static ResourceResponseHandler DefaultResourceResponseHandler = DefaultResourceResponseHandlerImplementation;
        #endregion default values

        #region implementation of default handlers
        /// <summary>
        /// The implementation of the default ShouldRetry logic, this is assigned to "DefaultShouldRetry" by default.
        /// Please note that context is only available when executing a feed (a Linq or SQL text query) including the default
        /// enumeration handler, but not for example if an exception is generated from an ExecuteResult...() call.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="exception"></param>
        /// <returns></returns>
        public static TimeSpan DefaultShouldRetryLogicImplementation(FeedResponseContext context, Exception exception)
        {
            HttpStatusCode statusCode = (HttpStatusCode)(-1);

            foreach (var inner in UnwrapAggregates(exception))
            {
                var documentClientException = inner as DocumentClientException;

                if (documentClientException != null)
                {
                    statusCode = documentClientException.StatusCode ?? (HttpStatusCode)(-1);

                    // expected retriable codes
                    if (statusCode == (HttpStatusCode)429 /* "TooManyRequests" - not in the HttpStatusCode enum, but it is in the RFC at https://tools.ietf.org/html/rfc6585 and is expected */ || 
                        statusCode == HttpStatusCode.ServiceUnavailable ||
                        statusCode == HttpStatusCode.RequestTimeout)
                    {
                        return documentClientException.RetryAfter;
                    }

                    // special case non-retriable (etag)
                    if(statusCode == HttpStatusCode.PreconditionFailed)
                    {
                        // note: inherits DocumentDbNonRetriableResponse
                        throw new DocumentDbConflictResponse("Conflict on write. This is recoverable but not retriable (" + statusCode + ").", exception);
                    }

                    // expected non-retriable codes
                    if (statusCode == HttpStatusCode.NotFound ||
                        statusCode == HttpStatusCode.Conflict ||
                        statusCode == HttpStatusCode.BadRequest)
                    {
                        throw new DocumentDbNonRetriableResponse("Response from DocumentDB client is expected but non-retriable (" + statusCode + ").", exception);
                    }
                }
            }

            // special case for a case of the Linq abstraction being "leaky"
            // I consider it a bug in the DocDB client lib that this isn't already wrapped/typed as a DocumentClientException saying something like "can't translate your Linq query"
            if (exception is TargetInvocationException && 
                exception.InnerException is InvalidOperationException && 
                exception.InnerException.Message == "Nullable object must have a value." &&
                exception.StackTrace.Contains("Microsoft.Azure.Documents.Linq.SubtreeEvaluator"))
            {
                throw new DocumentDbNonRetriableResponse("Exception from DocumentDB client indicates it cannot parse your Linq expression. This may be because you used a ternary similar to: (myLocalNullable ? myLocalNullable.Value : <etc>). Note that the DocumentDB client libs must evaluate every portion of the expression tree in order to translate it to a DocDB SQL query, like a compiler, even when the path is not expected to be followed in practice at runtime. You can fix this by saying instead: (myLocalNullable ? myLocalNullable : <etc>) which omits the '.Value' and should cause the expression tree visitor to emit a literal 'null' constant which will never be evaluated against.", exception);
            }

            throw new DocumentDbUnexpectedResponse("Unable to interpret response from DocumentDB client in order to determine if retry should happen (" + (statusCode == (HttpStatusCode)(-1) ? "Bare exception thrown by DocumentDB client library, no status code available, message: " + (exception.Message) : statusCode.ToString()) + ").", exception);
        }

        /// <summary>
        /// The implementation of the default QueryExecutionHandler logic, this is assigned to "DefaultQueryExecutionHandler" by default.
        /// 
        /// This implementation does nothing and throws away the query string.
        /// </summary>
        /// <param name="context">This instance will be constant across all invocations for a particular query.</param>
        /// <param name="query"></param>
        /// <returns></returns>
        public static void DefaultQueryExecutionHandlerImplementation(FeedResponseContext context, string query)
        {
            // intentionally left blank
        }

        /// <summary>
        /// The implementation of the default EnumerationExceptionHandler logic, this is assigned to "DefaultEnumerationExceptionHandler" by default.
        /// 
        /// This implementation will cause the caller to re-throw the exception from its original context which will be propagated back out to the code which is enumerating the results.
        /// </summary>
        /// <param name="context">This instance will be constant across all invocations for a particular query.</param>
        /// <param name="exception"></param>
        /// <returns></returns>
        public static bool DefaultEnumerationExceptionHandlerImplementation(FeedResponseContext context, Exception exception)
        {
            return false;
        }

        /// <summary>
        /// The implementation of the default FeedResponseHandler logic, this is assigned to "DefaultFeedResponseHandler" by default.
        /// 
        /// This implementation does nothing.
        /// </summary>
        /// <returns></returns>
        public static void DefaultFeedResponseHandlerImplementation(FeedResponseContext context, FeedResponseType type, IFeedResponse feedResponse)
        {
            // intentionally left blank
        }

        /// <summary>
        /// The implementation of the default ResourceResponseHandler logic, this is assigned to "DefaultResourceResponseHandler" by default.
        /// 
        /// This implementation does nothing.
        /// </summary>
        /// <returns></returns>
        public static void DefaultResourceResponseHandlerImplementation(IResourceResponseBase resourceResponse)
        {
            // intentionally left blank
        }

        /// <summary>
        /// A helper method that you may use in any custom retry logic to unwrap aggregate exceptions into a list of exceptions.  
        /// This is safe to call on a non-aggregate and also handles aggregate aggregates.
        /// </summary>
        /// <param name="exception"></param>
        /// <returns></returns>
        public static IList<Exception> UnwrapAggregates(Exception exception)
        {
            List<Exception> exceptions = new List<Exception>();

            AggregateException aggregate = exception as AggregateException;
            if (aggregate != null)
            {
                foreach (var inner in aggregate.InnerExceptions)
                {
                    // yes, you can have an aggregate aggregate
                    exceptions.AddRange(UnwrapAggregates(inner));
                }
            }
            else
            {
                exceptions.Add(exception);
            }

            return exceptions;
        }
        #endregion implementation of default handlers

        #region query interception
        /// <summary>
        /// To gain the advantage of the query translator (allows you to use things like DateTime and DateTimeOffset in queries) plus
        /// reliable execution, you must intercept the IQueryable returned by the DocumentDB Client by calling this method on it BEFORE 
        /// you start adding things like ".Where(...)" or ".Select(...)" etc.
        /// 
        /// Once the DocumentDB IQueryable is wrapped, you can use it just like any other standard IQueryable.  It will translate (some)
        /// expressions that DocumentDB doesn't handle, and lazily enumerate with retries on each "page".  You do not need to call any 
        /// of the query execution methods in this class on it afterward, everything is automatic once the IQueryable has been intercepted.
        /// </summary>
        /// <typeparam name="TElement">The type of the elements returned by the query.</typeparam>
        /// <param name="underlyingQuery"></param>
        /// <param name="queryExecutionHandler"></param>
        /// <param name="enumerationExceptionHandler"></param>
        /// <param name="feedResponseHandler"></param>
        /// <param name="maxRetries"></param>
        /// <param name="maxTime"></param>
        /// <param name="shouldRetry"></param>
        /// <returns></returns>
        public static IQueryable<TElement> InterceptQuery<TElement>(IQueryable<TElement> underlyingQuery, QueryExecutionHandler queryExecutionHandler = null, EnumerationExceptionHandler enumerationExceptionHandler = null, FeedResponseHandler feedResponseHandler = null, int? maxRetries = null, TimeSpan? maxTime = null, ShouldRetry shouldRetry = null)
        {
            if (underlyingQuery == null)
                throw new ArgumentException("underlyingQuery");
            return DocumentDbTranslatingReliableQueryProvider.Intercept(underlyingQuery, queryExecutionHandler ?? DefaultQueryExecutionHandlerImplementation, enumerationExceptionHandler ?? DefaultEnumerationExceptionHandler, feedResponseHandler ?? DefaultFeedResponseHandler, maxRetries ?? DefaultMaxRetryCount, maxTime ?? DefaultMaxRetryTime, shouldRetry ?? DefaultShouldRetryLogic);
        }

        /// <summary>
        /// This method will create an IQuerable which allows you to call GetNextPage(continuationToken) even after having "lost" the
        /// original IQueryable instance, as long as you still have the continuationToken.  If the original IQueryable created via
        /// InterceptQuery() is still around, you can simply call GetNextPage() with no parameters instead, as the continuations will
        /// be tracked internally to that instance.
        /// 
        /// ***When re-creating the underlyingQuery to pass to this method, it must be created the same way as the original, with the same
        /// linq clauses, or SQL query string, and the RequestContinuation property of the FeedOptions must have been set appropriately***
        /// </summary>
        /// <typeparam name="TElement">The type of the elements returned by the query.</typeparam>
        /// <param name="underlyingQuery"></param>
        /// <param name="queryExecutionHandler"></param>
        /// <param name="enumerationExceptionHandler"></param>
        /// <param name="feedResponseHandler"></param>
        /// <param name="maxRetries"></param>
        /// <param name="maxTime"></param>
        /// <param name="shouldRetry"></param>
        /// <returns></returns>
        public static IQueryable<TElement> InterceptQueryForPagingContinuationOnly<TElement>(IQueryable<TElement> underlyingQuery, QueryExecutionHandler queryExecutionHandler = null, EnumerationExceptionHandler enumerationExceptionHandler = null, FeedResponseHandler feedResponseHandler = null, int? maxRetries = null, TimeSpan? maxTime = null, ShouldRetry shouldRetry = null)
        {
            if (underlyingQuery == null)
                throw new ArgumentException("underlyingQuery");
            return DocumentDbTranslatingReliableQueryProvider.InterceptForPagingContinuationOnly(underlyingQuery, queryExecutionHandler ?? DefaultQueryExecutionHandlerImplementation, enumerationExceptionHandler ?? DefaultEnumerationExceptionHandler, feedResponseHandler ?? DefaultFeedResponseHandler, maxRetries ?? DefaultMaxRetryCount, maxTime ?? DefaultMaxRetryTime, shouldRetry ?? DefaultShouldRetryLogic);
        }
        #endregion query interception

        #region query execution (non-intercepted)
        /// <summary>
        /// This will execute a DocumentDB query in the form of an IQueryable (Linq form) and return the results.
        /// 
        /// It handles paging, continuation tokens, and retriable errors such as "too many requests" for you,
        /// while aggregating all query results in-memory before returning.
        /// 
        /// You don't need to use this if you have called InterceptQuery() on the IQueryable previously.
        /// </summary>
        /// <typeparam name="TElement">The type of the elements returned by the query.</typeparam>
        /// <param name="queryable"></param>
        /// <param name="queryExecutionHandler"></param>
        /// <param name="enumerationExceptionHandler"></param>
        /// <param name="feedResponseHandler"></param>
        /// <param name="maxRetries"></param>
        /// <param name="maxTime"></param>
        /// <param name="shouldRetry"></param>
        /// <returns></returns>
        public static IList<TElement> ExecuteQueryWithContinuationAndRetry<TElement>(IQueryable<TElement> queryable, QueryExecutionHandler queryExecutionHandler = null, EnumerationExceptionHandler enumerationExceptionHandler = null, FeedResponseHandler feedResponseHandler = null, int? maxRetries = null, TimeSpan? maxTime = null, ShouldRetry shouldRetry = null)
        {
            if (queryable == null)
                throw new ArgumentException("queryable");
            var task = DocumentDbReliableExecution.ExecuteQueryWithContinuationAndRetry(queryable, queryExecutionHandler ?? DefaultQueryExecutionHandlerImplementation, enumerationExceptionHandler ?? DefaultEnumerationExceptionHandler, feedResponseHandler ?? DefaultFeedResponseHandler, maxRetries ?? DefaultMaxRetryCount, maxTime ?? DefaultMaxRetryTime, shouldRetry ?? DefaultShouldRetryLogic);
            task.Wait();
            return task.Result;
        }

        /// <summary>
        /// This will execute a DocumentDB query in the form of an IQueryable (Linq form) and return the results.
        /// 
        /// It handles paging, continuation tokens, and retriable errors such as "too many requests" for you,
        /// while aggregating all query results in-memory before returning.
        /// 
        /// You don't need to use this if you have called InterceptQuery() on the IQueryable previously.
        /// </summary>
        /// <typeparam name="TElement">The type of the elements returned by the query.</typeparam>
        /// <param name="queryable"></param>
        /// <param name="queryExecutionHandler"></param>
        /// <param name="enumerationExceptionHandler"></param>
        /// <param name="feedResponseHandler"></param>
        /// <param name="maxRetries"></param>
        /// <param name="maxTime"></param>
        /// <param name="shouldRetry"></param>
        /// <returns></returns>
        public static async Task<IList<TElement>> ExecuteQueryWithContinuationAndRetryAsync<TElement>(IQueryable<TElement> queryable, QueryExecutionHandler queryExecutionHandler = null, EnumerationExceptionHandler enumerationExceptionHandler = null, FeedResponseHandler feedResponseHandler = null, int? maxRetries = null, TimeSpan? maxTime = null, ShouldRetry shouldRetry = null)
        {
            if (queryable == null)
                throw new ArgumentException("queryable");
            return await DocumentDbReliableExecution.ExecuteQueryWithContinuationAndRetry(queryable, queryExecutionHandler ?? DefaultQueryExecutionHandlerImplementation, enumerationExceptionHandler ?? DefaultEnumerationExceptionHandler, feedResponseHandler ?? DefaultFeedResponseHandler, maxRetries ?? DefaultMaxRetryCount, maxTime ?? DefaultMaxRetryTime, shouldRetry ?? DefaultShouldRetryLogic);
        }

        /// <summary>
        /// This will execute a DocumentDB query in the form of an IQueryable (Linq form) and return the results.
        /// 
        /// It handles paging, continuation tokens, and retriable errors such as "too many requests" for you,
        /// while streaming query results out in chunks via IEnumerable / yield.
        /// 
        /// You don't need to use this if you have called InterceptQuery() on the IQueryable previously.
        /// </summary>
        /// <typeparam name="TElement">The type of the elements returned by the query.</typeparam>
        /// <param name="queryable"></param>
        /// <param name="queryExecutionHandler"></param>
        /// <param name="enumerationExceptionHandler"></param>
        /// <param name="feedResponseHandler"></param>
        /// <param name="maxRetries"></param>
        /// <param name="maxTime"></param>
        /// <param name="shouldRetry"></param>
        /// <returns></returns>
        public static IEnumerable<TElement> StreamQueryWithContinuationAndRetry<TElement>(IQueryable<TElement> queryable, QueryExecutionHandler queryExecutionHandler = null, EnumerationExceptionHandler enumerationExceptionHandler = null, FeedResponseHandler feedResponseHandler = null, int? maxRetries = null, TimeSpan? maxTime = null, ShouldRetry shouldRetry = null)
        {
            if (queryable == null)
                throw new ArgumentException("queryable");
            return DocumentDbReliableExecution.StreamQueryWithContinuationAndRetry(queryable, queryExecutionHandler ?? DefaultQueryExecutionHandlerImplementation, enumerationExceptionHandler ?? DefaultEnumerationExceptionHandler, feedResponseHandler ?? DefaultFeedResponseHandler, maxRetries ?? DefaultMaxRetryCount, maxTime ?? DefaultMaxRetryTime, shouldRetry ?? DefaultShouldRetryLogic);
        }
        #endregion query execution (non-intercepted)

        #region DocumentDB client method execution
        /// <summary>
        /// This will execute a DocumentDB client method for you while handling retriable errors such as "too many requests".
        /// </summary>
        /// <param name="action"></param>
        /// <param name="resourceResponseHandler"></param>
        /// <param name="maxRetries"></param>
        /// <param name="maxTime"></param>
        /// <param name="shouldRetry"></param>
        public static void ExecuteResultWithRetry(Action action, ResourceResponseHandler resourceResponseHandler = null, int? maxRetries = null, TimeSpan? maxTime = null, ShouldRetry shouldRetry = null)
        {
            if (action == null)
                throw new ArgumentException("action");
            var task = DocumentDbReliableExecution.ExecuteResultWithRetry(action, resourceResponseHandler ?? DefaultResourceResponseHandler, maxRetries ?? DefaultMaxRetryCount, maxTime ?? DefaultMaxRetryTime, shouldRetry ?? DefaultShouldRetryLogic);
            task.Wait();
        }

        /// <summary>
        /// This will execute a DocumentDB client method for you while handling retriable errors such as "too many requests".
        /// </summary>
        /// <param name="action"></param>
        /// <param name="resourceResponseHandler"></param>
        /// <param name="maxRetries"></param>
        /// <param name="maxTime"></param>
        /// <param name="shouldRetry"></param>
        /// <returns></returns>
        public static async Task ExecuteResultWithRetryAsync(Action action, ResourceResponseHandler resourceResponseHandler = null, int? maxRetries = null, TimeSpan? maxTime = null, ShouldRetry shouldRetry = null)
        {
            if (action == null)
                throw new ArgumentException("action");
            await DocumentDbReliableExecution.ExecuteResultWithRetry(action, resourceResponseHandler ?? DefaultResourceResponseHandler, maxRetries ?? DefaultMaxRetryCount, maxTime ?? DefaultMaxRetryTime, shouldRetry ?? DefaultShouldRetryLogic);
        }

        /// <summary>
        /// This will execute a DocumentDB client method for you while handling retriable errors such as "too many requests".
        /// </summary>
        /// <typeparam name="R"></typeparam>
        /// <param name="action"></param>
        /// <param name="resourceResponseHandler"></param>
        /// <param name="maxRetries"></param>
        /// <param name="maxTime"></param>
        /// <param name="shouldRetry"></param>
        /// <returns></returns>
        public static R ExecuteResultWithRetry<R>(Func<R> action, ResourceResponseHandler resourceResponseHandler = null, int? maxRetries = null, TimeSpan? maxTime = null, ShouldRetry shouldRetry = null)
        {
            if (action == null)
                throw new ArgumentException("action");
            var task = DocumentDbReliableExecution.ExecuteResultWithRetry(action, resourceResponseHandler ?? DefaultResourceResponseHandler, maxRetries ?? DefaultMaxRetryCount, maxTime ?? DefaultMaxRetryTime, shouldRetry ?? DefaultShouldRetryLogic);
            task.Wait();
            return task.Result;
        }

        /// <summary>
        /// This will execute a DocumentDB client method for you while handling retriable errors such as "too many requests".
        /// </summary>
        /// <typeparam name="R"></typeparam>
        /// <param name="action"></param>
        /// <param name="resourceResponseHandler"></param>
        /// <param name="maxRetries"></param>
        /// <param name="maxTime"></param>
        /// <param name="shouldRetry"></param>
        /// <returns></returns>
        public static async Task<R> ExecuteResultWithRetryAsync<R>(Func<R> action, ResourceResponseHandler resourceResponseHandler = null, int? maxRetries = null, TimeSpan? maxTime = null, ShouldRetry shouldRetry = null)
        {
            if (action == null)
                throw new ArgumentException("action");
            return await DocumentDbReliableExecution.ExecuteResultWithRetry(action, resourceResponseHandler ?? DefaultResourceResponseHandler, maxRetries ?? DefaultMaxRetryCount, maxTime ?? DefaultMaxRetryTime, shouldRetry ?? DefaultShouldRetryLogic);
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
        public static R ExecuteResultWithRetry<R>(Func<Task<R>> action, ResourceResponseHandler resourceResponseHandler = null, int? maxRetries = null, TimeSpan? maxTime = null, ShouldRetry shouldRetry = null)
        {
            if (action == null)
                throw new ArgumentException("action");
            var task = DocumentDbReliableExecution.ExecuteResultWithRetry(action, resourceResponseHandler ?? DefaultResourceResponseHandler, maxRetries ?? DefaultMaxRetryCount, maxTime ?? DefaultMaxRetryTime, shouldRetry ?? DefaultShouldRetryLogic);
            task.Wait();
            return task.Result;
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
        public static async Task<R> ExecuteResultWithRetryAsync<R>(Func<Task<R>> action, ResourceResponseHandler resourceResponseHandler = null, int? maxRetries = null, TimeSpan? maxTime = null, ShouldRetry shouldRetry = null)
        {
            if (action == null)
                throw new ArgumentException("action");
            return await DocumentDbReliableExecution.ExecuteResultWithRetry(action, resourceResponseHandler ?? DefaultResourceResponseHandler, maxRetries ?? DefaultMaxRetryCount, maxTime ?? DefaultMaxRetryTime, shouldRetry ?? DefaultShouldRetryLogic);
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
        /// <param name="action"></param>
        /// <param name="resourceResponseHandler"></param>
        /// <param name="maxRetries"></param>
        /// <param name="maxTime"></param>
        /// <param name="shouldRetry"></param>
        /// <returns></returns>
        public static void ExecuteResultWithRetry(Func<Task> action, ResourceResponseHandler resourceResponseHandler = null, int? maxRetries = null, TimeSpan? maxTime = null, ShouldRetry shouldRetry = null)
        {
            if (action == null)
                throw new ArgumentException("action");
            var task = DocumentDbReliableExecution.ExecuteResultWithRetry(action, resourceResponseHandler ?? DefaultResourceResponseHandler, maxRetries ?? DefaultMaxRetryCount, maxTime ?? DefaultMaxRetryTime, shouldRetry ?? DefaultShouldRetryLogic);
            task.Wait();
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
        /// <param name="action"></param>
        /// <param name="resourceResponseHandler"></param>
        /// <param name="maxRetries"></param>
        /// <param name="maxTime"></param>
        /// <param name="shouldRetry"></param>
        /// <returns></returns>
        public static async Task ExecuteResultWithRetryAsync(Func<Task> action, ResourceResponseHandler resourceResponseHandler = null, int? maxRetries = null, TimeSpan? maxTime = null, ShouldRetry shouldRetry = null)
        {
            if (action == null)
                throw new ArgumentException("action");
            await DocumentDbReliableExecution.ExecuteResultWithRetry(action, resourceResponseHandler ?? DefaultResourceResponseHandler, maxRetries ?? DefaultMaxRetryCount, maxTime ?? DefaultMaxRetryTime, shouldRetry ?? DefaultShouldRetryLogic);
        }

        /// <summary>
        /// This will execute a DocumentDB client FeedResponse method for you while handling retriable errors such as "too many requests".
        /// 
        /// The caller must explicitly wrap the async call they want to make in a lambda.  This is so that WithRetry can 
        /// execute the lambda in order to ask for the task multiple times instead of getting an instance created at 
        /// WithRetry method invocation time.  
        /// 
        /// Because of an implementation detail of the DocumentDb Client, you'll need your lambda to take the continuation token and apply
        /// it to the feed options passed into the method you've wrapped in your lambda.
        /// 
        /// Example: "ExecuteResultWithRetry((continuation) => { feedOptions.RequestContinuation = continuation; YourCallHere(arguments, will, be, closured, feedOptions));"
        /// </summary>
        /// <param name="feedTakingContinuation"></param>
        /// <param name="enumerationExceptionHandler"></param>
        /// <param name="feedResponseHandler"></param>
        /// <param name="maxRetries"></param>
        /// <param name="maxTime"></param>
        /// <param name="shouldRetry"></param>
        /// <returns></returns>
        public static async Task<IList<R>> ExecuteFeedWithContinuationAndRetry<R>(Func<string, FeedResponse<R>> feedTakingContinuation, EnumerationExceptionHandler enumerationExceptionHandler, FeedResponseHandler feedResponseHandler, int? maxRetries, TimeSpan? maxTime, ShouldRetry shouldRetry)
        {
            if (feedTakingContinuation == null)
                throw new ArgumentException("action");
            return await DocumentDbReliableExecution.ExecuteFeedWithContinuationAndRetry(feedTakingContinuation, enumerationExceptionHandler ?? DefaultEnumerationExceptionHandler, feedResponseHandler ?? DefaultFeedResponseHandler, maxRetries ?? DefaultMaxRetryCount, maxTime ?? DefaultMaxRetryTime, shouldRetry ?? DefaultShouldRetryLogic);
        }

        /// <summary>
        /// This will execute a DocumentDB client FeedResponse method for you while handling retriable errors such as "too many requests".
        /// 
        /// The caller must explicitly wrap the async call they want to make in a lambda.  This is so that WithRetry can 
        /// execute the lambda in order to ask for the task multiple times instead of getting an instance created at 
        /// WithRetry method invocation time.  
        /// 
        /// Because of an implementation detail of the DocumentDb Client, you'll need your lambda to take the continuation token and apply
        /// it to the feed options passed into the method you've wrapped in your lambda.
        /// 
        /// Example: "ExecuteResultWithRetry((continuation) => { feedOptions.RequestContinuation = continuation; YourCallHere(arguments, will, be, closured, feedOptions));"
        /// </summary>
        /// <param name="feedTakingContinuation"></param>
        /// <param name="enumerationExceptionHandler"></param>
        /// <param name="feedResponseHandler"></param>
        /// <param name="maxRetries"></param>
        /// <param name="maxTime"></param>
        /// <param name="shouldRetry"></param>
        /// <returns></returns>
        public static async Task<IList<R>> ExecuteFeedWithContinuationAndRetry<R>(Func<string, Task<FeedResponse<R>>> feedTakingContinuation, EnumerationExceptionHandler enumerationExceptionHandler, FeedResponseHandler feedResponseHandler, int? maxRetries, TimeSpan? maxTime, ShouldRetry shouldRetry)
        {
            if (feedTakingContinuation == null)
                throw new ArgumentException("action");
            return await DocumentDbReliableExecution.ExecuteFeedWithContinuationAndRetry(feedTakingContinuation, enumerationExceptionHandler ?? DefaultEnumerationExceptionHandler, feedResponseHandler ?? DefaultFeedResponseHandler, maxRetries ?? DefaultMaxRetryCount, maxTime ?? DefaultMaxRetryTime, shouldRetry ?? DefaultShouldRetryLogic);
        }

        /// <summary>
        /// This will execute a DocumentDB client FeedResponse method for you while handling retriable errors such as "too many requests".
        /// 
        /// The caller must explicitly wrap the async call they want to make in a lambda.  This is so that WithRetry can 
        /// execute the lambda in order to ask for the task multiple times instead of getting an instance created at 
        /// WithRetry method invocation time.  
        /// 
        /// Because of an implementation detail of the DocumentDb Client, you'll need your lambda to take the continuation token and apply
        /// it to the feed options passed into the method you've wrapped in your lambda.
        /// 
        /// Example: "ExecuteResultWithRetry((continuation) => { feedOptions.RequestContinuation = continuation; YourCallHere(arguments, will, be, closured, feedOptions));"
        /// </summary>
        /// <param name="feedTakingContinuation"></param>
        /// <param name="enumerationExceptionHandler"></param>
        /// <param name="feedResponseHandler"></param>
        /// <param name="maxRetries"></param>
        /// <param name="maxTime"></param>
        /// <param name="shouldRetry"></param>
        /// <returns></returns>
        public static IEnumerable<R> StreamFeedWithContinuationAndRetry<R>(Func<string, FeedResponse<R>> feedTakingContinuation, EnumerationExceptionHandler enumerationExceptionHandler, FeedResponseHandler feedResponseHandler, int? maxRetries, TimeSpan? maxTime, ShouldRetry shouldRetry)
        {
            if (feedTakingContinuation == null)
                throw new ArgumentException("action");
            return DocumentDbReliableExecution.StreamFeedWithContinuationAndRetry(feedTakingContinuation, enumerationExceptionHandler ?? DefaultEnumerationExceptionHandler, feedResponseHandler ?? DefaultFeedResponseHandler, maxRetries ?? DefaultMaxRetryCount, maxTime ?? DefaultMaxRetryTime, shouldRetry ?? DefaultShouldRetryLogic);
        }

        /// <summary>
        /// This will execute a DocumentDB client FeedResponse method for you while handling retriable errors such as "too many requests".
        /// 
        /// The caller must explicitly wrap the async call they want to make in a lambda.  This is so that WithRetry can 
        /// execute the lambda in order to ask for the task multiple times instead of getting an instance created at 
        /// WithRetry method invocation time.  
        /// 
        /// Because of an implementation detail of the DocumentDb Client, you'll need your lambda to take the continuation token and apply
        /// it to the feed options passed into the method you've wrapped in your lambda.
        /// 
        /// Example: "ExecuteResultWithRetry((continuation) => { feedOptions.RequestContinuation = continuation; YourCallHere(arguments, will, be, closured, feedOptions));"
        /// </summary>
        /// <param name="feedTakingContinuation"></param>
        /// <param name="enumerationExceptionHandler"></param>
        /// <param name="feedResponseHandler"></param>
        /// <param name="maxRetries"></param>
        /// <param name="maxTime"></param>
        /// <param name="shouldRetry"></param>
        /// <returns></returns>
        public static IEnumerable<R> StreamFeedWithContinuationAndRetry<R>(Func<string, Task<FeedResponse<R>>> feedTakingContinuation, EnumerationExceptionHandler enumerationExceptionHandler, FeedResponseHandler feedResponseHandler, int? maxRetries, TimeSpan? maxTime, ShouldRetry shouldRetry)
        {
            if (feedTakingContinuation == null)
                throw new ArgumentException("action");
            return DocumentDbReliableExecution.StreamFeedWithContinuationAndRetry(feedTakingContinuation, enumerationExceptionHandler ?? DefaultEnumerationExceptionHandler, feedResponseHandler ?? DefaultFeedResponseHandler, maxRetries ?? DefaultMaxRetryCount, maxTime ?? DefaultMaxRetryTime, shouldRetry ?? DefaultShouldRetryLogic);
        }

        #endregion DocumentDB client method execution
    }
}
