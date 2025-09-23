// Services/KeywordExpander.cs
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel.Connectors.OpenAI;

public static class KeywordExpander
{
    public static async Task<string> TranslateToEnglishAsync(Kernel kernel, string jaQuestion)
    {
        var chat = kernel.Services.GetRequiredService<IChatCompletionService>();
        var h = new ChatHistory();
        h.AddSystemMessage("Translate the following Japanese question into English. No explanations.");
        h.AddUserMessage(jaQuestion);

        var msg = await chat.GetChatMessageContentAsync(h, new OpenAIPromptExecutionSettings { Temperature = 0 });
        return msg.Content ?? string.Empty;
    }

    public static async Task<IEnumerable<string>> ExtractSqlKeywordsAsync(Kernel kernel, string enQuestion)
    {
        var chat = kernel.Services.GetRequiredService<IChatCompletionService>();
        var h = new ChatHistory();
        h.AddSystemMessage("Extract 3-10 short English keywords (comma separated) relevant to SQL tables/columns from the given English question. No explanations.");
        h.AddUserMessage(enQuestion);

        var msg = await chat.GetChatMessageContentAsync(h, new OpenAIPromptExecutionSettings { Temperature = 0 });
        var raw = msg.Content ?? "";
        return raw.Split(new[] { ',', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                  .Select(s => s.Trim())
                  .Where(s => s.Length > 0);
    }

    public static async Task<IEnumerable<string>> ExpandToEnglishAsync(Kernel kernel, string jaQuestion)
    {
        var enQuestion = await TranslateToEnglishAsync(kernel, jaQuestion);
        return await ExtractSqlKeywordsAsync(kernel, enQuestion);
    }
}
