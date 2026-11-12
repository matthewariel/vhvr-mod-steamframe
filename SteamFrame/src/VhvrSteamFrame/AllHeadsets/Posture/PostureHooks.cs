using System;
using System.Reflection;
using BepInEx.Configuration;
using HarmonyLib;

namespace VhvrSteamFrame
{
    /// <summary>
    /// Seated / standing play. You set your standing and seated eye heights once with the buttons on the settings tab
    /// (<see cref="SetHeightFromHeadset"/> reads the headset). <see cref="PostureTracker"/> decides the posture at each
    /// VHVR height measurement (recenter pose, Home key, game start, or the Steam-button recenter via SteamVRRecenter) and
    /// follows your head between them, and a postfix on VHVR's VRPlayer.referencePlayerHeight hands VHVR the reference
    /// height that keeps your in-game eyes at your standing height. Recenters never change the saved heights.
    ///
    /// SteamVR's floor level isn't stable on the Steam Frame (the headset read ~1.5 m standing one day and 2.0 m the
    /// next). So the saved heights carry the SteamVR floor reference they were set against, and they're moved with it
    /// when it changes; a sudden jump of the headset at a SteamVR recenter is treated the same way.
    /// </summary>
    internal class PostureHooks : Module
    {
        public override string Name => "Posture";
        public override ModuleScope Scope => ModuleScope.AllHeadsets;

        internal static ConfigEntry<string> Mode;
        internal static ConfigEntry<float> StandingEyeHeight;
        internal static ConfigEntry<float> SeatedEyeHeight;
        internal static ConfigEntry<float> FloorReference;
        internal static ConfigEntry<bool> FollowHead;
        internal static ConfigEntry<bool> RecalibrateOnSteamVRRecenter;
        internal static ConfigEntry<bool> LogSteamVREvents;

        /// <summary>Latest VHVR measurement and what it was classified as (for the settings tab); NaN until the first one.</summary>
        internal static float LastMeasured = float.NaN;
        internal static Posture LastPosture;

        private static readonly PostureTracker tracker = new PostureTracker();
        internal static Posture CurrentPosture => tracker.Current;
        /// <summary>Today's standing / seated heights: the saved ones moved by any SteamVR floor change since they were set.</summary>
        internal static float EffectiveStanding => tracker.Standing;
        internal static float EffectiveSeated => tracker.Seated;
        internal static float Split => PostureCalibrator.Split(tracker.Standing, tracker.Seated);
        internal static bool Ready => tracker.Ready;

        private static FieldInfo rawEyeHeightField;
        private static MethodInfo referenceHeightGetter, minEyeHeight, maxEyeHeight, roomscaleSneaking;
        private static float vhvrMin = 0f, vhvrMax = 0f;

        // The postfix runs several times per frame: it reads these, Tick writes them.
        private static float vhvrReference = float.NaN, shownReference = float.NaN;
        private static bool overriding;

        private bool recenterHookReady;
        private float lastRaw = float.NaN, nextRead, lastHead = float.NaN, lastFloor = float.NaN;
        private float jumpAt = -100f, jumpBy;
        private bool remindedToSet, snapNext = true;
        private Posture? lastAnnounced;
        private static bool warnedHeadsetRead;
        private static readonly Valve.VR.TrackedDevicePose_t[] poses = new Valve.VR.TrackedDevicePose_t[1];

        public override void BindConfig(ConfigFile config)
        {
            Mode = config.Bind("Posture", "Mode", "Auto", new ConfigDescription(
                "Auto: with both heights set, you play at your standing height whether you sit or stand; it follows you as you " +
                "sit down or stand up, and each recenter checks again. Standing: VHVR's normal behaviour (your view is at your " +
                "real eye height). Seated: always play as if seated at your standing height.",
                new AcceptableValueList<string>("Auto", "Standing", "Seated")));
            // New keys in 0.4.0: the old StandingEyeHeight / SeatedEyeHeight were learned from recenters and could be wrong
            // (a 1.98 m glitch made every standing recenter count as seated), so they're not carried over.
            StandingEyeHeight = config.Bind("Posture", "StandingHeight", 0f, new ConfigDescription(
                "Your standing eye height in metres, as SteamVR measured it. Set it with \"Set standing height\" (Settings > VHVR > " +
                "Steam Frame > III Height) while standing up straight. Recenters never change it. 0 = not set.",
                new AcceptableValueRange<float>(0f, 2.2f)));
            SeatedEyeHeight = config.Bind("Posture", "SeatedHeight", 0f, new ConfigDescription(
                "Your seated eye height in metres, as SteamVR measured it. Set it with \"Set seated height\" while sitting as you play. " +
                "Recenters never change it. 0 = not set.",
                new AcceptableValueRange<float>(0f, 2.2f)));
            FloorReference = config.Bind("Posture", "FloorReference", 0f,
                "Written by the Set buttons: SteamVR's floor reference when the heights were set, so they can be moved with " +
                "SteamVR's floor when it changes. Don't edit. 0 = unknown.");
            FollowHead = config.Bind("Posture", "SwitchWhenYouSitOrStand", true,
                "Auto mode: switch between seated and standing as soon as you sit down or stand up (after about a second), " +
                "instead of only when you recenter.");
            ReportOldLearnedHeights(config);
            RecalibrateOnSteamVRRecenter = config.Bind("Posture", "RecalibrateOnSteamVRRecenter", true,
                "When you recenter with SteamVR (hold the Steam button), also do VHVR's recenter: re-measure your eye height and " +
                "re-centre your view. Without it only VHVR's own recenter (game start, Home key, hands-to-face pose) measures height.");
            LogSteamVREvents = config.Bind("Posture", "LogSteamVREvents", true,
                "Log each kind of SteamVR event once per session (a few dozen lines at most), to see what the headset sends when you recenter.");

            Mode.SettingChanged += (_, __) => LoadSettings();
            StandingEyeHeight.SettingChanged += (_, __) => LoadSettings();
            SeatedEyeHeight.SettingChanged += (_, __) => LoadSettings();
            FollowHead.SettingChanged += (_, __) => LoadSettings();
            LoadSettings();
        }

        /// <summary>Settings -> tracker, with the saved heights moved by any SteamVR floor change since they were set.</summary>
        private static void LoadSettings()
        {
            tracker.Mode = PostureCalibrator.ParseMode(Mode.Value);
            tracker.FollowHead = FollowHead.Value;
            float shift = FloorChangeSinceSet() + jumpShift;
            tracker.Standing = StandingEyeHeight.Value > 0f ? StandingEyeHeight.Value + shift : 0f;
            tracker.Seated = SeatedEyeHeight.Value > 0f ? SeatedEyeHeight.Value + shift : 0f;
            tracker.Reclassify();
        }

        /// <summary>Floor moves this session that SteamVR's floor reference didn't show (headset jumps at a recenter).</summary>
        private static float jumpShift;

        private static float FloorChangeSinceSet()
        {
            float now = FloorNow();
            float then = FloorReference.Value;
            return then != 0f && !float.IsNaN(now) ? now - then : 0f;
        }

        protected override void Start(Harmony harmony)
        {
            rawEyeHeightField = Vhvr.Field(VhvrApi.VrPlayerRawEyeHeight);
            referenceHeightGetter = Vhvr.Method(VhvrApi.VrPlayerReferenceHeight);
            minEyeHeight = Vhvr.TryMethod(VhvrApi.ConfigPlayerMinEyeHeight);
            maxEyeHeight = Vhvr.TryMethod(VhvrApi.ConfigPlayerMaxEyeHeight);
            roomscaleSneaking = Vhvr.TryMethod(VhvrApi.PostureIsRoomscaleSneaking);
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

            ReadVhvrLimits();
            LoadSettings();
            float shift = FloorChangeSinceSet();
            Plugin.Log.LogInfo($"Posture: mode {Mode.Value}, standing eye height {Metres(StandingEyeHeight.Value)}, seated eye height " +
                               $"{Metres(SeatedEyeHeight.Value)}" +
                               (Math.Abs(shift) > 0.005f ? $" (SteamVR's floor moved {shift:+0.00;-0.00} m since they were set: today {Metres(tracker.Standing)} / {Metres(tracker.Seated)})" : "") +
                               $", follow head {(FollowHead.Value ? "on" : "off")}, VHVR eye height range {vhvrMin:F2}-{vhvrMax:F2} m, " +
                               $"SteamVR floor reference {FloorNow():F3}.");
        }

        internal static string Metres(float height) => height > 0f ? height.ToString("F2") + " m" : "not set";

        private static void ReadVhvrLimits()
        {
            try
            {
                if (minEyeHeight != null) vhvrMin = (float)minEyeHeight.Invoke(null, null);
                if (maxEyeHeight != null) vhvrMax = (float)maxEyeHeight.Invoke(null, null);
            }
            catch { /* VHVR's config not ready yet: try again next time */ }
        }

        /// <summary>Logs (once) the heights 0.3.x learned under the old keys, which 0.4.0 no longer uses, and drops them from the file.</summary>
        private static void ReportOldLearnedHeights(ConfigFile config)
        {
            try
            {
                var orphans = AccessTools.Property(typeof(ConfigFile), "OrphanedEntries")?.GetValue(config) as
                    System.Collections.Generic.Dictionary<ConfigDefinition, string>;
                if (orphans == null) return;
                var oldStanding = new ConfigDefinition("Posture", "StandingEyeHeight");
                var oldSeated = new ConfigDefinition("Posture", "SeatedEyeHeight");
                var oldPercent = new ConfigDefinition("Posture", "SeatedBelowPercent");
                bool hadStanding = orphans.TryGetValue(oldStanding, out var standing);
                bool hadSeated = orphans.TryGetValue(oldSeated, out var seated);
                bool hadPercent = orphans.Remove(oldPercent);
                if (!hadStanding && !hadSeated && !hadPercent) return;
                orphans.Remove(oldStanding);
                orphans.Remove(oldSeated);
                config.Save();
                if (hadStanding || hadSeated)
                    Plugin.Log.LogInfo($"Posture: heights are now set with buttons on the Steam Frame tab, not learned from recenters. " +
                                       $"The old learned values (standing {standing ?? "-"} m, seated {seated ?? "-"} m) are no longer used.");
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Posture: couldn't tidy the old learned heights: {e.Message}");
            }
        }

        // ---- SteamVR readings ----

        /// <summary>
        /// The headset's height above the SteamVR floor right now: the same quantity VHVR measures on a recenter (SteamVR's
        /// standing-universe HMD height). NaN when it can't be read.
        /// </summary>
        internal static float HeadsetEyeHeight()
        {
            try
            {
                var system = Valve.VR.OpenVR.System;
                if (system != null)
                {
                    system.GetDeviceToAbsoluteTrackingPose(Valve.VR.ETrackingUniverseOrigin.TrackingUniverseStanding, 0f, poses);
                    if (poses[0].bPoseIsValid && poses[0].bDeviceIsConnected) return poses[0].mDeviceToAbsoluteTracking.m7;
                }
            }
            catch (Exception e)
            {
                if (!warnedHeadsetRead)
                {
                    warnedHeadsetRead = true;
                    Plugin.Log.LogWarning($"Posture: couldn't read the headset's height from SteamVR: {e.Message}");
                }
            }
            return float.NaN;
        }

        /// <summary>
        /// SteamVR's floor reference: the height part of its raw-tracking-to-standing transform. When SteamVR moves the floor,
        /// this changes by as much as the headset's height reading does. NaN when it can't be read.
        /// </summary>
        private static float FloorNow()
        {
            try
            {
                var system = Valve.VR.OpenVR.System;
                if (system != null)
                {
                    float y = system.GetRawZeroPoseToStandingAbsoluteTrackingPose().m7;
                    return y == 0f ? 0.0001f : y; // 0 is "unknown" in the saved setting
                }
            }
            catch { }
            return float.NaN;
        }

        // ---- Set buttons ----

        /// <summary>
        /// Saves the headset's current height as your standing or seated eye height (the Set buttons on the settings tab).
        /// Returns the message to show; <paramref name="saved"/> is the saved height, or NaN when the reading was refused.
        /// </summary>
        internal static string SetHeightFromHeadset(Posture which, out float saved)
        {
            saved = float.NaN;
            float reading = HeadsetEyeHeight();
            string what = which == Posture.Standing ? "standing" : "seated";
            string problem = PostureCalibrator.RejectReason(which, reading);
            if (problem != null)
            {
                Plugin.Log.LogInfo($"Height: set {what} height refused: {problem}");
                return problem;
            }

            // Both heights are saved against today's floor reference: the other one is moved to it first.
            float shift = FloorChangeSinceSet() + jumpShift;
            float floor = FloorNow();
            saved = (float)Math.Round(reading, 2);
            float other = which == Posture.Standing ? SeatedEyeHeight.Value : StandingEyeHeight.Value;
            if (other > 0f) other = (float)Math.Round(other + shift, 2);
            jumpShift = 0f; // both heights are in today's floor level from here on
            FloorReference.Value = float.IsNaN(floor) ? 0f : floor;
            if (which == Posture.Standing) { SeatedEyeHeight.Value = other; StandingEyeHeight.Value = saved; }
            else { StandingEyeHeight.Value = other; SeatedEyeHeight.Value = saved; }
            LoadSettings();

            string message = $"{(which == Posture.Standing ? "Standing" : "Seated")} height set: {saved:F2} m.";
            string gap = PostureCalibrator.GapWarning(tracker.Standing, tracker.Seated);
            Plugin.Log.LogInfo($"Height: {what} height set to {saved:F2} m from the headset; standing {Metres(tracker.Standing)}, " +
                               $"seated {Metres(tracker.Seated)}, floor reference {floor:F3}.{(gap != null ? " " + gap : "")}");
            Hud.Notice(message);
            return gap != null ? message + " " + gap : message;
        }

        // ---- VHVR's reference height ----

        public static void ReferenceHeightPostfix(ref float __result)
        {
            vhvrReference = __result;
            if (overriding && !float.IsNaN(shownReference)) __result = shownReference;
        }

        protected override void Tick()
        {
            if (recenterHookReady) SteamVRRecenter.Tick();
            float now = UnityEngine.Time.unscaledTime;

            float head = HeadsetEyeHeight();
            FollowFloor(head, now);

            if (now >= nextRead)
            {
                nextRead = now + 0.25f;
                ReadVhvrLimits();
                float raw = (float)rawEyeHeightField.GetValue(null);
                if (raw >= 0.3f && (float.IsNaN(lastRaw) || Math.Abs(raw - lastRaw) >= 0.005f))
                {
                    lastRaw = raw;
                    OnMeasured(raw);
                }
            }

            bool ducking = false;
            try { ducking = roomscaleSneaking != null && (bool)roomscaleSneaking.Invoke(null, null); } catch { }
            if (tracker.Update(head, now, ducking))
            {
                Plugin.Log.LogInfo($"Height: you {(tracker.Current == Posture.Standing ? "stood up" : "sat down")} " +
                                   $"(head {head:F2} m, level {tracker.Level(head):F2} between seated 0 and standing 1).");
                Announce();
            }

            UpdateReference(UnityEngine.Time.unscaledDeltaTime);
        }

        /// <summary>Moves the shown reference height toward the tracker's, quickly but without a jarring jump.</summary>
        private void UpdateReference(float dt)
        {
            float target = tracker.Reference(vhvrMin, vhvrMax);
            bool wanted = target > 0f;
            if (!wanted) target = vhvrReference;
            if (float.IsNaN(target)) { overriding = false; return; }
            if (snapNext || float.IsNaN(shownReference))
            {
                shownReference = target;
                snapNext = false;
            }
            else
            {
                float step = 1.5f * dt; // m/s: a seated <-> standing change of ~0.4 m takes about a quarter of a second
                shownReference = Math.Abs(target - shownReference) <= step ? target : shownReference + Math.Sign(target - shownReference) * step;
            }
            overriding = wanted || Math.Abs(shownReference - vhvrReference) > 0.001f;
        }

        /// <summary>Keeps the heights in step with SteamVR's floor: its floor reference, or a headset jump at a SteamVR recenter.</summary>
        private void FollowFloor(float head, float now)
        {
            float floor = FloorNow();
            float floorMoved = 0f;
            if (!float.IsNaN(floor) && float.IsNaN(lastFloor))
            {
                // First reading (SteamVR usually isn't up when the plugin starts): apply any floor change since the Set.
                lastFloor = floor;
                LoadSettings();
                float since = FloorChangeSinceSet();
                Plugin.Log.LogInfo($"Height: SteamVR floor reference {floor:F3}" +
                                   (FloorReference.Value != 0f ? $", {since:+0.00;-0.00} m since the heights were set" : "") +
                                   $"; heights today: standing {Metres(tracker.Standing)}, seated {Metres(tracker.Seated)}.");
            }
            if (!float.IsNaN(floor) && !float.IsNaN(lastFloor) && Math.Abs(floor - lastFloor) > 0.01f)
            {
                floorMoved = floor - lastFloor;
                tracker.OnFloorShift(floorMoved);
                Plugin.Log.LogInfo($"Height: SteamVR's floor reference moved {floorMoved:+0.00;-0.00} m; heights today: standing " +
                                   $"{Metres(tracker.Standing)}, seated {Metres(tracker.Seated)}.");
            }
            if (!float.IsNaN(floor)) lastFloor = floor;

            // A headset jump the floor reference doesn't explain, within a few seconds of a SteamVR recenter: SteamVR moved
            // the floor some other way. Nobody moves their head 15 cm in one frame.
            if (!float.IsNaN(head) && !float.IsNaN(lastHead))
            {
                float jump = head - lastHead - floorMoved;
                if (Math.Abs(jump) > 0.15f) { jumpAt = now; jumpBy = jump; }
            }
            if (!float.IsNaN(head)) lastHead = head;
            if (jumpAt > 0f && Math.Abs(SteamVRRecenter.LastEventAt - jumpAt) < 3f)
            {
                tracker.OnFloorShift(jumpBy);
                jumpShift += jumpBy;
                Plugin.Log.LogInfo($"Height: the headset jumped {jumpBy:+0.00;-0.00} m at a SteamVR recenter (floor reference unchanged), " +
                                   $"so SteamVR moved the floor; heights today: standing {Metres(tracker.Standing)}, seated {Metres(tracker.Seated)}.");
                jumpAt = -100f;
            }
            else if (jumpAt > 0f && now - jumpAt > 3f)
            {
                jumpAt = -100f; // no recenter around it: a tracking glitch, not the floor
            }
        }

        private void OnMeasured(float raw)
        {
            var posture = tracker.OnMeasured(raw);
            LastMeasured = raw;
            LastPosture = posture;
            snapNext = true; // VHVR re-centred the view anyway

            float reference = tracker.Reference(vhvrMin, vhvrMax);
            Plugin.Log.LogInfo($"Height: VHVR measured your eye height at {raw:F2} m -> {posture} ({Mode.Value} mode, " +
                               (tracker.Ready ? $"seated below {Split:F2} m" : "heights not set") +
                               $"); reference {(reference > 0f ? reference : vhvrReference):F2} m (VHVR's own {vhvrReference:F2}); " +
                               $"standing {Metres(tracker.Standing)}, seated {Metres(tracker.Seated)}.");

            if (!tracker.Ready && tracker.Mode == PostureMode.Auto)
            {
                if (!remindedToSet)
                {
                    remindedToSet = true;
                    Hud.Notice("Seated play: set your standing and seated heights in Settings > VHVR > Steam Frame > III Height.");
                }
                return;
            }
            Announce();
        }

        private void Announce()
        {
            if (tracker.Mode == PostureMode.Standing) return;
            var posture = tracker.Current;
            if (lastAnnounced == posture) return;
            if (posture == Posture.Seated) Hud.Notice("Seated: playing at your standing height.");
            else if (lastAnnounced == Posture.Seated) Hud.Notice("Standing.");
            lastAnnounced = posture;
        }
    }
}
