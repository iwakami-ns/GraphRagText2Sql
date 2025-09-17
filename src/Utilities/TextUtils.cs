using System.Text.RegularExpressions;


namespace GraphRagText2Sql.Utilities
{
public static class TextUtils
{
public static string ToSnake(string s)
=> Regex.Replace(s, "([a-z0-9])([A-Z])", "$1_$2").ToLower();


public static IEnumerable<string> Keywords(string text)
{
text = text.ToLowerInvariant();
var tokens = Regex.Matches(text, "[a-zA-Z0-9ぁ-んァ-ヴ一-龥_]+")
.Select(m => m.Value)
.Where(t => t.Length >= 2);
return tokens.Distinct();
}
}
}