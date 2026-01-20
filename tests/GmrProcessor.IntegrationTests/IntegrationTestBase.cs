using System.Text.Json;
using Amazon.SQS;
using Amazon.SQS.Model;
using AutoFixture;
using Defra.TradeImportsDataApi.Domain.CustomsDeclaration;
using Defra.TradeImportsDataApi.Domain.Events;
using Defra.TradeImportsDataApi.Domain.Ipaffs;
using GmrProcessor.Config;
using GmrProcessor.Data;
using GmrProcessor.Extensions;
using GmrProcessor.Utils.Mongo;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TestFixtures;
using Environment = System.Environment;

namespace GmrProcessor.IntegrationTests;

[Trait("Category", "IntegrationTests")]
[Collection("Integration Tests")]
public abstract class IntegrationTestBase
{
    protected readonly IConfiguration Configuration;
    protected readonly ServiceProvider ServiceProvider;
    protected readonly IMongoContext Mongo;

    private readonly Dictionary<string, string> _environmentVariables = new()
    {
        { "AWS_ACCESS_KEY_ID", "test" },
        { "AWS_SECRET_ACCESS_KEY", "test" },
        { "AWS_REGION", "eu-west-2" },
        { "SNS_ENDPOINT", "http://localhost:4566" },
        { "SQS_ENDPOINT", "http://localhost:4566" },
        { "USE_LOCALSTACK", "true" },
    };

    protected IntegrationTestBase()
    {
        SetEnvironmentVariables();

        var projectRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/GmrProcessor"));
        Configuration = new ConfigurationBuilder()
            .SetBasePath(projectRoot)
            .AddJsonFile("appsettings.json", false)
            .AddJsonFile("appsettings.Development.json", false)
            .AddEnvironmentVariables()
            .Build();

        var sc = new ServiceCollection();
        sc.AddSingleton(Configuration);
        sc.AddOptions<LocalStackOptions>().Bind(Configuration);
        sc.AddValidateOptions<EtaMatchedGmrsQueueOptions>(EtaMatchedGmrsQueueOptions.SectionName);
        sc.AddValidateOptions<GtoDataEventsQueueConsumerOptions>(GtoDataEventsQueueConsumerOptions.SectionName);
        sc.AddValidateOptions<GtoMatchedGmrsQueueOptions>(GtoMatchedGmrsQueueOptions.SectionName);
        sc.AddValidateOptions<ImportMatchedGmrsQueueOptions>(ImportMatchedGmrsQueueOptions.SectionName);
        sc.AddValidateOptions<TradeImportsServiceBusOptions>(TradeImportsServiceBusOptions.SectionName);

        sc.AddValidateOptions<MongoConfig>(MongoConfig.SectionName);
        sc.AddSqsClient();

        sc.AddLogging(c => c.AddConsole());
        sc.Configure<MongoConfig>(Configuration.GetSection("Mongo"));
        sc.AddSingleton<IMongoDbClientFactory, MongoDbClientFactory>();
        sc.AddSingleton<IMongoContext, MongoContext>();

        ServiceProvider = sc.BuildServiceProvider();

        Mongo = ServiceProvider.GetRequiredService<IMongoContext>();
    }

    protected IMongoContext MongoContext => ServiceProvider.GetRequiredService<IMongoContext>();

    private void SetEnvironmentVariables()
    {
        foreach (var (key, value) in _environmentVariables)
        {
            if (Environment.GetEnvironmentVariable(key) != null)
                continue;

            Environment.SetEnvironmentVariable(key, value);
        }
    }

    protected async Task<(IAmazonSQS, string)> GetSqsClient(string queueName)
    {
        var sqsClient = ServiceProvider.GetRequiredService<IAmazonSQS>();
        return (sqsClient, (await sqsClient.GetQueueUrlAsync(queueName)).QueueUrl);
    }

    protected T GetConfig<T>()
        where T : class => ServiceProvider.GetRequiredService<IOptions<T>>().Value;

    protected static async Task WaitForMessageConsumed(IAmazonSQS sqsClient, string queueUrl)
    {
        var messageConsumed = await AsyncWaiter.WaitForAsync(
            async () =>
            {
                var result = await sqsClient.GetQueueAttributesAsync(
                    queueUrl,
                    ["ApproximateNumberOfMessages", "ApproximateNumberOfMessagesNotVisible"]
                );

                var numberMessagesOnQueue =
                    result.ApproximateNumberOfMessages + result.ApproximateNumberOfMessagesNotVisible;

                return numberMessagesOnQueue == 0 ? (int?)numberMessagesOnQueue : null;
            },
            TestContext.Current.CancellationToken
        );

        messageConsumed.Should().NotBeNull("Message was not consumed");
    }

    protected static async Task SendResourceEventMessageAsync<T>(IAmazonSQS sqsClient, string queueUrl, T resourceEvent)
        where T : class
    {
        var message = new SendMessageRequest
        {
            MessageBody = JsonSerializer.Serialize(resourceEvent),
            MessageAttributes = new Dictionary<string, MessageAttributeValue>
            {
                {
                    "ResourceType",
                    new MessageAttributeValue
                    {
                        DataType = "String",
                        StringValue = (resourceEvent as dynamic).ResourceType,
                    }
                },
            },
            QueueUrl = queueUrl,
        };

        await sqsClient.SendMessageAsync(message, TestContext.Current.CancellationToken);
    }

    protected async Task<ResourceEvent<ImportPreNotification>> SendImportPreNotificationAsync(
        string chedReference,
        string mrn,
        Action<ImportPreNotification>? configure = null,
        bool waitForConsumption = true
    )
    {
        var config = GetConfig<GtoDataEventsQueueConsumerOptions>();
        var (sqsClient, queueUrl) = await GetSqsClient(config.QueueName);

        var importPreNotification = ImportPreNotificationFixtures
            .ImportPreNotificationFixture(chedReference)
            .WithMrn(mrn)
            .With(x => x.Status, "SUBMITTED")
            .With(x => x.PartOne, new PartOne { ProvideCtcMrn = "YES" })
            .Create();

        configure?.Invoke(importPreNotification);

        var resourceEvent = ImportPreNotificationFixtures
            .ImportPreNotificationResourceEventFixture(importPreNotification)
            .With(r => r.ResourceId, chedReference)
            .Create();

        await SendResourceEventMessageAsync(sqsClient, queueUrl, resourceEvent);

        if (waitForConsumption)
        {
            await WaitForMessageConsumed(sqsClient, queueUrl);
        }

        return resourceEvent;
    }

    protected async Task<ResourceEvent<CustomsDeclaration>> SendCustomsDeclarationAsync(
        string mrn,
        IEnumerable<string> chedReferences,
        Action<CustomsDeclaration>? configure = null,
        bool waitForConsumption = true
    )
    {
        var config = GetConfig<GtoDataEventsQueueConsumerOptions>();
        var (sqsClient, queueUrl) = await GetSqsClient(config.QueueName);

        var chedReferencesList = chedReferences.ToList();
        var customsDeclaration = CustomsDeclarationFixtures.CustomsDeclarationFixture().Create();

        customsDeclaration.ClearanceDecision = CustomsDeclarationFixtures
            .ClearanceDecisionFixture(chedReferencesList)
            .Create();

        configure?.Invoke(customsDeclaration);

        var resourceEvent = CustomsDeclarationFixtures
            .CustomsDeclarationResourceEventFixture(customsDeclaration)
            .With(r => r.ResourceId, mrn)
            .Create();

        await SendResourceEventMessageAsync(sqsClient, queueUrl, resourceEvent);

        if (waitForConsumption)
        {
            await WaitForMessageConsumed(sqsClient, queueUrl);
        }

        return resourceEvent;
    }
}
