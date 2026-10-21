using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Resources;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Threading;
using AutoMouseMover.Logic;
using AutoMouseMover.Mac.Logic;
using AutoMouseMover.Mac.Platform;
using AutoMouseMover.Mac.Services;
using AutoMouseMover.Utils;

namespace AutoMouseMover.Mac
{
    //
    // Main window: shows the status card and the settings, and owns the menu-bar icon.
    // The rules about when to move live in MoverController (shared with Windows):
    // this class displays its status and forwards clicks, timer ticks and macOS
    // conditions (permission, locked screen) to it.
    //
    public partial class MainWindow : Window
    {
        //
        // Constants
        //
        #region Constants

        // How often the schedule and pauses are checked
        private static readonly TimeSpan SCHEDULE_CHECK_INTERVAL = TimeSpan.FromSeconds(30);
        // Length of the menu-bar "pause for" item
        private static readonly TimeSpan SHORT_PAUSE = TimeSpan.FromMinutes(30);
        // Embedded language resource names
        private const string RES_NAME_PREFIX = "AutoMouseMover.Mac.Resources.lang_";
        private const string RES_NAME_SUFFIX = ".resources";
        // Language to be used when the system one is not available
        private const string DEFAULT_LANGUAGE = "en";
        // System Settings page for the permission
        private const string ACCESSIBILITY_SETTINGS_URL = "x-apple.systempreferences:com.apple.preference.security?Privacy_Accessibility";
        // Project page (About)
        private const string PROJECT_URL = "https://github.com/MohamedHamed001/auto_mouse_mover";
        // Days in the order they are shown (Monday first)
        private static readonly DayOfWeek[] DAYS_ORDER =
        {
            DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday,
            DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday,
        };
        // Status colors (readable on both light and dark themes)
        private static readonly Color ACTIVE_COLOR = Color.FromRgb(0x22, 0xC5, 0x5E);
        private static readonly Color IDLE_COLOR = Color.FromRgb(0x9C, 0xA3, 0xAF);
        private static readonly Color WARNING_COLOR = Color.FromRgb(0xF5, 0x9E, 0x0B);

        #endregion

        //
        // Members
        //
        #region Members

        // State rules
        private MoverController mController;
        // Cursor device (macOS, or a no-op stand-in when previewing on Windows)
        private ICursorDevice mDevice;
        // Mouse mover
        private MacMouseMover mMover;
        // Settings
        private MacSettings mSettings;
        // Start at login helper
        private LoginItemHelper mLoginItem;
        // Resource manager for the current language
        private ResourceManager mResourceMng;
        // Timer that moves the mouse
        private DispatcherTimer mMouseTimer;
        // Timer that checks the schedule and pauses
        private DispatcherTimer mScheduleTimer;
        // Menu-bar icon and its menu
        private TrayIcon mTrayIcon;
        // Day toggle buttons, by day
        private Dictionary<DayOfWeek, ToggleButton> mDayButtons;
        // Whether the mouse was moving at the last mouse tick
        private bool mWasRunning;
        // Whether the mouse could move at the last check (permission and unlocked)
        // (starts true so the first check always tells the controller when it cannot move)
        private bool mCanMove = true;
        // True while controls are being filled from settings (ignore their events)
        private bool mFillingControls;
        // True once the application is quitting
        private bool mExiting;

        #endregion

        //
        // Constructor
        //
        #region Constructor

        // Constructor
        public MainWindow(bool cAutoStart)
        {
            mSettings = new MacSettings(GetSettingsFolder());
            mSettings.Load();
            ApplySavedLanguage();
            InitializeResource();

            InitializeComponent();
            ApplyTheme();

            mDevice = OperatingSystem.IsMacOS() ? new MacCursorDevice() : new NullCursorDevice();
            mMover = new MacMouseMover(mDevice);
            mMover.Initialize(mSettings.MovingPixel, mSettings.LeftClickAfterMoving);

            mLoginItem = new LoginItemHelper(GetLaunchAgentsFolder());
            // Keep an enabled login item pointing at where the app is now
            mLoginItem.RefreshPath(Environment.ProcessPath);

            mController = new MoverController();
            mController.Launch(mSettings.CreateSchedule(), mSettings.PausedUntil, cAutoStart, DateTime.Now);
            mController.StateChanged += Controller_StateChanged;
            // Launch may have dropped a saved pause that already ended: save that now
            SavePausedUntil();

            // Ask for the permission once at start, if missing (macOS shows its prompt)
            if (!mDevice.CanPostEvents())
            {
                mDevice.RequestPostEvents();
            }
            UpdateCanMove(DateTime.Now);

            CreateDayButtons();
            FillControls();
            ApplyTexts();
            CreateTrayIcon();
            CreateTimers();
            RefreshStatus();
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

        // Open the Accessibility page of System Settings
        private void OpenSystemSettings_Click(object sender, RoutedEventArgs e)
        {
            mDevice.RequestPostEvents();
            OpenUrl(ACCESSIBILITY_SETTINGS_URL);
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

        // Start at login on/off
        private void StartAtLoginToggle_Click(object sender, RoutedEventArgs e)
        {
            if (StartAtLoginToggle.IsChecked == true)
            {
                // Refused from a temporary (translocated) copy: ask to move the app first
                if (!mLoginItem.Enable(Environment.ProcessPath))
                {
                    StartAtLoginToggle.IsChecked = false;
                    TranslocationHint.IsVisible = true;
                }
            }
            else
            {
                mLoginItem.Disable();
            }
        }

        // Moving interval changed
        private void IntervalBox_ValueChanged(object sender, NumericUpDownValueChangedEventArgs e)
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
        private void PixelBox_ValueChanged(object sender, NumericUpDownValueChangedEventArgs e)
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

        // Settings button: language, theme and about
        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            var flyout = new MenuFlyout();
            flyout.Items.Add(CreateMenuItem(GetText("MenuEnglish"), () => SetLanguage("en")));
            flyout.Items.Add(CreateMenuItem(GetText("MenuItalian"), () => SetLanguage("it")));
            flyout.Items.Add(new Separator());
            flyout.Items.Add(CreateThemeMenuItem(GetText("ThemeSystem"), eThemeChoice.SYSTEM));
            flyout.Items.Add(CreateThemeMenuItem(GetText("ThemeLight"), eThemeChoice.LIGHT));
            flyout.Items.Add(CreateThemeMenuItem(GetText("ThemeDark"), eThemeChoice.DARK));
            flyout.Items.Add(new Separator());
            flyout.Items.Add(CreateMenuItem(GetText("MenuAbout") + " (" + GetVersion() + ")", () => OpenUrl(PROJECT_URL)));
            flyout.ShowAt(SettingsButton);
        }

        // Window closing: hide it (macOS convention); only Quit exits
        protected override void OnClosing(WindowClosingEventArgs e)
        {
            if (!mExiting)
            {
                e.Cancel = true;
                Hide();
            }
            base.OnClosing(e);
        }

        #endregion

        //
        // Other events
        //
        #region Other events

        // Controller state changed: save the pause and redraw
        private void Controller_StateChanged(object sender, EventArgs e)
        {
            SavePausedUntil();
            RefreshStatus();
        }

        // Mouse timer elapsed
        private void MouseTimer_Tick(object sender, EventArgs e)
        {
            DateTime now = DateTime.Now;
            // Apply schedule crossings and expired pauses first
            mController.Tick(now);
            // Permission lost or screen locked: keep the mouse still (checked every tick)
            UpdateCanMove(now);

            bool is_running = mController.IsRunning(now);
            if (is_running && !mWasRunning)
            {
                // Just started: take the current cursor position as the baseline
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

        #endregion

        //
        // Public methods
        //
        #region Public methods

        // Show the main window (from the menu bar, or when the app is opened again)
        public void ShowMainWindow()
        {
            ApplyTheme();
            Show();
            WindowState = WindowState.Normal;
            Activate();
        }

        #endregion

        //
        // Private methods
        //
        #region Private methods

        // Tell the controller whether the mouse may move (permission granted and
        // screen unlocked); only when that changes, then redraw
        private void UpdateCanMove(DateTime cNow)
        {
            bool can_move = mDevice.CanPostEvents() && !mDevice.IsScreenLocked();
            if (can_move == mCanMove)
            {
                return;
            }

            mCanMove = can_move;
            if (can_move)
            {
                mMover.Initialize(mSettings.MovingPixel, mSettings.LeftClickAfterMoving);
            }
            mController.SetLocked(!can_move, cNow);
            RefreshStatus();
        }

        // Save the pause, so it survives a restart
        private void SavePausedUntil()
        {
            mSettings.PausedUntil = mController.PausedUntil;
            mSettings.Save();
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
        }

        // Create the menu-bar icon (a monochrome "template" image, so macOS colors it)
        private void CreateTrayIcon()
        {
            mTrayIcon = new TrayIcon
            {
                Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://AutoMouseMover.Mac/Assets/menubar-icon.png"))),
                Menu = new NativeMenu(),
                IsVisible = true,
            };
            MacOSProperties.SetIsTemplateIcon(mTrayIcon, true);
            TrayIcon.SetIcons(Application.Current, new TrayIcons { mTrayIcon });
        }

        // Rebuild the menu-bar menu for the current state
        private void RebuildTrayMenu(MoverStatus cStatus, eMacCardState cCard)
        {
            if (mTrayIcon == null)
            {
                return;
            }

            var menu = mTrayIcon.Menu;
            menu.Items.Clear();
            menu.Items.Add(new NativeMenuItem { Header = GetStatusLine(cStatus, cCard), IsEnabled = false });
            menu.Items.Add(new NativeMenuItemSeparator());
            if (cCard == eMacCardState.ACTIVE)
            {
                menu.Items.Add(CreateNativeItem(GetText("TrayPause30"), () => mController.PauseFor(SHORT_PAUSE, DateTime.Now)));
                if (mController.Schedule != null)
                {
                    menu.Items.Add(CreateNativeItem(GetText("TrayPauseTomorrow"), () => mController.PauseUntilTomorrow(DateTime.Now)));
                }
            }
            else
            {
                menu.Items.Add(CreateNativeItem(GetText("TrayResume"), () => mController.Resume(DateTime.Now)));
            }
            menu.Items.Add(new NativeMenuItemSeparator());
            menu.Items.Add(CreateNativeItem(GetText("TrayOpen"), ShowMainWindow));
            menu.Items.Add(CreateNativeItem(GetText("TrayQuit"), ExitApplication));
        }

        // Create a menu-bar menu item that runs an action when clicked
        private static NativeMenuItem CreateNativeItem(string cHeader, Action cAction)
        {
            var item = new NativeMenuItem { Header = cHeader };
            item.Click += (sender, e) => cAction();
            return item;
        }

        // Create a window menu item that runs an action when clicked
        private static MenuItem CreateMenuItem(string cHeader, Action cAction)
        {
            var item = new MenuItem { Header = cHeader };
            item.Click += (sender, e) => cAction();
            return item;
        }

        // Create a theme menu item, ticked if it is the current choice
        private MenuItem CreateThemeMenuItem(string cHeader, eThemeChoice cChoice)
        {
            var item = CreateMenuItem(cHeader, () => SetTheme(cChoice));
            item.ToggleType = MenuItemToggleType.CheckBox;
            item.IsChecked = (mSettings.Theme == cChoice);
            return item;
        }

        // Save, clean up and exit
        private void ExitApplication()
        {
            if (mExiting)
            {
                return;
            }
            mExiting = true;

            mMouseTimer.Stop();
            mScheduleTimer.Stop();
            mSettings.Save();
            mTrayIcon.Dispose();

            if (Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.Shutdown();
            }
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
                    // Little side padding: seven buttons must fit three-letter names
                    Padding = new Thickness(0, 4),
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
            StartTimeBox.Text = SettingsFormat.FormatTime(mSettings.WorkStartTime);
            EndTimeBox.Text = SettingsFormat.FormatTime(mSettings.WorkEndTime);
            foreach (var pair in mDayButtons)
            {
                pair.Value.IsChecked = mSettings.WorkDays.Contains(pair.Key);
            }
            StartAtLoginToggle.IsChecked = mLoginItem.IsEnabled();
            IntervalBox.Value = mSettings.MovingTime;
            PixelBox.Value = mSettings.MovingPixel;
            ClickToggle.IsChecked = mSettings.LeftClickAfterMoving;
            TranslocationHint.IsVisible = LoginItemHelper.IsTranslocated(Environment.ProcessPath);

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

            var start = SettingsFormat.ParseTime((StartTimeBox.Text ?? "").Trim());
            var end = SettingsFormat.ParseTime((EndTimeBox.Text ?? "").Trim());
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
            ScheduleError.IsVisible = (error != null);

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
            StartTimeBox.Text = SettingsFormat.FormatTime(start.Value);
            EndTimeBox.Text = SettingsFormat.FormatTime(end.Value);

            mSettings.ScheduleEnabled = enabled;
            mSettings.WorkStartTime = start.Value;
            mSettings.WorkEndTime = end.Value;
            mSettings.WorkDays = days;
            mSettings.Save();
            mController.SetSchedule(mSettings.CreateSchedule(), DateTime.Now);
        }

        // Redraw the status card, the main button and the menu-bar menu
        private void RefreshStatus()
        {
            if (StatusCard == null)
            {
                return;
            }

            DateTime now = DateTime.Now;
            var status = mController.GetStatus(now);
            var card = MacStatus.GetCardState(status, mDevice.CanPostEvents(), mDevice.IsScreenLocked());

            Color color;
            switch (card)
            {
                case eMacCardState.ACTIVE:
                    color = ACTIVE_COLOR;
                    break;
                case eMacCardState.NEEDS_PERMISSION:
                    color = WARNING_COLOR;
                    break;
                default:
                    color = IDLE_COLOR;
                    break;
            }

            StatusDot.Fill = new SolidColorBrush(color);
            StatusCard.Background = new SolidColorBrush(Color.FromArgb(0x26, color.R, color.G, color.B));
            StatusText.Text = GetCardText(card);
            StatusDetail.Text = (card == eMacCardState.NEEDS_PERMISSION) ? GetText("PermissionHint") : GetDetailText(status);
            StatusDetail.IsVisible = (StatusDetail.Text.Length > 0);
            OpenSettingsButton.IsVisible = (card == eMacCardState.NEEDS_PERMISSION);
            OpenSettingsButton.Content = GetText("OpenSystemSettings");
            PauseResumeButton.Content = (status.State == eMoverState.ACTIVE) ? GetText("Pause") : GetText("Resume");

            if (mTrayIcon != null)
            {
                mTrayIcon.ToolTipText = "Auto Mouse Mover - " + GetStatusLine(status, card);
            }
            RebuildTrayMenu(status, card);
        }

        // Get the state word shown on the card
        private string GetCardText(eMacCardState cCard)
        {
            switch (cCard)
            {
                case eMacCardState.ACTIVE:
                    return GetText("StatusActive");
                case eMacCardState.OUTSIDE_HOURS:
                    return GetText("StatusOutsideHours");
                case eMacCardState.NEEDS_PERMISSION:
                    return GetText("StatusNeedsPermission");
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

        // Get the one-line status used by the menu bar ("Active until 17:00")
        private string GetStatusLine(MoverStatus cStatus, eMacCardState cCard)
        {
            if (cCard == eMacCardState.NEEDS_PERMISSION)
            {
                return GetCardText(cCard);
            }
            string detail = GetDetailText(cStatus);
            return (detail.Length == 0) ? GetCardText(cCard) : GetCardText(cCard) + " " + detail;
        }

        // Set every visible text from the current language
        private void ApplyTexts()
        {
            ScheduleHeader.Text = GetText("GroupSchedule");
            ScheduleToggle.Content = GetText("ScheduleEnabled");
            WorkHoursLabel.Text = GetText("WorkHours");
            WorkDaysLabel.Text = GetText("WorkDays");
            StartAtLoginToggle.Content = GetText("StartAtLogin");
            MovementHeader.Text = GetText("GroupMovement");
            IntervalLabel.Text = GetText("MoveEvery");
            PixelLabel.Text = GetText("MoveBy");
            ClickToggle.Content = GetText("ClickAfterMoving");
            TranslocationHint.Text = GetText("MoveToApplications");

            var day_names = CultureInfo.CurrentUICulture.DateTimeFormat.AbbreviatedDayNames;
            foreach (var pair in mDayButtons)
            {
                pair.Value.Content = day_names[(int)pair.Key];
            }

            // Re-validate so an error message, if shown, is in the new language
            if (ScheduleError.IsVisible)
            {
                ApplyScheduleControls();
            }
        }

        // Switch language, save it and redraw
        private void SetLanguage(string cCulture)
        {
            CultureInfo.CurrentUICulture = new CultureInfo(cCulture);
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
                    CultureInfo.CurrentUICulture = new CultureInfo(mSettings.Language);
                }
                catch (CultureNotFoundException)
                {
                    // Unknown saved value: keep the system language
                }
            }
        }

        // Switch theme, save it and apply it
        private void SetTheme(eThemeChoice cChoice)
        {
            mSettings.Theme = cChoice;
            mSettings.Save();
            ApplyTheme();
        }

        // Apply the chosen theme ("Default" follows the macOS appearance)
        private void ApplyTheme()
        {
            ThemeVariant variant;
            switch (mSettings.Theme)
            {
                case eThemeChoice.LIGHT:
                    variant = ThemeVariant.Light;
                    break;
                case eThemeChoice.DARK:
                    variant = ThemeVariant.Dark;
                    break;
                default:
                    variant = ThemeVariant.Default;
                    break;
            }
            Application.Current.RequestedThemeVariant = variant;
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
            string res_name = RES_NAME_PREFIX + CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;

            // Only some languages are embedded: fall back to the default one for the others
            if (Array.IndexOf(assembly.GetManifestResourceNames(), res_name + RES_NAME_SUFFIX) < 0)
            {
                res_name = RES_NAME_PREFIX + DEFAULT_LANGUAGE;
            }

            mResourceMng = new ResourceManager(res_name, assembly);
        }

        // Get the app version (e.g. "2.2.0")
        private static string GetVersion()
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            return (version == null) ? "" : version.ToString(3);
        }

        // Open a URL with the system (browser, System Settings)
        private static void OpenUrl(string cUrl)
        {
            Process.Start(new ProcessStartInfo(cUrl) { UseShellExecute = true });
        }

        // Settings folder: ~/Library/Application Support/AutoMouseMover on macOS,
        // a separate preview folder when running on Windows during development
        private static string GetSettingsFolder()
        {
            if (OperatingSystem.IsMacOS())
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                                    "Library", "Application Support", "AutoMouseMover");
            }
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AutoMouseMover.MacPreview");
        }

        // LaunchAgents folder: ~/Library/LaunchAgents on macOS, a preview folder elsewhere
        private static string GetLaunchAgentsFolder()
        {
            if (OperatingSystem.IsMacOS())
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "LaunchAgents");
            }
            return Path.Combine(GetSettingsFolder(), "LaunchAgents");
        }

        #endregion
    }
}
