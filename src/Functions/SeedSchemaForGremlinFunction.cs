using System.Net;
using GraphRagText2Sql.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace GraphRagText2Sql.Functions
{
    public sealed class SeedSchemaForGremlinFunction
    {
        private readonly SchemaSeederforGremlin _seeder;
        public SeedSchemaForGremlinFunction(SchemaSeederforGremlin seeder) => _seeder = seeder;

        // 既存の "seed-schema" と競合しないよう Function 名と Route を変更
        [Function("seed-schema-gremlin")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "seed-gremlin")] HttpRequestData req)
        {
            await _seeder.SeedAsync();
            var res = req.CreateResponse(HttpStatusCode.OK);
            await res.WriteStringAsync("seeded (gremlin)");
            return res;
        }
    }
}
