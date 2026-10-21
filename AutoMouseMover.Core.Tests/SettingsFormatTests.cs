using AutoMouseMover.Utils;

namespace AutoMouseMover.Tests
{
    // Settings are saved as text, so these tests check the text <-> value conversions
    public class SettingsFormatTests
    {
        //
        // Work days
        //

        [Fact]
        public void ParseWorkDays_ReadsDayNames()
        {
            Assert.Equal(new[] { DayOfWeek.Monday, DayOfWeek.Friday }, SettingsFormat.ParseWorkDays("Monday,Friday"));
        }

        [Theory]
        [InlineData("Monday,,Funday")]   // empty and unknown names are skipped
        [InlineData(" Monday ")]         // spaces are trimmed
        public void ParseWorkDays_SkipsJunk(string text)
        {
            Assert.Equal(new[] { DayOfWeek.Monday }, SettingsFormat.ParseWorkDays(text));
        }

        [Fact]
        public void ParseWorkDays_Empty_IsEmpty()
        {
            Assert.Empty(SettingsFormat.ParseWorkDays(""));
        }

        [Fact]
        public void FormatWorkDays_RoundTrips()
        {
            var days = new[] { DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday };

            string text = SettingsFormat.FormatWorkDays(days);

            Assert.Equal("Monday,Wednesday,Friday", text);
            Assert.Equal(days, SettingsFormat.ParseWorkDays(text));
        }

        //
        // Paused until
        //

        [Fact]
        public void ParsePausedUntil_Empty_IsNull()
        {
            Assert.Null(SettingsFormat.ParsePausedUntil(""));
        }

        [Fact]
        public void FormatPausedUntil_Null_IsEmpty()
        {
            Assert.Equal("", SettingsFormat.FormatPausedUntil(null));
        }

        [Fact]
        public void PausedUntil_RoundTrips()
        {
            var value = DateTime.Parse("2026-10-20 09:00");

            string text = SettingsFormat.FormatPausedUntil(value);

            Assert.Equal("2026-10-20T09:00:00.0000000", text);
            Assert.Equal(value, SettingsFormat.ParsePausedUntil(text));
        }

        [Fact]
        public void PausedUntil_MaxValue_RoundTrips()
        {
            // MaxValue means "paused until Resume"
            Assert.Equal(DateTime.MaxValue,
                SettingsFormat.ParsePausedUntil(SettingsFormat.FormatPausedUntil(DateTime.MaxValue)));
        }

        [Fact]
        public void ParsePausedUntil_Garbage_IsNull()
        {
            // A damaged settings file must not crash the app or leave it paused forever
            Assert.Null(SettingsFormat.ParsePausedUntil("not a date"));
        }

        //
        // Theme
        //

        [Theory]
        [InlineData("", eThemeChoice.SYSTEM)]
        [InlineData("Light", eThemeChoice.LIGHT)]
        [InlineData("Dark", eThemeChoice.DARK)]
        [InlineData("Purple", eThemeChoice.SYSTEM)]  // unknown values fall back to the system theme
        public void ParseThemeChoice_ReadsSavedText(string text, eThemeChoice expected)
        {
            Assert.Equal(expected, SettingsFormat.ParseThemeChoice(text));
        }

        [Theory]
        [InlineData(eThemeChoice.SYSTEM)]
        [InlineData(eThemeChoice.LIGHT)]
        [InlineData(eThemeChoice.DARK)]
        public void ThemeChoice_RoundTrips(eThemeChoice choice)
        {
            Assert.Equal(choice, SettingsFormat.ParseThemeChoice(SettingsFormat.FormatThemeChoice(choice)));
        }

        [Fact]
        public void FormatThemeChoice_System_IsEmpty()
        {
            // Empty means "follow Windows", like the Language setting
            Assert.Equal("", SettingsFormat.FormatThemeChoice(eThemeChoice.SYSTEM));
        }

        //
        // Times
        //

        [Fact]
        public void Time_RoundTrips()
        {
            Assert.Equal("09:30", SettingsFormat.FormatTime(new TimeSpan(9, 30, 0)));
            Assert.Equal(new TimeSpan(9, 30, 0), SettingsFormat.ParseTime("09:30"));
        }

        [Theory]
        [InlineData("9:30")]
        [InlineData("09:30")]
        public void ParseTime_AcceptsOneOrTwoDigitHours(string text)
        {
            Assert.Equal(new TimeSpan(9, 30, 0), SettingsFormat.ParseTime(text));
        }

        [Theory]
        [InlineData("25:00")]
        [InlineData("9:3")]
        [InlineData("930")]
        [InlineData("")]
        public void ParseTime_Invalid_IsNull(string text)
        {
            Assert.Null(SettingsFormat.ParseTime(text));
        }
    }
}
