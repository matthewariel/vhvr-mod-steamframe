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
    /// <summary>One button of a Steam Frame controller, as the Button guide page shows it (one box each).</summary>
    public sealed class FrameButton
    {
        /// <summary>"left/dpad_up", "right/a", "right/thumbstick"... The key used in the ButtonRemaps setting.</summary>
        public readonly string Id;
        public readonly string Hand;      // "left" or "right"
        public readonly string Component; // the SteamVR input name: "a", "dpad_up", "thumbstick", "trigger"...
        public readonly string Label;     // "D-pad up", "Stick click"...
        /// <summary>The binding input the box shows: "click", or "position" for what the stick does when pushed.</summary>
        public readonly string Input;
        /// <summary>False for the trigger, grip and stick movement: VHVR's laser, grabbing and moving need them.</summary>
        public readonly bool Remappable;

        public FrameButton(string hand, string component, string label, string input = "click", bool remappable = true)
        {
            Hand = hand;
            Component = component;
            Label = label;
            Input = input;
            Remappable = remappable;
            Id = hand + "/" + component + (input == "click" ? "" : "/" + input);
        }

        public string Path => "/user/hand/" + Hand + "/input/" + Component;
    }

    /// <summary>One action a button can be given on the Button guide page.</summary>
    public sealed class RemapChoice
    {
        public readonly string Action; // last part of the SteamVR action path ("jump"), or FrameButtons.Nothing
        public readonly string Label;

        public RemapChoice(string action, string label)
        {
            Action = action;
            Label = label;
        }
    }

    /// <summary>
    /// The Steam Frame buttons the Button guide draws, what each one does in a binding document, and the player's
    /// remaps ("this button does that instead"). Remaps are edits to the binding document, applied after the binding
    /// tweaks; the page stores them in the [Bindings] ButtonRemaps setting as "right/a=dodge;left/bumper=none".
    /// </summary>
    public static class FrameButtons
    {
        public const string Nothing = "none";
        private const string ValheimSet = "/actions/valheim";
        private const string ActionPrefix = "/actions/valheim/in/";
        private const string StopGesturedLocomotion = "stopgesturedlocomotion";

        /// <summary>Longest text a box shows on its action line (190 units at 13-14 pt).</summary>
        public const int MaxBoxChars = 28;

        /// <summary>
        /// Left controller boxes, top to bottom: the face buttons and stick, then the grip button on the handle's neck (all
        /// on the view from above), then bumper and trigger (the side view under it). draw_frame_controllers.ps1 draws the
        /// leader lines in this order.
        /// </summary>
        public static readonly IReadOnlyList<FrameButton> Left = new[]
        {
            new FrameButton("left", "view", "View"),
            new FrameButton("left", "dpad_up", "D-pad up"),
            new FrameButton("left", "dpad_left", "D-pad left"),
            new FrameButton("left", "dpad_down", "D-pad down"),
            new FrameButton("left", "dpad_right", "D-pad right"),
            new FrameButton("left", "thumbstick", "Stick click"),
            new FrameButton("left", "thumbstick", "Stick", "position", false),
            new FrameButton("left", "grip", "Grip", remappable: false),
            new FrameButton("left", "bumper", "Bumper"),
            new FrameButton("left", "trigger", "Trigger", remappable: false),
        };

        /// <summary>Right controller boxes, top to bottom.</summary>
        public static readonly IReadOnlyList<FrameButton> Right = new[]
        {
            new FrameButton("right", "menu", "Menu"),
            new FrameButton("right", "y", "Y"),
            new FrameButton("right", "b", "B"),
            new FrameButton("right", "x", "X"),
            new FrameButton("right", "a", "A"),
            new FrameButton("right", "thumbstick", "Stick click"),
            new FrameButton("right", "thumbstick", "Stick", "position", false),
            new FrameButton("right", "grip", "Grip", remappable: false),
            new FrameButton("right", "bumper", "Bumper"),
            new FrameButton("right", "trigger", "Trigger", remappable: false),
        };

        public static IEnumerable<FrameButton> All => Left.Concat(Right);

        public static FrameButton ById(string id) =>
            All.FirstOrDefault(b => string.Equals(b.Id, id, StringComparison.OrdinalIgnoreCase));

        /// <summary>What a button can be set to, in the order the picker shows them (3 columns).</summary>
        public static readonly IReadOnlyList<RemapChoice> Choices = new[]
        {
            Choice("jump"), Choice("dodge"), Choice("holdrun"),
            Choice("togglerun"), Choice("togglecrouch"), Choice("toggleinventory"),
            Choice("togglemap"), Choice("togglemenu"), Choice("quickswitch"),
            Choice("quickactions"), Choice("rightclick"), Choice("middleclick"),
            Choice("scrollup"), Choice("scrolldown"), new RemapChoice(Nothing, "Nothing"),
        };

        private static RemapChoice Choice(string action) => new RemapChoice(action, BindingTable.ActionLabel(action));

        private static bool IsChoice(string action) => Choices.Any(c => c.Action == action);

        // ---- Reading a binding document ----

        /// <summary>The actions a button triggers (in the Valheim action set), e.g. ["togglecrouch", "middleclick"].</summary>
        public static List<string> ActionsOn(JObject root, FrameButton button)
        {
            var result = new List<string>();
            var sources = FrameBindingPatcher.Sources(root, ValheimSet);
            if (sources == null) return result;
            foreach (var source in sources.OfType<JObject>().Where(s => SamePath(s, button.Path)))
            {
                string action = ActionName((string)source["inputs"]?[button.Input]?["output"]);
                if (action != null && action != StopGesturedLocomotion && !result.Contains(action)) result.Add(action);
            }
            return result;
        }

        /// <summary>The box's action line: "Jump", "Middle click (ping) + Crouch", "Nothing". At most <see cref="MaxBoxChars"/>.</summary>
        public static string Describe(JObject root, FrameButton button)
        {
            var labels = ActionsOn(root, button).Select(BindingTable.ActionLabel).ToList();
            if (labels.Count == 0) return "Nothing";
            string all = string.Join(" + ", labels);
            if (all.Length <= MaxBoxChars) return all;
            string first = labels[0].Length <= MaxBoxChars - 4 ? labels[0] : labels[0].Substring(0, MaxBoxChars - 5) + ".";
            return first + " +" + (labels.Count - 1);
        }

        /// <summary>
        /// The two-button combinations (SteamVR chords), one line each: "Grip + Trigger: Add map pin / Quick-move item".
        /// The same combination on either hand is one line; a combination on one hand only says which.
        /// </summary>
        public static List<string> ChordLines(JObject root)
        {
            var chords = root?["bindings"]?[ValheimSet]?["chords"] as JArray;
            var order = new List<string>();
            var actions = new Dictionary<string, List<string>>();
            var hands = new Dictionary<string, HashSet<string>>();
            var bothHanded = new Dictionary<string, bool>();
            if (chords == null) return new List<string>();

            foreach (var chord in chords.OfType<JObject>())
            {
                string action = ActionName((string)chord["output"]);
                var parts = (chord["inputs"] as JArray)?.OfType<JArray>()
                    .Select(i => new { Path = (string)i.FirstOrDefault(), Input = (string)i.Skip(1).FirstOrDefault() })
                    .Where(p => p.Path != null).ToList();
                if (action == null || parts == null || parts.Count == 0) continue;

                var partHands = parts.Select(p => HandOf(p.Path)).Distinct().ToList();
                string key;
                if (partHands.Count == 1)
                    key = string.Join(" + ", parts.Select(p => PartName(p.Path, p.Input)));
                else
                    key = string.Join(" + ", parts.Select((p, i) => (i == 0 ? "" : "other ") + PartName(p.Path, p.Input)));

                if (!actions.ContainsKey(key))
                {
                    order.Add(key);
                    actions[key] = new List<string>();
                    hands[key] = new HashSet<string>();
                    bothHanded[key] = parts.All(p => OnBothHands(Component(p.Path)));
                }
                string label = ChordLabel(action);
                if (!actions[key].Contains(label)) actions[key].Add(label);
                foreach (var h in partHands) hands[key].Add(h);
            }

            return order.Select(key =>
            {
                string which = bothHanded[key] && hands[key].Count == 1 ? " (" + hands[key].First() + ")" : "";
                return key + which + ": " + string.Join(" / ", actions[key]);
            }).ToList();
        }

        // ---- Remaps ----

        /// <summary>"right/a=dodge; left/bumper=none" -> {right/a: dodge, left/bumper: none}. Unreadable parts are skipped.</summary>
        public static Dictionary<string, string> ParseRemaps(string text)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(text)) return result;
            foreach (var part in text.Split(';'))
            {
                int eq = part.IndexOf('=');
                if (eq <= 0) continue;
                string id = part.Substring(0, eq).Trim().ToLowerInvariant();
                string action = part.Substring(eq + 1).Trim().ToLowerInvariant();
                if (id.Length > 0 && action.Length > 0) result[id] = action;
            }
            return result;
        }

        /// <summary>The setting text for a set of remaps, in box order so it reads the same every time.</summary>
        public static string FormatRemaps(IDictionary<string, string> remaps)
        {
            if (remaps == null || remaps.Count == 0) return "";
            var ids = All.Select(b => b.Id).ToList();
            return string.Join(";", remaps
                .OrderBy(r => { int i = ids.IndexOf(r.Key.ToLowerInvariant()); return i < 0 ? int.MaxValue : i; })
                .ThenBy(r => r.Key, StringComparer.Ordinal)
                .Select(r => r.Key.ToLowerInvariant() + "=" + r.Value.ToLowerInvariant()));
        }

        /// <summary>
        /// Gives each remapped button its new action: its click no longer does what it did (in the Valheim action set;
        /// two-button combinations and touch sensing are left alone), and does the new action instead, or nothing.
        /// Returns a note per remap that couldn't be applied. Applying twice changes nothing.
        /// </summary>
        public static List<string> ApplyRemaps(JObject root, IDictionary<string, string> remaps)
        {
            var problems = new List<string>();
            if (remaps == null || remaps.Count == 0) return problems;
            var sources = FrameBindingPatcher.Sources(root, ValheimSet);
            if (sources == null)
            {
                problems.Add("remaps: no /actions/valheim sources");
                return problems;
            }

            foreach (var remap in remaps)
            {
                var button = ById(remap.Key);
                string action = (remap.Value ?? "").ToLowerInvariant();
                if (button == null || !button.Remappable) { problems.Add("remap " + remap.Key + ": not a button that can be changed"); continue; }
                if (!IsChoice(action)) { problems.Add("remap " + remap.Key + ": unknown action '" + remap.Value + "'"); continue; }
                SetClick(sources, button, action);
            }
            return problems;
        }

        /// <summary>
        /// Why a button can't be given an action, or null when it can. The Menu action must stay on some button:
        /// without it the player couldn't open the menu, or this page to undo it.
        /// </summary>
        public static string Refusal(JObject root, string buttonId, string action)
        {
            var button = ById(buttonId);
            if (button == null || !button.Remappable) return "That button can't be changed here.";
            action = (action ?? "").ToLowerInvariant();
            if (!IsChoice(action)) return "Unknown action.";

            var copy = (JObject)root.DeepClone();
            var problems = ApplyRemaps(copy, new Dictionary<string, string> { { button.Id, action } });
            if (problems.Count > 0) return "Couldn't change it: " + problems[0];
            if (HasMenuButton(root) && !HasMenuButton(copy))
                return "Menu has to stay on a button, or you couldn't open the menu (or this page) again. Put Menu on another button first.";
            return null;
        }

        private static bool HasMenuButton(JObject root) => All.Any(b => b.Input == "click" && ActionsOn(root, b).Contains("togglemenu"));

        private static void SetClick(JArray sources, FrameButton button, string action)
        {
            JObject keep = null;
            foreach (var source in sources.OfType<JObject>().Where(s => SamePath(s, button.Path)).ToList())
            {
                var inputs = source["inputs"] as JObject;
                string current = ActionName((string)inputs?["click"]?["output"]);
                if (current == null || current == StopGesturedLocomotion) continue;
                if (keep == null) keep = source;
                inputs.Remove("click");
                if (!inputs.HasValues) sources.Remove(source);
            }
            if (action == Nothing) return;

            if (keep == null)
            {
                keep = FrameBindingPatcher.Find(sources, button.Path, "button", null);
                if (keep == null)
                {
                    keep = new JObject { ["inputs"] = new JObject(), ["mode"] = "button", ["parameters"] = new JObject(), ["path"] = button.Path };
                    sources.Add(keep);
                }
            }
            else if (keep.Parent == null)
            {
                sources.Add(keep);
            }
            FrameBindingPatcher.SetInput(keep, "click", ActionPrefix + action);
        }

        // ---- Names ----

        private static bool SamePath(JObject source, string path) =>
            string.Equals((string)source["path"], path, StringComparison.OrdinalIgnoreCase);

        private static string ActionName(string output)
        {
            if (string.IsNullOrEmpty(output)) return null;
            int slash = output.LastIndexOf('/');
            return (slash >= 0 ? output.Substring(slash + 1) : output).ToLowerInvariant();
        }

        private static string HandOf(string path) =>
            path.IndexOf("/left/", StringComparison.OrdinalIgnoreCase) >= 0 ? "left" :
            path.IndexOf("/right/", StringComparison.OrdinalIgnoreCase) >= 0 ? "right" : "";

        private static string Component(string path)
        {
            int slash = path.LastIndexOf('/');
            return (slash >= 0 ? path.Substring(slash + 1) : path).ToLowerInvariant();
        }

        private static bool OnBothHands(string component) =>
            component == "trigger" || component == "grip" || component == "thumbstick" || component == "bumper";

        private static string PartName(string path, string input)
        {
            string c = Component(path);
            var b = All.FirstOrDefault(x => x.Component == c && x.Input == "click");
            string name = c == "thumbstick" ? "Stick" : b != null ? b.Label : c;
            if (input == "north" || input == "south" || input == "east" || input == "west") name += " tilt";
            return name;
        }

        private static string ChordLabel(string action)
        {
            switch (action)
            {
                case "rotatepiececlockwise":
                case "rotatepiececounterclockwise": return "Rotate piece";
                case "facepieceaway":
                case "facepiecetoward": return "Flip piece";
                default: return BindingTable.ActionLabel(action);
            }
        }
    }
}
