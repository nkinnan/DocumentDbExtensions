using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.Documents.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace Microsoft.Azure.Documents
{
    internal class DocumentDbTranslatingReliableQueryProvider : InterceptingQueryProvider
    {
        private enum Mode
        {
            Invalid,
            Intercept,
            InterceptWithPaging,
            PagingContinuationOnly,
            Executed
        };

        Mode mode = Mode.Invalid;

        private EnumerationExceptionHandler enumerationExceptionHandler;
        private FeedResponseHandler feedResponseHandler;
        private TimeSpan maxTime;
        private int maxRetries;
        private ShouldRetry shouldRetry;

        DocumentClient pagingClient;
        Uri pagingCollectionUri;
        FeedOptions pagingFeedOptions;

        Object pagingQuery;

        private const string AlreadyExecutedNowPagingMessage = "This query has already been executed and is in paging mode, call GetNextPage() instead.";
        private const string AlreadyExecutedMessage = "This query has already been executed.";
        private const string InstancePagingOnlyMessage = "This query is tracking paging internally, only calls to GetNextPage() without the continuationToken parameter are allowed.";
        private const string PagingContinuationOnlyMessage = "This query was created with the CreateForPagingContinuationOnly factory method, only calls to GetNextPage(continuationToken) are allowed.";
        private const string MustBeginPagingFirstMessage = "BeginPaging() must be called before attempting to get the next page.";
        private const string InternalErrorMessage = "Internal error: Unknown or unhandled execution mode";

        private DocumentDbTranslatingReliableQueryProvider(IQueryProvider underlyingProvider, EnumerationExceptionHandler enumerationExceptionHandler, FeedResponseHandler feedResponseHandler, int maxRetries, TimeSpan maxTime, ShouldRetry shouldRetry, params ExpressionVisitor[] visitors)
            : base(underlyingProvider, visitors)
        {
            this.enumerationExceptionHandler = enumerationExceptionHandler;
            this.feedResponseHandler = feedResponseHandler;
            this.maxRetries = maxRetries;
            this.maxTime = maxTime;
            this.shouldRetry = shouldRetry;

            this.mode = Mode.Intercept;
        }

        private DocumentDbTranslatingReliableQueryProvider(DocumentClient client, Uri collectionUri, FeedOptions feedOptions, EnumerationExceptionHandler enumerationExceptionHandler, FeedResponseHandler feedResponseHandler, int maxRetries, TimeSpan maxTime, ShouldRetry shouldRetry, params ExpressionVisitor[] visitors)
            : base()
        {
            this.enumerationExceptionHandler = enumerationExceptionHandler;
            this.feedResponseHandler = feedResponseHandler;
            this.maxRetries = maxRetries;
            this.maxTime = maxTime;
            this.shouldRetry = shouldRetry;

            this.pagingClient = client;
            this.pagingCollectionUri = collectionUri;
            this.pagingFeedOptions = feedOptions;

            this.mode = Mode.PagingContinuationOnly;
        }

        /// <summary>
        /// Begins interception of query evaluation, wraps the passed in IQueryable
        /// </summary>
        /// <typeparam name="TElement"></typeparam>
        /// <param name="underlyingQuery"></param>
        /// <param name="enumerationExceptionHandler"></param>
        /// <param name="feedResponseHandler"></param>
        /// <param name="maxRetries"></param>
        /// <param name="maxTime"></param>
        /// <param name="shouldRetry"></param>
        /// <returns></returns>
        public static IQueryable<TElement> Intercept<TElement>(IQueryable<TElement> underlyingQuery, EnumerationExceptionHandler enumerationExceptionHandler, FeedResponseHandler feedResponseHandler, int maxRetries, TimeSpan maxTime, ShouldRetry shouldRetry)
        {
            var provider = new DocumentDbTranslatingReliableQueryProvider(underlyingQuery.Provider, enumerationExceptionHandler, feedResponseHandler, maxRetries, maxTime, shouldRetry, new DocumentDbTranslateExpressionVisitor(typeof(TElement)));
            return provider.CreateQuery<TElement>(underlyingQuery.Expression);
        }

        public static IQueryable<TElement> CreateForPagingContinuationOnly<TElement>(DocumentClient client, Uri collectionUri, FeedOptions feedOptions, EnumerationExceptionHandler enumerationExceptionHandler, FeedResponseHandler feedResponseHandler, int maxRetries, TimeSpan maxTime, ShouldRetry shouldRetry)
        {
            var provider = new DocumentDbTranslatingReliableQueryProvider(client, collectionUri, feedOptions, enumerationExceptionHandler, feedResponseHandler, maxRetries, maxTime, shouldRetry, new DocumentDbTranslateExpressionVisitor(typeof(TElement)));
            return provider.CreateQuery<TElement>(Expression.Variable(typeof(TElement)));
        }

        /// <summary>
        /// Intercept Execute to add DocumentDB retry and continuation logic
        /// </summary>
        /// <typeparam name="TElement"></typeparam>
        /// <param name="expression"></param>
        /// <returns></returns>
        public override IEnumerator<TElement> ExecuteQuery<TElement>(Expression expression)
        {
            if(mode != Mode.Intercept)
            {
                if (mode == Mode.InterceptWithPaging)
                    throw new InvalidOperationException(AlreadyExecutedNowPagingMessage);
                else if (mode == Mode.PagingContinuationOnly)
                    throw new InvalidOperationException(PagingContinuationOnlyMessage);
                else if (mode == Mode.Executed)
                    throw new InvalidOperationException(AlreadyExecutedMessage);
                else
                    throw new InvalidOperationException(InternalErrorMessage);
            }

            mode = Mode.Executed;

            var interceptedExpression = base.InterceptExpression(expression);
            IQueryable<TElement> query = underlyingProvider.CreateQuery<TElement>(interceptedExpression);
            var enumerable = DocumentDbReliableExecution.StreamQueryWithContinuationAndRetry(query, enumerationExceptionHandler, feedResponseHandler, maxRetries, maxTime, shouldRetry);
            return enumerable.GetEnumerator();
        }

        /// <summary>
        /// Intercept execute to add DocumentDB retry logic
        /// </summary>
        /// <typeparam name="TResult"></typeparam>
        /// <param name="expression"></param>
        /// <returns></returns>
        public override TResult Execute<TResult>(Expression expression)
        {
            if (mode != Mode.Intercept)
            {
                if (mode == Mode.InterceptWithPaging)
                    throw new InvalidOperationException(AlreadyExecutedNowPagingMessage);
                else if (mode == Mode.PagingContinuationOnly)
                    throw new InvalidOperationException(PagingContinuationOnlyMessage);
                else if (mode == Mode.Executed)
                    throw new InvalidOperationException(AlreadyExecutedMessage);
                else
                    throw new InvalidOperationException(InternalErrorMessage);
            }

            throw new ArgumentException("DocumentDB Client does not support queries which return a single result rather than a list (even if the list is expected to contain only one result) - please call .ToArray() or similar to get the result set first, and only then call .Single(), .First(), or similar");

            // todo: remove the "single result" portion from the expression tree, call ExecuteQuery(), and then execute the "single result" portion of the query locally
#if false
            var interceptedExpression = base.InterceptExpression(expression);
            var t = Task.Run(async () => await DocumentDbReliableExecution.ExecuteMethodWithRetry<TResult>(
                () => Task.Run(() => underlyingProvider.Execute<TResult>(interceptedExpression)),
                maxRetries,
                maxTime,
                shouldRetry));
            t.Wait();
            return t.Result;
#endif
        }

        /// <summary>
        /// Intercept execute to add DocumentDB retry logic
        /// </summary>
        /// <param name="expression"></param>
        /// <returns></returns>
        public override object Execute(Expression expression)
        {
            if (mode != Mode.Intercept)
            {
                if (mode == Mode.InterceptWithPaging)
                    throw new InvalidOperationException(AlreadyExecutedNowPagingMessage);
                else if (mode == Mode.PagingContinuationOnly)
                    throw new InvalidOperationException(PagingContinuationOnlyMessage);
                else if (mode == Mode.Executed)
                    throw new InvalidOperationException(AlreadyExecutedMessage);
                else
                    throw new InvalidOperationException(InternalErrorMessage);
            }

            throw new ArgumentException("DocumentDB Client does not support queries which return a single result rather than a list (even if the list is expected to contain only one result) - please call .ToArray() or similar to get the result set first, and only then call .Single(), .First(), or similar");

            // todo: remove the "single result" portion from the expression tree, call ExecuteQuery(), and then execute the "single result" portion of the query locally
#if false
            var interceptedExpression = base.InterceptExpression(expression);
            var t = Task.Run(async () => await DocumentDbReliableExecution.ExecuteMethodWithRetry<object>(
                () => Task.Run(() => underlyingProvider.Execute(interceptedExpression)),
                maxRetries,
                maxTime,
                shouldRetry));
            t.Wait();
            return t.Result;
#endif
        }

        internal async Task<DocumentsPage<TElement>> BeginPagingAsync<TElement>(Expression expression)
        {
            if (mode != Mode.Intercept)
            {
                if (mode == Mode.InterceptWithPaging)
                    throw new InvalidOperationException(AlreadyExecutedNowPagingMessage);
                else if (mode == Mode.PagingContinuationOnly)
                    throw new InvalidOperationException(PagingContinuationOnlyMessage);
                else if (mode == Mode.Executed)
                    throw new InvalidOperationException(AlreadyExecutedMessage);
                else
                    throw new InvalidOperationException(InternalErrorMessage);
            }

            // switch into paging mode, we'll preserve the IDocumentQuery<TElement> to track continuation
            //     call the GetNextPageAsync overload which takes no parameters to continue paging
            // but user may still throw this instance away and continue paging later by preserving the continuation token and 
            //     calling the CreateForPagingContinuationOnly factory, and then the GetNextPageAsync overload instead, which takes the user-preserved continuation token
            // note: all Begin/GetNext paging calls will be invoked via the PagingExtensions to IQueryable
            this.mode = Mode.InterceptWithPaging;

            var interceptedExpression = base.InterceptExpression(expression);
            IQueryable<TElement> query = underlyingProvider.CreateQuery<TElement>(interceptedExpression);

            try
            {
                this.pagingQuery = query.AsDocumentQuery();
            }
            catch (Exception e)
            {
                throw new ArgumentException(DocumentDbReliableExecution.BadQueryableMessage, e);
            }

            return await DocumentDbReliableExecution.BeginPagingWithRetry((IDocumentQuery<TElement>)this.pagingQuery, enumerationExceptionHandler, feedResponseHandler, maxRetries, maxTime, shouldRetry);
        }

        /// <summary>
        /// Used if you're keeping the IQueryable (and thus this provider) around, continuation is tracked internally to the query
        /// </summary>
        /// <typeparam name="TElement"></typeparam>
        /// <returns></returns>
        internal async Task<DocumentsPage<TElement>> GetNextPageAsync<TElement>()
        {
            if (mode != Mode.InterceptWithPaging)
            {
                if (mode == Mode.Intercept)
                    throw new InvalidOperationException(MustBeginPagingFirstMessage);
                else if (mode == Mode.PagingContinuationOnly)
                    throw new InvalidOperationException(PagingContinuationOnlyMessage);
                else if (mode == Mode.Executed)
                    throw new InvalidOperationException(AlreadyExecutedMessage);
                else
                    throw new InvalidOperationException(InternalErrorMessage);
            }

            var query = this.pagingQuery as IDocumentQuery<TElement>;

            return await DocumentDbReliableExecution.GetNextPageWithRetry<TElement>(query, enumerationExceptionHandler, feedResponseHandler, maxRetries, maxTime, shouldRetry);
        }

        /// <summary>
        /// Used if you're re-creating the IQueryable for the purposes of next-page only, in this case you must track the continuation
        /// </summary>
        /// <typeparam name="TElement"></typeparam>
        /// <param name="continuationToken"></param>
        /// <returns></returns>
        internal async Task<DocumentsPage<TElement>> GetNextPageAsync<TElement>(string continuationToken)
        {
            if (mode != Mode.PagingContinuationOnly)
            {
                if (mode == Mode.Intercept)
                    throw new InvalidOperationException(MustBeginPagingFirstMessage);
                else if (mode == Mode.InterceptWithPaging)
                    throw new InvalidOperationException(InstancePagingOnlyMessage);
                else if (mode == Mode.Executed)
                    throw new InvalidOperationException(AlreadyExecutedMessage);
                else
                    throw new InvalidOperationException(InternalErrorMessage);
            }

            pagingFeedOptions.RequestContinuation = continuationToken;

            IDocumentQuery<TElement> query = null;
            try
            {
                query = this.pagingClient.CreateDocumentQuery<TElement>(pagingCollectionUri, pagingFeedOptions).AsDocumentQuery();
            }
            catch (Exception e)
            {
                throw new ArgumentException(DocumentDbReliableExecution.BadQueryableMessage, e);
            }

            return await DocumentDbReliableExecution.GetNextPageWithRetry<TElement>(query, enumerationExceptionHandler, feedResponseHandler, maxRetries, maxTime, shouldRetry);
        }
    }
}
