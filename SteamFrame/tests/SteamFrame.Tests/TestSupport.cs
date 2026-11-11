using System.Runtime.InteropServices;
using Newtonsoft.Json.Linq;
using Xunit;

namespace VhvrSteamFrame.Tests
{
    /// <summary>Where things are in the repository, found by walking up from the test output folder.</summary>
    internal static class Repo
    {
        /// <summary>The SteamFrame folder (the one holding SteamFrame.sln).</summary>
        public static readonly string SteamFrameDir = FindSteamFrameDir();

        /// <summary>VHVR 0.11.1's untouched Frame binding.</summary>
        public static string OriginalBinding => Path.Combine(SteamFrameDir, "tests", "SteamFrame.Tests", "fixtures", "bindings_frame_controller.vhvr-0.11.1.json");

        /// <summary>The 0.11.1 binding with every tweak applied, as generated once by the patcher and committed.</summary>
        public static string ExpectedPatchedBinding => Path.Combine(SteamFrameDir, "tests", "SteamFrame.Tests", "fixtures", "bindings_frame_controller.expected-all-tweaks.json");

        /// <summary>The Frame binding in this repository's copy of VHVR. It changes when "Sync fork" pulls a VHVR update.</summary>
        public static string UpstreamBinding => Path.GetFullPath(Path.Combine(SteamFrameDir, "..", "Unity", "ValheimVR", "Assets",
            "StreamingAssets", "SteamVR", SteamFrameProfile.BindingFile));

        private static string FindSteamFrameDir()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "SteamFrame.sln"))) dir = dir.Parent;
            if (dir == null) throw new DirectoryNotFoundException("SteamFrame.sln not found above " + AppContext.BaseDirectory);
            return dir.FullName;
        }
    }

    internal static class Bindings
    {
        public static JObject Load(string path) => JObject.Parse(File.ReadAllText(path));

        /// <summary>
        /// Flattens a binding document into "set | path mode sub | input -> output" lines, so two documents can be compared
        /// ignoring formatting and order.
        /// </summary>
        public static HashSet<string> Flat(JObject root)
        {
            var set = new HashSet<string>();
            foreach (var actionSet in ((JObject)root["bindings"]!).Properties())
            {
                foreach (var s in ((JArray)actionSet.Value["sources"]!).OfType<JObject>())
                    foreach (var inp in ((JObject)s["inputs"]!).Properties())
                        set.Add($"{actionSet.Name} | {s["path"]} {s["mode"]} {s["parameters"]?["sub_mode"]} | {inp.Name} -> {inp.Value["output"]}");
                foreach (var c in (actionSet.Value["chords"] as JArray ?? new JArray()).OfType<JObject>())
                    set.Add($"{actionSet.Name} | CHORD {string.Join(" + ", ((JArray)c["inputs"]!).Select(i => string.Join(":", i)))} -> {c["output"]}");
            }
            return set;
        }

        public static FrameBindingOptions AllOff() => new FrameBindingOptions
        {
            ThumbTouchSensing = false, SprintToggleOnLeftStickClick = false, CrouchOnRightStickClick = false, NoStickFlickRunCrouch = false,
        };
    }

    /// <summary>A test that only runs on Windows (reported as skipped elsewhere).</summary>
    public sealed class WindowsOnlyFactAttribute : FactAttribute
    {
        public WindowsOnlyFactAttribute()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) Skip = "Needs Windows (DPAPI).";
        }
    }
}
