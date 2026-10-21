using AutoMouseMover.Logic;

namespace AutoMouseMover.Tests
{
    public class WorkScheduleTests
    {
        // Known dates: 2026-10-19 is a Monday, 2026-10-23 a Friday, 2026-10-24 a Saturday
        private static readonly DayOfWeek[] WEEKDAYS =
        {
            DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday,
        };

        // 09:00-17:00, Monday to Friday
        private static WorkSchedule NineToFive()
        {
            return new WorkSchedule(new TimeSpan(9, 0, 0), new TimeSpan(17, 0, 0), WEEKDAYS);
        }

        //
        // IsWorkingTime
        //

        [Theory]
        [InlineData("2026-10-19 09:00", true)]   // start time is included
        [InlineData("2026-10-19 12:30", true)]
        [InlineData("2026-10-19 16:59", true)]
        [InlineData("2026-10-19 08:59", false)]  // before start
        [InlineData("2026-10-19 17:00", false)]  // end time is excluded
        [InlineData("2026-10-24 12:00", false)]  // Saturday
        public void IsWorkingTime_ChecksHoursAndDays(string now, bool expected)
        {
            Assert.Equal(expected, NineToFive().IsWorkingTime(DateTime.Parse(now)));
        }

        [Fact]
        public void Constructor_RejectsEndNotAfterStart()
        {
            Assert.Throws<ArgumentException>(
                () => new WorkSchedule(new TimeSpan(17, 0, 0), new TimeSpan(9, 0, 0), WEEKDAYS));
        }

        //
        // CheckTransition
        //

        [Fact]
        public void OpenedDuringHours_StartsOnce()
        {
            var schedule = NineToFive();

            Assert.Equal(eScheduleAction.START, schedule.CheckTransition(DateTime.Parse("2026-10-19 10:00")));
            // Later checks on the same day must not start again, otherwise a manual Stop would be undone
            Assert.Equal(eScheduleAction.NONE, schedule.CheckTransition(DateTime.Parse("2026-10-19 10:01")));
            Assert.Equal(eScheduleAction.NONE, schedule.CheckTransition(DateTime.Parse("2026-10-19 14:00")));
        }

        [Fact]
        public void OpenedOutsideHours_DoesNothing()
        {
            Assert.Equal(eScheduleAction.NONE, NineToFive().CheckTransition(DateTime.Parse("2026-10-19 20:00")));
        }

        [Fact]
        public void NormalDay_StartsAtStartAndStopsAtEnd()
        {
            var schedule = NineToFive();

            Assert.Equal(eScheduleAction.NONE, schedule.CheckTransition(DateTime.Parse("2026-10-19 08:59")));
            Assert.Equal(eScheduleAction.START, schedule.CheckTransition(DateTime.Parse("2026-10-19 09:00")));
            Assert.Equal(eScheduleAction.STOP, schedule.CheckTransition(DateTime.Parse("2026-10-19 17:00")));
            // Still outside hours: stop only once
            Assert.Equal(eScheduleAction.NONE, schedule.CheckTransition(DateTime.Parse("2026-10-19 17:01")));
            // Next morning starts again
            Assert.Equal(eScheduleAction.START, schedule.CheckTransition(DateTime.Parse("2026-10-20 09:00")));
        }

        [Fact]
        public void SleptOverWeekend_StartsOnMonday()
        {
            var schedule = NineToFive();

            // Last check on Friday afternoon, next one on Monday morning: both
            // crossings happened while the PC was asleep
            schedule.CheckTransition(DateTime.Parse("2026-10-23 16:00"));
            Assert.Equal(eScheduleAction.START, schedule.CheckTransition(DateTime.Parse("2026-10-26 10:00")));
        }

        [Fact]
        public void SleptPastEndTime_StopsOnWake()
        {
            var schedule = NineToFive();

            schedule.CheckTransition(DateTime.Parse("2026-10-19 16:00"));
            Assert.Equal(eScheduleAction.STOP, schedule.CheckTransition(DateTime.Parse("2026-10-19 19:00")));
        }

        //
        // Next start / end time
        //

        [Theory]
        [InlineData("2026-10-19 08:00", "2026-10-19 09:00")]  // before today's start
        [InlineData("2026-10-19 09:00", "2026-10-20 09:00")]  // strictly after: today's start already reached
        [InlineData("2026-10-19 14:00", "2026-10-20 09:00")]
        [InlineData("2026-10-23 18:00", "2026-10-26 09:00")]  // Friday evening -> Monday
        public void GetNextStart_FindsFirstStartAfter(string after, string expected)
        {
            Assert.Equal(DateTime.Parse(expected), NineToFive().GetNextStart(DateTime.Parse(after)));
        }

        [Theory]
        [InlineData("2026-10-19 08:00", "2026-10-20 09:00")]  // skips today even before its start
        [InlineData("2026-10-23 10:00", "2026-10-26 09:00")]  // Friday -> Monday
        public void GetStartOnNextWorkDay_SkipsToday(string now, string expected)
        {
            Assert.Equal(DateTime.Parse(expected), NineToFive().GetStartOnNextWorkDay(DateTime.Parse(now)));
        }

        [Fact]
        public void GetNextStart_NoWorkDays_ReturnsMaxValue()
        {
            var schedule = new WorkSchedule(new TimeSpan(9, 0, 0), new TimeSpan(17, 0, 0), Array.Empty<DayOfWeek>());

            Assert.Equal(DateTime.MaxValue, schedule.GetNextStart(DateTime.Parse("2026-10-19 08:00")));
        }

        [Fact]
        public void GetEndToday_IsTodayAtEndTime()
        {
            Assert.Equal(DateTime.Parse("2026-10-19 17:00"), NineToFive().GetEndToday(DateTime.Parse("2026-10-19 10:00")));
        }
    }
}
