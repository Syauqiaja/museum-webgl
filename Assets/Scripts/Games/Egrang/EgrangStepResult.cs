namespace Museum.Games.Egrang
{
    /// <summary>
    /// Outcome of one skill-check press: how well the player timed a step.
    /// Produced by <see cref="SkillCheckZones.Evaluate"/> and consumed by
    /// <see cref="EgrangStepMover"/>.
    /// </summary>
    public enum EgrangStepResult
    {
        /// <summary>Red zone. The player stumbles in place and gains no ground.</summary>
        Fail = 0,

        /// <summary>Yellow zone. A short step.</summary>
        Half = 1,

        /// <summary>Green zone. A clean, full-length step.</summary>
        Full = 2,
    }

    /// <summary>
    /// Keeps the result-to-distance mapping in one place so gameplay code never re-derives it.
    /// </summary>
    public static class EgrangStep
    {
        /// <summary>
        /// How many step lengths the given result is worth: 0 for <see cref="EgrangStepResult.Fail"/>,
        /// 1 for <see cref="EgrangStepResult.Half"/>, 2 for <see cref="EgrangStepResult.Full"/>.
        /// </summary>
        public static int StepsFor(EgrangStepResult result)
        {
            switch (result)
            {
                case EgrangStepResult.Full: return 2;
                case EgrangStepResult.Half: return 1;
                default: return 0;
            }
        }
    }
}
