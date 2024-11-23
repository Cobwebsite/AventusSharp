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
    public interface IWithError
    {
        [NoExport]
        public List<GenericError> Errors { get; }

        public void Print();
    }

    public interface IWithError<T> : IWithError where T : GenericError
    {
        [NoExport]
        public new List<T> Errors { get; }
    }

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

        public VoidWithError<T> RunAsync(Func<Task<List<T>>> fct)
        {
            if (Success)
            {
                List<T> execResult = fct().GetAwaiter().GetResult();
                if (execResult.Count > 0)
                {
                    Errors.AddRange(execResult);
                }
            }
            return this;
        }
        public VoidWithError<T> RunAsync<Y>(Func<Task<Y>> fct) where Y : IWithError<T>
        {
            if (Success)
            {
                Y execResult = fct().GetAwaiter().GetResult();
                if (execResult.Errors.Count > 0)
                {
                    Errors.AddRange(execResult.Errors);
                }
            }
            return this;
        }

    }

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

        public new VoidWithError RunAsync(Func<Task<List<GenericError>>> fct)
        {
            base.RunAsync(fct);
            return this;
        }
        public new VoidWithError RunAsync<Y>(Func<Task<Y>> fct) where Y : IWithError<GenericError>
        {
            base.RunAsync(fct);
            return this;
        }
    }

    public interface IResultWithError : IWithError
    {
        [NoExport]
        public object? Result { get; }
    }
    public interface IResultWithError<T> : IWithError<T>, IResultWithError where T : GenericError
    {

    }
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

        public new ResultWithError<T, U> RunAsync(Func<Task<List<U>>> fct)
        {
            if (Success)
            {
                List<U> execResult = fct().GetAwaiter().GetResult();
                if (execResult.Count > 0)
                {
                    Errors.AddRange(execResult);
                }
            }
            return this;
        }
        public new ResultWithError<T, U> RunAsync<Y>(Func<Task<Y>> fct) where Y : IWithError<U>
        {
            if (Success)
            {
                Y execResult = fct().GetAwaiter().GetResult();
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

        public X? Execute<X>(Func<ResultWithError<X, U>> fct)
        {
            if (Success)
            {
                ResultWithError<X, U> execResult = fct();
                if (execResult.Errors.Count > 0)
                {
                    Errors.AddRange(execResult.Errors);
                }
                return execResult.Result;
            }
            return default;
        }

        public X? ExecuteAsync<X>(Func<Task<ResultWithError<X, U>>> fct)
        {
            if (Success)
            {
                ResultWithError<X, U> execResult = fct().GetAwaiter().GetResult();
                if (execResult.Errors.Count > 0)
                {
                    Errors.AddRange(execResult.Errors);
                }
                return execResult.Result;
            }
            return default;
        }
    }

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

        public new ResultWithError<T> RunAsync(Func<Task<List<GenericError>>> fct)
        {
            base.RunAsync(fct);
            return this;
        }
        public new ResultWithError<T> RunAsync<Y>(Func<Task<Y>> fct) where Y : IWithError<GenericError>
        {
            base.RunAsync(fct);
            return this;
        }

    }




}
