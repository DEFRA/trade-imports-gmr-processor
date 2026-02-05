using System.Diagnostics.CodeAnalysis;
using System.Net;
using Amazon;
using Amazon.Runtime;
using Amazon.SQS;
using Azure.Messaging.ServiceBus;
using Defra.TradeImportsGmrFinder.GvmsClient.Client;
using GmrProcessor.Config;
using GmrProcessor.Services;
using GmrProcessor.Utils.Http;
using Microsoft.Extensions.Azure;
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
            ServiceBusQueue[] queues =
            [
                tradeImportsServiceBusOptions.Eta,
                tradeImportsServiceBusOptions.ImportMatchResult,
            ];
            foreach (var queue in queues)
            {
                azureBuilder
                    .AddServiceBusClient(queue.ConnectionString)
                    .WithName(queue.QueueName)
                    .ConfigureOptions(
                        (options, provider) =>
                        {
                            if (provider.GetRequiredService<IOptions<CdpOptions>>().Value.IsProxyEnabled)
                            {
                                options.TransportType = ServiceBusTransportType.AmqpWebSockets;
                                options.WebProxy = provider.GetRequiredService<IWebProxy>();
                            }
                        }
                    );

                azureBuilder
                    .AddClient<ServiceBusSender, ServiceBusClientOptions>(
                        (_, _, provider) =>
                        {
                            var clientFactory = provider.GetRequiredService<IAzureClientFactory<ServiceBusClient>>();
                            var client = clientFactory.CreateClient(queue.QueueName);
                            return client.CreateSender(queue.QueueName);
                        }
                    )
                    .WithName(queue.QueueName);
            }
        });
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
