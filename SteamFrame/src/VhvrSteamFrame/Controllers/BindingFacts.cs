using System;

namespace VhvrSteamFrame
{
    /// <summary>
    /// What the installed controller binding does, where that differs from VHVR's stock binding. A binding module
    /// (today only SteamFrame/Bindings) fills these in; all-headset modules read them without knowing who did.
    /// With no binding module, everything is "stock" (false).
    /// </summary>
    internal static class BindingFacts
    {
        /// <summary>The right stick click toggles crouch.</summary>
        public static Func<bool> CrouchOnStickClick = () => false;

        /// <summary>The left stick click toggles sprint.</summary>
        public static Func<bool> SprintOnStickClick = () => false;
    }
}
