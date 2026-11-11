using System;
using System.Reflection;
using BepInEx.Configuration;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace VhvrSteamFrame
{
    /// <summary>
    /// Adds a "Steam Frame" tab to VHVR's settings dialog, built the same way VHVR builds its full-body-tracking tab.
    ///
    /// VHVR's createModSettings can't be patched (it calls a private Valheim method), so:
    ///  - a prefix on ConfigSettings.CreateTab adds one to the tab count it lays buttons out for (keeps the row centred);
    ///  - a postfix on ConfigSettings.CreateFullBodyTrackingTab (always called) creates our tab with VHVR's own CreateTab.
    /// Rows use VHVR's createElement, so they look like VHVR's and are saved by its OK button (Back discards).
    /// </summary>
    internal class FrameTabHooks : Module
    {
        public override string Name => "FrameTab";
        public override ModuleScope Scope => ModuleScope.AllHeadsets;

        internal static ConfigEntry<bool> ShowTab;

        // Two lines on the tab button, like VHVR's "Motion Control".
        private const string TabName = "Steam\nFrame";

        private static MethodInfo createTab, createElement;
        private static FieldInfo togglePrefabField, transformButtonPrefabField;
        private static bool loggedAdded, warnedFailure;

        public override void BindConfig(ConfigFile config)
        {
            ShowTab = config.Bind("FrameTab", "ShowFrameTab", true,
                "Add a 'Steam Frame' tab to VHVR's settings (Settings > VHVR) with this plugin's settings, a button guide and " +
                "the recenter pose.");
        }

        protected override void Start(Harmony harmony)
        {
            createTab = Vhvr.Method(VhvrApi.SettingsCreateTab);
            createElement = Vhvr.Method(VhvrApi.SettingsCreateElement);
            togglePrefabField = Vhvr.Field(VhvrApi.SettingsTogglePrefab);
            transformButtonPrefabField = Vhvr.TryField(VhvrApi.SettingsTransformButtonPrefab); // optional: page buttons
            var fbtTab = Vhvr.Method(VhvrApi.SettingsCreateFbtTab);

            harmony.Patch(createTab, prefix: new HarmonyMethod(typeof(FrameTabHooks), nameof(BeforeCreateTab)));
            harmony.Patch(fbtTab, postfix: new HarmonyMethod(typeof(FrameTabHooks), nameof(AfterFullBodyTrackingTab)));
        }

        public static void BeforeCreateTab(ref int tabCount)
        {
            if (ShowTab.Value) tabCount++;
        }

        public static void AfterFullBodyTrackingTab(int tabCount)
        {
            if (!ShowTab.Value) return;
            try
            {
                var tab = (Transform)createTab.Invoke(null, new object[] { TabName, tabCount });
                var togglePrefab = togglePrefabField.GetValue(null) as GameObject;
                var labelPrefab = togglePrefab == null ? null : togglePrefab.GetComponentInChildren<TMP_Text>(true)?.gameObject;
                if (labelPrefab == null) throw new MissingMemberException("VHVR's toggle prefab has no label");
                var transformButtons = transformButtonPrefabField?.GetValue(null) as GameObject;
                var buttonPrefab = transformButtons == null ? null : transformButtons.transform.Find("SetButton")?.gameObject;

                tab.gameObject.AddComponent<FrameTabView>().Build(createElement, labelPrefab, buttonPrefab);
                if (!loggedAdded)
                {
                    loggedAdded = true;
                    Plugin.Log.LogInfo("FrameTab: Steam Frame tab added to VHVR's settings.");
                }
            }
            catch (Exception e)
            {
                if (e is TargetInvocationException && e.InnerException != null) e = e.InnerException;
                if (!warnedFailure)
                {
                    warnedFailure = true;
                    Plugin.Log.LogError($"FrameTab: couldn't build the Frame tab: {e}");
                }
            }
        }
    }
}
