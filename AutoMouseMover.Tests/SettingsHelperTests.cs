using AutoMouseMover.Utils;

namespace AutoMouseMover.Tests
{
    // Settings are saved as text, so these tests check the text <-> value conversions
    public class SettingsHelperTests
    {
        //
        // Work days
        //

        [Fact]
        public void ParseWorkDays_ReadsDayNames()
        {
            Assert.Equal(new[] { DayOfWeek.Monday, DayOfWeek.Friday }, SettingsHelper.ParseWorkDays("Monday,Friday"));
        }

        [Theory]
        [InlineData("Monday,,Funday")]   // empty and unknown names are skipped
        [InlineData(" Monday ")]         // spaces are trimmed
        public void ParseWorkDays_SkipsJunk(string text)
        {
            Assert.Equal(new[] { DayOfWeek.Monday }, SettingsHelper.ParseWorkDays(text));
        }

        [Fact]
        public void ParseWorkDays_Empty_IsEmpty()
        {
            Assert.Empty(SettingsHelper.ParseWorkDays(""));
        }

        [Fact]
        public void FormatWorkDays_RoundTrips()
        {
            var days = new[] { DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday };

            string text = SettingsHelper.FormatWorkDays(days);

            Assert.Equal("Monday,Wednesday,Friday", text);
            Assert.Equal(days, SettingsHelper.ParseWorkDays(text));
        }

        //
        // Paused until
        //

        [Fact]
        public void ParsePausedUntil_Empty_IsNull()
        {
            Assert.Null(SettingsHelper.ParsePausedUntil(""));
        }

        [Fact]
        public void FormatPausedUntil_Null_IsEmpty()
        {
            Assert.Equal("", SettingsHelper.FormatPausedUntil(null));
        }

        [Fact]
        public void PausedUntil_RoundTrips()
        {
            var value = DateTime.Parse("2026-10-20 09:00");

            string text = SettingsHelper.FormatPausedUntil(value);

            Assert.Equal("2026-10-20T09:00:00.0000000", text);
            Assert.Equal(value, SettingsHelper.ParsePausedUntil(text));
        }

        [Fact]
        public void PausedUntil_MaxValue_RoundTrips()
        {
            // MaxValue means "paused until Resume"
            Assert.Equal(DateTime.MaxValue,
                SettingsHelper.ParsePausedUntil(SettingsHelper.FormatPausedUntil(DateTime.MaxValue)));
        }

        [Fact]
        public void ParsePausedUntil_Garbage_IsNull()
        {
            // A damaged settings file must not crash the app or leave it paused forever
            Assert.Null(SettingsHelper.ParsePausedUntil("not a date"));
        }

        //
        // Times
        //

        [Fact]
        public void Time_RoundTrips()
        {
            Assert.Equal("09:30", SettingsHelper.FormatTime(new TimeSpan(9, 30, 0)));
            Assert.Equal(new TimeSpan(9, 30, 0), SettingsHelper.ParseTime("09:30"));
        }

        [Theory]
        [InlineData("9:30")]
        [InlineData("09:30")]
        public void ParseTime_AcceptsOneOrTwoDigitHours(string text)
        {
            Assert.Equal(new TimeSpan(9, 30, 0), SettingsHelper.ParseTime(text));
        }

        [Theory]
        [InlineData("25:00")]
        [InlineData("9:3")]
        [InlineData("930")]
        [InlineData("")]
        public void ParseTime_Invalid_IsNull(string text)
        {
            Assert.Null(SettingsHelper.ParseTime(text));
        }
    }
}
