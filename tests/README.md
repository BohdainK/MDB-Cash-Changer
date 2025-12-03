# MDB Cash Changer - Test Suite

## Test Coverage

### Test Summary
- **Total Tests**: 48
- **Status**: Pass

### Test Breakdown

#### 1. CoinTube Tests (10 tests)
Tests for the `CoinTube` domain model and calculated properties:
- `Fullness` property calculations
- Edge cases (zero capacity, overfilled, etc.)
- Various fullness scenarios

**File**: [CoinTubeTests.cs](./CoinTubeTests.cs)

#### 2. Exception Tests (7 tests)
Tests for custom exception classes:
- `MDBDeviceException` timestamp tracking
- Inheritance hierarchy (`SetupParseException`, `PollLoopException`, `TubeRefreshException`, `CoinOperationException`)
- Exception message and inner exception handling

**File**: [ExceptionTests.cs](./ExceptionTests.cs)

#### 3. SerialManager Tests (10 tests)
Tests for serial port management:
- Constructor validation
- Error handling when port is closed
- Timeout handling

**File**: [SerialManagerTests.cs](./SerialManagerTests.cs)

#### 4. MDBDevice Integration Tests (7 tests)
Integration tests for the main device controller:
- Constructor validation
- Tube management (reset, summary retrieval)
- Property access
- Event subscription

**File**: [MDBDeviceIntegrationTests.cs](./MDBDeviceIntegrationTests.cs)

#### 5. CoinRefundingManager Unit Tests (14 tests)
Unit tests for coin refund management business logic:
- Request amount validation
- Coin insertion tracking when not active
- Coin dispensing logic
- Refund calculations
- Event handling
- Amount tracking properties

**File**: [CoinRefundingManagerUnitTests.cs](./CoinRefundingManagerUnitTests.cs)

## Running the Tests

```bash
dotnet test
```

## Test Frameworks & Libraries

- **xUnit**: Test framework
- **FluentAssertions**: Assertion library for readable tests
- **Moq**: Mocking library (configured for internal classes)

## Key Testing Patterns

### 1. Arrange-Act-Assert (AAA)
All tests follow the AAA pattern for clarity:
```csharp
[Fact]
public void Fullness_WhenHalfFull_ReturnsPointFive()
{
    // Arrange
    var tube = new CoinTube { Count = 25, Capacity = 50 };

    // Act
    var fullness = tube.Fullness;

    // Assert
    fullness.Should().Be(0.5);
}
```

### 2. Theory Tests for Multiple Scenarios
Parameterized tests for testing multiple cases:
```csharp
[Theory]
[InlineData(0, 100, 0.0)]
[InlineData(50, 100, 0.5)]
[InlineData(100, 100, 1.0)]
public void Fullness_VariousScenarios_CalculatesCorrectly(int count, int capacity, double expected)
{
    // 
}
```

### 3. Exception Testing
Testing expected exceptions with FluentAssertions:
```csharp
[Fact]
public void RequestAmount_WithZeroAmount_ThrowsArgumentException()
{
    // Arrange
    var manager = CreateManager();

    // Act
    Action act = () => manager.RequestAmount(0);

    // Assert
    act.Should().Throw<ArgumentException>()
        .WithParameterName("amountCents");
}
```

## Notes

- Tests for `CoinRefundingManager.RequestAmount` that require serial port access are limited to validation tests
- Integration tests create test device instances but do not attempt to open serial ports
- All tests are designed to run without hardware
- Internal classes are exposed to tests via `InternalsVisibleTo` in the main project