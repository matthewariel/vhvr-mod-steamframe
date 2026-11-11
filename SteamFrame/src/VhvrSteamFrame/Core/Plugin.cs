using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;

namespace VhvrSteamFrame
{
    /// <summary>
    /// Steam Frame support for VHVR, as independent modules grouped by who they are for: AllHeadsets/, SteamFrame/ and
    /// Extras/ (see docs/ARCHITECTURE.md). Each module keeps its rules in plain C# under src/Policies (the part that
    /// could move into VHVR) and its Harmony/reflection glue in a *Hooks class here.
    /// </summary>
    [BepInPlugin(Guid, Name, Version)]
    [BepInDependency(VhvrGuid, BepInDependency.DependencyFlags.HardDependency)]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "local.vhvr.steamframe";
        public const string Name = "Steam Frame for VHVR";
        public const string Version = "0.2.0";
        public const string VhvrGuid = "org.bepinex.plugins.valheimvrmod";

        internal static ManualLogSource Log;
        internal static Plugin Instance;
        internal static ConfigEntry<string> FrameDetection;

        private readonly List<Module> modules = new List<Module>();

        private void Awake()
        {
            Instance = this;
            Log = Logger;

            FrameDetection = Config.Bind("General", "FrameDetection", "Auto", new ConfigDescription(
                "Auto: Frame-specific behaviour (button labels) turns on when SteamVR reports Steam Frame controllers. " +
                "Force: always on. Off: always off. Binding tweaks and other settings apply either way.",
                new AcceptableValueList<string>("Auto", "Force", "Off")));
            var imported = Config.Bind("General", "ImportedOldSettings", false,
                "Set once settings from the older 'VR Gamepad Prompts' plugin have been carried over (or there were none).");

            modules.Add(new PromptsHooks());
            modules.Add(new MenuStickClicksHooks());
            modules.Add(new CrouchHooks());
            modules.Add(new MapSharingHooks());
            modules.Add(new ServerPasswordsHooks());
            modules.Add(new PunchHooks());
            modules.Add(new PostureHooks());
            modules.Add(new BindingsHooks());
            modules.Add(new FrameTabHooks());

            foreach (var m in modules)
            {
                try { m.BindConfig(Config); }
                catch (Exception e) { Log.LogError($"{m.Name}: config failed: {e}"); }
            }

            if (!imported.Value)
            {
                ConfigMigration.ImportFromVrGamepadPrompts(Config);
                imported.Value = true;
            }

            foreach (var m in modules) m.TryStart();

            Log.LogInfo($"{Name} {Version}: {modules.Count(m => m.Active)}/{modules.Count} modules active " +
                        $"({string.Join(", ", modules.Where(m => !m.Active).Select(m => m.Name + " off").DefaultIfEmpty("all on"))}).");
            Log.LogInfo($"Modules: [all headsets] {Group(ModuleScope.AllHeadsets)} [Steam Frame] {Group(ModuleScope.SteamFrame)} " +
                        $"[extras] {Group(ModuleScope.Extra)}");
        }

        /// <summary>"Prompts, Crouch (off), Punch" for the startup log.</summary>
        private string Group(ModuleScope scope)
        {
            return string.Join(", ", modules.Where(m => m.Scope == scope).Select(m => m.Name + (m.Active ? "" : " (off)")).DefaultIfEmpty("none"));
        }

        private void Update()
        {
            ControllerDetector.Tick();
            foreach (var m in modules) m.TryTick();
            Hud.Tick();
        }

        internal static T Get<T>() where T : Module
        {
            return Instance == null ? null : Instance.modules.OfType<T>().FirstOrDefault();
        }
    }
}
