using System.Diagnostics.CodeAnalysis;
using Defra.TradeImportsDataApi.Api.Client;
using FluentValidation;
using GmrProcessor.Config;
using GmrProcessor.Consumers;
using GmrProcessor.Data;
using GmrProcessor.Data.Eta;
using GmrProcessor.Extensions;
using GmrProcessor.Processors.Eta;
using GmrProcessor.Processors.Gto;
using GmrProcessor.Processors.ImportGmrMatching;
using GmrProcessor.Services;
using GmrProcessor.Utils;
using GmrProcessor.Utils.Http;
using GmrProcessor.Utils.Logging;
using GmrProcessor.Utils.Mongo;
using MongoDB.Driver;
using MongoDB.Driver.Authentication.AWS;
using Serilog;
using GtoImportPreNotificationProcessor = GmrProcessor.Processors.Gto.GtoImportPreNotificationProcessor;
using IGtoImportPreNotificationProcessor = GmrProcessor.Processors.Gto.IGtoImportPreNotificationProcessor;

var app = CreateWebApplication(args);

// Ensure the database indices are initialized before starting the application.
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

    builder.Services.AddTransient<ProxyHttpMessageHandler>();
    builder.Services.AddHttpClient("proxy").ConfigurePrimaryHttpMessageHandler<ProxyHttpMessageHandler>();
    builder.Services.AddGvmsApiClient();

    builder
        .Services.AddOptions<DataApiOptions>()
        .BindConfiguration(DataApiOptions.SectionName)
        .ValidateDataAnnotations();

    builder.Services.AddDataApiHttpClient();

    builder.Services.AddOptions<LocalStackOptions>().Bind(builder.Configuration);
    builder.Services.AddValidateOptions<EtaMatchedGmrsQueueOptions>(EtaMatchedGmrsQueueOptions.SectionName);
    builder.Services.AddValidateOptions<GtoDataEventsQueueConsumerOptions>(
        GtoDataEventsQueueConsumerOptions.SectionName
    );
    builder.Services.AddValidateOptions<GtoMatchedGmrsQueueOptions>(GtoMatchedGmrsQueueOptions.SectionName);
    builder.Services.AddValidateOptions<ImportMatchedGmrsQueueOptions>(ImportMatchedGmrsQueueOptions.SectionName);

    builder.Services.AddHeaderPropagation(options =>
    {
        var traceHeader = builder.Configuration.GetValue<string>("TraceHeader");
        if (!string.IsNullOrWhiteSpace(traceHeader))
        {
            options.Headers.Add(traceHeader);
        }
    });

    MongoClientSettings.Extensions.AddAWSAuthentication();
    builder.Services.Configure<MongoConfig>(builder.Configuration.GetSection("Mongo"));
    builder.Services.AddSingleton<IMongoDbClientFactory, MongoDbClientFactory>();
    builder.Services.AddSingleton<IMongoContext, MongoContext>();
    builder.Services.AddSingleton<MongoDbInitializer>();

    builder.Services.AddSqsClient();

    builder.Services.AddSingleton<IEtaGmrCollection, EtaGmrCollection>();
    builder.Services.AddSingleton<IEtaMatchedGmrProcessor, EtaMatchedGmrProcessor>();

    builder.Services.AddSingleton<IGtoImportTransitRepository, GtoImportTransitRepository>();
    builder.Services.AddSingleton<IGtoMatchedGmrRepository, GtoMatchedGmrRepository>();
    builder.Services.AddSingleton<IGvmsApiClientService, GvmsApiClientService>();
    builder.Services.AddSingleton<IGtoImportPreNotificationProcessor, GtoImportPreNotificationProcessor>();
    builder.Services.AddSingleton<IGtoMatchedGmrProcessor, GtoMatchedGmrProcessor>();
    builder.Services.AddSingleton<IImportMatchedGmrsProcessor, ImportMatchedGmrsProcessor>();

    builder.Services.AddHostedService<EtaMatchedGmrsQueueConsumer>();
    builder.Services.AddHostedService<GtoDataEventsQueueConsumer>();
    builder.Services.AddHostedService<GtoMatchedGmrsQueueConsumer>();
    builder.Services.AddHostedService<ImportMatchedGmrsQueueConsumer>();

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
