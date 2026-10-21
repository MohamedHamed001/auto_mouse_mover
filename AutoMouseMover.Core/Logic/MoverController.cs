using System;
using System.Collections.Generic;

namespace AutoMouseMover.Logic
{
    //
    // State shown to the user
    //
    public enum eMoverState
    {
        ACTIVE,
        PAUSED,
        OUTSIDE_HOURS,
    }

    //
    // What the status card shows: a state and, when known, a time
    // ("until 17:00", "starts Tue 09:00")
    //
    public class MoverStatus
    {
        //
        // Members
        //
        #region Members

        // State
        private eMoverState mState;
        // Time to show, null if none
        private DateTime? mUntil;

        #endregion

        //
        // Public methods
        //
        #region Public methods

        // Constructor
        public MoverStatus(eMoverState cState, DateTime? cUntil)
        {
            mState = cState;
            mUntil = cUntil;
        }

        #endregion

        //
        // Properties
        //
        #region Properties

        // State property
        public eMoverState State
        {
            get { return mState; }
        }

        // Until property
        public DateTime? Until
        {
            get { return mUntil; }
        }

        #endregion
    }

    //
    // Mover controller: decides whether the mouse should be moving right now.
    // It keeps two things apart:
    //   - whether the app WANTS to run (set by schedule crossings, Resume and --autostart)
    //   - an optional "paused until" time laid on top (saved, so it survives a restart)
    // The mouse moves only when it wants to run, is not paused and the session is not locked.
    // The current time is always passed in by the caller, so tests can use any time.
    //
    public class MoverController
    {
        //
        // Members
        //
        #region Members

        // Work schedule (null if the schedule is off)
        private WorkSchedule mSchedule;
        // Whether the app wants to run, ignoring pauses
        private bool mWantRunning;
        // Paused until this moment (null if not paused, DateTime.MaxValue until Resume)
        private DateTime? mPausedUntil;
        // Session locked
        private bool mLocked;
        // System suspended (asleep)
        private bool mSuspended;

        #endregion

        //
        // Events
        //
        #region Events

        // Raised whenever the running state, the pause or the schedule may have changed
        public event EventHandler StateChanged;

        #endregion

        //
        // Public methods
        //
        #region Public methods

        // Initialize the state when the application starts
        public void Launch(WorkSchedule cSchedule, DateTime? cSavedPausedUntil, bool cAutoStart, DateTime cNow)
        {
            mSchedule = cSchedule;
            mPausedUntil = cSavedPausedUntil;
            mLocked = false;
            // Without a schedule, --autostart decides. With a schedule, the first
            // check below starts it if we are inside working hours.
            mWantRunning = (cSchedule == null) && cAutoStart;

            ApplyTime(cNow);
            RaiseStateChanged();
        }

        // Replace the schedule (null to turn it off) and apply it immediately
        public void SetSchedule(WorkSchedule cSchedule, DateTime cNow)
        {
            // Same hours and days as now: nothing to apply. Re-applying would
            // stop a manual run outside work hours for no reason.
            if (IsSameSchedule(mSchedule, cSchedule))
            {
                return;
            }

            mSchedule = cSchedule;
            if (mSchedule != null)
            {
                // Prime the new schedule, so this first check is not seen as a
                // crossing on the next tick, then apply it to the current time
                mSchedule.CheckTransition(cNow);
                mWantRunning = mSchedule.IsWorkingTime(cNow);
            }
            RaiseStateChanged();
        }

        // Resume running (clears any pause)
        public void Resume(DateTime cNow)
        {
            mPausedUntil = null;
            mWantRunning = true;
            RaiseStateChanged();
        }

        // Pause until the next work-day start, or until Resume if the schedule is off
        public void Pause(DateTime cNow)
        {
            mPausedUntil = (mSchedule == null) ? DateTime.MaxValue : mSchedule.GetNextStart(cNow);
            RaiseStateChanged();
        }

        // Pause for the given duration
        public void PauseFor(TimeSpan cDuration, DateTime cNow)
        {
            mPausedUntil = cNow + cDuration;
            RaiseStateChanged();
        }

        // Pause until the first work-day start on a later date
        public void PauseUntilTomorrow(DateTime cNow)
        {
            if (mSchedule == null)
            {
                throw new InvalidOperationException("Pause until tomorrow needs a schedule");
            }

            mPausedUntil = mSchedule.GetStartOnNextWorkDay(cNow);
            RaiseStateChanged();
        }

        // Periodic check: apply schedule crossings and end expired pauses
        public void Tick(DateTime cNow)
        {
            // While locked nothing is checked: crossings and expired pauses that
            // happened in the meantime are applied on unlock
            if (IsStill())
            {
                return;
            }

            bool was_wanting = mWantRunning;
            DateTime? was_paused_until = mPausedUntil;

            ApplyTime(cNow);

            if ((was_wanting != mWantRunning) || (was_paused_until != mPausedUntil))
            {
                RaiseStateChanged();
            }
        }

        // Session locked/unlocked
        public void SetLocked(bool cLocked, DateTime cNow)
        {
            mLocked = cLocked;
            if (!cLocked)
            {
                // An unlock proves the machine is awake: Windows does not always
                // send Resume after Suspend (e.g. Modern Standby laptops)
                mSuspended = false;
            }
            ApplyIfAwake(cNow);
            RaiseStateChanged();
        }

        // System suspended/resumed
        public void SetSuspended(bool cSuspended, DateTime cNow)
        {
            mSuspended = cSuspended;
            ApplyIfAwake(cNow);
            RaiseStateChanged();
        }

        // Get if the mouse should be moving at the given moment
        public bool IsRunning(DateTime cNow)
        {
            return mWantRunning && !IsPaused(cNow) && !IsStill();
        }

        // Get what the status card should show
        public MoverStatus GetStatus(DateTime cNow)
        {
            if (IsPaused(cNow))
            {
                DateTime? until = (mPausedUntil == DateTime.MaxValue) ? null : mPausedUntil;
                return new MoverStatus(eMoverState.PAUSED, until);
            }

            if (mWantRunning)
            {
                // "Until" is only known when running inside working hours
                if ((mSchedule != null) && mSchedule.IsWorkingTime(cNow))
                {
                    return new MoverStatus(eMoverState.ACTIVE, mSchedule.GetEndToday(cNow));
                }
                return new MoverStatus(eMoverState.ACTIVE, null);
            }

            if (mSchedule != null)
            {
                DateTime next_start = mSchedule.GetNextStart(cNow);
                DateTime? until = (next_start == DateTime.MaxValue) ? null : next_start;
                return new MoverStatus(eMoverState.OUTSIDE_HOURS, until);
            }

            return new MoverStatus(eMoverState.PAUSED, null);
        }

        #endregion

        //
        // Private methods
        //
        #region Private methods

        // Apply schedule crossings and drop a pause that has ended
        private void ApplyTime(DateTime cNow)
        {
            if (mSchedule != null)
            {
                switch (mSchedule.CheckTransition(cNow))
                {
                    case eScheduleAction.START:
                        mWantRunning = true;
                        break;
                    case eScheduleAction.STOP:
                        mWantRunning = false;
                        break;
                }
            }

            if ((mPausedUntil != null) && (cNow >= mPausedUntil))
            {
                mPausedUntil = null;
            }
        }

        // Get if two schedules have the same hours and days (two nulls are the same: both off)
        private static bool IsSameSchedule(WorkSchedule cFirst, WorkSchedule cSecond)
        {
            if ((cFirst == null) || (cSecond == null))
            {
                return (cFirst == null) && (cSecond == null);
            }

            return (cFirst.StartTime == cSecond.StartTime) &&
                   (cFirst.EndTime == cSecond.EndTime) &&
                   new HashSet<DayOfWeek>(cFirst.WorkDays).SetEquals(cSecond.WorkDays);
        }

        // Get if the mouse must stay still (session locked or system asleep)
        private bool IsStill()
        {
            return mLocked || mSuspended;
        }

        // Once neither locked nor asleep, apply what happened in the meantime
        private void ApplyIfAwake(DateTime cNow)
        {
            if (!IsStill())
            {
                ApplyTime(cNow);
            }
        }

        // Get if paused at the given moment
        private bool IsPaused(DateTime cNow)
        {
            return (mPausedUntil != null) && (cNow < mPausedUntil);
        }

        // Raise the state changed event
        private void RaiseStateChanged()
        {
            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        #endregion

        //
        // Properties
        //
        #region Properties

        // Work schedule (null if off)
        public WorkSchedule Schedule
        {
            get { return mSchedule; }
        }

        // Paused until (null if not paused, DateTime.MaxValue until Resume)
        public DateTime? PausedUntil
        {
            get { return mPausedUntil; }
        }

        #endregion
    }
}
