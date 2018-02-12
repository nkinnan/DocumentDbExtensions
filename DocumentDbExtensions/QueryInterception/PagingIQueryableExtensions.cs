using Microsoft.Azure.Documents;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Azure.Documents
{
    public static class PagingIQueryableExtensions
    {
        private const string QueryNotInterceptedMessage = "This method will only work on IQueryables created via DocumentDbExtensions.InterceptQuery() or DocumentDbExtensions.CreateQueryForPagingContinuationOnly()";

        public static DocumentsPage<T> BeginPaging<T>(this IQueryable<T> query)
        {
            return BeginPagingAsync(query).Result;
        }

        public static async Task<DocumentsPage<T>> BeginPagingAsync<T>(this IQueryable<T> query)
        {
            var wrapper = query.Provider as DocumentDbTranslatingReliableQueryProvider;
            if(wrapper == null)
            {
                throw new InvalidOperationException(QueryNotInterceptedMessage);
            }

            return await wrapper.BeginPagingAsync<T>(query.Expression);
        }

        public static DocumentsPage<T> GetNextPage<T>(this IQueryable<T> query, string continuationToken)
        {
            return GetNextPageAsync(query, continuationToken).Result;
        }

        public static async Task<DocumentsPage<T>> GetNextPageAsync<T>(this IQueryable<T> query, string continuationToken)
        {
            var wrapper = query.Provider as DocumentDbTranslatingReliableQueryProvider;
            if (wrapper == null)
            {
                throw new InvalidOperationException(QueryNotInterceptedMessage);
            }

            return await wrapper.GetNextPageAsync<T>(continuationToken);
        }

        public static DocumentsPage<T> GetNextPage<T>(this IQueryable<T> query)
        {
            return GetNextPageAsync(query).Result;
        }

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
