using AventusSharp.Data.Storage.Default;
using AventusSharp.Data.Storage.Default.TableMember;
using AventusSharp.Tools;
using System;
using System.Collections.Generic;

namespace AventusSharp.Data.Manager.DB.Builders
{
    public class DatabaseCreateBuilderInfo
    {
        public List<DatabaseCreateBuilderInfoQuery> Queries { get; set; } = new List<DatabaseCreateBuilderInfoQuery>();

        public List<TableReverseMemberInfo> ReverseMembers { get; set; } = new();

        public List<TableMemberInfoSql> ToCheckBefore { get; set; } = new();
    }

    public class DatabaseCreateBuilderInfoQuery
    {
        public string Sql { get; set; }
        public bool HasPrimaryResult { get; set; }
        public List<ParamsInfo> Parameters { get; }

        public ParamsInfo? PrimaryToSet { get; set; }

        public DatabaseCreateBuilderInfoQuery(string sql, bool havePrimaryResult, List<ParamsInfo> parameters)
        {
            Sql = sql;
            HasPrimaryResult = havePrimaryResult;
            Parameters = parameters;
        }

    }

    public class DatabaseCreateBuilder<T> where T : IStorable
    {
        public IDBStorage Storage { get; private set; }
        public IGenericDM DM { get; private set; }
        public TableInfo TableInfo { get; private set; }

        public DatabaseCreateBuilderInfo? info;

        public ParamsInfo? PrimaryParam { get; set; }

        public DatabaseCreateBuilder(IDBStorage storage, IGenericDM DM, Type? baseType = null)
        {
            this.DM = DM;
            Storage = storage;
            if (baseType == null)
            {
                baseType = typeof(T);
            }
            TableInfo tableInfo = Storage.GetTableInfo(baseType) ?? throw new Exception();
            TableInfo = tableInfo;
        }

        public ResultWithError<T> RunWithError(T item)
        {
            ResultWithError<T> result = new();
            VoidWithError resultTemp = Storage.CreateFromBuilder(this, item);
            if (resultTemp.Success)
            {
                result.Result = item;
            }
            else
            {
                result.Errors.AddRange(resultTemp.Errors);
            }
            DM.PrintErrors(result);
            return result;
        }

        public VoidWithError RunBulkWithError(List<T> items, bool withId)
        {
            VoidWithError result = Storage.BulkCreateFromBuilder(this, items, withId);
            DM.PrintErrors(result);
            return result;
        }

    }
}
