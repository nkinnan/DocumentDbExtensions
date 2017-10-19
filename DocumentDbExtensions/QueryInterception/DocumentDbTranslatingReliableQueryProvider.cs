using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace Microsoft.Azure.Documents
{
    internal class DocumentDbTranslatingReliableQueryProvider : InterceptingQueryProvider
    {
        private EnumerationExceptionHandler enumerationExceptionHandler;
        private FeedResponseHandler feedResponseHandler;
        private TimeSpan maxTime;
        private int maxRetries;
        private ShouldRetry shouldRetry;

        private DocumentDbTranslatingReliableQueryProvider(IQueryProvider underlyingProvider, EnumerationExceptionHandler enumerationExceptionHandler, FeedResponseHandler feedResponseHandler, int maxRetries, TimeSpan maxTime, ShouldRetry shouldRetry, params ExpressionVisitor[] visitors)
            : base(underlyingProvider, visitors)
        {
            this.enumerationExceptionHandler = enumerationExceptionHandler;
            this.feedResponseHandler = feedResponseHandler;
            this.maxRetries = maxRetries;
            this.maxTime = maxTime;
            this.shouldRetry = shouldRetry;
        }

        /// <summary>
        /// Begins interception of query evaluation, wraps the passed in IQueryable
        /// </summary>
        /// <typeparam name="TElement"></typeparam>
        /// <param name="underlyingQuery"></param>
        /// <param name="enumerationExceptionHandler"></param>
        /// <param name="maxRetries"></param>
        /// <param name="maxTime"></param>
        /// <param name="shouldRetry"></param>
        /// <returns></returns>
        public static IQueryable<TElement> Intercept<TElement>(IQueryable<TElement> underlyingQuery, EnumerationExceptionHandler enumerationExceptionHandler, FeedResponseHandler feedResponseHandler, int maxRetries, TimeSpan maxTime, ShouldRetry shouldRetry)
        {
            var provider = new DocumentDbTranslatingReliableQueryProvider(underlyingQuery.Provider, enumerationExceptionHandler, feedResponseHandler, maxRetries, maxTime, shouldRetry, new DocumentDbTranslateExpressionVisitor(typeof(TElement)));
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
    }
}
