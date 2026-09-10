namespace Museum.Games.Dakon
{
    /// <summary>
    /// Reasons a <see cref="DakonBoard.DropSeed"/> call is rejected. Mirrors the server's
    /// `DakonErrorCode` (`not_your_turn`, `invalid_hole`, `hole_already_sown`,
    /// `seed_not_in_hand`) so the Node authority reports the same rejections.
    /// </summary>
    public enum DakonError
    {
        NotYourTurn,

        /// <summary>Out of range, or on the opponent's side of the ring.</summary>
        InvalidHole,

        SeedNotInHand,

        /// <summary>One of the player's own holes, but it already took a seed this turn.</summary>
        HoleAlreadySown
    }
}
