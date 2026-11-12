using System;
using System.Collections.Generic;

namespace VhvrSteamFrame
{
    /// <summary>
    /// The Valve Steam Frame controllers. Frame buttons use Xbox names (A/B/X/Y, triggers, bumpers, View, Menu, D-pad),
    /// so Valheim's Xbox glyphs fit. The only profile tested in a headset so far.
    /// </summary>
    public sealed class SteamFrameProfile : ControllerProfile
    {
        /// <summary>SteamVR's controller_type for Steam Frame controllers (see VHVR's actions.json default_bindings).</summary>
        public const string Type = "frame_controller";

        /// <summary>VHVR's binding file for the Frame (valheim_Data/StreamingAssets/SteamVR/).</summary>
        public const string BindingFile = "bindings_frame_controller.json";

        public static readonly SteamFrameProfile Instance = new SteamFrameProfile();

        private SteamFrameProfile() { }

        public override string Id { get { return "steam-frame"; } }
        public override string DisplayName { get { return "Steam Frame"; } }
        public override string ControllerType { get { return Type; } }
        public override string BindingFileName { get { return BindingFile; } }

        /// <summary>Valheim action -> Steam Frame button VHVR binds it to (VHVR's Frame binding + this plugin's binding tweaks).</summary>
        public override Dictionary<string, string> PromptLabels(PromptLabelOptions options)
        {
            var o = options ?? new PromptLabelOptions();
            string crouch = o.CrouchOnStickClick ? PromptsPolicy.Glyph("button_rs") : "Right stick down";
            if (o.DoubleClickCrouch && o.CrouchOnStickClick) crouch = "2x " + crouch;
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "Use", PromptsPolicy.Glyph("button_rt") },        // either trigger
                { "Place", PromptsPolicy.Glyph("button_rt") },
                { "Jump", PromptsPolicy.Glyph("button_a") },
                { "Dodge", PromptsPolicy.Glyph("button_x") },
                { "Run", o.SprintOnStickClick ? PromptsPolicy.Glyph("button_ls") : PromptsPolicy.Glyph("button_y") }, // stick click toggles; hold Y also runs
                { "Crouch", crouch },
                { "Inventory", PromptsPolicy.Glyph("dpad_right") },
                { "Map", PromptsPolicy.Glyph("button_back") },      // View button
                { "Menu", PromptsPolicy.Glyph("button_start") },
                { "BuildMenu", PromptsPolicy.Glyph("button_rb") },
                { "Remove", "Grip + " + PromptsPolicy.Glyph("button_a") },
            };
        }

        /// <summary>
        /// The big map's hints, while it's open. Valheim labels them with its gamepad map controls (LT/RT zoom, X ping, A pin,
        /// LB remove, RB cross out, D-pad pin icons). In VR the map is driven with VHVR's laser instead; these are the Frame
        /// buttons that do each job with the laser on the map (VHVR's Frame binding).
        /// </summary>
        public override Dictionary<string, string> MapLabels()
        {
            string trigger = PromptsPolicy.Glyph("button_rt");
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "MapZoomIn", PromptsPolicy.Glyph("dpad_up") },          // left D-pad up = laser scroll up
                { "MapZoomOut", PromptsPolicy.Glyph("dpad_down") },
                // Valheim's zoom hint actually asks for the triggers ("LT / RT  Zoom out/in"; seen in the 0.3.0 log).
                { "LTrigger", PromptsPolicy.Glyph("dpad_down") },         // zoom out
                { "RTrigger", PromptsPolicy.Glyph("dpad_up") },           // zoom in
                { "ButtonX", PromptsPolicy.Glyph("button_rs") },          // ping = right stick click (VHVR middle click)
                { "ButtonA", "Grip + " + trigger },                       // add pin = grip + trigger chord
                // Remove pin (Valheim asks for TabRight) = the right bumper: VHVR's laser right-click is on the laser hand.
                // Confirmed in the headset (2026-11-10): the left bumper doesn't remove pins.
                { "TabRight", PromptsPolicy.Glyph("button_rb") },
                { "TabLeft", trigger },                                   // cross off pin (Valheim asks for TabLeft) = trigger on the pin
                // The pin icon picker and the toggles in the map's side panel ask for D-pad left / right / up / down and Y;
                // with the laser they're buttons: point at one and pull the trigger.
                { "DPadLeft", trigger },
                { "DPadUp", trigger },
                { "DPadDown", trigger },
                { "DPadRight", trigger },
                { "ButtonY", trigger },
                { "ButtonB", PromptsPolicy.Glyph("button_start") },       // close = right Menu button (confirmed in the headset)
            };
        }

        /// <summary>The extra "Close map" hint added to the big map's hint bar (Valheim has none for it).</summary>
        public override string MapCloseLabel()
        {
            return PromptsPolicy.Glyph("button_start");
        }
    }
}
