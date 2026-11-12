using Xunit;

namespace VhvrSteamFrame.Tests
{
    public class PunchTests
    {
        [Fact]
        public void Grip_plus_thumb_is_ok() =>
            Assert.Equal(PunchLooseness.Ok, PunchPolicy.Rate(new PunchRequirements { Grip = true, ThumbOnStick = true }));

        [Fact]
        public void Grip_only_is_loose() =>
            Assert.Equal(PunchLooseness.Loose, PunchPolicy.Rate(new PunchRequirements { Grip = true, ThumbOnStick = false }));

        [Fact]
        public void Thumb_only_is_very_loose() =>
            Assert.Equal(PunchLooseness.VeryLoose, PunchPolicy.Rate(new PunchRequirements { Grip = false, Trigger = false, ThumbOnStick = true }));

        [Fact]
        public void Trigger_plus_thumb_is_ok() =>
            Assert.Equal(PunchLooseness.Ok, PunchPolicy.Rate(new PunchRequirements { Grip = false, Trigger = true, ThumbOnStick = true }));

        [Fact]
        public void Grip_plus_thumb_allows_only_with_both_held()
        {
            var req = new PunchRequirements { Grip = true, Trigger = false, ThumbOnStick = true };
            Assert.True(PunchPolicy.Allows(req, true, false, true));
            Assert.False(PunchPolicy.Allows(req, true, false, false));
            Assert.False(PunchPolicy.Allows(req, false, true, true));
        }

        [Fact]
        public void Missing_input_is_named()
        {
            var req = new PunchRequirements { Grip = true, Trigger = false, ThumbOnStick = true };
            Assert.Equal("thumb on stick", PunchPolicy.MissingInput(req, true, false, false));
        }

        [Fact]
        public void Warnings_match_the_rating()
        {
            Assert.Null(PunchPolicy.Warning(new PunchRequirements { Grip = true, ThumbOnStick = true }));
            Assert.StartsWith("Loose", PunchPolicy.Warning(new PunchRequirements { Grip = true, ThumbOnStick = false }));
            Assert.StartsWith("Very loose", PunchPolicy.Warning(new PunchRequirements { Grip = false, ThumbOnStick = true }));
        }
    }

    public class PostureTests
    {
        // VHVR's [PlayerMinEyeHeight, PlayerMaxEyeHeight] in the user's config.
        private const float Min = 1.2f, Max = 1.9f;

        private static PostureTracker Tracker(float standing = 1.53f, float seated = 1.27f)
        {
            return new PostureTracker { Standing = standing, Seated = seated };
        }

        /// <summary>In-game eye height, the way VHVR places it: reference + (head now - anchor).</summary>
        private static float InGame(PostureTracker t, float vhvrOwn, float head)
        {
            float r = t.Reference(Min, Max);
            return (r > 0f ? r : vhvrOwn) + head - t.Anchor;
        }

        [Fact]
        public void Standing_recenter_leaves_vhvr_alone()
        {
            var t = Tracker();
            Assert.Equal(Posture.Standing, t.OnMeasured(1.54f));
            Assert.True(t.Reference(Min, Max) < 0f);
        }

        [Fact]
        public void Seated_recenter_plays_at_standing_height()
        {
            var t = Tracker();
            Assert.Equal(Posture.Seated, t.OnMeasured(1.27f));
            Assert.Equal(1.53f, InGame(t, 1.27f, 1.27f), 2);
        }

        /// <summary>2026-11-11, screenshots 1-2: seated recenter, then standing up made the viking comically tall.</summary>
        [Fact]
        public void Standing_up_after_a_seated_recenter_switches_and_keeps_the_height()
        {
            var t = Tracker();
            t.OnMeasured(1.27f);
            Assert.False(t.Update(1.53f, 0f));             // stood up: held for a moment first
            Assert.True(t.Update(1.53f, 1.1f));            // then it switches
            Assert.Equal(Posture.Standing, t.Current);
            Assert.Equal(1.53f, InGame(t, 1.27f, 1.53f), 2);
        }

        [Fact]
        public void Sitting_down_after_a_standing_recenter_switches_and_keeps_the_height()
        {
            var t = Tracker();
            t.OnMeasured(1.53f);
            t.Update(1.27f, 0f);
            Assert.True(t.Update(1.27f, 1.1f));
            Assert.Equal(1.53f, InGame(t, 1.53f, 1.27f), 2);
        }

        [Fact]
        public void A_quick_duck_does_not_count_as_sitting()
        {
            var t = Tracker();
            t.OnMeasured(1.53f);
            Assert.False(t.Update(1.27f, 0f));
            Assert.False(t.Update(1.53f, 0.5f));           // back up before the hold time
            Assert.False(t.Update(1.27f, 0.6f));
            Assert.False(t.Update(1.27f, 1.0f));           // the hold starts again
            Assert.Equal(Posture.Standing, t.Current);
        }

        [Fact]
        public void A_roomscale_sneak_does_not_count_as_sitting()
        {
            var t = Tracker();
            t.OnMeasured(1.53f);
            t.Update(1.0f, 0f, ducking: true);
            Assert.False(t.Update(1.0f, 2f, ducking: true));
        }

        /// <summary>
        /// 2026-11-11: SteamVR's floor was ~0.5 m off, so the headset read 2.04 m standing and 1.55 m seated, and VHVR's own
        /// standing reference was clamped to 1.90. Seated, the plugin used 2.04 unclamped: too tall. Now: the clamped 1.90.
        /// </summary>
        [Fact]
        public void A_high_floor_reading_still_gives_vhvrs_clamped_standing_height()
        {
            var t = Tracker(2.04f, 1.55f);
            t.OnMeasured(1.55f);                            // seated
            Assert.Equal(1.90f, InGame(t, 1.55f, 1.55f), 2);
            t.Update(2.04f, 0f);
            t.Update(2.04f, 1.1f);                          // stood up
            Assert.Equal(1.90f, InGame(t, 1.55f, 2.04f), 2);
        }

        [Fact]
        public void Heights_follow_a_floor_shift()
        {
            var t = Tracker();
            t.OnMeasured(1.53f);
            t.OnFloorShift(0.45f);                          // SteamVR moved the floor: the head now reads 0.45 m higher
            Assert.Equal(1.98f, t.Standing, 2);
            Assert.Equal(1.72f, t.Seated, 2);
            Assert.False(t.Update(1.98f, 0f));              // standing still, reading higher: not a posture change
            Assert.False(t.Update(1.98f, 2f));
            Assert.Equal(Posture.Standing, t.Current);
            Assert.Equal(Posture.Standing, t.OnMeasured(1.98f)); // VHVR re-measures after the shift: still standing
        }

        [Fact]
        public void Setting_heights_reclassifies_the_last_measurement()
        {
            var t = new PostureTracker();
            t.OnMeasured(1.48f);                            // no heights yet: standing
            t.Standing = 2.04f; t.Seated = 1.55f;
            t.Reclassify();
            Assert.Equal(Posture.Seated, t.AnchorPosture);  // below halfway (1.80)
        }

        [Fact]
        public void Without_both_heights_auto_does_nothing()
        {
            var t = Tracker(1.53f, 0f);
            t.OnMeasured(1.2f);
            Assert.True(t.Reference(Min, Max) < 0f);
            Assert.False(t.Update(1.53f, 5f));
        }

        [Fact]
        public void Seated_mode_with_only_standing_set_plays_at_it()
        {
            var t = new PostureTracker { Mode = PostureMode.Seated, Standing = 1.53f };
            t.OnMeasured(1.2f);
            Assert.Equal(1.53f, t.Reference(Min, Max), 2);
        }

        [Fact]
        public void Standing_mode_never_overrides()
        {
            var t = new PostureTracker { Mode = PostureMode.Standing, Standing = 1.53f, Seated = 1.27f };
            t.OnMeasured(1.27f);
            Assert.True(t.Reference(Min, Max) < 0f);
        }

        [Fact]
        public void Following_the_head_can_be_turned_off()
        {
            var t = Tracker();
            t.FollowHead = false;
            t.OnMeasured(1.27f);
            t.Update(1.53f, 0f);
            Assert.False(t.Update(1.53f, 5f));
            Assert.Equal(Posture.Seated, t.Current);
        }

        [Fact]
        public void Sensible_readings_can_be_set()
        {
            Assert.Null(PostureCalibrator.RejectReason(Posture.Standing, 1.53f));
            Assert.Null(PostureCalibrator.RejectReason(Posture.Seated, 1.27f));
        }

        [Fact]
        public void Glitch_readings_are_refused()
        {
            Assert.NotNull(PostureCalibrator.RejectReason(Posture.Standing, 2.5f));
            Assert.NotNull(PostureCalibrator.RejectReason(Posture.Seated, 0.4f));
            Assert.NotNull(PostureCalibrator.RejectReason(Posture.Standing, float.NaN));
        }

        [Fact]
        public void A_seated_reading_is_refused_as_standing() =>
            Assert.Contains("too low for standing", PostureCalibrator.RejectReason(Posture.Standing, 1.2f));

        [Fact]
        public void Heights_too_close_together_warn()
        {
            Assert.NotNull(PostureCalibrator.GapWarning(1.55f, 1.50f));
            Assert.Null(PostureCalibrator.GapWarning(1.55f, 1.27f));
            Assert.Null(PostureCalibrator.GapWarning(1.55f, 0f));
        }

        [Fact]
        public void Mode_names_parse()
        {
            Assert.Equal(PostureMode.Standing, PostureCalibrator.ParseMode("Standing"));
            Assert.Equal(PostureMode.Seated, PostureCalibrator.ParseMode("Seated"));
            Assert.Equal(PostureMode.Auto, PostureCalibrator.ParseMode("anything else"));
        }
    }

    public class CrouchTests
    {
        private static CrouchGate Gate() => new CrouchGate { DoubleClickWindow = 0.45f };

        [Fact]
        public void Crouch_click_in_a_chest_is_blocked() =>
            Assert.Equal(CrouchDecision.BlockInMenu, Gate().Decide(true, false, true, true, true, false, 1f));

        [Fact]
        public void Stand_up_click_in_a_chest_is_blocked_too() =>
            Assert.Equal(CrouchDecision.BlockInMenu, Gate().Decide(true, true, true, true, true, false, 1f));

        [Fact]
        public void With_blocking_off_a_chest_click_crouches() =>
            Assert.Equal(CrouchDecision.Allow, Gate().Decide(true, false, true, true, false, false, 1f));

        [Fact]
        public void Single_click_crouches_when_double_click_is_off() =>
            Assert.Equal(CrouchDecision.Allow, Gate().Decide(true, false, true, false, true, false, 1f));

        [Fact]
        public void Double_click_within_the_window_crouches()
        {
            var gate = Gate();
            Assert.Equal(CrouchDecision.WaitForSecondClick, gate.Decide(true, false, true, false, true, true, 10f));
            Assert.Equal(CrouchDecision.Allow, gate.Decide(true, false, true, false, true, true, 10.3f));
        }

        [Fact]
        public void Clicks_too_far_apart_do_not_crouch()
        {
            var gate = Gate();
            Assert.Equal(CrouchDecision.WaitForSecondClick, gate.Decide(true, false, true, false, true, true, 20f));
            Assert.Equal(CrouchDecision.WaitForSecondClick, gate.Decide(true, false, true, false, true, true, 20.6f));
        }

        [Fact]
        public void Standing_up_is_one_click() =>
            Assert.Equal(CrouchDecision.Allow, Gate().Decide(true, true, true, false, true, true, 30f));

        [Fact]
        public void Physical_ducking_is_not_double_click_gated() =>
            Assert.Equal(CrouchDecision.Allow, Gate().Decide(true, false, false, false, true, true, 40f));

        [Fact]
        public void No_toggle_requested_means_nothing_to_do() =>
            Assert.Equal(CrouchDecision.Allow, Gate().Decide(false, false, true, true, true, true, 50f));
    }

    public class MenuStickClicksTests
    {
        [Fact]
        public void Stick_click_keys_map_to_vr_actions()
        {
            Assert.Equal(MenuStickClicksPolicy.StickAction.ToggleCrouchRight, MenuStickClicksPolicy.ActionFor("JoyRStick"));
            Assert.Equal(MenuStickClicksPolicy.StickAction.ToggleRunLeft, MenuStickClicksPolicy.ActionFor("JoyLStick"));
            Assert.Equal(MenuStickClicksPolicy.StickAction.None, MenuStickClicksPolicy.ActionFor("JoyButtonA"));
        }
    }

    public class PromptsPolicyTests
    {
        [Fact]
        public void Keys_resolve_with_or_without_the_Joy_prefix()
        {
            Assert.Equal("MapZoomIn", PromptsPolicy.ActionFromKey("KEY_JoyMapZoomIn"));
            Assert.Equal("ButtonX", PromptsPolicy.ActionFromKey("KEY_ButtonX"));
            Assert.Null(PromptsPolicy.ActionFromKey("piece_workbench"));
        }

        [Fact]
        public void Dpad_pictures_are_recognised_by_object_or_sprite_name()
        {
            Assert.True(PromptsPolicy.IsDpadPicture("DPad", null));
            Assert.True(PromptsPolicy.IsDpadPicture("Image", "xbox_D-Pad_updown"));
            Assert.True(PromptsPolicy.IsDpadPicture("gamepad_d_pad", "UISprite"));
            Assert.False(PromptsPolicy.IsDpadPicture("Icon0", "mapicon_fire"));
            Assert.False(PromptsPolicy.IsDpadPicture(null, null));
        }

        [Fact]
        public void Overrides_parse()
        {
            var o = PromptsPolicy.ParseOverrides("Use=Right A; $KEY_AltPlace = Left Grip;;broken");
            Assert.Equal(2, o.Count);
            Assert.Equal("Right A", o["use"]);
            Assert.Equal("Left Grip", o["AltPlace"]);
        }
    }
}
