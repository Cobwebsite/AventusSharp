using AventusSharp.Tools;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace AventusSharp.Data.Manager
{
    /// <summary>
    /// Interface for building and executing create queries for a specific type.
    /// </summary>
    /// <typeparam name="T">The type of entity the update builder will work with.</typeparam>
    public interface ICreateBuilder<T>
    {
        /// <summary>
        /// Executes the create operation and returns the items.
        /// </summary>
        /// <param name="item">The item to be updated.</param>
        /// <returns>the <typeparamref name="T"/> object, or null if no items were created.</returns>
        public Task<T?> Run(T item);

        /// <summary>
        /// Executes the create operation and returns a result with error handling.
        /// </summary>
        /// <param name="item">The item to be created.</param>
        /// <returns>A ResultWithError containing the created <typeparamref name="T"/> object.</returns>
        public Task<ResultWithError<T>> RunWithError(T item);

        /// <summary>
        /// Executes a bulk insert 
        /// </summary>
        /// <param name="items">The items to be created.</param>
        /// <param name="withId">Set to true if you need insert Id</param>
        /// <returns></returns>
        public Task<VoidWithError> RunBulkWithError(List<T> items, bool withId);

    }

}
