namespace GraphRagText2Sql.Models
{
public sealed class AskResponse
{
public string Sql { get; set; } = string.Empty;
public object[] Rows { get; set; } = Array.Empty<object>();
public string Summary { get; set; } = string.Empty;
public string[] ContextTables { get; set; } = Array.Empty<string>();
public string PromptUsed { get; set; } = string.Empty; // デバッグ用
}
}