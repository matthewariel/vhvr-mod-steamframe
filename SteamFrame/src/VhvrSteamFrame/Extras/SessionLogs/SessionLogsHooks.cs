using System;
using System.IO;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace VhvrSteamFrame
{
    /// <summary>
    /// Keeps every game session's log. BepInEx overwrites LogOutput.log on each launch, so the previous session's log is
    /// gone as soon as the game starts again. At startup this copies what LogOutput.log holds so far (the preloader and
    /// the plugins loaded before this one) into BepInEx/SessionLogs/&lt;start time&gt;.log, then writes the rest of the
    /// session there too, with the same levels and format as LogOutput.log. LogOutput.log itself is unchanged.
    /// </summary>
    internal class SessionLogsHooks : Module
    {
        public override string Name => "SessionLogs";
        public override ModuleScope Scope => ModuleScope.Extra;

        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<int> Keep;

        private static SessionLogListener listener;

        public override void BindConfig(ConfigFile config)
        {
            Enabled = config.Bind("SessionLogs", "Enabled", true,
                "Also save each game session's log as its own file in BepInEx/SessionLogs, named by when the game started. " +
                "LogOutput.log only ever holds the latest session. Takes effect the next time the game starts.");
            Keep = config.Bind("SessionLogs", "Keep", SessionLogPolicy.DefaultKeep, new ConfigDescription(
                "How many session logs to keep. The oldest are deleted when the game starts.",
                new AcceptableValueRange<int>(SessionLogPolicy.MinKeep, SessionLogPolicy.MaxKeep)));
        }

        protected override void Start(Harmony harmony)
        {
            if (!Enabled.Value)
            {
                Plugin.Log.LogInfo("SessionLogs: off ([SessionLogs] Enabled = false).");
                return;
            }
            if (listener != null) return;

            var main = Logger.Listeners.OfType<DiskLogListener>().FirstOrDefault();
            if (main == null || main.LogWriter == null)
                throw new InvalidOperationException("BepInEx isn't writing LogOutput.log ([Logging.Disk] Enabled = false in BepInEx.cfg)");

            string dir = Path.Combine(Paths.BepInExRootPath, SessionLogPolicy.FolderName);
            Directory.CreateDirectory(dir);
            string name = SessionLogPolicy.FileNameFor(DateTime.Now, n => File.Exists(Path.Combine(dir, n)));
            string path = Path.Combine(dir, name);

            // BepInEx flushes LogOutput.log every 2 s; flush now so the copy has everything logged so far.
            main.LogWriter.Flush();
            var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
            try
            {
                string source = CurrentBepInExLog();
                if (source != null)
                {
                    using (var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                        input.CopyTo(output);
                }
                listener = new SessionLogListener(output, main.DisplayedLogLevel, main.WriteFromUnityLog);
            }
            catch
            {
                output.Dispose();
                throw;
            }

            Logger.Listeners.Add(listener);
            UnityEngine.Application.quitting += StopListening;

            int deleted = Prune(dir);
            Plugin.Log.LogInfo($"SessionLogs: this session is also saved to BepInEx/{SessionLogPolicy.FolderName}/{name} " +
                               $"(keeping the last {Keep.Value}{(deleted > 0 ? $", deleted {deleted} older" : "")}).");
        }

        private static void StopListening()
        {
            if (listener == null) return;
            Logger.Listeners.Remove(listener);
            listener.Dispose();
            listener = null;
        }

        /// <summary>LogOutput.log, or LogOutput.log.N when another copy of the game had it open: the newest one.</summary>
        private static string CurrentBepInExLog()
        {
            return new DirectoryInfo(Paths.BepInExRootPath).GetFiles("LogOutput.log*")
                .Where(f => f.Name == "LogOutput.log" || f.Name.StartsWith("LogOutput.log.", StringComparison.Ordinal))
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .Select(f => f.FullName)
                .FirstOrDefault();
        }

        private static int Prune(string dir)
        {
            int deleted = 0;
            var names = Directory.GetFiles(dir).Select(Path.GetFileName);
            foreach (string old in SessionLogPolicy.ToDelete(names, Keep.Value))
            {
                try
                {
                    File.Delete(Path.Combine(dir, old));
                    deleted++;
                }
                catch (Exception e)
                {
                    Plugin.Log.LogWarning($"SessionLogs: couldn't delete old log {old} ({e.GetType().Name}).");
                }
            }
            return deleted;
        }

        /// <summary>Writes log lines the way BepInEx's own LogOutput.log writer does, flushed every 2 s and at quit.</summary>
        private sealed class SessionLogListener : ILogListener
        {
            private readonly LogLevel levels;
            private readonly bool includeUnity;
            private readonly TextWriter writer;
            private readonly System.Threading.Timer flushTimer;

            public SessionLogListener(Stream stream, LogLevel levels, bool includeUnity)
            {
                this.levels = levels;
                this.includeUnity = includeUnity;
                writer = TextWriter.Synchronized(new StreamWriter(stream, new System.Text.UTF8Encoding(false)));
                flushTimer = new System.Threading.Timer(_ => Flush(), null, 2000, 2000);
            }

            public void LogEvent(object sender, LogEventArgs eventArgs)
            {
                if (!includeUnity && eventArgs.Source is UnityLogSource) return;
                if ((eventArgs.Level & levels) == 0) return;
                try { writer.WriteLine(eventArgs.ToString()); }
                catch (ObjectDisposedException) { /* game is quitting */ }
            }

            private void Flush()
            {
                try { writer.Flush(); }
                catch (ObjectDisposedException) { /* game is quitting */ }
            }

            public void Dispose()
            {
                flushTimer.Dispose();
                Flush();
                writer.Dispose();
            }
        }
    }
}
