// using Azure.Cosmos;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.SemanticKernel;
using GraphRagText2Sql.Services;


var host = new HostBuilder()
.ConfigureAppConfiguration(cfg =>
{
cfg.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
cfg.AddEnvironmentVariables();
})
.ConfigureFunctionsWorkerDefaults()
.ConfigureServices((ctx, services) =>
{
var config = ctx.Configuration;


// Cosmos
services.AddSingleton(sp =>
{
var ep = config["Cosmos:Endpoint"]!;
var key = config["Cosmos:Key"]!;
var client = new CosmosClient(ep, key, new CosmosClientOptions
{
SerializerOptions = new CosmosSerializationOptions { PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase }
});
return client;
});


services.AddSingleton<CosmosGraphService>();
services.AddSingleton<SchemaSeeder>();


// Semantic Kernel + Azure OpenAI
services.AddSingleton(sp =>
{
var builder = Kernel.CreateBuilder();
var endpoint = config["OpenAI:Endpoint"]!;
var apiKey = config["OpenAI:ApiKey"]!;
var deployment = config["OpenAI:Deployment"]!;
builder.AddAzureOpenAIChatCompletion(deployment, endpoint, apiKey);
return builder.Build();
});


services.AddSingleton<SqlGeneratorService>();
services.AddSingleton<SummarizerService>();


// PostgreSQL
services.AddSingleton(sp => config["Postgres:ConnectionString"]!);
services.AddSingleton<SqlExecutorService>();
})
.Build();


host.Run();