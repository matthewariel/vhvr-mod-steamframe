using System;
using System.Collections.Generic;
using System.Text;

namespace VhvrSteamFrame
{
    /// <summary>
    /// Saved server passwords, one per server, as plain C#. Passwords are only ever held encrypted (the caller supplies the
    /// protect/unprotect functions: Windows DPAPI in the game, so the file is useless to anyone but this Windows user).
    /// File format, one server per line: "v1 TAB base64(server) TAB base64(encrypted password)".
    /// </summary>
    public sealed class ServerPasswordStore
    {
        private const string Version = "v1";
        private static readonly Encoding Utf8 = new UTF8Encoding(false);

        private readonly Dictionary<string, string> entries = new Dictionary<string, string>(StringComparer.Ordinal);

        public int Count => entries.Count;

        public static ServerPasswordStore Parse(string text)
        {
            var store = new ServerPasswordStore();
            foreach (var line in (text ?? "").Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = line.Split('\t');
                if (parts.Length != 3 || parts[0] != Version) continue;
                try
                {
                    string server = Utf8.GetString(Convert.FromBase64String(parts[1]));
                    Convert.FromBase64String(parts[2]); // validate
                    if (server.Length > 0) store.entries[server] = parts[2];
                }
                catch (FormatException) { /* skip damaged line */ }
            }
            return store;
        }

        public string Serialize()
        {
            var sb = new StringBuilder();
            foreach (var kv in entries)
                sb.Append(Version).Append('\t').Append(Convert.ToBase64String(Utf8.GetBytes(kv.Key))).Append('\t').Append(kv.Value).Append('\n');
            return sb.ToString();
        }

        /// <summary>False when nothing is saved for the server or it can't be decrypted (e.g. another Windows user's file).</summary>
        public bool TryGet(string server, Func<byte[], byte[]> unprotect, out string password)
        {
            password = null;
            if (string.IsNullOrEmpty(server) || !entries.TryGetValue(server, out var stored)) return false;
            try
            {
                password = Utf8.GetString(unprotect(Convert.FromBase64String(stored)));
                return password.Length > 0;
            }
            catch
            {
                password = null;
                return false;
            }
        }

        public void Set(string server, string password, Func<byte[], byte[]> protect)
        {
            if (string.IsNullOrEmpty(server) || string.IsNullOrEmpty(password)) return;
            entries[server] = Convert.ToBase64String(protect(Utf8.GetBytes(password)));
        }

        public bool Remove(string server) => server != null && entries.Remove(server);

        public void Clear() => entries.Clear();
    }

    public enum PasswordOutcome { Pending, Save, ForgetSaved, Drop }

    public static class ServerPasswordPolicy
    {
        /// <summary>
        /// What to do with a password that was just submitted, given the connection status Valheim reports now
        /// (ZNet.ConnectionStatus as an int: 1 connecting, 2 connected, 6 wrong password, other values above 2 are errors).
        /// </summary>
        public static PasswordOutcome Outcome(int connectionStatus, bool cameFromSaved, float secondsSinceSubmit)
        {
            const int Connected = 2, ErrorPassword = 6;
            if (connectionStatus == Connected) return PasswordOutcome.Save;
            if (connectionStatus == ErrorPassword) return cameFromSaved ? PasswordOutcome.ForgetSaved : PasswordOutcome.Drop;
            if (connectionStatus > Connected) return PasswordOutcome.Drop;
            return secondsSinceSubmit > 120f ? PasswordOutcome.Drop : PasswordOutcome.Pending;
        }
    }
}
