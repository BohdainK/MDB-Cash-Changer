using System;
using System.Threading.Tasks;

namespace MDBCashChanger
{
    /// <summary>
    /// Demonstration class showing MDB Cash Changer functionality
    /// </summary>
    public class Demo
    {
        public static async Task RunDemo()
        {
            Console.WriteLine("=== MDB Cash Changer Demo ===");
            Console.WriteLine();

            // Create cash changer instance
            using var cashChanger = new CashChanger();

            // Set up event handlers
            cashChanger.LogMessage += (s, e) => Console.WriteLine($"[LOG] {e}");
            cashChanger.BillAccepted += (s, e) => 
                Console.WriteLine($"[BILL ACCEPTED] ${e.Amount:F2} (Type {e.BillType}) - Total: ${e.TotalAccepted:F2}");
            cashChanger.BillRejected += (s, e) => 
                Console.WriteLine($"[BILL REJECTED] Type {e.BillType}");
            cashChanger.StatusChanged += (s, e) => 
                Console.WriteLine($"[STATUS] {e.OldStatus} -> {e.NewStatus}: {e.Message}");

            Console.WriteLine("1. Attempting to initialize cash changer (simulation mode)...");
            
            // Simulate initialization (this would normally connect to actual hardware)
            Console.WriteLine("   - Opening MDB port COM1 at 9600 baud");
            Console.WriteLine("   - Sending RESET command");
            Console.WriteLine("   - Getting device setup information");
            Console.WriteLine("   - Enabling all bill types");
            
            // Simulate setup info
            Console.WriteLine("   - Feature Level: 3");
            Console.WriteLine("   - Country Code: 840 (USA)");
            Console.WriteLine("   - Bill Scaling Factor: 1");
            Console.WriteLine("   - Escrow Support: Yes");
            Console.WriteLine("   - Stacker Capacity: 500 bills");
            
            Console.WriteLine("✓ Initialization completed successfully");
            Console.WriteLine();

            Console.WriteLine("2. Starting polling loop...");
            Console.WriteLine("   - Monitoring device for bill insertion");
            Console.WriteLine("   - Checking device status every 100ms");
            Console.WriteLine("✓ Polling started");
            Console.WriteLine();

            Console.WriteLine("3. Simulating bill insertion events...");
            await Task.Delay(1000);

            // Simulate bill accepted event
            Console.WriteLine("[BILL ACCEPTED] $5.00 (Type 1) - Total: $5.00");
            Console.WriteLine("   Action: Bill is now in escrow position");
            Console.WriteLine("   Options: Press 'A' to accept/stack or 'D' to return");
            await Task.Delay(1000);

            Console.WriteLine("   Simulating STACK command...");
            Console.WriteLine("✓ Bill stacked successfully");
            Console.WriteLine();

            // Simulate another bill
            Console.WriteLine("[BILL ACCEPTED] $20.00 (Type 3) - Total: $25.00");
            await Task.Delay(1000);

            Console.WriteLine("   Simulating RETURN command...");
            Console.WriteLine("✓ Bill returned to customer");
            Console.WriteLine();

            Console.WriteLine("4. Demonstrating error handling...");
            Console.WriteLine("[ERROR] Bill jam detected");
            Console.WriteLine("[STATUS] Ready -> Error: Bill jam detected");
            await Task.Delay(1000);

            Console.WriteLine("   Sending RESET command to clear jam...");
            Console.WriteLine("✓ Device reset successful");
            Console.WriteLine("[STATUS] Error -> Ready: Device reset");
            Console.WriteLine();

            Console.WriteLine("5. Available keyboard commands in real application:");
            Console.WriteLine("   H  - Show help");
            Console.WriteLine("   I  - Initialize cash changer");
            Console.WriteLine("   S  - Start polling");
            Console.WriteLine("   P  - Stop polling");
            Console.WriteLine("   R  - Reset device");
            Console.WriteLine("   A  - Accept/Stack bill");
            Console.WriteLine("   D  - Decline/Return bill");
            Console.WriteLine("   C  - Clear total amount");
            Console.WriteLine("   L  - Clear log/screen");
            Console.WriteLine("   F1 - Show device information");
            Console.WriteLine("   Q  - Quit application");
            Console.WriteLine();

            Console.WriteLine("6. MDB Protocol Features Implemented:");
            Console.WriteLine("   ✓ Device initialization and reset");
            Console.WriteLine("   ✓ Setup information retrieval");
            Console.WriteLine("   ✓ Continuous polling");
            Console.WriteLine("   ✓ Bill type configuration");
            Console.WriteLine("   ✓ Bill acceptance/rejection");
            Console.WriteLine("   ✓ Escrow control (stack/return)");
            Console.WriteLine("   ✓ Status monitoring");
            Console.WriteLine("   ✓ Error handling and recovery");
            Console.WriteLine("   ✓ Event logging");
            Console.WriteLine("   ✓ Serial communication");
            Console.WriteLine();

            Console.WriteLine("Demo completed! The full application provides terminal-based");
            Console.WriteLine("control of all MDB cash changer functions via keyboard commands.");
            Console.WriteLine();
            Console.WriteLine("Run 'dotnet run' to start the interactive terminal interface.");
        }
    }
}