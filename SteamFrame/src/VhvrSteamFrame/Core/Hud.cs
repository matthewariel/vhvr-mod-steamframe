using System.Collections.Generic;

namespace VhvrSteamFrame
{
    /// <summary>Short notices in the game's message area (top left), queued until a player exists. Also logged.</summary>
    internal static class Hud
    {
        private static readonly Queue<string> pending = new Queue<string>();

        public static void Notice(string message)
        {
            Plugin.Log.LogInfo("Notice: " + message);
            if (pending.Count < 8) pending.Enqueue(message);
        }

        internal static void Tick()
        {
            if (pending.Count == 0) return;
            var player = Player.m_localPlayer;
            if (player == null || MessageHud.instance == null) return;
            while (pending.Count > 0)
                player.Message(MessageHud.MessageType.TopLeft, pending.Dequeue());
        }
    }
}
