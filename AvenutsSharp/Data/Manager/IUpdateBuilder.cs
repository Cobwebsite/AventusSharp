using AventusSharp.Tools;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;

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
        /// <returns>A single updated <typeparamref name="T"/> object.</returns>
        public T? Single(T item);
        /// <summary>
        /// Executes the update operation and returns a result with error handling for a single updated item.
        /// </summary>
        /// <param name="item">The item to be updated.</param>
        /// <returns>A ResultWithError containing a single updated <typeparamref name="T"/> object.</returns>
        public ResultWithError<T> SingleWithError(T item);

        /// <summary>
        /// Specifies a field to be updated in the query.
        /// </summary>
        /// <typeparam name="U">The type of the field to update.</typeparam>
        /// <param name="fct">The expression representing the field to update.</param>
        /// <returns>The current update builder instance for method chaining.</returns>
        public IUpdateBuilder<T> Field<U>(Expression<Func<T, U>> fct);

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
        public UpdateBuilderPrepared<T> WhereWithParameters(Expression<Func<T, bool>> func);

        internal void PrepareInternal(params object[] objects);
        internal void SetVariableInternal(string name, object value);
    }

     public class UpdateBuilderPrepared<T>
    {
        private Mutex mutex;
        private IUpdateBuilder<T> builder;
        public UpdateBuilderPrepared(IUpdateBuilder<T> builder)
        {
            this.builder = builder;
            mutex = new();
        }

        /// <summary>
        /// Start a new query where you can call Prepare or SetVariables
        /// </summary>
        /// <returns></returns>
        public UpdateBuilderPreparedInstance<T> New()
        {
            mutex.WaitOne();
            return new UpdateBuilderPreparedInstance<T>(builder, this);
        }

        internal void Done()
        {
            mutex.ReleaseMutex();
        }


        /// <summary>
        /// Specifies a field to be included in the query results.
        /// </summary>
        /// <typeparam name="U">The type of the field to include.</typeparam>
        /// <param name="expression">The expression representing the field to include.</param>
        /// <returns>The current query builder instance for method chaining.</returns>
        public UpdateBuilderPrepared<T> Field<U>(Expression<Func<T, U>> expression)
        {
            builder.Field(expression);
            return this;
        }

    }
    public class UpdateBuilderPreparedInstance<T>
    {
        private IUpdateBuilder<T> builder;
        private UpdateBuilderPrepared<T> prepared;
        public UpdateBuilderPreparedInstance(IUpdateBuilder<T> builder, UpdateBuilderPrepared<T> prepared)
        {
            this.builder = builder;
            this.prepared = prepared;
        }


        /// <summary>
        /// Prepares the query by adding parameters or additional objects.
        /// </summary>
        /// <param name="objects">Objects to be used in preparing the query.</param>
        /// <returns>The current query builder instance for method chaining.</returns>
        public UpdateBuilderPreparedInstance<T> Prepare(params object[] objects)
        {
            builder.PrepareInternal(objects);
            return this;
        }
        /// <summary>
        /// Sets all variables for the query.
        /// </summary>
        /// <param name="define">The fct to define the variable.</param>
        /// <returns>The current query builder instance for method chaining.</returns>
        public UpdateBuilderPreparedInstance<T> SetVariables(Action<Action<string, object>> define)
        {
            define(builder.SetVariableInternal);
            return this;
        }
        /// <summary>
        /// Executes the update operation and returns a list of updated items.
        /// </summary>
        /// <param name="item">The item to be updated.</param>
        /// <returns>A list of updated <typeparamref name="T"/> objects, or null if no items were updated.</returns>
        public List<T>? Run(T item)
        {
            List<T>? result = builder.Run(item);
            prepared.Done();
            return result;
        }
        /// <summary>
        /// Executes the update operation and returns a result with error handling.
        /// </summary>
        /// <param name="item">The item to be updated.</param>
        /// <returns>A ResultWithError containing a list of updated <typeparamref name="T"/> objects.</returns>
        public ResultWithError<List<T>> RunWithError(T item)
        {
            ResultWithError<List<T>> result = builder.RunWithError(item);
            prepared.Done();
            return result;
        }

        /// <summary>
        /// Executes the update operation and returns a result with error handling for a single updated item.
        /// </summary>
        /// <param name="item">The item to be updated.</param>
        /// <returns>A single updated <typeparamref name="T"/> object.</returns>
        public T? Single(T item)
        {
            T? result = builder.Single(item);
            prepared.Done();
            return result;
        }
        /// <summary>
        /// Executes the update operation and returns a result with error handling for a single updated item.
        /// </summary>
        /// <param name="item">The item to be updated.</param>
        /// <returns>A ResultWithError containing a single updated <typeparamref name="T"/> object.</returns>
        public ResultWithError<T> SingleWithError(T item)
        {
            ResultWithError<T> result = builder.SingleWithError(item);
            prepared.Done();
            return result;
        }
    }

}
