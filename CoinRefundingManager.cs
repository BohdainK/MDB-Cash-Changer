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

            // Snapshot of the current DB state:
            // CoinType, Value, Count, Capacity, Fullness (we only need the first three here).
            var tubesSnapshot = device.CoinTubes
                .Select(t => new { t.CoinType, t.Value, t.Count, t.Dispensable })
                .Where(t => t.Dispensable > 0 && t.Value > 0)
                .OrderByDescending(t => t.Value) // use highest value coins first
                .ToList();

            if (!tubesSnapshot.Any())
                return false;

            var plan = new Dictionary<int, int>(); // coinType -> quantity to dispense
            int remaining = amount;

            // Greedy selection of coins based on DB counts
            foreach (var tube in tubesSnapshot)
            {
                if (remaining <= 0)
                    break;

                // Max coins of this type we could use, by value
                int maxByValue = remaining / tube.Value;
                if (maxByValue <= 0)
                    continue;

                // But we cannot exceed what's actually in the tube
                int use = Math.Min(maxByValue, tube.Dispensable);
                if (use <= 0)
                    continue;

                plan[tube.CoinType] = use;
                remaining -= use * tube.Value;
            }

            // If we cannot match the amount exactly, do not dispense anything
            if (remaining != 0)
                return false;

            // Execute the plan: dispense coins via MDBDevice
            // MDBDevice.DispenseCoin will update the LiteDB tube Count itself.
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


//         public bool TryRefund(int amount, out Dictionary<int, int> selection)
// {
//     selection = new();
//     if (amount <= 0) return false;
//     if (coinTypeValues.Count == 0) return false;

//     var allTubes = tubes.FindAll()
//         .OrderByDescending(t => t.Value)
//         .ThenByDescending(t => t.Fullness)
//         .ToList();

//     int remaining = amount;
//     foreach (var t in allTubes)
//     {
//         int usable = Math.Min(t.Count, remaining / t.Value);
//         if (usable > 0)
//         {
//             selection[t.CoinType] = usable;
//             remaining -= usable * t.Value;
//         }
//         if (remaining == 0) break;
//     }

//     if (remaining != 0)
//     {
//         Console.WriteLine($"Refund failed: not enough coins to reach {amount}");
//         return false;
//     }

//     foreach (var kv in selection)
//         DispenseCoin(kv.Key, kv.Value);

//     return true;
// }