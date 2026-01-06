using System;
using System.Collections.Generic;
using AventusSharp.Data.Manager.DB;
using AventusSharp.Data.Manager.DB.Builders;
using AventusSharp.Data.Storage.Default;
using AventusSharp.Data.Storage.Default.TableMember;

namespace AventusSharp.Data.Storage.Mssql.Queries;


public class Query
{
    public static DatabaseQueryBuilderInfo PrepareSQL<X>(DatabaseQueryBuilder<X> queryBuilder, MsSqlStorage storage) where X : IStorable
    {
        DatabaseBuilderInfo mainInfo = queryBuilder.InfoByPath[""];
        List<string> fields = new();
        List<string> joins = new();
        string groupBy = "";

        void loadInfo(DatabaseBuilderInfo baseInfo, List<string> path, List<Type> types)
        {
            bool loadMembers = queryBuilder.MustLoadMembers(path);
            if (loadMembers)
            {
                storage.LoadAllTableFieldsQuery(baseInfo.TableInfo, baseInfo.Alias, baseInfo, path, types, queryBuilder);
            }
            // Add type name for abstract class
            else if (baseInfo.TableInfo.TypeMember != null)
            {
                TableMemberInfoSql member = baseInfo.TableInfo.TypeMember;
                string alias = baseInfo.Alias;
                fields.Add(alias + "." + member.SqlName + " [" + alias + "*" + member.SqlName + "]");
            }
            string lastAlias = baseInfo.Alias;
            TableInfo lastTableInfo = baseInfo.TableInfo;
            foreach (KeyValuePair<TableInfo, string> parentLink in baseInfo.Parents)
            {
                string alias = parentLink.Value;
                TableInfo info = parentLink.Key;
                if (loadMembers)
                {
                    storage.LoadAllTableFieldsQuery(info, alias, baseInfo, path, types, queryBuilder);
                }
                else if (parentLink.Key.TypeMember != null)
                {
                    TableMemberInfoSql member = parentLink.Key.TypeMember;
                    fields.Add(alias + "." + member.SqlName + " [" + alias + "*" + member.SqlName + "]");
                }
                joins.Add("INNER JOIN [" + info.SqlTableName + "] " + alias + " ON " + lastAlias + "." + lastTableInfo.Primary?.SqlName + "=" + alias + "." + info.Primary?.SqlName);
                lastAlias = alias;
                lastTableInfo = info;
            }

            Action<List<DatabaseBuilderInfoChild>, string, string?> loadChild = (children, parentAlias, parentPrimName) => { };
            loadChild = (children, parentAlias, parentPrimName) =>
            {
                foreach (DatabaseBuilderInfoChild child in children)
                {
                    string alias = child.Alias;
                    string primName = child.TableInfo.Primary?.SqlName ?? "";
                    if (loadMembers)
                    {
                        storage.LoadAllTableFieldsQuery(child.TableInfo, alias, baseInfo, path, types, queryBuilder);
                    }
                    else if (child.TableInfo.TypeMember != null)
                    {
                        TableMemberInfoSql member = child.TableInfo.TypeMember;
                        fields.Add(alias + "." + member.SqlName + " [" + alias + "*" + member.SqlName + "]");
                    }
                    joins.Add("LEFT OUTER JOIN [" + child.TableInfo.SqlTableName + "] " + child.Alias + " ON " + parentAlias + "." + parentPrimName + "=" + alias + "." + primName);
                    loadChild(child.Children, alias, primName);
                }
            };
            loadChild(baseInfo.Children, baseInfo.Alias, baseInfo.TableInfo.Primary?.SqlName);

            foreach (KeyValuePair<TableMemberInfoSql, DatabaseBuilderInfoMember> member in baseInfo.Members)
            {
                if (member.Key is ITableMemberInfoSqlLinkMultiple linkMultiple)
                {
                    if (linkMultiple.TableLinked == null) { continue; }

                    string alias = "";
                    if (baseInfo.joinsNM.ContainsKey(linkMultiple))
                    {
                        alias = baseInfo.joinsNM[linkMultiple];
                    }
                    else
                    {
                        alias = queryBuilder.CreateAlias(baseInfo.TableInfo, linkMultiple.TableLinked);
                    }
                    fields.Add("STRING_AGG(" + alias + "." + linkMultiple.TableIntermediateKey2 + ") [" + baseInfo.Alias + "*" + member.Key.SqlName + "]");
                    joins.Add("LEFT OUTER JOIN [" + linkMultiple.TableIntermediateName + "] " + alias + " ON " + alias + "." + linkMultiple.TableIntermediateKey1 + "=" + baseInfo.Alias + "." + baseInfo.TableInfo.Primary?.SqlName);
                    if (groupBy == "")
                    {
                        groupBy = " GROUP BY " + mainInfo.Alias + "." + mainInfo.TableInfo.Primary?.SqlName;
                    }
                }
                else
                {
                    string alias = member.Value.Alias;
                    fields.Add(alias + "." + member.Key.SqlName + " [" + alias + "*" + member.Key.SqlName + "]");
                }

            }

            foreach (KeyValuePair<TableMemberInfoSql, DatabaseBuilderInfo> linkInfo in baseInfo.joins)
            {
                TableMemberInfoSql tableMemberInfo = linkInfo.Key;
                DatabaseBuilderInfo databaseQueryBuilderInfo = linkInfo.Value;
                if (tableMemberInfo.MemberType == null)
                {
                    continue;
                }
                joins.Add("LEFT OUTER JOIN [" + databaseQueryBuilderInfo.TableInfo.SqlTableName + "] " + databaseQueryBuilderInfo.Alias + " ON " + baseInfo.Alias + "." + tableMemberInfo.SqlName + "=" + databaseQueryBuilderInfo.Alias + "." + databaseQueryBuilderInfo.TableInfo.Primary?.SqlName);
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

        List<string> orderByPart = new List<string>();
        if (queryBuilder.Sorting != null)
        {
            foreach (SortInfo sortInfo in queryBuilder.Sorting)
            {
                string order = sortInfo.Sort == Sort.ASC ? "ASC" : "DESC";
                orderByPart.Add(sortInfo.Alias + "." + sortInfo.TableMember.SqlName + " " + order);
            }
        }
        string limitOffset = "";
        if (queryBuilder.LimitSize != null)
        {
            int offset = queryBuilder.OffsetSize ?? 0;
            limitOffset = $" OFFSET {offset} ROWS FETCH NEXT {queryBuilder.LimitSize} ROWS ONLY";
            if (orderByPart.Count == 0)
            {
                DatabaseBuilderInfo baseInfo = queryBuilder.InfoByPath[""];
                TableMemberInfoSql? primary = baseInfo.TableInfo.Primary;
                if (primary != null)
                {
                    orderByPart.Add(baseInfo.Alias + "." + primary.SqlName);
                }
            }
        }
        string orderBy = "";
        if (orderByPart.Count > 0)
        {
            orderBy = " ORDER BY " + string.Join(", ", orderByPart);
        }

        string sql = "SELECT " + string.Join(",", fields)
            + " FROM [" + mainInfo.TableInfo.SqlTableName + "] " + mainInfo.Alias
            + joinTxt
            + whereTxt
            + groupBy
            + orderBy
            + limitOffset;


        return new DatabaseQueryBuilderInfo(sql);
    }
}
