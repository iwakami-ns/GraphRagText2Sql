// Services/KeywordExpander.cs
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel.Connectors.OpenAI;

public static class KeywordExpander
{
    public static async Task<IEnumerable<string>> ExpandToEnglishAsync(Kernel kernel, string jaQuestion)
    {
        var chat = kernel.Services.GetRequiredService<IChatCompletionService>();
        var h = new ChatHistory();
        h.AddSystemMessage("Extract 3-10 short English keywords (comma separated) relevant to SQL tables/columns from the given Japanese question. No explanations.");
        h.AddUserMessage(jaQuestion);

        var msg = await chat.GetChatMessageContentAsync(h, new OpenAIPromptExecutionSettings { Temperature = 0 });
        var raw = msg.Content ?? "";
        // "sales, orders, order count, daily, last 30 days" -> tokens
        return raw.Split(new[] { ',', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                  .Select(s => s.Trim())
                  .Where(s => s.Length > 0);
    }
}
