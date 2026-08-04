using System;
using AventusSharp.Scheduler.Extension;

namespace AventusSharp.Scheduler.Unit
{
    /// <summary>
    /// Unit of time in weekdays.
    /// </summary>
    public sealed class WeekdayUnit : ITimeRestrictableUnit
    {
        private readonly int _duration;

        internal WeekdayUnit(Schedule schedule, int duration)
        {
            _duration = duration < 1 ? 1 : duration;
            Schedule = schedule;
            Schedule.CalculateNextRun = x =>
            {
                var nextRun = x.Date.NextNWeekday(_duration);
                return x > nextRun || !nextRun.Date.IsWeekday() ? nextRun.NextNWeekday(_duration) : nextRun;
            };
        }

        internal Schedule Schedule { get; private set; }

        Schedule ITimeRestrictableUnit.Schedule => Schedule;

        /// <summary>
        /// Runs the job at the given time of day.
        /// </summary>
        /// <param name="hours">The hours (0 through 23).</param>
        /// <param name="minutes">The minutes (0 through 59).</param>
        /// <param name="seconds">The seconds (0 through 59).</param>
        public void At(int hours, int minutes, int seconds)
        {
            Schedule.CalculateNextRun = x =>
            {
                DateTime nextRun = x.Date.AddHours(hours).AddMinutes(minutes).AddSeconds(seconds);
                return x > nextRun || !nextRun.Date.IsWeekday() ? nextRun.NextNWeekday(_duration) : nextRun;
            };
        }
    }
}