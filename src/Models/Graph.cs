using Newtonsoft.Json;

namespace GraphRagText2Sql.Models
{
    public record GraphNode
    (
        string id,
        string label,   // "table" | "column"
        string name,    // e.g., ecommerce.orders
        string? table,  // column 用: 所属テーブル
        [property: JsonProperty(PropertyName = "pk")] string PartitionKey
    );

    public record GraphEdge
    (
        string id,
        string label,   // "has_column" | "fk"
        string from,
        string to,
        [property: JsonProperty(PropertyName = "pk")] string PartitionKey
    );

    public sealed class GraphContext
    {
        public List<GraphNode> Nodes { get; set; } = new();
        public List<GraphEdge> Edges { get; set; } = new();
    }
}
