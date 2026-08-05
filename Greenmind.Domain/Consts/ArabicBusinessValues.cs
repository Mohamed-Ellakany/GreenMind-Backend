using Greenmind.Domain.Consts.Enums;

namespace Greenmind.Domain.Consts;

public static class ArabicBusinessValues
{
    private static readonly IReadOnlyDictionary<ChatScope, string> ChatScopes =
        new Dictionary<ChatScope, string>
        {
            [ChatScope.ExportPlan] = "خطة تصدير",
            [ChatScope.Farm] = "المزرعة",
            [ChatScope.Diagnosis] = "تشخيص المحصول"
        };

    private static readonly IReadOnlyDictionary<ChatMessageRole, string> ChatMessageRoles =
        new Dictionary<ChatMessageRole, string>
        {
            [ChatMessageRole.User] = "المستخدم",
            [ChatMessageRole.Assistant] = "المساعد",
            [ChatMessageRole.System] = "النظام"
        };

    private static readonly IReadOnlyDictionary<ChatMessageStatus, string> ChatMessageStatuses =
        new Dictionary<ChatMessageStatus, string>
        {
            [ChatMessageStatus.Sending] = "قيد الإرسال",
            [ChatMessageStatus.Completed] = "مكتمل",
            [ChatMessageStatus.Failed] = "فشل"
        };

    private static readonly IReadOnlyDictionary<TaskChangeProposalStatus, string> ProposalStatuses =
        new Dictionary<TaskChangeProposalStatus, string>
        {
            [TaskChangeProposalStatus.PendingReview] = "قيد المراجعة",
            [TaskChangeProposalStatus.Applied] = "تم التطبيق",
            [TaskChangeProposalStatus.Rejected] = "مرفوض",
            [TaskChangeProposalStatus.Expired] = "منتهي",
            [TaskChangeProposalStatus.Failed] = "فشل"
        };

    private static readonly IReadOnlyDictionary<TaskChangeOperation, string> ProposalOperations =
        new Dictionary<TaskChangeOperation, string>
        {
            [TaskChangeOperation.Add] = "إضافة",
            [TaskChangeOperation.Modify] = "تعديل",
            [TaskChangeOperation.Cancel] = "إلغاء"
        };

    private static readonly IReadOnlyDictionary<DiseaseAnalysisStatus, string> DiseaseStatuses =
        new Dictionary<DiseaseAnalysisStatus, string>
        {
            [DiseaseAnalysisStatus.Waiting] = "قيد الانتظار",
            [DiseaseAnalysisStatus.Analyzing] = "جاري التحليل",
            [DiseaseAnalysisStatus.Completed] = "مكتمل",
            [DiseaseAnalysisStatus.Failed] = "فشل",
            [DiseaseAnalysisStatus.Draft] = "مسودة",
            [DiseaseAnalysisStatus.Ready] = "جاهز للتشخيص",
            [DiseaseAnalysisStatus.Cancelled] = "ملغي"
        };

    private static readonly IReadOnlyDictionary<DiseaseSeverity, string> DiseaseSeverities =
        new Dictionary<DiseaseSeverity, string>
        {
            [DiseaseSeverity.Low] = "منخفضة",
            [DiseaseSeverity.Medium] = "متوسطة",
            [DiseaseSeverity.High] = "عالية",
            [DiseaseSeverity.Critical] = "حرجة"
        };

    private static readonly IReadOnlyDictionary<ExpensePaymentMethod, string> PaymentMethods =
        new Dictionary<ExpensePaymentMethod, string>
        {
            [ExpensePaymentMethod.Cash] = "نقدي",
            [ExpensePaymentMethod.BankTransfer] = "تحويل بنكي",
            [ExpensePaymentMethod.Card] = "بطاقة",
            [ExpensePaymentMethod.DigitalWallet] = "محفظة إلكترونية"
        };

    private static readonly IReadOnlyDictionary<ExpenseAttachmentType, string> AttachmentTypes =
        new Dictionary<ExpenseAttachmentType, string>
        {
            [ExpenseAttachmentType.Receipt] = "إيصال",
            [ExpenseAttachmentType.Invoice] = "فاتورة",
            [ExpenseAttachmentType.Other] = "أخرى"
        };

    public static IReadOnlyDictionary<TEnum, string> For<TEnum>() where TEnum : struct, Enum
    {
        object values = typeof(TEnum) == typeof(ChatScope) ? ChatScopes
            : typeof(TEnum) == typeof(ChatMessageRole) ? ChatMessageRoles
            : typeof(TEnum) == typeof(ChatMessageStatus) ? ChatMessageStatuses
            : typeof(TEnum) == typeof(TaskChangeProposalStatus) ? ProposalStatuses
            : typeof(TEnum) == typeof(TaskChangeOperation) ? ProposalOperations
            : typeof(TEnum) == typeof(DiseaseAnalysisStatus) ? DiseaseStatuses
            : typeof(TEnum) == typeof(DiseaseSeverity) ? DiseaseSeverities
            : typeof(TEnum) == typeof(ExpensePaymentMethod) ? PaymentMethods
            : typeof(TEnum) == typeof(ExpenseAttachmentType) ? AttachmentTypes
            : throw new NotSupportedException($"{typeof(TEnum).Name} is not a Phase Two business enum.");

        return (IReadOnlyDictionary<TEnum, string>)values;
    }

    public static string ToArabic<TEnum>(this TEnum value) where TEnum : struct, Enum =>
        For<TEnum>().TryGetValue(value, out var label)
            ? label
            : throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown business enum value.");

    public static bool TryParseArabic<TEnum>(string? label, out TEnum value) where TEnum : struct, Enum
    {
        foreach (var pair in For<TEnum>())
        {
            if (string.Equals(pair.Value, label?.Trim(), StringComparison.Ordinal))
            {
                value = pair.Key;
                return true;
            }
        }

        value = default;
        return false;
    }
}
