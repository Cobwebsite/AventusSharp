using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using AventusSharp.Tools;

namespace AventusSharp.Data.Manager
{
    /// <summary>
    /// Interface for building and executing existence checks for a specific type.
    /// </summary>
    /// <typeparam name="T">The type of entity the existence builder will check.</typeparam>
    public interface IExistBuilder<T>
    {
        /// <summary>
        /// Executes the existence check and returns a boolean indicating if the item exists.
        /// </summary>
        /// <returns>True if the item exists, otherwise false.</returns>
        public bool Run();

        /// <summary>
        /// Executes the existence check and returns a result with error handling.
        /// </summary>
        /// <returns>A ResultWithError containing a boolean indicating existence (true or false).</returns>
        public ResultWithError<bool> RunWithError();

        /// <summary>
        /// Prepares the existence check by adding parameters or additional objects.
        /// </summary>
        /// <param name="objects">Objects to be used in preparing the existence check.</param>
        /// <returns>The current existence builder instance for method chaining.</returns>
        public IExistBuilder<T> Prepare(params object[] objects);

        /// <summary>
        /// Sets a variable for the existence check.
        /// </summary>
        /// <param name="name">The name of the variable.</param>
        /// <param name="value">The value of the variable.</param>
        /// <returns>The current existence builder instance for method chaining.</returns>
        public IExistBuilder<T> SetVariable(string name, object value);

        /// <summary>
        /// Adds a condition to the existence check using the provided expression.
        /// </summary>
        /// <param name="func">The condition to apply to the existence check.</param>
        /// <returns>The current existence builder instance for method chaining.</returns>
        public IExistBuilder<T> Where(Expression<Func<T, bool>> func);

        /// <summary>
        /// Adds a condition to the existence check with parameters for the provided expression.
        /// </summary>
        /// <param name="func">The condition to apply to the existence check.</param>
        /// <returns>The current existence builder instance for method chaining.</returns>
        public IExistBuilder<T> WhereWithParameters(Expression<Func<T, bool>> func);
    }
}
