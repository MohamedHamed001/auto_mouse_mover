using AutoMouseMover.Utils;
using Microsoft.Win32;

namespace AutoMouseMover.Tests
{
    // These tests use a throwaway key instead of the real Run key, so they never
    // change what starts with Windows. xUnit creates a new instance of this class
    // for every test and calls Dispose after it, so each test starts clean.
    public class StartupHelperTests : IDisposable
    {
        private const string TEST_ROOT_KEY = @"Software\AutoMouseMover.Tests";
        private const string TEST_RUN_KEY = TEST_ROOT_KEY + @"\Run";
        private const string EXE_PATH = @"C:\Apps\Auto Mouse Mover\AutoMouseMover.exe";

        private readonly StartupHelper mHelper = new StartupHelper(TEST_RUN_KEY);

        // Delete the throwaway key after each test
        public void Dispose()
        {
            Registry.CurrentUser.DeleteSubKeyTree(TEST_ROOT_KEY, false);
        }

        [Fact]
        public void Enable_ThenIsEnabled()
        {
            mHelper.Enable(EXE_PATH);

            Assert.True(mHelper.IsEnabled());
        }

        [Fact]
        public void Enable_WritesQuotedPathWithAutostart()
        {
            mHelper.Enable(EXE_PATH);

            // Quotes are needed because the path contains spaces
            using var key = Registry.CurrentUser.OpenSubKey(TEST_RUN_KEY);
            Assert.Equal("\"" + EXE_PATH + "\" --autostart", key?.GetValue(StartupHelper.VALUE_NAME));
        }

        [Fact]
        public void Disable_WhenMissing_DoesNotThrow()
        {
            // The key does not even exist yet
            mHelper.Disable();

            Assert.False(mHelper.IsEnabled());
        }

        [Fact]
        public void Disable_ThenNotEnabled()
        {
            mHelper.Enable(EXE_PATH);

            mHelper.Disable();

            Assert.False(mHelper.IsEnabled());
        }
    }
}
