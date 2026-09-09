namespace Museum.Games.Dakon
{
    /// <summary>
    /// Outcome of a <see cref="DakonBoard.DropSeed"/> call. Shaped to match the future P3
    /// wire message so client prediction and server authority agree field-for-field.
    /// </summary>
    public struct DropResult
    {
        public bool Ok;
        public DakonError? Error;        // set only when !Ok

        public int ScoringPlayer;        // who received the point
        public SeedCategory ScoringCategory; // store the seed landed in (always the seed's own category)
        public bool WasMatch;            // true = active-match, false = opponent-mismatch
        public bool TurnEnded;           // hand emptied on this drop
        public bool GameOver;            // pool emptied after this turn's final drop

        public static DropResult Fail(DakonError error) =>
            new DropResult { Ok = false, Error = error };
    }
}
