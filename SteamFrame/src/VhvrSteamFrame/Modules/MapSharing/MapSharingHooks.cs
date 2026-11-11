using BepInEx.Configuration;
using HarmonyLib;

namespace VhvrSteamFrame
{
    /// <summary>
    /// Turns on Valheim's "public position" (other players see you on the map) when you join a world. The checkbox lives
    /// on the full map, which is awkward to reach with VHVR's wrist map.
    /// </summary>
    internal class MapSharingHooks : Module
    {
        public override string Name => "MapSharing";

        internal static ConfigEntry<bool> SharePositionOnJoin;

        private float spawnedAt = -1f, nextCheck;
        private bool logged;

        public override void BindConfig(ConfigFile config)
        {
            SharePositionOnJoin = config.Bind("Map", "SharePositionOnJoin", true,
                "Turn on Valheim's 'public position' (other players see you on the map) each time you join a world. " +
                "You can still turn it off on the full map; it comes back on next time you join.");
        }

        protected override void Start(Harmony harmony) { }

        protected override void Tick()
        {
            if (!SharePositionOnJoin.Value) return;
            if (Player.m_localPlayer == null || ZNet.instance == null)
            {
                spawnedAt = -1f;
                logged = false;
                return;
            }

            float now = UnityEngine.Time.unscaledTime;
            if (spawnedAt < 0f) spawnedAt = now;
            if (!MapSharingPolicy.InEnforceWindow(now - spawnedAt) || now < nextCheck) return;
            nextCheck = now + 1f;

            if (!ZNet.instance.IsReferencePositionPublic())
            {
                ZNet.instance.SetPublicReferencePosition(true);
                if (!logged)
                {
                    logged = true;
                    Plugin.Log.LogInfo("Map: turned on public position, so other players can see you on the map.");
                }
            }
        }
    }
}
