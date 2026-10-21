using System;
using System.Collections.Generic;
using System.Globalization;

namespace AutoMouseMover.Utils
{
    //
    // Theme chosen by the user
    //
    public enum eThemeChoice
    {
        SYSTEM,     // follow the operating system light/dark setting
        LIGHT,
        DARK,
    }

    //
    // Settings text formats: converts setting values to and from the text saved
    // in the settings files. Shared by the Windows and macOS apps.
    //
    public static class SettingsFormat
    {
        //
        // Constants
        //
        #region Constants

        // Saved time format (hours and minutes, e.g. 09:00)
        private const string TIME_FORMAT = @"hh\:mm";
        // Accepted time inputs: two-digit or one-digit hours (09:00 or 9:00)
        private static readonly string[] TIME_INPUT_FORMATS = { @"hh\:mm", @"h\:mm" };
        // Saved date/time format (round-trip, e.g. 2026-10-20T09:00:00.0000000)
        private const string DATE_TIME_FORMAT = "o";
        // Saved theme names
        private const string THEME_LIGHT = "Light";
        private const string THEME_DARK = "Dark";

        #endregion

        //
        // Public methods
        //
        #region Public methods

        // Parse a saved list of day names (e.g. "Monday,Friday"), skipping unknown names
        public static List<DayOfWeek> ParseWorkDays(string cText)
        {
            var days = new List<DayOfWeek>();
            foreach (var part in cText.Split(','))
            {
                if (Enum.TryParse(part.Trim(), out DayOfWeek day) && Enum.IsDefined(day))
                {
                    days.Add(day);
                }
            }
            return days;
        }

        // Format a list of days for saving (e.g. "Monday,Friday")
        public static string FormatWorkDays(IEnumerable<DayOfWeek> cDays)
        {
            return string.Join(",", cDays);
        }

        // Parse a saved "paused until" moment (null if empty or not valid)
        public static DateTime? ParsePausedUntil(string cText)
        {
            if (DateTime.TryParseExact(cText, DATE_TIME_FORMAT, CultureInfo.InvariantCulture,
                                       DateTimeStyles.RoundtripKind, out DateTime value))
            {
                return value;
            }
            return null;
        }

        // Format a "paused until" moment for saving (empty if not paused)
        public static string FormatPausedUntil(DateTime? cValue)
        {
            return (cValue == null) ? "" : cValue.Value.ToString(DATE_TIME_FORMAT, CultureInfo.InvariantCulture);
        }

        // Parse a time of day typed or saved as "09:00" or "9:00", null if not valid
        public static TimeSpan? ParseTime(string cText)
        {
            if (TimeSpan.TryParseExact(cText, TIME_INPUT_FORMATS, CultureInfo.InvariantCulture, out TimeSpan value))
            {
                return value;
            }
            return null;
        }

        // Parse a saved theme choice ("Light", "Dark"); anything else follows Windows
        public static eThemeChoice ParseThemeChoice(string cText)
        {
            switch (cText)
            {
                case THEME_LIGHT:
                    return eThemeChoice.LIGHT;
                case THEME_DARK:
                    return eThemeChoice.DARK;
                default:
                    return eThemeChoice.SYSTEM;
            }
        }

        // Format a theme choice for saving (empty to follow Windows)
        public static string FormatThemeChoice(eThemeChoice cChoice)
        {
            switch (cChoice)
            {
                case eThemeChoice.LIGHT:
                    return THEME_LIGHT;
                case eThemeChoice.DARK:
                    return THEME_DARK;
                default:
                    return "";
            }
        }

        // Format a time of day for saving (e.g. "09:00")
        public static string FormatTime(TimeSpan cValue)
        {
            return cValue.ToString(TIME_FORMAT, CultureInfo.InvariantCulture);
        }

        #endregion
    }
}
