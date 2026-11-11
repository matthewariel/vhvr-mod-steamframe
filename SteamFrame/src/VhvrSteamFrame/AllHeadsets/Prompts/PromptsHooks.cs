using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx.Configuration;
using HarmonyLib;

namespace VhvrSteamFrame
{
    /// <summary>
    /// Makes Valheim draw gamepad button prompts while VHVR motion controls are in use, labelled with the Frame buttons.
    ///
    /// Valheim picks keyboard vs gamepad prompts with ZInput.IsGamepadActive(), which is only true after real gamepad input
    /// (VHVR injects SteamVR actions by patching ZInput.GetButton*). Forcing it globally would flip ~70 gameplay/UI paths
    /// and fight VHVR's laser pointer, so only the IsGamepadActive() calls inside prompt-text methods are rewritten.
    /// </summary>
    internal class PromptsHooks : Module
    {
        public override string Name => "Prompts";
        public override ModuleScope Scope => ModuleScope.AllHeadsets;

        internal static ConfigEntry<bool> ForcePrompts;
        internal static ConfigEntry<bool> ForceMenuHints;
        internal static ConfigEntry<string> ButtonLabels;
        internal static ConfigEntry<string> PromptOverrides;
        internal static ConfigEntry<bool> MapHints;

        // [Prompts] ButtonLabels values. The names are part of the saved settings, so they stay as they are:
        // "Frame" = always use the forced controller profile's labels, "Auto" = the detected profile's, "Xbox" = Valheim's own.
        private const string LabelsAlways = "Frame";
        private const string LabelsAuto = "Auto";

        private static Dictionary<string, string> overrides = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Labels of the profile in use, rebuilt when the profile or the binding facts change.
        private static ControllerProfile labelsProfile, mapLabelsProfile;
        private static Dictionary<string, string> profileLabels, mapLabels;
        private static int profileLabelsKey = -1;

        // One-time log of what the big map's hints ask for, so labels can be matched to Valheim's exact keys.
        private static readonly HashSet<string> mapKeysSeen = new HashSet<string>();
        private static bool mapReported;
        private float mapOpenSince = -1f;

        // The display-only methods whose IsGamepadActive() calls get rewritten (nothing else changes) are listed in
        // VhvrApi.PromptTextMethods and VhvrApi.MenuHintMethods.
        private static readonly MethodInfo ZIsGamepadActive = Vhvr.TryMethod(VhvrApi.ZInputIsGamepadActive);
        private static readonly MethodInfo ZGamepadActiveGetter = Vhvr.TryMethod(VhvrApi.ZInputGamepadActive);
        private static readonly MethodInfo ZIsMouseActive = Vhvr.TryMethod(VhvrApi.ZInputIsMouseActive);

        public override void BindConfig(ConfigFile config)
        {
            ForcePrompts = config.Bind("Prompts", "ForceGamepadPrompts", true,
                "Show gamepad button glyphs in hover text and interaction prompts ([E] Pick up -> [(A)] Pick up).");
            ForceMenuHints = config.Bind("Prompts", "ForceGamepadMenuHints", true,
                "Show gamepad button icons next to buttons in menus (crafting, inventory, etc).");
            ButtonLabels = config.Bind("Prompts", "ButtonLabels", "Auto", new ConfigDescription(
                "Frame: label prompts with the Steam Frame button VHVR binds each action to (Use = trigger, Jump = A, Inventory = D-pad right...). " +
                "Xbox: Valheim's standard Xbox layout (Use = A), which doesn't match VHVR. Auto: Frame labels when Frame controllers are detected.",
                new AcceptableValueList<string>("Auto", "Frame", "Xbox")));
            PromptOverrides = config.Bind("Prompts", "PromptOverrides", "",
                "Optional custom labels that replace a prompt entirely. Format: Action=Label; Action=Label  (e.g. \"Use=Right A; AltPlace=Left Grip\"). " +
                "Common actions: Use, AltPlace, AltKeys, Remove, Hide, Attack, SecondaryAttack, Block, Jump, Crouch, Run, Inventory, Map. " +
                "Labels can include TextMeshPro tags, e.g. <sprite=\"xbox\" name=\"button_y\">. These win over ButtonLabels.");
            MapHints = config.Bind("Prompts", "FrameMapHints", true,
                "On the big map, label the hints with the Frame buttons that work with VHVR's laser (zoom = D-pad up/down, " +
                "ping = right stick click, pin = grip + trigger, remove = bumper) instead of Valheim's gamepad map controls.");

            overrides = PromptsPolicy.ParseOverrides(PromptOverrides.Value);
            PromptOverrides.SettingChanged += (_, __) => overrides = PromptsPolicy.ParseOverrides(PromptOverrides.Value);
        }

        protected override void Start(Harmony harmony)
        {
            int patched = PatchAll(harmony, VhvrApi.PromptTextMethods, nameof(PromptTranspiler)) +
                          PatchAll(harmony, VhvrApi.MenuHintMethods, nameof(MenuHintTranspiler));

            var translate = Vhvr.Method(VhvrApi.LocalizationTranslate);
            harmony.Patch(translate, prefix: new HarmonyMethod(typeof(PromptsHooks), nameof(TranslatePrefix)));

            if (overrides.Count > 0)
                Plugin.Log.LogInfo($"Prompt overrides: {string.Join(", ", overrides.Select(kv => kv.Key + "=" + kv.Value))}");
            Plugin.Log.LogInfo($"Prompts: {patched} prompt methods patched.");
        }

        private static int PatchAll(Harmony harmony, ApiEntry[] targets, string transpiler)
        {
            int count = 0;
            foreach (var target in targets)
            {
                string typeName = target.Type, methodName = target.Member;
                var type = Vhvr.TryType(target);
                var methods = type == null
                    ? new List<MethodInfo>()
                    : AccessTools.GetDeclaredMethods(type).Where(m => m.Name == methodName && m.GetMethodBody() != null).ToList();
                if (methods.Count == 0)
                {
                    Plugin.Log.LogWarning($"Prompts: skipping {typeName}.{methodName} (not in this Valheim version).");
                    continue;
                }
                foreach (var method in methods)
                {
                    try
                    {
                        harmony.Patch(method, transpiler: new HarmonyMethod(typeof(PromptsHooks), transpiler));
                        count++;
                    }
                    catch (Exception e)
                    {
                        Plugin.Log.LogError($"Prompts: failed to patch {typeName}.{methodName}: {e.Message}");
                    }
                }
            }
            return count;
        }

        // ---- Replacements for ZInput calls inside the patched methods ----

        public static bool PromptGamepadActive() => ForcePrompts.Value || ZInput.IsGamepadActive();

        public static bool MenuGamepadActive() => ForceMenuHints.Value || ZInput.IsGamepadActive();

        // UIInputHint shows a mixed "gamepad + mouse" hint when both are active; hide the mouse half when forcing.
        public static bool MenuMouseActive() => !ForceMenuHints.Value && ZInput.IsMouseActive();

        public static IEnumerable<CodeInstruction> PromptTranspiler(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
        {
            var replacement = AccessTools.Method(typeof(PromptsHooks), nameof(PromptGamepadActive));
            return Rewrite(instructions, __originalMethod, new Dictionary<MethodInfo, MethodInfo>
            {
                { ZIsGamepadActive, replacement },
                { ZGamepadActiveGetter, replacement }, // CookingStation reads the property instead
            });
        }

        public static IEnumerable<CodeInstruction> MenuHintTranspiler(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
        {
            return Rewrite(instructions, __originalMethod, new Dictionary<MethodInfo, MethodInfo>
            {
                { ZIsGamepadActive, AccessTools.Method(typeof(PromptsHooks), nameof(MenuGamepadActive)) },
                { ZIsMouseActive, AccessTools.Method(typeof(PromptsHooks), nameof(MenuMouseActive)) },
            });
        }

        private static IEnumerable<CodeInstruction> Rewrite(IEnumerable<CodeInstruction> instructions, MethodBase original,
            Dictionary<MethodInfo, MethodInfo> map)
        {
            var list = instructions.ToList();
            int replaced = 0;
            foreach (var ci in list)
            {
                foreach (var pair in map)
                {
                    if (pair.Key != null && ci.Calls(pair.Key))
                    {
                        ci.operand = pair.Value; // same static bool() signature, labels/blocks untouched
                        replaced++;
                        break;
                    }
                }
            }
            if (replaced == 0)
                Plugin.Log.LogWarning($"Prompts: {original.DeclaringType?.Name}.{original.Name} has no ZInput calls to rewrite (game update?).");
            return list;
        }

        // ---- Labels ----

        // Localization.Translate("KEY_Use") produces the text for "$KEY_Use". Short-circuit it with our label when we have one.
        public static bool TranslatePrefix(string __0, ref string __result)
        {
            string action = PromptsPolicy.ActionFromKey(__0);
            if (action == null) return true;

            bool mapOpen = MapOpen();
            if (mapOpen && !mapReported && mapKeysSeen.Count < 64) mapKeysSeen.Add(__0);

            if (overrides.TryGetValue(action, out var label)) { __result = label; return false; }

            var profile = LabelProfile();
            if (profile == null) return true; // no controller profile: Valheim's own labels

            if ((mapOpen && MapHints.Value && ForceMenuHints.Value && MapLabels(profile).TryGetValue(action, out label)) ||
                (ForcePrompts.Value && PromptLabels(profile).TryGetValue(action, out label)))
            {
                __result = label;
                return false;
            }
            return true;
        }

        private static bool MapOpen()
        {
            try { return Minimap.instance != null && Minimap.IsOpen(); }
            catch { return false; }
        }

        protected override void Tick()
        {
            if (mapReported) return;
            if (!MapOpen())
            {
                mapOpenSince = -1f;
                return;
            }
            float now = UnityEngine.Time.unscaledTime;
            if (mapOpenSince < 0f) mapOpenSince = now;
            if (now - mapOpenSince < 1.5f) return;

            mapReported = true;
            var texts = new List<string>();
            foreach (var hint in Minimap.instance.m_hints)
            {
                if (hint == null) continue;
                foreach (var t in hint.GetComponentsInChildren<TMPro.TMP_Text>(true))
                    if (!string.IsNullOrEmpty(t.text))
                        texts.Add($"{t.gameObject.name}{(t.gameObject.activeInHierarchy ? "" : " (hidden)")}: {t.text.Replace("\n", " / ")}");
            }
            Plugin.Log.LogInfo($"Prompts: big map open. Keys translated: {string.Join(", ", mapKeysSeen.DefaultIfEmpty("none"))}. " +
                               $"Hint texts: {string.Join(" | ", texts.DefaultIfEmpty("none"))}");
        }

        /// <summary>The controller profile whose button names the prompts should show, or null for Valheim's own labels.</summary>
        private static ControllerProfile LabelProfile()
        {
            string style = ButtonLabels.Value;
            if (style == LabelsAlways) return ControllerDetector.ForcedProfile;
            return style == LabelsAuto ? ControllerDetector.ActiveProfile : null;
        }

        private static Dictionary<string, string> MapLabels(ControllerProfile profile)
        {
            if (mapLabels == null || profile != mapLabelsProfile)
            {
                mapLabels = profile.MapLabels();
                mapLabelsProfile = profile;
            }
            return mapLabels;
        }

        private static Dictionary<string, string> PromptLabels(ControllerProfile profile)
        {
            bool crouch = BindingFacts.CrouchOnStickClick();
            bool sprint = BindingFacts.SprintOnStickClick();
            bool doubleCrouch = CrouchHooks.DoubleClickEnabled;
            int key = (crouch ? 1 : 0) | (sprint ? 2 : 0) | (doubleCrouch ? 4 : 0);
            if (profileLabels == null || key != profileLabelsKey || profile != labelsProfile)
            {
                profileLabels = profile.PromptLabels(new PromptLabelOptions
                {
                    CrouchOnStickClick = crouch, SprintOnStickClick = sprint, DoubleClickCrouch = doubleCrouch,
                });
                profileLabelsKey = key;
                labelsProfile = profile;
            }
            return profileLabels;
        }
    }
}
