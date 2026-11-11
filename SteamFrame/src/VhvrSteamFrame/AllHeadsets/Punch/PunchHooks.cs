using System;
using System.Reflection;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using ValheimVRMod.Scripts;
using ValheimVRMod.VRCore.Backends;

namespace VhvrSteamFrame
{
    /// <summary>
    /// Bare-hand punching. Two parts:
    ///  - a postfix on VHVR's FistCollision.canAttackWithCollision (the gate both of VHVR's own hit paths ask) that applies
    ///    the RequireGrip / RequireTrigger / RequireThumbOnStick checkboxes to a free hand;
    ///  - ThumbPunch, added beside each FistCollision, which lands the hits VHVR skips (trees, rocks, ... without trigger).
    /// FistCollision.OnTriggerEnter itself can't be patched (it reads a private Valheim field), hence the companion component.
    /// </summary>
    internal class PunchHooks : Module
    {
        public override string Name => "Punch";
        public override ModuleScope Scope => ModuleScope.AllHeadsets;

        internal static ConfigEntry<bool> RequireGrip, RequireTrigger, RequireThumbOnStick, Diagnostics;
        internal static readonly PunchRequirements Requirements = new PunchRequirements();

        internal static FieldInfo HandGestureField, LastGrabbedField, IsRightHandField;
        internal static Func<FistCollision, VRInputSource> InputSource;
        internal static MethodInfo InFirstPerson;

        /// <summary>Why the gate last refused each hand (0 = left, 1 = right), for diagnostics.</summary>
        internal static readonly string[] LastRefusal = new string[2];

        private static bool warnedNoThumbSensing;
        private static float thumbInactiveSince = -1f;
        private bool warnedLoose, extraHitsReady;
        private float nextFistScan;

        public override void BindConfig(ConfigFile config)
        {
            const string loose = " Fewer requirements = more accidental punches when grabbing or touching things.";
            RequireGrip = config.Bind("Punch", "RequireGrip", true,
                "A bare-hand punch needs the grip (side button) held." + loose);
            RequireTrigger = config.Bind("Punch", "RequireTrigger", false,
                "A bare-hand punch needs the trigger held (VHVR's default asks for grip + trigger on non-creatures)." + loose);
            RequireThumbOnStick = config.Bind("Punch", "RequireThumbOnStick", true,
                "A bare-hand punch needs your thumb resting on that hand's thumbstick (Frame touch sensor). " +
                "Uses Bindings.ThumbTouchSensing; ignored if that's off." + loose);
            Diagnostics = config.Bind("Punch", "Diagnostics", false,
                "While a grip is held, log why each fist contact did or didn't become a punch (LogOutput.log, a few lines per second at most).");

            RequireGrip.SettingChanged += (_, __) => OnRequirementsChanged();
            RequireTrigger.SettingChanged += (_, __) => OnRequirementsChanged();
            RequireThumbOnStick.SettingChanged += (_, __) => OnRequirementsChanged();
            ReadRequirements();
        }

        protected override void Start(Harmony harmony)
        {
            HandGestureField = Vhvr.Field(VhvrApi.FistHandGesture);
            LastGrabbedField = Vhvr.Field(VhvrApi.FistLastGrabbedType);
            IsRightHandField = Vhvr.Field(VhvrApi.FistIsRightHand);
            InputSource = Vhvr.Delegate<Func<FistCollision, VRInputSource>>(Vhvr.Method(VhvrApi.FistInputSource));
            InFirstPerson = Vhvr.Method(VhvrApi.VrPlayerInFirstPerson);

            harmony.Patch(Vhvr.Method(VhvrApi.FistCanAttack),
                postfix: new HarmonyMethod(typeof(PunchHooks), nameof(CanAttackPostfix)));

            try { extraHitsReady = ThumbPunch.Init(); }
            catch (Exception e) { Plugin.Log.LogWarning($"Punch: extra hits off ({e.Message}); the checkboxes still apply to VHVR's hits."); }

            Plugin.Log.LogInfo($"Punch: needs {Describe(Requirements)}.");
        }

        protected override void Tick()
        {
            if (!warnedLoose && Player.m_localPlayer != null)
            {
                warnedLoose = true;
                var warning = PunchPolicy.Warning(Requirements);
                if (warning != null) Hud.Notice("Punch settings: " + warning);
            }

            if (!extraHitsReady || Time.unscaledTime < nextFistScan) return;
            nextFistScan = Time.unscaledTime + 1f;
            foreach (var f in UnityEngine.Object.FindObjectsByType<FistCollision>(FindObjectsSortMode.None))
            {
                if (f.GetComponent<ThumbPunch>() == null)
                    f.gameObject.AddComponent<ThumbPunch>().fist = f;
            }
        }

        private void OnRequirementsChanged()
        {
            ReadRequirements();
            Plugin.Log.LogInfo($"Punch: now needs {Describe(Requirements)}.");
            var warning = PunchPolicy.Warning(Requirements);
            if (warning != null) Hud.Notice("Punch settings: " + warning);
        }

        private static void ReadRequirements()
        {
            Requirements.Grip = RequireGrip.Value;
            Requirements.Trigger = RequireTrigger.Value;
            Requirements.ThumbOnStick = RequireThumbOnStick.Value;
        }

        internal static string Describe(PunchRequirements r)
        {
            if (r.Count == 0) return "nothing held (any contact)";
            var parts = new System.Collections.Generic.List<string>();
            if (r.Grip) parts.Add("grip");
            if (r.Trigger) parts.Add("trigger");
            if (r.ThumbOnStick) parts.Add("thumb on stick");
            return string.Join(" + ", parts);
        }

        // ---- The gate ----

        public static void CanAttackPostfix(FistCollision __instance, ref bool __result)
        {
            try
            {
                var gesture = HandGestureField.GetValue(__instance) as HandGesture;
                if (gesture == null || !gesture.isHandFree()) return; // weapon, tool or shield in hand: VHVR's rule as-is

                var src = InputSource(__instance);
                int hand = src == VRInputSource.RightHand ? 1 : 0;
                bool grip = VRInputActions.valheim_Grab.GetState(src);
                bool trigger = VRInputActions.valheim_Use.GetState(src);
                bool thumb = ThumbOnStick(src);

                string missing = PunchPolicy.MissingInput(Requirements, grip, trigger, thumb);
                if (missing != null)
                {
                    __result = false;
                    LastRefusal[hand] = "needs " + missing;
                    return;
                }

                // VHVR always wants grip for a free hand; when grip isn't required, allow it if VHVR's other conditions hold.
                if (!__result && !grip && !Requirements.Grip) __result = BaseConditionsHold(__instance);
                LastRefusal[hand] = __result ? null : BaseRefusal(__instance);
            }
            catch
            {
                // Never break VHVR's combat over a diagnostics/reflection hiccup.
            }
        }

        /// <summary>Thumb resting on the stick (Frame touch sensor, bound to VHVR's otherwise unused Teleport action).
        /// If that binding is missing the requirement can't be checked, so it counts as met.</summary>
        internal static bool ThumbOnStick(VRInputSource src)
        {
            var thumb = VRInputActions.default_Teleport;
            if (thumb == null || !thumb.activeBinding)
            {
                // SteamVR reports no binding for a while after startup and while its dashboard is open; only warn when
                // it stays missing (the binding really isn't there).
                float now = UnityEngine.Time.unscaledTime;
                if (thumbInactiveSince < 0f) thumbInactiveSince = now;
                if (!warnedNoThumbSensing && Requirements.ThumbOnStick && now - thumbInactiveSince > 30f)
                {
                    warnedNoThumbSensing = true;
                    Hud.Notice("Punch: thumb-on-stick can't be sensed (Bindings.ThumbTouchSensing off, or restart needed), so it's ignored.");
                }
                return true;
            }
            thumbInactiveSince = -1f;
            return thumb.GetState(src);
        }

        internal static bool ThumbSensed(VRInputSource src)
        {
            var thumb = VRInputActions.default_Teleport;
            return thumb != null && thumb.activeBinding && thumb.GetState(src);
        }

        private static bool BaseConditionsHold(FistCollision f)
        {
            return (bool)InFirstPerson.Invoke(null, null) && f.transform.parent != null && Convert.ToInt32(LastGrabbedField.GetValue(f)) == 0;
        }

        internal static string BaseRefusal(FistCollision f)
        {
            try
            {
                if (!(bool)InFirstPerson.Invoke(null, null)) return "third person view";
                if (f.transform.parent == null) return "hand not attached to the body yet";
                var grabbed = LastGrabbedField.GetValue(f);
                if (Convert.ToInt32(grabbed) != 0) return $"hand is holding on to {grabbed} (release grip to reset)";
            }
            catch { /* diagnostics only */ }
            return "VHVR refused";
        }
    }
}
