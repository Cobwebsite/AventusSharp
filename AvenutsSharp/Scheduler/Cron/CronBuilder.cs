using System;
using System.Collections.Generic;
using System.Linq;

namespace AventusSharp.Scheduler.Cron;


public class CronBuilder
{
    private string _secs = "*";
    private string _mins = "*";
    private string _hours = "*";
    private string _daysOfMonth = "*";
    private string _months = "*";
    private string _daysOfWeek = "*";

    public CronBuilder Second(params int[] secs)
    {
        foreach (var sec in secs)
        {
            if (sec > 59 || sec < 0)
                throw new ArgumentOutOfRangeException(nameof(secs), "Out of range for sec");
        }
        _secs = string.Join(",", secs.Distinct().OrderBy(m => m));
        return this;
    }
    public CronBuilder EachSeconds(int? step = null)
    {
        if (step == null)
        {
            _secs = "*";
            return this;
        }
        if (step > 59 || step < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(step), "Step must be between 1 and 59");
        }
        _secs = $"*/{step}";
        return this;
    }

    public CronBuilder Minute(params int[] minutes)
    {
        foreach (var min in minutes)
        {
            if (min > 59 || min < 0)
                throw new ArgumentOutOfRangeException(nameof(minutes), "Out of range for minutes");
        }
        _mins = string.Join(",", minutes.Distinct().OrderBy(m => m));
        return this;
    }
    public CronBuilder EachMinutes(int? step = null)
    {
        if (step == null)
        {
            _mins = "*";
            return this;
        }
        if (step > 59 || step < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(step), "Step must be between 1 and 59");
        }
        _mins = $"*/{step}";
        return this;
    }


    public CronBuilder Hour(params int[] hours)
    {
        foreach (var hour in hours)
        {
            if (hour > 23 || hour < 0)
                throw new ArgumentOutOfRangeException(nameof(hours), "Out of range for hours");
        }
        _hours = string.Join(",", hours.Distinct().OrderBy(m => m));
        return this;
    }
    public CronBuilder EachHours(int? step = null)
    {
        if (step == null)
        {
            _hours = "*";
            return this;
        }
        if (step > 23 || step < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(step), "Step must be between 1 and 23");
        }
        _hours = $"*/{step}";
        return this;
    }


    public CronBuilder DayOfMonth(params int[] days)
    {
        foreach (var day in days)
        {
            if (day > 31 || day < 1)
                throw new ArgumentOutOfRangeException(nameof(days), "Out of range for day of month");
        }
        _daysOfMonth = string.Join(",", days.Distinct().OrderBy(m => m));
        return this;
    }
    public CronBuilder EachDaysOfMonth(int? step = null)
    {
        if (step == null)
        {
            _daysOfMonth = "*";
            return this;
        }
        if (step > 31 || step < 1)
            throw new ArgumentOutOfRangeException(nameof(step), "Step must be between 1 and 31");
        _daysOfMonth = $"*/{step}";
        return this;
    }


    public CronBuilder Month(params int[] months)
    {
        foreach (var month in months)
        {
            if (month > 12 || month < 1)
                throw new ArgumentOutOfRangeException(nameof(months), "Out of range for months");
        }
        _months = string.Join(",", months.Distinct().OrderBy(m => m));
        return this;
    }
    public CronBuilder EachMonths(int? step = null)
    {
        if (step == null)
        {
            _months = "*";
            return this;
        }
        if (step > 12 || step < 1)
            throw new ArgumentOutOfRangeException(nameof(step), "Step must be between 1 and 12");
        _months = $"*/{step}";
        return this;
    }
   
    public CronBuilder DayOfWeek(params int[] days)
    {
        foreach (var day in days)
        {
            if (day > 6 || day < 0)
                throw new ArgumentOutOfRangeException(nameof(days), "Out of range for days of week");
        }
        _daysOfWeek = string.Join(",", days.Distinct().OrderBy(m => m));
        return this;
    }
    public CronBuilder EachDaysOfWeek(int? step = null)
    {
         if (step == null)
        {
            _daysOfWeek = "*";
            return this;
        }
        if (step > 12 || step < 1)
            throw new ArgumentOutOfRangeException(nameof(step), "Step must be between 1 and 12");
        _daysOfWeek = $"*/{step}";
        return this;
    }
    
    public override string ToString()
    {
        List<string> result = [
            _secs,
            _mins,
            _hours,
            _daysOfMonth,
            _months,
            _daysOfWeek,
        ];
        return string.Join(" ", result);
    }
}