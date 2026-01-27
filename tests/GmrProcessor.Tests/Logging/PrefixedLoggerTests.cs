using GmrProcessor.Logging;
using Microsoft.Extensions.Logging;
using Moq;

namespace GmrProcessor.Tests.Logging;

public class PrefixedLoggerTests
{
    [Fact]
    public void Log_PrependsPrefix()
    {
        var mockInnerLogger = new Mock<ILogger<PrefixedLoggerTests>>();
        var logger = new PrefixedLogger<PrefixedLoggerTests>(mockInnerLogger.Object, "TEST");

        logger.Log(LogLevel.Information, default, "message", null, (s, _) => s);

        mockInnerLogger.Verify(
            l =>
                l.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((state, _) => state.ToString() == "TEST: message"),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()
                ),
            Times.Once
        );
    }
}
