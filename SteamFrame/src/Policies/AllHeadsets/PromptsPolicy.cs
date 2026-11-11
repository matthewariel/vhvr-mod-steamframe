using System;
using System.Collections.Generic;

namespace VhvrSteamFrame
{
    /// <summary>
    /// Button prompt rules that are the same for every controller. Plain C#, no game hooks.
    /// Which button each action shows comes from the active <see cref="ControllerProfile"/>.
    /// </summary>
    public static class PromptsPolicy
    {
        /// <summary>A TextMeshPro tag for one of Valheim's Xbox glyphs (button_a, dpad_up...).</summary>
        public static string Glyph(string sprite)
        {
            return "<sprite=\"xbox\" name=\"" + sprite + "\">";
        }

        /// <summary>"Use=Right A; AltPlace=Left Grip" -> { Use: "Right A", AltPlace: "Left Grip" }.</summary>
        public static Dictionary<string, string> ParseOverrides(string raw)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in (raw ?? "").Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                int eq = entry.IndexOf('=');
                if (eq <= 0) continue;
                string action = entry.Substring(0, eq).Trim();
                string label = entry.Substring(eq + 1).Trim();
                if (action.StartsWith("$")) action = action.Substring(1);
                if (action.StartsWith("KEY_", StringComparison.OrdinalIgnoreCase)) action = action.Substring(4);
                if (action.Length > 0 && label.Length > 0) result[action] = label;
            }
            return result;
        }

        /// <summary>Localization key "KEY_Use" / "KEY_JoyUse" -> action "Use"; anything else -> null.</summary>
        public static string ActionFromKey(string key)
        {
            if (key == null || !key.StartsWith("KEY_", StringComparison.Ordinal)) return null;
            string action = key.Substring(4);
            return action.StartsWith("Joy", StringComparison.Ordinal) ? action.Substring(3) : action;
        }
    }
}
