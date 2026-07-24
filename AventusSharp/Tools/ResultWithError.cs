using AventusSharp.Data;
using AventusSharp.Tools.Attributes;
using AventusSharp.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace AventusSharp.Tools
{
    [NoExport]
    public interface IWithError
    {
        public bool Success { get; }

        public List<GenericError> Errors { get; }

        public void Print();
    }

    [NoExport]
    public interface IWithError<T> : IWithError where T : GenericError
    {
        [NoExport]
        public new List<T> Errors { get; }
    }

    [NoExport]
    public class VoidWithError<T> : IWithError<T> where T : GenericError
    {
        public bool Success { get => Errors.Count == 0; }

        public List<T> Errors { get; set; } = new();

        [NoExport]
        List<GenericError> IWithError.Errors
        {
            get
            {
                List<GenericError> errors = new List<GenericError>();
                foreach (T error in Errors)
                {
                    errors.Add(error);
                }
                return errors;
            }
        }

        public void Print()
        {
            foreach (T error in Errors)
            {
                error.Print();
            }
        }

        /// <summary>
        /// Transform to generic errors
        /// </summary>
        /// <returns></returns>
        public VoidWithError ToGeneric()
        {
            VoidWithError result = new();
            result.Errors = Errors.Select(p => (GenericError)p).ToList();
            return result;
        }

        public VoidWithError<T> Run(Func<List<T>> fct)
        {
            if (Success)
            {
                List<T> execResult = fct();
                if (execResult.Count > 0)
                {
                    Errors.AddRange(execResult);
                }
            }
            return this;
        }
        public VoidWithError<T> Run<Y>(Func<Y> fct) where Y : IWithError<T>
        {
            if (Success)
            {
                Y execResult = fct();
                if (execResult.Errors.Count > 0)
                {
                    Errors.AddRange(execResult.Errors);
                }
            }
            return this;
        }

        public async Task<VoidWithError<T>> RunAsync(Func<Task<List<T>>> fct)
        {
            if (Success)
            {
                List<T> execResult = await fct();
                if (execResult.Count > 0)
                {
                    Errors.AddRange(execResult);
                }
            }
            return this;
        }
        public async Task<VoidWithError<T>> RunAsync<Y>(Func<Task<Y>> fct) where Y : IWithError<T>
        {
            if (Success)
            {
                Y execResult = await fct();
                if (execResult.Errors.Count > 0)
                {
                    Errors.AddRange(execResult.Errors);
                }
            }
            return this;
        }

        public Y? Extract<Y>(Func<ResultWithError<Y, T>> fct)
        {
            if (Success)
            {
                ResultWithError<Y, T> execResult = fct();
                if (execResult.Success && execResult.Result != null)
                {
                    return execResult.Result;
                }
                if (execResult.Errors.Count > 0)
                {
                    Errors.AddRange(execResult.Errors);
                }
            }
            return default;
        }

        public async Task<Y?> ExtractAsync<Y>(Func<Task<ResultWithError<Y, T>>> fct)
        {
            if (Success)
            {
                ResultWithError<Y, T> execResult = await fct();
                if (execResult.Success && execResult.Result != null)
                {
                    return execResult.Result;
                }
                if (execResult.Errors.Count > 0)
                {
                    Errors.AddRange(execResult.Errors);
                }
            }
            return default;
        }
    }

    [NoExport]
    public class VoidWithError : VoidWithError<GenericError>
    {

        public new VoidWithError Run(Func<List<GenericError>> fct)
        {
            base.Run(fct);
            return this;
        }
        public new VoidWithError Run<Y>(Func<Y> fct) where Y : IWithError<GenericError>
        {
            base.Run(fct);
            return this;
        }

        public new async Task<VoidWithError> RunAsync(Func<Task<List<GenericError>>> fct)
        {
            await base.RunAsync(fct);
            return this;
        }
        public new async Task<VoidWithError> RunAsync<Y>(Func<Task<Y>> fct) where Y : IWithError<GenericError>
        {
            await base.RunAsync(fct);
            return this;
        }

        public Task<Y?> ExtractAsync<Y>(Func<Task<ResultWithError<Y>>> fct)
        {

            return base.ExtractAsync<Y>(async () => await fct());
        }

    }

    [NoExport]
    public interface IResultWithError : IWithError
    {
        [NoExport]
        public object? Result { get; }
    }
    [NoExport]
    public interface IResultWithError<T> : IWithError<T>, IResultWithError where T : GenericError
    {

    }

    [NoExport]
    public class ResultWithError<T, U> : VoidWithError<U>, IResultWithError<U> where U : GenericError
    {
        public T? Result { get; set; } = default;
        object? IResultWithError.Result
        {
            get => Result;
        }

        /// <summary>
        /// Transform to generic errors
        /// </summary>
        /// <returns></returns>
        public ResultWithError<X> ToGeneric<X>(Func<T?, X?> transform)
        {
            ResultWithError<X> result = new();
            result.Errors = Errors.Select(p => (GenericError)p).ToList();
            result.Result = transform(Result);
            return result;
        }

        /// <summary>
        /// <inheritdoc/>
        /// </summary>
        /// <returns></returns>
        public new ResultWithError<T> ToGeneric()
        {
            ResultWithError<T> result = new();
            result.Errors = Errors.Select(p => (GenericError)p).ToList();
            result.Result = Result;
            return result;
        }


        public new ResultWithError<T, U> Run(Func<List<U>> fct)
        {
            if (Success)
            {
                List<U> execResult = fct();
                if (execResult.Count > 0)
                {
                    Errors.AddRange(execResult);
                }
            }
            return this;
        }
        public new ResultWithError<T, U> Run<Y>(Func<Y> fct) where Y : IWithError<U>
        {
            if (Success)
            {
                Y execResult = fct();
                if (execResult.Errors.Count > 0)
                {
                    Errors.AddRange(execResult.Errors);
                }
                else if (execResult is IResultWithError saveResult && saveResult.Result is T element)
                {
                    Result = element;
                }
            }
            return this;
        }

        public new async Task<ResultWithError<T, U>> RunAsync(Func<Task<List<U>>> fct)
        {
            if (Success)
            {
                List<U> execResult = await fct();
                if (execResult.Count > 0)
                {
                    Errors.AddRange(execResult);
                }
            }
            return this;
        }
        public new async Task<ResultWithError<T, U>> RunAsync<Y>(Func<Task<Y>> fct) where Y : IWithError<U>
        {
            if (Success)
            {
                Y execResult = await fct();
                if (execResult.Errors.Count > 0)
                {
                    Errors.AddRange(execResult.Errors);
                }
                else if (execResult is IResultWithError saveResult && saveResult.Result is T element)
                {
                    Result = element;
                }
            }
            return this;
        }

    }

    [NoExport]
    public class ResultWithError<T> : ResultWithError<T, GenericError>
    {

        public new ResultWithError<T> Run(Func<List<GenericError>> fct)
        {
            base.Run(fct);
            return this;
        }
        public new ResultWithError<T> Run<Y>(Func<Y> fct) where Y : IWithError<GenericError>
        {
            base.Run(fct);
            return this;
        }

        public new async Task<ResultWithError<T>> RunAsync(Func<Task<List<GenericError>>> fct)
        {
            await base.RunAsync(fct);
            return this;
        }
        public new async Task<ResultWithError<T>> RunAsync<Y>(Func<Task<Y>> fct) where Y : IWithError<GenericError>
        {
            await base.RunAsync(fct);
            return this;
        }

        public Task<Y?> ExtractAsync<Y>(Func<Task<ResultWithError<Y>>> fct)
        {
            return base.ExtractAsync<Y>(async () => await fct());
        }

    }


    public static class WithErrorExtensions
    {
        public static async Task<VoidWithError<T>> RunAsync<T>(this Task<VoidWithError<T>> task, Func<Task<List<T>>> fct) where T : GenericError
        {
            var result = await task;
            return await result.RunAsync(fct);
        }

        public static async Task<VoidWithError<T>> RunAsync<T, Y>(this Task<VoidWithError<T>> task, Func<Task<Y>> fct) where T : GenericError where Y : IWithError<T>
        {
            var result = await task;
            return await result.RunAsync(fct);
        }

        public static async Task<VoidWithError> RunAsync(this Task<VoidWithError> task, Func<Task<List<GenericError>>> fct)
        {
            var result = await task;
            return await result.RunAsync(fct);
        }
        public static async Task<VoidWithError> RunAsync<Y>(this Task<VoidWithError> task, Func<Task<Y>> fct) where Y : IWithError<GenericError>
        {
            var result = await task;
            return await result.RunAsync(fct);
        }
        public static async Task<ResultWithError<T, U>> RunAsync<T, U>(this Task<ResultWithError<T, U>> task, Func<Task<List<U>>> fct) where U : GenericError
        {
            var result = await task;
            return await result.RunAsync(fct);
        }

        public static async Task<ResultWithError<T, U>> RunAsync<T, U, Y>(this Task<ResultWithError<T, U>> task, Func<Task<Y>> fct) where U : GenericError where Y : IWithError<U>
        {
            var result = await task;
            return await result.RunAsync(fct);
        }

        public static async Task<ResultWithError<T>> RunAsync<T>(this Task<ResultWithError<T>> task, Func<Task<List<GenericError>>> fct)
        {
            var result = await task;
            return await result.RunAsync(fct);
        }

        public static async Task<ResultWithError<T>> RunAsync<T, Y>(this Task<ResultWithError<T>> task, Func<Task<Y>> fct) where Y : IWithError<GenericError>
        {
            var result = await task;
            return await result.RunAsync(fct);
        }

        public static async Task<Y?> ExtractAsync<T, Y>(this Task<VoidWithError<T>> task, Func<Task<ResultWithError<Y, T>>> fct) where T : GenericError
        {
            var result = await task;
            return await result.ExtractAsync(fct);
        }
        public static async Task<Y?> ExtractAsync<T, Y>(this Task<VoidWithError> task, Func<Task<ResultWithError<Y>>> fct)
        {
            var result = await task;
            return await result.ExtractAsync(fct);
        }

        public static async Task<Y?> ExtractAsync<T, Y>(this Task<ResultWithError<T>> task, Func<Task<ResultWithError<Y>>> fct)
        {
            var result = await task;
            return await result.ExtractAsync(fct);
        }
    }

}
