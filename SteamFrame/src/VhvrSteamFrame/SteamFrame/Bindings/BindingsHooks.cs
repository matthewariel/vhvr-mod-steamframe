using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Valve.Newtonsoft.Json;
using Valve.Newtonsoft.Json.Linq;

namespace VhvrSteamFrame
{
    /// <summary>
    /// Applies the Frame binding tweaks to Valheim's own copy of VHVR's Steam Frame binding
    /// (valheim_Data/StreamingAssets/SteamVR/bindings_frame_controller.json). Only that file changes; nothing in SteamVR.
    ///
    /// VHVR's untouched file is kept in BepInEx/config/VhvrSteamFrame/ and every write starts from it, so turning a tweak
    /// off restores VHVR's binding for that button. When a VHVR update replaces the file, the new one becomes the base.
    /// SteamVR reads the file when Valheim starts, so changes show up after a restart.
    ///
    /// On top of the tweaks come the player's own remaps from the Button guide page ([Bindings] ButtonRemaps,
    /// see <see cref="FrameButtons"/>); "Reset to default" clears them.
    /// </summary>
    internal class BindingsHooks : Module
    {
        public override string Name => "Bindings";
        public override ModuleScope Scope => ModuleScope.SteamFrame;

        internal static ConfigEntry<bool> Manage, ThumbTouchSensing, SprintToggleOnLeftStickClick, CrouchOnRightStickClick, NoStickFlickRunCrouch;
        internal static ConfigEntry<string> ButtonRemaps;
        private static BindingsHooks instance;

        /// <summary>The remaps in use: the saved ones while the tweaks are managed, none otherwise.</summary>
        internal static Dictionary<string, string> ActiveRemaps =>
            Manage == null || !Manage.Value ? new Dictionary<string, string>() : FrameButtons.ParseRemaps(ButtonRemaps?.Value);

        /// <summary>How many buttons the player has changed (saved, whether or not in use).</summary>
        internal static int RemapCount => FrameButtons.ParseRemaps(ButtonRemaps?.Value).Count;

        internal static bool CrouchOnRightStickClickEnabled => StickClickDoes("right/thumbstick", "togglecrouch", CrouchOnRightStickClick);
        internal static bool SprintToggleOnLeftStickClickEnabled => StickClickDoes("left/thumbstick", "togglerun", SprintToggleOnLeftStickClick);

        /// <summary>A stick click does an action: the player's remap decides if there is one, else the tweak.</summary>
        private static bool StickClickDoes(string id, string action, ConfigEntry<bool> tweak)
        {
            if (Manage == null) return true;
            if (!Manage.Value) return false;
            return ActiveRemaps.TryGetValue(id, out var remap) ? remap == action : tweak.Value;
        }
        internal static bool ThumbTouchSensingEnabled => Manage == null || (Manage.Value && ThumbTouchSensing.Value);

        /// <summary>The binding file Valheim gives SteamVR.</summary>
        internal static string TargetPath => PathFor(SteamFrameProfile.Instance);

        /// <summary>Where Valheim keeps the binding file of a controller profile.</summary>
        internal static string PathFor(ControllerProfile profile)
        {
            return Path.Combine(UnityEngine.Application.streamingAssetsPath, "SteamVR", profile.BindingFileName);
        }

        private static string StoreDir => Path.Combine(Paths.ConfigPath, "VhvrSteamFrame");
        private static string BasePath => Path.Combine(StoreDir, "bindings_frame_controller.vhvr.json");
        private static string WrittenHashPath => Path.Combine(StoreDir, "bindings_frame_controller.written.sha256");

        /// <summary>True after this session changed the file (SteamVR needs a restart to load it).</summary>
        internal static bool RestartNeeded { get; private set; }

        public override void BindConfig(ConfigFile config)
        {
            const string restart = " Takes effect after restarting Valheim.";
            Manage = config.Bind("Bindings", "ManageFrameBindings", true,
                "Apply the tweaks below to VHVR's Steam Frame binding (Valheim's copy only). Off: put VHVR's binding back." + restart);
            ThumbTouchSensing = config.Bind("Bindings", "ThumbTouchSensing", true,
                "Sense a thumb resting anywhere on each thumbstick (needed for Punch.RequireThumbOnStick)." + restart);
            SprintToggleOnLeftStickClick = config.Bind("Bindings", "SprintToggleOnLeftStickClick", true,
                "Left stick click toggles sprint (VHVR: middle click). Also 'Take all' in chests." + restart);
            CrouchOnRightStickClick = config.Bind("Bindings", "CrouchOnRightStickClick", true,
                "Right stick click toggles crouch. Also 'Place stacks' in chests." + restart);
            NoStickFlickRunCrouch = config.Bind("Bindings", "NoStickFlickRunCrouch", true,
                "Remove VHVR's right-stick flick up = sprint / flick down = crouch, which trigger easily while turning." + restart);
            ButtonRemaps = config.Bind("Bindings", "ButtonRemaps", "",
                "Buttons changed on the Steam Frame tab's Button guide page, as hand/button=action pairs separated by ';' " +
                "(for example right/a=dodge;left/bumper=none). Empty: VHVR's buttons with the tweaks above. " +
                "Used only while ManageFrameBindings is on." + restart);

            foreach (var entry in new[] { Manage, ThumbTouchSensing, SprintToggleOnLeftStickClick, CrouchOnRightStickClick, NoStickFlickRunCrouch })
                entry.SettingChanged += (_, __) => instance?.OnSettingChanged();
            ButtonRemaps.SettingChanged += (_, __) => instance?.OnSettingChanged();

            // Tell the all-headset modules (prompt labels) what the stick clicks do now.
            BindingFacts.CrouchOnStickClick = () => CrouchOnRightStickClickEnabled;
            BindingFacts.SprintOnStickClick = () => SprintToggleOnLeftStickClickEnabled;
        }

        protected override void Start(Harmony harmony)
        {
            if (!File.Exists(TargetPath)) throw new MissingMemberException($"Frame binding file not found at {TargetPath}");
            instance = this;
            Apply(atStartup: true);
        }

        private void OnSettingChanged()
        {
            if (!Active) return;
            try { Apply(atStartup: false); }
            catch (Exception e) { Plugin.Log.LogError($"Bindings: couldn't update the Frame binding: {e.Message}"); }
        }

        internal static FrameBindingOptions CurrentOptions()
        {
            return new FrameBindingOptions
            {
                ThumbTouchSensing = ThumbTouchSensing.Value,
                SprintToggleOnLeftStickClick = SprintToggleOnLeftStickClick.Value,
                CrouchOnRightStickClick = CrouchOnRightStickClick.Value,
                NoStickFlickRunCrouch = NoStickFlickRunCrouch.Value,
            };
        }

        private static void Apply(bool atStartup)
        {
            Directory.CreateDirectory(StoreDir);
            string current = File.ReadAllText(TargetPath);
            string baseJson = File.Exists(BasePath) ? File.ReadAllText(BasePath) : null;
            string writtenHash = File.Exists(WrittenHashPath) ? File.ReadAllText(WrittenHashPath).Trim() : null;

            // First run, or the file isn't what we last wrote (VHVR update / reinstall / hand edit): that file is the new base.
            bool replacedSinceLastWrite = writtenHash != null && Hash(current) != writtenHash;
            if (baseJson == null || replacedSinceLastWrite)
            {
                baseJson = current;
                File.WriteAllText(BasePath, baseJson, new UTF8Encoding(false));
                Plugin.Log.LogInfo($"Bindings: saved {(replacedSinceLastWrite ? "the updated" : "VHVR's")} Frame binding as the base for tweaks ({BasePath}).");
            }

            string output = baseJson;
            if (Manage.Value)
            {
                var root = JObject.Parse(baseJson);
                foreach (var problem in FrameBindingPatcher.Apply(root, CurrentOptions()))
                    Plugin.Log.LogWarning("Bindings: skipped " + problem);
                var remaps = ActiveRemaps;
                foreach (var problem in FrameButtons.ApplyRemaps(root, remaps))
                    Plugin.Log.LogWarning("Bindings: skipped " + problem);
                if (remaps.Count > 0) Plugin.Log.LogInfo($"Bindings: button remaps: {FrameButtons.FormatRemaps(remaps)}.");
                output = root.ToString(Formatting.Indented);
            }

            if (output == current)
            {
                if (writtenHash == null) File.WriteAllText(WrittenHashPath, Hash(current));
                Plugin.Log.LogInfo("Bindings: Frame binding already up to date.");
                return;
            }

            File.WriteAllText(TargetPath, output, new UTF8Encoding(false));
            File.WriteAllText(WrittenHashPath, Hash(output));
            RestartNeeded = true;
            Hud.Notice(atStartup
                ? "Steam Frame bindings updated. If buttons don't match yet, restart Valheim."
                : "Steam Frame bindings changed. Restart Valheim to use them.");
        }

        /// <summary>
        /// What the Frame buttons do with no remaps: VHVR's binding plus the tweaks (just VHVR's when the tweaks are off).
        /// The Button guide compares against it to mark the boxes the player changed.
        /// </summary>
        internal static JObject DefaultDocument()
        {
            var root = JObject.Parse(File.Exists(BasePath) ? File.ReadAllText(BasePath) : File.ReadAllText(TargetPath));
            if (Manage.Value) FrameBindingPatcher.Apply(root, CurrentOptions());
            return root;
        }

        /// <summary>
        /// Gives a button a new action (FrameButtons.Nothing = none) and rewrites the binding file. Choosing what the
        /// button does by default removes its remap. Returns why it can't be done, or null when it's done.
        /// </summary>
        internal static string SetRemap(string buttonId, string action)
        {
            if (instance == null || !instance.Active) return "The Bindings module isn't running (see the log), so buttons can't be changed.";
            try
            {
                string refusal = FrameButtons.Refusal(ReadCurrent(SteamFrameProfile.Instance), buttonId, action);
                if (refusal != null) return refusal;

                var remaps = FrameButtons.ParseRemaps(ButtonRemaps.Value);
                var button = FrameButtons.ById(buttonId);
                var asDefault = FrameButtons.ActionsOn(DefaultDocument(), button);
                bool isDefault = action == FrameButtons.Nothing ? asDefault.Count == 0 : asDefault.Count == 1 && asDefault[0] == action;
                if (isDefault) remaps.Remove(button.Id);
                else remaps[button.Id] = action;
                Plugin.Log.LogInfo($"Bindings: {button.Id} set to {action}{(isDefault ? " (its default)" : "")}.");
                ButtonRemaps.Value = FrameButtons.FormatRemaps(remaps); // SettingChanged rewrites the file
                return null;
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Bindings: couldn't change {buttonId}: {e}");
                return "Couldn't change it: " + e.Message;
            }
        }

        /// <summary>Clears every remap: the buttons go back to VHVR's binding with the tweaks.</summary>
        internal static void ResetRemaps()
        {
            if (string.IsNullOrEmpty(ButtonRemaps?.Value)) return;
            Plugin.Log.LogInfo($"Bindings: button remaps reset (were {ButtonRemaps.Value}).");
            ButtonRemaps.Value = "";
        }

        /// <summary>The binding document currently on disk for a controller profile, for the button guide.</summary>
        internal static JObject ReadCurrent(ControllerProfile profile)
        {
            return JObject.Parse(File.ReadAllText(PathFor(profile)));
        }

        private static string Hash(string text)
        {
            using (var sha = SHA256.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(text));
                var sb = new StringBuilder(bytes.Length * 2);
                foreach (var b in bytes) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }
    }
}
