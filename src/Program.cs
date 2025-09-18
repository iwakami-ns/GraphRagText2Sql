using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging; // ★ 追加
using Microsoft.SemanticKernel;
using GraphRagText2Sql.Services;
using Azure;


var host = new HostBuilder()
    .ConfigureAppConfiguration((ctx, cfg) =>
    {
        cfg.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
        cfg.AddJsonFile($"appsettings.{ctx.HostingEnvironment.EnvironmentName}.json", optional: true, reloadOnChange: true);
        cfg.AddEnvironmentVariables();
    })
    // ★ ログ設定を追加（Functions 既定のプロバイダは残す）
    .ConfigureLogging((ctx, logging) =>
    {
        logging.AddConsole(); // ローカル実行/Functions Core Tools で見える
        logging.AddDebug();   // VS の Output ウィンドウ

        // 全体の既定レベル
        logging.SetMinimumLevel(LogLevel.Information);

        // ノイズ抑制＋必要なカテゴリだけ詳細化
        logging.AddFilter("Microsoft", LogLevel.Warning);
        logging.AddFilter("System", LogLevel.Warning);
        logging.AddFilter("GraphRagText2Sql.Services.SqlGeneratorService", LogLevel.Trace);
        logging.AddFilter("Microsoft.SemanticKernel", LogLevel.Debug);
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
            builder.AddAzureOpenAIChatCompletion(
                deployment,
                endpoint,
                apiKey
            );
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