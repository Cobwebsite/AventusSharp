using System;
using System.Collections.Generic;
using AventusSharp.Data.Manager.DB;
using AventusSharp.Data.Manager.DB.Builders;
using AventusSharp.Data.Storage.Default.TableMember;

namespace AventusSharp.Data.Storage.Postgresql.Queries;

public class Exist
{
    public static DatabaseExistBuilderInfo PrepareSQL<X>(DatabaseExistBuilder<X> queryBuilder, PostgreSqlStorage storage) where X : IStorable
    {
        DatabaseBuilderInfo mainInfo = queryBuilder.InfoByPath[""];
        List<string> fields = new() { "COUNT(*) as nb" };
        List<string> joins = new();

        void loadInfo(DatabaseBuilderInfo baseInfo, List<string> path, List<Type> types)
        {
            foreach (KeyValuePair<TableMemberInfoSql, DatabaseBuilderInfo> linkInfo in baseInfo.joins)
            {
                TableMemberInfoSql tableMemberInfo = linkInfo.Key;
                DatabaseBuilderInfo databaseQueryBuilderInfo = linkInfo.Value;
                if (tableMemberInfo.MemberType == null)
                {
                    continue;
                }
                joins.Add("LEFT OUTER JOIN \"" + databaseQueryBuilderInfo.TableInfo.SqlTableName + "\" " + databaseQueryBuilderInfo.Alias + " ON " + baseInfo.Alias + "." + tableMemberInfo.SqlName + "=" + databaseQueryBuilderInfo.Alias + "." + databaseQueryBuilderInfo.TableInfo.Primary?.SqlName);
                path.Add(tableMemberInfo.Name);
                types.Add(tableMemberInfo.MemberType);
                loadInfo(databaseQueryBuilderInfo, path, types);
                path.RemoveAt(path.Count - 1);
                types.RemoveAt(types.Count - 1);
            }
        }

        loadInfo(mainInfo, new List<string>(), new List<Type>());

        string whereTxt = BuilderTools.Where(queryBuilder.Wheres, storage);

        string joinTxt = string.Join(" ", joins);
        if (joinTxt.Length > 1)
        {
            joinTxt = " " + joinTxt;
        }

        string sql = "SELECT " + string.Join(",", fields)
            + " FROM \"" + mainInfo.TableInfo.SqlTableName + "\" " + mainInfo.Alias
            + joinTxt
            + whereTxt;


        return new DatabaseExistBuilderInfo(sql);
    }
}
