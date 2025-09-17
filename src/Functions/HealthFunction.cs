using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;


namespace GraphRagText2Sql.Functions
{
    public sealed class HealthFunction
    {
        [Function("health")]
        public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "health")] HttpRequestData req)
        {
            var res = req.CreateResponse(HttpStatusCode.OK);
            await res.WriteStringAsync("ok");
            return res;
        }
    }
}