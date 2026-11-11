using System;
using System.Collections.Generic;

namespace VhvrSteamFrame
{
    /// <summary>
    /// Which button glyph/label to show for a Valheim action on a Steam Frame. Plain C#, no game hooks.
    /// Frame buttons use Xbox names (A/B/X/Y, triggers, bumpers, View, Menu, D-pad), so Valheim's Xbox glyphs fit.
    /// </summary>
    public static class PromptsPolicy
    {
        public static string Glyph(string sprite)
        {
            return "<sprite=\"xbox\" name=\"" + sprite + "\">";
        }

        /// <summary>Valheim action -> Steam Frame button VHVR binds it to (VHVR's Frame binding + this plugin's binding tweaks).</summary>
        public static Dictionary<string, string> FrameLabels(bool crouchOnRightStickClick, bool sprintOnLeftStickClick,
            bool doubleClickCrouch = false)
        {
            string crouch = crouchOnRightStickClick ? Glyph("button_rs") : "Right stick down";
            if (doubleClickCrouch && crouchOnRightStickClick) crouch = "2x " + crouch;
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "Use", Glyph("button_rt") },        // either trigger
                { "Place", Glyph("button_rt") },
                { "Jump", Glyph("button_a") },
                { "Dodge", Glyph("button_x") },
                { "Run", sprintOnLeftStickClick ? Glyph("button_ls") : Glyph("button_y") }, // stick click toggles; hold Y also runs
                { "Crouch", crouch },
                { "Inventory", Glyph("dpad_right") },
                { "Map", Glyph("button_back") },      // View button
                { "Menu", Glyph("button_start") },
                { "BuildMenu", Glyph("button_rb") },
                { "Remove", "Grip + " + Glyph("button_a") },
            };
        }

        /// <summary>
        /// The big map's hints, while it's open. Valheim labels them with its gamepad map controls (LT/RT zoom, X ping, A pin,
        /// LB remove, RB cross out, D-pad pin icons). In VR the map is driven with VHVR's laser instead; these are the Frame
        /// buttons that do each job with the laser on the map (VHVR's Frame binding).
        /// </summary>
        public static Dictionary<string, string> FrameMapLabels()
        {
            string trigger = Glyph("button_rt");
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "MapZoomIn", Glyph("dpad_up") },          // left D-pad up = laser scroll up
                { "MapZoomOut", Glyph("dpad_down") },
                { "ButtonX", Glyph("button_rs") },          // ping = right stick click (VHVR middle click)
                { "ButtonA", "Grip + " + trigger },         // add pin = grip + trigger chord
                { "TabLeft", Glyph("button_lb") },          // remove pin = bumper (VHVR right click)
                { "TabRight", trigger },                    // cross out = trigger on the pin
                { "DPadUp", trigger },                      // pin icon / filter: point at it and pull the trigger
                { "DPadDown", trigger },
                { "DPadRight", trigger },
                { "ButtonB", Glyph("button_back") },        // close = Map button
            };
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
