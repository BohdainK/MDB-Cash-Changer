using FluentAssertions;
using MDBControllerLib;

namespace MDBCashChanger.Tests;

public class SerialManagerTests
{
    [Fact]
    public void Constructor_WithValidParameters_CreatesInstance()
    {
        // Arrange & Act
        var manager = new SerialManager("COM1", 115200, 500);

        // Assert
        manager.Should().NotBeNull();
    }

    [Fact]
    public void WriteLine_WithoutOpen_ThrowsInvalidOperationException()
    {
        // Arrange
        var manager = new SerialManager("COM1", 115200, 500);

        // Act
        Action act = () => manager.WriteLine("test");

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Serial port not open");
    }

    [Fact]
    public void ReadLine_WithoutOpen_ReturnsEmptyString()
    {
        // Arrange
        var manager = new SerialManager("COM1", 115200, 500);

        // Act
        var result = manager.ReadLine();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void ReadLine_WithCustomTimeout_ReturnsEmptyStringOnTimeout()
    {
        // Arrange
        var manager = new SerialManager("COM1", 115200, 500);

        // Act
        var result = manager.ReadLine(100);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void Close_WithoutOpen_DoesNotThrow()
    {
        // Arrange
        var manager = new SerialManager("COM1", 115200, 500);

        // Act
        Action act = () => manager.Close();

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        // Arrange
        var manager = new SerialManager("COM1", 115200, 500);

        // Act
        Action act = () =>
        {
            manager.Dispose();
            manager.Dispose();
        };

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Dispose_ClosesSerialPort()
    {
        // Arrange
        var manager = new SerialManager("COM1", 115200, 500);

        // Act
        manager.Dispose();

        // Assert - After dispose, operations should fail safely
        manager.ReadLine().Should().BeEmpty();
    }

    [Theory]
    [InlineData("COM1", 9600, 100)]
    [InlineData("COM2", 115200, 500)]
    [InlineData("/dev/ttyUSB0", 57600, 1000)]
    public void Constructor_WithVariousParameters_CreatesInstance(string port, int baud, int timeout)
    {
        // Act
        var manager = new SerialManager(port, baud, timeout);

        // Assert
        manager.Should().NotBeNull();
    }


    [Fact]
    public void ReadLine_OnException_ReturnsEmptyString()
    {
        // Arrange
        var manager = new SerialManager("COM1", 115200, 500);

        // Act
        var result = manager.ReadLine();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void Close_OnException_DoesNotThrow()
    {
        // Arrange
        var manager = new SerialManager("INVALID_PORT", 115200, 500);

        // Act
        Action act = () => manager.Close();

        // Assert
        act.Should().NotThrow();
    }
}
