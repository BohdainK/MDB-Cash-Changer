using System;

namespace MDBControllerLib
{
    internal class InputHandler
    {
        private readonly MDBDevice device;

        public InputHandler(MDBDevice device)
        {
            this.device = device ?? throw new ArgumentNullException(nameof(device));
        }

        public void InputLoop()
        {
            Console.WriteLine("type '<coin_type> <quantity>' (both max 15) to dispense, or 'q' to quit.");

            while (true)
            {
                Console.Write("> ");
                string line;
                try { line = Console.ReadLine()?.Trim() ?? string.Empty; }
                catch { break; }

                if (string.IsNullOrEmpty(line)) continue;
                if (line.Equals("q", StringComparison.OrdinalIgnoreCase))
                    break;

                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 1)
                {
                    Console.WriteLine("Expected coin_type or 'q'.");
                } if (parts.Length >= 2) {
                    if (int.TryParse(parts[0], out int coinType) && int.TryParse(parts[1], out int qty))
                        device.DispenseCoin(coinType, qty);
                } else {
                    Console.WriteLine("Please enter two integers: <coin_type> <quantity>");
                }
            }
        }
    }
}
