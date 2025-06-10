using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Reflection;
using AventusSharp.Data.Manager;
using AventusSharp.Data.Storage.Default;
using AventusSharp.Data.Storage.Default.TableMember;

namespace AventusSharp.Data.CustomTableMembers
{
    internal class StorableListTableMember : CustomTableMember
    {
        protected DataMemberInfo? dataMemberInfo { get; set; }
        public StorableListTableMember(MemberInfo? memberInfo, TableInfo tableInfo, bool isNullable) : base(memberInfo, tableInfo, isNullable)
        {
            if (memberInfo != null)
            {
                dataMemberInfo = new DataMemberInfo(memberInfo);
            }
        }

        public override DbType? GetDbType()
        {
            return DbType.String;
        }

        public override object? GetSqlValue(object obj)
        {
            object? result = GetValue(obj);
            if (result is IStorableList list)
            {
                List<string> listString = new List<string>();
                foreach (object? item in list)
                {
                    if (item != null)
                    {
                        string? txtValue = item.ToString();
                        if (txtValue != null)
                        {
                            listString.Add(txtValue);
                        }
                    }
                }
                return string.Join(",", listString);
            }
            return null;
        }

        protected override void SetSqlValue(object obj, string? value)
        {
            if (!string.IsNullOrEmpty(value) && dataMemberInfo != null && dataMemberInfo.Type != null)
            {
                object? newList = Activator.CreateInstance(dataMemberInfo.Type);
                if (newList is IStorableList list)
                {
                    string[] values = value.Split(",");
                    foreach (string val in values)
                    {
                        list.setStringValue(val);
                    }
                    SetValue(obj, list);
                }
            }
        }
    }

    public interface IStorableList : IList
    {
        public void setStringValue(string value);
    }

    [CustomTableMemberType<StorableListTableMember>]
    public abstract class StorableList<T> : List<T>, IStorableList
    {
        protected abstract bool stringToValue(string value, out T result);
        public void setStringValue(string valueTxt)
        {
            T value;
            bool success = stringToValue(valueTxt, out value);
            if (success)
            {
                Add(value);
            }
        }
    }

    public class StorableListInt : StorableList<int>
    {
        protected override bool stringToValue(string value, out int result) => int.TryParse(value, out result);
    }
    public class StorableListShort : StorableList<short>
    {
        protected override bool stringToValue(string value, out short result) => short.TryParse(value, out result);
    }
    public class StorableListLong : StorableList<long>
    {
        protected override bool stringToValue(string value, out long result) => long.TryParse(value, out result);
    }
    public class StorableListFloat : StorableList<float>
    {
        protected override bool stringToValue(string value, out float result) => float.TryParse(value, out result);
    }
    public class StorableListDouble: StorableList<double>
    {
        protected override bool stringToValue(string value, out double result) => double.TryParse(value, out result);
    }
    public class StorableListBool: StorableList<bool>
    {
        protected override bool stringToValue(string value, out bool result) => bool.TryParse(value, out result);
    }
    public class StorableListString : StorableList<string>
    {
        protected override bool stringToValue(string value, out string result)
        {
            result = value;
            return true;
        }
    }
}
