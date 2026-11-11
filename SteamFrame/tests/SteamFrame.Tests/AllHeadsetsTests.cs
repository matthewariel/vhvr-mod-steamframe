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
        [Fact]
        public void Low_reading_against_standing_height_is_seated() =>
            Assert.Equal(Posture.Seated, PostureCalibrator.Classify(1.20f, PostureMode.Auto, 1.67f, 0.8f));

        [Fact]
        public void Near_standing_height_is_standing() =>
            Assert.Equal(Posture.Standing, PostureCalibrator.Classify(1.64f, PostureMode.Auto, 1.67f, 0.8f));

        [Fact]
        public void Seated_uses_standing_height() =>
            Assert.Equal(1.67f, PostureCalibrator.ReferenceOverride(Posture.Seated, 1.67f));

        [Fact]
        public void Standing_leaves_vhvr_alone() =>
            Assert.True(PostureCalibrator.ReferenceOverride(Posture.Standing, 1.67f) < 0);

        [Fact]
        public void Unknown_standing_height_is_seated_but_not_overridden()
        {
            Assert.Equal(Posture.Seated, PostureCalibrator.Classify(1.20f, PostureMode.Auto, 0f, 0.8f));
            Assert.True(PostureCalibrator.ReferenceOverride(Posture.Seated, 0f) < 0);
        }

        [Fact]
        public void Standing_height_is_learned_only_when_standing()
        {
            Assert.True(PostureCalibrator.ShouldLearn(Posture.Standing, PostureMode.Auto, 1.67f));
            Assert.False(PostureCalibrator.ShouldLearn(Posture.Seated, PostureMode.Auto, 1.2f));
        }

        [Fact]
        public void Seated_mode_is_always_seated() =>
            Assert.Equal(Posture.Seated, PostureCalibrator.Classify(1.6f, PostureMode.Seated, 1.67f, 0.8f));

        [Fact]
        public void Both_heights_known_splits_halfway() =>
            Assert.Equal(1.435f, PostureCalibrator.SeatedThreshold(1.67f, 1.20f, 0.8f), 3);

        [Fact]
        public void Seated_unknown_uses_percentage_of_standing() =>
            Assert.Equal(1.336f, PostureCalibrator.SeatedThreshold(1.67f, 0f, 0.8f), 3);

        [Fact]
        public void Heights_too_close_fall_back_to_percentage() =>
            Assert.Equal(1.336f, PostureCalibrator.SeatedThreshold(1.67f, 1.60f, 0.8f), 3);

        [Fact]
        public void Only_seated_known_uses_seated_plus_margin() =>
            Assert.Equal(1.45f, PostureCalibrator.SeatedThreshold(0f, 1.20f, 0.8f), 3);

        [Fact]
        public void Learned_seated_height_catches_a_tall_seat() =>
            Assert.Equal(Posture.Seated, PostureCalibrator.Classify(1.40f, PostureMode.Auto, 1.67f, 1.20f, 0.8f));

        [Fact]
        public void Above_the_split_is_standing() =>
            Assert.Equal(Posture.Standing, PostureCalibrator.Classify(1.50f, PostureMode.Auto, 1.67f, 1.20f, 0.8f));

        [Fact]
        public void Seated_height_is_learned_when_seated() =>
            Assert.True(PostureCalibrator.ShouldLearnSeated(Posture.Seated, PostureMode.Auto, 1.2f, 1.67f));

        [Fact]
        public void A_glitch_reading_is_not_learned_as_seated() =>
            Assert.False(PostureCalibrator.ShouldLearnSeated(Posture.Seated, PostureMode.Auto, 0.4f, 1.67f));

        [Fact]
        public void Forced_seated_mode_does_not_learn_a_standing_reading() =>
            Assert.False(PostureCalibrator.ShouldLearnSeated(Posture.Seated, PostureMode.Seated, 1.66f, 1.67f));

        [Fact]
        public void Seated_height_is_not_learned_from_standing() =>
            Assert.False(PostureCalibrator.ShouldLearnSeated(Posture.Standing, PostureMode.Auto, 1.6f, 1.67f));

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
        public void Overrides_parse()
        {
            var o = PromptsPolicy.ParseOverrides("Use=Right A; $KEY_AltPlace = Left Grip;;broken");
            Assert.Equal(2, o.Count);
            Assert.Equal("Right A", o["use"]);
            Assert.Equal("Left Grip", o["AltPlace"]);
        }
    }
}
