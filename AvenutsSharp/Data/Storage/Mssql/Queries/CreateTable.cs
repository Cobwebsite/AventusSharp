using System.Collections.Generic;
using System.Linq;
using AventusSharp.Data.Storage.Default;
using AventusSharp.Data.Storage.Default.TableMember;
using AventusSharp.Data.Storage.Mysql.Tools;

namespace AventusSharp.Data.Storage.Mssql.Queries;


internal class CreateTable
{
    public static string GetQuery(TableInfo table, MsSqlStorage storage)
    {
        string sql = "CREATE TABLE [" + table.SqlTableName + "] (\r\n";

        List<string> schema = new();
        List<string> primaryConstraint = new();
        List<string> foreignConstraint = new();
        List<string> uniqueConstraint = new();
        string separator = ",\r\n";

        // key is sql_table_name
        Dictionary<string, Dictionary<string, List<TableMemberInfoSql>>> primariesByClass = new();

        foreach (TableMemberInfoSql member in table.Members)
        {
            if (member is ITableMemberInfoSqlWritable memberWritable)
            {
                string typeTxt = storage.GetSqlColumnType(memberWritable.SqlType, member);
                string schemaProp = "\t[" + member.SqlName + "] " + typeTxt;
                if (!member.IsNullable)
                {
                    schemaProp += " NOT NULL";
                }
                if (member.IsAutoIncrement)
                {
                    schemaProp += " IDENTITY(1,1)";
                }
                schema.Add(schemaProp);

                if (member.IsPrimary)
                {
                    primaryConstraint.Add("[" + member.SqlName + "]");
                }

                if (member.IsUnique)
                {
                    string constraintName = "UC_" + member.SqlName + "_" + table.SqlTableName;
                    uniqueConstraint.Add("\tCONSTRAINT [" + constraintName + "] UNIQUE ([" + member.SqlName + "])");
                }
            }

            if (member is ITableMemberInfoSqlLinkSingle memberLink)
            {
                if (memberLink.TableLinked != null)
                {
                    if (!primariesByClass.ContainsKey(memberLink.TableLinked.SqlTableName))
                    {
                        primariesByClass[memberLink.TableLinked.SqlTableName] = new Dictionary<string, List<TableMemberInfoSql>>();
                    }
                    if (!primariesByClass[memberLink.TableLinked.SqlTableName].ContainsKey(member.Name))
                    {
                        primariesByClass[memberLink.TableLinked.SqlTableName][member.Name] = new List<TableMemberInfoSql>();
                    }
                    primariesByClass[memberLink.TableLinked.SqlTableName][member.Name].Add(member);
                }
                else
                {
                    // TODO code external link
                }
            }
        }

        // There is only one constraint by class for foreignkey (if many primaries into foreign class)
        foreach (KeyValuePair<string, Dictionary<string, List<TableMemberInfoSql>>> primary in primariesByClass)
        {
            foreach (KeyValuePair<string, List<TableMemberInfoSql>> pri in primary.Value)
            {
                bool deleteOnCascade = pri.Value.FirstOrDefault(p => p.IsDeleteOnCascade) != null;
                bool deleteSetNull = pri.Value.FirstOrDefault(p => p.IsDeleteSetNull) != null;
                string constraintName = "FK_" + string.Join("_", pri.Value.Select(field => field.SqlName)) + "_" + table.SqlTableName + "_" + primary.Key;
                constraintName = Utils.CheckConstraint(constraintName);
                string foreignKey = string.Join(", ", pri.Value.Select(field => "[" + field.SqlName + "]"));
                string foreignTable = string.Join(", ", pri.Value.Select(field => "[" + ((ITableMemberInfoSqlLink)field).TableLinked?.Primary?.SqlName + "]"));
                string constraintProp = "\t" + "CONSTRAINT [" + constraintName + "] FOREIGN KEY (" + foreignKey + ") REFERENCES [" + primary.Key + "] (" + foreignTable + ")";
                if (deleteOnCascade)
                {
                    // TODO pour les tests mais doit être calculé du côté manager (seulement si stocker dans la RAM?)
                    constraintProp += " ON DELETE CASCADE";
                }
                else if (deleteSetNull)
                {
                    // TODO pour les tests mais doit être calculé du côté manager (seulement si stocker dans la RAM?)
                    constraintProp += " ON DELETE SET NULL";
                }
                foreignConstraint.Add(constraintProp);
            }

        }

        sql += string.Join(separator, schema);
        if (primaryConstraint.Count > 0)
        {
            sql += separator;
            string joinedPrimary = string.Join(",", primaryConstraint);
            string primaryProp = "\tCONSTRAINT [PK_" + table.SqlTableName + "] PRIMARY KEY (" + joinedPrimary + ")";
            sql += primaryProp;
        }
        if (foreignConstraint.Count > 0)
        {
            sql += separator;
            sql += string.Join(separator, foreignConstraint);
        }
        if (uniqueConstraint.Count > 0)
        {
            sql += separator;
            sql += string.Join(separator, uniqueConstraint);
        }
        sql += ")";
        return sql;
    }


}
