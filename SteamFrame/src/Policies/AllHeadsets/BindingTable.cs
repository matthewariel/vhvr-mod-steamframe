using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
#if VHVR_GAME
using Valve.Newtonsoft.Json.Linq; // the JSON library Valheim ships (SteamVR's copy)
#else
using Newtonsoft.Json.Linq;       // tests and the VHVR-compat build use the NuGet package
#endif

namespace VhvrSteamFrame
{
    /// <summary>One "button -> action" line of the controller guide.</summary>
    public sealed class BindingRow
    {
        public string Hand;    // "Left", "Right" or "Both"
        public string Control; // "A", "Grip + A", "Stick click", "Stick up (touch)"...
        public string Action;  // "Jump", "Crouch"...
        public int Order;
    }

    /// <summary>Turns a SteamVR binding document into a readable button -> action list (plain C#).</summary>
    public static class BindingTable
    {
        private static readonly string[] ButtonOrder =
            { "trigger", "grip", "a", "b", "x", "y", "thumbstick", "bumper", "dpad_up", "dpad_down", "dpad_left", "dpad_right", "view", "menu" };

        // Internal or analog actions that would only clutter the guide.
        private static readonly HashSet<string> Hidden = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "stopgesturedlocomotion", "grabgrip", "grabpinch", "squeeze", "interactui" };

        private static readonly Dictionary<string, string> Friendly = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "use", "Use / attack" }, { "grab", "Grab" }, { "jump", "Jump" }, { "dodge", "Dodge" },
            { "togglerun", "Sprint (toggle)" }, { "holdrun", "Sprint (hold)" }, { "togglecrouch", "Crouch (toggle)" },
            { "toggleinventory", "Inventory" }, { "togglemap", "Wrist map" }, { "togglemenu", "Menu" },
            { "quickswitch", "Quick switch menu" }, { "quickactions", "Quick actions menu (power, map...)" },
            { "walk", "Move" }, { "pitchandyaw", "Turn" },
            { "scrollup", "Scroll up / map zoom in" }, { "scrolldown", "Scroll down / map zoom out" },
            { "leftclick", "Laser click" }, { "rightclick", "Laser right-click" }, { "middleclick", "Middle click" },
            { "removepiece", "Remove build piece" }, { "rotatepiececlockwise", "Rotate piece right" },
            { "rotatepiececounterclockwise", "Rotate piece left" }, { "facepieceaway", "Tilt piece away" },
            { "facepiecetoward", "Tilt piece toward" }, { "splitstack", "Split stack" }, { "discarditem", "Quick-move item" },
            { "addmappin", "Add map pin" }, { "teleport", "Thumb on stick (punch sensing)" },
        };

        public static List<BindingRow> Build(JObject root)
        {
            var rows = new List<BindingRow>();
            foreach (var set in new[] { "/actions/valheim", "/actions/default" })
            {
                var sources = root?["bindings"]?[set]?["sources"] as JArray;
                if (sources != null)
                {
                    foreach (var source in sources.OfType<JObject>())
                        AddSource(rows, source);
                }

                var chords = root?["bindings"]?[set]?["chords"] as JArray;
                if (chords != null)
                {
                    foreach (var chord in chords.OfType<JObject>())
                        AddChord(rows, chord);
                }
            }

            return rows
                .GroupBy(r => r.Hand + "|" + r.Control + "|" + r.Action).Select(g => g.First())
                .OrderBy(r => r.Order).ThenBy(r => r.Control, StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>Rich text for one hand ("Left"/"Right"), with two-hand chords listed under both hands.</summary>
        public static string Format(List<BindingRow> rows, string hand)
        {
            var sb = new StringBuilder();
            sb.Append("<b>").Append(hand).Append(" controller</b>\n");
            foreach (var r in rows.Where(r => r.Hand == hand || r.Hand == "Both"))
                sb.Append("<b>").Append(r.Control).Append("</b>: ").Append(r.Action).Append('\n');
            return sb.ToString();
        }

        private static void AddSource(List<BindingRow> rows, JObject source)
        {
            string path = (string)source["path"];
            string mode = (string)source["mode"];
            string subMode = (string)source["parameters"]?["sub_mode"];
            var inputs = source["inputs"] as JObject;
            if (path == null || inputs == null) return;

            foreach (var input in inputs.Properties())
            {
                string action = ActionName((string)input.Value?["output"]);
                if (action == null || Hidden.Contains(action)) continue;
                if (mode == "trigger" && input.Name == "pull") continue; // same as click in the guide

                rows.Add(new BindingRow
                {
                    Hand = HandOf(path),
                    Control = ControlName(path, mode, subMode, input.Name),
                    Action = Friendly.TryGetValue(action, out var f) ? f : action,
                    Order = OrderOf(path),
                });
            }
        }

        private static void AddChord(List<BindingRow> rows, JObject chord)
        {
            string action = ActionName((string)chord["output"]);
            var inputs = chord["inputs"] as JArray;
            if (action == null || inputs == null || Hidden.Contains(action)) return;

            var parts = inputs.OfType<JArray>()
                .Select(i => new { Path = (string)i.FirstOrDefault(), Input = (string)i.Skip(1).FirstOrDefault() })
                .Where(p => p.Path != null).ToList();
            if (parts.Count == 0) return;
            var paths = parts.Select(p => p.Path).ToList();
            var hands = paths.Select(HandOf).Distinct().ToList();
            bool oneHand = hands.Count == 1;

            rows.Add(new BindingRow
            {
                Hand = oneHand ? hands[0] : "Both",
                Control = string.Join(" + ", parts.Select(p =>
                    (oneHand ? "" : HandOf(p.Path).Substring(0, 1) + " ") + ControlName(p.Path, "chord", null, p.Input ?? "click"))),
                Action = Friendly.TryGetValue(action, out var f) ? f : action,
                Order = 100 + paths.Select(OrderOf).Min(),
            });
        }

        private static string ActionName(string output)
        {
            if (string.IsNullOrEmpty(output)) return null;
            int slash = output.LastIndexOf('/');
            return slash >= 0 ? output.Substring(slash + 1) : output;
        }

        private static string HandOf(string path)
        {
            if (path.IndexOf("/left/", StringComparison.OrdinalIgnoreCase) >= 0) return "Left";
            if (path.IndexOf("/right/", StringComparison.OrdinalIgnoreCase) >= 0) return "Right";
            return "Both";
        }

        private static string Component(string path)
        {
            int slash = path.LastIndexOf('/');
            return slash >= 0 ? path.Substring(slash + 1).ToLowerInvariant() : path.ToLowerInvariant();
        }

        private static int OrderOf(string path)
        {
            int i = Array.IndexOf(ButtonOrder, Component(path));
            return i < 0 ? ButtonOrder.Length : i;
        }

        private static string ButtonName(string path)
        {
            string c = Component(path);
            switch (c)
            {
                case "a": case "b": case "x": case "y": return c.ToUpperInvariant();
                case "trigger": return "Trigger";
                case "grip": return "Grip";
                case "thumbstick": return "Stick";
                case "bumper": return "Bumper";
                case "view": return "View";
                case "menu": return "Menu";
                case "dpad_up": return "D-pad up";
                case "dpad_down": return "D-pad down";
                case "dpad_left": return "D-pad left";
                case "dpad_right": return "D-pad right";
                default: return c;
            }
        }

        private static string ControlName(string path, string mode, string subMode, string input)
        {
            string button = ButtonName(path);
            switch (input)
            {
                case "click": return mode == "joystick" || Component(path) == "thumbstick" ? button + " click" : button;
                case "touch": return button + " touch";
                case "position": return button;
                case "north": return button + " up" + (subMode == "touch" ? " (touch)" : "");
                case "south": return button + " down" + (subMode == "touch" ? " (touch)" : "");
                case "east": return button + " right" + (subMode == "touch" ? " (touch)" : "");
                case "west": return button + " left" + (subMode == "touch" ? " (touch)" : "");
                default: return button + " " + input;
            }
        }
    }
}
