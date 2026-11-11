using System;

namespace VhvrSteamFrame
{
    public enum PostureMode { Auto, Standing, Seated }

    public enum Posture { Standing, Seated }

    /// <summary>
    /// Seated vs standing play, as plain C#.
    ///
    /// VHVR places your view at your measured eye height (clamped to PlayerMin/MaxEyeHeight) above the character's feet,
    /// so sitting down makes you ~0.5 m short. Here, a measurement well below your standing eye height means you're seated,
    /// and the standing height is used instead. Both heights are learned from recenters (and can be set by hand); once both
    /// are known, the seated/standing split is halfway between them.
    /// </summary>
    public static class PostureCalibrator
    {
        /// <summary>Below this, a first measurement can't be a standing adult's eye height.</summary>
        public const float MinStandingEyeHeight = 1.3f;

        /// <summary>Below this, a measurement is noise (controller on the floor, tracking glitch), not a seated eye height.</summary>
        public const float MinSeatedEyeHeight = 0.6f;

        /// <summary>Seated and standing heights closer than this are treated as unreliable; fall back to the percentage.</summary>
        public const float MinGap = 0.15f;

        /// <summary>Eye height below which a recenter counts as seated (Auto mode).</summary>
        public static float SeatedThreshold(float standingEyeHeight, float seatedEyeHeight, float seatedBelowFraction)
        {
            if (standingEyeHeight > 0f && seatedEyeHeight > 0f && standingEyeHeight - seatedEyeHeight >= MinGap)
                return (standingEyeHeight + seatedEyeHeight) / 2f;
            if (standingEyeHeight > 0f) return standingEyeHeight * seatedBelowFraction;
            if (seatedEyeHeight > 0f) return Math.Max(seatedEyeHeight + 0.25f, MinSeatedEyeHeight);
            return MinStandingEyeHeight;
        }

        public static Posture Classify(float eyeHeight, PostureMode mode, float standingEyeHeight, float seatedEyeHeight, float seatedBelowFraction)
        {
            if (mode == PostureMode.Standing) return Posture.Standing;
            if (mode == PostureMode.Seated) return Posture.Seated;
            return eyeHeight < SeatedThreshold(standingEyeHeight, seatedEyeHeight, seatedBelowFraction) ? Posture.Seated : Posture.Standing;
        }

        public static Posture Classify(float eyeHeight, PostureMode mode, float standingEyeHeight, float seatedBelowFraction)
        {
            return Classify(eyeHeight, mode, standingEyeHeight, 0f, seatedBelowFraction);
        }

        /// <summary>Whether a measurement should become the remembered standing eye height.</summary>
        public static bool ShouldLearn(Posture posture, PostureMode mode, float eyeHeight)
        {
            return posture == Posture.Standing && mode != PostureMode.Seated && eyeHeight >= MinStandingEyeHeight;
        }

        /// <summary>Whether a measurement should become the remembered seated eye height.</summary>
        public static bool ShouldLearnSeated(Posture posture, PostureMode mode, float eyeHeight, float standingEyeHeight)
        {
            return posture == Posture.Seated && mode != PostureMode.Standing && eyeHeight >= MinSeatedEyeHeight &&
                   (standingEyeHeight <= 0f || eyeHeight <= standingEyeHeight - MinGap);
        }

        /// <summary>The eye height VHVR should use instead of the measured one, or a negative value to leave VHVR's alone.</summary>
        public static float ReferenceOverride(Posture posture, float standingEyeHeight)
        {
            return posture == Posture.Seated && standingEyeHeight > 0f ? standingEyeHeight : -1f;
        }

        public static PostureMode ParseMode(string value)
        {
            if (value == "Standing") return PostureMode.Standing;
            if (value == "Seated") return PostureMode.Seated;
            return PostureMode.Auto;
        }
    }
}
