using System;
using System.Runtime.InteropServices;

namespace AutoMouseMover.Mac.Platform
{
    //
    // Cursor device for macOS, calling the system frameworks directly (P/Invoke):
    //   - CoreGraphics: cursor position, screens, posting mouse events, permission
    //   - CoreFoundation: reading the "screen is locked" flag of the session
    // Every object returned by a Create/Copy function must be released (CFRelease).
    //
    class MacCursorDevice : ICursorDevice
    {
        //
        // Constants
        //
        #region Constants

        private const string CORE_GRAPHICS = "/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics";
        private const string CORE_FOUNDATION = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";

        // CGEventType values
        private const uint EVENT_LEFT_MOUSE_DOWN = 1;
        private const uint EVENT_LEFT_MOUSE_UP = 2;
        private const uint EVENT_MOUSE_MOVED = 5;
        // CGMouseButton left
        private const uint MOUSE_BUTTON_LEFT = 0;
        // CGEventTapLocation: post as if from the hardware (resets the idle time)
        private const uint HID_EVENT_TAP = 0;
        // kCFStringEncodingUTF8
        private const uint STRING_ENCODING_UTF8 = 0x08000100;
        // Session dictionary key telling whether the screen is locked
        private const string SCREEN_LOCKED_KEY = "CGSSessionScreenIsLocked";

        #endregion

        //
        // Native types and functions
        //
        #region Native

        [StructLayout(LayoutKind.Sequential)]
        private struct CGPoint
        {
            public double X;
            public double Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct CGRect
        {
            public double X;
            public double Y;
            public double Width;
            public double Height;
        }

        [DllImport(CORE_GRAPHICS)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool CGPreflightPostEventAccess();

        [DllImport(CORE_GRAPHICS)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool CGRequestPostEventAccess();

        [DllImport(CORE_GRAPHICS)]
        private static extern IntPtr CGEventCreate(IntPtr cSource);

        [DllImport(CORE_GRAPHICS)]
        private static extern CGPoint CGEventGetLocation(IntPtr cEvent);

        [DllImport(CORE_GRAPHICS)]
        private static extern IntPtr CGEventCreateMouseEvent(IntPtr cSource, uint cType, CGPoint cPosition, uint cButton);

        [DllImport(CORE_GRAPHICS)]
        private static extern void CGEventPost(uint cTap, IntPtr cEvent);

        [DllImport(CORE_GRAPHICS)]
        private static extern int CGGetDisplaysWithPoint(CGPoint cPoint, uint cMaxDisplays, uint[] cDisplays, out uint cCount);

        [DllImport(CORE_GRAPHICS)]
        private static extern CGRect CGDisplayBounds(uint cDisplay);

        [DllImport(CORE_GRAPHICS)]
        private static extern IntPtr CGSessionCopyCurrentDictionary();

        [DllImport(CORE_FOUNDATION)]
        private static extern IntPtr CFStringCreateWithCString(IntPtr cAllocator, string cText, uint cEncoding);

        [DllImport(CORE_FOUNDATION)]
        private static extern IntPtr CFDictionaryGetValue(IntPtr cDictionary, IntPtr cKey);

        [DllImport(CORE_FOUNDATION)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool CFBooleanGetValue(IntPtr cBoolean);

        [DllImport(CORE_FOUNDATION)]
        private static extern void CFRelease(IntPtr cObject);

        #endregion

        //
        // Public methods
        //
        #region Public methods

        // Get if the app may post input events (Accessibility permission).
        // Note: macOS may only report a new grant after the app is relaunched.
        public bool CanPostEvents()
        {
            return CGPreflightPostEventAccess();
        }

        // Ask macOS for the permission (shows the system prompt once)
        public void RequestPostEvents()
        {
            CGRequestPostEventAccess();
        }

        // Get if the screen is locked (the key is missing when it is not)
        public bool IsScreenLocked()
        {
            IntPtr session = CGSessionCopyCurrentDictionary();
            if (session == IntPtr.Zero)
            {
                // No session information (e.g. at the login window): treat as locked
                return true;
            }

            IntPtr key = CFStringCreateWithCString(IntPtr.Zero, SCREEN_LOCKED_KEY, STRING_ENCODING_UTF8);
            try
            {
                IntPtr value = CFDictionaryGetValue(session, key);
                return (value != IntPtr.Zero) && CFBooleanGetValue(value);
            }
            finally
            {
                CFRelease(key);
                CFRelease(session);
            }
        }

        // Get the cursor position
        public (double X, double Y) GetPosition()
        {
            IntPtr ev = CGEventCreate(IntPtr.Zero);
            if (ev == IntPtr.Zero)
            {
                return (double.NaN, double.NaN);
            }
            try
            {
                var point = CGEventGetLocation(ev);
                return (point.X, point.Y);
            }
            finally
            {
                CFRelease(ev);
            }
        }

        // Get the bounds of the screen containing the point, null if none
        public (double X, double Y, double Width, double Height)? GetScreenBounds(double cX, double cY)
        {
            var displays = new uint[1];
            int error = CGGetDisplaysWithPoint(new CGPoint { X = cX, Y = cY }, 1, displays, out uint count);
            if ((error != 0) || (count == 0))
            {
                return null;
            }

            var rect = CGDisplayBounds(displays[0]);
            return (rect.X, rect.Y, rect.Width, rect.Height);
        }

        // Move the cursor to the point
        public void MoveTo(double cX, double cY)
        {
            PostMouseEvent(EVENT_MOUSE_MOVED, cX, cY);
        }

        // Left-click at the point
        public void LeftClick(double cX, double cY)
        {
            PostMouseEvent(EVENT_LEFT_MOUSE_DOWN, cX, cY);
            PostMouseEvent(EVENT_LEFT_MOUSE_UP, cX, cY);
        }

        #endregion

        //
        // Private methods
        //
        #region Private methods

        // Create, post and release one mouse event
        private static void PostMouseEvent(uint cType, double cX, double cY)
        {
            IntPtr ev = CGEventCreateMouseEvent(IntPtr.Zero, cType, new CGPoint { X = cX, Y = cY }, MOUSE_BUTTON_LEFT);
            if (ev == IntPtr.Zero)
            {
                return;
            }
            try
            {
                CGEventPost(HID_EVENT_TAP, ev);
            }
            finally
            {
                CFRelease(ev);
            }
        }

        #endregion
    }
}
