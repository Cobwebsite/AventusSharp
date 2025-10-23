using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;
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
        public ExistBuilderPrepared<T> WhereWithParameters(Expression<Func<T, bool>> func);

        internal void PrepareInternal(params object[] objects);
        internal void SetVariableInternal(string name, object value);
    }

    public class ExistBuilderPrepared<T>
    {
        private Mutex mutex;
        private IExistBuilder<T> builder;
        public ExistBuilderPrepared(IExistBuilder<T> builder)
        {
            this.builder = builder;
            mutex = new();
        }

        /// <summary>
        /// Start a new Exist where you can call Prepare or SetVariables
        /// </summary>
        /// <returns></returns>
        public ExistBuilderPreparedInstance<T> New()
        {
            mutex.WaitOne();
            return new ExistBuilderPreparedInstance<T>(builder, this);
        }

        internal void Done()
        {
            mutex.ReleaseMutex();
        }
    }
    public class ExistBuilderPreparedInstance<T>
    {
        private IExistBuilder<T> builder;
        private ExistBuilderPrepared<T> prepared;
        public ExistBuilderPreparedInstance(IExistBuilder<T> builder, ExistBuilderPrepared<T> prepared)
        {
            this.builder = builder;
            this.prepared = prepared;
        }


        /// <summary>
        /// Prepares the Exist by adding parameters or additional objects.
        /// </summary>
        /// <param name="objects">Objects to be used in preparing the Exist.</param>
        /// <returns>The current Exist builder instance for method chaining.</returns>
        public ExistBuilderPreparedInstance<T> Prepare(params object[] objects)
        {
            builder.PrepareInternal(objects);
            return this;
        }
        /// <summary>
        /// Sets all variables for the Exist.
        /// </summary>
        /// <param name="define">The fct to define the variable.</param>
        /// <returns>The current Exist builder instance for method chaining.</returns>
        public ExistBuilderPreparedInstance<T> SetVariables(Action<Action<string, object>> define)
        {
            define(builder.SetVariableInternal);
            return this;
        }
        /// <summary>
        /// Executes the Exist and returns a list of results.
        /// </summary>
        /// <returns>A list of type <typeparamref name="T"/>.</returns>
        public bool Run()
        {
            bool result = builder.Run();
            prepared.Done();
            return result;
        }
        /// <summary>
        /// Executes the Exist and returns a result with error handling.
        /// </summary>
        /// <returns>A ResultWithError containing a list of <typeparamref name="T"/>.</returns>
        public ResultWithError<bool> RunWithError()
        {
            ResultWithError<bool> result = builder.RunWithError();
            prepared.Done();
            return result;
        }
    }
}
