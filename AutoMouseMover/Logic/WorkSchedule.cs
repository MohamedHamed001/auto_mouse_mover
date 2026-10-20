using System;
using System.Collections.Generic;

namespace AutoMouseMover.Logic
{
    //
    // Action requested by the work schedule
    //
    enum eScheduleAction
    {
        NONE,
        START,
        STOP,
    }

    //
    // Work schedule: tells when the mouse mover should start and stop by itself.
    // The current time is always passed in by the caller, so tests can use any time.
    //
    class WorkSchedule
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

        #endregion
    }
}
