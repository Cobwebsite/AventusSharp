using AventusSharp.Data.Manager.DB;
using AventusSharp.Tools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace AventusSharp.Data.Manager
{
    /// <summary>
    /// Interface for building and executing queries for a specific type.
    /// </summary>
    /// <typeparam name="T">The type of entity the query builder will work with.</typeparam>
    public interface IQueryBuilder<T>
    {
        /// <summary>
        /// Executes the query and returns a list of results.
        /// </summary>
        /// <returns>A list of type <typeparamref name="T"/>.</returns>
        public List<T> Run();

        /// <summary>
        /// Executes the query and returns a result with error handling.
        /// </summary>
        /// <returns>A ResultWithError containing a list of <typeparamref name="T"/>.</returns>
        public ResultWithError<List<T>> RunWithError();

        /// <summary>
        /// Executes the query and returns a single result.
        /// </summary>
        /// <returns>A single <typeparamref name="T"/> object, or null if no result is found.</returns>
        public T? Single();

        /// <summary>
        /// Executes the query and returns a single result with error handling.
        /// </summary>
        /// <returns>A ResultWithError containing a single <typeparamref name="T"/> object.</returns>
        public ResultWithError<T> SingleWithError();

        /// <summary>
        /// Prepares the query by adding parameters or additional objects.
        /// </summary>
        /// <param name="objects">Objects to be used in preparing the query.</param>
        /// <returns>The current query builder instance for method chaining.</returns>
        public IQueryBuilder<T> Prepare(params object[] objects);

        /// <summary>
        /// Sets a variable for the query.
        /// </summary>
        /// <param name="name">The name of the variable.</param>
        /// <param name="value">The value of the variable.</param>
        /// <returns>The current query builder instance for method chaining.</returns>
        public IQueryBuilder<T> SetVariable(string name, object value);

        /// <summary>
        /// Adds a condition to the query using the provided expression.
        /// </summary>
        /// <param name="func">The condition to apply to the query.</param>
        /// <returns>The current query builder instance for method chaining.</returns>
        public IQueryBuilder<T> Where(Expression<Func<T, bool>> func);

        /// <summary>
        /// Adds a condition to the query with parameters for the provided expression.
        /// </summary>
        /// <param name="func">The condition to apply to the query.</param>
        /// <returns>The current query builder instance for method chaining.</returns>
        public IQueryBuilder<T> WhereWithParameters(Expression<Func<T, bool>> func);

        /// <summary>
        /// Specifies a field to be included in the query results.
        /// </summary>
        /// <typeparam name="U">The type of the field to include.</typeparam>
        /// <param name="memberExpression">The expression representing the field to include.</param>
        /// <returns>The current query builder instance for method chaining.</returns>
        public IQueryBuilder<T> Field<U>(Expression<Func<T, U>> memberExpression);

        /// <summary>
        /// Specifies sorting for the query based on the provided expression and sorting order.
        /// </summary>
        /// <typeparam name="U">The type of the field to sort by.</typeparam>
        /// <param name="expression">The field to sort by.</param>
        /// <param name="sort">The sorting order (ascending or descending).</param>
        /// <returns>The current query builder instance for method chaining.</returns>
        public IQueryBuilder<T> Sort<U>(Expression<Func<T, U>> expression, Sort? sort);

        /// <summary>
        /// Includes a related object in the query.
        /// </summary>
        /// <param name="memberExpression">The expression representing the related object to include.</param>
        /// <returns>The current query builder instance for method chaining.</returns>
        public IQueryBuilder<T> Include(Expression<Func<T, IStorable>> memberExpression);

        /// <summary>
        /// Limits the number of results returned by the query.
        /// </summary>
        /// <param name="limit">The maximum number of results to return.</param>
        /// <returns>The current query builder instance for method chaining.</returns>
        public IQueryBuilder<T> Limit(int? limit);

        /// <summary>
        /// Skips a number of results and then limits the remaining results.
        /// </summary>
        /// <param name="offset">The number of results to skip.</param>
        /// <returns>The current query builder instance for method chaining.</returns>
        public IQueryBuilder<T> Offset(int? offset);

        /// <summary>
        /// Specifies the number of results to return.
        /// </summary>
        /// <param name="length">The number of results to return.</param>
        /// <returns>The current query builder instance for method chaining.</returns>
        public IQueryBuilder<T> Take(int length);

        /// <summary>
        /// Specifies the number of results to return, starting from a given offset.
        /// </summary>
        /// <param name="length">The number of results to return.</param>
        /// <param name="offset">The number of results to skip before returning results.</param>
        /// <returns>The current query builder instance for method chaining.</returns>
        public IQueryBuilder<T> Take(int length, int offset);
    }
}
