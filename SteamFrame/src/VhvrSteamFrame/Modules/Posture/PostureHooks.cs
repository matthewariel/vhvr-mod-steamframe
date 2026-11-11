using System;
using System.Reflection;
using BepInEx.Configuration;
using HarmonyLib;

namespace VhvrSteamFrame
{
    /// <summary>
    /// Seated / standing play. A postfix on VHVR's VRPlayer.referencePlayerHeight swaps in your standing eye height when
    /// you're seated (PostureCalibrator decides), and a light poll notices each new VHVR height measurement (recenter pose,
    /// Home key, game start, or SteamVR's Steam-button recenter via SteamVRRecenter) to learn the standing height and
    /// tell you which posture was detected.
    /// </summary>
    internal class PostureHooks : Module
    {
        public override string Name => "Posture";

        internal static ConfigEntry<string> Mode;
        internal static ConfigEntry<float> StandingEyeHeight;
        internal static ConfigEntry<float> SeatedEyeHeight;
        internal static ConfigEntry<float> SeatedBelowPercent;
        internal static ConfigEntry<bool> RecalibrateOnSteamVRRecenter;
        internal static ConfigEntry<bool> LogSteamVREvents;

        /// <summary>Latest VHVR measurement and what it was classified as (for the settings tab); NaN until the first one.</summary>
        internal static float LastMeasured = float.NaN;
        internal static Posture LastPosture;

        // Cached for the postfix, which runs several times per frame.
        private static PostureMode mode;
        private static float standingEyeHeight, seatedEyeHeight, seatedBelowFraction;

        private FieldInfo rawEyeHeightField;
        private MethodInfo referenceHeightGetter;
        private bool recenterHookReady;
        private float lastRaw = float.NaN, nextRead;
        private Posture? lastPosture;

        internal static float Threshold => PostureCalibrator.SeatedThreshold(standingEyeHeight, seatedEyeHeight, seatedBelowFraction);

        public override void BindConfig(ConfigFile config)
        {
            Mode = config.Bind("Posture", "Mode", "Auto", new ConfigDescription(
                "Auto: when you recenter while seated (eye height well below your standing height), play at your standing height; " +
                "recenter standing to switch back. Standing: VHVR's normal behaviour (your view is at your real eye height). " +
                "Seated: always play at your standing height.",
                new AcceptableValueList<string>("Auto", "Standing", "Seated")));
            StandingEyeHeight = config.Bind("Posture", "StandingEyeHeight", 0f, new ConfigDescription(
                "Your standing eye height in metres. Seated play uses it as your in-game height. Updated each time you recenter " +
                "standing; set it by hand if it's wrong. 0 = not learned yet.",
                new AcceptableValueRange<float>(0f, 2.5f)));
            SeatedEyeHeight = config.Bind("Posture", "SeatedEyeHeight", 0f, new ConfigDescription(
                "Your seated eye height in metres. Updated each time you recenter seated; set it by hand if Auto mode mixes up " +
                "sitting and standing. With both heights known, anything below halfway between them counts as seated. 0 = not learned yet.",
                new AcceptableValueRange<float>(0f, 2.0f)));
            SeatedBelowPercent = config.Bind("Posture", "SeatedBelowPercent", 80f, new ConfigDescription(
                "Until your seated height is known, Auto mode counts you as seated when a recenter measures your eyes below this " +
                "percentage of your standing eye height.",
                new AcceptableValueRange<float>(50f, 95f)));
            RecalibrateOnSteamVRRecenter = config.Bind("Posture", "RecalibrateOnSteamVRRecenter", true,
                "When you recenter with SteamVR (hold the Steam button), also do VHVR's recenter: re-measure your eye height and " +
                "re-centre your view. Without it only VHVR's own recenter (game start, Home key, hands-to-face pose) measures height.");
            LogSteamVREvents = config.Bind("Posture", "LogSteamVREvents", true,
                "Log each kind of SteamVR event once per session (a few dozen lines at most), to see what the headset sends when you recenter.");

            Mode.SettingChanged += (_, __) => Cache();
            StandingEyeHeight.SettingChanged += (_, __) => Cache();
            SeatedEyeHeight.SettingChanged += (_, __) => Cache();
            SeatedBelowPercent.SettingChanged += (_, __) => Cache();
            Cache();
        }

        private static void Cache()
        {
            mode = PostureCalibrator.ParseMode(Mode.Value);
            standingEyeHeight = StandingEyeHeight.Value;
            seatedEyeHeight = SeatedEyeHeight.Value;
            seatedBelowFraction = SeatedBelowPercent.Value / 100f;
        }

        protected override void Start(Harmony harmony)
        {
            var vrPlayer = Vhvr.Type("ValheimVRMod.VRCore.VRPlayer");
            rawEyeHeightField = Vhvr.Field(vrPlayer, "rawCaliberatedEyeHeight");
            referenceHeightGetter = Vhvr.Getter(vrPlayer, "referencePlayerHeight");
            harmony.Patch(referenceHeightGetter, postfix: new HarmonyMethod(typeof(PostureHooks), nameof(ReferenceHeightPostfix)));

            try
            {
                SteamVRRecenter.Init(harmony);
                recenterHookReady = true;
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Posture: can't follow SteamVR recenters ({e.Message}); VHVR's recenter pose still works.");
            }

            Plugin.Log.LogInfo($"Posture: mode {Mode.Value}, standing eye height {Metres(standingEyeHeight)}, seated eye height " +
                               $"{Metres(seatedEyeHeight)}, seated below {Threshold:F2} m.");
        }

        internal static string Metres(float height) => height > 0f ? height.ToString("F2") + " m" : "not learned yet";

        public static void ReferenceHeightPostfix(ref float __result)
        {
            var posture = PostureCalibrator.Classify(__result, mode, standingEyeHeight, seatedEyeHeight, seatedBelowFraction);
            float reference = PostureCalibrator.ReferenceOverride(posture, standingEyeHeight);
            if (reference > 0f) __result = reference;
        }

        protected override void Tick()
        {
            if (recenterHookReady) SteamVRRecenter.Tick();

            float now = UnityEngine.Time.unscaledTime;
            if (now < nextRead) return;
            nextRead = now + 0.25f;

            float raw = (float)rawEyeHeightField.GetValue(null);
            if (raw < 0.3f || (!float.IsNaN(lastRaw) && Math.Abs(raw - lastRaw) < 0.005f)) return;
            lastRaw = raw;
            OnMeasured(raw);
        }

        private void OnMeasured(float raw)
        {
            float threshold = Threshold;
            var posture = PostureCalibrator.Classify(raw, mode, standingEyeHeight, seatedEyeHeight, seatedBelowFraction);
            if (PostureCalibrator.ShouldLearn(posture, mode, raw) && Math.Abs(raw - standingEyeHeight) > 0.01f)
                StandingEyeHeight.Value = (float)Math.Round(raw, 2); // saves the config, refreshes the cache
            else if (PostureCalibrator.ShouldLearnSeated(posture, mode, raw, standingEyeHeight) && Math.Abs(raw - seatedEyeHeight) > 0.01f)
                SeatedEyeHeight.Value = (float)Math.Round(raw, 2);
            LastMeasured = raw;
            LastPosture = posture;

            float view = (float)referenceHeightGetter.Invoke(null, null);
            Plugin.Log.LogInfo($"Height: VHVR measured your eye height at {raw:F2} m -> {posture} ({Mode.Value} mode, seated below " +
                               $"{threshold:F2} m); view height {view:F2} m; standing {Metres(standingEyeHeight)}, seated {Metres(seatedEyeHeight)}.");

            if (posture == Posture.Seated)
            {
                Hud.Notice(standingEyeHeight > 0
                    ? $"Seated: keeping your standing height ({standingEyeHeight:F2} m)."
                    : "Seated, but your standing height isn't known yet: stand up and recenter once.");
            }
            else if (lastPosture == Posture.Seated)
            {
                Hud.Notice($"Standing: height {raw:F2} m.");
            }
            lastPosture = posture;
        }
    }
}
