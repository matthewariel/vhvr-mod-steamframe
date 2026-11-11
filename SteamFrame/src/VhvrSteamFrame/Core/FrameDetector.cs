using System;
using System.Text;
using Valve.VR;

namespace VhvrSteamFrame
{
    /// <summary>Pure rule: which SteamVR controller type is a Steam Frame controller.</summary>
    public static class FrameControllers
    {
        /// <summary>SteamVR's controller_type for Steam Frame controllers (see VHVR's actions.json default_bindings).</summary>
        public const string ControllerType = "frame_controller";

        public static bool IsFrame(string controllerType)
        {
            return string.Equals(controllerType, ControllerType, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// Asks SteamVR which controllers are in each hand. Polls until both hands are known (controllers can connect late),
    /// then keeps checking slowly in case they change. Never changes anything in SteamVR.
    /// </summary>
    internal static class FrameDetector
    {
        public enum Result { Unknown, Frame, Other }

        public static Result State { get; private set; } = Result.Unknown;
        public static string LeftType { get; private set; }
        public static string RightType { get; private set; }

        /// <summary>True when Frame-specific behaviour should apply (detected, or forced in config).</summary>
        public static bool IsFrame
        {
            get
            {
                string mode = Plugin.FrameDetection?.Value ?? "Auto";
                if (mode == "Force") return true;
                if (mode == "Off") return false;
                return State == Result.Frame;
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

            var state = FrameControllers.IsFrame(left) || FrameControllers.IsFrame(right) ? Result.Frame : Result.Other;
            if (state == State && left == LeftType && right == RightType) return;

            State = state;
            LeftType = left;
            RightType = right;
            Plugin.Log.LogInfo(state == Result.Frame
                ? $"Steam Frame detected (left: {left ?? "none"}, right: {right ?? "none"})."
                : $"Steam Frame not detected (left: {left ?? "none"}, right: {right ?? "none"}); Frame-only labels stay off unless FrameDetection = Force.");
            Changed?.Invoke();
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
