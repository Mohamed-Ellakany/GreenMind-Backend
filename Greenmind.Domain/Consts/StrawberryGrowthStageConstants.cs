namespace Greenmind.Domain.Consts;

public static class StrawberryGrowthStageConstants
{
    public static class Stages
    {
        public const string PrePlanting = "PrePlanting";
        public const string Establishment = "Establishment";
        public const string Vegetative = "Vegetative";
        public const string Flowering = "Flowering";
        public const string Fruiting = "Fruiting";
        public const string HarvestReady = "HarvestReady";
    }

    public static readonly (decimal MaxProgressPercentage, string StageName)[] DefaultProgressStageRanges = new[]
    {
        (25.00m, Stages.Establishment),
        (50.00m, Stages.Vegetative),
        (75.00m, Stages.Flowering),
        (100.00m, Stages.Fruiting)
    };
}
