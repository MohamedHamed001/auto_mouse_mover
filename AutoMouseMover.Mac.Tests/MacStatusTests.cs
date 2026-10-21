using AutoMouseMover.Logic;
using AutoMouseMover.Mac.Logic;

namespace AutoMouseMover.Mac.Tests
{
    public class MacStatusTests
    {
        [Fact]
        public void PermissionLost_ShowsNeedsPermission()
        {
            // Never "Active" while macOS does not let the app move the mouse
            var active = new MoverStatus(eMoverState.ACTIVE, null);

            Assert.Equal(eMacCardState.NEEDS_PERMISSION, MacStatus.GetCardState(active, false, false));
        }

        [Theory]
        [InlineData(eMoverState.ACTIVE, eMacCardState.ACTIVE)]
        [InlineData(eMoverState.PAUSED, eMacCardState.PAUSED)]
        [InlineData(eMoverState.OUTSIDE_HOURS, eMacCardState.OUTSIDE_HOURS)]
        public void Permission_Ok_PassesStateThrough(eMoverState state, eMacCardState expected)
        {
            Assert.Equal(expected, MacStatus.GetCardState(new MoverStatus(state, null), true, false));
        }

        [Fact]
        public void Locked_ShowsControllerState()
        {
            // The window is not visible while locked; the controller's state is shown
            var paused = new MoverStatus(eMoverState.PAUSED, null);

            Assert.Equal(eMacCardState.PAUSED, MacStatus.GetCardState(paused, true, true));
        }
    }
}
