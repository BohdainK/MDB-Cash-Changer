namespace MDBControllerLib
{
    internal class CoinRefundingManager
    {
        private readonly MDBDevice device;
        private readonly Dictionary<int, int> coinTypeValues;

        internal CoinRefundingManager(MDBDevice device, Dictionary<int, int> coinTypeValues)
        {
            this.device = device;
            this.coinTypeValues = coinTypeValues;
        }

        public bool RefundAmount(int amount)
        {
            if (amount <= 0)
                return true; // nothing to refund

            var tubesSnapshot = device.CoinTubes
                .Select(t => new { t.CoinType, t.Value, t.Count, t.Dispensable })
                .Where(t => t.Dispensable > 0 && t.Value > 0)
                .OrderByDescending(t => t.Value) // use highest value coins first
                .ToList();

            if (!tubesSnapshot.Any())
                return false;

            var plan = new Dictionary<int, int>(); // coinType -> quantity to dispense
            int remaining = amount;

            foreach (var tube in tubesSnapshot)
            {
                if (remaining <= 0)
                    break;

                int maxByValue = remaining / tube.Value;
                if (maxByValue <= 0)
                    continue;

                int use = Math.Min(maxByValue, tube.Dispensable);
                if (use <= 0)
                    continue;

                plan[tube.CoinType] = use;
                remaining -= use * tube.Value;
            }

            if (remaining != 0)
                return false;

            foreach (var kvp in plan)
            {
                int coinType = kvp.Key;
                int qty = kvp.Value;
                if (qty > 0)
                {
                    device.DispenseCoin(coinType, qty);
                }
            }

            return true;
        }

    }
}