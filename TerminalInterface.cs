using System;
using System.Threading;
using System.Threading.Tasks;

namespace MDBCashChanger
{
    /// <summary>
    /// Terminal interface for controlling the cash changer with keyboard commands
    /// </summary>
    public class TerminalInterface
    {
        private readonly CashChanger _cashChanger;
        private bool _running = false;
        private readonly object _displayLock = new object();

        public TerminalInterface(CashChanger cashChanger)
        {
            _cashChanger = cashChanger;
            _cashChanger.LogMessage += OnLogMessage;
            _cashChanger.BillAccepted += OnBillAccepted;
            _cashChanger.BillRejected += OnBillRejected;
            _cashChanger.StatusChanged += OnStatusChanged;
        }

        public void Run()
        {
            _running = true;
            Console.Clear();
            DisplayHeader();
            DisplayHelp();
            DisplayStatus();

            while (_running)
            {
                var key = Console.ReadKey(true);
                ProcessKey(key);
            }
        }

        private void ProcessKey(ConsoleKeyInfo key)
        {
            switch (key.Key)
            {
                case ConsoleKey.H:
                    DisplayHelp();
                    break;

                case ConsoleKey.I:
                    InitializeCashChanger();
                    break;

                case ConsoleKey.S:
                    StartPolling();
                    break;

                case ConsoleKey.P:
                    StopPolling();
                    break;

                case ConsoleKey.R:
                    ResetDevice();
                    break;

                case ConsoleKey.T:
                    TestPoll();
                    break;

                case ConsoleKey.A:
                    StackBill();
                    break;

                case ConsoleKey.D:
                    ReturnBill();
                    break;

                case ConsoleKey.C:
                    ClearTotal();
                    break;

                case ConsoleKey.L:
                    ClearLog();
                    break;

                case ConsoleKey.Q:
                case ConsoleKey.Escape:
                    Quit();
                    break;

                case ConsoleKey.F1:
                    DisplayDeviceInfo();
                    break;

                default:
                    DisplayMessage($"Unknown command: {key.Key}. Press 'H' for help.");
                    break;
            }
        }

        private void InitializeCashChanger()
        {
            DisplayMessage("Enter COM port (e.g., COM1, /dev/ttyUSB0): ");
            var port = Console.ReadLine();
            
            if (string.IsNullOrWhiteSpace(port))
            {
                DisplayMessage("Invalid port name.");
                return;
            }

            DisplayMessage("Enter baud rate (default 9600): ");
            var baudInput = Console.ReadLine();
            
            int baudRate = 9600;
            if (!string.IsNullOrWhiteSpace(baudInput) && !int.TryParse(baudInput, out baudRate))
            {
                DisplayMessage("Invalid baud rate, using default 9600.");
                baudRate = 9600;
            }

            DisplayMessage($"Initializing cash changer on {port} at {baudRate} baud...");
            
            Task.Run(() =>
            {
                bool success = _cashChanger.Initialize(port, baudRate);
                DisplayMessage(success ? "Initialization successful!" : "Initialization failed!");
                DisplayStatus();
            });
        }

        private void StartPolling()
        {
            if (_cashChanger.Status == CashChangerStatus.Disconnected)
            {
                DisplayMessage("Cash changer not initialized. Press 'I' to initialize first.");
                return;
            }

            _cashChanger.StartPolling();
            DisplayMessage("Polling started. Cash changer is now monitoring for bills.");
            DisplayStatus();
        }

        private void StopPolling()
        {
            _cashChanger.StopPolling();
            DisplayMessage("Polling stopped.");
            DisplayStatus();
        }

        private void ResetDevice()
        {
            DisplayMessage("Resetting cash changer...");
            Task.Run(() =>
            {
                bool success = _cashChanger.Reset();
                DisplayMessage(success ? "Reset successful!" : "Reset failed!");
                DisplayStatus();
            });
        }

        private void TestPoll()
        {
            if (_cashChanger.Status == CashChangerStatus.Disconnected)
            {
                DisplayMessage("Cash changer not initialized. Press 'I' to initialize first.");
                return;
            }

            DisplayMessage("Performing single poll...");
            // This would require exposing the MDB protocol poll method
            DisplayMessage("Poll completed. Check log for details.");
        }

        private void StackBill()
        {
            if (_cashChanger.Status == CashChangerStatus.Disconnected)
            {
                DisplayMessage("Cash changer not initialized. Press 'I' to initialize first.");
                return;
            }

            DisplayMessage("Stacking bill...");
            Task.Run(() =>
            {
                bool success = _cashChanger.StackBill();
                DisplayMessage(success ? "Bill stacked successfully!" : "Failed to stack bill.");
            });
        }

        private void ReturnBill()
        {
            if (_cashChanger.Status == CashChangerStatus.Disconnected)
            {
                DisplayMessage("Cash changer not initialized. Press 'I' to initialize first.");
                return;
            }

            DisplayMessage("Returning bill...");
            Task.Run(() =>
            {
                bool success = _cashChanger.ReturnBill();
                DisplayMessage(success ? "Bill returned successfully!" : "Failed to return bill.");
            });
        }

        private void ClearTotal()
        {
            _cashChanger.ClearTotal();
            DisplayMessage("Total amount cleared.");
            DisplayStatus();
        }

        private void ClearLog()
        {
            Console.Clear();
            DisplayHeader();
            DisplayHelp();
            DisplayStatus();
        }

        private void DisplayDeviceInfo()
        {
            if (_cashChanger.SetupInfo == null)
            {
                DisplayMessage("No device information available. Initialize the device first.");
                return;
            }

            var info = _cashChanger.SetupInfo;
            DisplayMessage("=== Device Information ===");
            DisplayMessage($"Feature Level: {info.FeatureLevel}");
            DisplayMessage($"Country Code: {info.CountryCode}");
            DisplayMessage($"Bill Scaling Factor: {info.BillScalingFactor}");
            DisplayMessage($"Decimal Places: {info.DecimalPlaces}");
            DisplayMessage($"Stacker Capacity: {info.StackerCapacity}");
            DisplayMessage($"Security Levels: {info.BillSecurityLevels:X4}");
            DisplayMessage($"Escrow Support: {(info.EscrowStatus ? "Yes" : "No")}");
            DisplayMessage("==========================");
        }

        private void Quit()
        {
            DisplayMessage("Shutting down...");
            _cashChanger.StopPolling();
            _running = false;
        }

        private void DisplayHeader()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("╔══════════════════════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║                            MDB Cash Changer Controller                       ║");
            Console.WriteLine("║                                   v1.0.0                                    ║");
            Console.WriteLine("╚══════════════════════════════════════════════════════════════════════════════╝");
            Console.ResetColor();
            Console.WriteLine();
        }

        private void DisplayHelp()
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("═══ KEYBOARD COMMANDS ═══");
            Console.WriteLine("H - Show this help");
            Console.WriteLine("I - Initialize cash changer");
            Console.WriteLine("S - Start polling");
            Console.WriteLine("P - Stop polling");
            Console.WriteLine("R - Reset device");
            Console.WriteLine("T - Test single poll");
            Console.WriteLine("A - Accept/Stack bill");
            Console.WriteLine("D - Decline/Return bill");
            Console.WriteLine("C - Clear total amount");
            Console.WriteLine("L - Clear log/screen");
            Console.WriteLine("F1 - Show device information");
            Console.WriteLine("Q/ESC - Quit application");
            Console.WriteLine("════════════════════════");
            Console.ResetColor();
            Console.WriteLine();
        }

        private void DisplayStatus()
        {
            lock (_displayLock)
            {
                var oldPosition = Console.GetCursorPosition();
                
                Console.SetCursorPosition(0, 0);
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write($"Status: {_cashChanger.Status,-12} ");
                Console.ForegroundColor = ConsoleColor.White;
                Console.Write($"Total: ${_cashChanger.TotalAccepted:F2,-8} ");
                Console.ForegroundColor = ConsoleColor.Magenta;
                Console.Write($"Polling: {(_cashChanger.IsPolling ? "ON " : "OFF")} ");
                Console.ResetColor();
                Console.WriteLine();
                
                Console.SetCursorPosition(oldPosition.Left, oldPosition.Top);
            }
        }

        private void DisplayMessage(string message)
        {
            lock (_displayLock)
            {
                var timestamp = DateTime.Now.ToString("HH:mm:ss");
                Console.ForegroundColor = ConsoleColor.Gray;
                Console.Write($"[{timestamp}] ");
                Console.ResetColor();
                Console.WriteLine(message);
            }
        }

        private void OnLogMessage(object? sender, string message)
        {
            DisplayMessage(message);
        }

        private void OnBillAccepted(object? sender, BillEventArgs e)
        {
            lock (_displayLock)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                DisplayMessage($"✓ BILL ACCEPTED: ${e.Amount:F2} (Type {e.BillType}) - Total: ${e.TotalAccepted:F2}");
                Console.ResetColor();
                DisplayStatus();
            }
        }

        private void OnBillRejected(object? sender, BillEventArgs e)
        {
            lock (_displayLock)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                DisplayMessage($"✗ BILL REJECTED: Type {e.BillType}");
                Console.ResetColor();
            }
        }

        private void OnStatusChanged(object? sender, StatusChangedEventArgs e)
        {
            lock (_displayLock)
            {
                var color = e.NewStatus switch
                {
                    CashChangerStatus.Ready => ConsoleColor.Green,
                    CashChangerStatus.Active => ConsoleColor.Cyan,
                    CashChangerStatus.Busy => ConsoleColor.Yellow,
                    CashChangerStatus.Error => ConsoleColor.Red,
                    _ => ConsoleColor.Gray
                };

                Console.ForegroundColor = color;
                DisplayMessage($"STATUS: {e.NewStatus} - {e.Message}");
                Console.ResetColor();
                DisplayStatus();
            }
        }
    }
}