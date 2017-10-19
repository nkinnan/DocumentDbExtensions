using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace Microsoft.Azure.Documents
{
    /// <summary>
    /// Allows evaluation of IQueryables to be "hooked" in conjunction with InterceptingQueryProvider.
    /// </summary>
    /// <typeparam name="TElement"></typeparam>
    internal class InterceptingQuery<TElement> : IQueryable<TElement>, IOrderedQueryable<TElement>
    {
        private readonly IQueryable underlyingQueryable;
        private readonly InterceptingQueryProvider interceptingProvider;

        public InterceptingQuery(IQueryable underlyingQueryable, InterceptingQueryProvider interceptingProvider)
        {
            this.underlyingQueryable = underlyingQueryable;
            this.interceptingProvider = interceptingProvider;
        }

        // this is something to do with intercepting EF queryables, we don't need it, leaving it for completeness
        ////public IQueryable<TElement> Include(string path)
        ////{
        ////    return new InterceptingQuery<TElement>(underlyingQueryable.Include(path), interceptingProvider);
        ////}

        public IEnumerator<TElement> GetEnumerator()
        {
            Expression expression = underlyingQueryable.Expression;
            return interceptingProvider.ExecuteQuery<TElement>(expression);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public Type ElementType
        {
            get { return typeof(TElement); }
        }

        public Expression Expression
        {
            get { return underlyingQueryable.Expression; }
        }

        public IQueryProvider Provider
        {
            get { return interceptingProvider; }
        }
    }
}