namespace GraphRagText2Sql.Models
{
public record GraphNode
(
string id,
string label, // "table" | "column"
string name, // e.g., ecommerce.orders
string? table, // column 用: 所属テーブル
string pk
)
{
public string Pk => pk; // Cosmos パーティションキー
}


public record GraphEdge
(
string id,
string label, // "has_column" | "fk"
string from,
string to,
string pk
)
{
public string Pk => pk;
}


public sealed class GraphContext
{
public List<GraphNode> Nodes { get; set; } = new();
public List<GraphEdge> Edges { get; set; } = new();
}
}