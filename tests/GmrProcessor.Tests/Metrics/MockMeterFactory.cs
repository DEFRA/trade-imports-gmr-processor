using System.Diagnostics.Metrics;
using Moq;

namespace GmrProcessor.Tests.Metrics;

internal static class MockMeterFactory
{
    public static IMeterFactory Create()
    {
        var meterFactory = new Mock<IMeterFactory>();
        meterFactory.Setup(x => x.Create(It.IsAny<MeterOptions>())).Returns(new Meter("test"));
        return meterFactory.Object;
    }
}
