

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using AventusSharp.Data;
using CsvHelper.Configuration;

namespace AventusSharp.Tools;

public class AventusClassMap<T> : ClassMap<T> { }
public class CSVMapper<T>
{
    internal AventusClassMap<T> mapper;
    internal List<DataError> errors = new List<DataError>();
    internal List<string> removeNames = new List<string>();
    private CultureInfo cultureInfo;
    public CSVMapper(CultureInfo cultureInfo)
    {
        this.cultureInfo = cultureInfo;
        mapper = new();
    }
    public void Map(string objectName, string csvName)
    {
        MemberInfo[] members = typeof(T).GetMember(objectName);
        if (members.Length > 0)
        {
            mapper.Map(typeof(T), members[0]).Name(csvName);
        }
        else
        {
            errors.Add(new DataError(DataErrorCode.MemberNotFound, "The member " + objectName + " can't be found on " + TypeTools.GetReadableName(typeof(T))));
        }
    }
    public void Map(Expression<Func<T, object?>> expression, string csvName)
    {
        mapper.Map(expression).Name(csvName);
    }

    public void Ignore(string name)
    {
        MemberInfo[] members = typeof(T).GetMember(name);
        if (members.Length > 0)
        {
            mapper.Map(typeof(T), members[0]).Ignore();
        }
        else
        {
            errors.Add(new DataError(DataErrorCode.MemberNotFound, "The member " + name + " can't be found on " + TypeTools.GetReadableName(typeof(T))));
        }
    }
    public void Ignore(Expression<Func<T, object?>> expression)
    {
        if (mapper.MemberMaps.Count == 0)
        {
            mapper.AutoMap(this.cultureInfo);
        }
        mapper.Map(expression).Ignore();
    }

}