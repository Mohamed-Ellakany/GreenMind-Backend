using System.Runtime.CompilerServices;
using Greenmind.Application.Interfaces.AI;
using Greenmind.Domain.Consts.Enums;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace Greenmind.Infrastructure.Agents;

public sealed class ConversationAiService(Kernel kernel) : IConversationAiService
{
    public async IAsyncEnumerable<string> StreamReplyAsync(
        ConversationAiRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var history = new ChatHistory();
        history.AddSystemMessage(BuildSystemPrompt(request.Scope));
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

        history.AddUserMessage(request.UserMessage);

        var settings = new OpenAIPromptExecutionSettings
        {
            Temperature = 0.2,
            FunctionChoiceBehavior = FunctionChoiceBehavior.None()
        };

        var chatCompletion = kernel.GetRequiredService<IChatCompletionService>();
        await foreach (var chunk in chatCompletion.GetStreamingChatMessageContentsAsync(
                           history,
                           settings,
                           kernel,
                           cancellationToken))
        {
            if (!string.IsNullOrEmpty(chunk.Content))
            {
                yield return chunk.Content;
            }
        }
    }

    private static AuthorRole MapRole(ChatMessageRole role) => role switch
    {
        ChatMessageRole.User => AuthorRole.User,
        ChatMessageRole.Assistant => AuthorRole.Assistant,
        _ => AuthorRole.System
    };

    private static string BuildSystemPrompt(ChatScope scope) =>
        scope == ChatScope.Farm
            ? """
              أنت مساعد زراعي استشاري لمنصة GreenMind. أجب بالعربية بوضوح واختصار.
              محادثة المزرعة للقراءة والمقارنة فقط. لا تنشئ أو تعدل أو تلغي أي مهمة،
              ولا تدّع تنفيذ أي إجراء. عند طلب تغيير تشغيلي، حدد خطة التصدير المعنية
              واطلب من المستخدم فتح محادثة تلك الخطة. استخدم فقط سياق الخادم.
              """
            : """
              أنت مساعد زراعي استشاري لمنصة GreenMind. أجب بالعربية بوضوح واختصار.
              استخدم فقط سياق خطة التصدير الذي يقدمه الخادم. لا تدّع تنفيذ أي تغيير
              على المهام. أي تغيير محتمل يجب أن يظل اقتراحاً يحتاج إلى معاينة وموافقة
              صريحة من مالك المزرعة. اذكر عدم اليقين ولا تقدّم تشخيصاً مخبرياً مؤكداً.
              """;
}
