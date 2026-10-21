using System;
using System.Collections.Generic;

namespace AutoMouseMover.Logic
{
    //
    // Action requested by the work schedule
    //
    public enum eScheduleAction
    {
        NONE,
        START,
        STOP,
    }

    //
    // Work schedule: tells when the mouse mover should start and stop by itself.
    // The current time is always passed in by the caller, so tests can use any time.
    //
    public class WorkSchedule
    {
        //
        // Members
        //
        #region Members

        // Start of working hours (inclusive)
        private TimeSpan mStartTime;
        // End of working hours (exclusive)
        private TimeSpan mEndTime;
        // Working days
        private HashSet<DayOfWeek> mWorkDays;
        // Whether the last check was inside working time
        private bool mWasWorkingTime;
        // Date of the last working period that was seen (null if none yet)
        private DateTime? mLastWorkingDate;

        #endregion

        //
        // Public methods
        //
        #region Public methods

        // Constructor
        public WorkSchedule(TimeSpan cStartTime, TimeSpan cEndTime, IEnumerable<DayOfWeek> cWorkDays)
        {
            // Overnight hours (e.g. 22:00-06:00) are not supported
            if (cEndTime <= cStartTime)
            {
                throw new ArgumentException("End time must be after start time");
            }

            mStartTime = cStartTime;
            mEndTime = cEndTime;
            mWorkDays = new HashSet<DayOfWeek>(cWorkDays);
        }

        // Get if the given moment is inside working hours on a working day
        public bool IsWorkingTime(DateTime cNow)
        {
            if (!mWorkDays.Contains(cNow.DayOfWeek))
            {
                return false;
            }

            var time = cNow.TimeOfDay;
            return (time >= mStartTime) && (time < mEndTime);
        }

        // Check whether working time was entered or left since the last check.
        // Only crossings produce an action, so a manual Start/Stop by the user is
        // left alone until the next crossing.
        public eScheduleAction CheckTransition(DateTime cNow)
        {
            bool is_working = IsWorkingTime(cNow);
            var action = eScheduleAction.NONE;

            if (is_working)
            {
                // Start once per working day. Comparing dates (instead of only the
                // previous check) also catches a new day whose start was missed,
                // e.g. the PC slept from Friday afternoon to Monday morning.
                if (mLastWorkingDate != cNow.Date)
                {
                    action = eScheduleAction.START;
                }
                mLastWorkingDate = cNow.Date;
            }
            else if (mWasWorkingTime)
            {
                action = eScheduleAction.STOP;
            }

            mWasWorkingTime = is_working;
            return action;
        }

        // Get the first work-day start strictly after the given moment
        // (DateTime.MaxValue if there are no working days)
        public DateTime GetNextStart(DateTime cAfter)
        {
            return FindFirstStart(cAfter.Date, cAfter);
        }

        // Get the first work-day start on a later date than today, even if
        // today's start has not been reached yet ("until tomorrow")
        public DateTime GetStartOnNextWorkDay(DateTime cNow)
        {
            return FindFirstStart(cNow.Date.AddDays(1), cNow);
        }

        // Get today's end of working hours
        public DateTime GetEndToday(DateTime cNow)
        {
            return cNow.Date + mEndTime;
        }

        #endregion

        //
        // Private methods
        //
        #region Private methods

        // Scan the days starting from the given date and return the first
        // work-day start that comes after the given moment. Eight days are
        // enough to wrap around a whole week (e.g. one working day per week).
        private DateTime FindFirstStart(DateTime cFirstDate, DateTime cAfter)
        {
            for (int i = 0; i < 8; i++)
            {
                var date = cFirstDate.AddDays(i);
                if (mWorkDays.Contains(date.DayOfWeek))
                {
                    var start = date + mStartTime;
                    if (start > cAfter)
                    {
                        return start;
                    }
                }
            }
            return DateTime.MaxValue;
        }

        #endregion

        //
        // Properties
        //
        #region Properties

        // Start of working hours
        public TimeSpan StartTime
        {
            get { return mStartTime; }
        }

        // End of working hours
        public TimeSpan EndTime
        {
            get { return mEndTime; }
        }

        // Working days
        public IReadOnlyCollection<DayOfWeek> WorkDays
        {
            get { return mWorkDays; }
        }

        #endregion
    }
}
