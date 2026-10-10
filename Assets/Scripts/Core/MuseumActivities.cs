namespace Museum.Core
{
    /// <summary>
    /// What a museum visitor is doing, as the presence room's <c>MuseumVisitor.activity</c> carries
    /// it: <see cref="InHall"/> while walking the museum, or the game room they went through a
    /// doorway to play. The ids are the server's <c>MUSEUM_ACTIVITIES</c>
    /// (src/rooms/museumStations.ts).
    /// </summary>
    public static class MuseumActivities
    {
        public const string InHall = "";

        /// <summary>The game's name as a visitor reads it: "Dakon", "Egrang"; empty for anything else.</summary>
        public static string DisplayName(string id)
        {
            switch (id)
            {
                case "dakon": return "Dakon";
                case "egrang": return "Egrang";
                default: return string.Empty;
            }
        }

        /// <summary>
        /// The tag under an away visitor's name — "Sedang bermain Dakon". Empty while they are in
        /// the hall; just "Sedang bermain" for a game this build does not know by name.
        /// </summary>
        public static string Tag(string id)
        {
            if (string.IsNullOrEmpty(id)) return string.Empty;

            string name = DisplayName(id);
            return name.Length > 0 ? "Sedang bermain " + name : "Sedang bermain";
        }
    }
}
