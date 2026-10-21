using System;
using Avalonia;
using Avalonia.Controls;

namespace AutoMouseMover.Mac
{
    static class Program
    {
        // Main entry point for the application.
        // The app keeps running in the menu bar when the window is closed;
        // it only exits with Quit (OnExplicitShutdown).
        [STAThread]
        public static void Main(string[] cArgs)
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(cArgs, ShutdownMode.OnExplicitShutdown);
        }

        // Avalonia configuration (also used by the IDE previewer)
        public static AppBuilder BuildAvaloniaApp()
        {
            return AppBuilder.Configure<App>()
                .UsePlatformDetect()
                // Menu-bar app: no Dock icon (Avalonia would otherwise override LSUIElement)
                .With(new MacOSPlatformOptions { ShowInDock = false })
                .LogToTrace();
        }
    }
}
