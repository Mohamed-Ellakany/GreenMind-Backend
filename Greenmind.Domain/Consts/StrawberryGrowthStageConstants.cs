namespace Greenmind.Domain.Consts
{
    public static class StrawberryGrowthStageConstants
    {
        public static class Stages
        {
            public const string NotStarted = "Not Started";
            public const string Establishment = "Establishment";
            public const string Vegetative = "Vegetative Growth";
            public const string Flowering = "Flowering & Bloom";
            public const string FruitDevelopment = "Fruit Development & Ripening";
            public const string Harvesting = "Harvesting";
        }

        public static readonly (int MinDay, int MaxDay, string StageName)[] DefaultStageRanges = new[]
        {
            (1, 30, Stages.Establishment),
            (31, 60, Stages.Vegetative),
            (61, 90, Stages.Flowering),
            (91, 120, Stages.FruitDevelopment),
            (121, int.MaxValue, Stages.Harvesting)
        };
    }
}
