﻿using AventusSharp.Data.Manager.DB;
using AventusSharp.Data.Manager.DB.Builders;
using AventusSharp.Data.Storage.Default;
using AventusSharp.Data.Storage.Default.TableMember;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AventusSharp.Data.Storage.Mysql.Queries
{
    public class Query
    {
        public static DatabaseQueryBuilderInfo PrepareSQL<X>(DatabaseQueryBuilder<X> queryBuilder, MySQLStorage storage) where X : IStorable
        {
            DatabaseBuilderInfo mainInfo = queryBuilder.InfoByPath[""];
            List<string> fields = new();
            List<string> joins = new();
            List<string> groupByPart = new List<string>();

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
                    fields.Add(alias + "." + member.SqlName + " `" + alias + "*" + member.SqlName + "`");
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
                        fields.Add(alias + "." + member.SqlName + " `" + alias + "*" + member.SqlName + "`");
                    }
                    joins.Add("INNER JOIN `" + info.SqlTableName + "` " + alias + " ON " + lastAlias + "." + lastTableInfo.Primary?.SqlName + "=" + alias + "." + info.Primary?.SqlName);
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
                            fields.Add(alias + "." + member.SqlName + " `" + alias + "*" + member.SqlName + "`");
                        }
                        joins.Add("LEFT OUTER JOIN `" + child.TableInfo.SqlTableName + "` " + child.Alias + " ON " + parentAlias + "." + parentPrimName + "=" + alias + "." + primName);
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
                        fields.Add("GROUP_CONCAT(" + alias + "." + linkMultiple.TableIntermediateKey2 + ") `" + baseInfo.Alias + "*" + member.Key.SqlName + "`");
                        joins.Add("LEFT OUTER JOIN `" + linkMultiple.TableIntermediateName + "` " + alias + " ON " + alias + "." + linkMultiple.TableIntermediateKey1 + "=" + baseInfo.Alias + "." + baseInfo.TableInfo.Primary?.SqlName);
                        groupByPart.Add(mainInfo.Alias + "." + mainInfo.TableInfo.Primary?.SqlName);
                    }
                    else
                    {
                        string alias = member.Value.Alias;
                        if (member.Value.Transformators != null && member.Value.Transformators.Count > 0)
                        {
                            string open = "";
                            string close = "";

                            foreach (WhereGroupFctSqlEnum transformator in member.Value.Transformators)
                            {
                                open += BuilderTools.GetFctSqlName(transformator) + "(";
                                close += ")";
                            }
                            fields.Add(open + alias + "." + member.Key.SqlName + close + " `" + alias + "*" + member.Key.SqlName + "`");

                        }
                        else
                        {
                            fields.Add(alias + "." + member.Key.SqlName + " `" + alias + "*" + member.Key.SqlName + "`");
                        }
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
                    joins.Add("LEFT OUTER JOIN `" + databaseQueryBuilderInfo.TableInfo.SqlTableName + "` " + databaseQueryBuilderInfo.Alias + " ON " + baseInfo.Alias + "." + tableMemberInfo.SqlName + "=" + databaseQueryBuilderInfo.Alias + "." + databaseQueryBuilderInfo.TableInfo.Primary?.SqlName);
                    path.Add(tableMemberInfo.Name);
                    types.Add(tableMemberInfo.MemberType);
                    loadInfo(databaseQueryBuilderInfo, path, types);
                    path.RemoveAt(path.Count - 1);
                    types.RemoveAt(types.Count - 1);
                }

            }

            loadInfo(mainInfo, new List<string>(), new List<Type>());

            string whereTxt = BuilderTools.Where(queryBuilder.Wheres);

            string joinTxt = string.Join(" ", joins);
            if (joinTxt.Length > 1)
            {
                joinTxt = " " + joinTxt;
            }

            if (queryBuilder.Groups != null)
            {
                foreach (GroupInfo groupInfo in queryBuilder.Groups)
                {
                    groupByPart.Add(groupInfo.Alias + "." + groupInfo.TableMember.SqlName);
                }
            }
            string groupBy = "";
            if (groupByPart.Count > 0)
            {
                groupBy = " GROUP BY " + string.Join(", ", groupByPart);
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
            string orderBy = "";
            if (orderByPart.Count > 0)
            {
                orderBy = " ORDER BY " + string.Join(", ", orderByPart);
            }
            string limitOffset = "";
            if (queryBuilder.LimitSize != null)
            {
                limitOffset = " LIMIT " + queryBuilder.LimitSize;
                if (queryBuilder.OffsetSize != null)
                {
                    limitOffset += " OFFSET " + queryBuilder.OffsetSize;
                }
            }
            else if (queryBuilder.OffsetSize != null)
            {
                limitOffset = " LIMIT 18446744073709551615 OFFSET " + queryBuilder.OffsetSize;
            }

            string sql = "SELECT " + string.Join(",", fields)
                + " FROM `" + mainInfo.TableInfo.SqlTableName + "` " + mainInfo.Alias
                + joinTxt
                + whereTxt
                + groupBy
                + orderBy
                + limitOffset;


            return new DatabaseQueryBuilderInfo(sql);
        }
    }
}
