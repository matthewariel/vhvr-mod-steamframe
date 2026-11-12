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
        // "Frame" = always use the forced controller profile's labels, "Auto" = the detected profile's, "Xbox" = Valheim's own,
        // "Keyboard" = don't force gamepad prompts at all: Valheim's keyboard / mouse prompts, as without this plugin.
        private const string LabelsAlways = "Frame";
        private const string LabelsAuto = "Auto";
        private const string LabelsKeyboard = "Keyboard";

        /// <summary>Button labels set to Keyboard: no gamepad prompts are forced, Valheim decides as it normally would.</summary>
        internal static bool KeyboardLabels => ButtonLabels != null && ButtonLabels.Value == LabelsKeyboard;

        // The big map's D-pad hint pictures (pin icon picker): they show the D-pad, which does nothing there in VR.
        private static readonly HashSet<UIGamePad> mapDpadHints = new HashSet<UIGamePad>();
        // The cross next to the pin icons is a plain picture (not a UIGamePad hint), found by name; its Image is switched
        // off while hidden. Valheim only switches whole objects on and off, so the Image stays off.
        private static readonly List<UnityEngine.UI.Graphic> mapDpadPictures = new List<UnityEngine.UI.Graphic>();
        private static Minimap mapDpadHintsFor;
        private static bool hideMapDpadHints;

        private static Dictionary<string, string> overrides = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Labels of the profile in use, rebuilt when the profile or the binding facts change.
        private static ControllerProfile labelsProfile, mapLabelsProfile;
        private static Dictionary<string, string> profileLabels, mapLabels;
        private static int profileLabelsKey = -1;

        // One-time log of what the big map's hints ask for, so labels can be matched to Valheim's exact keys.
        private static readonly HashSet<string> mapKeysSeen = new HashSet<string>();
        private static bool mapReported;
        private float mapOpenSince = -1f;
        private bool closeHintChecked;

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
                "Xbox: Valheim's standard Xbox layout (Use = A), which doesn't match VHVR. Auto: Frame labels when Frame controllers are detected. " +
                "Keyboard: Valheim's own keyboard and mouse prompts ([E] Pick up), as without this plugin, e.g. for flat-screen play.",
                new AcceptableValueList<string>("Auto", "Frame", "Xbox", LabelsKeyboard)));
            PromptOverrides = config.Bind("Prompts", "PromptOverrides", "",
                "Optional custom labels that replace a prompt entirely. Format: Action=Label; Action=Label  (e.g. \"Use=Right A; AltPlace=Left Grip\"). " +
                "Common actions: Use, AltPlace, AltKeys, Remove, Hide, Attack, SecondaryAttack, Block, Jump, Crouch, Run, Inventory, Map. " +
                "Labels can include TextMeshPro tags, e.g. <sprite=\"xbox\" name=\"button_y\">. These win over ButtonLabels.");
            MapHints = config.Bind("Prompts", "FrameMapHints", true,
                "On the big map, label the hints with the Frame buttons that work with VHVR's laser (zoom = D-pad up/down, " +
                "ping = right stick click, pin = grip + trigger, remove = right bumper, pin icons = trigger) instead of Valheim's " +
                "gamepad map controls, and add a \"Close map\" hint (Menu button).");

            overrides = PromptsPolicy.ParseOverrides(PromptOverrides.Value);
            PromptOverrides.SettingChanged += (_, __) => overrides = PromptsPolicy.ParseOverrides(PromptOverrides.Value);
        }

        protected override void Start(Harmony harmony)
        {
            int patched = PatchAll(harmony, VhvrApi.PromptTextMethods, nameof(PromptTranspiler)) +
                          PatchAll(harmony, VhvrApi.MenuHintMethods, nameof(MenuHintTranspiler));

            var translate = Vhvr.Method(VhvrApi.LocalizationTranslate);
            harmony.Patch(translate, prefix: new HarmonyMethod(typeof(PromptsHooks), nameof(TranslatePrefix)));

            var gamePadUpdate = Vhvr.TryMethod(VhvrApi.UIGamePadUpdate);
            if (gamePadUpdate != null)
                harmony.Patch(gamePadUpdate, postfix: new HarmonyMethod(typeof(PromptsHooks), nameof(UIGamePadUpdatePostfix)));
            else
                Plugin.Log.LogWarning("Prompts: UIGamePad.Update not found; the big map's D-pad hints stay visible.");

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

        public static bool PromptGamepadActive() => (ForcePrompts.Value && !KeyboardLabels) || ZInput.IsGamepadActive();

        public static bool MenuGamepadActive() => (ForceMenuHints.Value && !KeyboardLabels) || ZInput.IsGamepadActive();

        // UIInputHint shows a mixed "gamepad + mouse" hint when both are active; hide the mouse half when forcing.
        public static bool MenuMouseActive() => (!ForceMenuHints.Value || KeyboardLabels) && ZInput.IsMouseActive();

        // Runs after each gamepad hint picture decides whether to show; hides the big map's D-pad ones while the map is open.
        public static void UIGamePadUpdatePostfix(UIGamePad __instance)
        {
            if (!hideMapDpadHints || __instance == null) return;
            var hint = __instance.m_hint;
            if (hint != null && hint.activeSelf && mapDpadHints.Contains(__instance)) hint.SetActive(false);
        }

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
            if (!MapOpen())
            {
                mapOpenSince = -1f;
                hideMapDpadHints = false;
                return;
            }
            float now = UnityEngine.Time.unscaledTime;
            if (mapOpenSince < 0f)
            {
                mapOpenSince = now;
                closeHintChecked = false;
                FindMapDpadHints();
                hideMapDpadHints = LabelProfile() != null && MapHints.Value && ForceMenuHints.Value;
            }
            foreach (var picture in mapDpadPictures)
                if (picture != null && picture.enabled == hideMapDpadHints) picture.enabled = !hideMapDpadHints;
            // A moment after opening, once Valheim has switched the gamepad hint bar on.
            if (!closeHintChecked && now - mapOpenSince >= 0.3f)
            {
                closeHintChecked = true;
                UpdateCloseHint();
            }
            if (mapReported || now - mapOpenSince < 1.5f) return;

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

        // ---- "Close map" hint ----

        // Valheim's big-map hint bar has no hint for closing the map, so one is added: a copy of the "Add pin" hint entry,
        // placed after it by the bar's layout group. Without a layout group the copy would sit on top of another hint, so
        // then nothing is added (and the log says why). Rebuilt when Valheim recreates the map (each world load).
        private static UnityEngine.GameObject closeHint;
        private static Minimap closeHintTriedFor;

        private static void UpdateCloseHint()
        {
            try
            {
                var profile = LabelProfile();
                string glyph = profile?.MapCloseLabel();
                bool want = glyph != null && MapHints.Value && ForceMenuHints.Value;
                if (closeHint != null)
                {
                    closeHint.SetActive(want);
                    return;
                }
                if (!want || closeHintTriedFor == Minimap.instance) return;
                closeHintTriedFor = Minimap.instance;

                foreach (var hint in Minimap.instance.m_hints)
                {
                    if (hint == null || !hint.activeInHierarchy) continue;
                    var add = hint.GetComponentsInChildren<TMPro.TMP_Text>(false).FirstOrDefault(t => t.gameObject.name == "Text - AddPin");
                    if (add == null) continue;

                    // The entry is the AddPin text's ancestor that sits directly under a layout group.
                    var entry = add.transform;
                    while (entry.parent != null && entry.parent.GetComponent<UnityEngine.UI.LayoutGroup>() == null && entry.parent != hint.transform)
                        entry = entry.parent;
                    if (entry.parent == null || entry.parent.GetComponent<UnityEngine.UI.LayoutGroup>() == null)
                    {
                        Plugin.Log.LogInfo($"Prompts: no \"Close map\" hint: the map's hint bar ({hint.name}) has no layout group, " +
                                           "so an extra hint would overlap the others.");
                        return;
                    }

                    var copy = UnityEngine.Object.Instantiate(entry.gameObject, entry.parent);
                    copy.name = "SteamFrame CloseMap";
                    copy.transform.SetSiblingIndex(entry.GetSiblingIndex() + 1);
                    var texts = copy.GetComponentsInChildren<TMPro.TMP_Text>(true);
                    var text = texts.FirstOrDefault(t => t.gameObject.name == "Text - AddPin") ?? texts.FirstOrDefault();
                    if (text == null)
                    {
                        UnityEngine.Object.Destroy(copy);
                        return;
                    }
                    text.gameObject.name = "Text - CloseMap";
                    text.text = $"<mspace=0.6em>{glyph} </mspace>Close map";
                    closeHint = copy;
                    Plugin.Log.LogInfo($"Prompts: added a \"Close map\" hint to the big map ({entry.parent.name}).");
                    return;
                }
                Plugin.Log.LogInfo("Prompts: no \"Close map\" hint: couldn't find the map's \"Add pin\" hint to copy.");
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Prompts: couldn't add the \"Close map\" hint: {e.Message}");
            }
        }

        /// <summary>
        /// Collects the big map's gamepad hint pictures for D-pad keys (the pin icon picker shows one), once per map, and
        /// logs every hint picture the map has so a wrong one can be found by name.
        /// </summary>
        private static void FindMapDpadHints()
        {
            if (mapDpadHintsFor == Minimap.instance) return;
            mapDpadHintsFor = Minimap.instance;
            mapDpadHints.Clear();
            try
            {
                var root = Minimap.instance.m_largeRoot;
                if (root == null) return;
                var all = root.GetComponentsInChildren<UIGamePad>(true);
                foreach (var pad in all)
                    if (pad != null && pad.m_hint != null && (pad.m_zinputKey ?? "").IndexOf("DPad", StringComparison.OrdinalIgnoreCase) >= 0)
                        mapDpadHints.Add(pad);
                Plugin.Log.LogInfo($"Prompts: big map gamepad hint pictures: " +
                                   $"{string.Join(", ", all.Where(p => p != null).Select(p => $"{p.gameObject.name} ({p.m_zinputKey}{(mapDpadHints.Contains(p) ? ", hidden in VR" : "")})").DefaultIfEmpty("none"))}.");

                // Plain D-pad pictures anywhere on the big map, by object or sprite name.
                mapDpadPictures.Clear();
                foreach (var picture in root.GetComponentsInChildren<UnityEngine.UI.Graphic>(true))
                    if ((picture is UnityEngine.UI.Image || picture is UnityEngine.UI.RawImage) &&
                        PromptsPolicy.IsDpadPicture(picture.gameObject.name, PictureName(picture)))
                        mapDpadPictures.Add(picture);
                Plugin.Log.LogInfo($"Prompts: big map D-pad pictures hidden in VR: " +
                                   $"{string.Join(", ", mapDpadPictures.Select(Describe).DefaultIfEmpty("none found"))}.");

                // If none matched, list the pictures around the pin icon picker so the cross can be found by name.
                if (mapDpadPictures.Count == 0 && Minimap.instance.m_selectedIcon0 != null)
                {
                    var panel = Minimap.instance.m_selectedIcon0.transform.parent;
                    if (panel != null && panel.parent != null) panel = panel.parent;
                    if (panel != null && panel.parent != null) panel = panel.parent;
                    if (panel != null)
                        Plugin.Log.LogInfo($"Prompts: pictures around the pin icon picker ({panel.name}): " +
                                           $"{string.Join(", ", panel.GetComponentsInChildren<UnityEngine.UI.Graphic>(true)
                                               .Where(g => g is UnityEngine.UI.Image || g is UnityEngine.UI.RawImage).Select(Describe))}.");
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Prompts: couldn't look for the big map's D-pad hints: {e.Message}");
            }
        }

        private static string PictureName(UnityEngine.UI.Graphic picture)
        {
            if (picture is UnityEngine.UI.Image image) return image.sprite != null ? image.sprite.name : null;
            if (picture is UnityEngine.UI.RawImage raw) return raw.texture != null ? raw.texture.name : null;
            return null;
        }

        // "Panel/Icons/DPad (sprite dpad_updown)", path from the big map's root.
        private static string Describe(UnityEngine.UI.Graphic picture)
        {
            var parts = new List<string>();
            for (var t = picture.transform; t != null && t.gameObject != Minimap.instance.m_largeRoot; t = t.parent) parts.Insert(0, t.name);
            return $"{string.Join("/", parts)} (sprite {PictureName(picture) ?? "none"}{(picture.gameObject.activeInHierarchy ? "" : ", not shown now")})";
        }

        /// <summary>The controller profile whose button names the prompts should show, or null for Valheim's own labels.</summary>
        private static ControllerProfile LabelProfile()
        {
            string style = ButtonLabels.Value;
            if (style == LabelsKeyboard) return null;
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
