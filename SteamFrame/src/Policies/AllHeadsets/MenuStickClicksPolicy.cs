namespace VhvrSteamFrame
{
    /// <summary>
    /// Valheim's on-screen menu buttons fire on ZInput.GetButtonDown(key). Two of them are labelled with stick clicks:
    /// "Place stacks" (JoyRStick) and "Take all" (JoyLStick). VHVR leaves both keys unmapped, so they never worked in VR.
    /// Which VR action stands in for each stick click (with the Frame binding tweaks: right click = crouch toggle,
    /// left click = sprint toggle).
    /// </summary>
    public static class MenuStickClicksPolicy
    {
        public enum StickAction { None, ToggleCrouchRight, ToggleRunLeft }

        public static StickAction ActionFor(string zinputKey)
        {
            if (zinputKey == "JoyRStick") return StickAction.ToggleCrouchRight;
            if (zinputKey == "JoyLStick") return StickAction.ToggleRunLeft;
            return StickAction.None;
        }
    }
}
