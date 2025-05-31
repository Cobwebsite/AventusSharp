using AventusSharp.Data.Manager.DB;
using System.Collections.Generic;
using BuilderToolsMysql = AventusSharp.Data.Storage.Mysql.Queries.BuilderTools;

namespace AventusSharp.Data.Storage.Sqlite.Queries;
public static class BuilderTools
{
    public static string Where(List<IWhereRootGroup>? wheres)
    {
        return BuilderToolsMysql.Where(wheres);
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
