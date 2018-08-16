using Microsoft.Azure.Documents.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace Microsoft.Azure.Documents
{
    /// <summary>
    /// Allows evaluation of IQueryables to be "hooked" in conjunction with InterceptingQuery.
    /// </summary>
    internal abstract class InterceptingQueryProvider : IQueryProvider
    {
        protected readonly IQueryProvider underlyingProvider;
        protected readonly ExpressionVisitor[] visitors;

        protected InterceptingQueryProvider(IQueryProvider underlyingProvider, params ExpressionVisitor[] visitors)
        {
            this.underlyingProvider = underlyingProvider;
            this.visitors = visitors;
        }

        public virtual IQueryable<TElement> CreateQuery<TElement>(Expression expression)
        {
            IQueryable<TElement> queryable = underlyingProvider.CreateQuery<TElement>(expression);
            return new InterceptingQuery<TElement>(queryable, this);
        }

        public virtual IQueryable CreateQuery(Expression expression)
        {
            IQueryable queryable = underlyingProvider.CreateQuery(expression);
            Type elementType = queryable.ElementType;
            Type queryType = typeof(InterceptingQuery<>).MakeGenericType(elementType);
            return (IQueryable)Activator.CreateInstance(queryType, queryable, this);
        }

        public virtual IEnumerator<TElement> ExecuteQuery<TElement>(Expression expression)
        {
            var interceptedExpression = InterceptExpression(expression);
            IQueryable<TElement> query = underlyingProvider.CreateQuery<TElement>(interceptedExpression);
            IEnumerator<TElement> enumerator = query.GetEnumerator();
            return enumerator;
        }

        public virtual TResult Execute<TResult>(Expression expression)
        {
            var interceptedExpression = InterceptExpression(expression);
            return underlyingProvider.Execute<TResult>(interceptedExpression);
        }

        public virtual object Execute(Expression expression)
        {
            var interceptedExpression = InterceptExpression(expression);
            return underlyingProvider.Execute(interceptedExpression);
        }

        protected Expression InterceptExpression(Expression expression)
        {
            Expression exp = expression;
            foreach (var visitor in visitors)
            {
                exp = visitor.Visit(exp);
            }
            return exp;
        } 
    }
}