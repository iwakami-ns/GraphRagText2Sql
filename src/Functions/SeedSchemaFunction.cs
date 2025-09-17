using System.Net;
using GraphRagText2Sql.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;


namespace GraphRagText2Sql.Functions
{
    public sealed class SeedSchemaFunction
    {
        private readonly SchemaSeeder _seeder;
        public SeedSchemaFunction(SchemaSeeder seeder) => _seeder = seeder;

        [Function("seed-schema")]
        public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "seed")] HttpRequestData req)
        {
            await _seeder.SeedAsync();
            var res = req.CreateResponse(HttpStatusCode.OK);
            await res.WriteStringAsync("seeded");
            return res;
        }
    }
}