using System;
using AventusSharp.Scheduler.Unit;

namespace AventusSharp.Scheduler.Unit
{
    /// <summary>
    /// Unit of time in years.
    /// </summary>
    public sealed class YearUnit
    {
        private readonly int _duration;

        internal YearUnit(Schedule schedule, int duration)
        {
            _duration = duration < 0 ? 0 : duration;
            Schedule = schedule;
            Schedule.CalculateNextRun = x =>
            {
                DateTime nextRun = x.Date.AddYears(_duration);
                return x > nextRun ? nextRun.AddYears(Math.Max(_duration, 1)) : nextRun;
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
                return x > nextRun ? nextRun.AddYears(Math.Max(_duration, 1)) : nextRun;
            };
        }
    }
}
