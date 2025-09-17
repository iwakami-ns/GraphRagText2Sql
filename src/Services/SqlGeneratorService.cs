using Microsoft.SemanticKernel;
// using Microsoft.SemanticKernel.Prompts;
using GraphRagText2Sql.Models;
using Microsoft.Extensions.Configuration; // 追加

namespace GraphRagText2Sql.Services
{
    public sealed class SqlGeneratorService
    {
        private readonly Kernel _kernel;
        private readonly IConfiguration _config;
        public SqlGeneratorService(Kernel kernel, IConfiguration config)
        {
            _kernel = kernel; _config = config;
        }

        public async Task<(string sql, string promptUsed)> GenerateAsync(string question, GraphContext ctx)
        {
            var schemaContext = CosmosGraphService.BuildSchemaContext(ctx);
            var relationships = CosmosGraphService.BuildRelationships(ctx);
            var promptText = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Prompts", "SqlGenPrompt.txt"));
            // var t = new KernelPromptTemplate(promptText);
            // var rendered = await t.RenderAsync(_kernel, new(new()
            // {
            //     ["schema_context"] = schemaContext,
            //     ["relationships"] = relationships,
            //     ["question"] = question
            // }));
            
            // {{var}} を素朴に置換（POC簡易版）
            string rendered = promptText
                .Replace("{{schema_context}}", schemaContext)
                .Replace("{{relationships}}", relationships)
                .Replace("{{question}}", question);


            var result = await _kernel.InvokePromptAsync(rendered);
            var sql = result.GetValue<string>()?.Trim() ?? string.Empty;
            return (sql, rendered);
        }
    }
}