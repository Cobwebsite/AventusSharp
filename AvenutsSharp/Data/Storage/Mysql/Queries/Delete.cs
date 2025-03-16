using AventusSharp.Data.Manager.DB;
using AventusSharp.Data.Manager.DB.Builders;
using AventusSharp.Data.Storage.Default;
using AventusSharp.Data.Storage.Default.TableMember;
using System.Collections.Generic;
using System.Data;

namespace AventusSharp.Data.Storage.Mysql.Queries
{

    internal class Delete
    {
        public static void LoadMembers<X>(DatabaseDeleteBuilder<X> deleteBuilder, TableInfo table) where X : IStorable
        {

        }
        public static DatabaseDeleteBuilderInfo PrepareSQL<X>(DatabaseDeleteBuilder<X> deleteBuilder, MySQLStorage storage) where X : IStorable
        {
            DatabaseDeleteBuilderInfo result = new DatabaseDeleteBuilderInfo("");
            DatabaseBuilderInfo mainInfo = deleteBuilder.InfoByPath[""];
            List<string> joins = new();
            List<string> aliases = new();

            void loadMembers(TableInfo table)
            {
                foreach (TableMemberInfoSql memberInfo in table.Members)
                {
                    if (memberInfo is ITableMemberInfoSqlLinkMultiple multiple)
                    {
                        string key = multiple.TableIntermediateKey1 ?? "";
                        string sql = "DELETE FROM `" + multiple.TableIntermediateName + "` WHERE `" + key + "`=@" + key + "";
                        result.DeleteNM.Add(sql, new Dictionary<string, ParamsInfo> {
                            {
                                multiple.TableIntermediateKey1 ?? "", 
                                // the value will be set inside the DefaultDBStorage
                                new ParamsInfo() {
                                    DbType = DbType.Int32,
                                    FctMethodCall = WhereGroupFctEnum.ListContains,
                                    Name = key,
                                }
                            }
                        });
                    }
                    else if(memberInfo is ITableMemberInfoSqlLinkSingle single)
                    {
                        if(memberInfo.IsAutoDelete)
                        {
                            // TODO : code
                            //string sql = "SELECT * FROM product where tag in (1,2)";
                            //sql = "SELECT COUNT(*)";
                            
                        }
                    }
                }
             
                result.ReverseMembers.AddRange(table.ReverseMembers);
            }

            void loadInfo(DatabaseBuilderInfo baseInfo)
            {
                string lastAlias = baseInfo.Alias;
                TableInfo lastTableInfo = baseInfo.TableInfo;
                aliases.Insert(0, lastAlias + ".*");
                loadMembers(lastTableInfo);
                foreach (KeyValuePair<TableInfo, string> parentLink in baseInfo.Parents)
                {
                    string alias = parentLink.Value;
                    TableInfo info = parentLink.Key;
                    loadMembers(info);
                    joins.Add("INNER JOIN `" + info.SqlTableName + "` " + alias + " ON " + lastAlias + "." + lastTableInfo.Primary?.SqlName + "=" + alias + "." + info.Primary?.SqlName);
                    lastAlias = alias;
                    lastTableInfo = info;
                    aliases.Insert(0, lastAlias + ".*");
                }
            }
            loadInfo(mainInfo);

            string whereTxt = BuilderTools.Where(deleteBuilder.Wheres);

            string joinTxt = string.Join(" ", joins);
            if (joinTxt.Length > 1)
            {
                joinTxt = " " + joinTxt;
            }

            result.Sql = "DELETE " + string.Join(",", aliases) + " FROM `" + mainInfo.TableInfo.SqlTableName + "` " + mainInfo.Alias
                + joinTxt
                + whereTxt;


            return result;
        }

    }
}
