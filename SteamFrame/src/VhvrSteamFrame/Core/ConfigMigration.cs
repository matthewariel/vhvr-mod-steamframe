using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using BepInEx.Configuration;

namespace VhvrSteamFrame
{
    /// <summary>On first run, carries settings over from the earlier "VR Gamepad Prompts" plugin (local.vrgamepadprompts).</summary>
    internal static class ConfigMigration
    {
        public static void ImportFromVrGamepadPrompts(ConfigFile config)
        {
            string oldPath = Path.Combine(Paths.ConfigPath, "local.vrgamepadprompts.cfg");
            if (!File.Exists(oldPath)) return;

            Dictionary<string, string> old;
            try { old = Read(oldPath); }
            catch (Exception e) { Plugin.Log.LogWarning($"Couldn't read {oldPath}: {e.Message}"); return; }

            int copied = 0;
            copied += Copy(old, "General.ForceGamepadPrompts", config, "Prompts", "ForceGamepadPrompts");
            copied += Copy(old, "General.ForceGamepadMenuHints", config, "Prompts", "ForceGamepadMenuHints");
            copied += Copy(old, "General.PromptOverrides", config, "Prompts", "PromptOverrides");
            copied += Copy(old, "General.ButtonLabels", config, "Prompts", "ButtonLabels");
            copied += Copy(old, "Combat.PunchDiagnostics", config, "Punch", "Diagnostics");
            copied += Copy(old, "Map.SharePositionOnJoin", config, "Map", "SharePositionOnJoin");
            copied += Copy(old, "Height.RecalibrateOnSteamVRRecenter", config, "Posture", "RecalibrateOnSteamVRRecenter");
            copied += Copy(old, "Height.RecenterDiagnostics", config, "Posture", "LogSteamVREvents");

            // PunchMode (GripAndThumb / GripOnly / Vanilla) became three checkboxes.
            if (old.TryGetValue("Combat.PunchMode", out var punchMode))
            {
                bool thumb = punchMode == "GripAndThumb";
                bool trigger = punchMode == "Vanilla";
                copied += Set(config, "Punch", "RequireGrip", "true");
                copied += Set(config, "Punch", "RequireThumbOnStick", thumb ? "true" : "false");
                copied += Set(config, "Punch", "RequireTrigger", trigger ? "true" : "false");
            }

            Plugin.Log.LogInfo($"Imported {copied} setting(s) from VR Gamepad Prompts ({oldPath}).");
        }

        private static Dictionary<string, string> Read(string path)
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string section = "";
            foreach (var raw in File.ReadAllLines(path))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                if (line.StartsWith("[") && line.EndsWith("]")) { section = line.Substring(1, line.Length - 2); continue; }
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                values[section + "." + line.Substring(0, eq).Trim()] = line.Substring(eq + 1).Trim();
            }
            return values;
        }

        private static int Copy(Dictionary<string, string> old, string oldKey, ConfigFile config, string section, string key)
        {
            return old.TryGetValue(oldKey, out var value) ? Set(config, section, key, value) : 0;
        }

        private static int Set(ConfigFile config, string section, string key, string value)
        {
            var def = new ConfigDefinition(section, key);
            if (!config.ContainsKey(def)) return 0;
            try
            {
                config[def].SetSerializedValue(value);
                return 1;
            }
            catch
            {
                return 0;
            }
        }
    }
}
