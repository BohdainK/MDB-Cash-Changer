using FluentAssertions;
using MDBControllerLib;
using MDBControllerLib.Domain;

namespace MDBCashChanger.Tests;

public class MDBDeviceIntegrationTests
{
    [Fact]
    public void Constructor_WithNullSerial_ThrowsArgumentNullException()
    {
        // Act
        Action act = () => new MDBDevice(null!, CancellationToken.None);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("serial");
    }

    [Fact]
    public void Constructor_WithValidSerial_CreatesInstance()
    {
        // Arrange
        var serial = new SerialManager("COM_TEST", 115200, 500);

        // Act
        var device = new MDBDevice(serial, CancellationToken.None);

        // Assert
        device.Should().NotBeNull();
    }

    [Fact]
    public void ResetAllTubes_SetsCountsToZero()
    {
        // Arrange
        var serial = new SerialManager("COM_TEST", 115200, 500);
        var device = new MDBDevice(serial, CancellationToken.None);

        // Act
        device.ResetAllTubes();

        // Assert
        var summary = device.GetTubeSummary().ToList();
        summary.Should().AllSatisfy(tube =>
        {
            tube.Count.Should().Be(0);
            tube.Dispensable.Should().Be(0);
        });
    }

    [Fact]
    public void GetTubeSummary_ReturnsCoinTubeSummaryCollection()
    {
        // Arrange
        var serial = new SerialManager("COM_TEST", 115200, 500);
        var device = new MDBDevice(serial, CancellationToken.None);

        // Act
        var summary = device.GetTubeSummary();

        // Assert
        summary.Should().NotBeNull();
        summary.Should().AllBeOfType<CoinTubeSummary>();
    }

    [Fact]
    public void CoinTubes_Property_ReturnsEnumerableOfCoinTubes()
    {
        // Arrange
        var serial = new SerialManager("COM_TEST", 115200, 500);
        var device = new MDBDevice(serial, CancellationToken.None);

        // Act
        var tubes = device.CoinTubes;

        // Assert
        tubes.Should().NotBeNull();
        tubes.Should().AllBeOfType<CoinTube>();
    }

    [Fact]
    public void LastEvent_InitiallyNull()
    {
        // Arrange
        var serial = new SerialManager("COM_TEST", 115200, 500);
        var device = new MDBDevice(serial, CancellationToken.None);

        // Act
        var lastEvent = device.LastEvent;

        // Assert
        lastEvent.Should().BeNull();
    }

    [Fact]
    public void OnStateChanged_Event_CanBeSubscribed()
    {
        // Arrange
        var serial = new SerialManager("COM_TEST", 115200, 500);
        var device = new MDBDevice(serial, CancellationToken.None);
        var eventFired = false;

        // Act
        Action act = () => device.OnStateChanged += (message) => eventFired = true;

        // Assert
        act.Should().NotThrow();
    }
}

