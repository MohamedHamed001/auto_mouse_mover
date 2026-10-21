/*
 * Copyright (c) 2020 Emanuele Bellocchia
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in
 * all copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
 * THE SOFTWARE.
 */

using System;
using System.Collections.Generic;
using System.Globalization;
using AutoMouseMover.Logic;

namespace AutoMouseMover.Utils
{
    //
    // Settings helper
    //
    class SettingsHelper
    {
        //
        // Constants
        //
        #region Constants

        // Default settings
        private const int DEF_MOVING_PERIOD = 5;
        private const int DEF_MOVING_PIXEL = 5;
        private const bool DEF_LEFT_CLICK_AFTER_MOVING = true;
        private const bool DEF_SCHEDULE_ENABLED = false;
        private static readonly TimeSpan DEF_WORK_START_TIME = new TimeSpan(9, 0, 0);
        private static readonly TimeSpan DEF_WORK_END_TIME = new TimeSpan(17, 0, 0);
        private static readonly DayOfWeek[] DEF_WORK_DAYS =
        {
            DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday,
        };

        #endregion

        //
        // Members
        //
        #region Members

        // Moving period
        private int  mMovingPeriod;
        // Moving pixel
        private int  mMovingPixel;
        // Left click after moving
        private bool mLeftClickAfterMoving;
        // Schedule enabled flag
        private bool mScheduleEnabled;
        // Start of working hours
        private TimeSpan mWorkStartTime;
        // End of working hours
        private TimeSpan mWorkEndTime;
        // Working days
        private List<DayOfWeek> mWorkDays;
        // Paused until (null if not paused)
        private DateTime? mPausedUntil;
        // Language name (e.g. "it"), empty for the system language
        private string mLanguage;
        // Theme choice
        private eThemeChoice mTheme;

        #endregion

        //
        // Public methods
        //
        #region Public methods

        // Constructor
        public SettingsHelper()
        {
            Load();
        }

        // Load settings
        public void Load()
        {
            // Settings are stored per application version: after an update, copy
            // the previous version's values once (e.g. from 1.3.0)
            if (Properties.Settings.Default.SettingsUpgradeRequired)
            {
                Properties.Settings.Default.Upgrade();
                Properties.Settings.Default.SettingsUpgradeRequired = false;
                Properties.Settings.Default.Save();
            }

            mMovingPeriod = Properties.Settings.Default.MovingPeriod;
            mMovingPixel = Properties.Settings.Default.MovingPixel;
            mLeftClickAfterMoving = Properties.Settings.Default.LeftClickAfterMoving;
            mScheduleEnabled = Properties.Settings.Default.ScheduleEnabled;
            // Damaged values fall back to the defaults instead of crashing
            mWorkStartTime = SettingsFormat.ParseTime(Properties.Settings.Default.WorkStartTime) ?? DEF_WORK_START_TIME;
            mWorkEndTime = SettingsFormat.ParseTime(Properties.Settings.Default.WorkEndTime) ?? DEF_WORK_END_TIME;
            mWorkDays = SettingsFormat.ParseWorkDays(Properties.Settings.Default.WorkDays);
            mPausedUntil = SettingsFormat.ParsePausedUntil(Properties.Settings.Default.PausedUntil);
            mLanguage = Properties.Settings.Default.Language;
            mTheme = SettingsFormat.ParseThemeChoice(Properties.Settings.Default.Theme);
        }

        // Load default settings
        public void LoadDefault()
        {
            mMovingPeriod = DEF_MOVING_PERIOD;
            mMovingPixel = DEF_MOVING_PIXEL;
            mLeftClickAfterMoving = DEF_LEFT_CLICK_AFTER_MOVING;
            mScheduleEnabled = DEF_SCHEDULE_ENABLED;
            mWorkStartTime = DEF_WORK_START_TIME;
            mWorkEndTime = DEF_WORK_END_TIME;
            mWorkDays = new List<DayOfWeek>(DEF_WORK_DAYS);
            mPausedUntil = null;
            mLanguage = "";
            mTheme = eThemeChoice.SYSTEM;
        }

        // Save settings
        public void Save()
        {
            Properties.Settings.Default.MovingPeriod = mMovingPeriod;
            Properties.Settings.Default.MovingPixel = mMovingPixel;
            Properties.Settings.Default.LeftClickAfterMoving = mLeftClickAfterMoving;
            Properties.Settings.Default.ScheduleEnabled = mScheduleEnabled;
            Properties.Settings.Default.WorkStartTime = SettingsFormat.FormatTime(mWorkStartTime);
            Properties.Settings.Default.WorkEndTime = SettingsFormat.FormatTime(mWorkEndTime);
            Properties.Settings.Default.WorkDays = SettingsFormat.FormatWorkDays(mWorkDays);
            Properties.Settings.Default.PausedUntil = SettingsFormat.FormatPausedUntil(mPausedUntil);
            Properties.Settings.Default.Language = mLanguage;
            Properties.Settings.Default.Theme = SettingsFormat.FormatThemeChoice(mTheme);
            Properties.Settings.Default.Save();
        }

        // Create the work schedule from the settings.
        // Returns null if the schedule is off or the saved values are not valid
        // (end not after start, or no working days).
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
        // Properties
        //
        #region Properties
        
        // Moving time property
        public int MovingTime
        {
            get { return mMovingPeriod; }
            set { mMovingPeriod = value; }
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

        // Theme choice property
        public eThemeChoice Theme
        {
            get { return mTheme; }
            set { mTheme = value; }
        }

        // Language property (e.g. "it"), empty for the system language
        public string Language
        {
            get { return mLanguage; }
            set { mLanguage = value; }
        }

        #endregion
    }
}
