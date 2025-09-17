namespace GraphRagText2Sql.Models
{
public sealed class AskRequest
{
public string Question { get; set; } = string.Empty;
public int? TopK { get; set; } // 省略時は設定値
public int? MaxHops { get; set; } // 省略時は設定値
}
}