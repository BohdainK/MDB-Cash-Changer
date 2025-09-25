# MDB Cash Changer Controller

A comprehensive C# console application for controlling MDB (Multi-Drop Bus) cash changers with full terminal keyboard interface.

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
| **H** | Show help menu |
| **I** | Initialize cash changer (setup serial port) |
| **S** | Start polling (begin monitoring for bills) |
| **P** | Stop polling |
| **R** | Reset device |
| **T** | Test single poll |
| **A** | Accept/Stack bill (move bill to stacker) |
| **D** | Decline/Return bill (return bill to customer) |
| **C** | Clear total amount counter |
| **L** | Clear log/screen |
| **F1** | Show device information |
| **Q/ESC** | Quit application |

### Basic Operation Flow

1. **Initialize**: Press 'I' to set up serial port connection
2. **Start Polling**: Press 'S' to begin monitoring for bills
3. **Bill Processing**: When a bill is inserted:
   - Press 'A' to accept and stack the bill
   - Press 'D' to return the bill to customer
4. **Monitor Status**: Watch real-time status updates and total amount

## Architecture

### Core Components

- **MDBProtocol.cs**: Low-level MDB protocol implementation
- **CashChanger.cs**: High-level cash changer management
- **TerminalInterface.cs**: Interactive terminal interface
- **Program.cs**: Application entry point

### MDB Protocol Support

- Device initialization and setup
- Bill type configuration
- Continuous polling
- Bill acceptance/rejection
- Escrow control (stack/return)
- Status monitoring
- Error handling

### Supported MDB Commands

- `RESET` - Device reset
- `SETUP` - Get device configuration
- `POLL` - Status polling
- `BILL_TYPE` - Configure accepted bill types
- `ESCROW` - Control bill escrow (stack/return)
- `SECURITY` - Security level configuration

## Configuration

### Serial Port Settings
- **Baud Rate**: 9600 (default, configurable)
- **Data Bits**: 8
- **Stop Bits**: 1
- **Parity**: None
- **Flow Control**: None

### Bill Types
The application supports standard bill denominations:
- $1, $5, $10, $20, $50, $100

## Event Handling

The application handles all standard MDB events:
- **Bill Accepted**: Bill successfully validated and escrowed
- **Bill Rejected**: Bill rejected due to validation failure
- **Device Errors**: Jams, stacker full, defective device
- **Status Changes**: Ready, active, busy, error states

## Error Handling

Comprehensive error handling for:
- Serial port communication failures
- MDB protocol errors
- Device malfunctions
- Invalid commands
- Timeout conditions

## Logging

All operations are logged with timestamps:
- MDB command/response packets
- Device status changes
- Bill acceptance/rejection events
- Error conditions
- User actions

## Development

### Adding New Features

1. **MDB Commands**: Add new commands in `MDBProtocol.cs`
2. **Terminal Commands**: Add keyboard handlers in `TerminalInterface.cs` 
3. **Business Logic**: Extend functionality in `CashChanger.cs`

### Testing

The application includes a test mode for validating MDB communication without physical bill insertion.

## Troubleshooting

### Common Issues

1. **Port Access**: Ensure proper permissions for serial port access
2. **Device Connection**: Verify MDB wiring and power supply
3. **Protocol Errors**: Check MDB addressing and checksums
4. **Timeout Issues**: Verify device responsiveness and baud rate

### Debug Mode

Use the logging output to diagnose communication issues and protocol problems.

## License

This project is open source and available under the MIT License.

## Contributing

Contributions are welcome! Please submit pull requests with:
- Clear description of changes
- Proper error handling
- Updated documentation
- Tested functionality

## Support

For issues and questions:
- Open an issue on GitHub
- Check the troubleshooting section
- Review MDB protocol documentation