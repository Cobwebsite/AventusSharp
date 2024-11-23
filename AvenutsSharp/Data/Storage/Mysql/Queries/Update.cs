using AventusSharp.Data.Manager;
using AventusSharp.Data.Manager.DB;
using AventusSharp.Data.Manager.DB.Builders;
using AventusSharp.Data.Storage.Default;
using AventusSharp.Data.Storage.Default.TableMember;
using AventusSharp.Tools;
using Microsoft.AspNetCore.Http.Extensions;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Xml.Linq;

namespace AventusSharp.Data.Storage.Mysql.Queries
{
    public class Update
    {
        public static DatabaseUpdateBuilderInfo PrepareSQL<X>(DatabaseUpdateBuilder<X> updateBuilder, MySQLStorage storage) where X : IStorable
        {
            DatabaseUpdateBuilderInfo result = new DatabaseUpdateBuilderInfo("");
            DatabaseBuilderInfo mainInfo = updateBuilder.InfoByPath[""];
            List<string> fields = new();
            List<string> joins = new();
            List<ParamsInfo> paramsInfosGrab = new();
            List<DatabaseUpdateBuilderInfoQuery> updateBefore = new List<DatabaseUpdateBuilderInfoQuery>();
            List<DatabaseUpdateBuilderInfoQuery> updateAfter = new List<DatabaseUpdateBuilderInfoQuery>();

            void loadInfo(DatabaseBuilderInfo baseInfo, List<TableMemberInfoSql> membersList)
            {
                if (updateBuilder.AllFieldsUpdate)
                {
                    storage.LoadAllTableFieldsUpdate<X>(baseInfo.TableInfo, baseInfo.Alias, baseInfo);
                }

                string lastAlias = baseInfo.Alias;
                TableInfo lastTableInfo = baseInfo.TableInfo;
                

                foreach (KeyValuePair<TableMemberInfoSql, DatabaseBuilderInfoMember> member in baseInfo.Members)
                {
                    if (member.Key is ITableMemberInfoSqlLink)
                    {
                        if (member.Key.IsAutoCreate || member.Key.IsAutoUpdate || member.Key.IsAutoDelete)
                        {
                            result.ToCheckBefore.Add(member.Key);
                        }
                    }
                    if (member.Key is ITableMemberInfoSqlWritable writable)
                    {
                        string alias = member.Value.Alias;
                        string name = alias + "." + member.Key.SqlName;
                        List<TableMemberInfoSql> membersListTemp = new List<TableMemberInfoSql>();
                        membersListTemp.AddRange(membersList);
                        membersListTemp.Add(member.Key);
                        paramsInfosGrab.Add(new ParamsInfo()
                        {
                            DbType = writable.SqlType,
                            Name = name,
                            TypeLvl0 = baseInfo.TableInfo.Type,
                            MembersList = membersListTemp
                        });
                        fields.Add(name + " = @" + name);
                    }
                    else if (member.Key is ITableMemberInfoSqlLinkMultiple memberNM)
                    {
                        string? intermediateTableName = memberNM.TableIntermediateName;
                        if (!(member.Key.TableInfo.Primary is ITableMemberInfoSqlWritable primary))
                        {
                            continue;
                        }

                        string sqlDeleteNM = "DELETE FROM `" + memberNM.TableIntermediateName + "` WHERE `" + memberNM.TableIntermediateKey1 + "`=@" + memberNM.TableIntermediateKey1 + "";
                        DatabaseUpdateBuilderInfoQuery deleteQuery = new DatabaseUpdateBuilderInfoQuery(sqlDeleteNM, new List<ParamsInfo>(), new List<ParamsInfo>() {
                            new ParamsInfo()
                            {
                                DbType = DbType.Int32,
                                FctMethodCall = WhereGroupFctEnum.ListContains,
                                Name = memberNM.TableIntermediateKey1 ?? "",
                                MembersList = new List<TableMemberInfoSql>() { member.Key.TableInfo.Primary }
                            }
                        });
                        updateBefore.Add(deleteQuery);

                        string linkInsert = $"INSERT INTO `{intermediateTableName}` (`{memberNM.TableIntermediateKey1}`, `{memberNM.TableIntermediateKey2}`) VALUES (@{memberNM.TableIntermediateKey1}, @{memberNM.TableIntermediateKey2});";
                        List<ParamsInfo> linkInfo = new List<ParamsInfo>()
                        {
                            new ParamsInfo()
                            {
                                DbType = primary.SqlType,
                                Name = memberNM.TableIntermediateKey1 ?? "",
                                TypeLvl0 = lastTableInfo.Type,
                                MembersList = new List<TableMemberInfoSql>() { member.Key.TableInfo.Primary }
                            },
                            new ParamsInfo()
                            {
                                DbType = memberNM.LinkFieldType,
                                Name = memberNM.TableIntermediateKey2 ?? "",
                                TypeLvl0 = lastTableInfo.Type,
                                MembersList = new List<TableMemberInfoSql>() { member.Key }
                            }
                        };
                        DatabaseUpdateBuilderInfoQuery resultLink = new(linkInsert, updateBuilder.WhereParamsInfo.Values.ToList(), linkInfo);
                        updateAfter.Add(resultLink);
                    }

                }
                foreach (KeyValuePair<TableInfo, string> parentLink in baseInfo.Parents)
                {
                    string alias = parentLink.Value;
                    TableInfo info = parentLink.Key;
                    if (updateBuilder.AllFieldsUpdate)
                    {
                        LoadTableFieldUpdate(info, alias, baseInfo, paramsInfosGrab, fields);
                    }
                    joins.Add("INNER JOIN `" + info.SqlTableName + "` " + alias + " ON " + lastAlias + "." + lastTableInfo.Primary?.SqlName + "=" + alias + "." + info.Primary?.SqlName);
                    lastAlias = alias;
                    lastTableInfo = info;
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
                    membersList.Add(tableMemberInfo);
                    loadInfo(databaseQueryBuilderInfo, membersList);
                    membersList.Remove(tableMemberInfo);

                }

                result.ReverseMembers.AddRange(baseInfo.ReverseLinks);
               
            }

            loadInfo(mainInfo, new List<TableMemberInfoSql>());


            string whereTxt = BuilderTools.Where(updateBuilder.Wheres);

            string joinTxt = string.Join(" ", joins);
            if (joinTxt.Length > 1)
            {
                joinTxt = " " + joinTxt;
            }

            string sql = "UPDATE `" + mainInfo.TableInfo.SqlTableName + "` " + mainInfo.Alias
                + joinTxt
                + " SET " + string.Join(",", fields)
                + whereTxt;

            DatabaseUpdateBuilderInfoQuery resultTemp = new(sql, updateBuilder.WhereParamsInfo.Values.ToList(), paramsInfosGrab);


            KeyValuePair<TableMemberInfoSql?, string> pair = updateBuilder.InfoByPath[""].GetTableMemberInfoAndAlias(TypeTools.GetMemberName((IStorable i) => i.Id));
            if (pair.Key == null)
            {
                throw new Exception("Can't find Id... 0_o");
            }
            string idField = pair.Value + "." + pair.Key.SqlName;
            result.QuerySql = "SELECT " + idField + " FROM `" + mainInfo.TableInfo.SqlTableName + "` " + mainInfo.Alias + joinTxt + whereTxt;


            result.Queries.AddRange(updateBefore);
            result.Queries.Add(resultTemp);
            result.Queries.AddRange(updateAfter);
            updateBuilder.UpdateParamsInfo = paramsInfosGrab.ToDictionary(p => p.Name, p => p);

            return result;
        }

        private static void LoadTableFieldUpdate(TableInfo tableInfo, string alias, DatabaseBuilderInfo baseInfo, List<ParamsInfo> updateParamsInfo, List<string> fields)
        {
            foreach (TableMemberInfoSql member in tableInfo.Members)
            {
                if (!member.IsUpdatable)
                {
                    continue;
                }

                if (member is ITableMemberInfoSqlWritable memberLink)
                {
                    string name = alias + "." + member.SqlName;
                    updateParamsInfo.Add(new ParamsInfo()
                    {
                        DbType = memberLink.SqlType,
                        Name = name,
                        TypeLvl0 = tableInfo.Type,
                        MembersList = new List<TableMemberInfoSql>() { member }
                    });
                    fields.Add(name + " = @" + name);
                }
            }

            foreach (TableReverseMemberInfo member in tableInfo.ReverseMembers)
            {
                if (member.IsAutoCreate || member.IsAutoUpdate || member.IsAutoDelete)
                {
                    baseInfo.ReverseLinks.Add(member);
                }
            }
        }

    }
}
