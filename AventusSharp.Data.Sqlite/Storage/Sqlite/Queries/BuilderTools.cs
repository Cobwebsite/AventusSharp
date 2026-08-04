using AventusSharp.Data.Manager.DB;
using AventusSharp.Data.Storage.Default;
using System.Collections.Generic;
using CommonBuilderTools = AventusSharp.Data.Storage.Relational.SqlBuilderTools;

namespace AventusSharp.Data.Storage.Sqlite.Queries;
public static class BuilderTools
{
    public static string Where(List<IWhereRootGroup>? wheres, IDBStorage storage)
    {
        return CommonBuilderTools.Where(wheres, storage);
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
