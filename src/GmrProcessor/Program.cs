using System.Diagnostics.CodeAnalysis;
using FluentValidation;
using GmrProcessor.Config;
using GmrProcessor.Consumers;
using GmrProcessor.Data;
using GmrProcessor.Data.Auditing;
using GmrProcessor.Data.Eta;
using GmrProcessor.Data.Gto;
using GmrProcessor.Data.Matching;
using GmrProcessor.Endpoints;
using GmrProcessor.Extensions;
using GmrProcessor.Metrics;
using GmrProcessor.Processors.Eta;
using GmrProcessor.Processors.Gto;
using GmrProcessor.Processors.ImportGmrMatching;
using GmrProcessor.Services;
using GmrProcessor.Utils;
using GmrProcessor.Utils.Http;
using GmrProcessor.Utils.Logging;
using GmrProcessor.Utils.Mongo;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using MongoDB.Driver.Authentication.AWS;
using Serilog;
using GtoImportPreNotificationProcessor = GmrProcessor.Processors.Gto.GtoImportPreNotificationProcessor;
using IGtoImportPreNotificationProcessor = GmrProcessor.Processors.Gto.IGtoImportPreNotificationProcessor;

var app = CreateWebApplication(args);

using (var scope = app.Services.CreateScope())
{
    var initializer = scope.ServiceProvider.GetRequiredService<MongoDbInitializer>();
    await initializer.Init();
}

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

    builder.Services.AddHttpProxyClient();

    builder.Services.AddOptions<CdpOptions>().Bind(builder.Configuration);
    builder.Services.AddOptions<LocalStackOptions>().Bind(builder.Configuration);
    builder.Services.AddOptions<FeatureOptions>().Bind(builder.Configuration);

    builder.Services.AddValidateOptions<EtaMatchedGmrsQueueOptions>(EtaMatchedGmrsQueueOptions.SectionName);
    builder.Services.AddValidateOptions<GtoDataEventsQueueConsumerOptions>(
        GtoDataEventsQueueConsumerOptions.SectionName
    );
    builder.Services.AddValidateOptions<GtoMatchedGmrsQueueOptions>(GtoMatchedGmrsQueueOptions.SectionName);
    builder.Services.AddValidateOptions<ImportMatchedGmrsQueueOptions>(ImportMatchedGmrsQueueOptions.SectionName);
    builder.Services.AddValidateOptions<TradeImportsServiceBusOptions>(TradeImportsServiceBusOptions.SectionName);

    builder.Services.AddHeaderPropagation(options =>
    {
        var traceHeader = builder.Configuration.GetValue<string>("TraceHeader");
        if (!string.IsNullOrWhiteSpace(traceHeader))
        {
            options.Headers.Add(traceHeader);
        }
    });

    builder.Services.AddTradeImportsMessaging(builder.Configuration);

    MongoClientSettings.Extensions.AddAWSAuthentication();
    builder.Services.Configure<MongoConfig>(builder.Configuration.GetSection("Mongo"));
    builder.Services.AddSingleton<IMongoDbClientFactory, MongoDbClientFactory>();
    builder.Services.AddSingleton<IMongoContext, MongoContext>();
    builder.Services.AddSingleton<MongoDbInitializer>();

    builder.Services.AddSqsClient();

    builder.Services.AddGvmsApiClient();
    builder.Services.AddSingleton<IGvmsApiMetrics, GvmsApiMetrics>();
    builder.Services.AddSingleton<IGvmsHoldService, GvmsHoldService>();

    builder.Services.AddSingleton<IEtaGmrCollection, EtaGmrCollection>();
    builder.Services.AddSingleton<IEtaMatchedGmrProcessor, EtaMatchedGmrProcessor>();

    builder.Services.AddSingleton<IGtoGmrCollection, GtoGmrCollection>();
    builder.Services.AddSingleton<IGtoImportTransitCollection, GtoImportTransitCollection>();
    builder.Services.AddSingleton<IGtoMatchedGmrCollection, GtoMatchedGmrCollection>();

    builder.Services.AddSingleton<IGtoImportPreNotificationProcessor, GtoImportPreNotificationProcessor>();
    builder.Services.AddSingleton<IMrnChedMatchProcessor, MrnChedMatchProcessor>();
    builder.Services.AddSingleton<IGtoMatchedGmrProcessor, GtoMatchedGmrProcessor>();
    builder.Services.AddSingleton<IImportMatchedGmrsProcessor, ImportMatchedGmrsProcessor>();

    var featureOptions = builder.Configuration.Get<FeatureOptions>() ?? new FeatureOptions();
    if (featureOptions.EnableSqsConsumers)
    {
        builder.Services.AddHostedService<EtaMatchedGmrsQueueConsumer>();
        builder.Services.AddHostedService<DataEventsQueueConsumer>();
        builder.Services.AddHostedService<GtoMatchedGmrsQueueConsumer>();
        builder.Services.AddHostedService<ImportMatchedGmrsQueueConsumer>();
    }

    builder.Services.AddSingleton<ConsumerMetrics>();

    builder.Services.AddHealthChecks();
    builder.Services.AddValidatorsFromAssemblyContaining<Program>();
    builder.Services.AddSingleton<IMessageAuditRepository, MessageAuditRepository>();
    builder.Services.AddSingleton<IMatchReferenceRepository, MatchReferenceRepository>();
}

[ExcludeFromCodeCoverage]
static WebApplication SetupApplication(WebApplication app)
{
    app.UseHeaderPropagation();
    app.UseRouting();
    app.MapHealthChecks("/health");

    var featureOptions = app.Services.GetRequiredService<IOptions<FeatureOptions>>().Value;
    if (featureOptions.EnableDevEndpoints)
    {
        app.MapMessageEndpoints();
    }

    app.UseEmfExporter(app.Environment.ApplicationName);

    if (!featureOptions.EnableSqsConsumers)
    {
        Log.Warning("SQS consumers are disabled");
    }

    return app;
}
