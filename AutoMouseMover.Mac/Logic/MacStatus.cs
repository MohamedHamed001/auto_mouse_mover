using AutoMouseMover.Logic;

namespace AutoMouseMover.Mac.Logic
{
    //
    // What the macOS status card shows. Public because test methods take it as a parameter.
    //
    public enum eMacCardState
    {
        ACTIVE,
        PAUSED,
        OUTSIDE_HOURS,
        NEEDS_PERMISSION,
    }

    //
    // macOS status rule: the controller's state, except that a missing permission
    // always wins (the app must never show Active while it cannot move the mouse)
    //
    static class MacStatus
    {
        // Get the card state for the controller status and the macOS conditions
        public static eMacCardState GetCardState(MoverStatus cStatus, bool cCanPostEvents, bool cScreenLocked)
        {
            if (!cCanPostEvents)
            {
                return eMacCardState.NEEDS_PERMISSION;
            }

            // A locked screen needs no special state: the window cannot be seen then,
            // and the controller already keeps the mouse still
            switch (cStatus.State)
            {
                case eMoverState.ACTIVE:
                    return eMacCardState.ACTIVE;
                case eMoverState.OUTSIDE_HOURS:
                    return eMacCardState.OUTSIDE_HOURS;
                default:
                    return eMacCardState.PAUSED;
            }
        }
    }
}
