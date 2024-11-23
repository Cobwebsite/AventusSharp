using AventusSharp.Tools;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace AventusSharp.Data.Manager
{
    /// <summary>
    /// Interface for building and executing update queries for a specific type.
    /// </summary>
    /// <typeparam name="T">The type of entity the update builder will work with.</typeparam>
    public interface IUpdateBuilder<T>
    {
        /// <summary>
        /// Executes the update operation and returns a list of updated items.
        /// </summary>
        /// <param name="item">The item to be updated.</param>
        /// <returns>A list of updated <typeparamref name="T"/> objects, or null if no items were updated.</returns>
        public List<T>? Run(T item);

        /// <summary>
        /// Executes the update operation and returns a result with error handling.
        /// </summary>
        /// <param name="item">The item to be updated.</param>
        /// <returns>A ResultWithError containing a list of updated <typeparamref name="T"/> objects.</returns>
        public ResultWithError<List<T>> RunWithError(T item);

        /// <summary>
        /// Executes the update operation and returns a result with error handling for a single updated item.
        /// </summary>
        /// <param name="item">The item to be updated.</param>
        /// <returns>A ResultWithError containing a single updated <typeparamref name="T"/> object.</returns>
        public ResultWithError<T> RunWithErrorSingle(T item);

        /// <summary>
        /// Specifies a field to be updated in the query.
        /// </summary>
        /// <typeparam name="U">The type of the field to update.</typeparam>
        /// <param name="fct">The expression representing the field to update.</param>
        /// <returns>The current update builder instance for method chaining.</returns>
        public IUpdateBuilder<T> Field<U>(Expression<Func<T, U>> fct);

        /// <summary>
        /// Prepares the update query by adding parameters or additional objects.
        /// </summary>
        /// <param name="objects">Objects to be used in preparing the update query.</param>
        /// <returns>The current update builder instance for method chaining.</returns>
        public IUpdateBuilder<T> Prepare(params object[] objects);

        /// <summary>
        /// Sets a variable for the update query.
        /// </summary>
        /// <param name="name">The name of the variable.</param>
        /// <param name="value">The value of the variable.</param>
        /// <returns>The current update builder instance for method chaining.</returns>
        public IUpdateBuilder<T> SetVariable(string name, object value);

        /// <summary>
        /// Adds a condition to the update query using the provided expression.
        /// </summary>
        /// <param name="func">The condition to apply to the update query.</param>
        /// <returns>The current update builder instance for method chaining.</returns>
        public IUpdateBuilder<T> Where(Expression<Func<T, bool>> func);

        /// <summary>
        /// Adds a condition to the update query with parameters for the provided expression.
        /// </summary>
        /// <param name="func">The condition to apply to the update query.</param>
        /// <returns>The current update builder instance for method chaining.</returns>
        public IUpdateBuilder<T> WhereWithParameters(Expression<Func<T, bool>> func);
    }
}
