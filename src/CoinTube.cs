namespace MDBControllerLib.Domain
{
    internal class CoinTube
    {
        public int Id { get; set; }
        public int CoinType { get; set; }
        public int Value { get; set; }
        public int Count { get; set; }
        public int Dispensable { get; set; }
        public int Capacity { get; set; }
        public double Fullness => Capacity == 0 ? 0 : (double)Count / Capacity;
    }

    internal class CoinTubeSummary
    {
        public int CoinType { get; set; }
        public int Value { get; set; }
        public int Count { get; set; }
        public int Dispensable { get; set; }
        public int Capacity { get; set; }
        public int FullnessPercent { get; set; }
        public string Status { get; set; } = "OK";
    }

    internal enum CoinEventType
    {
        None,
        Accepted,
        Dispensed,
        Cashbox,
        Returned
    }
}
