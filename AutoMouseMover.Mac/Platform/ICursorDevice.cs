namespace AutoMouseMover.Mac.Platform
{
    //
    // Cursor device: everything the app needs from the operating system to move
    // the mouse. macOS uses MacCursorDevice; tests use a fake; Windows development
    // previews use NullCursorDevice.
    //
    interface ICursorDevice
    {
        // Get if the app may post input events (macOS Accessibility permission)
        bool CanPostEvents();

        // Ask macOS for the permission (shows the system prompt once)
        void RequestPostEvents();

        // Get if the screen is locked
        bool IsScreenLocked();

        // Get the cursor position (global coordinates, origin top-left)
        (double X, double Y) GetPosition();

        // Get the bounds of the screen containing the point, null if none
        (double X, double Y, double Width, double Height)? GetScreenBounds(double cX, double cY);

        // Move the cursor to the point
        void MoveTo(double cX, double cY);

        // Left-click at the point
        void LeftClick(double cX, double cY);
    }
}
