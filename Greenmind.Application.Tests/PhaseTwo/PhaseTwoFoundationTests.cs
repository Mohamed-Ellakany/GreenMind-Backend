using System.Text.Json;
using Greenmind.Api.Json;
using Greenmind.Domain.Consts;
using Greenmind.Domain.Consts.Enums;
using Greenmind.Domain.Entities;

namespace Greenmind.Application.Tests.PhaseTwo;

public sealed class PhaseTwoFoundationTests
{
    [Fact]
    public void EveryPhaseTwoBusinessEnum_HasAUniqueArabicLabel()
    {
        AssertComplete<ChatScope>();
        AssertComplete<ChatMessageRole>();
        AssertComplete<ChatMessageStatus>();
        AssertComplete<TaskChangeProposalStatus>();
        AssertComplete<TaskChangeOperation>();
        AssertComplete<DiseaseAnalysisStatus>();
        AssertComplete<DiseaseSeverity>();
        AssertComplete<ExpensePaymentMethod>();
        AssertComplete<ExpenseAttachmentType>();
    }

    [Fact]
    public void ArabicJsonConverter_WritesAndReadsOnlyApprovedBusinessLabels()
    {
        var options = new JsonSerializerOptions();
        options.Converters.Add(new ArabicBusinessEnumJsonConverterFactory());

        var json = JsonSerializer.Serialize(ExpensePaymentMethod.BankTransfer, options);
        var value = JsonSerializer.Deserialize<ExpensePaymentMethod>("\"تحويل بنكي\"", options);

        Assert.Equal("تحويل بنكي", JsonSerializer.Deserialize<string>(json));
        Assert.Equal(ExpensePaymentMethod.BankTransfer, value);
        Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize<ExpensePaymentMethod>("\"BankTransfer\"", options));
    }

    [Fact]
    public void Expense_RecalculateAmount_UsesAwayFromZeroRounding()
    {
        var expense = new Expense
        {
            Description = "اختبار التقريب",
            CreatedByUserId = "owner",
            Quantity = 1m,
            UnitPrice = 2.345m,
            PaymentMethod = ExpensePaymentMethod.Cash
        };

        expense.RecalculateAmount();

        Assert.Equal(2.35m, expense.Amount);
    }

    [Fact]
    public void Budget_EffectiveValue_UsesLinesOnlyWhenExplicitTotalIsMissing()
    {
        var budget = new ExportPlanBudget
        {
            UpdatedAtUtc = DateTime.UtcNow,
            Lines =
            [
                new ExportPlanBudgetLine { PlannedAmount = 100.25m },
                new ExportPlanBudgetLine { PlannedAmount = 200.50m }
            ]
        };

        Assert.Equal(300.75m, budget.EffectivePlannedBudget);

        budget.TotalBudget = 500m;

        Assert.Equal(500m, budget.EffectivePlannedBudget);
    }

    [Fact]
    public void DefaultExpenseCategories_AreArabicUniqueAndComplete()
    {
        Assert.Equal(15, DefaultExpenseCategories.ArabicNames.Count);
        Assert.Equal(
            DefaultExpenseCategories.ArabicNames.Count,
            DefaultExpenseCategories.ArabicNames.Distinct(StringComparer.Ordinal).Count());
        Assert.Contains("العمالة", DefaultExpenseCategories.ArabicNames);
        Assert.Contains("أخرى", DefaultExpenseCategories.ArabicNames);
    }

    private static void AssertComplete<TEnum>() where TEnum : struct, Enum
    {
        var labels = ArabicBusinessValues.For<TEnum>();
        Assert.Equal(Enum.GetValues<TEnum>().Length, labels.Count);
        Assert.Equal(labels.Count, labels.Values.Distinct(StringComparer.Ordinal).Count());
        Assert.All(labels.Values, label => Assert.Matches(@"[\u0600-\u06FF]", label));
    }
}
