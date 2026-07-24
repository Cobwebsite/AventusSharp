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
    internal class DateTableMember : CustomTableMember
    {
        protected DataMemberInfo? dataMemberInfo { get; set; }
        public DateTableMember(MemberInfo? memberInfo, TableInfo tableInfo, bool isNullable) : base(memberInfo, tableInfo, isNullable)
        {
            if (memberInfo != null)
            {
                dataMemberInfo = new DataMemberInfo(memberInfo);
            }
        }

        public override DbType? GetDbType()
        {
            return DbType.Date;
        }

        public override object? GetSqlValue(object obj)
        {
            object? result = GetValue(obj);
            if (result is Date date)
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
                if (newFile is Date date)
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
    [CustomTableMemberType<DateTableMember>]
    public class Date
    {
        private static readonly string Pattern = "yyyy-MM-dd";

        public int Year { get => DateTime.Year; }
        public int Month { get => DateTime.Month; }
        public int Day { get => DateTime.Day; }
        public DateTime DateTime { get; set; }
        public Date()
        {
            DateTime = DateTime.Now.Date;
        }

        public Date(DateTime date)
        {
            DateTime = date;
        }

        public override string ToString()
        {
            return DateTime.ToString(Pattern);
        }

        public static bool operator ==(Date d1, Date d2)
        {

            return d1.ToString() == d2.ToString();
        }

        public static bool operator ==(Date d1, DateTime d2)
        {

            return d1.ToString() == d2.ToString(Pattern);
        }

        public static bool operator !=(Date d1, Date d2) => !(d1 == d2);
        public static bool operator !=(Date d1, DateTime d2) => !(d1 == d2);

        public static bool operator >(Date d1, Date d2)
        {
            return d1.DateTime > d2.DateTime;
        }
        public static bool operator <(Date d1, Date d2)
        {
            return d1.DateTime < d2.DateTime;
        }
        public static bool operator >=(Date d1, Date d2)
        {
            return d1.DateTime >= d2.DateTime;
        }
        public static bool operator <=(Date d1, Date d2)
        {
            return d1.DateTime <= d2.DateTime;
        }

        public static bool operator >(Date d1, DateTime d2)
        {
            return d1.DateTime > d2;
        }
        public static bool operator <(Date d1, DateTime d2)
        {
            return d1.DateTime < d2;
        }
        public static bool operator >=(Date d1, DateTime d2)
        {
            return d1.DateTime >= d2;
        }
        public static bool operator <=(Date d1, DateTime d2)
        {
            return d1.DateTime <= d2;
        }

        public override bool Equals(object? obj)
        {
            if (obj is DateTime dateTime)
            {
                return this == dateTime;
            }
            if (obj is Date date)
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
