using System.Text.RegularExpressions;
using Xunit;

namespace VhvrSteamFrame.Tests
{
    public class ServerPasswordTests
    {
        private const string Server = "steam/123/1.2.3.4:2456";
        private const string Password = "hunter2 ünïcode";

        // A stand-in for encryption so the file-format tests run on any OS: reversible, and clearly not plain text.
        private static byte[] Flip(byte[] data) => data.Select(b => (byte)(b ^ 0x5A)).Reverse().ToArray();

        private static ServerPasswordStore Reloaded()
        {
            var store = new ServerPasswordStore();
            store.Set(Server, Password, Flip);
            return ServerPasswordStore.Parse(store.Serialize() + "garbage line\nv1\tnot base64\t!!\n");
        }

        [WindowsOnlyFact]
        public void Saved_file_does_not_contain_the_password_in_plain_text()
        {
            var store = new ServerPasswordStore();
            store.Set(Server, Password, Dpapi.Protect);
            Assert.DoesNotContain("hunter2", store.Serialize());
        }

        [WindowsOnlyFact]
        public void Dpapi_round_trip_through_the_file_format()
        {
            var store = new ServerPasswordStore();
            store.Set(Server, Password, Dpapi.Protect);
            var reloaded = ServerPasswordStore.Parse(store.Serialize());
            Assert.True(reloaded.TryGet(Server, Dpapi.Unprotect, out var back));
            Assert.Equal(Password, back);
        }

        [Fact]
        public void Damaged_lines_are_skipped()
        {
            var reloaded = Reloaded();
            Assert.Equal(1, reloaded.Count);
            Assert.True(reloaded.TryGet(Server, Flip, out var back));
            Assert.Equal(Password, back);
        }

        [Fact]
        public void Unknown_server_has_no_password() =>
            Assert.False(Reloaded().TryGet("socket/other:2456", Flip, out _));

        [Fact]
        public void Undecryptable_entry_reads_as_missing() =>
            Assert.False(Reloaded().TryGet(Server, b => throw new Exception("wrong user"), out _));

        [Fact]
        public void Remove_and_clear()
        {
            var reloaded = Reloaded();
            Assert.True(reloaded.Remove(Server));
            Assert.Equal(0, reloaded.Count);
            Assert.Equal("", reloaded.Serialize());
        }

        [Fact]
        public void Connected_saves() =>
            Assert.Equal(PasswordOutcome.Save, ServerPasswordPolicy.Outcome(2, false, 1f));

        [Fact]
        public void Wrong_saved_password_is_forgotten() =>
            Assert.Equal(PasswordOutcome.ForgetSaved, ServerPasswordPolicy.Outcome(6, true, 1f));

        [Fact]
        public void Wrong_typed_password_is_not_saved() =>
            Assert.Equal(PasswordOutcome.Drop, ServerPasswordPolicy.Outcome(6, false, 1f));

        [Fact]
        public void Still_connecting_waits_then_gives_up()
        {
            Assert.Equal(PasswordOutcome.Pending, ServerPasswordPolicy.Outcome(1, false, 5f));
            Assert.Equal(PasswordOutcome.Drop, ServerPasswordPolicy.Outcome(1, false, 200f));
        }

        [Fact]
        public void Server_full_keeps_the_saved_password() =>
            Assert.Equal(PasswordOutcome.Drop, ServerPasswordPolicy.Outcome(9, true, 1f));
    }

    public class MapSharingTests
    {
        [Fact]
        public void Public_position_is_enforced_only_shortly_after_spawning()
        {
            Assert.False(MapSharingPolicy.InEnforceWindow(0.5f));
            Assert.True(MapSharingPolicy.InEnforceWindow(5f));
            Assert.False(MapSharingPolicy.InEnforceWindow(60f));
        }
    }

    public class VersionTests
    {
        /// <summary>BepInEx needs the version as a const in Plugin.cs; the build takes it from Directory.Build.props.</summary>
        [Fact]
        public void Plugin_version_const_matches_the_build_version()
        {
            string props = File.ReadAllText(Path.Combine(Repo.SteamFrameDir, "Directory.Build.props"));
            string plugin = File.ReadAllText(Path.Combine(Repo.SteamFrameDir, "src", "VhvrSteamFrame", "Core", "Plugin.cs"));

            var build = Regex.Match(props, @"<SteamFrameVersion>([^<]+)</SteamFrameVersion>");
            var code = Regex.Match(plugin, @"const\s+string\s+Version\s*=\s*""([^""]+)""");
            Assert.True(build.Success, "SteamFrameVersion not found in Directory.Build.props");
            Assert.True(code.Success, "Plugin.Version const not found in Core/Plugin.cs");
            Assert.Equal(build.Groups[1].Value, code.Groups[1].Value);
        }
    }
}
