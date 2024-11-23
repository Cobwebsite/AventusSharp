using System;

namespace AventusSharp.Scheduler.Unit
{
    /// <summary>
    /// Unit of specific time of the day.
    /// </summary>
    public sealed class SpecificTimeUnit
    {

        internal SpecificTimeUnit(Schedule schedule)
        {
            Schedule = schedule;
        }

        internal Schedule Schedule { get; private set; }

        /// <summary>
        /// Also runs the job according to the given interval.
        /// </summary>
        /// <param name="interval">Interval to wait.</param>
        public TimeUnit AndEvery(int interval)
        {
            Schedule parent = Schedule.Parent ?? Schedule;

            Schedule child =
                new Schedule(Schedule.Jobs)
                {
                    Parent = parent,
                    Reentrant = parent.Reentrant,
                    Name = parent.Name,
                };

            if (parent.CalculateNextRun != null)
            {
                DateTime now = JobManager.Now;
                TimeSpan delay = parent.CalculateNextRun(now) - now;

                if (delay > TimeSpan.Zero)
                {
                    child.DelayRunFor = delay;
                }
            }

            child.Parent.AdditionalSchedules.Add(child);
            return child.ToRunEvery(interval);
        }
    }
}
