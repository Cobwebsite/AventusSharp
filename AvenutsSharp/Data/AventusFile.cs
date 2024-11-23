using System;
using System.Data;
using System.IO;
using System.Reflection;
using AventusSharp.Data.Manager;
using AventusSharp.Data.Storage.Default;
using AventusSharp.Data.Storage.Default.TableMember;
using AventusSharp.Routes;
using AventusSharp.Routes.Request;
using AventusSharp.Tools;
using AventusSharp.Tools.Attributes;
using MySqlX.XDevAPI.Common;

namespace AventusSharp.Data
{
    internal class FileTableMember : CustomTableMember
    {
        protected DataMemberInfo? dataMemberInfo { get; set; }
        public FileTableMember(MemberInfo? memberInfo, TableInfo tableInfo, bool isNullable) : base(memberInfo, tableInfo, isNullable)
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
            if (result is AventusFile file)
            {
                return file.Uri;
            }
            return null;
        }

        protected override void SetSqlValue(object obj, string? value)
        {
            if (!string.IsNullOrEmpty(value) && dataMemberInfo != null && dataMemberInfo.Type != null)
            {
                object? newFile = Activator.CreateInstance(dataMemberInfo.Type);
                if (newFile is AventusFile file)
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
    public interface IAventusFile
    {

    }

    /// <summary>
    /// Class to handle file during process
    /// </summary>
    [CustomTableMemberType<FileTableMember>]
    public class AventusFile : IAventusFile
    {
        /// <summary>
        /// The current file Uri
        /// </summary>
        public string Uri { get; set; } = "";

        /// <summary>
        /// The file uploaded though a form
        /// </summary>
        public HttpFile? Upload { get; set; }

        public virtual void SetUriFromStorage(string uri) {
            Uri = uri;
        }

        public ResultWithError<bool> SaveToFolderOnUpload(string folderPath)
        {
            if (Upload == null)
            {
                ResultWithError<bool> result = new ResultWithError<bool>();
                result.Result = true;
                return result;
            }

            string savePath = Path.Combine(folderPath, Upload.FileName);
            return SaveToFileOnUpload(savePath);
        }

        public ResultWithError<bool> SaveToFileOnUpload(string filePath)
        {
            ResultWithError<bool> result = new ResultWithError<bool>();

            if (Upload == null)
            {
                result.Result = true;
                return result;
            }

            ResultWithRouteError<bool> resultTemp = Upload.MoveWithError(filePath);
            result.Result = resultTemp.Result;
            result.Errors = resultTemp.ToGeneric().Errors;

            return result;
        }
    }
}