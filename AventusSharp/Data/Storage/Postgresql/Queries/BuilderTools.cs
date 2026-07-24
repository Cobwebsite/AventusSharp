using AventusSharp.Data.Manager.DB;
using AventusSharp.Data.Storage.Default;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using BuilderToolsMysql = AventusSharp.Data.Storage.Mysql.Queries.BuilderTools;

namespace AventusSharp.Data.Storage.Postgresql.Queries;
public static class BuilderTools
{
    public static string Where(List<IWhereRootGroup>? wheres, IDBStorage storage)
    {
        return QuoteQualifiedColumns(BuilderToolsMysql.Where(wheres, storage));
    }

    public static string QuoteQualifiedColumns(string sql)
    {
        return Regex.Replace(sql, @"\b([A-Za-z_][A-Za-z0-9_]*)\.([A-Za-z_][A-Za-z0-9_]*)\b",
            "$1.\"$2\"");
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
