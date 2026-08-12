using System.Runtime.CompilerServices;
using System.Text.Json;
using Greenmind.Application.Contracts.Regulatory;
using Greenmind.Application.Interfaces.AI;
using Greenmind.Domain.Consts.Enums;
using Greenmind.Infrastructure.Agents.Plugins;
using Greenmind.Infrastructure.Iti;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace Greenmind.Infrastructure.Agents;

public sealed class ConversationAiService(
    IItiBedrockGatewayClient gatewayClient,
    IRegulatoryKnowledgeQueryService regulatoryQueryService,
    ComplianceRagPlugin? complianceRag = null) : IConversationAiService
{
    private const string OutOfScopeReply =
        "عذرًا، لا يسمح هذا النموذج بالتواصل في مجال خارج المحتوى الأساسي للمشروع، وهو الزراعة وكل ما يتعلق بها.";

    public async IAsyncEnumerable<string> StreamReplyAsync(
        ConversationAiRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var deterministicRegulatoryReply = await TryBuildRegulatoryDecisionReplyAsync(
            request,
            cancellationToken);
        if (deterministicRegulatoryReply is not null)
        {
            yield return deterministicRegulatoryReply;
            yield break;
        }

        if (!IsAgricultureRelated(request))
        {
            yield return OutOfScopeReply;
            yield break;
        }

        var history = new ChatHistory();
        history.AddSystemMessage(BuildSystemPrompt(request.Scope));
        history.AddSystemMessage(BuildComplianceKnowledgePrompt());
        history.AddSystemMessage($"عنوان المحادثة: {request.SessionTitle}");

        if (!string.IsNullOrWhiteSpace(request.ConversationSummary))
        {
            history.AddSystemMessage($"ملخص المحادثة السابقة:\n{request.ConversationSummary}");
        }

        history.AddSystemMessage(
            "السياق التالي أنشأه الخادم من بيانات المزرعة المصرح بها فقط. " +
            "لا تفترض أي بيانات غير موجودة فيه:\n" + request.ContextJson);

        foreach (var item in request.RecentMessages)
        {
            history.AddMessage(MapRole(item.Role), item.Content);
        }

        history.AddSystemMessage(
            "أجب عن الرسالة الحالية الآن إجابة مكتملة. لا تكرر وعدًا سابقًا مثل «سأتحقق» أو «لحظة من فضلك». " +
            "أكواد عبوات المبيدات مثل EC والنسب المئوية والأوزان وأسماء المواد الفعالة هي بيانات زراعية، " +
            "ولا يجوز تفسيرها على أنها سؤال شخصي أو عن الزواج.");
        history.AddUserMessage(request.UserMessage);

        var ragContext = await BuildRagContextAsync(request);
        if (!string.IsNullOrWhiteSpace(ragContext))
            history.AddSystemMessage($"Authoritative retrieved compliance context:\n{ragContext}");
        history.AddSystemMessage(
            "No runtime tools are available in this gateway request. Use the farm context and the " +
            "retrieved compliance context already supplied above; never claim that you called a tool.");

        var systemPrompt = string.Join(
            "\n\n",
            history.Where(message => message.Role == AuthorRole.System)
                .Select(message => message.Content));
        var messages = history
            .Where(message => message.Role != AuthorRole.System)
            .Select(message => new ItiChatMessage(
                message.Role == AuthorRole.Assistant ? "assistant" : "user",
                message.Content ?? string.Empty))
            .ToList();
        var usageContext = new ItiAiRequestContext(
            request.Images is { Count: > 0 }
                ? AiFeatureNames.ChatImageAnalysis
                : request.Scope == ChatScope.ExportPlan
                    ? AiFeatureNames.ExportPlanChat
                    : AiFeatureNames.FarmChat,
            request.OwnerUserId,
            request.FarmId,
            request.ExportPlanId);
        var reply = request.Images is { Count: > 0 }
            ? await gatewayClient.MultimodalChatAsync(
                systemPrompt,
                string.IsNullOrWhiteSpace(request.UserMessage)
                    ? "حلّل الصور المرفقة وقدّم ملاحظاتك المهنية بوضوح."
                    : request.UserMessage,
                request.Images!.Select(image => new ItiImageInput(image.Format, image.DataBase64)).ToList(),
                cancellationToken: cancellationToken,
                usageContext: usageContext)
            : await gatewayClient.ChatAsync(
                systemPrompt,
                messages,
                cancellationToken: cancellationToken,
                usageContext: usageContext);

        if (!string.IsNullOrWhiteSpace(reply))
            yield return reply;
    }

    private static AuthorRole MapRole(ChatMessageRole role) => role switch
    {
        ChatMessageRole.User => AuthorRole.User,
        ChatMessageRole.Assistant => AuthorRole.Assistant,
        _ => AuthorRole.System
    };

    private async Task<string?> BuildRagContextAsync(ConversationAiRequest request)
    {
        if (complianceRag is null ||
            !TryGetSingleRegulatoryContext(request.ContextJson, out _, out var crop))
        {
            return null;
        }

        var query = request.UserMessage;
        if (ContainsAny(query, "disease", "treatment", "symptom", "مرض", "علاج", "أعراض"))
            return await complianceRag.SearchDiseaseTreatmentAsync(query, crop);
        if (ContainsAny(query, "pesticide", "residue", "mrl", "phi", "مبيد", "متبقيات", "فترة الأمان"))
            return await complianceRag.SearchPesticideResidueAsync(query, crop);
        if (ContainsAny(query, "phytosanitary", "certificate", "import", "export", "شهادة", "تصدير", "استيراد"))
            return await complianceRag.SearchPhytosanitaryAsync(query, crop);
        if (ContainsAny(query, "quality", "grade", "size", "packaging", "جودة", "تصنيف", "حجم", "تعبئة"))
            return await complianceRag.SearchQualityGradingAsync(query, crop);

        return null;
    }

    private static bool ContainsAny(string value, params string[] terms) =>
        terms.Any(term => value.Contains(term, StringComparison.OrdinalIgnoreCase));

    internal static bool IsAgricultureRelated(ConversationAiRequest request)
    {
        if (request.Images is { Count: > 0 })
            return true;

        if (ContainsAgricultureScopeTerm(request.UserMessage))
            return true;

        var isShortFollowUp = request.UserMessage.Trim().Length <= 120;
        return isShortFollowUp &&
               request.RecentMessages.Any(item => ContainsAgricultureScopeTerm(item.Content));
    }

    private static bool ContainsAgricultureScopeTerm(string value)
    {
        string[] terms =
        [
            "زراع", "محصول", "محاصيل", "مزرعة", "مزارع", "أرض", "ارض", "حقل", "صوب", "صوبة",
            "تربة", "ري", "مياه", "رطوبة", "سماد", "تسميد", "كمبوست", "نيتروجين", "فوسفور", "بوتاسيوم",
            "مبيد", "مبيدات", "المادة الفعالة", "متبقيات", "فترة الأمان", "mrl", "phi", "pesticide",
            "آفة", "افة", "حشرة", "فطر", "مرض", "أمراض", "علاج", "أعراض", "اصابة", "إصابة",
            "ورق", "أوراق", "جذر", "جذور", "ثمار", "زهور", "نبات", "نباتات", "شتلة", "شتلات",
            "فراولة", "طماطم", "بطاطس", "عنب", "برتقال", "خيار", "فلفل", "مانجو", "قمح", "ذرة",
            "حصاد", "زراعة", "تصدير", "تعبئة", "جودة", "تصنيف", "شهادة", "اشتراطات", "الاتحاد الأوروبي",
            "خطة", "مهام", "مهمة", "ميزانية", "تكلفة", "تكاليف", "مصروف", "مصروفات", "تأخير", "متأخرة",
            "farm", "farmer", "crop", "crops", "soil", "irrigation", "fertilizer", "compost",
            "harvest", "plant", "plants", "seedling", "disease", "treatment", "symptom", "pest",
            "export", "quality", "grading", "packaging", "phytosanitary", "residue", "budget", "task"
        ];

        return terms.Any(term => value.Contains(term, StringComparison.OrdinalIgnoreCase));
    }

    internal static bool RequiresRegulatoryLookup(ConversationAiRequest request)
    {
        if (ContainsRegulatoryTerm(request.UserMessage))
            return true;

        // Short follow-ups such as "في الخطة الثانية" or "ينفع أستخدمه؟" inherit
        // the pesticide subject from the recent conversation. This is the common
        // two-turn path on the farm-wide assistant page.
        if (request.UserMessage.Length <= 120 &&
            request.RecentMessages.Any(item =>
                item.Role == ChatMessageRole.User && ContainsRegulatoryTerm(item.Content)))
        {
            return true;
        }

        return false;
    }

    private static bool ContainsRegulatoryTerm(string value)
    {
        string[] terms =
        [
            "مبيد", "مبيدات", "المادة الفعالة", "مادة فعالة", "متبقيات",
            "حد متبقي", "فترة الأمان", "فترة امان", "بروبيكونازول",
            "pesticide", "active substance", "propiconazole", "mrl", "phi"
        ];

        return terms.Any(term => value.Contains(term, StringComparison.OrdinalIgnoreCase));
    }

    private async Task<string?> TryBuildRegulatoryDecisionReplyAsync(
        ConversationAiRequest request,
        CancellationToken cancellationToken)
    {
        if (!ContainsPesticideDecisionIntent(request.UserMessage))
            return null;

        if (!TryGetSingleRegulatoryContext(request.ContextJson, out var region, out var crop))
        {
            return null;
        }

        var substances = await FindMentionedActiveSubstancesAsync(
            region,
            request.UserMessage,
            cancellationToken);
        if (substances.Count == 0)
        {
            foreach (var item in request.RecentMessages
                         .Where(item => item.Role == ChatMessageRole.User)
                         .Reverse())
            {
                substances = await FindMentionedActiveSubstancesAsync(
                    region,
                    item.Content,
                    cancellationToken);
                if (substances.Count > 0)
                    break;
            }
        }

        if (substances.Count == 0)
            return null;

        var regionLabel = ToArabicRegion(region);
        var cropLabel = ToArabicCrop(crop);
        var replyLines = new List<string>();

        foreach (var substance in substances)
        {
            var cas = string.IsNullOrWhiteSpace(substance.CasNumber)
                ? string.Empty
                : $"، ورقم CAS هو {substance.CasNumber}";

            if (substance.ApprovalStatus == "غير معتمد")
            {
                replyLines.Add(
                    $"المادة الفعالة {substance.Name} موجودة في قاعدة بيانات {regionLabel}{cas}، " +
                    "لكن حالتها «غير معتمد». لذلك لا تعتمد استخدام المنتج الذي يحتوي عليها ضمن خطة تصدير " +
                    $"{cropLabel} إلى {regionLabel}.");
                continue;
            }

            var mrls = await regulatoryQueryService.FindProductMrlsAsync(
                region,
                crop,
                substance.Name,
                cancellationToken);
            var mrl = mrls.FirstOrDefault();
            var mrlText = mrl?.MaximumResidueLevel is decimal value
                ? $" حد المتبقيات المسجل لـ{cropLabel} هو {value} {mrl.Unit}" +
                  (mrl.IsDetectionLimit ? " كحد كشف." : ".")
                : $" لا يوجد حد متبقيات MRL مسجل لها على {cropLabel} في البيانات الحالية.";

            replyLines.Add(
                $"المادة الفعالة {substance.Name} موجودة في قاعدة بيانات {regionLabel}{cas}، " +
                $"وحالة اعتمادها «{substance.ApprovalStatus}».{mrlText}");
        }

        return string.Join("\n", replyLines) +
               "\n\nملحوظة مهمة: اعتماد المادة الفعالة وحده لا يثبت اعتماد الاسم التجاري أو الجرعة أو توقيت التطبيق. راجع ملصق المنتج والجهة التنظيمية قبل أي استخدام فعلي.";
    }

    private async Task<IReadOnlyList<Greenmind.Application.Dtos.Regulatory.ActiveSubstanceKnowledgeDto>>
        FindMentionedActiveSubstancesAsync(
            string region,
            string text,
            CancellationToken cancellationToken)
    {
        var found = (await regulatoryQueryService.FindActiveSubstancesMentionedAsync(
                region,
                text,
                cancellationToken))
            .ToList();

        // Arabic transliterations cannot be discovered from an English workbook name,
        // so retain the small translation map only as a supplement to database matching.
        foreach (var alias in FindKnownActiveSubstances(text))
        {
            var aliasMatches = await regulatoryQueryService.FindActiveSubstancesAsync(
                region,
                alias,
                cancellationToken);
            foreach (var match in aliasMatches)
            {
                if (found.All(item => item.ExternalId != match.ExternalId))
                    found.Add(match);
            }
        }

        return found;
    }

    private static bool ContainsPesticideDecisionIntent(string value)
    {
        string[] terms =
        [
            "ينفع", "استخدم", "استخدام", "مسموح", "معتمد", "اعتماد", "متبقيات", "mrl",
            "can i use", "approved", "approval"
        ];

        return terms.Any(term => value.Contains(term, StringComparison.OrdinalIgnoreCase));
    }

    private static IReadOnlyList<string> FindKnownActiveSubstances(string value)
    {
        var aliases = new (string Alias, string Search)[]
        {
            ("بروبيكونازول", "Propiconazole"),
            ("propiconazole", "Propiconazole"),
            ("أبامكتين", "Abamectin"),
            ("ابامكتين", "Abamectin"),
            ("abamectin", "Abamectin"),
            ("إيميداكلوبريد", "Imidacloprid"),
            ("ايميداكلوبريد", "Imidacloprid"),
            ("imidacloprid", "Imidacloprid")
        };

        var found = new List<string>();
        foreach (var (alias, search) in aliases)
        {
            if (!value.Contains(alias, StringComparison.OrdinalIgnoreCase) ||
                found.Contains(search, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            found.Add(search);
        }

        return found;
    }

    private static bool TryGetSingleRegulatoryContext(
        string contextJson,
        out string region,
        out string crop)
    {
        region = string.Empty;
        crop = string.Empty;

        try
        {
            using var document = JsonDocument.Parse(contextJson);
            var root = document.RootElement;
            if (root.TryGetProperty("plan", out var plan))
                return TryReadPlanContext(plan, out region, out crop);

            if (!root.TryGetProperty("plans", out var plans) ||
                plans.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            var contexts = plans
                .EnumerateArray()
                .Select(item => TryReadPlanContext(item, out var itemRegion, out var itemCrop)
                    ? (Region: itemRegion, Crop: itemCrop)
                    : default)
                .Where(item => !string.IsNullOrWhiteSpace(item.Region) &&
                               !string.IsNullOrWhiteSpace(item.Crop))
                .Distinct()
                .ToList();

            if (contexts.Count != 1)
                return false;

            region = contexts[0].Region;
            crop = contexts[0].Crop;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TryReadPlanContext(
        JsonElement plan,
        out string region,
        out string crop)
    {
        region = plan.TryGetProperty("destination", out var destination)
            ? destination.GetString() ?? string.Empty
            : string.Empty;
        crop = plan.TryGetProperty("crop", out var cropElement)
            ? cropElement.GetString() ?? string.Empty
            : string.Empty;
        return !string.IsNullOrWhiteSpace(region) && !string.IsNullOrWhiteSpace(crop);
    }

    private static string ToArabicRegion(string region) =>
        region.Equals("European Union", StringComparison.OrdinalIgnoreCase)
            ? "الاتحاد الأوروبي"
            : region;

    private static string ToArabicCrop(string crop) =>
        crop.Equals("Strawberry", StringComparison.OrdinalIgnoreCase)
            ? "الفراولة"
            : crop;

    private static string BuildComplianceKnowledgePrompt() =>
        """
        قاعدة المعرفة التنظيمية متاحة لك في المحادثة العامة ومحادثة خطة التصدير معًا عبر أدوات ComplianceRag.
        استخدم الأداة المناسبة قبل الإجابة عندما يسأل المستخدم عن متطلبات الجودة أو التصنيف أو الأحجام أو التعبئة،
        أو اشتراطات الصحة النباتية، أو الحدود القصوى للمتبقيات وفترات الأمان، أو علاج الأمراض.

        - استخدم search_quality_grading_rules لمعايير الجودة والتصنيف والتعبئة.
        - استخدم search_phytosanitary_rules لاشتراطات الصحة النباتية ودخول الأسواق.
        - استخدم search_pesticide_residue_rules لقواعد المتبقيات وفترات الأمان عندما تكون هي المصدر المطلوب،
          مع الالتزام كذلك بقواعد RegulatoryData للبيانات الجدولية المستوردة.
        - استخدم search_disease_treatment عند طلب بروتوكول علاج مرض.
        - استخدم اسم المحصول كما يرد في سياق الخادم. في المحادثة العامة التي تضم أكثر من محصول، حدّد المحصول المقصود
          من سؤال المستخدم والسياق، واطلب توضيحًا إذا ظل الاختيار غامضًا.
        - اعرض اسم المصدر ومرجع القسم اللذين تعيدهما الأداة عند الاستناد إلى النتيجة، ولا تخترع قاعدة غير موجودة.
        - إذا لم تُرجع الأداة نتائج، صرّح بأن قاعدة المعرفة لا تحتوي مصدرًا مناسبًا واطلب التحقق اليدوي.
        """;

    private static string BuildSystemPrompt(ChatScope scope) =>
        scope == ChatScope.Farm
            ? """
              أنت «عم خضر»، المساعد الزراعي في منصة GreenMind. أجب بالعربية الفصحى الرسمية، بوضوح واختصار.
              خاطب المستخدم بصيغة مهنية ومحايدة، ولا تستخدم الألقاب أو العبارات العامية مثل «يا باشا» أو «يا هندسة» أو «يا كبير».
              محادثة المزرعة للقراءة والمقارنة فقط. لا تنشئ أو تعدل أو تلغي أي مهمة،
              ولا تدّع تنفيذ أي إجراء. عند طلب تغيير تشغيلي، حدد خطة التصدير المعنية
              واطلب من المستخدم فتح محادثة تلك الخطة. استخدم فقط سياق الخادم.
              الخطة ذات ordinal=2 هي «الخطة الثانية». وإذا قال المستخدم «خطة 2» فطابق plan.id=2 أولاً،
              ثم استخدم ordinal=2 فقط عند عدم وجود خطة بهذا المعرّف. لا تطلب المحصول أو الوجهة إذا كانا
              موجودين بالفعل في بيانات الخطة المختارة داخل سياق الخادم.

              قواعد البيانات التنظيمية إلزامية عند الحديث عن المبيدات:
              - قبل الحكم على اعتماد مادة فعالة أو صلاحية مبيد لوجهة تصدير، استدعِ find_active_substance
                مستخدمًا وجهة الخطة كما تظهر حرفيًا في سياق الخادم، وابحث باسم المادة الفعالة أو CAS أو رقمها.
              - قبل ذكر حد متبقيات MRL، استدعِ find_product_mrl مستخدمًا وجهة الخطة والمحصول كما يظهران
                حرفيًا في سياق الخادم، وابحث باسم المادة الفعالة/المتبقي أو رقم المبيد.
              - إذا ذكر المستخدم اسمًا تجاريًا وتركيبه، ابحث بالمادة الفعالة المذكورة، ولا تفترض أن اعتماد
                المادة يعني تلقائيًا اعتماد المنتج التجاري أو الاستخدام المحدد.
              - لا تقل «سأتحقق» أو «لحظة من فضلك» كإجابة. استدعِ الأداة ثم قدم النتيجة الحالية في نفس الرد.
              - النتيجة الفارغة تعني أن السجل غير موجود في البيانات المستوردة، لا أنه معتمد أو مرفوض.
                صرّح بذلك واطلب التحقق التنظيمي اليدوي، ولا تخترع قيمة MRL أو فترة أمان.
              - إذا أعادت find_active_substance سجلًا، فالمادة موجودة في قاعدة البيانات. اذكر حالة الاعتماد التي أعادتها الأداة حرفيًا.
                حالة «غير معتمد» تعني أن المادة موجودة لكنها غير معتمدة، ولا يجوز وصفها بأنها «غير موجودة».
              - ميّز بوضوح بين حالة اعتماد المادة الفعالة وبين حد المتبقيات الخاص بالمحصول والوجهة.
              """
            : """
              أنت «عم خضر»، المساعد الزراعي في منصة GreenMind. أجب بالعربية الفصحى الرسمية، بوضوح واختصار.
              خاطب المستخدم بصيغة مهنية ومحايدة، ولا تستخدم الألقاب أو العبارات العامية مثل «يا باشا» أو «يا هندسة» أو «يا كبير».
              استخدم فقط سياق خطة التصدير الذي يقدمه الخادم. لا تدّع تنفيذ أي تغيير
              على المهام. أي تغيير محتمل يجب أن يظل اقتراحاً يحتاج إلى معاينة وموافقة
              صريحة من مالك المزرعة. اذكر عدم اليقين ولا تقدّم تشخيصاً مخبرياً مؤكداً.

              قواعد البيانات التنظيمية إلزامية عند الحديث عن المبيدات:
              - قبل الحكم على اعتماد مادة فعالة أو صلاحية مبيد لوجهة التصدير، استدعِ find_active_substance
                بوجهة الخطة كما تظهر حرفيًا في سياق الخادم.
              - قبل ذكر MRL أو تقييم المتبقيات، استدعِ find_product_mrl بوجهة الخطة ومحصولها كما يظهران
                حرفيًا في سياق الخادم.
              - لا تقل «سأتحقق» أو «لحظة من فضلك» كإجابة. استدعِ الأداة ثم قدم النتيجة في نفس الرد.
                النتيجة الفارغة تعني عدم وجود سجل
                في البيانات المستوردة، ولا تسمح باستنتاج الاعتماد أو الرفض أو اختراع قيمة.
              - إذا أعادت find_active_substance سجلًا، فالمادة موجودة في قاعدة البيانات. اذكر حالة الاعتماد التي أعادتها الأداة حرفيًا.
                حالة «غير معتمد» تعني أن المادة موجودة لكنها غير معتمدة، ولا يجوز وصفها بأنها «غير موجودة».
              - اعتماد المادة الفعالة لا يثبت وحده اعتماد المنتج التجاري أو الاستخدام المقترح.
              """;
}
