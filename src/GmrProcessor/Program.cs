using System.Diagnostics.CodeAnalysis;
using FluentValidation;
using GmrProcessor.Config;
using GmrProcessor.Consumers;
using GmrProcessor.Extensions;
using GmrProcessor.Utils;
using GmrProcessor.Utils.Http;
using GmrProcessor.Utils.Logging;
using GmrProcessor.Utils.Mongo;
using Serilog;

var app = CreateWebApplication(args);
await app.RunAsync();
return;

[ExcludeFromCodeCoverage]
static WebApplication CreateWebApplication(string[] args)
{
    var builder = WebApplication.CreateBuilder(args);
    ConfigureBuilder(builder);

    var app = builder.Build();
    return SetupApplication(app);
}

[ExcludeFromCodeCoverage]
static void ConfigureBuilder(WebApplicationBuilder builder)
{
    builder.Configuration.AddEnvironmentVariables();

    builder.Services.AddCustomTrustStore();

    builder.Services.AddHttpContextAccessor();
    builder.Host.UseSerilog(CdpLogging.Configuration);

    builder.Services.AddHttpClient("DefaultClient").AddHeaderPropagation();

    builder.Services.AddTransient<ProxyHttpMessageHandler>();
    builder.Services.AddHttpClient("proxy").ConfigurePrimaryHttpMessageHandler<ProxyHttpMessageHandler>();

    builder.Services.AddOptions<LocalStackOptions>().Bind(builder.Configuration);
    builder.Services.AddValidateOptions<DataEventsQueueConsumerOptions>(DataEventsQueueConsumerOptions.SectionName);

    builder.Services.AddHeaderPropagation(options =>
    {
        var traceHeader = builder.Configuration.GetValue<string>("TraceHeader");
        if (!string.IsNullOrWhiteSpace(traceHeader))
        {
            options.Headers.Add(traceHeader);
        }
    });

    builder.Services.Configure<MongoConfig>(builder.Configuration.GetSection("Mongo"));
    builder.Services.AddSingleton<IMongoDbClientFactory, MongoDbClientFactory>();

    builder.Services.AddSqsClient();

    builder.Services.AddHostedService<DataEventsQueueConsumer>();

    builder.Services.AddHealthChecks();
    builder.Services.AddValidatorsFromAssemblyContaining<Program>();
}

[ExcludeFromCodeCoverage]
static WebApplication SetupApplication(WebApplication app)
{
    app.UseHeaderPropagation();
    app.UseRouting();
    app.MapHealthChecks("/health");

    return app;
}
