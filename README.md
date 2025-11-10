# MDB Cash Changer Controller\

## TODO
### Code
- MUST: Logic for coin dispensing of desired amount
   - COULD: logic for entering desired amount and disabling any further coin input
- SHOULD: Support for pre-filled tubes? I.E. also accept semi-accurate sensor readings when new casette is inserted

### Documentation
- Software Deisgn Descriptions
- Happy path (and unhappy)


## Features

- **Complete MDB Protocol Implementation**: Full support for MDB cash changer communication
- **Terminal Interface**: Intuitive keyboard-driven interface for all operations
- **Real-time Monitoring**: Continuous polling and event handling
- **Bill Management**: Accept, reject, stack, and return bills
- **Status Monitoring**: Real-time device status and error handling
- **Event Logging**: Comprehensive logging of all operations and events

## Requirements

- .NET 9.0 or later
- Serial port access (USB-to-Serial adapter for MDB communication)
- MDB-compatible cash changer device

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
   dotnet run
   ```

## Usage

### Keyboard Commands

| Key | Function |
|-----|----------|
|**Q**|Quit      |
|**`<coin_type> <quantity>`**|Disepense coins |


### Basic Operation Flow

1. **InitCoinAcceptor**: Initialises coin changer
2. **StartPollingAsync**: Starts polling coin changer for updates
3. **DispenseCoin**: Dispenses coins bases on keyboard input

## Architecture

### Core Components

- **MDBController.cs**: Main class
- **MDBDevice.cs**: implementation
- **SerialManager.cs**: Manages serial connection with device
- **InputHandler.cs**: Manages console input
- **WebUI.cs**: Manages web ui
- **CommandConstants.cs**: Class for code readability


### MDB Protocol Support

- Device initialization and setup
- Coin type configuration
- Continuous polling
- Coin acceptance/rejection
- Coin refund
- Error handling

## Configuration

### Serial Port Settings
- **Baud Rate**: 9600 (default, configurable)
- **Data Bits**: 8
- **Stop Bits**: 1
- **Parity**: None
- **Flow Control**: None

### Coin Types
- Available coin types are indexed automatically

## Event Handling

The application handles all standard MDB events:
- 0 => "unknown",
- 1 => "coin routed to cashbox",
- 2 => "coin rejected",
- 3 => "tube jam",
- 4 => "routed to cash box",
- 5 => "coin accepted",
- 6 => "mechanical reject",
- 7 => "tube full",

## Error Handling

Comprehensive error handling for:
- Serial port communication failures
- MDB protocol errors
- Device malfunctions
- Invalid commands
- Timeout conditions
