using AventusSharp.Data.Storage.Default;
using AventusSharp.Data.Storage.Default.TableMember;
using AventusSharp.Data.Storage.Mysql.Tools;
using System.Collections.Generic;

namespace AventusSharp.Data.Storage.Mysql.Queries
{
    internal class CreateIntermediateTable
    {
        public static string GetQuery(TableMemberInfoSql memberInfo, MySQLStorage storage)
        {
            if(!(memberInfo is ITableMemberInfoSqlLinkMultiple memberMultiple))
            {
                return "";
            }

            string? intermediateTableName = memberMultiple.TableIntermediateName;
            if (intermediateTableName == null)
            {
                return "";
            }

            TableInfo instance = memberInfo.TableInfo;
            TableInfo? link = memberMultiple.TableLinked;
            // si on a pas de lien, il faut rajouter la table intermédiaire sans la contrainte
            bool addLinkConstraint = link != null;
            string linkFieldType = storage.GetSqlColumnType(memberMultiple.LinkFieldType, link?.Primary);
            string linkTableName = memberMultiple.LinkTableName;
            string linkPrimaryName = memberMultiple.LinkPrimaryName;

            
            if (!(instance.Primary is ITableMemberInfoSqlWritable primary))
            {
                return "";
            }

            List<string> schema = new();
            List<string> primaryConstraint = new();
            List<string> foreignConstraint = new();
            string separator = ",\r\n";

            string intermediateName = "`" + memberMultiple.TableIntermediateKey1 + "`";
            string schemaProp = "\t" + intermediateName + " " + storage.GetSqlColumnType(primary.SqlType, instance.Primary);
            string constraintName = "`FK_" + intermediateTableName + "_" + instance.SqlTableName+"`";

            primaryConstraint.Add(intermediateName);
            schema.Add(schemaProp);
            
            constraintName = Utils.CheckConstraint(constraintName);
            string constraintProp = "\t" + "CONSTRAINT " + constraintName + " FOREIGN KEY (" + intermediateName + ") REFERENCES `" + instance.SqlTableName + "` (" + instance.Primary.SqlName + ")";
            foreignConstraint.Add(constraintProp);



            intermediateName = "`" + memberMultiple.TableIntermediateKey2 + "`";
            schemaProp = "\t" + intermediateName + " " + linkFieldType;

            primaryConstraint.Add(intermediateName);
            schema.Add(schemaProp);

            if (addLinkConstraint)
            {
                constraintName = "`FK_" + intermediateTableName + "_" + linkTableName + "`";
                constraintName = Utils.CheckConstraint(constraintName);
                constraintProp = "\t" + "CONSTRAINT " + constraintName + " FOREIGN KEY (" + intermediateName + ") REFERENCES `" + linkTableName + "` (" + linkPrimaryName + ")";
                foreignConstraint.Add(constraintProp);
            }
            


            string sql = "CREATE TABLE `" + intermediateTableName + "` (\r\n";
            sql += string.Join(separator, schema);
            if (primaryConstraint.Count > 0)
            {
                sql += separator;
                string joinedPrimary = string.Join(",", primaryConstraint);
                string primaryProp = "\tCONSTRAINT `PK_" + intermediateTableName + "` PRIMARY KEY (" + joinedPrimary + ")";
                sql += primaryProp;
            }
            if (foreignConstraint.Count > 0)
            {
                sql += separator;
                sql += string.Join(separator, foreignConstraint);
            }
            sql += ")";

            return sql;
        }
    }
}
