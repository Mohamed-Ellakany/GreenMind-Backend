using System.Text.Json.Serialization;

namespace Greenmind.Domain.Consts.Enums;

public enum ActiveSubstanceApprovalStatus : byte
{
    [JsonStringEnumMemberName("قيد المراجعة")]
    قيد_المراجعة = 1,

    [JsonStringEnumMemberName("غير معتمد")]
    غير_معتمد = 2,

    [JsonStringEnumMemberName("معتمد")]
    معتمد = 3
}
