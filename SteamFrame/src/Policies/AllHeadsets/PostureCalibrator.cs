using System;

namespace VhvrSteamFrame
{
    public enum PostureMode { Auto, Standing, Seated }

    public enum Posture { Standing, Seated }

    /// <summary>
    /// Seated vs standing play: checks and settings, as plain C#. The live part is <see cref="PostureTracker"/>.
    ///
    /// You set both heights yourself (a button on the settings tab reads the headset's height); they are never learned.
    /// Until 0.3.x they were learned from every recenter, and one bad reading (1.98 m, 2026-11-10) made every later
    /// standing recenter count as seated.
    /// </summary>
    public static class PostureCalibrator
    {
        /// <summary>Below this, a reading can't be a standing adult's eye height.</summary>
        public const float MinStandingEyeHeight = 1.3f;

        /// <summary>Below this, a reading is noise (controller on the floor, tracking glitch), not a seated eye height.</summary>
        public const float MinSeatedEyeHeight = 0.6f;

        /// <summary>Above this, a reading is a tracking glitch, not anyone's eye height.</summary>
        public const float MaxEyeHeight = 2.2f;

        /// <summary>Seated and standing heights closer than this can't be told apart.</summary>
        public const float MinGap = 0.15f;

        /// <summary>Why a headset reading can't be saved as your standing / seated eye height, or null when it can.</summary>
        public static string RejectReason(Posture which, float eyeHeight)
        {
            if (float.IsNaN(eyeHeight) || eyeHeight <= 0f)
                return "Couldn't read the headset's height. Is it tracking?";
            if (eyeHeight > MaxEyeHeight)
                return "The headset reads " + eyeHeight.ToString("F2") + " m, too high to be an eye height (tracking glitch?). Try again.";
            float min = which == Posture.Standing ? MinStandingEyeHeight : MinSeatedEyeHeight;
            if (eyeHeight < min)
                return "The headset reads " + eyeHeight.ToString("F2") + " m, too low for " +
                       (which == Posture.Standing ? "standing. Stand up straight" : "sitting. Sit upright") + " and try again.";
            return null;
        }

        /// <summary>A warning when both heights are set but too close to tell apart, else null.</summary>
        public static string GapWarning(float standingEyeHeight, float seatedEyeHeight)
        {
            if (standingEyeHeight <= 0f || seatedEyeHeight <= 0f) return null;
            if (standingEyeHeight - seatedEyeHeight >= MinGap) return null;
            return "Standing and seated heights are less than " + (MinGap * 100f).ToString("F0") +
                   " cm apart, so they can't be told apart. Set them again: standing up straight, then sitting.";
        }

        /// <summary>Both heights set and far enough apart for Auto mode.</summary>
        public static bool Ready(float standingEyeHeight, float seatedEyeHeight)
        {
            return standingEyeHeight > 0f && seatedEyeHeight > 0f && standingEyeHeight - seatedEyeHeight >= MinGap;
        }

        /// <summary>Halfway between the two heights: a VHVR measurement below it counts as seated.</summary>
        public static float Split(float standingEyeHeight, float seatedEyeHeight)
        {
            return (standingEyeHeight + seatedEyeHeight) / 2f;
        }

        public static PostureMode ParseMode(string value)
        {
            if (value == "Standing") return PostureMode.Standing;
            if (value == "Seated") return PostureMode.Seated;
            return PostureMode.Auto;
        }
    }

    /// <summary>
    /// Which posture you're in and the eye height VHVR should use for it.
    ///
    /// How VHVR places your view: at its last height measurement ("anchor", taken on a recenter) your in-game eyes are
    /// at VHVR's reference height (the measurement clamped to its PlayerMin/MaxEyeHeight); after that they move with
    /// your head: in-game eye = reference + (head now - anchor). Plain VHVR therefore makes you short when you recenter
    /// seated, and tall when you stand up after it.
    ///
    /// This keeps your in-game eyes at your standing height in both postures:
    ///  - only the gap between your set heights is used for that, never the absolute heights, so it holds even when
    ///    SteamVR's floor level is off (on 2026-11-11 the headset read 2.04 m standing, ~0.5 m more than other days);
    ///  - the absolute heights are only used to tell, at each VHVR measurement, whether you were seated (below halfway);
    ///  - between measurements, posture follows your head: rising past 60% of the gap from seated counts as standing
    ///    up, dropping below 30% as sitting down, each held for <see cref="HoldSeconds"/> so a quick duck doesn't count.
    /// </summary>
    public sealed class PostureTracker
    {
        public const float StandAbove = 0.6f;
        public const float SitBelow = 0.3f;
        public const float HoldSeconds = 1.0f;

        public PostureMode Mode = PostureMode.Auto;
        public float Standing, Seated;
        public bool FollowHead = true;

        public bool HasAnchor { get; private set; }
        /// <summary>VHVR's raw eye height at its last measurement.</summary>
        public float Anchor { get; private set; }
        public Posture AnchorPosture { get; private set; }
        public Posture Current { get; private set; }
        /// <summary>How far SteamVR moved the floor since the anchor (the head reads this much higher for the same pose).</summary>
        public float FloorShiftSinceAnchor { get; private set; }

        private float changeSince = -1f;

        public float Gap { get { return Standing - Seated; } }
        public bool Ready { get { return PostureCalibrator.Ready(Standing, Seated); } }

        /// <summary>A new VHVR measurement: re-anchor and decide the posture from it.</summary>
        public Posture OnMeasured(float raw)
        {
            HasAnchor = true;
            Anchor = raw;
            FloorShiftSinceAnchor = 0f;
            changeSince = -1f;
            if (Mode == PostureMode.Standing) AnchorPosture = Posture.Standing;
            else if (Mode == PostureMode.Seated) AnchorPosture = Posture.Seated;
            else AnchorPosture = Ready && raw < PostureCalibrator.Split(Standing, Seated) ? Posture.Seated : Posture.Standing;
            Current = AnchorPosture;
            return AnchorPosture;
        }

        /// <summary>The heights changed (a Set button): decide again whether the last measurement was seated.</summary>
        public void Reclassify()
        {
            if (!HasAnchor || Mode != PostureMode.Auto) return;
            float anchorNow = Anchor + FloorShiftSinceAnchor;  // in today's floor level, like the heights
            AnchorPosture = Ready && anchorNow < PostureCalibrator.Split(Standing, Seated) ? Posture.Seated : Posture.Standing;
            Current = AnchorPosture;
            changeSince = -1f;
        }

        /// <summary>SteamVR moved its floor by <paramref name="shift"/> (head reads that much higher): keep the set heights in step.</summary>
        public void OnFloorShift(float shift)
        {
            if (Standing > 0f) Standing += shift;
            if (Seated > 0f) Seated += shift;
            if (HasAnchor) FloorShiftSinceAnchor += shift;
        }

        /// <summary>Where the head is between seated (0) and standing (1), from the live head height.</summary>
        public float Level(float head)
        {
            float fromAnchor = head - FloorShiftSinceAnchor - Anchor;
            return (fromAnchor + (AnchorPosture == Posture.Standing ? Gap : 0f)) / Gap;
        }

        /// <summary>
        /// Follows the head between measurements. Returns true when the posture changed. <paramref name="ducking"/>: VHVR's
        /// roomscale sneak is on (a deliberate duck), which must not count as sitting down.
        /// </summary>
        public bool Update(float head, float now, bool ducking = false)
        {
            if (!FollowHead || Mode != PostureMode.Auto || !Ready || !HasAnchor || float.IsNaN(head))
            {
                changeSince = -1f;
                return false;
            }
            float level = Level(head);
            bool wantsChange = Current == Posture.Seated ? level > StandAbove : level < SitBelow && !ducking;
            if (!wantsChange)
            {
                changeSince = -1f;
                return false;
            }
            if (changeSince < 0f) changeSince = now;
            if (now - changeSince < HoldSeconds) return false;
            Current = Current == Posture.Seated ? Posture.Standing : Posture.Seated;
            changeSince = -1f;
            return true;
        }

        /// <summary>
        /// The reference height VHVR should use, given its PlayerMin/MaxEyeHeight, or -1 to leave VHVR's own value
        /// (anchored standing and still standing, or nothing to go on).
        /// </summary>
        public float Reference(float minEyeHeight, float maxEyeHeight)
        {
            if (!HasAnchor || Mode == PostureMode.Standing) return -1f;
            if (!Ready)
            {
                // Seated mode with only the standing height: play at it, as before 0.4.0.
                return Mode == PostureMode.Seated && Standing > 0f ? Clamp(Standing, minEyeHeight, maxEyeHeight) : -1f;
            }
            if (AnchorPosture == Posture.Standing && Current == Posture.Standing) return -1f;

            float standingNow = AnchorPosture == Posture.Standing ? Anchor : Anchor + Gap;  // in the anchor's floor level
            float postureNow = Current == Posture.Standing ? standingNow : standingNow - Gap;
            return Clamp(standingNow, minEyeHeight, maxEyeHeight) + Anchor - postureNow;
        }

        private static float Clamp(float value, float min, float max)
        {
            if (max > 0f && value > max) return max;
            if (value < min) return min;
            return value;
        }
    }
}
