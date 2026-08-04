using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.Common;

namespace AventusSharp.Data.Storage.Default
{
    public class DeleteQueryInfo
    {
        public string Sql { get; set; }
        public List<DbParameter> Parameters { get; set; }
        public Func<IList, List<Dictionary<string, object?>>> GetParams { get; set; }

        public DeleteQueryInfo(string sql, List<DbParameter> parameters, Func<IList, List<Dictionary<string, object?>>> getParams)
        {
            Sql = sql;
            Parameters = parameters;
            GetParams = getParams;
        }
    }
}
