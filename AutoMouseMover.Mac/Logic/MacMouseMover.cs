using AutoMouseMover.Mac.Platform;

namespace AutoMouseMover.Mac.Logic
{
    //
    // Mouse mover for macOS: the same rule as the Windows AutomaticMouseMover.
    // The cursor is moved only if it has not moved since the last check (so the
    // user is never disturbed), back and forth on the same spot.
    //
    class MacMouseMover
    {
        //
        // Members
        //
        #region Members

        // Cursor device
        private ICursorDevice mDevice;
        // Last cursor position
        private (double X, double Y) mLastPos;
        // Moving pixels amount
        private int mMovingPixel;
        // Move forward (+) next time, otherwise backward (-)
        private bool mForward;
        // Left click flag
        private bool mLeftClick;

        #endregion

        //
        // Public methods
        //
        #region Public methods

        // Constructor
        public MacMouseMover(ICursorDevice cDevice)
        {
            mDevice = cDevice;
            Initialize(5, false);
        }

        // Initialize (takes the current position as the baseline)
        public void Initialize(int cMovingPixel, bool cLeftClick)
        {
            mMovingPixel = cMovingPixel;
            mLeftClick = cLeftClick;
            mForward = true;
            mLastPos = mDevice.GetPosition();
        }

        // Move the mouse if the user did not move it since the last call
        public void MoveMouse()
        {
            var pos = mDevice.GetPosition();

            if (pos == mLastPos)
            {
                var bounds = mDevice.GetScreenBounds(pos.X, pos.Y);
                if (bounds != null)
                {
                    double delta = mForward ? mMovingPixel : -mMovingPixel;
                    double delta_x = delta;
                    double delta_y = delta;
                    var b = bounds.Value;

                    // Invert a delta that would put the cursor out of its screen
                    if ((pos.X + delta_x < b.X) || (pos.X + delta_x > b.X + b.Width))
                    {
                        delta_x = -delta_x;
                    }
                    if ((pos.Y + delta_y < b.Y) || (pos.Y + delta_y > b.Y + b.Height))
                    {
                        delta_y = -delta_y;
                    }

                    mDevice.MoveTo(pos.X + delta_x, pos.Y + delta_y);
                    if (mLeftClick)
                    {
                        mDevice.LeftClick(pos.X + delta_x, pos.Y + delta_y);
                    }
                    mForward = !mForward;
                }
            }

            // Reset last cursor position
            mLastPos = mDevice.GetPosition();
        }

        #endregion
    }
}
