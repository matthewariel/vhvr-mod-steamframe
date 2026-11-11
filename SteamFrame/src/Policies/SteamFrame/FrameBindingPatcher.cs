using System;
using System.Collections.Generic;
using System.Linq;
#if VHVR_GAME
using Valve.Newtonsoft.Json.Linq; // the JSON library Valheim ships (SteamVR's copy)
#else
using Newtonsoft.Json.Linq;       // tests and the VHVR-compat build use the NuGet package
#endif

namespace VhvrSteamFrame
{
    /// <summary>Which tweaks to apply on top of VHVR's Steam Frame binding.</summary>
    public sealed class FrameBindingOptions
    {
        /// <summary>Whole-stick touch -> Teleport (unused by VHVR), so "thumb resting on the stick" can be sensed for punching.</summary>
        public bool ThumbTouchSensing = true;
        /// <summary>Left stick click -> sprint toggle (VHVR: middle click).</summary>
        public bool SprintToggleOnLeftStickClick = true;
        /// <summary>Right stick click -> crouch toggle.</summary>
        public bool CrouchOnRightStickClick = true;
        /// <summary>Stop VHVR's right-stick touch-flick up = sprint / down = crouch (easy to trigger by accident).
        /// The directions stay defined so the grip + stick up/down build chords keep working.</summary>
        public bool NoStickFlickRunCrouch = true;
    }

    /// <summary>
    /// Edits a SteamVR binding document (VHVR's bindings_frame_controller.json) in place. Every edit is idempotent and
    /// relative to VHVR's file, so turning a tweak off means "don't apply it" and applying twice changes nothing.
    /// In VHVR itself these would simply be edits to the shipped JSON.
    /// </summary>
    public static class FrameBindingPatcher
    {
        private const string ValheimSet = "/actions/valheim";
        private const string DefaultSet = "/actions/default";
        private const string Teleport = "/actions/default/in/teleport";
        private const string ToggleRun = "/actions/valheim/in/togglerun";
        private const string ToggleCrouch = "/actions/valheim/in/togglecrouch";
        private const string StopGesturedLocomotion = "/actions/valheim/in/stopgesturedlocomotion";

        private static string Stick(string hand) => "/user/hand/" + hand + "/input/thumbstick";

        /// <summary>Applies the selected tweaks. Returns a note per tweak that couldn't be applied (VHVR's file changed shape).</summary>
        public static List<string> Apply(JObject root, FrameBindingOptions o)
        {
            var problems = new List<string>();

            if (o.ThumbTouchSensing)
            {
                var sources = Sources(root, DefaultSet);
                if (sources == null) problems.Add("ThumbTouchSensing: no /actions/default sources");
                else
                {
                    foreach (var hand in new[] { "left", "right" })
                    {
                        // VHVR: stick touch-flick north -> teleport. Replace with plain stick touch -> teleport.
                        var dpad = Find(sources, Stick(hand), "dpad", "touch");
                        if (dpad != null)
                        {
                            RemoveInput(dpad, "north", Teleport);
                            RemoveIfEmpty(sources, dpad);
                        }
                        SetInput(Ensure(sources, Stick(hand), "joystick"), "touch", Teleport);
                    }
                }
            }

            var valheim = Sources(root, ValheimSet);
            if (valheim == null)
            {
                if (o.SprintToggleOnLeftStickClick || o.CrouchOnRightStickClick || o.NoStickFlickRunCrouch)
                    problems.Add("no /actions/valheim sources");
                return problems;
            }

            if (o.SprintToggleOnLeftStickClick)
            {
                var leftStick = Find(valheim, Stick("left"), "joystick", null);
                if (leftStick == null) problems.Add("SprintToggleOnLeftStickClick: left stick joystick binding not found");
                else SetInput(leftStick, "click", ToggleRun);
            }

            if (o.CrouchOnRightStickClick)
                SetInput(Ensure(valheim, Stick("right"), "button"), "click", ToggleCrouch);

            if (o.NoStickFlickRunCrouch)
            {
                // Keep the up/down directions defined (the grip + stick up/down "tilt piece" chords need them), but point
                // them at the same harmless action VHVR uses for left/right instead of sprint/crouch.
                var flick = Find(valheim, Stick("right"), "dpad", "touch");
                if (flick == null) problems.Add("NoStickFlickRunCrouch: right stick touch-flick binding not found");
                else
                {
                    ReplaceOrAdd(flick, "north", ToggleRun, StopGesturedLocomotion);
                    ReplaceOrAdd(flick, "south", ToggleCrouch, StopGesturedLocomotion);
                }
            }

            return problems;
        }

        // ---- JSON helpers ----

        internal static JArray Sources(JObject root, string actionSet)
        {
            return root?["bindings"]?[actionSet]?["sources"] as JArray;
        }

        internal static JObject Find(JArray sources, string path, string mode, string subMode)
        {
            return sources.OfType<JObject>().FirstOrDefault(s =>
                string.Equals((string)s["path"], path, StringComparison.OrdinalIgnoreCase) &&
                string.Equals((string)s["mode"], mode, StringComparison.OrdinalIgnoreCase) &&
                (subMode == null || string.Equals((string)s["parameters"]?["sub_mode"], subMode, StringComparison.OrdinalIgnoreCase)));
        }

        private static JObject Ensure(JArray sources, string path, string mode)
        {
            var source = Find(sources, path, mode, null);
            if (source != null) return source;
            source = new JObject
            {
                ["inputs"] = new JObject(),
                ["mode"] = mode,
                ["parameters"] = new JObject(),
                ["path"] = path,
            };
            sources.Add(source);
            return source;
        }

        private static void SetInput(JObject source, string input, string output)
        {
            var inputs = source["inputs"] as JObject;
            if (inputs == null) source["inputs"] = inputs = new JObject();
            inputs[input] = new JObject { ["output"] = output };
        }

        private static void RemoveInput(JObject source, string input, string expectedOutput)
        {
            var inputs = source["inputs"] as JObject;
            var existing = inputs?[input];
            if (existing != null && string.Equals((string)existing["output"], expectedOutput, StringComparison.OrdinalIgnoreCase))
                inputs.Remove(input);
        }

        /// <summary>Points an input at <paramref name="replacement"/> if it's missing or bound to <paramref name="unwanted"/>.</summary>
        private static void ReplaceOrAdd(JObject source, string input, string unwanted, string replacement)
        {
            var existing = (source["inputs"] as JObject)?[input];
            if (existing == null || string.Equals((string)existing["output"], unwanted, StringComparison.OrdinalIgnoreCase))
                SetInput(source, input, replacement);
        }

        private static void RemoveIfEmpty(JArray sources, JObject source)
        {
            if (!((source["inputs"] as JObject)?.HasValues ?? false)) sources.Remove(source);
        }
    }
}
