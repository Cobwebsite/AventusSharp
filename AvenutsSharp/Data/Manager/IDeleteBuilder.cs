using AventusSharp.Tools;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace AventusSharp.Data.Manager
{
    /// <summary>
    /// Interface for building and executing delete queries for a specific type.
    /// </summary>
    /// <typeparam name="T">The type of entity the delete builder will work with.</typeparam>
    public interface IDeleteBuilder<T>
    {
        /// <summary>
        /// Executes the delete operation and returns a list of deleted items.
        /// </summary>
        /// <returns>A list of deleted <typeparamref name="T"/> objects, or null if no items were deleted.</returns>
        public List<T>? Run();

        /// <summary>
        /// Executes the delete operation and returns a result with error handling.
        /// </summary>
        /// <returns>A ResultWithError containing a list of deleted <typeparamref name="T"/> objects.</returns>
        public ResultWithError<List<T>> RunWithError();

        /// <summary>
        /// Prepares the delete query by adding parameters or additional objects.
        /// </summary>
        /// <param name="objects">Objects to be used in preparing the delete query.</param>
        /// <returns>The current delete builder instance for method chaining.</returns>
        public IDeleteBuilder<T> Prepare(params object[] objects);

        /// <summary>
        /// Sets a variable for the delete query.
        /// </summary>
        /// <param name="name">The name of the variable.</param>
        /// <param name="value">The value of the variable.</param>
        /// <returns>The current delete builder instance for method chaining.</returns>
        public IDeleteBuilder<T> SetVariable(string name, object value);

        /// <summary>
        /// Adds a condition to the delete query using the provided expression.
        /// </summary>
        /// <param name="func">The condition to apply to the delete query.</param>
        /// <returns>The current delete builder instance for method chaining.</returns>
        public IDeleteBuilder<T> Where(Expression<Func<T, bool>> func);

        /// <summary>
        /// Adds a condition to the delete query with parameters for the provided expression.
        /// </summary>
        /// <param name="func">The condition to apply to the delete query.</param>
        /// <returns>The current delete builder instance for method chaining.</returns>
        public IDeleteBuilder<T> WhereWithParameters(Expression<Func<T, bool>> func);
    }
}
