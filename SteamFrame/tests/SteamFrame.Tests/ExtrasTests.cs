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

    public class SessionLogTests
    {
        private static readonly DateTime Start = new DateTime(2026, 11, 10, 23, 41, 5);

        [Fact]
        public void Session_log_is_named_after_its_start_time()
        {
            Assert.Equal("2026-11-10_23-41-05.log", SessionLogPolicy.FileNameFor(Start, _ => false));
        }

        [Fact]
        public void Second_launch_in_the_same_second_gets_a_suffix()
        {
            var taken = new HashSet<string> { "2026-11-10_23-41-05.log", "2026-11-10_23-41-05_2.log" };
            Assert.Equal("2026-11-10_23-41-05_3.log", SessionLogPolicy.FileNameFor(Start, taken.Contains));
        }

        [Fact]
        public void Oldest_logs_beyond_the_limit_are_deleted()
        {
            var files = new[]
            {
                "2026-11-10_23-41-05.log", "2026-11-09_08-00-00.log", "2026-11-10_23-41-05_10.log",
                "2026-11-10_23-41-05_2.log", "2025-01-01_00-00-00.log",
            };
            Assert.Equal(new[] { "2025-01-01_00-00-00.log", "2026-11-09_08-00-00.log", "2026-11-10_23-41-05.log" },
                SessionLogPolicy.ToDelete(files, 2));
        }

        [Fact]
        public void Only_session_log_names_are_ever_deleted()
        {
            var files = new[] { "notes.txt", "LogOutput.log", "2026-11-10_23-41-05.log.bak", "2026-11-10.log", "2026-11-10_23-41-05.log" };
            Assert.Empty(SessionLogPolicy.ToDelete(files, 1));
        }

        [Fact]
        public void Keep_is_clamped_to_at_least_one()
        {
            var files = new[] { "2026-11-09_08-00-00.log", "2026-11-10_23-41-05.log" };
            Assert.Equal(new[] { "2026-11-09_08-00-00.log" }, SessionLogPolicy.ToDelete(files, 0));
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
