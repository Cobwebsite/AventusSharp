using AventusSharp.Data.Manager;
using AventusSharp.Data.Storage.Default.TableMember;
using AventusSharp.Data.Storage.Default;
using System.Data;
using System.Reflection;
using System;
using AventusSharp.Routes.Request;
using AventusSharp.Routes;
using AventusSharp.Tools;

namespace AventusSharp.Data
{
    internal class DatetimeTableMember : CustomTableMember
    {
        protected DataMemberInfo? dataMemberInfo { get; set; }
        public DatetimeTableMember(MemberInfo? memberInfo, TableInfo tableInfo, bool isNullable) : base(memberInfo, tableInfo, isNullable)
        {
            if (memberInfo != null)
            {
                dataMemberInfo = new DataMemberInfo(memberInfo);
            }
        }

        public override DbType? GetDbType()
        {
            return DbType.DateTime;
        }

        public override object? GetSqlValue(object obj)
        {
            object? result = GetValue(obj);
            if (result is Datetime date)
            {
                return date.ToString();
            }
            return null;
        }

        protected override void SetSqlValue(object obj, string? value)
        {
            if (!string.IsNullOrEmpty(value) && dataMemberInfo != null && dataMemberInfo.Type != null)
            {
                object? newFile = Activator.CreateInstance(dataMemberInfo.Type);
                if (newFile is Datetime date)
                {
                    if (DateTime.TryParse(value, out DateTime dateTime))
                    {
                        date.DateTime = dateTime;
                        SetValue(obj, date);
                    }
                }
            }
        }
    }


    /// <summary>
    /// Class to handle date during process
    /// </summary>
    [CustomTableMemberType<DatetimeTableMember>]
    public class Datetime
    {
        private static readonly string Pattern = "yyyy-MM-dd HH-mm-ss";

        public int Year { get => DateTime.Year; }
        public int Month { get => DateTime.Month; }
        public int Day { get => DateTime.Day; }
        public int Hour { get => DateTime.Hour; }
        public int Minute { get => DateTime.Minute; }
        public int Second { get => DateTime.Second; }

        public DateTime DateTime { get; set; }
        public Datetime()
        {
            DateTime = DateTime.Now;
        }
        public Datetime(DateTime date)
        {
            DateTime = date;
        }

        public Date DateOnly()
        {
            return new Date(DateTime);
        }

        public override string ToString()
        {
            return DateTime.ToString(Pattern);
        }
        public static bool operator ==(Datetime d1, Datetime d2)
        {

            return d1.ToString() == d2.ToString();
        }

        public static bool operator ==(Datetime d1, DateTime d2)
        {

            return d1.ToString() == d2.ToString(Pattern);
        }

        public static bool operator !=(Datetime d1, Datetime d2) => !(d1 == d2);
        public static bool operator !=(Datetime d1, DateTime d2) => !(d1 == d2);

        public static bool operator >(Datetime d1, Datetime d2)
        {
            return d1.DateTime > d2.DateTime;
        }
        public static bool operator <(Datetime d1, Datetime d2)
        {
            return d1.DateTime < d2.DateTime;
        }
        public static bool operator >=(Datetime d1, Datetime d2)
        {
            return d1.DateTime >= d2.DateTime;
        }
        public static bool operator <=(Datetime d1, Datetime d2)
        {
            return d1.DateTime <= d2.DateTime;
        }

        public static bool operator >(Datetime d1, DateTime d2)
        {
            return d1.DateTime > d2;
        }
        public static bool operator <(Datetime d1, DateTime d2)
        {
            return d1.DateTime < d2;
        }
        public static bool operator >=(Datetime d1, DateTime d2)
        {
            return d1.DateTime >= d2;
        }
        public static bool operator <=(Datetime d1, DateTime d2)
        {
            return d1.DateTime <= d2;
        }

        public override bool Equals(object? obj)
        {
            if (obj is DateTime dateTime)
            {
                return this == dateTime;
            }
            if (obj is Datetime date)
            {
                return this == date;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }

}
