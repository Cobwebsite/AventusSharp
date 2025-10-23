using AventusSharp.Tools;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;

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
        public DeleteBuilderPrepared<T> WhereWithParameters(Expression<Func<T, bool>> func);

        internal void PrepareInternal(params object[] objects);
        internal void SetVariableInternal(string name, object value);
    }

    public class DeleteBuilderPrepared<T>
    {
        private Mutex mutex;
        private IDeleteBuilder<T> builder;
        public DeleteBuilderPrepared(IDeleteBuilder<T> builder)
        {
            this.builder = builder;
            mutex = new();
        }

        /// <summary>
        /// Start a new query where you can call Prepare or SetVariables
        /// </summary>
        /// <returns></returns>
        public DeleteBuilderPreparedInstance<T> New()
        {
            mutex.WaitOne();
            return new DeleteBuilderPreparedInstance<T>(builder, this);
        }

        internal void Done()
        {
            mutex.ReleaseMutex();
        }

    }
    public class DeleteBuilderPreparedInstance<T>
    {
        private IDeleteBuilder<T> builder;
        private DeleteBuilderPrepared<T> prepared;
        public DeleteBuilderPreparedInstance(IDeleteBuilder<T> builder, DeleteBuilderPrepared<T> prepared)
        {
            this.builder = builder;
            this.prepared = prepared;
        }


        /// <summary>
        /// Prepares the query by adding parameters or additional objects.
        /// </summary>
        /// <param name="objects">Objects to be used in preparing the query.</param>
        /// <returns>The current query builder instance for method chaining.</returns>
        public DeleteBuilderPreparedInstance<T> Prepare(params object[] objects)
        {
            builder.PrepareInternal(objects);
            return this;
        }
        /// <summary>
        /// Sets all variables for the query.
        /// </summary>
        /// <param name="define">The fct to define the variable.</param>
        /// <returns>The current query builder instance for method chaining.</returns>
        public DeleteBuilderPreparedInstance<T> SetVariables(Action<Action<string, object>> define)
        {
            define(builder.SetVariableInternal);
            return this;
        }
        /// <summary>
        /// Executes the query and returns a list of results.
        /// </summary>
        /// <returns>A list of type <typeparamref name="T"/>.</returns>
        public List<T>? Run()
        {
            List<T>? result = builder.Run();
            prepared.Done();
            return result;
        }
        /// <summary>
        /// Executes the query and returns a result with error handling.
        /// </summary>
        /// <returns>A ResultWithError containing a list of <typeparamref name="T"/>.</returns>
        public ResultWithError<List<T>> RunWithError()
        {
            ResultWithError<List<T>> result = builder.RunWithError();
            prepared.Done();
            return result;
        }
    }

}
