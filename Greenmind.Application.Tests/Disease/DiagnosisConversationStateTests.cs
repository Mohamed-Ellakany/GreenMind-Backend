using Greenmind.Application.Dtos.Disease;
using Greenmind.Application.Interfaces.AI;
using Greenmind.Domain.Consts.Enums;
using Greenmind.Infrastructure.Disease;

namespace Greenmind.Application.Tests.Disease;

public sealed class DiagnosisConversationStateTests
{
    [Fact]
    public void RecoverFacts_RebuildsFactsFromShortArabicConversationAnswers()
    {
        var history = new List<ConversationHistoryItem>
        {
            new(ChatMessageRole.Assistant, "متى ظهرت الأعراض؟ وهل هي منتشرة؟"),
            new(ChatMessageRole.User, "ظهرت من 4 ايام و محدوده في منطقه معينه"),
            new(ChatMessageRole.Assistant, "نسبة الثمار المتأثرة؟ وهل تغير الطقس؟"),
            new(ChatMessageRole.User, "مكانش فيه تغيير في الطقس و تقريبا 5 % من الزرع")
        };

        var facts = DiagnosisConversationState.RecoverFacts(EmptyFacts(), history, string.Empty);

        Assert.Contains("4 ايام", facts.Onset);
        Assert.Contains("محدوده", facts.SpreadPattern);
        Assert.Contains("5 %", facts.AffectedExtent);
        Assert.Contains("الطقس", facts.Conditions);
    }

    [Fact]
    public void RecoverFacts_UnderstandsExtentSpreadAndANegativeTreatmentAnswerTogether()
    {
        var history = new List<ConversationHistoryItem>
        {
            new(ChatMessageRole.Assistant, "الإصابة موجودة على كام ثمرة وهل هي منتشرة؟ هل اتعملت أي إجراءات أو علاج؟")
        };

        var facts = DiagnosisConversationState.RecoverFacts(
            EmptyFacts(), history, "موجوده على معظم الثمرات ومتمركزه في مكان معين\nلا");

        Assert.Contains("الثمرات", facts.AffectedPart);
        Assert.Contains("معظم", facts.AffectedExtent);
        Assert.Contains("متمركزه", facts.SpreadPattern);
        Assert.Equal("لا توجد مبيدات أو علاجات سابقة", facts.PreviousTreatments);
    }

    [Fact]
    public void RecoverFacts_UsesTheSingleServerQuestionForATerseAnswer()
    {
        var history = new List<ConversationHistoryItem>
        {
            new(ChatMessageRole.Assistant, "تمام، سجلت إجابتك.\n\nهل استخدمت أي مبيد أو علاج على الفراولة قبل ظهور الأعراض؟")
        };

        var facts = DiagnosisConversationState.RecoverFacts(EmptyFacts(), history, "لا");

        Assert.Equal("لا توجد مبيدات أو علاجات سابقة", facts.PreviousTreatments);
    }

    [Fact]
    public void BuildAssistantReply_AsksOnlyTheNextActuallyMissingFact()
    {
        var readiness = new DiagnosisReadinessDto(false,
        [
            new("image", "صورة واضحة للحالة", true),
            new("onset", "بداية الأعراض ومدتها", true),
            new("affectedExtent", "حجم أو نسبة الإصابة", false),
            new("conditions", "ظروف الري أو الطقس المحيطة", false)
        ], ["حجم أو نسبة الإصابة", "ظروف الري أو الطقس المحيطة"]);

        var reply = DiagnosisConversationState.BuildAssistantReply(
            "متى ظهرت الأعراض أول مرة؟ وهل هي منتشرة؟", readiness, false);

        Assert.Contains("كام في المية", reply);
        Assert.DoesNotContain("متى ظهرت", reply);
        Assert.DoesNotContain("الطقس", reply);
        Assert.Equal(1, reply.Count(character => character == '؟'));
    }

    private static DiagnosisFactsDto EmptyFacts() => new(null, null, null, null, null, null);
}
