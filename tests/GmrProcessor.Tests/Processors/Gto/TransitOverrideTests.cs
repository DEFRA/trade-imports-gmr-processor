using Defra.TradeImportsDataApi.Domain.Ipaffs;
using FluentAssertions;
using GmrProcessor.Processors.Gto;

namespace GmrProcessor.Tests.Processors.Gto;

public class TransitOverrideTests
{
    [Theory]
    [InlineData("REJECTED")]
    [InlineData("PARTIALLY_REJECTED")]
    [InlineData("VALIDATED")]
    public void IsTransitOverrideRequired_WhenImportStatusIsComplete_ReturnsNotRequired(string status)
    {
        var importPreNotification = new ImportPreNotification
        {
            Status = status,
            PartTwo = new PartTwo { InspectionRequired = "Required" },
        };

        var result = TransitOverride.IsTransitOverrideRequired(importPreNotification);

        result.IsOverrideRequired.Should().BeFalse();
        result.Reason.Should().Be($"Import status is complete : '{status}'");
    }

    [Theory]
    [InlineData("Required")]
    [InlineData("Inconclusive")]
    public void IsTransitOverrideRequired_WhenInspectionRequiredAndImportNotComplete_ReturnsRequired(
        string inspectionRequired
    )
    {
        var importPreNotification = new ImportPreNotification
        {
            Status = "SUBMITTED",
            PartTwo = new PartTwo { InspectionRequired = inspectionRequired },
        };

        var result = TransitOverride.IsTransitOverrideRequired(importPreNotification);

        result.IsOverrideRequired.Should().BeTrue();
        result.Reason.Should().Be("Transit Override Required");
    }

    [Fact]
    public void IsTransitOverrideRequired_WhenInspectionNotRequired_ReturnsNotRequired()
    {
        var importPreNotification = new ImportPreNotification
        {
            Status = "SUBMITTED",
            PartTwo = new PartTwo { InspectionRequired = "Not Required" },
        };

        var result = TransitOverride.IsTransitOverrideRequired(importPreNotification);

        result.IsOverrideRequired.Should().BeFalse();
        result.Reason.Should().Be("Inspection is not required : 'Not Required'");
    }

    [Fact]
    public void IsTransitOverrideRequired_WhenCompleteStatusTakesPrecedenceOverInspectionRequired_ReturnsNotRequired()
    {
        var importPreNotification = new ImportPreNotification
        {
            Status = "Validated",
            PartTwo = new PartTwo { InspectionRequired = "Required" },
        };

        var result = TransitOverride.IsTransitOverrideRequired(importPreNotification);

        result.IsOverrideRequired.Should().BeFalse();
        result.Reason.Should().Be("Import status is complete : 'Validated'");
    }

    [Fact]
    public void IsTransitOverrideRequired_WhenPartTwoIsNull_ReturnsNotRequired()
    {
        var importPreNotification = new ImportPreNotification { Status = "SUBMITTED", PartTwo = null };

        var result = TransitOverride.IsTransitOverrideRequired(importPreNotification);

        result.IsOverrideRequired.Should().BeFalse();
        result.Reason.Should().Be("Inspection is not required : ''");
    }

    [Fact]
    public void IsTransitOverrideRequired_WhenInspectionRequiredIsNull_ReturnsNotRequired()
    {
        var importPreNotification = new ImportPreNotification
        {
            Status = "SUBMITTED",
            PartTwo = new PartTwo { InspectionRequired = null },
        };

        var result = TransitOverride.IsTransitOverrideRequired(importPreNotification);

        result.IsOverrideRequired.Should().BeFalse();
        result.Reason.Should().Be("Inspection is not required : ''");
    }
}
