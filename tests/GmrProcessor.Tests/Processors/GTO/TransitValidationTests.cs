using Defra.TradeImportsDataApi.Domain.Ipaffs;
using FluentAssertions;
using GmrProcessor.Processors.GTO;

namespace GmrProcessor.Tests.Processors.GTO;

public class TransitValidationTests
{
    [Theory]
    [InlineData("NO")]
    public void IsTransit_WhenProvideCtcMrnIsNo_ReturnsNotTransit(string provideCtcMrn)
    {
        var importPreNotification = new ImportPreNotification
        {
            PartOne = new PartOne { ProvideCtcMrn = provideCtcMrn },
        };

        var result = TransitValidation.IsTransit(importPreNotification);

        result.IsTransit.Should().BeFalse();
        result.Reason.Should().Be("");
    }

    [Theory]
    [InlineData("YES_ADD_LATER")]
    [InlineData("yes_add_later")]
    public void IsTransit_WhenProvideCtcMrnIsYesAddLater_ReturnsNotTransit(string provideCtcMrn)
    {
        var importPreNotification = new ImportPreNotification
        {
            PartOne = new PartOne { ProvideCtcMrn = provideCtcMrn },
        };

        var result = TransitValidation.IsTransit(importPreNotification);

        result.IsTransit.Should().BeFalse();
        result.Reason.Should().Be("CTC - Reference not provided yet");
    }

    [Theory]
    [InlineData("24GB12345678901234")]
    [InlineData("25FR98765432109876")]
    [InlineData("23DE11111111111111")]
    public void IsTransit_WhenProvideCtcMrnIsYesAndValidNctsReference_ReturnsTransit(string nctsReference)
    {
        var importPreNotification = new ImportPreNotification
        {
            PartOne = new PartOne { ProvideCtcMrn = "YES" },
            ExternalReferences = [new ExternalReference { System = "NCTS", Reference = nctsReference }],
        };

        var result = TransitValidation.IsTransit(importPreNotification);

        result.IsTransit.Should().BeTrue();
        result.Mrn.Should().Be(nctsReference.ToUpper());
        result.Reason.Should().Be(string.Empty);
    }

    [Theory]
    [InlineData("YES")]
    [InlineData("yes")]
    public void IsTransit_WhenProvideCtcMrnCaseInsensitiveAndValidNctsReference_ReturnsTransit(string provideCtcMrn)
    {
        var importPreNotification = new ImportPreNotification
        {
            PartOne = new PartOne { ProvideCtcMrn = provideCtcMrn },
            ExternalReferences = [new ExternalReference { System = "NCTS", Reference = "24GB12345678901234" }],
        };

        var result = TransitValidation.IsTransit(importPreNotification);

        result.IsTransit.Should().BeTrue();
        result.Mrn.Should().Be("24GB12345678901234");
        result.Reason.Should().Be(string.Empty);
    }

    [Theory]
    [InlineData("NCTS")]
    [InlineData("ncts")]
    public void IsTransit_WhenNctsSystemIsCaseInsensitive_ReturnsTransit(string systemName)
    {
        var importPreNotification = new ImportPreNotification
        {
            PartOne = new PartOne { ProvideCtcMrn = "YES" },
            ExternalReferences = [new ExternalReference { System = systemName, Reference = "24GB12345678901234" }],
        };

        var result = TransitValidation.IsTransit(importPreNotification);

        result.IsTransit.Should().BeTrue();
        result.Mrn.Should().Be("24GB12345678901234");
        result.Reason.Should().Be(string.Empty);
    }

    [Theory]
    [InlineData("INVALID")]
    [InlineData("24GB123456789012")]
    [InlineData("24GB12345678901234567")]
    [InlineData("24G12345678901234")]
    [InlineData("2GB12345678901234")]
    [InlineData("")]
    [InlineData("   ")]
    public void IsTransit_WhenProvideCtcMrnIsYesAndInvalidNctsReference_ReturnsNotTransit(string nctsReference)
    {
        var importPreNotification = new ImportPreNotification
        {
            PartOne = new PartOne { ProvideCtcMrn = "YES" },
            ExternalReferences = [new ExternalReference { System = "NCTS", Reference = nctsReference }],
        };

        var result = TransitValidation.IsTransit(importPreNotification);

        result.IsTransit.Should().BeFalse();
        result.Reason.Should().Contain("CTC");
    }

    [Fact]
    public void IsTransit_WhenProvideCtcMrnIsYesAndNoNctsReference_ReturnsNotTransit()
    {
        var importPreNotification = new ImportPreNotification
        {
            PartOne = new PartOne { ProvideCtcMrn = "YES" },
            ExternalReferences = [],
        };

        var result = TransitValidation.IsTransit(importPreNotification);

        result.IsTransit.Should().BeFalse();
        result.Reason.Should().Be("CTC - Empty NCTS reference");
    }

    [Fact]
    public void IsTransit_WhenProvideCtcMrnIsYesAndNctsReferenceIsNull_ReturnsNotTransit()
    {
        var importPreNotification = new ImportPreNotification
        {
            PartOne = new PartOne { ProvideCtcMrn = "YES" },
            ExternalReferences = [new ExternalReference { System = "NCTS", Reference = null }],
        };

        var result = TransitValidation.IsTransit(importPreNotification);

        result.IsTransit.Should().BeFalse();
        result.Reason.Should().Be("CTC - Empty NCTS reference");
    }

    [Fact]
    public void IsTransit_WhenProvideCtcMrnIsYesAndWrongSystem_ReturnsNotTransit()
    {
        var importPreNotification = new ImportPreNotification
        {
            PartOne = new PartOne { ProvideCtcMrn = "YES" },
            ExternalReferences = [new ExternalReference { System = "REFX", Reference = "24GB12345678901234" }],
        };

        var result = TransitValidation.IsTransit(importPreNotification);

        result.IsTransit.Should().BeFalse();
        result.Reason.Should().Be("CTC - Empty NCTS reference");
    }

    [Fact]
    public void IsTransit_WhenProvideCtcMrnIsInvalid_ReturnsNotTransitWithReason()
    {
        var importPreNotification = new ImportPreNotification { PartOne = new PartOne { ProvideCtcMrn = "INVALID" } };

        var result = TransitValidation.IsTransit(importPreNotification);

        result.IsTransit.Should().BeFalse();
        result.Reason.Should().StartWith("Invalid CTC indicator:");
    }

    [Fact]
    public void IsTransit_WhenPartOneIsNull_ReturnsNotTransit()
    {
        var importPreNotification = new ImportPreNotification { PartOne = null };

        var result = TransitValidation.IsTransit(importPreNotification);

        result.IsTransit.Should().BeFalse();
        result.Reason.Should().StartWith("Invalid CTC indicator:");
    }

    [Fact]
    public void IsTransit_WhenProvideCtcMrnIsNull_ReturnsNotTransit()
    {
        var importPreNotification = new ImportPreNotification { PartOne = new PartOne { ProvideCtcMrn = null } };

        var result = TransitValidation.IsTransit(importPreNotification);

        result.IsTransit.Should().BeFalse();
        result.Reason.Should().StartWith("Invalid CTC indicator:");
    }

    [Theory]
    [InlineData("  24gb12345678901234  ", "24GB12345678901234")]
    [InlineData("24gb12345678901234", "24GB12345678901234")]
    public void IsTransit_WhenNctsReferenceHasWhitespaceOrLowercase_NormalizesCorrectly(
        string nctsReference,
        string expectedMrn
    )
    {
        var importPreNotification = new ImportPreNotification
        {
            PartOne = new PartOne { ProvideCtcMrn = "YES" },
            ExternalReferences = [new ExternalReference { System = "NCTS", Reference = nctsReference }],
        };

        var result = TransitValidation.IsTransit(importPreNotification);

        result.IsTransit.Should().BeTrue();
        result.Mrn.Should().Be(expectedMrn);
        result.Reason.Should().Be(string.Empty);
    }
}
