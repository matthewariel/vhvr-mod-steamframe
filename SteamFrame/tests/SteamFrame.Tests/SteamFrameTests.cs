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

        /// <summary>The keys Valheim's map hints really ask for, from the 0.3.0 game log.</summary>
        [Fact]
        public void Map_hints_match_the_keys_valheim_asks_for()
        {
            var map = Frame.MapLabels();
            Assert.Contains("dpad_down", map["LTrigger"]); // "LT / RT  Zoom out/in"
            Assert.Contains("dpad_up", map["RTrigger"]);
            Assert.Contains("button_rb", map["TabRight"]); // remove pin = right bumper (confirmed in the headset)
            Assert.Contains("button_rt", map["TabLeft"]);  // cross off pin = trigger
            Assert.Contains("Grip", map["ButtonA"]);       // add pin
        }

        /// <summary>The 0.3.3 log: the map also asked for D-pad left (pin icon picker) and Y, which fell back to Xbox glyphs.</summary>
        [Fact]
        public void Every_map_key_from_the_0_3_3_log_has_a_frame_label()
        {
            var map = Frame.MapLabels();
            foreach (var key in new[] { "DPadLeft", "ButtonY", "LTrigger", "RTrigger", "DPadRight", "ButtonX", "TabRight", "TabLeft", "ButtonA" })
                Assert.True(map.ContainsKey(key), key + " has no Frame label");
            Assert.Contains("button_rt", map["DPadLeft"]); // pin icons: point and pull the trigger, not the D-pad
        }

        [Fact]
        public void Map_closes_with_the_menu_button()
        {
            Assert.Contains("button_start", Frame.MapCloseLabel());
            Assert.Contains("button_start", Frame.MapLabels()["ButtonB"]);
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
            Assert.Contains(rows, r => r.Hand == "Right" && r.Control == "Stick click" && r.Action == "Crouch");
            Assert.Contains(rows, r => r.Hand == "Left" && r.Control == "Stick click" && r.Action == "Sprint");
        }

        [Fact]
        public void Guide_hides_internal_actions_and_has_no_duplicates()
        {
            var rows = PatchedRows();
            Assert.DoesNotContain(rows, r => r.Action == "stopgesturedlocomotion" || r.Action == "squeeze");
            Assert.Equal(rows.Count, rows.Select(r => r.Hand + "|" + r.Control + "|" + r.Action).Distinct().Count());
        }

        [Fact]
        public void Guide_fits_the_tab_without_scrolling()
        {
            // The list the guide falls back to for a controller with no picture: two columns 280 wide and 405 tall
            // (down to -240, inside the panel's bottom edge) at 12 pt.
            // Measured in the 0.3.2 headset screenshots at 13 pt: ~6.4 units per character and ~13.6 per line, so at
            // 12 pt about 47 characters a line and 32 lines. With the page title above and a margin: 44 and 29.
            const int charsPerLine = 44, maxLines = 29;
            var rows = PatchedRows();
            foreach (var hand in new[] { "Left", "Right" })
            {
                var plain = System.Text.RegularExpressions.Regex.Replace(BindingTable.Format(rows, hand), "<[^>]+>", "");
                int lines = plain.TrimEnd('\n').Split('\n').Sum(l => Math.Max(1, (l.Length + charsPerLine - 1) / charsPerLine));
                Assert.True(lines <= maxLines, $"{hand} guide needs {lines} lines, room for {maxLines}.");
            }
        }

        [Fact]
        public void Guide_of_an_empty_document_is_empty()
        {
            Assert.Empty(BindingTable.Build(new JObject()));
            Assert.Empty(BindingTable.Build(null));
        }
    }

    /// <summary>The Button guide's boxes, remaps and two-button combinations (FrameButtons).</summary>
    public class FrameButtonsTests
    {
        private static JObject Patched()
        {
            var doc = Bindings.Load(Repo.OriginalBinding);
            FrameBindingPatcher.Apply(doc, new FrameBindingOptions());
            return doc;
        }

        private static JObject Remapped(params string[] pairs)
        {
            var doc = Patched();
            var remaps = new Dictionary<string, string>();
            for (int i = 0; i < pairs.Length; i += 2) remaps[pairs[i]] = pairs[i + 1];
            Assert.Empty(FrameButtons.ApplyRemaps(doc, remaps));
            return doc;
        }

        private static FrameButton Button(string id) => FrameButtons.ById(id) ?? throw new Xunit.Sdk.XunitException("no button " + id);

        [Fact]
        public void Every_box_has_a_unique_id_and_ten_per_controller()
        {
            Assert.Equal(10, FrameButtons.Left.Count);
            Assert.Equal(10, FrameButtons.Right.Count);
            var ids = FrameButtons.All.Select(b => b.Id).ToList();
            Assert.Equal(ids.Count, ids.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        }

        [Fact]
        public void Trigger_grip_and_stick_movement_cannot_be_changed()
        {
            foreach (var id in new[] { "left/trigger", "right/trigger", "left/grip", "right/grip", "left/thumbstick/position", "right/thumbstick/position" })
                Assert.False(Button(id).Remappable, id);
            Assert.NotNull(FrameButtons.Refusal(Patched(), "right/trigger", "jump"));
        }

        [Fact]
        public void Boxes_show_what_the_tweaked_binding_does()
        {
            var doc = Patched();
            Assert.Equal("Jump", FrameButtons.Describe(doc, Button("right/a")));
            Assert.Equal("Dodge", FrameButtons.Describe(doc, Button("right/x")));
            Assert.Equal("Wrist map", FrameButtons.Describe(doc, Button("left/view")));
            Assert.Equal("Sprint", FrameButtons.Describe(doc, Button("left/thumbstick")));
            Assert.Equal("Middle click (ping) + Crouch", FrameButtons.Describe(doc, Button("right/thumbstick")));
            Assert.Equal("Use / attack + Laser click", FrameButtons.Describe(doc, Button("right/trigger")));
            Assert.Equal("Grab", FrameButtons.Describe(doc, Button("left/grip")));
            Assert.Equal("Move", FrameButtons.Describe(doc, Button("left/thumbstick/position")));
            Assert.Equal("Turn", FrameButtons.Describe(doc, Button("right/thumbstick/position")));
        }

        [Fact]
        public void Every_box_line_fits_its_box()
        {
            foreach (var doc in new[] { Patched(), Bindings.Load(Repo.OriginalBinding) })
                foreach (var b in FrameButtons.All)
                {
                    string text = FrameButtons.Describe(doc, b);
                    Assert.True(text.Length <= FrameButtons.MaxBoxChars, $"{b.Id}: '{text}' is {text.Length} characters, room for {FrameButtons.MaxBoxChars}.");
                    Assert.True((b.Label + " (fixed)  - changed").Length <= 34, b.Label);
                }
        }

        [Fact]
        public void Too_many_actions_on_one_button_are_counted_not_listed()
        {
            var doc = Patched();
            var sources = (JArray)doc["bindings"]!["/actions/valheim"]!["sources"]!;
            sources.Add(JObject.Parse("{\"path\":\"/user/hand/right/input/a\",\"mode\":\"button\",\"inputs\":{\"click\":{\"output\":\"/actions/valheim/in/scrolldown\"}}}"));
            Assert.Equal("Jump +1", FrameButtons.Describe(doc, Button("right/a")));
        }

        [Fact]
        public void Remap_gives_a_button_its_new_action_only()
        {
            var doc = Remapped("right/a", "dodge");
            Assert.Equal(new[] { "dodge" }, FrameButtons.ActionsOn(doc, Button("right/a")));
            var flat = Bindings.Flat(doc);
            Assert.DoesNotContain(flat, l => l.Contains("right/input/a ") && l.Contains("-> /actions/valheim/in/jump"));
            // Touch sensing and the grip + A combination stay.
            Assert.Contains(flat, l => l.Contains("right/input/a button") && l.Contains("touch -> /actions/valheim/in/stopgesturedlocomotion"));
            Assert.Contains(flat, l => l.Contains("CHORD") && l.Contains("/user/hand/right/input/a:click") && l.Contains("removepiece"));
        }

        [Fact]
        public void Remap_to_nothing_leaves_the_button_doing_nothing()
        {
            var doc = Remapped("left/bumper", FrameButtons.Nothing);
            Assert.Empty(FrameButtons.ActionsOn(doc, Button("left/bumper")));
            Assert.Equal("Nothing", FrameButtons.Describe(doc, Button("left/bumper")));
            Assert.Contains(Bindings.Flat(doc), l => l.Contains("left/input/bumper button") && l.Contains("click -> /actions/valheim/in/stopgesturedlocomotion"));
        }

        [Fact]
        public void Remapping_a_stick_click_keeps_the_stick_moving()
        {
            var doc = Remapped("left/thumbstick", "jump", "right/thumbstick", "dodge");
            Assert.Equal(new[] { "jump" }, FrameButtons.ActionsOn(doc, Button("left/thumbstick")));
            Assert.Equal(new[] { "dodge" }, FrameButtons.ActionsOn(doc, Button("right/thumbstick")));
            Assert.Equal("Move", FrameButtons.Describe(doc, Button("left/thumbstick/position")));
            Assert.Equal("Turn", FrameButtons.Describe(doc, Button("right/thumbstick/position")));
        }

        [Fact]
        public void A_button_with_no_action_can_be_given_one()
        {
            var doc = Remapped("left/bumper", FrameButtons.Nothing);
            Assert.Empty(FrameButtons.ApplyRemaps(doc, new Dictionary<string, string> { { "left/bumper", "jump" } }));
            Assert.Equal(new[] { "jump" }, FrameButtons.ActionsOn(doc, Button("left/bumper")));
        }

        [Fact]
        public void Applying_remaps_twice_changes_nothing()
        {
            var remaps = new Dictionary<string, string> { { "right/a", "dodge" }, { "right/x", "jump" }, { "left/bumper", "none" }, { "right/thumbstick", "togglemap" } };
            var once = Patched();
            FrameButtons.ApplyRemaps(once, remaps);
            var twice = JObject.Parse(once.ToString());
            FrameButtons.ApplyRemaps(twice, remaps);
            Assert.True(JToken.DeepEquals(once, twice));
        }

        [Fact]
        public void Bad_remaps_are_reported_and_change_nothing()
        {
            var doc = Patched();
            var problems = FrameButtons.ApplyRemaps(doc, new Dictionary<string, string>
                { { "right/trigger", "jump" }, { "right/nope", "jump" }, { "right/a", "fly" } });
            Assert.Equal(3, problems.Count);
            Assert.True(JToken.DeepEquals(Patched(), doc));
        }

        [Fact]
        public void Menu_has_to_stay_on_a_button()
        {
            var doc = Patched();
            Assert.NotNull(FrameButtons.Refusal(doc, "right/menu", "jump"));
            Assert.NotNull(FrameButtons.Refusal(doc, "right/menu", FrameButtons.Nothing));
            Assert.Null(FrameButtons.Refusal(doc, "right/a", "togglemenu"));
            var moved = Remapped("left/view", "togglemenu");
            Assert.Null(FrameButtons.Refusal(moved, "right/menu", "togglemap"));
        }

        [Fact]
        public void Remap_setting_reads_back_what_it_wrote()
        {
            var remaps = FrameButtons.ParseRemaps(" Right/A = Dodge ; left/bumper=none;;junk; =x;right/x=");
            Assert.Equal(2, remaps.Count);
            Assert.Equal("dodge", remaps["right/a"]);
            Assert.Equal("none", remaps["left/bumper"]);
            Assert.Equal("left/bumper=none;right/a=dodge", FrameButtons.FormatRemaps(remaps));
            Assert.Equal(remaps, FrameButtons.ParseRemaps(FrameButtons.FormatRemaps(remaps)));
            Assert.Empty(FrameButtons.ParseRemaps(""));
            Assert.Equal("", FrameButtons.FormatRemaps(new Dictionary<string, string>()));
        }

        [Fact]
        public void Every_choice_has_a_name_and_fits_a_picker_button()
        {
            Assert.Equal(FrameButtons.Choices.Count, FrameButtons.Choices.Select(c => c.Action).Distinct().Count());
            foreach (var c in FrameButtons.Choices)
            {
                Assert.False(string.IsNullOrEmpty(c.Label));
                Assert.NotEqual(c.Action, c.Label); // has a readable name, not the raw action
                // Picker buttons are 150 wide at 13 pt (about 6.4 units a character).
                Assert.True(c.Label.Length * 6.4 <= FrameGuideLayout.PickerButtonWidth - 8, c.Label);
            }
            Assert.Contains(FrameButtons.Choices, c => c.Action == FrameButtons.Nothing);
        }

        [Fact]
        public void Two_button_combinations_are_one_line_each()
        {
            var lines = FrameButtons.ChordLines(Patched());
            Assert.Contains("Grip + Trigger: Add map pin / Quick-move item", lines);
            Assert.Contains("Grip + other Trigger: Split stack", lines);
            Assert.Contains("Grip + A: Remove build piece", lines);
            Assert.Contains("Grip + Stick tilt (right): Flip piece / Rotate piece", lines);
            Assert.Equal(4, lines.Count);
            Assert.Empty(FrameButtons.ChordLines(new JObject()));
        }
    }

    /// <summary>The Button guide page's layout stays inside the panel, and the picture's leader lines meet the boxes.</summary>
    public class FrameGuideLayoutTests
    {
        private static string DrawScript => Path.Combine(Repo.SteamFrameDir, "src", "VhvrSteamFrame", "Assets", "draw_frame_controllers.ps1");

        [Fact]
        public void Boxes_reset_and_note_stay_inside_the_panel()
        {
            int boxes = Math.Max(FrameButtons.Left.Count, FrameButtons.Right.Count);
            Assert.True(FrameGuideLayout.BoxY(0) + FrameGuideLayout.BoxHeight / 2 <= FrameGuideLayout.TopY);
            float lastBottom = FrameGuideLayout.BoxY(boxes - 1) - FrameGuideLayout.BoxHeight / 2;
            float resetTop = FrameGuideLayout.ResetY + Math.Max(FrameGuideLayout.ResetHeight, FrameGuideLayout.NoteHeight) / 2;
            Assert.True(resetTop <= lastBottom, $"Reset row top {resetTop} overlaps the last box (bottom {lastBottom}).");
            Assert.True(FrameGuideLayout.ResetY - Math.Max(FrameGuideLayout.ResetHeight, FrameGuideLayout.NoteHeight) / 2 >= FrameGuideLayout.BottomY);
            Assert.True(FrameGuideLayout.BoxStep >= FrameGuideLayout.BoxHeight + 2, "boxes touch");
            Assert.Equal(FrameGuideLayout.EdgeX, FrameGuideLayout.ColumnInnerX + FrameGuideLayout.BoxWidth);
            Assert.True(FrameGuideLayout.EdgeX <= 440f);
            Assert.True(FrameGuideLayout.BottomY >= -240f && FrameGuideLayout.TopY <= 165f);
        }

        [Fact]
        public void Text_is_never_below_12_pt()
        {
            foreach (var size in new[] { FrameGuideLayout.BoxNameSize, FrameGuideLayout.BoxActionSize, FrameGuideLayout.NoteSize, FrameGuideLayout.NotesSize })
                Assert.True(size >= 12, size + " pt");
        }

        [Fact]
        public void Picture_and_combinations_stay_between_the_columns()
        {
            Assert.True(FrameGuideLayout.PictureWidth / 2 <= FrameGuideLayout.ColumnInnerX);
            Assert.True(FrameGuideLayout.PictureCentreY + FrameGuideLayout.PictureHeight / 2 <= FrameGuideLayout.TopY);
            Assert.True(FrameGuideLayout.PictureCentreY - FrameGuideLayout.PictureHeight / 2 >= FrameGuideLayout.BottomY);
            Assert.True(FrameGuideLayout.NotesWidth / 2 <= FrameGuideLayout.ColumnInnerX);

            // The combination lines: at 13 pt about 6.4 units a character and 13.6 a line (0.3.2 headset screenshots).
            var lines = FrameButtons.ChordLines(Bindings.Load(Repo.ExpectedPatchedBinding));
            double perLine = FrameGuideLayout.NotesWidth / 6.4;
            int rows = lines.Sum(l => (int)Math.Ceiling(l.Length / perLine));
            Assert.True(rows * 13.6 <= FrameGuideLayout.NotesTopY - FrameGuideLayout.BottomY, $"{rows} lines of combinations don't fit.");
        }

        [Fact]
        public void Picker_fits_between_the_columns_above_cancel()
        {
            int n = FrameButtons.Choices.Count;
            for (int i = 0; i < n; i++)
            {
                Assert.True(Math.Abs(FrameGuideLayout.PickerX(i)) + FrameGuideLayout.PickerButtonWidth / 2 <= FrameGuideLayout.ColumnInnerX - 5, "choice " + i + " reaches the boxes");
                Assert.True(FrameGuideLayout.PickerY(i) + FrameGuideLayout.PickerButtonHeight / 2 <= FrameGuideLayout.PickerTitleY - 14, "choice " + i + " overlaps the title");
            }
            float lastBottom = FrameGuideLayout.PickerY(n - 1) - FrameGuideLayout.PickerButtonHeight / 2;
            Assert.True(FrameGuideLayout.CancelY + FrameGuideLayout.PickerButtonHeight / 2 < lastBottom, "Cancel overlaps the choices");
            Assert.True(FrameGuideLayout.PickerMessageY + FrameGuideLayout.PickerMessageHeight / 2 <= FrameGuideLayout.CancelY - FrameGuideLayout.PickerButtonHeight / 2);
            Assert.True(FrameGuideLayout.PickerMessageY - FrameGuideLayout.PickerMessageHeight / 2 >= FrameGuideLayout.BottomY);
            Assert.True(FrameGuideLayout.PickerMessageWidth / 2 <= FrameGuideLayout.ColumnInnerX);
        }

        [Fact]
        public void Picture_leader_lines_are_drawn_in_box_order()
        {
            // The leader lines are baked into frame_controllers.png by draw_frame_controllers.ps1: its list of buttons and
            // its box positions must be the page's, or the lines point at the wrong boxes.
            string script = File.ReadAllText(DrawScript);
            foreach (var (side, buttons) in new[] { ("Right", FrameButtons.Right), ("Left", FrameButtons.Left) })
            {
                var block = System.Text.RegularExpressions.Regex.Match(script, side + @" = @\((.*?)\)\)\s*$",
                    System.Text.RegularExpressions.RegexOptions.Singleline | System.Text.RegularExpressions.RegexOptions.Multiline);
                Assert.True(block.Success, "no " + side + " anchor list in the draw script");
                var names = System.Text.RegularExpressions.Regex.Matches(block.Groups[1].Value, @"@\('([^']+)'").Select(m => m.Groups[1].Value).ToList();
                Assert.Equal(buttons.Select(b => b.Label).ToList(), names);
            }
            Assert.Contains($"{FrameGuideLayout.FirstBoxY:0.##} - {FrameGuideLayout.BoxStep:0.##} * $_", script);
            Assert.Contains($"$BoxEdge = {FrameGuideLayout.ColumnInnerX:0.##}", script);
            Assert.Contains($"$Left = {-FrameGuideLayout.PictureWidth / 2:0.##}; $Top = {FrameGuideLayout.TopY:0.##}; $Wt = {FrameGuideLayout.PictureWidth:0.##}; $Ht = {FrameGuideLayout.PictureHeight:0.##}", script);
            Assert.True(File.Exists(Path.Combine(Path.GetDirectoryName(DrawScript)!, "frame_controllers.png")));
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
            Assert.Contains(rows, r => r.Hand == "Right" && r.Control == "Stick click" && r.Action == "Crouch");
        }
    }
}
