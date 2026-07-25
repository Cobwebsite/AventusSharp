using AventusSharp.Data.Manager.DB;
using AventusSharp.Data.Storage.Default;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using BuilderToolsMysql = AventusSharp.Data.Storage.Mysql.Queries.BuilderTools;

namespace AventusSharp.Data.Storage.Mssql.Queries;
public static class BuilderTools
{
    public static string Where(List<IWhereRootGroup>? wheres, IDBStorage storage)
    {
        return Regex.Replace(
            BuilderToolsMysql.Where(wheres, storage),
            @"\b(YEAR|MONTH|DAY|HOUR|MINUTE|SECOND)\(([^()]+)\)",
            match => $"DATEPART({match.Groups[1].Value}, {match.Groups[2].Value})",
            RegexOptions.IgnoreCase);
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
