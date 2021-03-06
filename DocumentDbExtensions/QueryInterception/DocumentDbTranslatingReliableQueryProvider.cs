﻿using Microsoft.Azure.Documents.Client;
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
            ResumePaging,
            Executed
        };

        Mode mode = Mode.Invalid;

        private QueryExecutionHandler queryExecutionHandler;
        private EnumerationExceptionHandler enumerationExceptionHandler;
        private FeedResponseHandler feedResponseHandler;
        private TimeSpan maxTime;
        private int maxRetries;
        private ShouldRetry shouldRetry;

        Object pagingQuery;

        private const string AlreadyExecutedNowPagingMessage = "This query has already been executed and is in paging mode, call GetNextPage() instead.";
        private const string AlreadyExecutedMessage = "This query has already been executed.";
        private const string InstancePagingOnlyMessage = "This query is tracking paging internally, only calls to GetNextPage() without the continuationToken parameter are allowed.";
        private const string ResumePagingMessage = "This query was created with the InterceptForPagingContinuationOnly factory method, only calls to GetNextPage() are allowed.";
        private const string MustBeginPagingFirstMessage = "BeginPaging() must be called before attempting to get the next page.";
        private const string InternalErrorMessage = "Internal error: Unknown or unhandled execution mode";

        private DocumentDbTranslatingReliableQueryProvider(IQueryProvider underlyingProvider, QueryExecutionHandler queryExecutionHandler, EnumerationExceptionHandler enumerationExceptionHandler, FeedResponseHandler feedResponseHandler, int maxRetries, TimeSpan maxTime, ShouldRetry shouldRetry, params ExpressionVisitor[] visitors)
            : base(underlyingProvider, visitors)
        {
            this.queryExecutionHandler = queryExecutionHandler;
            this.enumerationExceptionHandler = enumerationExceptionHandler;
            this.feedResponseHandler = feedResponseHandler;
            this.maxRetries = maxRetries;
            this.maxTime = maxTime;
            this.shouldRetry = shouldRetry;

            this.mode = Mode.Intercept;
        }


        /// <summary>
        /// Begins interception of query evaluation, wraps the passed in IQueryable
        /// </summary>
        /// <typeparam name="TElement"></typeparam>
        /// <param name="underlyingQuery"></param>
        /// <param name="queryExecutionHandler"></param>
        /// <param name="enumerationExceptionHandler"></param>
        /// <param name="feedResponseHandler"></param>
        /// <param name="maxRetries"></param>
        /// <param name="maxTime"></param>
        /// <param name="shouldRetry"></param>
        /// <returns></returns>
        public static IQueryable<TElement> Intercept<TElement>(IQueryable<TElement> underlyingQuery, QueryExecutionHandler queryExecutionHandler, EnumerationExceptionHandler enumerationExceptionHandler, FeedResponseHandler feedResponseHandler, int maxRetries, TimeSpan maxTime, ShouldRetry shouldRetry)
        {
            var provider = new DocumentDbTranslatingReliableQueryProvider(underlyingQuery.Provider, queryExecutionHandler, enumerationExceptionHandler, feedResponseHandler, maxRetries, maxTime, shouldRetry, new DocumentDbTranslateExpressionVisitor(typeof(TElement)));
            return provider.CreateQuery<TElement>(underlyingQuery.Expression);
        }

        public static IQueryable<TElement> InterceptForPagingContinuationOnly<TElement>(IQueryable<TElement> underlyingQuery, QueryExecutionHandler queryExecutionHandler, EnumerationExceptionHandler enumerationExceptionHandler, FeedResponseHandler feedResponseHandler, int maxRetries, TimeSpan maxTime, ShouldRetry shouldRetry)
        {
            var provider = new DocumentDbTranslatingReliableQueryProvider(underlyingQuery.Provider, queryExecutionHandler, enumerationExceptionHandler, feedResponseHandler, maxRetries, maxTime, shouldRetry, new DocumentDbTranslateExpressionVisitor(typeof(TElement)));
            provider.mode = Mode.ResumePaging;
            return provider.CreateQuery<TElement>(underlyingQuery.Expression);
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
                else if (mode == Mode.ResumePaging)
                    throw new InvalidOperationException(ResumePagingMessage);
                else if (mode == Mode.Executed)
                    throw new InvalidOperationException(AlreadyExecutedMessage);
                else
                    throw new InvalidOperationException(InternalErrorMessage);
            }

            mode = Mode.Executed;

            var interceptedExpression = base.InterceptExpression(expression);
            IQueryable<TElement> query = underlyingProvider.CreateQuery<TElement>(interceptedExpression);
            var enumerable = DocumentDbReliableExecution.StreamQueryWithContinuationAndRetry(query, queryExecutionHandler, enumerationExceptionHandler, feedResponseHandler, maxRetries, maxTime, shouldRetry);
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
                else if (mode == Mode.ResumePaging)
                    throw new InvalidOperationException(ResumePagingMessage);
                else if (mode == Mode.Executed)
                    throw new InvalidOperationException(AlreadyExecutedMessage);
                else
                    throw new InvalidOperationException(InternalErrorMessage);
            }

            var interceptedExpression = base.InterceptExpression(expression);
            var t = Task.Run(async () => await DocumentDbReliableExecution.ExecuteResultWithRetry<TResult>(
                () => underlyingProvider.Execute<TResult>(interceptedExpression),
                null,
                maxRetries,
                maxTime,
                shouldRetry));
            t.Wait();
            return t.Result;
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
                else if (mode == Mode.ResumePaging)
                    throw new InvalidOperationException(ResumePagingMessage);
                else if (mode == Mode.Executed)
                    throw new InvalidOperationException(AlreadyExecutedMessage);
                else
                    throw new InvalidOperationException(InternalErrorMessage);
            }

            var interceptedExpression = base.InterceptExpression(expression);
            var t = DocumentDbReliableExecution.ExecuteResultWithRetry<object>(
                () => underlyingProvider.Execute(interceptedExpression),
                null,
                maxRetries,
                maxTime,
                shouldRetry);
            t.Wait();
            return t.Result;
        }

        private void SwitchToPagingMode<TElement>(Expression expression)
        {
            // switch into paging mode, we'll preserve the IDocumentQuery<TElement> to track continuation
            //     call the GetNextPageAsync overload which takes no parameters to continue paging
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
        }

        internal async Task<DocumentsPage<TElement>> BeginPagingAsync<TElement>(Expression expression)
        {
            if (mode != Mode.Intercept)
            {
                if (mode == Mode.InterceptWithPaging)
                    throw new InvalidOperationException(AlreadyExecutedNowPagingMessage);
                else if (mode == Mode.ResumePaging)
                    throw new InvalidOperationException(ResumePagingMessage);
                else if (mode == Mode.Executed)
                    throw new InvalidOperationException(AlreadyExecutedMessage);
                else
                    throw new InvalidOperationException(InternalErrorMessage);
            }

            SwitchToPagingMode<TElement>(expression);

            return await DocumentDbReliableExecution.BeginPagingWithRetry((IDocumentQuery<TElement>)this.pagingQuery, queryExecutionHandler, enumerationExceptionHandler, feedResponseHandler, maxRetries, maxTime, shouldRetry);
        }

        /// <summary>
        /// Used if you're keeping the IQueryable (and thus this provider) around, continuation is tracked internally to the query
        /// </summary>
        /// <typeparam name="TElement"></typeparam>
        /// <returns></returns>
        internal async Task<DocumentsPage<TElement>> GetNextPageAsync<TElement>(Expression expression)
        {
            if (mode == Mode.ResumePaging)
            {
                SwitchToPagingMode<TElement>(expression);
            }

            if (mode != Mode.InterceptWithPaging)
            {
                if (mode == Mode.Intercept)
                    throw new InvalidOperationException(MustBeginPagingFirstMessage);
                else if (mode == Mode.Executed)
                    throw new InvalidOperationException(AlreadyExecutedMessage);
                else
                    throw new InvalidOperationException(InternalErrorMessage);
            }

            var query = this.pagingQuery as IDocumentQuery<TElement>;

            return await DocumentDbReliableExecution.GetNextPageWithRetry<TElement>(query, queryExecutionHandler, enumerationExceptionHandler, feedResponseHandler, maxRetries, maxTime, shouldRetry);
        }
    }
}
