using AutoMouseMover.Mac.Logic;
using AutoMouseMover.Mac.Platform;

namespace AutoMouseMover.Mac.Tests
{
    public class MacMouseMoverTests
    {
        // Fake cursor: remembers moves and clicks, and moves itself when told to
        private class FakeCursor : ICursorDevice
        {
            public double X;
            public double Y;
            public (double X, double Y, double Width, double Height)? Bounds = (0, 0, 1440, 900);
            public List<(double X, double Y)> Moves = new();
            public int Clicks;

            public bool CanPostEvents() => true;
            public void RequestPostEvents() { }
            public bool IsScreenLocked() => false;
            public (double X, double Y) GetPosition() => (X, Y);
            public (double X, double Y, double Width, double Height)? GetScreenBounds(double cX, double cY) => Bounds;
            public void MoveTo(double cX, double cY) { X = cX; Y = cY; Moves.Add((cX, cY)); }
            public void LeftClick(double cX, double cY) { Clicks++; }
        }

        // Mover over a fake cursor at the given position
        private static (MacMouseMover Mover, FakeCursor Cursor) Create(double cX, double cY, bool cClick = false)
        {
            var cursor = new FakeCursor { X = cX, Y = cY };
            var mover = new MacMouseMover(cursor);
            mover.Initialize(5, cClick);
            return (mover, cursor);
        }

        [Fact]
        public void MoveMouse_CursorStill_MovesForwardThenBack()
        {
            var (mover, cursor) = Create(100, 100);

            mover.MoveMouse();
            mover.MoveMouse();

            Assert.Equal(new[] { (105.0, 105.0), (100.0, 100.0) }, cursor.Moves);
        }

        [Fact]
        public void MoveMouse_UserMovedCursor_DoesNothing()
        {
            var (mover, cursor) = Create(100, 100);

            cursor.X = 300;   // the user is working
            mover.MoveMouse();

            Assert.Empty(cursor.Moves);
        }

        [Fact]
        public void MoveNear_RightEdge_FlipsDirection()
        {
            var (mover, cursor) = Create(1438, 450);

            mover.MoveMouse();

            Assert.Equal(new[] { (1433.0, 455.0) }, cursor.Moves);
        }

        [Fact]
        public void MoveMouse_ClickEnabled_ClicksAfterMove()
        {
            var (mover, cursor) = Create(100, 100, cClick: true);

            mover.MoveMouse();

            Assert.Equal(1, cursor.Clicks);
        }

        [Fact]
        public void MoveMouse_NoScreen_DoesNothing()
        {
            var (mover, cursor) = Create(100, 100);
            cursor.Bounds = null;

            mover.MoveMouse();

            Assert.Empty(cursor.Moves);
        }
    }
}
