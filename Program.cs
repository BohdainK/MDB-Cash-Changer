using System;
using System.Threading.Tasks;
using MDBCashChanger;

namespace MDBCashChanger
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.Title = "MDB Cash Changer Controller";
            
            // Check for demo mode
            if (args.Length > 0 && args[0].ToLower() == "demo")
            {
                await Demo.RunDemo();
                return;
            }
            
            // Create cash changer instance
            using var cashChanger = new CashChanger();
            
            // Create and run terminal interface
            var terminal = new TerminalInterface(cashChanger);
            
            try
            {
                terminal.Run();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Application error: {ex.Message}");
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
            }
        }
    }
}
