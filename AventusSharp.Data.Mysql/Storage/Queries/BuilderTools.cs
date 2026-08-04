using AventusSharp.Data.Manager.DB;
using AventusSharp.Data.Storage.Default;
using AventusSharp.Data.Storage.Relational;

namespace AventusSharp.Data.Storage.Mysql.Queries;

public static class BuilderTools
{
    public static string Where(List<IWhereRootGroup>? wheres, IDBStorage storage) =>
        SqlBuilderTools.Where(wheres, storage);

    public static string GetFctName(WhereGroupFctEnum value) =>
        SqlBuilderTools.GetFctName(value);

    public static string GetFctSqlName(WhereGroupFctSqlEnum value) =>
        SqlBuilderTools.GetFctSqlName(value);
}
