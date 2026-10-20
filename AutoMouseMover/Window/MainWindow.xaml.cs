using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Resources;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using AutoMouseMover.Logic;
using AutoMouseMover.Utils;
using Microsoft.Win32;
using Wpf.Ui.Appearance;

namespace AutoMouseMover
{
    //
    // Main window: shows the status card and the settings, and owns the tray icon.
    // All the rules about when to move live in MoverController: this class only
    // displays its status and forwards clicks, timer ticks and Windows events to it.
    //
    public partial class MainWindow : Wpf.Ui.Controls.FluentWindow
    {
        //
        // Constants
        //
        #region Constants

        // How often the schedule and pauses are checked
        private static readonly TimeSpan SCHEDULE_CHECK_INTERVAL = TimeSpan.FromSeconds(30);
        // Length of the tray "pause for" item
        private static readonly TimeSpan SHORT_PAUSE = TimeSpan.FromMinutes(30);
        // Embedded language resource names
        private const string RES_NAME_PREFIX = "AutoMouseMover.Properties.lang_";
        private const string RES_NAME_SUFFIX = ".resources";
        // Language to be used when the system one is not available
        private const string DEFAULT_LANGUAGE = "en";
        // Days in the order they are shown (Monday first)
        private static readonly DayOfWeek[] DAYS_ORDER =
        {
            DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday,
            DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday,
        };
        // Status colors (readable on both light and dark themes)
        private static readonly Color ACTIVE_COLOR = Color.FromRgb(0x22, 0xC5, 0x5E);
        private static readonly Color IDLE_COLOR = Color.FromRgb(0x9C, 0xA3, 0xAF);

        #endregion

        //
        // Members
        //
        #region Members

        // State rules
        private MoverController mController;
        // Mouse mover
        private AutomaticMouseMover mMover;
        // Settings
        private SettingsHelper mSettings;
        // Start with Windows helper
        private StartupHelper mStartup;
        // Resource manager for the current language
        private ResourceManager mResourceMng;
        // Timer that moves the mouse
        private DispatcherTimer mMouseTimer;
        // Timer that checks the schedule and pauses
        private DispatcherTimer mScheduleTimer;
        // Timer that waits to tell a tray single click from a double click
        private DispatcherTimer mTrayClickTimer;
        // Tray icon (WinForms: it works even while no WPF window is shown)
        private System.Windows.Forms.NotifyIcon mTrayIcon;
        // Tray popup menu
        private ContextMenu mTrayMenu;
        // Day toggle buttons, by day
        private Dictionary<DayOfWeek, ToggleButton> mDayButtons;
        // Whether the mouse was moving at the last mouse tick
        private bool mWasRunning;
        // True while controls are being filled from settings (ignore their events)
        private bool mFillingControls;
        // True once the application is closing
        private bool mExiting;

        #endregion

        //
        // Native methods
        //
        #region Native methods

        // Bring a window to the foreground (needed so the tray menu closes when clicking elsewhere)
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        #endregion

        //
        // Constructor
        //
        #region Constructor

        // Constructor
        public MainWindow(bool cAutoStart)
        {
            mSettings = new SettingsHelper();
            ApplySavedLanguage();
            InitializeResource();

            InitializeComponent();
            ApplySystemTheme();

            mMover = new AutomaticMouseMover();
            mMover.Initialize(mSettings.MovingPixel, mSettings.LeftClickAfterMoving);
            mStartup = new StartupHelper();

            mController = new MoverController();
            mController.Launch(mSettings.CreateSchedule(), mSettings.PausedUntil, cAutoStart, DateTime.Now);
            mController.StateChanged += Controller_StateChanged;

            CreateDayButtons();
            FillControls();
            ApplyTexts();
            CreateTrayIcon();
            CreateTimers();
            RefreshStatus();

            // React to sleep/resume, lock/unlock and monitor changes
            SystemEvents.PowerModeChanged += OnPowerModeChanged;
            SystemEvents.SessionSwitch += OnSessionSwitch;
            SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        }

        #endregion

        //
        // GUI events
        //
        #region GUI events

        // Pause / Resume button
        private void PauseResumeButton_Click(object sender, RoutedEventArgs e)
        {
            if (mController.GetStatus(DateTime.Now).State == eMoverState.ACTIVE)
            {
                mController.Pause(DateTime.Now);
            }
            else
            {
                mController.Resume(DateTime.Now);
            }
        }

        // Schedule on/off
        private void ScheduleToggle_Click(object sender, RoutedEventArgs e)
        {
            ApplyScheduleControls();
        }

        // Time box left: validate and apply
        private void TimeBox_LostFocus(object sender, RoutedEventArgs e)
        {
            ApplyScheduleControls();
        }

        // Enter in a time box: apply without leaving the box
        private void TimeBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                ApplyScheduleControls();
            }
        }

        // Day toggled
        private void DayButton_Click(object sender, RoutedEventArgs e)
        {
            ApplyScheduleControls();
        }

        // Start with Windows on/off
        private void StartWithWindowsToggle_Click(object sender, RoutedEventArgs e)
        {
            if (StartWithWindowsToggle.IsChecked == true)
            {
                mStartup.Enable(Environment.ProcessPath);
            }
            else
            {
                mStartup.Disable();
            }
        }

        // Moving interval changed
        private void IntervalBox_ValueChanged(object sender, RoutedEventArgs e)
        {
            if (mFillingControls || (IntervalBox.Value == null))
            {
                return;
            }

            mSettings.MovingTime = (int)IntervalBox.Value.Value;
            mSettings.Save();
            mMouseTimer.Interval = TimeSpan.FromSeconds(mSettings.MovingTime);
        }

        // Moving distance changed
        private void PixelBox_ValueChanged(object sender, RoutedEventArgs e)
        {
            if (mFillingControls || (PixelBox.Value == null))
            {
                return;
            }

            mSettings.MovingPixel = (int)PixelBox.Value.Value;
            mSettings.Save();
            mMover.Initialize(mSettings.MovingPixel, mSettings.LeftClickAfterMoving);
        }

        // Click after moving on/off
        private void ClickToggle_Click(object sender, RoutedEventArgs e)
        {
            mSettings.LeftClickAfterMoving = (ClickToggle.IsChecked == true);
            mSettings.Save();
            mMover.Initialize(mSettings.MovingPixel, mSettings.LeftClickAfterMoving);
        }

        // Language button: show the language/about menu
        private void LanguageButton_Click(object sender, RoutedEventArgs e)
        {
            var menu = new ContextMenu();
            menu.Items.Add(CreateMenuItem(GetText("MenuEnglish"), () => SetLanguage("en")));
            menu.Items.Add(CreateMenuItem(GetText("MenuItalian"), () => SetLanguage("it")));
            menu.Items.Add(new Separator());
            menu.Items.Add(CreateMenuItem(GetText("MenuAbout"), () => new AboutForm().ShowDialog()));
            menu.PlacementTarget = LanguageButton;
            menu.Placement = PlacementMode.Bottom;
            menu.IsOpen = true;
        }

        // Window minimized: hide it to the tray
        private void Window_StateChanged(object sender, EventArgs e)
        {
            if (WindowState == WindowState.Minimized)
            {
                Hide();
            }
        }

        // Window closing (title bar X): exit the application
        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (!mExiting)
            {
                ExitApplication();
            }
        }

        // Tray icon clicked
        private void TrayIcon_MouseClick(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            if (e.Button == System.Windows.Forms.MouseButtons.Right)
            {
                ShowTrayMenu();
            }
            else if (e.Button == System.Windows.Forms.MouseButtons.Left)
            {
                // Wait: if a second click comes it is a double click (open the window)
                mTrayClickTimer.Start();
            }
        }

        // Tray icon double clicked: open the window
        private void TrayIcon_MouseDoubleClick(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            mTrayClickTimer.Stop();
            ShowMainWindow();
        }

        // No second click came: it was a single click, show the menu
        private void TrayClickTimer_Tick(object sender, EventArgs e)
        {
            mTrayClickTimer.Stop();
            ShowTrayMenu();
        }

        // Tray menu opened: bring it to the foreground so it closes when clicking elsewhere
        private void TrayMenu_Opened(object sender, RoutedEventArgs e)
        {
            if (PresentationSource.FromVisual(mTrayMenu) is HwndSource source)
            {
                SetForegroundWindow(source.Handle);
            }
        }

        #endregion

        //
        // Other events
        //
        #region Other events

        // Controller state changed: save the pause and redraw
        private void Controller_StateChanged(object sender, EventArgs e)
        {
            mSettings.PausedUntil = mController.PausedUntil;
            mSettings.Save();
            RefreshStatus();
        }

        // Mouse timer elapsed
        private void MouseTimer_Tick(object sender, EventArgs e)
        {
            // Apply schedule crossings and expired pauses first, so the mouse never
            // moves past the end time while waiting for the 30 s schedule check
            DateTime now = DateTime.Now;
            mController.Tick(now);
            bool is_running = mController.IsRunning(now);

            if (is_running && !mWasRunning)
            {
                // Just started: take the current cursor position as the baseline
                // instead of moving right away
                mMover.Initialize(mSettings.MovingPixel, mSettings.LeftClickAfterMoving);
            }
            else if (is_running)
            {
                mMover.MoveMouse();
            }

            mWasRunning = is_running;
        }

        // Schedule timer elapsed
        private void ScheduleTimer_Tick(object sender, EventArgs e)
        {
            mController.Tick(DateTime.Now);
            // Redraw anyway: e.g. "until" may now refer to another day
            RefreshStatus();
        }

        // System power mode changed (system going to sleep or waking up)
        private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
        {
            // Logged from the SystemEvents thread on purpose: the write is synchronous,
            // so the entry survives even if the machine suspends right after
            CrashLogger.Log($"PowerModeChanged: {e.Mode}");
            RunOnUiThread(() =>
            {
                if (e.Mode == PowerModes.Suspend)
                {
                    mController.SetSuspended(true, DateTime.Now);
                }
                else if (e.Mode == PowerModes.Resume)
                {
                    // Monitors may have been docked/undocked while asleep
                    mMover.Initialize(mSettings.MovingPixel, mSettings.LeftClickAfterMoving);
                    mController.SetSuspended(false, DateTime.Now);
                }
            });
        }

        // Session locked/unlocked (e.g. Win+L). On Modern Standby laptops this is a
        // reliable signal where PowerModeChanged often is not.
        private void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
        {
            CrashLogger.Log($"SessionSwitch: {e.Reason}");
            RunOnUiThread(() =>
            {
                if (e.Reason == SessionSwitchReason.SessionLock)
                {
                    mController.SetLocked(true, DateTime.Now);
                }
                else if (e.Reason == SessionSwitchReason.SessionUnlock)
                {
                    mMover.Initialize(mSettings.MovingPixel, mSettings.LeftClickAfterMoving);
                    mController.SetLocked(false, DateTime.Now);
                }
            });
        }

        // Display settings changed (monitor docked/undocked, resolution or DPI change)
        private void OnDisplaySettingsChanged(object sender, EventArgs e)
        {
            CrashLogger.Log($"DisplaySettingsChanged: {CrashLogger.DescribeDisplays()}");
            RunOnUiThread(() =>
            {
                // Drop the cached cursor position: it may refer to a monitor that is gone
                mMover.Initialize(mSettings.MovingPixel, mSettings.LeftClickAfterMoving);
            });
        }

        #endregion

        //
        // Private methods
        //
        #region Private methods

        // Run an action on the UI thread.
        // SystemEvents handlers are raised on a background thread, so no WPF
        // control or timer may be touched directly from them.
        private void RunOnUiThread(Action cAction)
        {
            if (mExiting)
            {
                return;
            }
            Dispatcher.BeginInvoke(cAction);
        }

        // Follow the Windows light/dark theme, now and when it changes
        private void ApplySystemTheme()
        {
            var theme = (ApplicationThemeManager.GetSystemTheme() == SystemTheme.Dark)
                ? ApplicationTheme.Dark
                : ApplicationTheme.Light;
            ApplicationThemeManager.Apply(theme, Wpf.Ui.Controls.WindowBackdropType.Mica, true);
            SystemThemeWatcher.Watch(this);
        }

        // Create the timers
        private void CreateTimers()
        {
            mMouseTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(mSettings.MovingTime) };
            mMouseTimer.Tick += MouseTimer_Tick;
            mMouseTimer.Start();

            mScheduleTimer = new DispatcherTimer { Interval = SCHEDULE_CHECK_INTERVAL };
            mScheduleTimer.Tick += ScheduleTimer_Tick;
            mScheduleTimer.Start();

            mTrayClickTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(System.Windows.Forms.SystemInformation.DoubleClickTime),
            };
            mTrayClickTimer.Tick += TrayClickTimer_Tick;
        }

        // Create the tray icon (always visible while the application runs)
        private void CreateTrayIcon()
        {
            mTrayIcon = new System.Windows.Forms.NotifyIcon
            {
                Icon = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath),
                Visible = true,
            };
            mTrayIcon.MouseClick += TrayIcon_MouseClick;
            mTrayIcon.MouseDoubleClick += TrayIcon_MouseDoubleClick;

            mTrayMenu = new ContextMenu { Placement = PlacementMode.MousePoint };
            mTrayMenu.Opened += TrayMenu_Opened;
        }

        // Rebuild the tray menu for the current state and show it
        private void ShowTrayMenu()
        {
            var status = mController.GetStatus(DateTime.Now);

            mTrayMenu.Items.Clear();
            mTrayMenu.Items.Add(new MenuItem { Header = GetStatusLine(status), IsEnabled = false });
            mTrayMenu.Items.Add(new Separator());
            if (status.State == eMoverState.ACTIVE)
            {
                mTrayMenu.Items.Add(CreateMenuItem(GetText("TrayPause30"),
                                                   () => mController.PauseFor(SHORT_PAUSE, DateTime.Now)));
                if (mController.Schedule != null)
                {
                    mTrayMenu.Items.Add(CreateMenuItem(GetText("TrayPauseTomorrow"),
                                                       () => mController.PauseUntilTomorrow(DateTime.Now)));
                }
            }
            else
            {
                mTrayMenu.Items.Add(CreateMenuItem(GetText("TrayResume"), () => mController.Resume(DateTime.Now)));
            }
            mTrayMenu.Items.Add(new Separator());
            mTrayMenu.Items.Add(CreateMenuItem(GetText("TrayOpen"), ShowMainWindow));
            mTrayMenu.Items.Add(CreateMenuItem(GetText("TrayClose"), ExitApplication));

            mTrayMenu.IsOpen = true;
        }

        // Create a menu item that runs an action when clicked
        private static MenuItem CreateMenuItem(string cHeader, Action cAction)
        {
            var item = new MenuItem { Header = cHeader };
            item.Click += (sender, e) => cAction();
            return item;
        }

        // Show the main window (from the tray, or when the exe is opened again)
        public void ShowMainWindow()
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();
        }

        // Save, clean up and exit
        private void ExitApplication()
        {
            if (mExiting)
            {
                return;
            }
            mExiting = true;

            // SystemEvents are static and would otherwise keep this window alive
            SystemEvents.PowerModeChanged -= OnPowerModeChanged;
            SystemEvents.SessionSwitch -= OnSessionSwitch;
            SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;

            mMouseTimer.Stop();
            mScheduleTimer.Stop();
            mTrayClickTimer.Stop();
            mSettings.Save();

            // Remove the icon right away, otherwise it stays in the tray until hovered
            mTrayIcon.Visible = false;
            mTrayIcon.Dispose();

            Application.Current.Shutdown();
        }

        // Create the seven day toggle buttons
        private void CreateDayButtons()
        {
            mDayButtons = new Dictionary<DayOfWeek, ToggleButton>();
            foreach (var day in DAYS_ORDER)
            {
                var button = new ToggleButton
                {
                    Margin = new Thickness(2),
                    Padding = new Thickness(0, 4, 0, 4),
                    // Fill the grid cell so all seven buttons have the same width
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    HorizontalContentAlignment = HorizontalAlignment.Center,
                };
                button.Click += DayButton_Click;
                mDayButtons[day] = button;
                DaysPanel.Children.Add(button);
            }
        }

        // Fill the controls from the settings (their events are ignored meanwhile)
        private void FillControls()
        {
            mFillingControls = true;

            ScheduleToggle.IsChecked = mSettings.ScheduleEnabled;
            StartTimeBox.Text = SettingsHelper.FormatTime(mSettings.WorkStartTime);
            EndTimeBox.Text = SettingsHelper.FormatTime(mSettings.WorkEndTime);
            foreach (var pair in mDayButtons)
            {
                pair.Value.IsChecked = mSettings.WorkDays.Contains(pair.Key);
            }
            StartWithWindowsToggle.IsChecked = mStartup.IsEnabled();
            IntervalBox.Value = mSettings.MovingTime;
            PixelBox.Value = mSettings.MovingPixel;
            ClickToggle.IsChecked = mSettings.LeftClickAfterMoving;

            mFillingControls = false;
        }

        // Read the schedule controls, validate them and apply them.
        // Invalid input shows an error and leaves the running schedule unchanged.
        private void ApplyScheduleControls()
        {
            if (mFillingControls)
            {
                return;
            }

            var start = SettingsHelper.ParseTime(StartTimeBox.Text.Trim());
            var end = SettingsHelper.ParseTime(EndTimeBox.Text.Trim());
            var days = new List<DayOfWeek>();
            foreach (var pair in mDayButtons)
            {
                if (pair.Value.IsChecked == true)
                {
                    days.Add(pair.Key);
                }
            }

            string error = null;
            if ((start == null) || (end == null))
            {
                error = GetText("ScheduleErrorTimeFormat");
            }
            else if (end.Value <= start.Value)
            {
                error = GetText("ScheduleErrorEndBeforeStart");
            }
            else if (days.Count == 0)
            {
                error = GetText("ScheduleErrorNoDays");
            }

            ScheduleError.Text = error ?? "";
            ScheduleError.Visibility = (error == null) ? Visibility.Collapsed : Visibility.Visible;

            bool enabled = (ScheduleToggle.IsChecked == true);
            if (error != null)
            {
                // Turning the schedule off always works, even with invalid fields
                if (!enabled && mSettings.ScheduleEnabled)
                {
                    mSettings.ScheduleEnabled = false;
                    mSettings.Save();
                    mController.SetSchedule(null, DateTime.Now);
                }
                return;
            }

            // Show what was understood (e.g. "9:00" becomes "09:00")
            StartTimeBox.Text = SettingsHelper.FormatTime(start.Value);
            EndTimeBox.Text = SettingsHelper.FormatTime(end.Value);

            mSettings.ScheduleEnabled = enabled;
            mSettings.WorkStartTime = start.Value;
            mSettings.WorkEndTime = end.Value;
            mSettings.WorkDays = days;
            mSettings.Save();
            mController.SetSchedule(mSettings.CreateSchedule(), DateTime.Now);
        }

        // Redraw the status card, the main button and the tray tooltip
        private void RefreshStatus()
        {
            var status = mController.GetStatus(DateTime.Now);
            bool active = (status.State == eMoverState.ACTIVE);
            var color = active ? ACTIVE_COLOR : IDLE_COLOR;

            StatusDot.Fill = new SolidColorBrush(color);
            StatusCard.Background = new SolidColorBrush(Color.FromArgb(0x26, color.R, color.G, color.B));
            StatusText.Text = GetStateText(status.State);
            StatusDetail.Text = GetDetailText(status);
            StatusDetail.Visibility = (StatusDetail.Text.Length == 0) ? Visibility.Collapsed : Visibility.Visible;
            PauseResumeButton.Content = active ? GetText("Pause") : GetText("Resume");

            // Tray tooltips are limited to 127 characters
            string tooltip = "Auto Mouse Mover - " + GetStatusLine(status);
            mTrayIcon.Text = (tooltip.Length > 127) ? tooltip.Substring(0, 127) : tooltip;
        }

        // Get the state word (Active, Paused, Outside work hours)
        private string GetStateText(eMoverState cState)
        {
            switch (cState)
            {
                case eMoverState.ACTIVE:
                    return GetText("StatusActive");
                case eMoverState.OUTSIDE_HOURS:
                    return GetText("StatusOutsideHours");
                default:
                    return GetText("StatusPaused");
            }
        }

        // Get the time line under the state ("until 17:00", "starts Tue 09:00"), empty if none
        private string GetDetailText(MoverStatus cStatus)
        {
            if (cStatus.Until == null)
            {
                return "";
            }

            DateTime until = cStatus.Until.Value;
            string format;
            if (cStatus.State == eMoverState.OUTSIDE_HOURS)
            {
                format = GetText("StartsFormat");
            }
            else
            {
                format = (until.Date == DateTime.Now.Date) ? GetText("UntilFormat") : GetText("UntilDayFormat");
            }
            return string.Format(CultureInfo.CurrentUICulture, format, until);
        }

        // Get the one-line status used by the tray ("Active until 17:00")
        private string GetStatusLine(MoverStatus cStatus)
        {
            string detail = GetDetailText(cStatus);
            return (detail.Length == 0) ? GetStateText(cStatus.State) : GetStateText(cStatus.State) + " " + detail;
        }

        // Set every visible text from the current language
        private void ApplyTexts()
        {
            ScheduleHeader.Text = GetText("GroupSchedule");
            ScheduleToggle.Content = GetText("ScheduleEnabled");
            WorkHoursLabel.Text = GetText("WorkHours");
            WorkDaysLabel.Text = GetText("WorkDays");
            StartWithWindowsToggle.Content = GetText("StartWithWindows");
            MovementHeader.Text = GetText("GroupMovement");
            IntervalLabel.Text = GetText("MoveEvery");
            PixelLabel.Text = GetText("MoveBy");
            ClickToggle.Content = GetText("ClickAfterMoving");

            var day_names = CultureInfo.CurrentUICulture.DateTimeFormat.AbbreviatedDayNames;
            foreach (var pair in mDayButtons)
            {
                pair.Value.Content = day_names[(int)pair.Key];
            }

            // Re-validate so an error message, if shown, is in the new language
            if (ScheduleError.Visibility == Visibility.Visible)
            {
                ApplyScheduleControls();
            }
        }

        // Switch language, save it and redraw
        private void SetLanguage(string cCulture)
        {
            System.Threading.Thread.CurrentThread.CurrentUICulture = new CultureInfo(cCulture);
            mSettings.Language = cCulture;
            mSettings.Save();

            InitializeResource();
            ApplyTexts();
            RefreshStatus();
        }

        // Use the saved language, if any
        private void ApplySavedLanguage()
        {
            if (!string.IsNullOrEmpty(mSettings.Language))
            {
                try
                {
                    System.Threading.Thread.CurrentThread.CurrentUICulture = new CultureInfo(mSettings.Language);
                }
                catch (CultureNotFoundException)
                {
                    // Unknown saved value: keep the system language
                }
            }
        }

        // Get a text from the current language
        private string GetText(string cName)
        {
            return mResourceMng.GetString(cName) ?? cName;
        }

        // Initialize resource
        private void InitializeResource()
        {
            var assembly = Assembly.GetExecutingAssembly();
            string res_name = RES_NAME_PREFIX +
                CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;

            // Only some languages are embedded in the assembly. Fall back to the default
            // one for all the others, otherwise the resource manager would throw a
            // MissingManifestResourceException at the first lookup.
            if (Array.IndexOf(assembly.GetManifestResourceNames(), res_name + RES_NAME_SUFFIX) < 0)
            {
                res_name = RES_NAME_PREFIX + DEFAULT_LANGUAGE;
            }

            mResourceMng = new ResourceManager(res_name, assembly);
        }

        #endregion
    }
}
