namespace VhvrSteamFrame
{
    public enum CrouchDecision { Allow, BlockInMenu, WaitForSecondClick }

    /// <summary>
    /// Whether a crouch toggle should go through. Plain C#, one instance per player.
    ///
    /// Valheim toggles crouch whenever Player.SetControls gets crouch = true; VHVR sets that on the crouch button's press
    /// (and to stand up when you start running). On the Frame the right stick click is both crouch and VHVR's "middle
    /// click" (move item / ping the map), so in chests and on the map every click also crouched you.
    /// </summary>
    public sealed class CrouchGate
    {
        public float DoubleClickWindow = 0.45f;

        private float lastPress = float.NegativeInfinity;

        /// <param name="toggleRequested">SetControls was asked to toggle crouch this update.</param>
        /// <param name="crouched">The player is crouched now (so the toggle would stand them up).</param>
        /// <param name="fromButton">The toggle came from the crouch button, not from physically ducking (roomscale sneak).</param>
        /// <param name="menuOpen">An inventory, chest, store, map, text box or the pause menu is open.</param>
        public CrouchDecision Decide(bool toggleRequested, bool crouched, bool fromButton, bool menuOpen,
            bool blockInMenus, bool requireDoubleClick, float now)
        {
            if (!toggleRequested) return CrouchDecision.Allow;
            if (blockInMenus && menuOpen) return CrouchDecision.BlockInMenu;

            // Standing up stays a single click (and running still stands you up): only going down needs the double click.
            if (!requireDoubleClick || !fromButton || crouched) return CrouchDecision.Allow;

            if (now - lastPress <= DoubleClickWindow)
            {
                lastPress = float.NegativeInfinity;
                return CrouchDecision.Allow;
            }
            lastPress = now;
            return CrouchDecision.WaitForSecondClick;
        }
    }
}
