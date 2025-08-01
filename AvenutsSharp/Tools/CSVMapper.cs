

using System;
using System.Collections.Generic;
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
    public CSVMapper()
    {
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
            errors.Add(new DataError(DataErrorCode.MemberNotFound, "The member " + csvName + " can't be found on " + TypeTools.GetReadableName(typeof(T))));
        }
    }
    public void Map(Expression<Func<T, object?>> expression, string csvName)
    {
        mapper.Map(expression).Name(csvName);
    }
}