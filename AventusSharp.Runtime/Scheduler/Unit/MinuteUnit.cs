namespace AventusSharp.Scheduler.Unit
{
    /// <summary>
    /// Unit of time in minutes.
    /// </summary>
    public sealed class MinuteUnit : ITimeRestrictableUnit
    {
        private readonly int _duration;

        internal MinuteUnit(Schedule schedule, int duration)
        {
            _duration = duration;
            Schedule = schedule;
            Schedule.CalculateNextRun = x => x.AddMinutes(_duration);
        }

        internal Schedule Schedule { get; private set; }

        Schedule ITimeRestrictableUnit.Schedule => Schedule;
    }
}
