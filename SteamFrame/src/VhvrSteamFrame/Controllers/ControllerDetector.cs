using System;
using System.Linq;
using System.Text;
using Valve.VR;

namespace VhvrSteamFrame
{
    /// <summary>
    /// Asks SteamVR which controllers are in each hand and picks the matching <see cref="ControllerProfile"/>.
    /// Polls until both hands are known (controllers can connect late), then keeps checking slowly in case they change.
    /// Never changes anything in SteamVR.
    ///
    /// All-headset modules ask <see cref="ActiveProfile"/> and never name a headset themselves.
    /// </summary>
    internal static class ControllerDetector
    {
        public enum Result
        {
            /// <summary>SteamVR hasn't reported a controller yet.</summary>
            Unknown,
            /// <summary>A controller with a profile is in at least one hand.</summary>
            Recognized,
            /// <summary>Controllers are connected, but none has a profile.</summary>
            Unrecognized,
        }

        public static Result State { get; private set; } = Result.Unknown;
        public static string LeftType { get; private set; }
        public static string RightType { get; private set; }

        /// <summary>The profile of the controllers SteamVR reports, or null.</summary>
        public static ControllerProfile DetectedProfile { get; private set; }

        /// <summary>
        /// The profile used when the player forces controller-specific behaviour ([General] FrameDetection = Force, or
        /// [Prompts] ButtonLabels = Frame). Both settings predate profiles and mean the Steam Frame.
        /// </summary>
        public static ControllerProfile ForcedProfile
        {
            get { return SteamFrameProfile.Instance; }
        }

        /// <summary>
        /// The profile controller-specific behaviour should use right now, or null for none.
        /// [General] FrameDetection: Auto = what SteamVR reports, Force = the forced profile, Off = none.
        /// </summary>
        public static ControllerProfile ActiveProfile
        {
            get
            {
                string mode = Plugin.FrameDetection?.Value ?? "Auto";
                if (mode == "Force") return ForcedProfile;
                if (mode == "Off") return null;
                return DetectedProfile;
            }
        }

        public static event Action Changed;

        private static float nextPoll;
        private static bool warned;

        internal static void Tick()
        {
            float now = UnityEngine.Time.unscaledTime;
            if (now < nextPoll) return;
            nextPoll = now + (State == Result.Unknown ? 2f : 15f);

            string left = Read(ETrackedControllerRole.LeftHand);
            string right = Read(ETrackedControllerRole.RightHand);
            if (left == null && right == null) return;

            var profile = ControllerProfiles.ForControllerType(left) ?? ControllerProfiles.ForControllerType(right);
            var state = profile != null ? Result.Recognized : Result.Unrecognized;
            if (state == State && profile == DetectedProfile && left == LeftType && right == RightType) return;

            State = state;
            DetectedProfile = profile;
            LeftType = left;
            RightType = right;
            Plugin.Log.LogInfo(profile != null
                ? $"{profile.DisplayName} detected (left: {left ?? "none"}, right: {right ?? "none"})."
                : $"{KnownNames()} not detected (left: {left ?? "none"}, right: {right ?? "none"}); Frame-only labels stay off unless FrameDetection = Force.");
            Changed?.Invoke();
        }

        private static string KnownNames()
        {
            return string.Join(" / ", ControllerProfiles.All.Select(p => p.DisplayName));
        }

        private static string Read(ETrackedControllerRole role)
        {
            try
            {
                var system = OpenVR.System;
                if (system == null) return null;
                uint index = system.GetTrackedDeviceIndexForControllerRole(role);
                if (index == OpenVR.k_unTrackedDeviceIndexInvalid) return null;

                var error = ETrackedPropertyError.TrackedProp_Success;
                var sb = new StringBuilder(64);
                system.GetStringTrackedDeviceProperty(index, ETrackedDeviceProperty.Prop_ControllerType_String, sb, (uint)sb.Capacity, ref error);
                return error == ETrackedPropertyError.TrackedProp_Success ? sb.ToString() : null;
            }
            catch (Exception e)
            {
                if (!warned)
                {
                    warned = true;
                    Plugin.Log.LogWarning($"Couldn't ask SteamVR for the controller type ({e.GetType().Name}); set FrameDetection = Force if you use a Steam Frame.");
                }
                return null;
            }
        }
    }
}
