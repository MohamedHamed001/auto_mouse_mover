using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using AutoMouseMover.Logic;
using AutoMouseMover.Utils;

namespace AutoMouseMover.Mac.Services
{
    //
    // macOS settings, saved as JSON in a folder chosen by the caller
    // (the app uses ~/Library/Application Support/AutoMouseMover).
    // Values are saved as text with the shared SettingsFormat, like on Windows.
    // A damaged file or value falls back to the default, never a crash.
    //
    class MacSettings
    {
        //
        // Constants
        //
        #region Constants

        // Settings file name
        private const string FILE_NAME = "settings.json";
        // Limits and defaults (same as Windows)
        private const int MIN_MOVING_TIME = 2;
        private const int MAX_MOVING_TIME = 30;
        private const int MIN_MOVING_PIXEL = 1;
        private const int MAX_MOVING_PIXEL = 200;
        private const int DEF_MOVING_TIME = 5;
        private const int DEF_MOVING_PIXEL = 5;
        private static readonly TimeSpan DEF_WORK_START_TIME = new TimeSpan(9, 0, 0);
        private static readonly TimeSpan DEF_WORK_END_TIME = new TimeSpan(17, 0, 0);
        private static readonly DayOfWeek[] DEF_WORK_DAYS =
        {
            DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday,
        };

        #endregion

        //
        // Saved file layout (what the JSON file contains)
        //
        #region Saved file layout

        private class SettingsData
        {
            public int? MovingTime { get; set; }
            public int? MovingPixel { get; set; }
            public bool? LeftClickAfterMoving { get; set; }
            public bool? ScheduleEnabled { get; set; }
            public string WorkStartTime { get; set; }
            public string WorkEndTime { get; set; }
            public string WorkDays { get; set; }
            public string PausedUntil { get; set; }
            public string Language { get; set; }
            public string Theme { get; set; }
        }

        #endregion

        //
        // Members
        //
        #region Members

        // Folder of the settings file
        private string mFolder;
        // Setting values
        private int mMovingTime;
        private int mMovingPixel;
        private bool mLeftClickAfterMoving;
        private bool mScheduleEnabled;
        private TimeSpan mWorkStartTime;
        private TimeSpan mWorkEndTime;
        private List<DayOfWeek> mWorkDays;
        private DateTime? mPausedUntil;
        private string mLanguage;
        private eThemeChoice mTheme;

        #endregion

        //
        // Public methods
        //
        #region Public methods

        // Constructor (starts with the defaults; call Load to read the file)
        public MacSettings(string cFolder)
        {
            mFolder = cFolder;
            LoadDefault();
        }

        // Load the settings file. Missing or unreadable file: defaults.
        // Invalid single values: default for that value only.
        public void Load()
        {
            LoadDefault();

            SettingsData data;
            try
            {
                data = JsonSerializer.Deserialize<SettingsData>(File.ReadAllText(FilePath()));
            }
            catch (Exception e) when (e is IOException || e is JsonException || e is UnauthorizedAccessException)
            {
                // Missing (FileNotFound/DirectoryNotFound are IOExceptions), damaged or unreadable
                return;
            }
            if (data == null)
            {
                return;
            }

            if (IsInRange(data.MovingTime, MIN_MOVING_TIME, MAX_MOVING_TIME))
            {
                mMovingTime = data.MovingTime.Value;
            }
            if (IsInRange(data.MovingPixel, MIN_MOVING_PIXEL, MAX_MOVING_PIXEL))
            {
                mMovingPixel = data.MovingPixel.Value;
            }
            mLeftClickAfterMoving = data.LeftClickAfterMoving ?? mLeftClickAfterMoving;
            mScheduleEnabled = data.ScheduleEnabled ?? mScheduleEnabled;
            mWorkStartTime = SettingsFormat.ParseTime(data.WorkStartTime ?? "") ?? mWorkStartTime;
            mWorkEndTime = SettingsFormat.ParseTime(data.WorkEndTime ?? "") ?? mWorkEndTime;
            if (data.WorkDays != null)
            {
                mWorkDays = SettingsFormat.ParseWorkDays(data.WorkDays);
            }
            mPausedUntil = SettingsFormat.ParsePausedUntil(data.PausedUntil ?? "");
            mLanguage = data.Language ?? "";
            mTheme = SettingsFormat.ParseThemeChoice(data.Theme ?? "");
        }

        // Load default settings
        public void LoadDefault()
        {
            mMovingTime = DEF_MOVING_TIME;
            mMovingPixel = DEF_MOVING_PIXEL;
            mLeftClickAfterMoving = false;
            mScheduleEnabled = false;
            mWorkStartTime = DEF_WORK_START_TIME;
            mWorkEndTime = DEF_WORK_END_TIME;
            mWorkDays = new List<DayOfWeek>(DEF_WORK_DAYS);
            mPausedUntil = null;
            mLanguage = "";
            mTheme = eThemeChoice.SYSTEM;
        }

        // Save the settings file (written to a temporary file first, so a crash
        // while saving never leaves a half-written file behind)
        public void Save()
        {
            var data = new SettingsData
            {
                MovingTime = mMovingTime,
                MovingPixel = mMovingPixel,
                LeftClickAfterMoving = mLeftClickAfterMoving,
                ScheduleEnabled = mScheduleEnabled,
                WorkStartTime = SettingsFormat.FormatTime(mWorkStartTime),
                WorkEndTime = SettingsFormat.FormatTime(mWorkEndTime),
                WorkDays = SettingsFormat.FormatWorkDays(mWorkDays),
                PausedUntil = SettingsFormat.FormatPausedUntil(mPausedUntil),
                Language = mLanguage,
                Theme = SettingsFormat.FormatThemeChoice(mTheme),
            };

            try
            {
                Directory.CreateDirectory(mFolder);
                string temp_path = FilePath() + ".tmp";
                File.WriteAllText(temp_path, JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
                File.Move(temp_path, FilePath(), true);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                // Disk full or no permission: keep running with the values in memory
            }
        }

        // Create the work schedule from the settings.
        // Returns null if the schedule is off or the values are not valid.
        public WorkSchedule CreateSchedule()
        {
            if (!mScheduleEnabled || (mWorkDays.Count == 0))
            {
                return null;
            }

            try
            {
                return new WorkSchedule(mWorkStartTime, mWorkEndTime, mWorkDays);
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        #endregion

        //
        // Private methods
        //
        #region Private methods

        // Full path of the settings file
        private string FilePath()
        {
            return Path.Combine(mFolder, FILE_NAME);
        }

        // Get if an optional value is present and inside the limits
        private static bool IsInRange(int? cValue, int cMin, int cMax)
        {
            return (cValue != null) && (cValue.Value >= cMin) && (cValue.Value <= cMax);
        }

        #endregion

        //
        // Properties
        //
        #region Properties

        // Moving time (seconds) property
        public int MovingTime
        {
            get { return mMovingTime; }
            set { mMovingTime = value; }
        }

        // Moving pixel property
        public int MovingPixel
        {
            get { return mMovingPixel; }
            set { mMovingPixel = value; }
        }

        // Left click after moving property
        public bool LeftClickAfterMoving
        {
            get { return mLeftClickAfterMoving; }
            set { mLeftClickAfterMoving = value; }
        }

        // Schedule enabled property
        public bool ScheduleEnabled
        {
            get { return mScheduleEnabled; }
            set { mScheduleEnabled = value; }
        }

        // Start of working hours property
        public TimeSpan WorkStartTime
        {
            get { return mWorkStartTime; }
            set { mWorkStartTime = value; }
        }

        // End of working hours property
        public TimeSpan WorkEndTime
        {
            get { return mWorkEndTime; }
            set { mWorkEndTime = value; }
        }

        // Working days property
        public List<DayOfWeek> WorkDays
        {
            get { return mWorkDays; }
            set { mWorkDays = value; }
        }

        // Paused until property (null if not paused)
        public DateTime? PausedUntil
        {
            get { return mPausedUntil; }
            set { mPausedUntil = value; }
        }

        // Language property (e.g. "it"), empty for the system language
        public string Language
        {
            get { return mLanguage; }
            set { mLanguage = value; }
        }

        // Theme choice property
        public eThemeChoice Theme
        {
            get { return mTheme; }
            set { mTheme = value; }
        }

        #endregion
    }
}
