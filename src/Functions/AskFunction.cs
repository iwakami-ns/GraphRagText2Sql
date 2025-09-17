using System.Net;
using GraphRagText2Sql.Models;
using GraphRagText2Sql.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;


namespace GraphRagText2Sql.Functions
{
    public sealed class AskFunction
    {
        private readonly CosmosGraphService _graph;
        private readonly SqlGeneratorService _sqlGen;
        private readonly SqlExecutorService _exec;
        private readonly SummarizerService _sum;
        private readonly IConfiguration _config;
        public AskFunction(CosmosGraphService g, SqlGeneratorService sg, SqlExecutorService ex, SummarizerService su, IConfiguration cfg)
        { _graph = g; _sqlGen = sg; _exec = ex; _sum = su; _config = cfg; }

        [Function("ask")]
        public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "ask")] HttpRequestData req)
        {
            var body = await new StreamReader(req.Body).ReadToEndAsync();
            var ask = System.Text.Json.JsonSerializer.Deserialize<AskRequest>(body) ?? new AskRequest();
            int topK = ask.TopK ?? int.Parse(_config["App:GraphTopK"] ?? "30");
            int hops = ask.MaxHops ?? int.Parse(_config["App:GraphMaxHops"] ?? "2");

            // 1) Graph RAG で関連スキーマ抽出
            var ctx = await _graph.RetrieveSubgraphAsync(ask.Question, topK, hops);

            // 2) LLMでSQL生成
            var (sql, promptUsed) = await _sqlGen.GenerateAsync(ask.Question, ctx);
            if (string.IsNullOrWhiteSpace(sql))
            return await CreateError(req, HttpStatusCode.BadRequest, "SQL generation failed.");

            // 3) SQL 実行
            object[] rows;
            try
            {
                rows = await _exec.ExecuteAsync(sql);
            }
            catch (Exception ex)
            {
                return await CreateError(req, HttpStatusCode.BadRequest, $"SQL execution error: {ex.Message}\nSQL: {sql}");
            }

            // 4) 要約
            var summary = await _sum.SummarizeAsync(ask.Question, sql, rows);

            var res = req.CreateResponse(HttpStatusCode.OK);
            await res.WriteAsJsonAsync(new AskResponse
            {
                Sql = sql,
                Rows = rows,
                Summary = summary,
                ContextTables = ctx.Nodes.Where(n => n.label == "table").Select(n => n.name).Distinct().OrderBy(x => x).ToArray(),
                PromptUsed = promptUsed
            });
            return res;
        }

        private static async Task<HttpResponseData> CreateError(HttpRequestData req, HttpStatusCode code, string msg)
        {
            var res = req.CreateResponse(code);
            await res.WriteStringAsync(msg);
            return res;
        }
    }
}