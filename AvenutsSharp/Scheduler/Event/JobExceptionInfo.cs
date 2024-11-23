using System;

namespace AventusSharp.Scheduler.Event
{
    /// <summary>
    /// Information of an exception occurred in a job.
    /// </summary>
    public class JobExceptionInfo
    {
        /// <summary>
        /// Name of the job.
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// Job's exception.
        /// </summary>
        public Exception? Exception { get; set; }
    }
}
