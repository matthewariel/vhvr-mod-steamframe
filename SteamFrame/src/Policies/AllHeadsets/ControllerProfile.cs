using System;
using System.Collections.Generic;

namespace VhvrSteamFrame
{
    /// <summary>What the prompt labels depend on besides the controller itself.</summary>
    public sealed class PromptLabelOptions
    {
        /// <summary>The right stick click toggles crouch (a binding tweak), instead of VHVR's default.</summary>
        public bool CrouchOnStickClick;
        /// <summary>The left stick click toggles sprint (a binding tweak), instead of VHVR's default.</summary>
        public bool SprintOnStickClick;
        /// <summary>Crouching needs a double click.</summary>
        public bool DoubleClickCrouch;
    }

    /// <summary>
    /// Everything the all-headset code needs to know about one kind of VR controller. Data only: no game hooks.
    /// All-headset modules never name a headset; they ask the active profile (or do nothing special when there is none).
    /// Supporting another controller = one new subclass, listed in <see cref="ControllerProfiles"/>.
    /// </summary>
    public abstract class ControllerProfile
    {
        /// <summary>Short stable id, e.g. "steam-frame".</summary>
        public abstract string Id { get; }

        /// <summary>Name shown to the player, e.g. "Steam Frame".</summary>
        public abstract string DisplayName { get; }

        /// <summary>SteamVR's controller_type string for this controller (see VHVR's actions.json default_bindings).</summary>
        public abstract string ControllerType { get; }

        /// <summary>The binding file VHVR ships for this controller, inside StreamingAssets/SteamVR.</summary>
        public abstract string BindingFileName { get; }

        /// <summary>Valheim action ("Use", "Jump"...) -> the label or glyph of the button VHVR binds it to.</summary>
        public abstract Dictionary<string, string> PromptLabels(PromptLabelOptions options);

        /// <summary>Valheim's big-map hint keys ("MapZoomIn", "ButtonX"...) -> the button that does that job with VHVR's laser.</summary>
        public abstract Dictionary<string, string> MapLabels();

        /// <summary>The button that closes the big map, for an extra hint Valheim doesn't have; null = no such hint.</summary>
        public virtual string MapCloseLabel()
        {
            return null;
        }

        public bool Matches(string controllerType)
        {
            return string.Equals(controllerType, ControllerType, StringComparison.OrdinalIgnoreCase);
        }
    }
}
