namespace Museum.Games.Dakon
{
    /// <summary>
    /// Player-facing wording for a rejected drop. Same shape as the lobby's
    /// <c>LobbyError.MessageFor</c>: one switch, one sentence, no formatting of its own.
    ///
    /// The wording is Indonesian because the rest of this screen is — the turn line, the
    /// waiting line and the results panel all are. A toast reading "SeedNotInHand" was the
    /// enum leaking through, and told the player nothing about what to do next.
    ///
    /// Deliberately outside <c>UnityEngine</c>, like the rest of the model, so it can be
    /// asserted in EditMode without a scene.
    /// </summary>
    public static class DakonErrorText
    {
        public const string NotYourTurn = "Bukan giliran kamu";
        public const string InvalidHole = "Masukkan biji ke lubang di sisimu sendiri";
        public const string SeedNotInHand = "Biji itu tidak ada di tanganmu";
        public const string HoleAlreadySown = "Lubang itu sudah terisi giliran ini";
        public const string Unknown = "Langkah itu ditolak";

        public static string MessageFor(DakonError error)
        {
            switch (error)
            {
                case DakonError.NotYourTurn: return NotYourTurn;
                case DakonError.InvalidHole: return InvalidHole;
                case DakonError.SeedNotInHand: return SeedNotInHand;
                case DakonError.HoleAlreadySown: return HoleAlreadySown;
                default: return Unknown;
            }
        }
    }
}
