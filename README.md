# MDB Cash Changer Controller

A .NET library for controlling MDB (Multi-Drop Bus) compatible coin changers. This library provides complete MDB protocol implementation with support for coin acceptance, dispensing, and automated payment handling with change calculation.

## Requirements

- .NET 9.0 or later
- Serial port access (USB-to-Serial adapter for MDB communication)
- MDB-compatible coin changer device

## Installation

1. Clone the repository:
   ```bash
   git clone https://github.com/BohdainK/MDB-Cash-Changer.git
   cd MDB-Cash-Changer
   ```

2. Build the project:
   ```bash
   dotnet build
   ```

3. Run the application:

   ```bash
   dotnet run <serial_port>
   ```

   Example: `dotnet run /dev/tty.usbmodem01` or `dotnet run COM1`

## Architecture

### Core Components

#### MDBDevice

Main class for controlling the MDB coin changer device.

**Key Methods:**

- `InitCoinAcceptor()` - Initializes the coin acceptor, configures coin types, and refreshes tube levels
- `StartPollingAsync()` - Starts the asynchronous polling loop to monitor device events (returns Task)
- `DispenseCoin(int coinType, int quantity)` - Dispenses specified quantity of coins of the given type
- `ApplyCoinInhibitState(bool enabled)` - Enables or disables coin acceptance (true = enabled, false = disabled)
- `GetTubeSummary()` - Returns a collection of coin tube summaries with current levels and status
- `ResetAllTubes()` - Resets all tube counts to 0 (useful for maintenance or testing)

**Properties:**

- `CoinTubes` - Returns enumerable collection of all coin tubes with their current state
- `LastEvent` - Gets the last event payload received from the device

**Events:**

- `OnStateChanged` - Fires when device state changes (coin inserted, dispensed, etc.) with JSON message

#### CoinRefundingManager

Manages payment requests with automatic change calculation and refund handling.

**Key Methods:**

- `RequestAmount(int amountCents)` - Requests a specific amount in cents, enables coin acceptance, and tracks inserted coins
- `CancelRequest()` - Cancels active payment request and automatically refunds all inserted coins
- `RefundAmount(int amount)` - Calculates and dispenses the exact change amount using available coin tubes (returns true if successful)

**Properties:**

- `RequestedAmount` - Gets the total amount requested in cents
- `InsertedAmount` - Gets the total amount inserted so far in cents
- `RemainingAmount` - Gets the remaining amount needed (RequestedAmount - InsertedAmount)
- `IsRequestActive` - Returns true if a payment request is currently active

**Events:**

- `OnAmountStateChanged` - Fires when payment state changes (active, success, cancelled) with AmountRequestState object

#### SerialManager

Handles low-level serial port communication with the MDB device.

**Key Methods:**

- `Open()` - Opens the serial port connection with configured settings
- `Close()` - Safely closes the serial port connection
- `WriteLine(string line)` - Writes a command line to the serial port
- `ReadLine(int? timeoutMs)` - Reads a response line from the serial port with optional timeout override

**Configuration:**

- **Baud Rate**: 115200 (defined in CommandConstants.BAUD)
- **Data Bits**: 8
- **Stop Bits**: 1
- **Parity**: None
- **Default Timeout**: 500ms (configurable per read)

#### CoinTube (Domain Model)

Represents a single coin tube in the changer.

**Properties:**

- `CoinType` - The coin type identifier (1-16)
- `Value` - Coin value in cents
- `Count` - Current number of coins in the tube
- `Dispensable` - Number of coins available for dispensing
- `Capacity` - Maximum tube capacity (default: 50 coins)
- `Fullness` - Calculated fullness percentage (0.0 to 1.0)

#### InputHandler

Console-based input handler for manual testing and debugging.

**Key Methods:**

- `InputLoop()` - Starts interactive console loop for manual coin dispensing commands

**Console Commands:**

- `<coin_type> <quantity>` - Dispense coins (e.g., `1 5` dispenses 5 coins of type 1)
- `q` - Quit the application

#### CommandConstants

Static class containing MDB protocol command constants and configuration values.

**Constants:**

- `BAUD` - Serial baud rate (115200)
- `TIMEOUT` - Default serial timeout in milliseconds (500)
- Various MDB command strings (ENABLE_MASTER, RESET_COIN_ACCEPTOR, POLL, DISPENSE, etc.)

### Exceptions

#### MDBDeviceException

Base exception class for all MDB device-related errors.

**Properties:**

- `Timestamp` - UTC timestamp when the exception occurred

#### Specialized Exceptions

- `SetupParseException` - Thrown when setup information parsing fails
- `PollLoopException` - Thrown for polling loop and communication errors
- `TubeRefreshException` - Thrown when tube level refresh operations fail
- `CoinOperationException` - Thrown for coin dispensing or refund errors

All exceptions include timestamps for debugging and logging purposes.

### WebUI (Testing Only)

Web-based interface for testing and monitoring the coin changer. **This component is intended for testing and demonstration purposes only.**

**Features:**

- Real-time WebSocket communication showing coin events
- Live tube level monitoring
- Manual coin dispensing controls
- Amount request testing interface
- Tube reset functionality

**Access:** [http://localhost:8080/](http://localhost:8080/) (default port)

**Note:** The WebUI should not be used in production environments. For production integration, use the MDBDevice and CoinRefundingManager classes directly.

## Usage Examples

### Basic Setup and Coin Dispensing

```csharp
using var serial = new SerialManager("/dev/tty.usbmodem01", CommandConstants.BAUD, CommandConstants.TIMEOUT);
serial.Open();

var cts = new CancellationTokenSource();
var device = new MDBDevice(serial, cts.Token);

// Initialize the coin acceptor
device.InitCoinAcceptor();

// Start polling for events
var pollingTask = device.StartPollingAsync();

// Dispense 3 coins of type 1
device.DispenseCoin(coinType: 1, quantity: 3);

// Cleanup
cts.Cancel();
await pollingTask;
```

### Payment Request with Automatic Change

```csharp
var device = new MDBDevice(serial, cts.Token);
device.InitCoinAcceptor();

// Create coin type value mapping
var coinTypeValues = new Dictionary<int, int>
{
    { 1, 5 },   // Type 1 = 5 cents
    { 2, 10 },  // Type 2 = 10 cents
    { 3, 25 },  // Type 3 = 25 cents
    { 4, 50 }   // Type 4 = 50 cents
};

var refundManager = new CoinRefundingManager(device, coinTypeValues);

// Subscribe to payment state changes
refundManager.OnAmountStateChanged += (state) =>
{
    Console.WriteLine($"Status: {state.Status}");
    Console.WriteLine($"Requested: {state.RequestedAmount} cents");
    Console.WriteLine($"Inserted: {state.InsertedAmount} cents");
    Console.WriteLine($"Remaining: {state.RemainingAmount} cents");
};

// Request payment of 75 cents
refundManager.RequestAmount(75);

// User inserts coins...
// When exact or overpayment is reached, change is automatically dispensed

// Or cancel the request and refund all inserted coins
refundManager.CancelRequest();
```

### Monitoring Device Events

```csharp
var device = new MDBDevice(serial, cts.Token);
device.InitCoinAcceptor();

// Subscribe to device state changes
device.OnStateChanged += (message) =>
{
    Console.WriteLine($"Device event: {message}");
    // JSON format: {"eventType":"coin","coinType":1,"value":5,"newCount":10,"dispensable":10}
};

await device.StartPollingAsync();
```

### Checking Tube Levels

```csharp
var device = new MDBDevice(serial, cts.Token);
device.InitCoinAcceptor();

// Get current tube status
var tubes = device.GetTubeSummary();
foreach (var tube in tubes)
{
    Console.WriteLine($"Coin Type {tube.CoinType}: {tube.Value} cents");
    Console.WriteLine($"  Count: {tube.Count}/{tube.Capacity}");
    Console.WriteLine($"  Dispensable: {tube.Dispensable}");
    Console.WriteLine($"  Status: {tube.Status} ({tube.FullnessPercent}% full)");
}
```

### Enabling/Disabling Coin Acceptance

```csharp
var device = new MDBDevice(serial, cts.Token);
device.InitCoinAcceptor();

// Enable coin acceptance
device.ApplyCoinInhibitState(true);

// Disable coin acceptance
device.ApplyCoinInhibitState(false);
```

## MDB Protocol Support

- Device initialization and setup
- Coin type configuration and mapping
- Continuous polling for events
- Coin acceptance tracking
- Coin dispensing with quantity control
- Automatic tube level management
- Expansion commands
- Error handling and recovery

## Event Types

The device reports the following coin events:

- **coin** - Coin accepted and routed to tube
- **cashbox** - Coin routed to cashbox (tube full or not configured)
- **dispense** - Coin dispensed from tube
- **returned** - Coin returned/rejected

## Payment Request States

- **idle** - No active request
- **active** - Request active, waiting for coins
- **success** - Requested amount received, change dispensed if needed
- **cancelled** - Request cancelled, coins refunded

## Error Handling

The library provides comprehensive error handling:

- Serial port communication failures → `MDBDeviceException`
- MDB protocol errors → `SetupParseException`, `PollLoopException`
- Tube refresh failures → `TubeRefreshException`
- Coin operation errors → `CoinOperationException`
- Device timeout conditions (10 consecutive poll failures triggers exception)