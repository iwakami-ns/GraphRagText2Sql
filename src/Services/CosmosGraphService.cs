// using Azure.Cosmos;
using Microsoft.Azure.Cosmos;
using GraphRagText2Sql.Models;
using GraphRagText2Sql.Utilities;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;
using Microsoft.Extensions.Logging;

namespace GraphRagText2Sql.Services
{
    public sealed class CosmosGraphService
    {
        private readonly CosmosClient _client;
        private readonly string _db;
        private readonly string _container;
        private Container Container => _client.GetDatabase(_db).GetContainer(_container);
        private readonly IConfiguration _config;
        private readonly Kernel _kernel;
        private readonly ILogger<CosmosGraphService> _logger;

        public CosmosGraphService(CosmosClient client, IConfiguration config, Kernel kernel, ILogger<CosmosGraphService> logger)
        {
            _client = client;
            _db = config["Cosmos:Database"]!;
            _container = config["Cosmos:Container"]!;
            _config = config;
            _kernel = kernel;
            _logger = logger;
        }
        public async Task UpsertNodesAsync(IEnumerable<GraphNode> nodes)
        {
            foreach (var n in nodes)
                await Container.UpsertItemAsync(n, new PartitionKey(n.PartitionKey));
        }
        public async Task UpsertEdgesAsync(IEnumerable<GraphEdge> edges)
        {
            foreach (var e in edges)
                await Container.UpsertItemAsync(e, new PartitionKey(e.PartitionKey));
        }

        public async Task<GraphContext> RetrieveSubgraphAsync(string question, int topK, int maxHops)
        {
            // var tokens = TextUtils.Keywords(question).ToArray();
            // 1) 日本語→英語キーワードを LLM で補完（簡易版）
            var jaTokens = TextUtils.Keywords(question);                 // 形態素分割済みの日本語トークン
            var enTokens = await KeywordExpander.ExpandToEnglishAsync(_kernel, question); // LLMで英語候補を出す
            var tokens = jaTokens.Concat(enTokens).Select(t => t.ToLowerInvariant()).Distinct().ToArray();

            // tokens が空の場合のフォールバック（全テーブル/主要列を少量取得）
            QueryDefinition q;
            if (tokens.Length == 0)
            {
                q = new QueryDefinition(
                    "SELECT * FROM c WHERE c.label IN ('table','column')"
                );
            }
            else
            {
                var likeClauses = string.Join(" OR ", tokens.Select((t, i) => $"CONTAINS(c.name, @t{i})"));
                q = new QueryDefinition($"SELECT * FROM c WHERE c.label IN ('table','column') AND ({likeClauses})");
                for (int i = 0; i < tokens.Length; i++)
                {
                    q.WithParameter($"@t{i}", tokens[i]);
                    _logger.LogDebug("Parameter t{i} : {tokens[i]}", i, tokens[i]);
                }

                _logger.LogDebug("Retrieve Initial Node : {q}", q.QueryText);
            }

            var nodes = new List<GraphNode>();
            var it = Container.GetItemQueryIterator<GraphNode>(
                q,
                requestOptions: new QueryRequestOptions { MaxItemCount = topK }
            );
            while (it.HasMoreResults)
            {
                var resp = await it.ReadNextAsync();
                nodes.AddRange(resp.Resource);
                if (nodes.Count >= topK) break;
            }

            // 近傍展開（maxHops）
            var edges = new List<GraphEdge>();
            var nodeIds = new HashSet<string>(nodes.Select(n => n.id));

            for (int hop = 0; hop < maxHops; hop++)
            {
                if (nodeIds.Count == 0) break;

                // 関連エッジ取得
                var eDef = new QueryDefinition(
                    "SELECT * FROM c WHERE c.label IN ('has_column','fk') AND " +
                    "(ARRAY_CONTAINS(@ids, c[\"from\"]) OR ARRAY_CONTAINS(@ids, c[\"to\"]))"
                ).WithParameter("@ids", nodeIds.ToArray());

                _logger.LogDebug("Retrieve Related Edges : {eDef}", eDef.QueryText);

                var eit = Container.GetItemQueryIterator<GraphEdge>(eDef);
                var newEdges = new List<GraphEdge>();
                while (eit.HasMoreResults)
                {
                    var eresp = await eit.ReadNextAsync();
                    newEdges.AddRange(eresp.Resource);
                }
                foreach (var e in newEdges) edges.Add(e);

                // 新規ノードID
                var neighborIds = newEdges
                    .SelectMany(e => new[] { e.from, e.to })
                    .Where(id => !nodeIds.Contains(id))
                    .Distinct()
                    .ToArray();

                if (neighborIds.Length == 0) break;

                // 隣接ノード取得
                var nDef = new QueryDefinition(
                    "SELECT * FROM c WHERE ARRAY_CONTAINS(@ids, c.id)"
                ).WithParameter("@ids", neighborIds);

                _logger.LogDebug("Retrieve Adjacent Nodes : {nDef}", nDef.QueryText);

                var nit = Container.GetItemQueryIterator<GraphNode>(nDef);
                while (nit.HasMoreResults)
                {
                    var nresp = await nit.ReadNextAsync();
                    nodes.AddRange(nresp.Resource);
                }
                foreach (var id in neighborIds) nodeIds.Add(id);
            }

            return new GraphContext
            {
                Nodes = nodes.DistinctBy(n => n.id).ToList(),
                Edges = edges.DistinctBy(e => e.id).ToList()
            };
        }

        public static string BuildSchemaContext(GraphContext ctx)
        {
            // テーブルごとに列をまとめたコンテキスト文字列を構築
            var tables = ctx.Nodes.Where(n => n.label == "table").OrderBy(n => n.name).ToList();
            var columns = ctx.Nodes.Where(n => n.label == "column").ToList();
            var colByTable = columns.GroupBy(c => c.table).ToDictionary(g => g.Key ?? string.Empty, g => g.Select(c => c.name).OrderBy(x => x).ToArray());


            var sb = new StringBuilder();
            foreach (var t in tables)
            {
                var cols = colByTable.TryGetValue(t.name, out var arr) ? string.Join(", ", arr) : "";
                sb.AppendLine($"TABLE {t.name} (columns: {cols})");
            }
        return sb.ToString();
        }

        public static string BuildRelationships(GraphContext ctx)
        {
            var sb = new StringBuilder();
            foreach (var e in ctx.Edges.OrderBy(e => e.label))
            {
                sb.AppendLine($"{e.label}: {e.from} -> {e.to}");
            }
            return sb.ToString();
        }

    }
}
