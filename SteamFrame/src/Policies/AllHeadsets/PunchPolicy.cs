namespace VhvrSteamFrame
{
    /// <summary>Which inputs a bare hand needs held for a punch to count.</summary>
    public sealed class PunchRequirements
    {
        public bool Grip = true;
        public bool Trigger;
        public bool ThumbOnStick = true;

        public int Count
        {
            get { return (Grip ? 1 : 0) + (Trigger ? 1 : 0) + (ThumbOnStick ? 1 : 0); }
        }
    }

    public enum PunchLooseness { Ok, Loose, VeryLoose }

    /// <summary>
    /// Bare-hand punch rules, as plain C#. Applied to every bare-hand hit (VHVR's own and the extra ones this plugin adds),
    /// so one set of checkboxes decides what a fist needs.
    /// </summary>
    public static class PunchPolicy
    {
        public static bool Allows(PunchRequirements r, bool grip, bool trigger, bool thumbOnStick)
        {
            return MissingInput(r, grip, trigger, thumbOnStick) == null;
        }

        /// <summary>The first required input that isn't held, for diagnostics; null when the punch is allowed.</summary>
        public static string MissingInput(PunchRequirements r, bool grip, bool trigger, bool thumbOnStick)
        {
            if (r.Grip && !grip) return "grip";
            if (r.Trigger && !trigger) return "trigger";
            if (r.ThumbOnStick && !thumbOnStick) return "thumb on stick";
            return null;
        }

        /// <summary>
        /// How easy it is to punch by accident. Without grip or trigger, any touch can count (thumb alone rests on the stick
        /// while walking). With a single requirement, grabbing or using things near objects can punch them.
        /// </summary>
        public static PunchLooseness Rate(PunchRequirements r)
        {
            if (!r.Grip && !r.Trigger) return PunchLooseness.VeryLoose;
            return r.Count >= 2 ? PunchLooseness.Ok : PunchLooseness.Loose;
        }

        public static string Warning(PunchRequirements r)
        {
            switch (Rate(r))
            {
                case PunchLooseness.VeryLoose:
                    return "Very loose: a bare hand can punch almost anything it touches. Require grip or trigger.";
                case PunchLooseness.Loose:
                    return "Loose: grabbing or using things up close may punch them. Requiring two inputs is safer.";
                default:
                    return null;
            }
        }
    }
}
