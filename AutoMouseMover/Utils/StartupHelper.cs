using Microsoft.Win32;

namespace AutoMouseMover.Utils
{
    //
    // Startup helper: makes the application start with Windows.
    // Windows starts every program listed under the current user's "Run" registry
    // key at sign-in. The key belongs to the user, so no admin rights are needed.
    //
    class StartupHelper
    {
        //
        // Constants
        //
        #region Constants

        // Current user's Run key (under HKEY_CURRENT_USER)
        public const string RUN_KEY_PATH = @"Software\Microsoft\Windows\CurrentVersion\Run";
        // Name of our value in the Run key
        public const string VALUE_NAME = "AutoMouseMover";
        // Switch passed when started by Windows
        private const string AUTO_START_SWITCH = "--autostart";

        #endregion

        //
        // Members
        //
        #region Members

        // Run key path (tests use a throwaway key instead of the real one)
        private string mRunKeyPath;

        #endregion

        //
        // Public methods
        //
        #region Public methods

        // Constructor
        public StartupHelper(string cRunKeyPath = RUN_KEY_PATH)
        {
            mRunKeyPath = cRunKeyPath;
        }

        // Get if the application is set to start with Windows
        public bool IsEnabled()
        {
            using (var key = Registry.CurrentUser.OpenSubKey(mRunKeyPath))
            {
                return (key != null) && (key.GetValue(VALUE_NAME) != null);
            }
        }

        // Start the given executable with Windows
        public void Enable(string cExePath)
        {
            using (var key = Registry.CurrentUser.CreateSubKey(mRunKeyPath))
            {
                // Quote the path: it may contain spaces
                key.SetValue(VALUE_NAME, "\"" + cExePath + "\" " + AUTO_START_SWITCH);
            }
        }

        // Stop starting with Windows (nothing happens if it was not enabled)
        public void Disable()
        {
            using (var key = Registry.CurrentUser.OpenSubKey(mRunKeyPath, true))
            {
                key?.DeleteValue(VALUE_NAME, false);
            }
        }

        #endregion
    }
}
