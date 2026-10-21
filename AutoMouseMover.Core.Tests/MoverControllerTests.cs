using AutoMouseMover.Logic;

namespace AutoMouseMover.Tests
{
    public class MoverControllerTests
    {
        // Known dates: 2026-10-19 is a Monday, 2026-10-23 a Friday, 2026-10-26 the next Monday
        private static readonly DayOfWeek[] WEEKDAYS =
        {
            DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday,
        };

        // Shorthand for a moment in time
        private static DateTime T(string cText)
        {
            return DateTime.Parse(cText);
        }

        // Weekday schedule between the given hours
        private static WorkSchedule Hours(int cStartHour, int cEndHour)
        {
            return new WorkSchedule(new TimeSpan(cStartHour, 0, 0), new TimeSpan(cEndHour, 0, 0), WEEKDAYS);
        }

        // Controller launched with the 09:00-17:00 weekday schedule
        private static MoverController LaunchedNineToFive(string cNow)
        {
            var controller = new MoverController();
            controller.Launch(Hours(9, 17), null, false, T(cNow));
            return controller;
        }

        //
        // Launch
        //

        [Fact]
        public void Launch_ScheduleOff_NoAutostart_IsIdle()
        {
            var controller = new MoverController();
            controller.Launch(null, null, false, T("2026-10-19 10:00"));

            Assert.False(controller.IsRunning(T("2026-10-19 10:00")));
            var status = controller.GetStatus(T("2026-10-19 10:00"));
            Assert.Equal(eMoverState.PAUSED, status.State);
            Assert.Null(status.Until);
        }

        [Fact]
        public void Launch_ScheduleOff_Autostart_Runs()
        {
            var controller = new MoverController();
            controller.Launch(null, null, true, T("2026-10-19 10:00"));

            Assert.True(controller.IsRunning(T("2026-10-19 10:00")));
        }

        [Fact]
        public void Launch_ScheduleOn_InsideHours_Runs()
        {
            var controller = LaunchedNineToFive("2026-10-19 10:00");

            Assert.True(controller.IsRunning(T("2026-10-19 10:00")));
            var status = controller.GetStatus(T("2026-10-19 10:00"));
            Assert.Equal(eMoverState.ACTIVE, status.State);
            Assert.Equal(T("2026-10-19 17:00"), status.Until);
        }

        [Fact]
        public void Launch_WithSavedPause_StaysPaused()
        {
            // Restart on Monday 11:00 with a pause saved until Tuesday 09:00
            var controller = new MoverController();
            controller.Launch(Hours(9, 17), T("2026-10-20 09:00"), false, T("2026-10-19 11:00"));

            Assert.False(controller.IsRunning(T("2026-10-19 11:00")));

            controller.Tick(T("2026-10-20 09:00"));
            Assert.True(controller.IsRunning(T("2026-10-20 09:00")));
        }

        [Fact]
        public void Launch_WithExpiredPause_Runs()
        {
            var controller = new MoverController();
            controller.Launch(Hours(9, 17), T("2026-10-19 10:30"), false, T("2026-10-19 11:00"));

            Assert.True(controller.IsRunning(T("2026-10-19 11:00")));
            Assert.Null(controller.PausedUntil);
        }

        //
        // Pause
        //

        [Fact]
        public void Pause_ScheduleOn_UntilNextWorkDayStart()
        {
            var controller = LaunchedNineToFive("2026-10-19 10:00");

            controller.Pause(T("2026-10-19 14:00"));

            Assert.Equal(T("2026-10-20 09:00"), controller.PausedUntil);
            controller.Tick(T("2026-10-19 16:00"));
            Assert.False(controller.IsRunning(T("2026-10-19 16:00")));
        }

        [Fact]
        public void Pause_ScheduleOff_UntilResume()
        {
            var controller = new MoverController();
            controller.Launch(null, null, true, T("2026-10-19 10:00"));

            controller.Pause(T("2026-10-19 10:00"));

            Assert.Equal(DateTime.MaxValue, controller.PausedUntil);
            controller.Tick(T("2026-10-20 10:00"));
            Assert.False(controller.IsRunning(T("2026-10-20 10:00")));

            controller.Resume(T("2026-10-20 10:00"));
            Assert.True(controller.IsRunning(T("2026-10-20 10:00")));
        }

        [Fact]
        public void PauseFor_EndsInsideHours_Resumes()
        {
            var controller = LaunchedNineToFive("2026-10-19 10:00");

            controller.PauseFor(TimeSpan.FromMinutes(30), T("2026-10-19 10:00"));

            controller.Tick(T("2026-10-19 10:29"));
            Assert.False(controller.IsRunning(T("2026-10-19 10:29")));
            controller.Tick(T("2026-10-19 10:30"));
            Assert.True(controller.IsRunning(T("2026-10-19 10:30")));
        }

        [Fact]
        public void PauseFor_CrossesEndTime_StaysStopped()
        {
            // The 17:00 end happens during the pause (e.g. the PC slept until 18:00)
            var controller = LaunchedNineToFive("2026-10-19 10:00");

            controller.PauseFor(TimeSpan.FromMinutes(30), T("2026-10-19 16:45"));
            controller.Tick(T("2026-10-19 18:00"));

            Assert.False(controller.IsRunning(T("2026-10-19 18:00")));
            var status = controller.GetStatus(T("2026-10-19 18:00"));
            Assert.Equal(eMoverState.OUTSIDE_HOURS, status.State);
            Assert.Equal(T("2026-10-20 09:00"), status.Until);
        }

        [Fact]
        public void PauseFor_DuringManualRunOutsideHours_Continues()
        {
            var controller = LaunchedNineToFive("2026-10-19 20:00");
            controller.Resume(T("2026-10-19 20:00"));

            controller.PauseFor(TimeSpan.FromMinutes(30), T("2026-10-19 20:00"));
            controller.Tick(T("2026-10-19 20:30"));

            Assert.True(controller.IsRunning(T("2026-10-19 20:30")));
        }

        [Fact]
        public void PauseUntilTomorrow_BeforeStart_SkipsToday()
        {
            var controller = LaunchedNineToFive("2026-10-19 08:00");

            controller.PauseUntilTomorrow(T("2026-10-19 08:00"));

            controller.Tick(T("2026-10-19 09:00"));
            Assert.False(controller.IsRunning(T("2026-10-19 09:00")));
            controller.Tick(T("2026-10-20 09:00"));
            Assert.True(controller.IsRunning(T("2026-10-20 09:00")));
        }

        [Fact]
        public void PauseUntilTomorrow_Friday_UntilMonday()
        {
            var controller = LaunchedNineToFive("2026-10-23 10:00");

            controller.PauseUntilTomorrow(T("2026-10-23 10:00"));

            Assert.Equal(T("2026-10-26 09:00"), controller.PausedUntil);
        }

        //
        // Lock / sleep
        //

        [Fact]
        public void Locked_StopsAndUnlockResumes()
        {
            var controller = LaunchedNineToFive("2026-10-19 10:00");

            controller.SetLocked(true, T("2026-10-19 10:00"));
            Assert.False(controller.IsRunning(T("2026-10-19 10:00")));

            controller.SetLocked(false, T("2026-10-19 10:05"));
            Assert.True(controller.IsRunning(T("2026-10-19 10:05")));
        }

        [Fact]
        public void LockedThroughEndTime_UnlockStaysStopped()
        {
            var controller = LaunchedNineToFive("2026-10-19 10:00");

            controller.SetLocked(true, T("2026-10-19 16:50"));
            controller.SetLocked(false, T("2026-10-19 17:10"));

            Assert.False(controller.IsRunning(T("2026-10-19 17:10")));
        }

        [Fact]
        public void SuspendWithoutResume_UnlockResumes()
        {
            // Lock, then sleep: Windows sends Suspend but (on some laptops) never
            // Resume. The unlock proves the machine is awake again.
            var controller = LaunchedNineToFive("2026-10-19 10:00");

            controller.SetLocked(true, T("2026-10-19 10:00"));
            controller.SetSuspended(true, T("2026-10-19 10:01"));
            controller.SetLocked(false, T("2026-10-19 10:30"));

            Assert.True(controller.IsRunning(T("2026-10-19 10:30")));
        }

        [Fact]
        public void ResumeWhileStillLocked_StaysStopped()
        {
            // Waking up on the lock screen must not move the mouse
            var controller = LaunchedNineToFive("2026-10-19 10:00");

            controller.SetLocked(true, T("2026-10-19 10:00"));
            controller.SetSuspended(true, T("2026-10-19 10:01"));
            controller.SetSuspended(false, T("2026-10-19 10:30"));

            Assert.False(controller.IsRunning(T("2026-10-19 10:30")));
        }

        //
        // Schedule changes
        //

        [Fact]
        public void SetSchedule_OutsideNewHours_Stops()
        {
            var controller = LaunchedNineToFive("2026-10-19 10:00");
            controller.Tick(T("2026-10-19 16:00"));

            controller.SetSchedule(Hours(9, 15), T("2026-10-19 16:00"));

            Assert.False(controller.IsRunning(T("2026-10-19 16:00")));
        }

        [Fact]
        public void SetSchedule_Unchanged_KeepsManualRun()
        {
            // Manual run in the evening; leaving a time box without editing
            // re-sends the same schedule, which must not stop the run
            var controller = LaunchedNineToFive("2026-10-19 20:00");
            controller.Resume(T("2026-10-19 20:00"));

            controller.SetSchedule(Hours(9, 17), T("2026-10-19 20:05"));

            Assert.True(controller.IsRunning(T("2026-10-19 20:05")));
        }

        [Fact]
        public void SetSchedule_KeepsSavedPause()
        {
            var controller = LaunchedNineToFive("2026-10-19 10:00");
            controller.PauseFor(TimeSpan.FromMinutes(30), T("2026-10-19 10:00"));

            controller.SetSchedule(Hours(9, 18), T("2026-10-19 10:05"));

            Assert.Equal(T("2026-10-19 10:30"), controller.PausedUntil);
        }

        //
        // Events
        //

        [Fact]
        public void StateChanged_RaisedOnPause()
        {
            var controller = LaunchedNineToFive("2026-10-19 10:00");
            int raised = 0;
            controller.StateChanged += (sender, e) => raised++;

            controller.Pause(T("2026-10-19 10:00"));

            Assert.Equal(1, raised);
        }
    }
}
