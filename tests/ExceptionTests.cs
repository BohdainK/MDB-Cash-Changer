using FluentAssertions;
using MDBControllerLib;

namespace MDBCashChanger.Tests;

public class ExceptionTests
{
    [Fact]
    public void MDBDeviceException_DefaultConstructor_SetsTimestamp()
    {
        // Arrange
        var before = DateTime.UtcNow;

        // Act
        var exception = new MDBDeviceException();
        var after = DateTime.UtcNow;

        // Assert
        exception.Timestamp.Should().BeOnOrAfter(before);
        exception.Timestamp.Should().BeOnOrBefore(after);
    }

    [Fact]
    public void MDBDeviceException_WithMessage_SetsMessageAndTimestamp()
    {
        // Arrange
        var message = "Test error message";
        var before = DateTime.UtcNow;

        // Act
        var exception = new MDBDeviceException(message);
        var after = DateTime.UtcNow;

        // Assert
        exception.Message.Should().Be(message);
        exception.Timestamp.Should().BeOnOrAfter(before);
        exception.Timestamp.Should().BeOnOrBefore(after);
    }

    [Fact]
    public void MDBDeviceException_WithInnerException_SetsInnerExceptionAndTimestamp()
    {
        // Arrange
        var message = "Outer exception";
        var innerException = new InvalidOperationException("Inner exception");
        var before = DateTime.UtcNow;

        // Act
        var exception = new MDBDeviceException(message, innerException);
        var after = DateTime.UtcNow;

        // Assert
        exception.Message.Should().Be(message);
        exception.InnerException.Should().Be(innerException);
        exception.Timestamp.Should().BeOnOrAfter(before);
        exception.Timestamp.Should().BeOnOrBefore(after);
    }

    [Fact]
    public void SetupParseException_InheritsFromMDBDeviceException()
    {
        // Act
        var exception = new SetupParseException("Setup error");

        // Assert
        exception.Should().BeAssignableTo<MDBDeviceException>();
        exception.Message.Should().Be("Setup error");
        exception.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void PollLoopException_InheritsFromMDBDeviceException()
    {
        // Act
        var exception = new PollLoopException("Poll error");

        // Assert
        exception.Should().BeAssignableTo<MDBDeviceException>();
        exception.Message.Should().Be("Poll error");
        exception.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void TubeRefreshException_InheritsFromMDBDeviceException()
    {
        // Act
        var exception = new TubeRefreshException("Tube refresh error");

        // Assert
        exception.Should().BeAssignableTo<MDBDeviceException>();
        exception.Message.Should().Be("Tube refresh error");
        exception.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void CoinOperationException_InheritsFromMDBDeviceException()
    {
        // Act
        var exception = new CoinOperationException("Coin operation error");

        // Assert
        exception.Should().BeAssignableTo<MDBDeviceException>();
        exception.Message.Should().Be("Coin operation error");
        exception.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void AllExceptionTypes_WithInnerException_PreserveInnerException()
    {
        // Arrange
        var innerException = new InvalidOperationException("Inner");

        // Act
        var setupEx = new SetupParseException("Setup", innerException);
        var pollEx = new PollLoopException("Poll", innerException);
        var tubeEx = new TubeRefreshException("Tube", innerException);
        var coinEx = new CoinOperationException("Coin", innerException);

        // Assert
        setupEx.InnerException.Should().Be(innerException);
        pollEx.InnerException.Should().Be(innerException);
        tubeEx.InnerException.Should().Be(innerException);
        coinEx.InnerException.Should().Be(innerException);
    }
}
