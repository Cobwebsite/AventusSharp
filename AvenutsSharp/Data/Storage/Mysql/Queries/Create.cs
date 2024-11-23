using AventusSharp.Data.Manager.DB;
using AventusSharp.Data.Manager.DB.Builders;
using AventusSharp.Data.Storage.Default;
using AventusSharp.Data.Storage.Default.TableMember;
using AventusSharp.Data.Storage.Mysql.Tools;
using System.Collections.Generic;
using System.Reflection;

namespace AventusSharp.Data.Storage.Mysql.Queries
{

    internal class Create
    {
        public static DatabaseCreateBuilderInfo PrepareSQL<X>(DatabaseCreateBuilder<X> createBuilder) where X : IStorable
        {
            DatabaseCreateBuilderInfo result = new();

            void createSql(TableInfo tableInfo)
            {
                List<ParamsInfo> paramsInfos = new();
                List<string> columns = new();
                List<string> values = new();
                bool hasPrimaryResult = tableInfo.Parent == null;
                ParamsInfo? primaryToSet = null;
                List<DatabaseCreateBuilderInfoQuery> createAfter = new List<DatabaseCreateBuilderInfoQuery>();
                foreach (TableMemberInfoSql member in tableInfo.Members)
                {
                    if (member.IsAutoIncrement)
                    {
                        continue;
                    }

                    if (member is ITableMemberInfoSqlLink)
                    {
                        if (member.IsAutoCreate || member.IsAutoUpdate)
                        {
                            result.ToCheckBefore.Add(member);
                        }
                    }


                    if (member is ITableMemberInfoSqlWritable memberBasic)
                    {
                        ParamsInfo paramsInfo = new()
                        {
                            DbType = memberBasic.SqlType,
                            Name = member.SqlName,
                            TypeLvl0 = tableInfo.Type,
                            MembersList = new List<TableMemberInfoSql>() { member }
                        };
                        if (!hasPrimaryResult && member.IsPrimary)
                        {
                            primaryToSet = paramsInfo;
                        }
                        else
                        {
                            paramsInfos.Add(paramsInfo);
                        }
                        columns.Add("`" + member.SqlName + "`");
                        values.Add("@" + member.SqlName);
                    }
                    else if (member is ITableMemberInfoSqlLinkMultiple memberNM)
                    {
                        string? intermediateTableName = memberNM.TableIntermediateName;
                        if (!(member.TableInfo.Primary is ITableMemberInfoSqlWritable primary))
                        {
                            continue;
                        }

                        string linkInsert = $"INSERT INTO `{intermediateTableName}` (`{memberNM.TableIntermediateKey1}`, `{memberNM.TableIntermediateKey2}`) VALUES (@{memberNM.TableIntermediateKey1}, @{memberNM.TableIntermediateKey2});";
                        List<ParamsInfo> linkInfo = new List<ParamsInfo>()
                        {
                            new ParamsInfo()
                            {
                                DbType = primary.SqlType,
                                Name = memberNM.TableIntermediateKey1 ?? "",
                                TypeLvl0 = tableInfo.Type,
                                MembersList = new List<TableMemberInfoSql>() { member.TableInfo.Primary }
                            },
                            new ParamsInfo()
                            {
                                DbType = memberNM.LinkFieldType,
                                Name = memberNM.TableIntermediateKey2 ?? "",
                                TypeLvl0 = tableInfo.Type,
                                MembersList = new List<TableMemberInfoSql>() { member }
                            }
                        };
                        DatabaseCreateBuilderInfoQuery resultLink = new(linkInsert, false, linkInfo);
                        createAfter.Add(resultLink);
                    }

                }

                string sql = $"INSERT INTO `{tableInfo.SqlTableName}` ({string.Join(",", columns)}) VALUES ({string.Join(",", values)});";

                if (hasPrimaryResult)
                {
                    sql += "SELECT last_insert_id() as Id;";
                }
                DatabaseCreateBuilderInfoQuery resultTemp = new(sql, hasPrimaryResult, paramsInfos)
                {
                    PrimaryToSet = primaryToSet
                };
                foreach (TableReverseMemberInfo memberInfo in tableInfo.ReverseMembers)
                {
                    if (memberInfo.IsAutoCreate || memberInfo.IsAutoUpdate)
                    {
                        result.ReverseMembers.Add(memberInfo);
                    }
                }
                if (hasPrimaryResult && tableInfo.Primary is ITableMemberInfoSqlWritable primaryWritable)
                {
                    createBuilder.PrimaryParam = new ParamsInfo()
                    {
                        DbType = primaryWritable.SqlType,
                        Name = tableInfo.Primary.SqlName,
                        TypeLvl0 = tableInfo.Type,
                        MembersList = new List<TableMemberInfoSql>() { tableInfo.Primary }
                    };
                }
                if (tableInfo.Parent != null)
                {
                    createSql(tableInfo.Parent);
                }
                result.Queries.Add(resultTemp);
                result.Queries.AddRange(createAfter);
            };

            createSql(createBuilder.TableInfo);

            return result;
        }

    }
}
