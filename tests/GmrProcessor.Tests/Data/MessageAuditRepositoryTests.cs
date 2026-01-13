using System.Linq.Expressions;
using GmrProcessor.Data;
using Microsoft.Extensions.Logging;
using Moq;

namespace GmrProcessor.Tests.Data;

public class MessageAuditRepositoryTests
{
    private readonly Mock<IMongoContext> _mongoContext = new();
    private readonly Mock<IMongoCollectionSet<MessageAudit>> _messageAudits = new();
    private readonly MessageAuditRepository _repository;

    public MessageAuditRepositoryTests()
    {
        _mongoContext.Setup(m => m.MessageAudits).Returns(_messageAudits.Object);
        _repository = new MessageAuditRepository(_mongoContext.Object);
    }

    [Fact]
    public async Task GetByMessageTypeAsync_FiltersCorrectly()
    {
        const string messageType = "GvmsHoldRequest";
        var fromTimestamp = DateTime.UtcNow.AddMinutes(-15);
        var expectedMessages = new List<MessageAudit>
        {
            new()
            {
                Id = "1",
                Direction = MessageDirection.Outbound,
                IntegrationType = IntegrationType.GvmsApi,
                Target = "GvmsApi",
                MessageBody = "{}",
                Timestamp = DateTime.UtcNow.AddMinutes(-5),
                MessageType = messageType,
            },
        };

        _messageAudits
            .Setup(m =>
                m.FindMany<MessageAudit>(
                    It.IsAny<Expression<Func<MessageAudit, bool>>>(),
                    It.IsAny<CancellationToken>(),
                    null,
                    null
                )
            )
            .ReturnsAsync(expectedMessages);

        var result = await _repository.GetByMessageTypeAsync(messageType, fromTimestamp, CancellationToken.None);

        result.Should().BeEquivalentTo(expectedMessages);
        _messageAudits.Verify(
            m =>
                m.FindMany<MessageAudit>(
                    It.Is<Expression<Func<MessageAudit, bool>>>(expr =>
                        VerifyFilterExpression(expr, messageType, fromTimestamp)
                    ),
                    CancellationToken.None,
                    null,
                    null
                ),
            Times.Once
        );
    }

    private static bool VerifyFilterExpression(
        Expression<Func<MessageAudit, bool>> expr,
        string messageType,
        DateTime fromTimestamp
    )
    {
        var compiled = expr.Compile();

        var matchingMessage = new MessageAudit
        {
            Id = "1",
            Direction = MessageDirection.Outbound,
            IntegrationType = IntegrationType.GvmsApi,
            Target = "test",
            MessageBody = "{}",
            Timestamp = fromTimestamp.AddMinutes(5),
            MessageType = messageType,
        };

        var wrongType = new MessageAudit
        {
            Id = "2",
            Direction = MessageDirection.Outbound,
            IntegrationType = IntegrationType.GvmsApi,
            Target = "test",
            MessageBody = "{}",
            Timestamp = fromTimestamp.AddMinutes(5),
            MessageType = "DifferentType",
        };

        var tooOld = new MessageAudit
        {
            Id = "3",
            Direction = MessageDirection.Outbound,
            IntegrationType = IntegrationType.GvmsApi,
            Target = "test",
            MessageBody = "{}",
            Timestamp = fromTimestamp.AddMinutes(-5),
            MessageType = messageType,
        };

        return compiled(matchingMessage) && !compiled(wrongType) && !compiled(tooOld);
    }
}
