using System.IO;
using System.Security;

namespace AutoMouseMover.Mac.Services
{
    //
    // Login item helper: makes the app start when the user logs in.
    // macOS starts every "LaunchAgent" file found in ~/Library/LaunchAgents at login.
    // The folder belongs to the user, and this works without code signing.
    // (The Mac equivalent of the Windows per-user Run key.)
    //
    class LoginItemHelper
    {
        //
        // Constants
        //
        #region Constants

        // LaunchAgent label, also the file name (without .plist)
        public const string LABEL = "io.github.mohamedhamed001.automousemover";
        // Part of the path of the temporary copy macOS runs unsigned downloads from
        private const string TRANSLOCATION_MARKER = "/AppTranslocation/";
        // LaunchAgent file content; {0} is the XML-escaped executable path
        private const string PLIST_TEMPLATE =
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" +
            "<!DOCTYPE plist PUBLIC \"-//Apple//DTD PLIST 1.0//EN\" \"http://www.apple.com/DTDs/PropertyList-1.0.dtd\">\n" +
            "<plist version=\"1.0\">\n" +
            "<dict>\n" +
            "  <key>Label</key><string>" + LABEL + "</string>\n" +
            "  <key>ProgramArguments</key>\n" +
            "  <array><string>{0}</string><string>--autostart</string></array>\n" +
            "  <key>RunAtLoad</key><true/>\n" +
            "</dict>\n" +
            "</plist>\n";

        #endregion

        //
        // Members
        //
        #region Members

        // LaunchAgents folder (tests use a temporary one)
        private string mFolder;

        #endregion

        //
        // Public methods
        //
        #region Public methods

        // Constructor
        public LoginItemHelper(string cLaunchAgentsFolder)
        {
            mFolder = cLaunchAgentsFolder;
        }

        // Get if the app is set to start at login (the file is the source of truth)
        public bool IsEnabled()
        {
            return File.Exists(PlistPath());
        }

        // Start the given executable at login.
        // Returns false (and writes nothing) for a translocated path: that copy
        // disappears, so the login item would never start.
        public bool Enable(string cExecutablePath)
        {
            if (IsTranslocated(cExecutablePath))
            {
                return false;
            }

            Directory.CreateDirectory(mFolder);
            File.WriteAllText(PlistPath(), string.Format(PLIST_TEMPLATE, SecurityElement.Escape(cExecutablePath)));
            return true;
        }

        // Stop starting at login (nothing happens if it was not enabled)
        public void Disable()
        {
            if (File.Exists(PlistPath()))
            {
                File.Delete(PlistPath());
            }
        }

        // Point an enabled login item at the app's current location
        // (called at every launch, so moving the app does not break it)
        public void RefreshPath(string cExecutablePath)
        {
            if (IsEnabled())
            {
                Enable(cExecutablePath);
            }
        }

        // Get if the path is a temporary translocated copy (app opened from Downloads)
        public static bool IsTranslocated(string cPath)
        {
            return (cPath != null) && cPath.Contains(TRANSLOCATION_MARKER);
        }

        #endregion

        //
        // Private methods
        //
        #region Private methods

        // Full path of the LaunchAgent file
        private string PlistPath()
        {
            return Path.Combine(mFolder, LABEL + ".plist");
        }

        #endregion
    }
}
