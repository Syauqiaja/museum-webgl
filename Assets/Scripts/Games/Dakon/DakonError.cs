namespace Museum.Games.Dakon
{
    /// <summary>
    /// Reasons a <see cref="DakonBoard.DropSeed"/> call is rejected. Mirrors the intended
    /// P3 server error codes so the Node authority reports the same rejections.
    /// </summary>
    public enum DakonError
    {
        NotYourTurn,
        InvalidHole,
        SeedNotInHand
    }
}
