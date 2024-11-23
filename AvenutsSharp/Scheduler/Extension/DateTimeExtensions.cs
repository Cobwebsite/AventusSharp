using System;
using AventusSharp.Scheduler.Enum;

namespace AventusSharp.Scheduler.Extension
{
    // Some borrowed from http://datetimeextensions.codeplex.com
    internal static class DateTimeExtensions
    {
        internal static DateTime First(this DateTime current)
        {
            return current.AddDays(1 - current.Day);
        }

        internal static DateTime FirstOfYear(this DateTime current)
        {
            return new DateTime(current.Year, 1, 1);
        }

        internal static DateTime Last(this DateTime current)
        {
            var daysInMonth = DateTime.DaysInMonth(current.Year, current.Month);
            return current.First().AddDays(daysInMonth - 1);
        }

        internal static DateTime Last(this DateTime current, DayOfWeek dayOfWeek)
        {
            var last = current.Last();
            var diff = dayOfWeek - last.DayOfWeek;

            if (diff > 0)
            {
                diff -= 7;
            }

            return last.AddDays(diff);
        }

        internal static DateTime ThisOrNext(this DateTime current, DayOfWeek dayOfWeek)
        {
            int offsetDays = dayOfWeek - current.DayOfWeek;

            if (offsetDays < 0)
            {
                offsetDays += 7;
            }

            return current.AddDays(offsetDays);
        }

        internal static DateTime Next(this DateTime current, DayOfWeek dayOfWeek)
        {
            int offsetDays = dayOfWeek - current.DayOfWeek;

            if (offsetDays <= 0)
            {
                offsetDays += 7;
            }

            return current.AddDays(offsetDays);
        }

        internal static DateTime NextNWeekday(this DateTime current, int toAdvance)
        {
            while (toAdvance >= 1)
            {
                toAdvance--;
                current = current.AddDays(1);

                while (!current.IsWeekday())
                {
                    current = current.AddDays(1);
                }
            }
            return current;
        }

        internal static bool IsWeekday(this DateTime current)
        {
#pragma warning disable IDE0010 // Ajouter les instructions case manquantes
            switch (current.DayOfWeek)
#pragma warning restore IDE0010 // Ajouter les instructions case manquantes
            {
                case DayOfWeek.Saturday:
                case DayOfWeek.Sunday:
                    return false;
                default:
                    return true;
            }
        }

        internal static DateTime ClearMinutesAndSeconds(this DateTime current)
        {
            return current.AddMinutes(-1 * current.Minute)
                .AddSeconds(-1 * current.Second)
                .AddMilliseconds(-1 * current.Millisecond);
        }

        internal static DateTime ToWeek(this DateTime current, Week week)
        {
#pragma warning disable IDE0010 // Ajouter les instructions case manquantes
            switch (week)
#pragma warning restore IDE0010 // Ajouter les instructions case manquantes
            {
                case Week.Second:
                    return current.First().AddDays(7);
                case Week.Third:
                    return current.First().AddDays(14);
                case Week.Fourth:
                    return current.First().AddDays(21);
                case Week.Last:
                    return current.Last().AddDays(-7);
            }
            return current.First();
        }
    }
}
