using AventusSharp.Data.Manager.DB;
using AventusSharp.Data.Manager.DB.Builders;
using AventusSharp.Data.Storage.Default;
using AventusSharp.Data.Storage.Default.TableMember;
using System.Collections.Generic;
using System.Linq;

namespace AventusSharp.Data.Storage.Mysql.Queries
{

    internal class BulkCreate
    {
        public static DatabaseCreateBuilderInfo PrepareSQL<X>(DatabaseCreateBuilder<X> createBuilder, int nbItems, bool withId) where X : IStorable
        {
            DatabaseCreateBuilderInfo result = new();

            void createSql(TableInfo tableInfo)
            {
                List<ParamsInfo> paramsInfos = new();
                List<string> columns = new();
                List<string> values = new();
                foreach (TableMemberInfoSql member in tableInfo.Members)
                {
                    if (member.IsAutoIncrement && !withId)
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

                        paramsInfos.Add(paramsInfo);
                        columns.Add("`" + member.SqlName + "`");
                        values.Add("@" + member.SqlName);
                    }
                    else if (member is ITableMemberInfoSqlLinkMultiple)
                    {
                        continue;
                    }

                }

                List<string> allValues = new List<string>();
                for (int i = 0; i < nbItems; i++)
                {
                    allValues.Add("(" + string.Join(",", values.Select(p => p + "__" + i)) + ")");
                }

                string sql = $"INSERT INTO `{tableInfo.SqlTableName}` ({string.Join(",", columns)}) VALUES {string.Join(",", allValues)};";


                DatabaseCreateBuilderInfoQuery resultTemp = new(sql, false, paramsInfos);


                if (tableInfo.Parent != null)
                {
                    createSql(tableInfo.Parent);
                }
                result.Queries.Add(resultTemp);
            }

            createSql(createBuilder.TableInfo);

            return result;
        }

    }
}
