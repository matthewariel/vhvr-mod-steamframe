using System.Collections.Generic;

namespace VhvrSteamFrame
{
    /// <summary>Which game DLL a member lives in.</summary>
    public enum ApiAssembly
    {
        /// <summary>ValheimVRMod.dll (BepInEx/plugins).</summary>
        Vhvr,
        /// <summary>One of Valheim's own DLLs (assembly_valheim, assembly_guiutils, assembly_utils).</summary>
        Valheim,
    }

    /// <summary>What kind of member an entry names.</summary>
    public enum ApiShape { Type, Method, Getter, Setter, Field, AnyMember }

    /// <summary>How the plugin uses it.</summary>
    public enum ApiKind
    {
        /// <summary>Patched with Harmony. For VHVR methods this only works if the method touches no private Valheim member.</summary>
        HarmonyPatch,
        /// <summary>Looked up by name and read, written or invoked through reflection.</summary>
        Reflect,
        /// <summary>Looked up by name and called through a plain delegate (no IL generation).</summary>
        Delegate,
        /// <summary>Used directly in the plugin's source, so the compiler baked the name into the DLL.</summary>
        Compiled,
    }

    /// <summary>One VHVR or Valheim type/member this plugin depends on.</summary>
    public sealed class ApiEntry
    {
        public string Module;       // which of our modules needs it
        public ApiAssembly Assembly;
        public string Type;         // full type name
        public string Member;       // null for a type entry
        public ApiShape Shape;
        public ApiKind Kind;
        public bool Optional;       // the module still starts without it (diagnostics, nicer behaviour)
        public string[] Parameters; // full type names for a specific overload; null = whichever method has the name

        public override string ToString()
        {
            string type = Type.Substring(Type.LastIndexOf('.') + 1);
            if (Member == null) return type;
            string args = Parameters == null ? "" : "(" + string.Join(", ", Parameters) + ")";
            return type + "." + Member + args;
        }
    }

    /// <summary>
    /// The one list of VHVR and Valheim internals this plugin reaches into: everything it looks up by name, patches with
    /// Harmony, or (for VHVR) uses directly. Pure strings, no game references, so tools/VhvrCompatCheck can read the same
    /// list and check a new VHVR or Valheim version before you install it.
    ///
    /// Adding a lookup anywhere in the plugin = add an entry here and use it through Vhvr.Method/Field/Type.
    /// </summary>
    public static class VhvrApi
    {
        /// <summary>VHVR versions this plugin has been played on. Others get one warning in the log at startup.</summary>
        public static readonly string[] TestedVhvrVersions = { "0.11.1" };

        private static readonly List<ApiEntry> all = new List<ApiEntry>();
        private static readonly List<ApiEntry> knownUnpatchable = new List<ApiEntry>();

        /// <summary>Every entry, in declaration order.</summary>
        public static IReadOnlyList<ApiEntry> All { get { return all; } }

        /// <summary>
        /// VHVR methods we would have liked to patch but can't, because they touch private Valheim members (Harmony's
        /// patched copy fails to compile). The plugin works around each one; the checker reports if that ever changes.
        /// </summary>
        public static IReadOnlyList<ApiEntry> KnownUnpatchable { get { return knownUnpatchable; } }

        private const string Fist = "ValheimVRMod.Scripts.FistCollision";
        private const string HandGesture = "ValheimVRMod.Scripts.HandGesture";
        private const string WeaponCollision = "ValheimVRMod.Scripts.WeaponCollision";
        private const string MeshCooldown = "ValheimVRMod.Scripts.AttackTargetMeshCooldown";
        private const string VrPlayer = "ValheimVRMod.VRCore.VRPlayer";
        private const string InputActions = "ValheimVRMod.VRCore.Backends.VRInputActions";
        private const string InputSource = "ValheimVRMod.VRCore.Backends.VRInputSource";
        private const string VhvrConfig = "ValheimVRMod.Utilities.VHVRConfig";
        private const string SneakPatch = "ValheimVRMod.Patches.Player_SetControls_SneakPatch";
        private const string ConfigSettings = "ValheimVRMod.VRCore.UI.ConfigSettings";

        private const string Str = "System.String";
        private const string Int = "System.Int32";
        private const string Bool = "System.Boolean";

        // ---- Punch (AllHeadsets/Punch) ----
        public static readonly ApiEntry FistType = Vhvr("Punch", Fist, null, ApiShape.Type, ApiKind.Compiled);
        public static readonly ApiEntry FistCanAttack = Vhvr("Punch", Fist, "canAttackWithCollision", ApiShape.Method, ApiKind.HarmonyPatch);
        public static readonly ApiEntry FistHandGesture = Vhvr("Punch", Fist, "handGesture", ApiShape.Field, ApiKind.Reflect);
        public static readonly ApiEntry FistLastGrabbedType = Vhvr("Punch", Fist, "lastGrabbedType", ApiShape.Field, ApiKind.Reflect);
        public static readonly ApiEntry FistIsRightHand = Vhvr("Punch", Fist, "isRightHand", ApiShape.Field, ApiKind.Reflect);
        public static readonly ApiEntry FistInputSource = Vhvr("Punch", Fist, "inputSource", ApiShape.Getter, ApiKind.Delegate);
        public static readonly ApiEntry FistIsLaserPointerActive = Vhvr("Punch", Fist, "isLaserPointerActive", ApiShape.Method, ApiKind.Delegate);
        public static readonly ApiEntry FistTryHitCollider = Vhvr("Punch", Fist, "tryHitCollider", ApiShape.Method, ApiKind.Delegate, false, "UnityEngine.Collider", Bool);
        public static readonly ApiEntry FistHasMomentum = Vhvr("Punch", Fist, "hasMomentum", ApiShape.Method, ApiKind.Delegate, true);
        public static readonly ApiEntry VrPlayerInFirstPerson = Vhvr("Punch", VrPlayer, "inFirstPerson", ApiShape.Getter, ApiKind.Reflect);
        public static readonly ApiEntry VrPlayerInstance = Vhvr("Punch", VrPlayer, "instance", ApiShape.Getter, ApiKind.Reflect, true);
        public static readonly ApiEntry ConfigSwingSpeedRequirement = Vhvr("Punch", VhvrConfig, "SwingSpeedRequirement", ApiShape.Method, ApiKind.Reflect, true);
        public static readonly ApiEntry MeshCooldownType = Vhvr("Punch", MeshCooldown, null, ApiShape.Type, ApiKind.Reflect, true);
        public static readonly ApiEntry MeshCooldownInCoolDown = Vhvr("Punch", MeshCooldown, "inCoolDown", ApiShape.Method, ApiKind.Reflect, true);
        public static readonly ApiEntry HandGestureIsHandFree = Vhvr("Punch", HandGesture, "isHandFree", ApiShape.Method, ApiKind.Compiled);
        public static readonly ApiEntry WeaponCollisionIsFriendly = Vhvr("Punch", WeaponCollision, "IsFriendly", ApiShape.Method, ApiKind.Compiled);
        public static readonly ApiEntry WeaponCollisionIsTrainingDummy = Vhvr("Punch", WeaponCollision, "IsTrainingDummy", ApiShape.Method, ApiKind.Compiled);
        public static readonly ApiEntry ActionGrab = Vhvr("Punch", InputActions, "valheim_Grab", ApiShape.AnyMember, ApiKind.Compiled);
        public static readonly ApiEntry ActionUse = Vhvr("Punch", InputActions, "valheim_Use", ApiShape.AnyMember, ApiKind.Compiled);
        public static readonly ApiEntry ActionTeleport = Vhvr("Punch", InputActions, "default_Teleport", ApiShape.AnyMember, ApiKind.Compiled);
        public static readonly ApiEntry InputSourceRightHand = Vhvr("Punch", InputSource, "RightHand", ApiShape.Field, ApiKind.Compiled);
        public static readonly ApiEntry InputSourceLeftHand = Vhvr("Punch", InputSource, "LeftHand", ApiShape.Field, ApiKind.Compiled);
        public static readonly ApiEntry PlayerInCraftingStation = Valheim("Punch", "Player", "m_inCraftingStation", ApiShape.Field, ApiKind.Reflect);
        public static readonly ApiEntry HumanoidRightItem = Valheim("Punch", "Humanoid", "m_rightItem", ApiShape.Field, ApiKind.Reflect, true);
        public static readonly ApiEntry HumanoidLeftItem = Valheim("Punch", "Humanoid", "m_leftItem", ApiShape.Field, ApiKind.Reflect, true);

        // ---- Posture (AllHeadsets/Posture) ----
        public static readonly ApiEntry VrPlayerReferenceHeight = Vhvr("Posture", VrPlayer, "referencePlayerHeight", ApiShape.Getter, ApiKind.HarmonyPatch);
        public static readonly ApiEntry VrPlayerRawEyeHeight = Vhvr("Posture", VrPlayer, "rawCaliberatedEyeHeight", ApiShape.Field, ApiKind.Reflect);
        public static readonly ApiEntry VrPlayerRequestRecentering = Vhvr("Posture", VrPlayer, "RequestRecentering", ApiShape.Method, ApiKind.Reflect, true, Bool);
        public static readonly ApiEntry VrPlayerVrPlayerInstance = Vhvr("Posture", VrPlayer, "vrPlayerInstance", ApiShape.Getter, ApiKind.Reflect, true);
        public static readonly ApiEntry VrPlayerRequestPelvis = Vhvr("Posture", VrPlayer, "RequestPelvisCaliberation", ApiShape.Method, ApiKind.Reflect, true);
        public static readonly ApiEntry VrPlayerResetRoomscaleCamera = Vhvr("Posture", VrPlayer, "ResetRoomscaleCamera", ApiShape.Method, ApiKind.Reflect, true);
        public static readonly ApiEntry VrPlayerShouldPauseMovement = Vhvr("Posture", VrPlayer, "ShouldPauseMovement", ApiShape.Getter, ApiKind.Reflect, true);

        // ---- Crouch (AllHeadsets/Crouch) ----
        public static readonly ApiEntry PlayerSetControls = Valheim("Crouch", "Player", "SetControls", ApiShape.Method, ApiKind.HarmonyPatch);
        public static readonly ApiEntry PlayerCrouchToggled = Valheim("Crouch", "Player", "m_crouchToggled", ApiShape.Field, ApiKind.Reflect);
        public static readonly ApiEntry VrPlayerIsRoomscaleSneaking = Vhvr("Crouch", VrPlayer, "isRoomscaleSneaking", ApiShape.Getter, ApiKind.Delegate, true);
        public static readonly ApiEntry SneakPatchIsJoystickSneaking = Vhvr("Crouch", SneakPatch, "_isJoystickSneaking", ApiShape.Field, ApiKind.Reflect, true);

        // ---- MenuStickClicks (AllHeadsets/MenuStickClicks) ----
        public static readonly ApiEntry ZInputGetButtonDown = Valheim("MenuStickClicks", "ZInput", "GetButtonDown", ApiShape.Method, ApiKind.HarmonyPatch, false, Str);
        public static readonly ApiEntry ActionToggleCrouch = Vhvr("MenuStickClicks", InputActions, "valheim_ToggleCrouch", ApiShape.AnyMember, ApiKind.Compiled);
        public static readonly ApiEntry ActionToggleRun = Vhvr("MenuStickClicks", InputActions, "valheim_ToggleRun", ApiShape.AnyMember, ApiKind.Compiled);

        // ---- Prompts (AllHeadsets/Prompts) ----
        public static readonly ApiEntry LocalizationTranslate = Valheim("Prompts", "Localization", "Translate", ApiShape.Method, ApiKind.HarmonyPatch, false, Str);
        public static readonly ApiEntry ZInputIsGamepadActive = Valheim("Prompts", "ZInput", "IsGamepadActive", ApiShape.Method, ApiKind.Reflect);
        public static readonly ApiEntry ZInputGamepadActive = Valheim("Prompts", "ZInput", "GamepadActive", ApiShape.Getter, ApiKind.Reflect, true);
        public static readonly ApiEntry ZInputIsMouseActive = Valheim("Prompts", "ZInput", "IsMouseActive", ApiShape.Method, ApiKind.Reflect, true);

        /// <summary>Display-only Valheim methods whose "is a gamepad active?" calls get rewritten. A missing one is skipped.</summary>
        public static readonly ApiEntry[] PromptTextMethods =
        {
            Valheim("Prompts", "Localization", "Translate", ApiShape.Method, ApiKind.HarmonyPatch, true),   // every $KEY_Xxx token in hover text and UI strings
            Valheim("Prompts", "Hud", "UpdateCrosshair", ApiShape.Method, ApiKind.HarmonyPatch, true),      // tidies brackets around gamepad glyphs in hover text
            Valheim("Prompts", "Tameable", "GetHoverText", ApiShape.Method, ApiKind.HarmonyPatch, true),    // picks gamepad vs keyboard wording for combos
            Valheim("Prompts", "Sadle", "GetHoverText", ApiShape.Method, ApiKind.HarmonyPatch, true),
            Valheim("Prompts", "Pet", "GetHoverText", ApiShape.Method, ApiKind.HarmonyPatch, true),
            Valheim("Prompts", "ItemStand", "GetHoverText", ApiShape.Method, ApiKind.HarmonyPatch, true),
            Valheim("Prompts", "CookingStation", "HoverText", ApiShape.Method, ApiKind.HarmonyPatch, true),
        };

        /// <summary>The little button icons next to menu buttons.</summary>
        public static readonly ApiEntry[] MenuHintMethods =
        {
            Valheim("Prompts", "UIInputHint", "UpdateInputHints", ApiShape.Method, ApiKind.HarmonyPatch, true),
        };

        // ---- Settings tab (UI/SettingsTab) ----
        public static readonly ApiEntry SettingsCreateTab = Vhvr("FrameTab", ConfigSettings, "CreateTab", ApiShape.Method, ApiKind.HarmonyPatch, false, Str, Int);
        public static readonly ApiEntry SettingsCreateFbtTab = Vhvr("FrameTab", ConfigSettings, "CreateFullBodyTrackingTab", ApiShape.Method, ApiKind.HarmonyPatch, false, Int);
        public static readonly ApiEntry SettingsCreateElement = Vhvr("FrameTab", ConfigSettings, "createElement", ApiShape.Method, ApiKind.Reflect);
        public static readonly ApiEntry SettingsTogglePrefab = Vhvr("FrameTab", ConfigSettings, "togglePrefab", ApiShape.Field, ApiKind.Reflect);
        public static readonly ApiEntry SettingsTransformButtonPrefab = Vhvr("FrameTab", ConfigSettings, "transformButtonPrefab", ApiShape.Field, ApiKind.Reflect, true);

        // ---- Server passwords (Extras/ServerPasswords) ----
        public static readonly ApiEntry FejdServerPasswordGet = Valheim("ServerPasswords", "FejdStartup", "ServerPassword", ApiShape.Getter, ApiKind.Reflect);
        public static readonly ApiEntry FejdServerPasswordSet = Valheim("ServerPasswords", "FejdStartup", "ServerPassword", ApiShape.Setter, ApiKind.Reflect);
        public static readonly ApiEntry ZNetClientHandshake = Valheim("ServerPasswords", "ZNet", "RPC_ClientHandshake", ApiShape.Method, ApiKind.HarmonyPatch);
        public static readonly ApiEntry ZNetOnPasswordEntered = Valheim("ServerPasswords", "ZNet", "OnPasswordEntered", ApiShape.Method, ApiKind.HarmonyPatch, false, Str);

        // ---- Known traps: not used, only watched ----
        public static readonly ApiEntry TrapCreateModSettings = Trap("FrameTab", ConfigSettings, "createModSettings");
        public static readonly ApiEntry TrapFistOnTriggerEnter = Trap("Punch", Fist, "OnTriggerEnter");

        private static ApiEntry Vhvr(string module, string type, string member, ApiShape shape, ApiKind kind, bool optional = false, params string[] parameters)
        {
            return Add(ApiAssembly.Vhvr, module, type, member, shape, kind, optional, parameters);
        }

        private static ApiEntry Valheim(string module, string type, string member, ApiShape shape, ApiKind kind, bool optional = false, params string[] parameters)
        {
            return Add(ApiAssembly.Valheim, module, type, member, shape, kind, optional, parameters);
        }

        private static ApiEntry Add(ApiAssembly assembly, string module, string type, string member, ApiShape shape, ApiKind kind, bool optional, string[] parameters)
        {
            var entry = new ApiEntry
            {
                Module = module, Assembly = assembly, Type = type, Member = member, Shape = shape, Kind = kind, Optional = optional,
                Parameters = parameters == null || parameters.Length == 0 ? null : parameters,
            };
            all.Add(entry);
            return entry;
        }

        private static ApiEntry Trap(string module, string type, string member)
        {
            var entry = new ApiEntry
            {
                Module = module, Assembly = ApiAssembly.Vhvr, Type = type, Member = member, Shape = ApiShape.Method, Kind = ApiKind.HarmonyPatch, Optional = true,
            };
            knownUnpatchable.Add(entry);
            return entry;
        }
    }
}
