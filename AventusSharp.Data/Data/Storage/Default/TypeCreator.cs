using System;
using System.Collections.Generic;
using System.Linq;
using AventusSharp.Tools;
using AventusSharp.Data.Storage.Default.TableMember;

namespace AventusSharp.Data.Storage.Default;

public static class TypeCreator
{
    private static Dictionary<Type, List<TableInfo>> Tables { get; set; } = new();

    public static ResultWithError<X> CreateObj<X>(Dictionary<string, string?> line)
    {
        ResultWithError<X> result = new();
        Type type = typeof(X);
        if (!Tables.ContainsKey(type))
        {
            List<TableInfo> tables = new();
            Type? currentType = type;
            while (currentType != null && currentType != typeof(object))
            {
                TableInfo table = new(currentType);
                result.Run(() => table.Init().ToGeneric());
                if (!result.Success)
                {
                    return result;
                }
                tables.Add(table);
                currentType = currentType.BaseType;
            }
            Tables[type] = tables;
        }

        CreateObj(line, Tables[type], result);
        return result;
    }

    private static void CreateObj<X>(
        Dictionary<string, string?> line,
        List<TableInfo> tableInfos,
        ResultWithError<X> result)
    {
        object instance = TypeTools.CreateNewObj(typeof(X));

        try
        {
            foreach (TableInfo tableInfo in tableInfos)
            {
                foreach (TableMemberInfoSql member in tableInfo.Members)
                {
                    string key = line.Keys.FirstOrDefault(column =>
                        string.Equals(column, member.SqlName,
                            StringComparison.OrdinalIgnoreCase)) ?? member.SqlName;

                    if (member is TableMemberInfoSqlBasic ||
                        member is TableMemberInfoSql1NInt ||
                        member is CustomTableMember)
                    {
                        member.ApplySqlValue(instance, line[key]);
                    }
                    else if (member is TableMemberInfoSql1N ||
                             member is TableMemberInfoSqlNMInt ||
                             member is TableMemberInfoSqlNM)
                    {
                        throw new NotImplementedException();
                    }
                }
            }
        }
        catch (Exception exception)
        {
            result.Errors.Add(new DataError(DataErrorCode.UnknownError, exception));
            return;
        }

        if (instance is X value)
        {
            result.Result = value;
        }
    }
}
