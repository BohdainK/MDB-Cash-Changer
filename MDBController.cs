using MDBControllerLib;

namespace MDBController
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length != 1)
            {
                Console.WriteLine("Usage: dotnet run <serial_port>  (e.g. /dev/tty.usbmodem01 or COM1)");
                Environment.Exit(1);
            }

            string port = args[0];
            var cts = new CancellationTokenSource();

            try
            {
                using var serial = new SerialManager(port, CommandConstants.BAUD, CommandConstants.TIMEOUT);
                serial.Open();

                // read firmware/version
                try {
                    serial.WriteLine("V");
                    var version = serial.ReadLine(500);
                    if (!string.IsNullOrEmpty(version))
                        Console.WriteLine($"Firmware: {version}");
                } catch {
                    Console.WriteLine("Failed to read firmware/version");
                }

                var device = new MDBDevice(serial, cts.Token);
                device.InitCoinAcceptor();

                // start poll loop
                var poll = device.StartPollingAsync();


                var webui = new WebUI(device);
                _ = webui.StartAsync(); // start web server asynchronously
                Console.WriteLine("WebUI running on http://localhost:8080/");


                // console
                var input = new InputHandler(device);
                input.InputLoop();

                // signal shutdown
                cts.Cancel();
                poll.Wait();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }
    }
}
