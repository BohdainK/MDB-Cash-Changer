
namespace MDBControllerLib
{
    internal class MDBDevice
    {
        private readonly SerialManager serial;
        private readonly CancellationToken cancellationToken;
        private readonly Dictionary<int, int> coinTypeValues = new Dictionary<int, int>();
        private readonly Dictionary<int, int> tubeCounts = new Dictionary<int, int>();

        private string? lastEventPayload;

        public MDBDevice(SerialManager serial, CancellationToken cancellationToken)
        {
            this.serial = serial ?? throw new ArgumentNullException(nameof(serial));
            this.cancellationToken = cancellationToken;
        }

        public void InitCoinAcceptor()
        {
            // Enable master
            serial.WriteLine(CommandConstants.ENABLE_MASTER);
            serial.ReadLine(200);

            // Reset
            serial.WriteLine(CommandConstants.RESET_COIN_ACCEPTOR);
            ThreadShortDelay();
            serial.ReadLine(200);

            // Request setup info
            serial.WriteLine(CommandConstants.REQUEST_SETUP_INFO);
            var setup = serial.ReadLine(500);
            if (!string.IsNullOrEmpty(setup))
            {
                Console.WriteLine($"Setup ({setup})");
                TryBuildCoinMapFromSetup(setup);
            }

            // Expansion
            serial.WriteLine(CommandConstants.EXPANSION_REQUEST);
            var expansion = serial.ReadLine(500);
            if (!string.IsNullOrEmpty(expansion))
            {
                Console.WriteLine($"Expansion ({expansion})");
            }

            serial.WriteLine(CommandConstants.EXPANSION_FEATURE_ENABLE);
            serial.ReadLine(200);

            serial.WriteLine(CommandConstants.TUBE_STATUS_REQUEST);
            var tubeResp = serial.ReadLine(200);
            if (!string.IsNullOrEmpty(tubeResp))
            {
                UpdateTubeCountsFromStatus(tubeResp);
            }

            serial.WriteLine(CommandConstants.COIN_TYPE);
            serial.ReadLine(200);
        }

        public Task StartPollingAsync()
        {
            return Task.Run(PollLoop);
        }

        private async Task PollLoop()
        {
            string? lastSeen = null;

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    serial.WriteLine(CommandConstants.POLL);
                    var resp = serial.ReadLine(600);

                    if (string.IsNullOrEmpty(resp))
                    {
                        await Task.Delay(150, cancellationToken);
                        continue;
                    }

                    var (isEvent, parsed) = IsCoinEvent(resp);
                    if (isEvent)
                    {
                        lastEventPayload = parsed;
                        if (resp != lastSeen)
                            Console.WriteLine($"Coin update: {parsed}");
                        lastSeen = resp;
                    }
                    else
                    {
                        lastSeen = null;
                    }

                    await Task.Delay(250, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Poll loop error: {ex.Message}");
                    try { await Task.Delay(500, cancellationToken); } catch { break; }
                }
            }
        }

        public void DispenseCoin(int coinType, int quantity = 1)
        {
            if (coinType < 0 || coinType > 15 || quantity < 1 || quantity > 15)
            {
                Console.WriteLine("coin_type 0-15, quantity 1-15");
                return;
            }

            byte y1 = (byte)(((quantity & 0x0F) << 4) | (coinType & 0x0F));
            string cmd = $"{CommandConstants.DISPENSE},{y1:X2}";
            serial.WriteLine(cmd);

            var resp = serial.ReadLine(800);
            Console.WriteLine($"Request (coin {coinType}, qty {quantity}), response: {resp}");

            // Poging tot tube count
            if (tubeCounts.ContainsKey(coinType))
            {
                tubeCounts[coinType] = Math.Max(0, tubeCounts[coinType] - quantity);
            }
            //
        }

        // Expose coin values and last seen event for UI
        public IReadOnlyDictionary<int, int> CoinTypeValues => coinTypeValues;
        public string? LastEvent => lastEventPayload;
        public IReadOnlyDictionary<int, int> TubeCounts => tubeCounts;

        public bool TryRefund(int amount, out Dictionary<int, int> selection)
        {
            selection = [];
            if (amount <= 0) return false;

            if (coinTypeValues == null || coinTypeValues.Count == 0)
                return false;

            // Build sorted list of coin types by value descending, prefer fuller tubes when values equal
            var types = coinTypeValues
                .Where(kv => kv.Value > 0)
                .Select(kv => (coinType: kv.Key, value: kv.Value, maxCount: tubeCounts.ContainsKey(kv.Key) ? tubeCounts[kv.Key] : 1000))
                .OrderByDescending(t => t.value)
                .ThenByDescending(t => t.maxCount)
                .ToList();

            int remaining = amount;

            // First attempt: using available counts)
            var greedySelection = new Dictionary<int, int>();
            int remGreedy = remaining;
            foreach (var t in types)
            {
                int maxAvail = Math.Min(t.maxCount, remGreedy / t.value);
                if (maxAvail <= 0) continue;
                greedySelection[t.coinType] = maxAvail;
                remGreedy -= maxAvail * t.value;
                if (remGreedy == 0) break;
            }

            if (remGreedy == 0)
            {
                selection = greedySelection;
            }
            else
            {
                // Try recursive search finding fuller tubes (types already sorted by value then fullness)
                var temp = new Dictionary<int, int>();
                bool found = TryFindCombination(types, 0, remaining, temp);
                if (!found) return false;
                selection = temp;
            }

            // Execute dispense 
            foreach (var kv in selection)
            {
                int coinType = kv.Key;
                int qty = kv.Value;
                while (qty > 0)
                {
                    int batch = Math.Min(15, qty);
                    DispenseCoin(coinType, batch);
                    qty -= batch;
                    try { System.Threading.Thread.Sleep(150); } catch { }
                }
            }

            return true;
        }

        // Find combination of coin types to sum to amount, respecting max counts
        private bool TryFindCombination(List<(int coinType, int value, int maxCount)> types, int idx, int remaining, Dictionary<int, int> used)
        {
            if (remaining == 0) return true;
            if (idx >= types.Count) return false;

            var t = types[idx];
            int maxUse = Math.Min(t.maxCount, remaining / t.value);

            for (int use = maxUse; use >= 0; use--)
            {
                if (use > 0) used[t.coinType] = use; else used.Remove(t.coinType);
                int newRem = remaining - use * t.value;
                if (TryFindCombination(types, idx + 1, newRem, used)) return true;
            }

            return false;
        }

        // Only partial tube status, only displays full or empty
        private void UpdateTubeCountsFromStatus(string statusResp)
        {
            if (string.IsNullOrEmpty(statusResp) || !statusResp.StartsWith("p,")) return;
            var payload = statusResp.Substring(2).Trim();
            var bytes = ParseHexBytes(payload);
            if (bytes.Count < 13) return;

            // tube counts
            for (int i = 7; i < Math.Min(bytes.Count, 13); i++)
            {
                int index = i - 7;
                tubeCounts[index] = bytes[i];
            }

            if (bytes.Count > 13)
            {
                Console.WriteLine($"Tube full bits (byte 13) hit");
                byte tubeFullFlags = bytes[13];
                for (int i = 0; i < 6; i++)
                {
                    bool isFull = (tubeFullFlags & (1 << i)) != 0;
                    if (isFull)
                    {
                        tubeCounts[i] = 255; // mark full visually/logically
                        Console.WriteLine($"Tube {i} marked as full (flag bit set)");
                    }
                }
            }

            Console.WriteLine($"Tube counts: {string.Join(", ", tubeCounts.Select(kv => $"{kv.Key}={kv.Value}"))}");
        }


        #region parsing helpers

        private static List<byte> ParseHexBytes(string hexStr)
        {
            var result = new List<byte>();
            string hs = hexStr.Trim().Replace(",", "").Replace(" ", "");
            if (string.IsNullOrEmpty(hs)) return result;
            if (hs.Length % 2 != 0) hs = "0" + hs;

            for (int i = 0; i < hs.Length; i += 2)
            {
                if (byte.TryParse(hs.Substring(i, 2), System.Globalization.NumberStyles.HexNumber, null, out byte b))
                    result.Add(b);
                else
                    break;
            }

            return result;
        }

        private void TryBuildCoinMapFromSetup(string setupResp)
        {
            if (!setupResp.StartsWith("p,")) return;
            var payload = setupResp.Substring(2).Trim();
            var bytes = ParseHexBytes(payload);
            if (bytes.Count < 13) return;

            try
            {
                int scaling = bytes[3];
                int maxTypes = Math.Min(16, bytes.Count - 7);

                coinTypeValues.Clear();
                for (int i = 0; i < maxTypes; i++)
                {
                    byte creditUnits = bytes[7 + i];
                    if (creditUnits == 0) continue;
                    int value = creditUnits * scaling;
                    coinTypeValues[i + 1] = value; // 1-based per MDB spec
                }

                Console.WriteLine($"Coin map (scaling={scaling}): {string.Join(", ", coinTypeValues.Select(kv => $"{kv.Key}={kv.Value}"))}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to parse setup info: {ex.Message}");
            }
        }


        private (bool isCoin, string? message) IsCoinEvent(string resp)
        {
            if (string.IsNullOrEmpty(resp) || !resp.StartsWith("p,")) return (false, null);
            var payload = resp.Substring(2).Trim();
            if (payload.Equals("ACK", StringComparison.OrdinalIgnoreCase) || payload.Equals("NACK", StringComparison.OrdinalIgnoreCase))
                return (false, null);

            var bytes = ParseHexBytes(payload);
            if (bytes.Count == 0) return (false, null);

            byte b = bytes[0];
            int statusNibble = (b >> 4) & 0x0F;
            int coinType = b & 0x0F;

            // Debug
            Console.WriteLine($"Payload: {payload}");
            Console.WriteLine($"Bytes: {string.Join(" ", bytes.Select(x => x.ToString("X2")))}");
            Console.WriteLine($"Status nibble: {statusNibble}");
            Console.WriteLine($"Coin-type nibble: {coinType}");

            // status nibble
            string statusMsg = statusNibble switch
            {
                0 => "unknown",
                1 => "coin routed to cashbox",
                2 => "coin rejected",
                3 => "tube jam",
                4 => "routed to cash box",
                5 => "coin accepted",
                6 => "mechanical reject",
                7 => "tube full",
                _ => $"unknown status {statusNibble}"
            };

            // Reject events
            if (statusNibble == 7)
                return (true, $"rejected coin (payload {payload})");

            // Valid coin
            if (coinTypeValues.ContainsKey(coinType))
            {
                int value = coinTypeValues[coinType];
                string msg = $"accepted coin type {coinType} ({value} units) - {statusMsg}";
                return (true, msg);
            }

            // Unknown type
            return (true, $"unmapped coin event (status={statusMsg}, type={coinType}, payload={payload})");
        }


        #endregion

        private void ThreadShortDelay()
        {
            try { System.Threading.Thread.Sleep(50); } catch { }
        }
    }
}
