using System;
using AventusSharp.Scheduler.Unit;

namespace AventusSharp.Scheduler.Unit
{
    /// <summary>
    /// Unit of time in weeks.
    /// </summary>
    public sealed class WeekUnit
    {
        private readonly int _duration;

        internal WeekUnit(Schedule schedule, int duration)
        {
            _duration = duration < 0 ? 0 : duration;
            Schedule = schedule;
            Schedule.CalculateNextRun = x =>
            {
                DateTime nextRun = x.Date.AddDays(_duration * 7);
                return x > nextRun ? nextRun.AddDays(Math.Max(_duration, 1) * 7) : nextRun;
            };
        }

        internal Schedule Schedule { get; private set; }

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
                return x > nextRun ? nextRun.AddDays(Math.Max(_duration, 1) * 7) : nextRun;
            };
        }

        /// <summary>
        /// Runs the job on the given day of the week.
        /// </summary>
        /// <param name="day">The day of the week.</param>
        public DayOfWeekUnit On(DayOfWeek day)
        {
            return new DayOfWeekUnit(Schedule, _duration, day);
        }
    }
}
