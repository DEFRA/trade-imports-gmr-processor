using System.Reflection;
using System.Text.Json;
using Amazon.SQS;
using Amazon.SQS.Model;
using Defra.TradeImportsGmrFinder.Domain.Events;
using GmrProcessor.Config;
using GmrProcessor.Consumers;
using GmrProcessor.Processors.Gto;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using TestFixtures;

namespace GmrProcessor.Tests.Consumers;

public class GtoMatchedGmrsQueueConsumerTests
{
    private readonly Mock<IGtoMatchedGmrProcessor> _processor = new();
    private readonly GtoMatchedGmrsQueueConsumer _consumer;

    public GtoMatchedGmrsQueueConsumerTests()
    {
        _processor
            .Setup(p => p.Process(It.IsAny<MatchedGmr>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(GtoMatchedGmrProcessorResult.NoHoldChange);
        _consumer = new GtoMatchedGmrsQueueConsumer(
            NullLogger<GtoMatchedGmrsQueueConsumer>.Instance,
            _processor.Object,
            Options.Create(new GtoMatchedGmrsQueueOptions { QueueName = "queue" }),
            new Mock<IAmazonSQS>().Object
        );
    }

    [Fact]
    public async Task ProcessMessageAsync_WhenReceivingAValidMessage_CallsProcessorWithMatchedGmr()
    {
        var matchedGmr = new MatchedGmr { Mrn = "MRN123", Gmr = GmrFixtures.GmrFixture().Create() };
        var message = new Message { Body = JsonSerializer.Serialize(matchedGmr) };

        await InvokeProcessMessageAsync(message, CancellationToken.None);

        _processor.Verify(
            p =>
                p.Process(
                    It.Is<MatchedGmr>(m => m.Mrn == matchedGmr.Mrn && m.Gmr.GmrId == matchedGmr.Gmr.GmrId),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task ProcessMessageAsync_WhenReceivingAnInvalidMessage_ThrowsJsonException()
    {
        var message = new Message { Body = "invalid" };

        var act = async () => await InvokeProcessMessageAsync(message, CancellationToken.None);

        await act.Should().ThrowAsync<JsonException>();
        _processor.Verify(p => p.Process(It.IsAny<MatchedGmr>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private Task InvokeProcessMessageAsync(Message message, CancellationToken cancellationToken)
    {
        var method = typeof(GtoMatchedGmrsQueueConsumer).GetMethod(
            "ProcessMessageAsync",
            BindingFlags.Instance | BindingFlags.NonPublic
        )!;

        return (Task)method.Invoke(_consumer, [message, cancellationToken])!;
    }
}
