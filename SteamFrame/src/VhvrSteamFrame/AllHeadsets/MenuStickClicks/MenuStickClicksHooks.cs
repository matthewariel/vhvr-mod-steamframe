using System;
using BepInEx.Configuration;
using HarmonyLib;
using ValheimVRMod.VRCore.Backends;

namespace VhvrSteamFrame
{
    /// <summary>
    /// While the inventory/container screen is open, reports JoyRStick / JoyLStick as pressed when the matching stick is
    /// clicked, so "Place stacks" and "Take all" work. Outside that screen nothing changes.
    /// </summary>
    internal class MenuStickClicksHooks : Module
    {
        public override string Name => "MenuStickClicks";
        public override ModuleScope Scope => ModuleScope.AllHeadsets;

        internal static ConfigEntry<bool> Enabled;

        public override void BindConfig(ConfigFile config)
        {
            Enabled = config.Bind("Menus", "StickClickMenuButtons", true,
                "In chests and graves: right stick click = Place stacks, left stick click = Take all " +
                "(the buttons Valheim labels with stick clicks). Needs the Frame binding tweaks for stick clicks.");
        }

        protected override void Start(Harmony harmony)
        {
            var getButtonDown = AccessTools.Method(typeof(ZInput), nameof(ZInput.GetButtonDown), new[] { typeof(string) });
            if (getButtonDown == null) throw new MissingMemberException("ZInput.GetButtonDown(string) not found");
            harmony.Patch(getButtonDown, postfix: new HarmonyMethod(typeof(MenuStickClicksHooks), nameof(GetButtonDownPostfix)));
        }

        public static void GetButtonDownPostfix(string __0, ref bool __result)
        {
            if (__result || !Enabled.Value) return;
            var action = MenuStickClicksPolicy.ActionFor(__0);
            if (action == MenuStickClicksPolicy.StickAction.None) return;
            try
            {
                if (!InventoryGui.IsVisible()) return;
                __result = action == MenuStickClicksPolicy.StickAction.ToggleCrouchRight
                    ? VRInputActions.valheim_ToggleCrouch.GetStateDown(VRInputSource.RightHand)
                    : VRInputActions.valheim_ToggleRun.GetStateDown(VRInputSource.LeftHand);
            }
            catch
            {
                // VHVR not ready (main menu, loading): leave Valheim's answer alone.
            }
        }
    }
}
