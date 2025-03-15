using System;
using System.Data;
using System.Reflection;
using AventusSharp.Data.Manager;
using AventusSharp.Data.Storage.Default;
using AventusSharp.Data.Storage.Default.TableMember;
using AventusSharp.Routes;
using AventusSharp.Routes.Request;
using AventusSharp.Tools;

namespace AventusSharp.Data.CustomTableMembers
{
    internal class GenericFileTableMember : CustomTableMember
    {
        protected DataMemberInfo? dataMemberInfo { get; set; }
        public GenericFileTableMember(MemberInfo? memberInfo, TableInfo tableInfo, bool isNullable) : base(memberInfo, tableInfo, isNullable)
        {
            if (memberInfo != null)
            {
                dataMemberInfo = new DataMemberInfo(memberInfo);
            }
        }

        public override DbType? GetDbType()
        {
            return DbType.String;
        }

        public override object? GetSqlValue(object obj)
        {
            object? result = GetValue(obj);
            if (result is _GenericFile file)
            {
                ResultWithError<bool> beforeSaveResult = file.BeforeSave(obj);
                if(!beforeSaveResult.Success) {
                    throw beforeSaveResult.Errors[0].GetException();
                }
                return file.Uri;
            }
            return null;
        }

        protected override void SetSqlValue(object obj, string? value)
        {
            if (!string.IsNullOrEmpty(value) && dataMemberInfo != null && dataMemberInfo.Type != null)
            {
                object? newFile = Activator.CreateInstance(dataMemberInfo.Type);
                if (newFile is _GenericFile file)
                {
                    file.SetUriFromStorage(value);
                    SetValue(obj, file);
                }
            }
        }
    }

    /// <summary>
    /// Generic interface for File that you can upload and save into the database
    /// </summary>
    public interface IGenericFile
    {
        string Uri { get; set; }
        HttpFile? Upload { get; set; }
    }

    /// <summary>
    /// Class to handle file during process
    /// </summary>
    [CustomTableMemberType<GenericFileTableMember>]
    public abstract class _GenericFile : IGenericFile
    {
        /// <summary>
        /// The current file Uri
        /// </summary>
        public string Uri { get; set; } = "";

        /// <summary>
        /// The file uploaded though a form
        /// </summary>
        public HttpFile? Upload { get; set; }


        public virtual void SetUriFromStorage(string uri)
        {
            Uri = uri;
        }
        public virtual ResultWithError<bool> BeforeSave(object instance)
        {
            ResultWithError<bool> result = new ResultWithError<bool>();

            if (Upload == null)
            {
                result.Result = true;
                return result;
            }

            ResultWithRouteError<bool> resultTemp = Upload.MoveWithError(DefineFileSave(Upload));
            result.Result = resultTemp.Result;
            result.Errors = resultTemp.ToGeneric().Errors;
            Upload = null;

            return result;
        }

        protected abstract string DefineFileSave(HttpFile file);
    }

    public abstract class GenericFile<T> : _GenericFile where T : IStorable
    {
        public override sealed ResultWithError<bool> BeforeSave(object instance)
        {
            ResultWithError<bool> result = new ResultWithError<bool>();
            if (instance is T tInstance)
            {
                return BeforeSave(tInstance);
            }
            string errorTxt = "The type " + GetType().Name + " is used on " + instance.GetType().Name + " that isn't a child of " + typeof(T).Name;
            result.Errors.Add(new DataError(DataErrorCode.WrongType, errorTxt));
            return result;
        }

        public virtual ResultWithError<bool> BeforeSave(T instance)
        {
            ResultWithError<bool> result = new ResultWithError<bool>();

            if (Upload == null)
            {
                result.Result = true;
                return result;
            }

            ResultWithRouteError<bool> resultTemp = Upload.MoveWithError(DefineFileSave(Upload));
            result.Result = resultTemp.Result;
            result.Errors = resultTemp.ToGeneric().Errors;
            Upload = null;

            return result;
        }

    }
}