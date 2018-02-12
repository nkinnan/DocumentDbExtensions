using Microsoft.Azure.Documents;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Azure.Documents
{
    /// <summary>
    /// Implements extension methods for IQueryable to enable paging of results from DocumentDB
    /// </summary>
    public static class PagingIQueryableExtensions
    {
        private const string QueryNotInterceptedMessage = "This method will only work on IQueryables created via DocumentDbExtensions.InterceptQuery() or DocumentDbExtensions.CreateQueryForPagingContinuationOnly()";

        /// <summary>
        /// Starts paging of query results
        /// </summary>
        /// <typeparam name="T">Returned type</typeparam>
        /// <param name="query">Instance of IQueryable to operate on</param>
        /// <returns>The first page of results</returns>
        public static DocumentsPage<T> BeginPaging<T>(this IQueryable<T> query)
        {
            return BeginPagingAsync(query).Result;
        }

        /// <summary>
        /// Starts paging of query results
        /// </summary>
        /// <typeparam name="T">Returned type</typeparam>
        /// <param name="query">Instance of IQueryable to operate on</param>
        /// <returns>The first page of results</returns>
        public static async Task<DocumentsPage<T>> BeginPagingAsync<T>(this IQueryable<T> query)
        {
            var wrapper = query.Provider as DocumentDbTranslatingReliableQueryProvider;
            if(wrapper == null)
            {
                throw new InvalidOperationException(QueryNotInterceptedMessage);
            }

            return await wrapper.BeginPagingAsync<T>(query.Expression);
        }

        /// <summary>
        /// Gets the next page of query results
        /// </summary>
        /// <typeparam name="T">Returned type</typeparam>
        /// <param name="query">Instance of IQueryable to operate on</param>
        /// <param name="continuationToken">The continuation token to use</param>
        /// <returns>The next page of results</returns>
        public static DocumentsPage<T> GetNextPage<T>(this IQueryable<T> query, string continuationToken)
        {
            return GetNextPageAsync(query, continuationToken).Result;
        }

        /// <summary>
        /// Gets the next page of query results
        /// </summary>
        /// <typeparam name="T">Returned type</typeparam>
        /// <param name="query">Instance of IQueryable to operate on</param>
        /// <param name="continuationToken">The continuation token to use</param>
        /// <returns>The next page of results</returns>
        public static async Task<DocumentsPage<T>> GetNextPageAsync<T>(this IQueryable<T> query, string continuationToken)
        {
            var wrapper = query.Provider as DocumentDbTranslatingReliableQueryProvider;
            if (wrapper == null)
            {
                throw new InvalidOperationException(QueryNotInterceptedMessage);
            }

            return await wrapper.GetNextPageAsync<T>(continuationToken);
        }

        /// <summary>
        /// Gets the next page of query results
        /// </summary>
        /// <typeparam name="T">Returned type</typeparam>
        /// <param name="query">Instance of IQueryable to operate on</param>
        /// <returns>The next page of results</returns>
        public static DocumentsPage<T> GetNextPage<T>(this IQueryable<T> query)
        {
            return GetNextPageAsync(query).Result;
        }

        /// <summary>
        /// Gets the next page of query results
        /// </summary>
        /// <typeparam name="T">Returned type</typeparam>
        /// <param name="query">Instance of IQueryable to operate on</param>
        /// <returns>The next page of results</returns>
        public static async Task<DocumentsPage<T>> GetNextPageAsync<T>(this IQueryable<T> query)
        {
            var wrapper = query.Provider as DocumentDbTranslatingReliableQueryProvider;
            if (wrapper == null)
            {
                throw new InvalidOperationException(QueryNotInterceptedMessage);
            }

            return await wrapper.GetNextPageAsync<T>();
        }
    }
}
