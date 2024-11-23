using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.Common;
using AventusSharp.Data.Storage.Default.TableMember;

namespace AventusSharp.Data.Storage.Default
{
    public enum CreateOrUpdate
    {
        Create,
        Update,
        Both
    }
    public class CreateQueryInfo
    {
        public string Sql { get; set; }
        public List<DbParameter> Parameters { get; set; }
        public Func<IList, List<Dictionary<string, object?>>> GetParams { get; set; }

        public bool IsRoot { get; set; }

        public Dictionary<TableMemberInfo, CreateOrUpdate> ActionsBefore { get; set; }

        public CreateQueryInfo(string sql, List<DbParameter> parameters, Func<IList, List<Dictionary<string, object?>>> getParams, bool isRoot, Dictionary<TableMemberInfo, CreateOrUpdate> actionsBefore)
        {
            Sql = sql;
            Parameters = parameters;
            GetParams = getParams;
            IsRoot = isRoot;
            ActionsBefore = actionsBefore;
        }
    }
}
