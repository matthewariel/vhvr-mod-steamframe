using System;
using System.Reflection;
using BepInEx.Configuration;
using HarmonyLib;

namespace VhvrSteamFrame
{
    /// <summary>
    /// A prefix on Player.SetControls that runs after VHVR's own sneak prefix and can cancel the crouch toggle it asked for:
    /// while a menu/chest/map is open, and (optionally) until the crouch button is double-clicked.
    /// </summary>
    internal class CrouchHooks : Module
    {
        public override string Name => "Crouch";

        private const string VhvrHarmonyId = "com.valheimvrmod.patches";

        internal static ConfigEntry<bool> BlockInMenus;
        internal static ConfigEntry<bool> DoubleClick;
        internal static ConfigEntry<float> DoubleClickWindow;

        private static readonly CrouchGate gate = new CrouchGate();
        private static FieldInfo crouchToggled;
        private static FieldInfo vhvrJoystickSneaking; // VHVR's "crouched with the button" flag (lowers the camera); may be null
        private static Func<bool> roomscaleSneaking;

        internal static bool DoubleClickEnabled => DoubleClick != null && DoubleClick.Value;

        public override void BindConfig(ConfigFile config)
        {
            BlockInMenus = config.Bind("Crouch", "BlockCrouchInMenus", true,
                "Ignore the crouch button while an inventory, chest, store, the big map, a text box or the pause menu is open. " +
                "On the Frame the right stick click is also VHVR's 'middle click' (move items, ping the map), so without this " +
                "every click there also crouches you.");
            DoubleClick = config.Bind("Crouch", "DoubleClickToCrouch", false,
                "Crouching needs two clicks of the crouch button in quick succession, so a stray click in a fight doesn't crouch " +
                "you. Standing up is still one click (or start running).");
            DoubleClickWindow = config.Bind("Crouch", "DoubleClickWindow", 0.45f, new ConfigDescription(
                "Seconds allowed between the two clicks when DoubleClickToCrouch is on.",
                new AcceptableValueRange<float>(0.2f, 1.0f)));

            gate.DoubleClickWindow = DoubleClickWindow.Value;
            DoubleClickWindow.SettingChanged += (_, __) => gate.DoubleClickWindow = DoubleClickWindow.Value;
        }

        protected override void Start(Harmony harmony)
        {
            crouchToggled = AccessTools.Field(typeof(Player), "m_crouchToggled");
            if (crouchToggled == null) throw new MissingMemberException("Player.m_crouchToggled not found (game update?)");

            try
            {
                roomscaleSneaking = Vhvr.Delegate<Func<bool>>(Vhvr.Getter(Vhvr.Type("ValheimVRMod.VRCore.VRPlayer"), "isRoomscaleSneaking"));
            }
            catch (Exception e)
            {
                roomscaleSneaking = () => false;
                Plugin.Log.LogWarning($"Crouch: can't tell physical ducking from the button ({e.Message}); double click applies to both.");
            }
            vhvrJoystickSneaking = AccessTools.Field(AccessTools.TypeByName("ValheimVRMod.Patches.Player_SetControls_SneakPatch"), "_isJoystickSneaking");
            if (vhvrJoystickSneaking == null)
                Plugin.Log.LogWarning("Crouch: VHVR's sneak flag not found; after a blocked click the crouch camera height may lag one toggle.");

            var setControls = AccessTools.Method(typeof(Player), nameof(Player.SetControls));
            if (setControls == null) throw new MissingMemberException("Player.SetControls not found (game update?)");
            harmony.Patch(setControls, prefix: new HarmonyMethod(typeof(CrouchHooks), nameof(SetControlsPrefix))
            {
                priority = Priority.Low,
                after = new[] { VhvrHarmonyId },
            });
        }

        public static void SetControlsPrefix(Player __instance, ref bool crouch)
        {
            if (!crouch || __instance != Player.m_localPlayer) return;

            bool crouched = (bool)crouchToggled.GetValue(__instance);
            bool fromButton = !roomscaleSneaking();
            var decision = gate.Decide(true, crouched, fromButton, MenuOpen(),
                BlockInMenus.Value, DoubleClick.Value, UnityEngine.Time.unscaledTime);
            if (decision == CrouchDecision.Allow) return;

            crouch = false;
            // VHVR flipped its flag for the toggle it asked for; put it back in line with the crouch state that stays.
            if (fromButton)
                try { vhvrJoystickSneaking?.SetValue(null, crouched); } catch { /* cosmetic only */ }
        }

        private static bool MenuOpen()
        {
            return InventoryGui.IsVisible() || Minimap.IsOpen() || StoreGui.IsVisible() || TextInput.IsVisible() ||
                   Menu.IsVisible() || Minimap.InTextInput();
        }
    }
}
