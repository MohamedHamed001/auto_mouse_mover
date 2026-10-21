using AutoMouseMover.Mac.Services;

namespace AutoMouseMover.Mac.Tests
{
    // Each test gets its own temporary "LaunchAgents" folder, deleted afterwards
    public class LoginItemHelperTests : IDisposable
    {
        private const string APP_PATH = "/Applications/Auto Mouse Mover.app/Contents/MacOS/AutoMouseMover.Mac";

        private readonly string mFolder = Path.Combine(Path.GetTempPath(), "amm-agents-" + Guid.NewGuid().ToString("N"));

        // Delete the temporary folder
        public void Dispose()
        {
            if (Directory.Exists(mFolder))
            {
                Directory.Delete(mFolder, true);
            }
        }

        // Path of the LaunchAgent file the helper writes
        private string PlistPath()
        {
            return Path.Combine(mFolder, LoginItemHelper.LABEL + ".plist");
        }

        [Fact]
        public void Enable_WritesPlistWithPathAndAutostart()
        {
            var helper = new LoginItemHelper(mFolder);

            Assert.True(helper.Enable(APP_PATH));

            string plist = File.ReadAllText(PlistPath());
            Assert.Contains("<string>" + APP_PATH + "</string>", plist);
            Assert.Contains("<string>--autostart</string>", plist);
            Assert.Contains("<key>RunAtLoad</key><true/>", plist);
            Assert.True(helper.IsEnabled());
        }

        [Fact]
        public void Enable_PathWithAmpersand_IsEscaped()
        {
            var helper = new LoginItemHelper(mFolder);

            helper.Enable("/Applications/A&B.app/Contents/MacOS/AutoMouseMover.Mac");

            Assert.Contains("A&amp;B.app", File.ReadAllText(PlistPath()));
        }

        [Fact]
        public void Enable_TranslocatedPath_Refused()
        {
            // Unsigned apps opened from Downloads run from a temporary copy that
            // disappears: a login item pointing there would never start
            var helper = new LoginItemHelper(mFolder);

            bool enabled = helper.Enable("/private/var/folders/x/AppTranslocation/y/Auto Mouse Mover.app/Contents/MacOS/AutoMouseMover.Mac");

            Assert.False(enabled);
            Assert.False(File.Exists(PlistPath()));
        }

        [Fact]
        public void Disable_WhenMissing_DoesNotThrow()
        {
            var helper = new LoginItemHelper(mFolder);

            helper.Disable();

            Assert.False(helper.IsEnabled());
        }

        [Fact]
        public void RefreshPath_WhenDisabled_WritesNothing()
        {
            var helper = new LoginItemHelper(mFolder);

            helper.RefreshPath(APP_PATH);

            Assert.False(File.Exists(PlistPath()));
        }

        [Fact]
        public void RefreshPath_WhenEnabled_UpdatesPath()
        {
            var helper = new LoginItemHelper(mFolder);
            helper.Enable("/Users/me/Desktop/Auto Mouse Mover.app/Contents/MacOS/AutoMouseMover.Mac");

            helper.RefreshPath(APP_PATH);

            Assert.Contains("<string>" + APP_PATH + "</string>", File.ReadAllText(PlistPath()));
        }
    }
}
