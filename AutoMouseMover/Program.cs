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
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;

namespace AutoMouseMover
{
    static class Program
    {
        // Name shared by all copies of the app in this Windows session
        private const string SINGLE_INSTANCE_MUTEX = @"Local\AutoMouseMover.SingleInstance";
        // Signal a second copy sets to ask the running copy to show its window
        private const string SHOW_WINDOW_EVENT = @"Local\AutoMouseMover.ShowWindow";
        // Let any process bring a window to the foreground
        private const int ASFW_ANY = -1;
        // Command line switch to start in the tray and (without a schedule) start moving
        private const string AUTO_START_SWITCH = "--autostart";

        //
        // Main entry point for the application
        //
        [STAThread]
        static void Main()
        {
            // Only one copy may run: a second one (e.g. an old Startup-folder shortcut
            // plus the Start with Windows setting) would move the mouse twice and show
            // two tray icons. A named mutex is a system-wide "taken" flag.
            bool auto_start = IsAutoStartRequested();

            using (var mutex = new Mutex(true, SINGLE_INSTANCE_MUTEX, out bool created_new))
            {
                if (!created_new)
                {
                    // Opened by the user while already running: show the running
                    // copy's window. Not for --autostart (e.g. started by Windows).
                    if (!auto_start)
                    {
                        AskRunningCopyToShowWindow();
                    }
                    return;
                }

                // Visual styles for the remaining WinForms dialog (About)
                System.Windows.Forms.Application.EnableVisualStyles();
                System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);

                try
                {
                    // ensure we capture any issue occurring instead of silently failing.
                    var app = new App();
                    app.InitializeComponent();
                    // The app keeps running in the tray when the window is hidden;
                    // it only exits when the user closes it
                    app.ShutdownMode = ShutdownMode.OnExplicitShutdown;

                    var window = new MainWindow(auto_start);
                    // With --autostart start hidden in the tray
                    if (!auto_start)
                    {
                        window.Show();
                    }

                    // When a second copy sets the signal, show the window. The wait runs on
                    // a thread-pool thread, so the window is shown through its Dispatcher.
                    using (var show_event = new EventWaitHandle(false, EventResetMode.AutoReset, SHOW_WINDOW_EVENT))
                    {
                        ThreadPool.RegisterWaitForSingleObject(
                            show_event,
                            (state, timed_out) => window.Dispatcher.BeginInvoke(new Action(window.ShowMainWindow)),
                            null,
                            Timeout.Infinite,
                            false);
                        app.Run();
                    }
                }
                catch (Exception e)
                {
                    // display the details to the user
                    var text = $"There was an error that caused the application to crash.\n\n{e}";
                    MessageBox.Show(text, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // Ask the running copy to show its window
        private static void AskRunningCopyToShowWindow()
        {
            try
            {
                // This copy was opened by the user, so Windows lets it hand the
                // "may come to the foreground" right to the running copy
                AllowSetForegroundWindow(ASFW_ANY);
                using (var show_event = EventWaitHandle.OpenExisting(SHOW_WINDOW_EVENT))
                {
                    show_event.Set();
                }
            }
            catch (WaitHandleCannotBeOpenedException)
            {
                // The running copy is still starting up: nothing to show yet
            }
        }

        // Allow another process to bring its window to the foreground
        [DllImport("user32.dll")]
        private static extern bool AllowSetForegroundWindow(int dwProcessId);

        // Get if the application was started with the auto start switch
        private static bool IsAutoStartRequested()
        {
            foreach (var arg in Environment.GetCommandLineArgs())
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
