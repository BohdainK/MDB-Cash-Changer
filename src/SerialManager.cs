using System;
using System.IO.Ports;
using System.Threading;

namespace MDBControllerLib
{
    internal class SerialManager : IDisposable
    {
        private readonly string portName;
        private readonly int baudRate;
        private readonly int defaultTimeoutMs;
        private SerialPort? serialPort;

        public SerialManager(string portName, int baudRate, int defaultTimeoutMs)
        {
            this.portName = portName;
            this.baudRate = baudRate;
            this.defaultTimeoutMs = defaultTimeoutMs;
        }

        public void Open()
        {
            serialPort = new SerialPort(portName, baudRate, Parity.None, 8, StopBits.One)
            {
                ReadTimeout = defaultTimeoutMs,
                WriteTimeout = defaultTimeoutMs
            };
            serialPort.Open();
            Thread.Sleep(100);
        }
        public void Close()
        {
            try
            {
                if (serialPort != null && serialPort.IsOpen)
                    serialPort.Close();
            }
            catch { }
        }

        public void WriteLine(string line)
        {
            if (serialPort == null || !serialPort.IsOpen)
                throw new InvalidOperationException("Serial port not open");

            serialPort.Write(line + "\n");
        }

        public string ReadLine(int? timeoutMs = null)
        {
            if (serialPort == null || !serialPort.IsOpen)
                return string.Empty;

            int old = serialPort.ReadTimeout;
            if (timeoutMs.HasValue)
                serialPort.ReadTimeout = timeoutMs.Value;

            try
            {
                var l = serialPort.ReadLine();
                return l?.Trim() ?? string.Empty;
            }
            catch (TimeoutException)
            {
                return string.Empty;
            }
            catch
            {
                return string.Empty;
            }
            finally
            {
                if (timeoutMs.HasValue)
                    serialPort.ReadTimeout = old;
            }
        }

        public void Dispose()
        {
            Close();
            serialPort?.Dispose();
            serialPort = null;
        }
    }
}
