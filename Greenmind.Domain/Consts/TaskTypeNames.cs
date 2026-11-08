namespace Greenmind.Domain.Consts;

public static class TaskTypeNames
{
    public const string Irrigation = "Irrigation";
    public const string Fertilization = "Fertilization";
    public const string PestControl = "PestControl";
    public const string Pruning = "Pruning";
    public const string SoilTest = "SoilTest";
    public const string Harvest = "Harvest";
    public const string ExportPrep = "ExportPrep";
    public const string QualityCheck = "QualityCheck";

    public static string Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;

        var trimmed = raw.Trim();

        if (trimmed.Equals(Irrigation, StringComparison.OrdinalIgnoreCase)) return Irrigation;
        if (trimmed.Equals(Fertilization, StringComparison.OrdinalIgnoreCase)) return Fertilization;
        if (trimmed.Equals(PestControl, StringComparison.OrdinalIgnoreCase)) return PestControl;
        if (trimmed.Equals(Pruning, StringComparison.OrdinalIgnoreCase)) return Pruning;
        if (trimmed.Equals(SoilTest, StringComparison.OrdinalIgnoreCase)) return SoilTest;
        if (trimmed.Equals(Harvest, StringComparison.OrdinalIgnoreCase)) return Harvest;
        if (trimmed.Equals(ExportPrep, StringComparison.OrdinalIgnoreCase)) return ExportPrep;
        if (trimmed.Equals(QualityCheck, StringComparison.OrdinalIgnoreCase)) return QualityCheck;

        var lower = trimmed.ToLowerInvariant();

        if (lower.Contains("irrigat") || lower.Contains("water") || lower.Contains("ري") || lower.Contains("سقي") || lower.Contains("تنقيط"))
            return Irrigation;

        if (lower.Contains("fertil") || lower.Contains("fertig") || lower.Contains("تسميد") || lower.Contains("سماد") || lower.Contains("مغذيات") || lower.Contains("npk"))
            return Fertilization;

        if (lower.Contains("pest") || lower.Contains("fungi") || lower.Contains("insect") || lower.Contains("مكافح") || lower.Contains("رش") || lower.Contains("وقاي") || lower.Contains("مبيد") || lower.Contains("آفات") || lower.Contains("افات"))
            return PestControl;

        if (lower.Contains("prun") || lower.Contains("trim") || lower.Contains("تقليم") || lower.Contains("قص") || lower.Contains("توريق") || lower.Contains("خف"))
            return Pruning;

        if (lower.Contains("soil") || lower.Contains("sample") || lower.Contains("ترب") || lower.Contains("تحليل") || lower.Contains("فحص التربة"))
            return SoilTest;

        if (lower.Contains("harvest") || lower.Contains("pick") || lower.Contains("حصاد") || lower.Contains("جمع") || lower.Contains("قطف") || lower.Contains("جني"))
            return Harvest;

        if (lower.Contains("export") || lower.Contains("pack") || lower.Contains("تصدير") || lower.Contains("تجهيز") || lower.Contains("تعبئ") || lower.Contains("فرز") || lower.Contains("شحن"))
            return ExportPrep;

        if (lower.Contains("qualit") || lower.Contains("check") || lower.Contains("inspect") || lower.Contains("جود") || lower.Contains("فحص") || lower.Contains("مطابق") || lower.Contains("معاين"))
            return QualityCheck;

        return trimmed;
    }
}
