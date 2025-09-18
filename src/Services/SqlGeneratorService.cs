using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using GraphRagText2Sql.Models;
using Microsoft.Extensions.Configuration; // 追加
using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace GraphRagText2Sql.Services
{
    public sealed class SqlGeneratorService
    {
        private readonly Kernel _kernel;

        private readonly IChatCompletionService _chat;
        private readonly IConfiguration _config;
        private readonly ILogger<SqlGeneratorService> _logger;
        public SqlGeneratorService(Kernel kernel, IConfiguration config, ILogger<SqlGeneratorService> logger)
        {
            _kernel = kernel;
            _chat = _kernel.GetRequiredService<IChatCompletionService>();
            _config = config;
            _logger = logger;
        }

        public async Task<(string sql, string promptUsed)> GenerateAsync(string question, GraphContext ctx)
        {

            var schemaContext = CosmosGraphService.BuildSchemaContext(ctx);
            var relationships = CosmosGraphService.BuildRelationships(ctx);
            var promptText = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Prompts", "SqlGenPrompt.txt"));
            string rendered = promptText
                .Replace("{{schema_context}}", schemaContext)
                .Replace("{{relationships}}", relationships)
                .Replace("{{question}}", question);

            _logger.LogDebug("schemaContext : {schemaContext}", schemaContext);
            _logger.LogDebug("relationships : {relationships}", relationships);

            var history = new ChatHistory();
            history.AddSystemMessage(rendered);
            var result = await _chat.GetChatMessageContentAsync(history, kernel: _kernel);
            var sql = result?.Content?
                        .Trim()
                        .Replace("```sql", "")
                        .Replace("```", "") ?? string.Empty;
            
            return (sql, rendered);
        }
    }
}