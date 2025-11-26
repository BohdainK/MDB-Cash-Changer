using System;

namespace MDBControllerLib
{
    internal class MDBDeviceException : Exception
    {
        public MDBDeviceException() { }
        public MDBDeviceException(string message) : base(message) { }
        public MDBDeviceException(string message, Exception inner) : base(message, inner) { }
    }

    internal class PollLoopException : MDBDeviceException
    {
        public PollLoopException(string message) : base(message) { }
        public PollLoopException(string message, Exception inner) : base(message, inner) { }
    }

    internal class TubeRefreshException : MDBDeviceException
    {
        public TubeRefreshException(string message) : base(message) { }
        public TubeRefreshException(string message, Exception inner) : base(message, inner) { }
    }

    internal class SetupParseException : MDBDeviceException
    {
        public SetupParseException(string message) : base(message) { }
        public SetupParseException(string message, Exception inner) : base(message, inner) { }
    }

    internal class CoinOperationException : MDBDeviceException
    {
        public CoinOperationException(string message) : base(message) { }
        public CoinOperationException(string message, Exception inner) : base(message, inner) { }
    }
}
