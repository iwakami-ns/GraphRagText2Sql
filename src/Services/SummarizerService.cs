using Microsoft.SemanticKernel;
// using Microsoft.SemanticKernel.Prompts;
// using System.Text.Json;


namespace GraphRagText2Sql.Services
{
    public sealed class SummarizerService
    {
        private readonly Kernel _kernel;
        public SummarizerService(Kernel kernel) => _kernel = kernel;

        public async Task<string> SummarizeAsync(string question, string sql, object[] rows)
        {
            var promptText = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Prompts", "SummaryPrompt.txt"));
            // var t = new KernelPromptTemplate(promptText);
            // var rendered = await t.RenderAsync(_kernel, new(new()
            // {
            //     ["question"] = question,
            //     ["sql"] = sql,
            //     ["rows_json"] = JsonSerializer.Serialize(rows)
            // }));
            var rowsJson = System.Text.Json.JsonSerializer.Serialize(rows);

            string rendered = promptText
                .Replace("{{question}}", question)
                .Replace("{{sql}}", sql)
                .Replace("{{rows_json}}", rowsJson);

            var result = await _kernel.InvokePromptAsync(rendered);
            return result.GetValue<string>()?.Trim() ?? string.Empty;
        }
    }
}