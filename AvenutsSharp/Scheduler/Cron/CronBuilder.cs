using System;
using System.Collections.Generic;

namespace AventusSharp.Scheduler.Cron;


public class CronBuilder
{
    private int? _sec;
    private int? _min;
    private int? _hour;
    private int? _dayOfMonth;
    private int? _month;
    private int? _dayOfWeek;
    public CronBuilder Second(int sec)
    {
        if (sec > 59 || sec < 0)
        {
            throw new Exception("Out of range for sec");
        }
        _sec = sec;
        return this;
    }
    public CronBuilder EachSeconds()
    {
        _sec = null;
        return this;
    }

    public CronBuilder Minute(int min)
    {
        if (min > 59 || min < 0)
        {
            throw new Exception("Out of range for minute");
        }
        _min = min;
        return this;
    }
    public CronBuilder EachMinutes()
    {
        _min = null;
        return this;
    }

    public CronBuilder Hour(int hour)
    {
        if (hour > 23 || hour < 0)
        {
            throw new Exception("Out of range for hour");
        }
        _hour = hour;
        return this;
    }
    public CronBuilder EachHours()
    {
        _hour = null;
        return this;
    }

    public CronBuilder DayOfMonth(int day)
    {
        if (day > 31 || day < 1)
        {
            throw new Exception("Out of range for day of month");
        }
        _dayOfMonth = day;
        return this;
    }
    public CronBuilder EachDaysOfMonth()
    {
        _dayOfMonth = null;
        return this;
    }

    public CronBuilder Month(int month)
    {
        if (month > 12 || month < 1)
        {
            throw new Exception("Out of range for month");
        }
        _month = month;
        return this;
    }
    public CronBuilder EachMonths()
    {
        _month = null;
        return this;
    }

    public CronBuilder DayOfWeek(int day)
    {
        if (day > 6 || day < 0)
        {
            throw new Exception("Out of range for day of week");
        }
        _dayOfWeek = day;
        return this;
    }
    public CronBuilder EachDaysOfWeek()
    {
        _dayOfWeek = null;
        return this;
    }

    public override string ToString()
    {
        List<string> result = [
            _sec == null ? "*" : _sec+"",
            _min == null ? "*" : _min+"",
            _hour == null ? "*" : _hour+"",
            _dayOfMonth == null ? "*" : _dayOfMonth+"",
            _month == null ? "*" : _month+"",
            _dayOfWeek == null ? "*" : _dayOfWeek+"",
        ];
        return string.Join(" ", result);
    }
}