using FluentAssertions;
using MDBControllerLib;

namespace MDBCashChanger.Tests;

public class CoinRefundingManagerUnitTests
{
    [Fact]
    public void Constructor_InitializesWithDefaultValues()
    {
        // Arrange
        var serial = new SerialManager("COM_TEST", 115200, 500);
        var device = new MDBDevice(serial, CancellationToken.None);
        var coinTypeValues = new Dictionary<int, int>();

        // Act
        var manager = new CoinRefundingManager(device, coinTypeValues);

        // Assert
        manager.RequestedAmount.Should().Be(0);
        manager.InsertedAmount.Should().Be(0);
        manager.RemainingAmount.Should().Be(0);
        manager.IsRequestActive.Should().BeFalse();
    }

    [Fact]
    public void RequestAmount_WithZeroAmount_ThrowsArgumentException()
    {
        // Arrange
        var serial = new SerialManager("COM_TEST", 115200, 500);
        var device = new MDBDevice(serial, CancellationToken.None);
        var coinTypeValues = new Dictionary<int, int>();
        var manager = new CoinRefundingManager(device, coinTypeValues);

        // Act
        Action act = () => manager.RequestAmount(0);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("amountCents");
    }

    [Fact]
    public void RequestAmount_WithNegativeAmount_ThrowsArgumentException()
    {
        // Arrange
        var serial = new SerialManager("COM_TEST", 115200, 500);
        var device = new MDBDevice(serial, CancellationToken.None);
        var coinTypeValues = new Dictionary<int, int>();
        var manager = new CoinRefundingManager(device, coinTypeValues);

        // Act
        Action act = () => manager.RequestAmount(-50);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("amountCents");
    }

    [Fact]
    public void OnCoinInserted_WhenNotActive_DoesNotUpdateAmount()
    {
        // Arrange
        var serial = new SerialManager("COM_TEST", 115200, 500);
        var device = new MDBDevice(serial, CancellationToken.None);
        var coinTypeValues = new Dictionary<int, int> { { 1, 5 } };
        var manager = new CoinRefundingManager(device, coinTypeValues);

        // Act
        manager.OnCoinInserted(1);

        // Assert
        manager.InsertedAmount.Should().Be(0);
        manager.RemainingAmount.Should().Be(0);
    }

    [Fact]
    public void OnCoinInserted_WithUnknownCoinType_IsIgnored()
    {
        // Arrange
        var serial = new SerialManager("COM_TEST", 115200, 500);
        var device = new MDBDevice(serial, CancellationToken.None);
        var coinTypeValues = new Dictionary<int, int> { { 1, 5 } };
        var manager = new CoinRefundingManager(device, coinTypeValues);

        // Act
        manager.OnCoinInserted(99);

        // Assert
        manager.InsertedAmount.Should().Be(0);
    }

    [Fact]
    public void CancelRequest_WhenNotActive_DoesNotThrow()
    {
        // Arrange
        var serial = new SerialManager("COM_TEST", 115200, 500);
        var device = new MDBDevice(serial, CancellationToken.None);
        var coinTypeValues = new Dictionary<int, int>();
        var manager = new CoinRefundingManager(device, coinTypeValues);

        // Act
        Action act = () => manager.CancelRequest();

        // Assert
        act.Should().NotThrow();
        manager.IsRequestActive.Should().BeFalse();
    }

    [Fact]
    public void RefundAmount_WithZeroAmount_ReturnsTrue()
    {
        // Arrange
        var serial = new SerialManager("COM_TEST", 115200, 500);
        var device = new MDBDevice(serial, CancellationToken.None);
        var coinTypeValues = new Dictionary<int, int>();
        var manager = new CoinRefundingManager(device, coinTypeValues);

        // Act
        var result = manager.RefundAmount(0);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void RefundAmount_WithNegativeAmount_ReturnsTrue()
    {
        // Arrange
        var serial = new SerialManager("COM_TEST", 115200, 500);
        var device = new MDBDevice(serial, CancellationToken.None);
        var coinTypeValues = new Dictionary<int, int>();
        var manager = new CoinRefundingManager(device, coinTypeValues);

        // Act
        var result = manager.RefundAmount(-10);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void RefundAmount_WhenNoTubesAvailable_ReturnsFalse()
    {
        // Arrange
        var serial = new SerialManager("COM_TEST", 115200, 500);
        var device = new MDBDevice(serial, CancellationToken.None);
        var coinTypeValues = new Dictionary<int, int> { { 1, 5 } };
        var manager = new CoinRefundingManager(device, coinTypeValues);

        // Act
        var result = manager.RefundAmount(50);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void RemainingAmount_Property_CalculatesCorrectly()
    {
        // Arrange
        var serial = new SerialManager("COM_TEST", 115200, 500);
        var device = new MDBDevice(serial, CancellationToken.None);
        var coinTypeValues = new Dictionary<int, int>();
        var manager = new CoinRefundingManager(device, coinTypeValues);

        // Act & Assert
        manager.RemainingAmount.Should().Be(0);
    }

    [Fact]
    public void OnAmountStateChanged_Event_CanBeSubscribed()
    {
        // Arrange
        var serial = new SerialManager("COM_TEST", 115200, 500);
        var device = new MDBDevice(serial, CancellationToken.None);
        var coinTypeValues = new Dictionary<int, int>();
        var manager = new CoinRefundingManager(device, coinTypeValues);
        var eventFired = false;

        // Act
        Action act = () => manager.OnAmountStateChanged += (state) => eventFired = true;

        // Assert
        act.Should().NotThrow();
    }
}
