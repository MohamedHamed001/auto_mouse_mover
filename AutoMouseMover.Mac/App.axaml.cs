using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace AutoMouseMover.Mac
{
    //
    // Avalonia application: creates the main window (which owns the menu-bar icon)
    //
    public partial class App : Application
    {
        // Command line switch to start hidden in the menu bar
        private const string AUTO_START_SWITCH = "--autostart";

        // Load App.axaml
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        // Create the window once Avalonia is ready
        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                bool auto_start = IsAutoStartRequested(desktop.Args);
                var window = new MainWindow(auto_start);
                // With --autostart start hidden in the menu bar
                if (!auto_start)
                {
                    window.Show();
                }

                // Opening the app again (Finder, Spotlight) shows the window.
                // macOS keeps a single copy of an app bundle running and sends "reopen".
                if (TryGetFeature(typeof(IActivatableLifetime)) is IActivatableLifetime activatable)
                {
                    activatable.Activated += (sender, e) =>
                    {
                        if (e.Kind == ActivationKind.Reopen)
                        {
                            window.ShowMainWindow();
                        }
                    };
                }
            }

            base.OnFrameworkInitializationCompleted();
        }

        // Get if the application was started with the auto start switch
        private static bool IsAutoStartRequested(string[] cArgs)
        {
            foreach (var arg in cArgs ?? Array.Empty<string>())
            {
                if (string.Equals(arg, AUTO_START_SWITCH, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
