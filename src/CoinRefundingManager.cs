namespace MDBControllerLib
{
    internal class CoinRefundingManager
    {
        private readonly MDBDevice device;
        private readonly Dictionary<int, int> coinTypeValues;

        private int requestedAmountCents = 0;
        private int insertedAmountCents = 0;
        private bool requestActive = false;

        public event Action<AmountRequestState>? OnAmountStateChanged;

        public int RequestedAmount => requestedAmountCents;
        public int InsertedAmount => insertedAmountCents;
        public int RemainingAmount => Math.Max(0, requestedAmountCents - insertedAmountCents);
        public bool IsRequestActive => requestActive;

        internal CoinRefundingManager(MDBDevice device, Dictionary<int, int> coinTypeValues)
        {
            this.device = device;
            this.coinTypeValues = coinTypeValues;

            device.OnStateChanged += HandleDeviceEvent;
        }

        private void HandleDeviceEvent(string message)
        {
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(message);
                var root = doc.RootElement;

                if (!root.TryGetProperty("eventType", out var evtProp))
                    return;

                var evtType = evtProp.GetString();
                if (string.IsNullOrEmpty(evtType))
                    return;

                if (!root.TryGetProperty("coinType", out var ctProp))
                    return;

                int coinType = ctProp.GetInt32();

                switch (evtType)
                {
                    case "coin":
                    case "cashbox":
                        OnCoinInserted(coinType);
                        break;

                    case "dispense":
                        OnCoinDispensed(coinType);
                        break;
                }
            }
            catch
            {
                // ignore parsing errors
            }
        }

        public void RequestAmount(int amountCents)
        {
            if (amountCents <= 0)
                throw new ArgumentException("Amount must be greater than 0", nameof(amountCents));

            if (requestActive)
                throw new InvalidOperationException("A request is already active. Cancel it first.");

            requestedAmountCents = amountCents;
            insertedAmountCents = 0;
            requestActive = true;

            Console.WriteLine($"Amount request started: {requestedAmountCents} cents");
            NotifyStateChanged("active");
        }

        public void OnCoinInserted(int coinType)
        {
            if (!requestActive)
                return;

            if (!coinTypeValues.TryGetValue(coinType, out var value) || value <= 0)
                return;

            insertedAmountCents += value;
            Console.WriteLine($"Inserted +{value} ct, total {insertedAmountCents} / {requestedAmountCents}");

            EvaluateAmountState();
        }

        public void OnCoinDispensed(int coinType)
        {
            if (!requestActive)
                return;

            if (!coinTypeValues.TryGetValue(coinType, out var value) || value <= 0)
                return;

            insertedAmountCents = Math.Max(0, insertedAmountCents - value);
            Console.WriteLine($"Dispensed {value} ct, total {insertedAmountCents} / {requestedAmountCents}");

            EvaluateAmountState();
        }

        public void CancelRequest()
        {
            if (!requestActive)
            {
                requestedAmountCents = 0;
                insertedAmountCents = 0;
                NotifyStateChanged("idle");
                return;
            }

            Console.WriteLine($"Cancelling request. Refunding {insertedAmountCents} cents.");
            if (insertedAmountCents > 0)
            {
                try
                {
                    if (!RefundAmount(insertedAmountCents))
                    {
                        Console.WriteLine("Warning: Could not refund exact amount.");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Refund error on cancel: {ex.Message}");
                }
            }

            requestedAmountCents = 0;
            insertedAmountCents = 0;
            requestActive = false;
            NotifyStateChanged("cancelled");
        }

        private void EvaluateAmountState()
        {
            if (!requestActive || requestedAmountCents <= 0)
            {
                NotifyStateChanged("idle");
                return;
            }

            if (insertedAmountCents < requestedAmountCents)
            {
                NotifyStateChanged("active");
                return;
            }

            int overpay = insertedAmountCents - requestedAmountCents;
            if (overpay > 0)
            {
                Console.WriteLine($"Overpay: {overpay} cents.");
                try
                {
                    if (RefundAmount(overpay))
                    {
                        insertedAmountCents -= overpay;
                    }
                    else
                    {
                        Console.WriteLine("Warning: Could not refund exact overpay amount.");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Refund error on overpay: {ex.Message}");
                }
            }

            requestActive = false;
            Console.WriteLine($"Amount request completed. Inserted: {insertedAmountCents} ct.");
            NotifyStateChanged("success");
        }

        private void NotifyStateChanged(string status)
        {
            OnAmountStateChanged?.Invoke(new AmountRequestState
            {
                Status = status,
                RequestedAmount = requestedAmountCents,
                InsertedAmount = insertedAmountCents,
                RemainingAmount = RemainingAmount
            });
        }

        public bool RefundAmount(int amount)
        {
            if (amount <= 0)
                return true;

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

    public class AmountRequestState
    {
        public string Status { get; set; } = "idle";
        public int RequestedAmount { get; set; }
        public int InsertedAmount { get; set; }
        public int RemainingAmount { get; set; }
    }
}