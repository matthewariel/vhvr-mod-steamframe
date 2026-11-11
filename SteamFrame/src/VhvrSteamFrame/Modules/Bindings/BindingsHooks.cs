using System;
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
    /// </summary>
    internal class BindingsHooks : Module
    {
        public override string Name => "Bindings";

        internal static ConfigEntry<bool> Manage, ThumbTouchSensing, SprintToggleOnLeftStickClick, CrouchOnRightStickClick, NoStickFlickRunCrouch;

        internal static bool CrouchOnRightStickClickEnabled => Manage == null || (Manage.Value && CrouchOnRightStickClick.Value);
        internal static bool SprintToggleOnLeftStickClickEnabled => Manage == null || (Manage.Value && SprintToggleOnLeftStickClick.Value);
        internal static bool ThumbTouchSensingEnabled => Manage == null || (Manage.Value && ThumbTouchSensing.Value);

        /// <summary>The binding file Valheim gives SteamVR.</summary>
        internal static string TargetPath => Path.Combine(UnityEngine.Application.streamingAssetsPath, "SteamVR", "bindings_frame_controller.json");

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

            foreach (var entry in new[] { Manage, ThumbTouchSensing, SprintToggleOnLeftStickClick, CrouchOnRightStickClick, NoStickFlickRunCrouch })
                entry.SettingChanged += (_, __) => OnSettingChanged();
        }

        protected override void Start(Harmony harmony)
        {
            if (!File.Exists(TargetPath)) throw new MissingMemberException($"Frame binding file not found at {TargetPath}");
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

        /// <summary>The binding document currently on disk, for the button guide.</summary>
        internal static JObject ReadCurrent()
        {
            return JObject.Parse(File.ReadAllText(TargetPath));
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
