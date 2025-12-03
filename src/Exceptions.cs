using System;

namespace MDBControllerLib
{
    internal class MDBDeviceException : Exception // main exception for MDB device errors
    {
        public DateTime Timestamp { get; }

        public MDBDeviceException()
        {
            Timestamp = DateTime.UtcNow;
        }
        
        public MDBDeviceException(string message) : base(message)
        {
            Timestamp = DateTime.UtcNow;
        }

        public MDBDeviceException(string message, Exception inner) : base(message, inner)
        {
            Timestamp = DateTime.UtcNow;
        }
    }

    internal class SetupParseException : MDBDeviceException // for errors related to setup parsing
    {
        public SetupParseException(string message) : base(message) { }
        public SetupParseException(string message, Exception inner) : base(message, inner) { }
    }

    internal class PollLoopException : MDBDeviceException // for errors related to the polling loop and communication
    {
        public PollLoopException(string message) : base(message) { }
        public PollLoopException(string message, Exception inner) : base(message, inner) { }
    }

    internal class TubeRefreshException : MDBDeviceException // for errors related to tube refresh operations
    {
        public TubeRefreshException(string message) : base(message) { }
        public TubeRefreshException(string message, Exception inner) : base(message, inner) { }
    }


    internal class CoinOperationException : MDBDeviceException // for errors related to coin operations
    {
        public CoinOperationException(string message) : base(message) { }
        public CoinOperationException(string message, Exception inner) : base(message, inner) { }
    }
}
