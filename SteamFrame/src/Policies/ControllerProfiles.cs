using System.Collections.Generic;

namespace VhvrSteamFrame
{
    /// <summary>
    /// The list of known controllers. This is the one file that names every profile, so it sits outside the
    /// AllHeadsets / SteamFrame folders. To support another controller, write its profile class and add it here.
    /// </summary>
    public static class ControllerProfiles
    {
        public static readonly IReadOnlyList<ControllerProfile> All = new ControllerProfile[]
        {
            SteamFrameProfile.Instance,
        };

        /// <summary>The profile for a SteamVR controller_type, or null when the controller isn't known.</summary>
        public static ControllerProfile ForControllerType(string controllerType)
        {
            if (string.IsNullOrEmpty(controllerType)) return null;
            foreach (var profile in All)
                if (profile.Matches(controllerType)) return profile;
            return null;
        }
    }
}
