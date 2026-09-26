using AventusSharp.Data.Manager.DB;
using AventusSharp.Data.Storage.Default;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AventusSharp.Data.Storage.Default.TableMember;

namespace AventusSharp.Data.Storage.Relational
{
    public static class SqlBuilderTools
    {
        public static string Where(List<IWhereRootGroup>? wheres, IDBStorage storage)
        {
            if (wheres == null)
            {
                return "";
            }
            string whereTxt = "";
            foreach (IWhereRootGroup whereGroup in wheres)
            {
                whereTxt += WherePart(whereGroup, whereTxt, storage);
            }
            if (whereTxt.Length > 1)
            {
                whereTxt = " WHERE " + whereTxt;
            }
            return whereTxt;
        }
        private static string WherePart(IWhereRootGroup rootWhereGroup, string whereTxt, IDBStorage storage)
        {
            whereTxt += "(";
            string subQuery = "";
            IWhereGroup? lastGroup = null;
            bool applyNegate = true;
            if (rootWhereGroup is WhereGroup whereGroup)
            {
                foreach (IWhereGroup queryGroup in whereGroup.Groups)
                {
                    if (queryGroup is WhereGroup childWhereGroup)
                    {
                        subQuery += WherePart(childWhereGroup, "", storage);
                    }
                    else if (queryGroup is WhereGroupSingleBool childWhereBoolGroup)
                    {
                        subQuery += WherePart(childWhereBoolGroup, "", storage);
                    }
                    else if (queryGroup is WhereGroupFct fctGroup)
                    {
                        subQuery += GetFctName(fctGroup.Fct);
                    }
                    else if (queryGroup is WhereGroupFctSql fctGroupSql)
                    {
                        subQuery += GetFctSqlName(fctGroupSql.Fct);
                    }
                    else if (queryGroup is WhereGroupLinkContains contains)
                    {
                        if (
                            contains.Link is not TableMemberInfoSql member ||
                            member.TableInfo.Primary == null ||
                            contains.Link.TableIntermediateName == null ||
                            contains.Link.TableIntermediateKey1 == null ||
                            contains.Link.TableIntermediateKey2 == null
                        )
                            throw new NotSupportedException("The many-to-many link has no intermediate table or primary key.");

                        string table = string.Join(".", contains.Link.TableIntermediateName.Split('.').Select(storage.QuoteIdentifier));
                        string key1 = storage.QuoteIdentifier(contains.Link.TableIntermediateKey1);
                        string key2 = storage.QuoteIdentifier(contains.Link.TableIntermediateKey2);
                        string ownerKey = storage.QuoteIdentifier(member.TableInfo.Primary.SqlName);
                        subQuery += "EXISTS (SELECT 1 FROM " + table + " LC WHERE LC." + key1
                            + " = " + contains.OwnerAlias + "." + ownerKey
                            + " AND LC." + key2 + " = "
                            + contains.LinkedId.ToString(CultureInfo.InvariantCulture) + ")";
                    }
                    else if (queryGroup is WhereGroupConstantNull nullConst)
                    {
                        // special case for IS and IS NOT
                        if (whereGroup.Groups.Count == 3)
                        {
                            WhereGroupFct? fctGrp = null;
                            WhereGroupField? fieldGrp = null;
                            for (int i = 0; i < whereGroup.Groups.Count; i++)
                            {
                                if (whereGroup.Groups[i] is WhereGroupFct fctGrpTemp && (fctGrpTemp.Fct == WhereGroupFctEnum.Equal || fctGrpTemp.Fct == WhereGroupFctEnum.NotEqual))
                                {
                                    fctGrp = fctGrpTemp;
                                }
                                else if (whereGroup.Groups[i] is WhereGroupField fieldGrpTemp)
                                {
                                    fieldGrp = fieldGrpTemp;
                                }
                            }

                            if (fctGrp != null && fieldGrp != null)
                            {
                                string action = fctGrp.Fct == WhereGroupFctEnum.Equal ? " IS NULL" : " IS NOT NULL";
                                subQuery = fieldGrp.Alias + "." + storage.QuoteIdentifier(fieldGrp.SqlName) + action;
                                break;
                            }
                        }

                        subQuery += "NULL";
                    }
                    else if (queryGroup is WhereGroupConstantBool boolConst)
                    {
                        bool nativeBoolean = storage.SupportsNativeBoolean;
                        subQuery += nativeBoolean
                            ? (boolConst.Value ? "TRUE" : "FALSE")
                            : (boolConst.Value ? "1" : "0");
                    }
                    else if (queryGroup is WhereGroupConstantString stringConst)
                    {
                        string escapedValue = stringConst.Value.Replace("'", "''");
                        string strValue = "'" + escapedValue + "'";
                        if (lastGroup is WhereGroupFct groupFct)
                        {
                            if (groupFct.Fct == WhereGroupFctEnum.StartsWith)
                            {
                                strValue = "'" + escapedValue + "%'";
                            }
                            else if (groupFct.Fct == WhereGroupFctEnum.EndsWith)
                            {
                                strValue = "'%" + escapedValue + "'";
                            }
                            else if (groupFct.Fct == WhereGroupFctEnum.ContainsStr)
                            {
                                strValue = "'%" + escapedValue + "%'";
                            }
                        }
                        subQuery += strValue;
                    }
                    else if (queryGroup is WhereGroupConstantDateTime dateTimeConst)
                    {
                        string format = storage.DateTimeFormat ?? "yyyy-MM-dd HH:mm:ss.fffffff";
                        subQuery += "'" + dateTimeConst.Value.ToString(format, CultureInfo.InvariantCulture) + "'";
                    }
                    else if (queryGroup is WhereGroupConstantOther otherConst)
                    {
                        subQuery += otherConst.Value;
                    }
                    else if (queryGroup is WhereGroupConstantParameter paramConst)
                    {
                        string strValue = "@" + paramConst.Value;
                        subQuery += strValue;
                    }
                    else if (queryGroup is WhereGroupField fieldGrp)
                    {
                        subQuery += fieldGrp.Alias + "." + storage.QuoteIdentifier(fieldGrp.SqlName);
                    }
                    lastGroup = queryGroup;
                }
                whereTxt += subQuery;

            }
            else if (rootWhereGroup is WhereGroupSingleBool whereSingleBool)
            {
                object? transformedValue = rootWhereGroup.negate
                    ? whereSingleBool.FalseValue
                    : whereSingleBool.TrueValue;
                string value = FormatConstant(transformedValue, storage);
                whereTxt += whereSingleBool.Alias + "." + storage.QuoteIdentifier(whereSingleBool.TableMemberInfo.SqlName) + " = " + value;
                applyNegate = false;
            }

            whereTxt += ")";
            if (rootWhereGroup.negate && applyNegate)
            {
                whereTxt = "NOT " + whereTxt;
            }
            return whereTxt;
        }

        private static string FormatConstant(object? value, IDBStorage storage)
        {
            if (value == null)
            {
                return "NULL";
            }
            if (value is bool boolean)
            {
                bool nativeBoolean = storage.SupportsNativeBoolean;
                return nativeBoolean
                    ? (boolean ? "TRUE" : "FALSE")
                    : (boolean ? "1" : "0");
            }
            if (value is string or char)
            {
                return "'" + value.ToString()!.Replace("'", "''") + "'";
            }
            if (value is DateTime dateTime)
            {
                string format =
                    storage.DateTimeFormat ?? "yyyy-MM-dd HH:mm:ss.fffffff";
                return "'" + dateTime.ToString(
                    format,
                    CultureInfo.InvariantCulture) + "'";
            }
            return Convert.ToString(value, CultureInfo.InvariantCulture) ?? "NULL";
        }

        public static string GetFctName(WhereGroupFctEnum fctEnum)
        {
            return fctEnum switch
            {
                WhereGroupFctEnum.Add => " + ",
                WhereGroupFctEnum.And => " AND ",
                WhereGroupFctEnum.ContainsStr or WhereGroupFctEnum.StartsWith or WhereGroupFctEnum.EndsWith => " LIKE ",
                WhereGroupFctEnum.Divide => " / ",
                WhereGroupFctEnum.Equal => " = ",
                WhereGroupFctEnum.GreaterThan => " > ",
                WhereGroupFctEnum.GreaterThanOrEqual => " >= ",
                WhereGroupFctEnum.LessThan => " < ",
                WhereGroupFctEnum.LessThanOrEqual => " <= ",
                WhereGroupFctEnum.Multiply => " * ",
                WhereGroupFctEnum.Not => " NOT ",
                WhereGroupFctEnum.NotEqual => " <> ",
                WhereGroupFctEnum.Or => " OR ",
                WhereGroupFctEnum.Subtract => " - ",
                WhereGroupFctEnum.ListContains => " IN ",
                _ => "",
            };
        }

        public static string GetFctSqlName(WhereGroupFctSqlEnum fctEnum)
        {
            return fctEnum switch
            {
                WhereGroupFctSqlEnum.Coalesce => "COALESCE",
                WhereGroupFctSqlEnum.Date => "DATE",
                WhereGroupFctSqlEnum.Time => "TIME",
                WhereGroupFctSqlEnum.ToLower => "LOWER",
                WhereGroupFctSqlEnum.ToUpper => "UPPER",
                WhereGroupFctSqlEnum.Year => "YEAR",
                WhereGroupFctSqlEnum.Month => "MONTH",
                WhereGroupFctSqlEnum.Day => "DAY",
                WhereGroupFctSqlEnum.Hour => "HOUR",
                WhereGroupFctSqlEnum.Minute => "MINUTE",
                WhereGroupFctSqlEnum.Second => "SECOND",
                WhereGroupFctSqlEnum.Max => "MAX",
                WhereGroupFctSqlEnum.Min => "MIN",
                WhereGroupFctSqlEnum.Abs => "ABS",
                WhereGroupFctSqlEnum.Round => "ROUND",
                WhereGroupFctSqlEnum.Ceil => "CEIL",
                WhereGroupFctSqlEnum.Floor => "FLOOR",
                _ => "",
            };
        }

    }
}
