namespace VhvrSteamFrame
{
    /// <summary>
    /// When to (re)apply "public position" after joining a world. Valheim loads saved map settings during the first
    /// seconds, so it's enforced for a short window after spawning and then left alone (turning it off later sticks).
    /// </summary>
    public static class MapSharingPolicy
    {
        public const float StartAfterSeconds = 2f;
        public const float StopAfterSeconds = 30f;

        public static bool InEnforceWindow(float secondsSinceSpawn)
        {
            return secondsSinceSpawn >= StartAfterSeconds && secondsSinceSpawn <= StopAfterSeconds;
        }
    }
}
