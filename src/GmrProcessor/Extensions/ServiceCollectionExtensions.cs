using System.Diagnostics.CodeAnalysis;
using System.Net;
using Amazon;
using Amazon.Runtime;
using Amazon.SQS;
using Azure.Messaging.ServiceBus;
using Defra.TradeImportsDataApi.Api.Client;
using Defra.TradeImportsGmrFinder.GvmsClient.Client;
using GmrProcessor.Config;
using GmrProcessor.Metrics;
using GmrProcessor.Services;
using GmrProcessor.Utils.Http;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using Polly;

namespace GmrProcessor.Extensions;

[ExcludeFromCodeCoverage]
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTradeImportsMessaging(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        var featureOptions = new FeatureOptions();
        configuration.Bind(featureOptions);

        if (featureOptions.EnableTradeImportsMessaging)
        {
            var serviceBusOptions = configuration
                .GetRequiredSection(TradeImportsServiceBusOptions.SectionName)
                .Get<TradeImportsServiceBusOptions>()!;

            services.AddTradeImportsServiceBus(serviceBusOptions);

            services.AddSingleton<TradeImportsServiceBus>();

            if (featureOptions.EnableStoreOutboundMessages)
            {
                services.AddSingleton<ITradeImportsServiceBus>(sp =>
                {
                    var tradeImportsServiceBus = sp.GetRequiredService<TradeImportsServiceBus>();
                    var mongoContext = sp.GetRequiredService<Data.IMongoContext>();
                    var logger = sp.GetRequiredService<ILogger<TradeImportsServiceBusWithStorage>>();
                    return new TradeImportsServiceBusWithStorage(tradeImportsServiceBus, mongoContext, logger);
                });
            }
            else
            {
                services.AddSingleton<ITradeImportsServiceBus>(sp => sp.GetRequiredService<TradeImportsServiceBus>());
            }
        }
        else
        {
            services.AddSingleton<ITradeImportsServiceBus, StubTradeImportsServiceBus>();
        }

        return services;
    }

    public static IServiceCollection AddTradeImportsServiceBus(
        this IServiceCollection services,
        TradeImportsServiceBusOptions tradeImportsServiceBusOptions
    )
    {
        services.AddAzureClients(azureBuilder =>
        {
            var serviceBusClientBuilder = azureBuilder.AddServiceBusClient(
                tradeImportsServiceBusOptions.ConnectionString
            );
            serviceBusClientBuilder.ConfigureOptions(
                (options, provider) =>
                {
                    if (!provider.GetRequiredService<IOptions<CdpOptions>>().Value.IsProxyEnabled)
                        return;

                    options.TransportType = ServiceBusTransportType.AmqpWebSockets;
                    options.WebProxy = provider.GetRequiredService<IWebProxy>();
                }
            );

            string[] queueNames =
            [
                tradeImportsServiceBusOptions.EtaQueueName,
                tradeImportsServiceBusOptions.ImportMatchResultQueueName,
            ];
            foreach (var queueName in queueNames)
            {
                azureBuilder
                    .AddClient<ServiceBusSender, ServiceBusClientOptions>(
                        (_, _, provider) => provider.GetRequiredService<ServiceBusClient>().CreateSender(queueName)!
                    )
                    .WithName(queueName);
            }
        });
        return services;
    }

    public static IServiceCollection AddGvmsApiClientService(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        var featureOptions = new FeatureOptions();
        configuration.Bind(featureOptions);

        services.AddSingleton<GvmsApiClientService>();
        services.AddSingleton<GvmsApiMetrics>();

        services.AddSingleton<IGvmsApiClientService>(sp =>
        {
            IGvmsApiClientService service = sp.GetRequiredService<GvmsApiClientService>();

            var metrics = sp.GetRequiredService<GvmsApiMetrics>();
            service = new GvmsApiClientServiceWithMetrics(service, metrics);

            if (!featureOptions.EnableStoreOutboundMessages)
                return service;

            var mongoContext = sp.GetRequiredService<Data.IMongoContext>();
            var storageLogger = sp.GetRequiredService<ILogger<GvmsApiClientServiceWithStorage>>();
            service = new GvmsApiClientServiceWithStorage(service, mongoContext, storageLogger);

            return service;
        });

        return services;
    }

    public static IServiceCollection AddDataApiHttpClient(this IServiceCollection services)
    {
        var resilienceOptions = new HttpStandardResilienceOptions { Retry = { UseJitter = true } };
        resilienceOptions.Retry.DisableForUnsafeHttpMethods();

        services
            .AddTradeImportsDataApiClient()
            .ConfigureHttpClient(
                (sp, c) =>
                {
                    sp.GetRequiredService<IOptions<DataApiOptions>>().Value.Configure(c);

                    // Disable the HttpClient timeout to allow the resilient pipeline below
                    // to handle all timeouts
                    c.Timeout = Timeout.InfiniteTimeSpan;
                }
            )
            .AddResilienceHandler(
                "DataApi",
                builder =>
                {
                    builder
                        .AddTimeout(resilienceOptions.TotalRequestTimeout)
                        .AddRetry(resilienceOptions.Retry)
                        .AddTimeout(resilienceOptions.AttemptTimeout);
                }
            );

        return services;
    }

    public static IServiceCollection AddSqsClient(this IServiceCollection services) =>
        services.AddSingleton<IAmazonSQS>(sp =>
        {
            var localStackOptions = sp.GetRequiredService<IOptions<LocalStackOptions>>().Value;
            if (localStackOptions.UseLocalStack == false)
            {
                return new AmazonSQSClient();
            }

            return new AmazonSQSClient(
                new BasicAWSCredentials(localStackOptions.AccessKeyId, localStackOptions.SecretAccessKey),
                new AmazonSQSConfig
                {
                    // https://github.com/aws/aws-sdk-net/issues/1781
                    AuthenticationRegion = localStackOptions.AwsRegion ?? RegionEndpoint.EUWest2.ToString(),
                    RegionEndpoint = RegionEndpoint.GetBySystemName(
                        localStackOptions.AwsRegion ?? RegionEndpoint.EUWest2.ToString()
                    ),
                    ServiceURL = localStackOptions.SqsEndpoint,
                }
            );
        });

    public static IServiceCollection AddGvmsApiClient(this IServiceCollection services)
    {
        services
            .AddValidateOptions<GmrProcessorGvmsApiOptions>(GmrProcessorGvmsApiOptions.SectionName)
            .Validate(
                apiOptions =>
                {
                    var baseUri = apiOptions.BaseUri;
                    return Uri.TryCreate(baseUri, UriKind.Absolute, out _) && baseUri.EndsWith('/');
                },
                "BaseUri must be a valid absolute URI with trailing slash"
            );

        services.AddValidateOptions<GvmsApiOptions>(GmrProcessorGvmsApiOptions.SectionName);

        services
            .AddMemoryCache()
            .AddHttpClient<IGvmsApiClient, GvmsApiClient>()
            .ConfigureHttpClient(
                (sp, c) =>
                {
                    var settings = sp.GetRequiredService<IOptions<GmrProcessorGvmsApiOptions>>().Value;
                    c.BaseAddress = new Uri(settings.BaseUri);
                }
            )
            .ConfigurePrimaryHttpMessageHandler(Proxy.ConfigurePrimaryHttpMessageHandler)
            .AddResilienceHandler(
                "GvmsApi",
                (pipelineBuilder, context) =>
                {
                    var gvmsApiSettings = context.GetOptions<GmrProcessorGvmsApiOptions>();

                    pipelineBuilder
                        .AddRetry(gvmsApiSettings.Retry)
                        .AddTimeout(gvmsApiSettings.Timeout)
                        .AddCircuitBreaker(gvmsApiSettings.CircuitBreaker);
                }
            );

        return services;
    }
}
