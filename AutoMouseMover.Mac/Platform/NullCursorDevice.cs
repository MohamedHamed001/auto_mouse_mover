namespace AutoMouseMover.Mac.Platform
{
    //
    // Cursor device that does nothing, so the Mac app's window can be run and
    // checked on Windows during development. It never moves the mouse.
    // ponytail: dev-only stand-in; delete if Windows previews are no longer needed.
    //
    class NullCursorDevice : ICursorDevice
    {
        // Permission is never missing here
        public bool CanPostEvents()
        {
            return true;
        }

        // Nothing to request
        public void RequestPostEvents()
        {
        }

        // Never locked
        public bool IsScreenLocked()
        {
            return false;
        }

        // Fixed position
        public (double X, double Y) GetPosition()
        {
            return (0, 0);
        }

        // No screen: the mover then never moves
        public (double X, double Y, double Width, double Height)? GetScreenBounds(double cX, double cY)
        {
            return null;
        }

        // Does nothing
        public void MoveTo(double cX, double cY)
        {
        }

        // Does nothing
        public void LeftClick(double cX, double cY)
        {
        }
    }
}
