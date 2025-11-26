using System;

namespace MDBControllerLib
{
    internal static class CommandConstants
    {
        public const int BAUD = 115200;
        public const int TIMEOUT = 500;

        // MDB / QIBIXX commands
        public const string ENABLE_MASTER = "M,1";
        public const string RESET_COIN_ACCEPTOR = "R,08";
        public const string REQUEST_SETUP_INFO = "R,09";
        public const string EXPANSION_REQUEST = "R,0F,00";
        public const string EXPANSION_FEATURE_ENABLE = "R,0F,0100000000";
        public const string TUBE_STATUS_REQUEST = "R,0A";
        public const string COIN_TYPE = "R,0C,001F0000";
        public const string INHIBIT_COIN_ACCEPTOR = "R,0C,00000000";
        public const string POLL = "R,0B";
        public const string DISPENSE = "R,0D";
    }
}
