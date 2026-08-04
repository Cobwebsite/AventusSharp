using AventusSharp.Data.Manager.DB;
using AventusSharp.Data.Storage.Default;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using CommonBuilderTools = AventusSharp.Data.Storage.Relational.SqlBuilderTools;

namespace AventusSharp.Data.Storage.Mssql.Queries;
public static class BuilderTools
{
    public static string Where(List<IWhereRootGroup>? wheres, IDBStorage storage)
    {
        string sql = Regex.Replace(
            CommonBuilderTools.Where(wheres, storage),
            @"\b(YEAR|MONTH|DAY|HOUR|MINUTE|SECOND)\(([^()]+)\)",
            match => $"DATEPART({match.Groups[1].Value}, {match.Groups[2].Value})",
            RegexOptions.IgnoreCase);
        return Regex.Replace(sql, @"\bCEIL\(", "CEILING(", RegexOptions.IgnoreCase);
    }

    public static string GetFctName(WhereGroupFctEnum fctEnum)
    {
        return CommonBuilderTools.GetFctName(fctEnum);
    }

    public static string GetFctSqlName(WhereGroupFctSqlEnum fctEnum)
    {
        return CommonBuilderTools.GetFctSqlName(fctEnum);
    }

}
