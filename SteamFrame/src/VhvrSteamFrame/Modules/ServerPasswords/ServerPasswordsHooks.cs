using System;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;

namespace VhvrSteamFrame
{
    /// <summary>
    /// Remembers a server's password after a successful join and fills it in next time.
    ///
    /// Valheim already skips the password box when FejdStartup.ServerPassword is set (the +password launch option):
    /// ZNet.RPC_ClientHandshake submits it straight away. So a prefix on the handshake sets it from the saved copy and a
    /// finalizer clears it again; a prefix on ZNet.OnPasswordEntered notes what was submitted, and Tick saves it once
    /// Valheim reports Connected (or forgets a saved one the server rejected). Not VR-specific, just tedious in VR.
    /// </summary>
    internal class ServerPasswordsHooks : Module
    {
        public override string Name => "ServerPasswords";

        internal static ConfigEntry<bool> Remember;
        internal static ConfigEntry<bool> ForgetAll;

        private static string FilePath => Path.Combine(Path.Combine(Paths.ConfigPath, "VhvrSteamFrame"), "server-passwords.dat");

        private static ServerPasswordStore store = new ServerPasswordStore();
        private static MethodInfo serverPasswordSetter;
        private static PropertyInfo serverPasswordProperty;

        private static bool filledFromSaved;   // this handshake used a saved password (cleared by the finalizer)
        private static string filledServer;
        private static string pendingServer, pendingPassword;
        private static bool pendingFromSaved;
        private static float pendingSince;

        internal static int SavedCount => store.Count;

        public override void BindConfig(ConfigFile config)
        {
            Remember = config.Bind("Servers", "RememberPasswords", true,
                "After you join a password-protected server, save its password (encrypted for your Windows account) and fill it " +
                "in next time. A saved password the server rejects is forgotten, so you'll be asked again.");
            ForgetAll = config.Bind("Servers", "ForgetSavedPasswords", false,
                "Tick and press OK to delete every saved server password (it unticks itself).");
            ForgetAll.SettingChanged += (_, __) =>
            {
                if (!ForgetAll.Value) return;
                store.Clear();
                Save();
                Hud.Notice("Saved server passwords deleted.");
                ForgetAll.Value = false;
            };
        }

        protected override void Start(Harmony harmony)
        {
            serverPasswordProperty = AccessTools.Property(typeof(FejdStartup), "ServerPassword");
            serverPasswordSetter = serverPasswordProperty?.GetSetMethod(true);
            if (serverPasswordSetter == null) throw new MissingMemberException("FejdStartup.ServerPassword setter not found (game update?)");

            var handshake = AccessTools.Method(typeof(ZNet), "RPC_ClientHandshake");
            var entered = AccessTools.Method(typeof(ZNet), "OnPasswordEntered", new[] { typeof(string) });
            if (handshake == null || entered == null) throw new MissingMemberException("ZNet password handshake not found (game update?)");

            // Fails here (module off) rather than mid-join if DPAPI isn't usable.
            var probe = Dpapi.Unprotect(Dpapi.Protect(new byte[] { 1, 2, 3 }));
            if (probe.Length != 3 || probe[2] != 3) throw new InvalidOperationException("Windows DPAPI round trip failed");

            Load();
            harmony.Patch(handshake,
                prefix: new HarmonyMethod(typeof(ServerPasswordsHooks), nameof(HandshakePrefix)),
                finalizer: new HarmonyMethod(typeof(ServerPasswordsHooks), nameof(HandshakeFinalizer)));
            harmony.Patch(entered, prefix: new HarmonyMethod(typeof(ServerPasswordsHooks), nameof(PasswordEnteredPrefix)));
            Plugin.Log.LogInfo($"ServerPasswords: {store.Count} saved.");
        }

        public static void HandshakePrefix(bool needPassword)
        {
            filledFromSaved = false;
            filledServer = null;
            if (!needPassword || !Remember.Value || serverPasswordProperty.GetValue(null) != null) return;

            string server = ZNet.GetServerString(true);
            if (!store.TryGet(server, Dpapi.Unprotect, out var password)) return;
            serverPasswordSetter.Invoke(null, new object[] { password });
            filledFromSaved = true;
            filledServer = server;
            Plugin.Log.LogInfo("ServerPasswords: using the saved password for this server.");
        }

        public static Exception HandshakeFinalizer(Exception __exception)
        {
            if (filledFromSaved)
            {
                try { serverPasswordSetter.Invoke(null, new object[] { null }); }
                catch (Exception e) { Plugin.Log.LogError($"ServerPasswords: couldn't clear the filled-in password: {e.Message}"); }
                filledFromSaved = false;
            }
            return __exception;
        }

        public static void PasswordEnteredPrefix(string pwd)
        {
            if (!Remember.Value || string.IsNullOrEmpty(pwd)) return;
            pendingServer = ZNet.GetServerString(true);
            pendingPassword = pwd;
            pendingFromSaved = filledServer != null && filledServer == pendingServer;
            pendingSince = UnityEngine.Time.unscaledTime;
            filledServer = null;
        }

        protected override void Tick()
        {
            if (pendingServer == null) return;

            var outcome = ServerPasswordPolicy.Outcome((int)ZNet.GetConnectionStatus(), pendingFromSaved,
                UnityEngine.Time.unscaledTime - pendingSince);
            switch (outcome)
            {
                case PasswordOutcome.Pending:
                    return;
                case PasswordOutcome.Save:
                    if (!store.TryGet(pendingServer, Dpapi.Unprotect, out var existing) || existing != pendingPassword)
                    {
                        store.Set(pendingServer, pendingPassword, Dpapi.Protect);
                        Save();
                        Hud.Notice("Server password saved for next time (Settings > VHVR > Steam Frame to forget it).");
                    }
                    break;
                case PasswordOutcome.ForgetSaved:
                    if (store.Remove(pendingServer))
                    {
                        Save();
                        Hud.Notice("The saved password for that server was wrong, so it's been forgotten.");
                    }
                    break;
            }
            pendingServer = null;
            pendingPassword = null;
        }

        private static void Load()
        {
            try
            {
                store = File.Exists(FilePath) ? ServerPasswordStore.Parse(File.ReadAllText(FilePath)) : new ServerPasswordStore();
            }
            catch (Exception e)
            {
                store = new ServerPasswordStore();
                Plugin.Log.LogWarning($"ServerPasswords: couldn't read saved passwords ({e.Message}).");
            }
        }

        private static void Save()
        {
            try
            {
                if (store.Count == 0)
                {
                    if (File.Exists(FilePath)) File.Delete(FilePath);
                    return;
                }
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                string tmp = FilePath + ".tmp";
                File.WriteAllText(tmp, store.Serialize());
                if (File.Exists(FilePath)) File.Delete(FilePath);
                File.Move(tmp, FilePath);
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"ServerPasswords: couldn't save ({e.Message}).");
            }
        }
    }
}
