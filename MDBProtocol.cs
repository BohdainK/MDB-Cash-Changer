using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;

namespace MDBCashChanger
{
    /// <summary>
    /// MDB Protocol implementation for cash changer communication
    /// </summary>
    public class MDBProtocol : IDisposable
    {
        private SerialPort? _serialPort;
        private readonly object _lockObject = new object();
        private bool _disposed = false;

        // MDB Protocol Constants
        public const byte ACK = 0x00;
        public const byte NAK = 0xFF;
        public const byte RET = 0xAA;
        public const byte SYNC = 0x55;

        // Cash Changer MDB Commands
        public const byte CHANGER_ADDRESS = 0x08;
        public const byte RESET = 0x00;
        public const byte SETUP = 0x01;
        public const byte SECURITY = 0x02;
        public const byte POLL = 0x03;
        public const byte BILL_TYPE = 0x04;
        public const byte ESCROW = 0x05;
        public const byte STACKER = 0x06;
        public const byte EXPANSION = 0x07;

        public event EventHandler<string>? LogMessage;

        public bool IsConnected => _serialPort?.IsOpen ?? false;

        public bool Initialize(string portName, int baudRate = 9600)
        {
            try
            {
                _serialPort = new SerialPort(portName, baudRate, Parity.None, 8, StopBits.One)
                {
                    ReadTimeout = 1000,
                    WriteTimeout = 1000,
                    Handshake = Handshake.None,
                    DtrEnable = true,
                    RtsEnable = true
                };

                _serialPort.Open();
                LogMessage?.Invoke(this, $"MDB Port {portName} opened successfully at {baudRate} baud");
                return true;
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke(this, $"Failed to open MDB port: {ex.Message}");
                return false;
            }
        }

        public byte[] SendCommand(byte address, byte command, byte[]? data = null)
        {
            lock (_lockObject)
            {
                if (_serialPort == null || !_serialPort.IsOpen)
                {
                    LogMessage?.Invoke(this, "MDB port not open");
                    return Array.Empty<byte>();
                }

                try
                {
                    // Build MDB command packet
                    var packet = new List<byte> { (byte)(address | command) };
                    if (data != null && data.Length > 0)
                    {
                        packet.AddRange(data);
                    }

                    // Calculate checksum
                    byte checksum = 0;
                    foreach (byte b in packet)
                    {
                        checksum += b;
                    }
                    packet.Add(checksum);

                    // Send command
                    _serialPort.Write(packet.ToArray(), 0, packet.Count);
                    LogMessage?.Invoke(this, $"Sent: {BitConverter.ToString(packet.ToArray())}");

                    // Wait for response
                    Thread.Sleep(100);
                    
                    if (_serialPort.BytesToRead > 0)
                    {
                        var response = new byte[_serialPort.BytesToRead];
                        _serialPort.Read(response, 0, response.Length);
                        LogMessage?.Invoke(this, $"Received: {BitConverter.ToString(response)}");
                        return response;
                    }

                    return Array.Empty<byte>();
                }
                catch (Exception ex)
                {
                    LogMessage?.Invoke(this, $"Command failed: {ex.Message}");
                    return Array.Empty<byte>();
                }
            }
        }

        public bool Reset()
        {
            var response = SendCommand(CHANGER_ADDRESS, RESET);
            return response.Length > 0 && response[0] == ACK;
        }

        public MDBSetupInfo? GetSetupInfo()
        {
            var response = SendCommand(CHANGER_ADDRESS, SETUP);
            if (response.Length >= 27)
            {
                var setupInfo = new MDBSetupInfo
                {
                    FeatureLevel = response[0],
                    CountryCode = BitConverter.ToUInt16(response, 1),
                    BillScalingFactor = BitConverter.ToUInt16(response, 3),
                    DecimalPlaces = response[5],
                    StackerCapacity = BitConverter.ToUInt16(response, 6),
                    BillSecurityLevels = BitConverter.ToUInt16(response, 8),
                    EscrowStatus = response[10] != 0,
                    BillTypeCredit = new byte[16]
                };
                Array.Copy(response, 11, setupInfo.BillTypeCredit, 0, 16);
                return setupInfo;
            }
            return null;
        }

        public MDBPollResult Poll()
        {
            var response = SendCommand(CHANGER_ADDRESS, POLL);
            var result = new MDBPollResult();

            if (response.Length == 0)
            {
                result.Status = MDBStatus.NoResponse;
                return result;
            }

            if (response[0] == ACK)
            {
                result.Status = MDBStatus.Ready;
                return result;
            }

            // Parse poll response for status and events
            result.Status = MDBStatus.Active;
            result.Events = ParsePollEvents(response);
            
            return result;
        }

        private MDBEvent[] ParsePollEvents(byte[] response)
        {
            var events = new List<MDBEvent>();
            
            for (int i = 0; i < response.Length; i++)
            {
                byte eventCode = response[i];
                
                // Bill accepted events (0x80-0x8F)
                if ((eventCode & 0xF0) == 0x80)
                {
                    events.Add(new MDBEvent
                    {
                        Type = MDBEventType.BillAccepted,
                        BillType = (byte)(eventCode & 0x0F),
                        Amount = GetBillAmount(eventCode & 0x0F)
                    });
                }
                // Bill rejected events (0x90-0x9F)
                else if ((eventCode & 0xF0) == 0x90)
                {
                    events.Add(new MDBEvent
                    {
                        Type = MDBEventType.BillRejected,
                        BillType = (byte)(eventCode & 0x0F)
                    });
                }
                // Status events
                else
                {
                    events.Add(ParseStatusEvent(eventCode));
                }
            }
            
            return events.ToArray();
        }

        private MDBEvent ParseStatusEvent(byte eventCode)
        {
            return eventCode switch
            {
                0x01 => new MDBEvent { Type = MDBEventType.Defective },
                0x02 => new MDBEvent { Type = MDBEventType.InvalidCommand },
                0x03 => new MDBEvent { Type = MDBEventType.Busy },
                0x04 => new MDBEvent { Type = MDBEventType.Jam },
                0x05 => new MDBEvent { Type = MDBEventType.StackerFull },
                0x10 => new MDBEvent { Type = MDBEventType.Cheated },
                0x11 => new MDBEvent { Type = MDBEventType.Pause },
                _ => new MDBEvent { Type = MDBEventType.Unknown, Data = eventCode }
            };
        }

        private decimal GetBillAmount(int billType)
        {
            // This would typically come from setup info
            // For now, return common bill denominations
            return billType switch
            {
                0 => 1.00m,
                1 => 5.00m,
                2 => 10.00m,
                3 => 20.00m,
                4 => 50.00m,
                5 => 100.00m,
                _ => 0.00m
            };
        }

        public bool EnableBillTypes(ushort billTypes)
        {
            var data = BitConverter.GetBytes(billTypes);
            var response = SendCommand(CHANGER_ADDRESS, BILL_TYPE, data);
            return response.Length > 0 && response[0] == ACK;
        }

        public bool Stack()
        {
            var response = SendCommand(CHANGER_ADDRESS, ESCROW, new byte[] { 0x01 });
            return response.Length > 0 && response[0] == ACK;
        }

        public bool Return()
        {
            var response = SendCommand(CHANGER_ADDRESS, ESCROW, new byte[] { 0x00 });
            return response.Length > 0 && response[0] == ACK;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _serialPort?.Close();
                _serialPort?.Dispose();
                _disposed = true;
            }
        }
    }

    public class MDBSetupInfo
    {
        public byte FeatureLevel { get; set; }
        public ushort CountryCode { get; set; }
        public ushort BillScalingFactor { get; set; }
        public byte DecimalPlaces { get; set; }
        public ushort StackerCapacity { get; set; }
        public ushort BillSecurityLevels { get; set; }
        public bool EscrowStatus { get; set; }
        public byte[] BillTypeCredit { get; set; } = new byte[16];
    }

    public class MDBPollResult
    {
        public MDBStatus Status { get; set; }
        public MDBEvent[] Events { get; set; } = Array.Empty<MDBEvent>();
    }

    public class MDBEvent
    {
        public MDBEventType Type { get; set; }
        public byte BillType { get; set; }
        public decimal Amount { get; set; }
        public byte Data { get; set; }
    }

    public class MDBStatusEventArgs : EventArgs
    {
        public MDBStatus Status { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public enum MDBStatus
    {
        Disconnected,
        Ready,
        Active,
        Busy,
        Error,
        NoResponse
    }

    public enum MDBEventType
    {
        BillAccepted,
        BillRejected,
        Defective,
        InvalidCommand,
        Busy,
        Jam,
        StackerFull,
        Cheated,
        Pause,
        Unknown
    }
}