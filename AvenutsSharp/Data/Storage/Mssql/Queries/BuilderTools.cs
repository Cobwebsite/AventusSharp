using AventusSharp.Data.Manager.DB;
using AventusSharp.Data.Storage.Default;
using Microsoft.AspNetCore.Http.Extensions;
using System.Collections.Generic;
using BuilderToolsMysql = AventusSharp.Data.Storage.Mysql.Queries.BuilderTools;

namespace AventusSharp.Data.Storage.Mssql.Queries;
public static class BuilderTools
{
    public static string Where(List<IWhereRootGroup>? wheres, IDBStorage storage)
    {
        return BuilderToolsMysql.Where(wheres, storage);
    }

    public static string GetFctName(WhereGroupFctEnum fctEnum)
    {
        return BuilderToolsMysql.GetFctName(fctEnum);
    }

    public static string GetFctSqlName(WhereGroupFctSqlEnum fctEnum)
    {
        return BuilderToolsMysql.GetFctSqlName(fctEnum);
    }

}
