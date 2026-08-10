namespace Greenmind.Domain.Consts;

/// <summary>
/// Stable identities and capabilities for system-seeded crops.
/// Display names are intentionally not used for capability or knowledge decisions.
/// </summary>
public static class CropCatalog
{
    public static class Strawberry
    {
        public const int Id = 1;
        public const string CanonicalKey = "strawberry";
        public const string DefaultDisplayName = "Strawberry";
    }

    public static bool SupportsSmartDiagnosis(int cropId) =>
        cropId == Strawberry.Id;

    public static string? GetDiagnosisKnowledgeKey(int cropId) =>
        GetCanonicalKey(cropId);

    public static string? GetCanonicalKey(int cropId) =>
        cropId == Strawberry.Id ? Strawberry.CanonicalKey : null;

    public static string? NormalizeCanonicalKey(string? crop)
    {
        if (string.IsNullOrWhiteSpace(crop))
            return null;

        var trimmed = crop.Trim();
        return trimmed.Equals(Strawberry.CanonicalKey, StringComparison.OrdinalIgnoreCase)
            ? Strawberry.CanonicalKey
            : trimmed;
    }

    public static string[] GetLookupKeys(string crop)
    {
        var normalized = NormalizeCanonicalKey(crop);
        if (normalized is null)
            return [];

        return normalized == Strawberry.CanonicalKey
            ? [Strawberry.CanonicalKey, Strawberry.DefaultDisplayName]
            : [normalized];
    }
}
