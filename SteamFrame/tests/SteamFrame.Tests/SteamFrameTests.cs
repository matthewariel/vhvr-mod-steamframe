using Newtonsoft.Json.Linq;
using Xunit;

namespace VhvrSteamFrame.Tests
{
    /// <summary>The Steam Frame controller profile: labels and detection.</summary>
    public class SteamFrameProfileTests
    {
        private static readonly ControllerProfile Frame = SteamFrameProfile.Instance;

        [Fact]
        public void Map_labels_zoom_with_dpad_and_ping_with_right_stick_click()
        {
            var map = Frame.MapLabels();
            Assert.Contains("dpad_up", map["MapZoomIn"]);
            Assert.Contains("dpad_down", map["mapzoomout"]); // lookups ignore case
            Assert.Contains("button_rs", map["ButtonX"]);
        }

        [Fact]
        public void Map_keys_resolve_whether_valheim_asks_for_KEY_JoyX_or_KEY_X()
        {
            var map = Frame.MapLabels();
            Assert.Equal("MapZoomIn", PromptsPolicy.ActionFromKey("KEY_JoyMapZoomIn"));
            Assert.True(map.ContainsKey(PromptsPolicy.ActionFromKey("KEY_ButtonX")));
        }

        [Fact]
        public void Crouch_prompt_shows_2x_only_with_double_click()
        {
            var twice = Frame.PromptLabels(new PromptLabelOptions { CrouchOnStickClick = true, SprintOnStickClick = true, DoubleClickCrouch = true });
            var once = Frame.PromptLabels(new PromptLabelOptions { CrouchOnStickClick = true, SprintOnStickClick = true, DoubleClickCrouch = false });
            Assert.StartsWith("2x ", twice["Crouch"]);
            Assert.DoesNotContain("2x", once["Crouch"]);
        }

        [Fact]
        public void Prompt_labels_follow_the_binding_tweaks()
        {
            var tweaked = Frame.PromptLabels(new PromptLabelOptions { CrouchOnStickClick = true, SprintOnStickClick = true });
            var vhvr = Frame.PromptLabels(new PromptLabelOptions());
            Assert.Contains("button_ls", tweaked["Run"]);
            Assert.Contains("button_y", vhvr["Run"]);
            Assert.Equal("Right stick down", vhvr["Crouch"]);
            Assert.Contains("button_a", vhvr["Jump"]);
        }

        [Fact]
        public void Frame_controller_type_selects_the_frame_profile()
        {
            Assert.Same(SteamFrameProfile.Instance, ControllerProfiles.ForControllerType("frame_controller"));
            Assert.Same(SteamFrameProfile.Instance, ControllerProfiles.ForControllerType("FRAME_CONTROLLER"));
        }

        [Fact]
        public void Other_controllers_have_no_profile()
        {
            Assert.Null(ControllerProfiles.ForControllerType("knuckles"));
            Assert.Null(ControllerProfiles.ForControllerType("oculus_touch"));
            Assert.Null(ControllerProfiles.ForControllerType(null));
            Assert.Null(ControllerProfiles.ForControllerType(""));
        }

        [Fact]
        public void Profile_ids_and_controller_types_are_unique()
        {
            Assert.Equal(ControllerProfiles.All.Count, ControllerProfiles.All.Select(p => p.Id).Distinct().Count());
            Assert.Equal(ControllerProfiles.All.Count, ControllerProfiles.All.Select(p => p.ControllerType.ToLowerInvariant()).Distinct().Count());
        }
    }

    /// <summary>The Frame binding tweaks, applied to VHVR 0.11.1's original binding (a committed fixture).</summary>
    public class FrameBindingPatcherTests
    {
        private static JObject Original() => Bindings.Load(Repo.OriginalBinding);

        private static HashSet<string> AllTweaksOn(out List<string> problems)
        {
            var patched = Original();
            problems = FrameBindingPatcher.Apply(patched, new FrameBindingOptions());
            return Bindings.Flat(patched);
        }

        [Fact]
        public void No_tweak_is_skipped()
        {
            AllTweaksOn(out var problems);
            Assert.Empty(problems);
        }

        /// <summary>
        /// The whole patched document must stay exactly what it was when the expected file was generated.
        /// If you change the patcher on purpose, regenerate it:  $env:STEAMFRAME_UPDATE_FIXTURES=1; .\scripts\Test.ps1
        /// </summary>
        [Fact]
        public void All_tweaks_on_matches_the_expected_binding()
        {
            var patched = Original();
            FrameBindingPatcher.Apply(patched, new FrameBindingOptions());

            if (Environment.GetEnvironmentVariable("STEAMFRAME_UPDATE_FIXTURES") == "1")
                File.WriteAllText(Repo.ExpectedPatchedBinding, patched.ToString(Newtonsoft.Json.Formatting.Indented) + "\n");

            var expected = Bindings.Load(Repo.ExpectedPatchedBinding);
            var only = Bindings.Flat(patched); only.SymmetricExceptWith(Bindings.Flat(expected));
            Assert.True(only.Count == 0, "Bindings that differ from the expected file:\n" + string.Join("\n", only));
            Assert.True(JToken.DeepEquals(expected, patched), "Same bindings, but the documents differ elsewhere.");
        }

        [Fact]
        public void Tilt_chord_directions_stay_defined()
        {
            var a = AllTweaksOn(out _);
            Assert.Contains(a, l => l.Contains("thumbstick dpad touch | north -> /actions/valheim/in/stopgesturedlocomotion"));
            Assert.Contains(a, l => l.Contains("thumbstick dpad touch | south -> /actions/valheim/in/stopgesturedlocomotion"));
        }

        [Fact]
        public void No_flick_sprint_or_crouch()
        {
            var a = AllTweaksOn(out _);
            Assert.DoesNotContain(a, l => l.Contains("dpad touch | north -> /actions/valheim/in/togglerun"));
            Assert.DoesNotContain(a, l => l.Contains("dpad touch | south -> /actions/valheim/in/togglecrouch"));
        }

        [Fact]
        public void Stick_clicks_are_sprint_and_crouch()
        {
            var a = AllTweaksOn(out _);
            Assert.Contains(a, l => l.Contains("left/input/thumbstick") && l.Contains("click -> /actions/valheim/in/togglerun"));
            Assert.Contains(a, l => l.Contains("right/input/thumbstick button") && l.Contains("click -> /actions/valheim/in/togglecrouch"));
        }

        [Fact]
        public void Thumb_touch_is_sensed_on_both_sticks()
        {
            var a = AllTweaksOn(out _);
            Assert.Contains(a, l => l.Contains("left/input/thumbstick joystick") && l.Contains("touch -> /actions/default/in/teleport"));
            Assert.Contains(a, l => l.Contains("right/input/thumbstick joystick") && l.Contains("touch -> /actions/default/in/teleport"));
        }

        [Fact]
        public void Applying_twice_changes_nothing()
        {
            var once = Original();
            FrameBindingPatcher.Apply(once, new FrameBindingOptions());
            var twice = JObject.Parse(once.ToString());
            FrameBindingPatcher.Apply(twice, new FrameBindingOptions());
            Assert.True(JToken.DeepEquals(once, twice));
        }

        [Fact]
        public void All_tweaks_off_leaves_vhvrs_binding_alone()
        {
            var none = Original();
            var problems = FrameBindingPatcher.Apply(none, Bindings.AllOff());
            Assert.Empty(problems);
            Assert.True(JToken.DeepEquals(Original(), none));
        }

        [Fact]
        public void Crouch_tweaks_off_keep_vhvrs_flick_down_crouch()
        {
            var doc = Original();
            FrameBindingPatcher.Apply(doc, new FrameBindingOptions { CrouchOnRightStickClick = false, NoStickFlickRunCrouch = false });
            Assert.Contains(Bindings.Flat(doc), l => l.Contains("right/input/thumbstick dpad touch | south -> /actions/valheim/in/togglecrouch"));
        }

        [Fact]
        public void Crouch_tweak_off_adds_no_stick_click_crouch()
        {
            var doc = Original();
            FrameBindingPatcher.Apply(doc, new FrameBindingOptions { CrouchOnRightStickClick = false, NoStickFlickRunCrouch = false });
            Assert.DoesNotContain(Bindings.Flat(doc), l => l.Contains("right/input/thumbstick button") && l.Contains("togglecrouch"));
        }

        [Fact]
        public void A_document_without_bindings_reports_problems_instead_of_throwing()
        {
            var problems = FrameBindingPatcher.Apply(new JObject(), new FrameBindingOptions());
            Assert.NotEmpty(problems);
        }
    }

    /// <summary>The button guide built from a binding document.</summary>
    public class BindingTableTests
    {
        private static List<BindingRow> PatchedRows()
        {
            var doc = Bindings.Load(Repo.OriginalBinding);
            FrameBindingPatcher.Apply(doc, new FrameBindingOptions());
            return BindingTable.Build(doc);
        }

        [Fact]
        public void Guide_lists_both_hands()
        {
            var rows = PatchedRows();
            Assert.Contains(rows, r => r.Hand == "Left");
            Assert.Contains(rows, r => r.Hand == "Right");
            Assert.StartsWith("<b>Left controller</b>", BindingTable.Format(rows, "Left"));
            Assert.StartsWith("<b>Right controller</b>", BindingTable.Format(rows, "Right"));
        }

        [Fact]
        public void Guide_shows_the_stick_click_tweaks()
        {
            var rows = PatchedRows();
            Assert.Contains(rows, r => r.Hand == "Right" && r.Control == "Stick click" && r.Action == "Crouch (toggle)");
            Assert.Contains(rows, r => r.Hand == "Left" && r.Control == "Stick click" && r.Action == "Sprint (toggle)");
        }

        [Fact]
        public void Guide_hides_internal_actions_and_has_no_duplicates()
        {
            var rows = PatchedRows();
            Assert.DoesNotContain(rows, r => r.Action == "stopgesturedlocomotion" || r.Action == "squeeze");
            Assert.Equal(rows.Count, rows.Select(r => r.Hand + "|" + r.Control + "|" + r.Action).Distinct().Count());
        }

        [Fact]
        public void Guide_of_an_empty_document_is_empty()
        {
            Assert.Empty(BindingTable.Build(new JObject()));
            Assert.Empty(BindingTable.Build(null));
        }
    }

    /// <summary>
    /// The same checks against the Frame binding in this repository's copy of VHVR. After "Sync fork" pulls a VHVR update,
    /// these tell you whether the binding tweaks still fit the new file.
    /// </summary>
    public class UpstreamBindingTests
    {
        private static JObject Upstream() => Bindings.Load(Repo.UpstreamBinding);

        [Fact]
        public void Upstream_binding_file_is_where_vhvr_keeps_it() =>
            Assert.True(File.Exists(Repo.UpstreamBinding), "Not found: " + Repo.UpstreamBinding + " (did VHVR move or rename it?)");

        [Fact]
        public void Patcher_reports_no_problems_on_upstream()
        {
            var problems = FrameBindingPatcher.Apply(Upstream(), new FrameBindingOptions());
            Assert.True(problems.Count == 0, "Tweaks that no longer fit VHVR's binding: " + string.Join("; ", problems));
        }

        [Fact]
        public void Applying_twice_is_idempotent_on_upstream()
        {
            var once = Upstream();
            FrameBindingPatcher.Apply(once, new FrameBindingOptions());
            var twice = JObject.Parse(once.ToString());
            FrameBindingPatcher.Apply(twice, new FrameBindingOptions());
            Assert.True(JToken.DeepEquals(once, twice));
        }

        [Fact]
        public void All_tweaks_off_leaves_upstream_unchanged()
        {
            var doc = Upstream();
            FrameBindingPatcher.Apply(doc, Bindings.AllOff());
            Assert.True(JToken.DeepEquals(Upstream(), doc));
        }

        [Fact]
        public void Guide_builds_from_upstream()
        {
            var doc = Upstream();
            FrameBindingPatcher.Apply(doc, new FrameBindingOptions());
            var rows = BindingTable.Build(doc);
            Assert.True(rows.Count > 10, "Only " + rows.Count + " guide rows.");
            Assert.Contains(rows, r => r.Action == "Jump");
            Assert.Contains(rows, r => r.Hand == "Right" && r.Control == "Stick click" && r.Action == "Crouch (toggle)");
        }
    }
}
