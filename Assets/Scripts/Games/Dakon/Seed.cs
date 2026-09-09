namespace Museum.Games.Dakon
{
    /// <summary>
    /// One seed (pure model value — no UnityEngine dependency). <see cref="Id"/> is unique per
    /// game and is the handle the view/protocol passes to <see cref="DakonBoard.DropSeed"/>.
    /// <see cref="Category"/> is what scores; <see cref="TypeId"/> names the species and maps
    /// to a <see cref="SeedType"/> asset the view resolves for sprite/name.
    /// </summary>
    public readonly struct Seed
    {
        public readonly string Id;
        public readonly SeedCategory Category;
        public readonly string TypeId;

        public Seed(string id, SeedCategory category, string typeId)
        {
            Id = id;
            Category = category;
            TypeId = typeId;
        }
    }
}
