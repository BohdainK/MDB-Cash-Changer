using FluentAssertions;
using MDBControllerLib.Domain;

namespace MDBCashChanger.Tests;

public class CoinTubeTests
{
    [Fact]
    public void Fullness_WhenCapacityIsZero_ReturnsZero()
    {
        // Arrange
        var tube = new CoinTube
        {
            Count = 10,
            Capacity = 0
        };

        // Act
        var fullness = tube.Fullness;

        // Assert
        fullness.Should().Be(0);
    }

    [Fact]
    public void Fullness_WhenCountIsZero_ReturnsZero()
    {
        // Arrange
        var tube = new CoinTube
        {
            Count = 0,
            Capacity = 50
        };

        // Act
        var fullness = tube.Fullness;

        // Assert
        fullness.Should().Be(0);
    }

    [Fact]
    public void Fullness_WhenHalfFull_ReturnsPointFive()
    {
        // Arrange
        var tube = new CoinTube
        {
            Count = 25,
            Capacity = 50
        };

        // Act
        var fullness = tube.Fullness;

        // Assert
        fullness.Should().Be(0.5);
    }

    [Fact]
    public void Fullness_WhenFull_ReturnsOne()
    {
        // Arrange
        var tube = new CoinTube
        {
            Count = 50,
            Capacity = 50
        };

        // Act
        var fullness = tube.Fullness;

        // Assert
        fullness.Should().Be(1.0);
    }

    [Fact]
    public void Fullness_WhenOverfilled_ReturnsGreaterThanOne()
    {
        // Arrange
        var tube = new CoinTube
        {
            Count = 60,
            Capacity = 50
        };

        // Act
        var fullness = tube.Fullness;

        // Assert
        fullness.Should().Be(1.2);
    }

    [Theory]
    [InlineData(0, 100, 0.0)]
    [InlineData(25, 100, 0.25)]
    [InlineData(50, 100, 0.5)]
    [InlineData(75, 100, 0.75)]
    [InlineData(100, 100, 1.0)]
    public void Fullness_VariousScenarios_CalculatesCorrectly(int count, int capacity, double expected)
    {
        // Arrange
        var tube = new CoinTube
        {
            Count = count,
            Capacity = capacity
        };

        // Act
        var fullness = tube.Fullness;

        // Assert
        fullness.Should().BeApproximately(expected, 0.001);
    }
}
