using System;
using BepInEx.Configuration;
using HarmonyLib;

namespace VhvrSteamFrame
{
    /// <summary>Who a module is for. Drives the source folders, the startup log, the settings tab badges and the docs.</summary>
    internal enum ModuleScope
    {
        /// <summary>Works with any VR controller VHVR supports (tested on Steam Frame only).</summary>
        AllHeadsets,
        /// <summary>Only makes sense with Steam Frame controllers.</summary>
        SteamFrame,
        /// <summary>Not about VR at all; a convenience that happens to live here.</summary>
        Extra,
    }

    /// <summary>
    /// One feature. Each module binds its own config section, installs its own Harmony patches (under its own Harmony id,
    /// so a failure unpatches just that module), and can't take the others down with it.
    /// </summary>
    internal abstract class Module
    {
        public abstract string Name { get; }

        /// <summary>Which group the module belongs to (see <see cref="ModuleScope"/>).</summary>
        public abstract ModuleScope Scope { get; }

        /// <summary>True once Start succeeded.</summary>
        public bool Active { get; private set; }

        private Harmony harmony;
        private bool tickFailed;

        /// <summary>Bind config entries. Runs for every module before any Start, so the settings tab can show them all.</summary>
        public virtual void BindConfig(ConfigFile config) { }

        /// <summary>Find VHVR members and install patches. Throw (e.g. MissingMemberException) to disable the module.</summary>
        protected abstract void Start(Harmony harmony);

        /// <summary>Called every frame while active.</summary>
        protected virtual void Tick() { }

        internal void TryStart()
        {
            harmony = new Harmony(Plugin.Guid + "." + Name);
            try
            {
                Start(harmony);
                Active = true;
                Plugin.Log.LogInfo($"{Name}: ready.");
            }
            catch (Exception e)
            {
                Active = false;
                try { harmony.UnpatchSelf(); } catch { /* nothing patched */ }
                Plugin.Log.LogWarning($"{Name}: disabled ({Describe(e)}).");
            }
        }

        internal void TryTick()
        {
            if (!Active || tickFailed) return;
            try
            {
                Tick();
            }
            catch (Exception e)
            {
                tickFailed = true;
                Plugin.Log.LogError($"{Name}: stopped after an error: {e}");
            }
        }

        private static string Describe(Exception e)
        {
            if (e is System.Reflection.TargetInvocationException && e.InnerException != null) e = e.InnerException;
            return e is MissingMemberException ? e.Message : e.GetType().Name + ": " + e.Message;
        }
    }
}
