using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace VhvrSteamFrame
{
    /// <summary>
    /// Names and pruning for the per-session copies of BepInEx's log in BepInEx/SessionLogs. BepInEx overwrites
    /// LogOutput.log on every launch; each session's copy is named after its start time
    /// (<c>2026-11-10_23-41-05.log</c>), so the names sort oldest first. Only files with exactly that name shape are ever
    /// deleted, so anything else in the folder is left alone.
    /// </summary>
    public static class SessionLogPolicy
    {
        public const string FolderName = "SessionLogs";
        public const int DefaultKeep = 50;
        public const int MinKeep = 1;
        public const int MaxKeep = 1000;

        private const string StampFormat = "yyyy-MM-dd_HH-mm-ss";
        private static readonly Regex SessionName = new Regex(@"^(\d{4}-\d{2}-\d{2}_\d{2}-\d{2}-\d{2})(?:_(\d+))?\.log$");

        /// <summary>The file name for a session that started at <paramref name="start"/>. Two launches in the same second
        /// get <c>_2</c>, <c>_3</c>... after the time.</summary>
        public static string FileNameFor(DateTime start, Func<string, bool> taken)
        {
            string stem = start.ToString(StampFormat, CultureInfo.InvariantCulture);
            string name = stem + ".log";
            for (int n = 2; taken(name); n++) name = stem + "_" + n + ".log";
            return name;
        }

        public static bool IsSessionLog(string fileName)
        {
            return fileName != null && SessionName.IsMatch(fileName);
        }

        /// <summary>The session logs to delete, oldest first, so that at most <paramref name="keep"/> remain
        /// (clamped to <see cref="MinKeep"/>..<see cref="MaxKeep"/>). Other file names are never returned.</summary>
        public static List<string> ToDelete(IEnumerable<string> fileNames, int keep)
        {
            keep = Math.Max(MinKeep, Math.Min(MaxKeep, keep));
            var logs = fileNames
                .Select(f => new { Name = f, Match = f == null ? null : SessionName.Match(f) })
                .Where(f => f.Match != null && f.Match.Success)
                .OrderBy(f => f.Match.Groups[1].Value, StringComparer.Ordinal)
                .ThenBy(f => f.Match.Groups[2].Success ? int.Parse(f.Match.Groups[2].Value, CultureInfo.InvariantCulture) : 1)
                .Select(f => f.Name)
                .ToList();
            return logs.Take(Math.Max(0, logs.Count - keep)).ToList();
        }
    }
}
