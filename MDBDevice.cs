using LiteDB;

namespace MDBControllerLib
{
    internal class MDBDevice
    {
        private readonly SerialManager serial;
        private readonly CancellationToken cancellationToken;
        private readonly Dictionary<int, int> coinTypeValues = new();

        private const string DatabasePath = "coins.db";
        private const int SECURITY_STOCK = 0; //wegwerken !!
        private readonly LiteDatabase db;
        private readonly ILiteCollection<CoinTube> tubes;

        public IReadOnlyDictionary<int, int> CoinTypeValues => coinTypeValues;
        public string? LastEvent => lastEventPayload;

        private enum CoinEventType { None, Accepted, Dispensed }

        private string? lastEventPayload;

        private bool coinInputEnabled = true;


        public MDBDevice(SerialManager serial, CancellationToken cancellationToken)
        {
            this.serial = serial ?? throw new ArgumentNullException(nameof(serial));
            this.cancellationToken = cancellationToken;

            db = new LiteDatabase(DatabasePath);
            tubes = db.GetCollection<CoinTube>("coin_tubes");
            tubes.EnsureIndex(x => x.CoinType);

            foreach (var tube in tubes.FindAll())
            {
                int expect = Math.Max(0, tube.Count - SECURITY_STOCK);
                if (tube.Dispensable != expect)
                {
                    tube.Dispensable = expect;
                    tubes.Update(tube);
                }
            }
        }

        #region Initialization
        public void InitCoinAcceptor()
        {
            serial.WriteLine(CommandConstants.ENABLE_MASTER);
            serial.ReadLine(200);

            serial.WriteLine(CommandConstants.RESET_COIN_ACCEPTOR);
            ThreadShortDelay();
            serial.ReadLine(200);

            serial.WriteLine(CommandConstants.REQUEST_SETUP_INFO);
            var setup = serial.ReadLine(500);

            if (!string.IsNullOrEmpty(setup))
            {
                TryBuildCoinMapFromSetup(setup);
            }
            else
            {
                Console.WriteLine("No setup response received; using fallback coin map.");
                BuildFallbackCoinMap();
            }

            // Ensure tube definitions exist and denominations are correct
            foreach (var kv in coinTypeValues)
            {
                int coinType = kv.Key;
                int value = kv.Value;

                var tube = tubes.FindOne(t => t.CoinType == coinType);
                if (tube == null)
                {
                    tube = new CoinTube
                    {
                        CoinType = coinType,
                        Value = value,
                        Count = 0,
                        Capacity = 50,
                        Dispensable = 0
                    };
                    tubes.Insert(tube);
                }
                else
                {
                    tube.Value = value;
                    tubes.Update(tube);
                }
            }

            // If DB has no tubes or all counts are 0, get hardware tube status
            var allTubes = tubes.FindAll().ToList();
            bool dbLooksEmpty = allTubes.Count == 0 || allTubes.All(t => t.Count == 0);

            if (dbLooksEmpty)
            {
                Console.WriteLine("Tube DB is empty or zeroed. Reading tube levels from hardware...");
                RefreshTubeLevelsFromHardware();
            }

            serial.WriteLine(CommandConstants.COIN_TYPE);
            serial.ReadLine(200);
        }

        public IEnumerable<CoinTube> CoinTubes => tubes.FindAll();

        public Task StartPollingAsync() => Task.Run(PollLoop);

        #endregion

        #region Polling loop
        private async Task PollLoop()

        {
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

                    var (eventType, parsed, coinType) = ParseCoinEvent(resp);
                    if (eventType != CoinEventType.None && coinType.HasValue)
                    {
                        lastEventPayload = parsed;
                        Console.WriteLine($"Coin event: {parsed}");

                        var tube = tubes.FindOne(t => t.CoinType == coinType.Value);
                        if (tube != null)
                        {
                            switch (eventType)
                            {
                                case CoinEventType.Accepted:
                                    tube.Count = Math.Min(tube.Count + 1, tube.Capacity);
                                    tube.Dispensable = Math.Max(0, tube.Count - SECURITY_STOCK);
                                    NotifyStateChanged(System.Text.Json.JsonSerializer.Serialize(
                                        new { eventType = "coin", coinType, newCount = tube.Count, dispensable = tube.Dispensable }));
                                    break;

                                case CoinEventType.Dispensed:
                                    tube.Count = Math.Max(0, tube.Count - 1);
                                    tube.Dispensable = Math.Max(0, tube.Count - SECURITY_STOCK);
                                    NotifyStateChanged(System.Text.Json.JsonSerializer.Serialize(
                                        new { eventType = "dispense", coinType, newCount = tube.Count, dispensable = tube.Dispensable }));
                                    break;
                            }

                            tubes.Update(tube);
                        }
                    }


                    await Task.Delay(250, cancellationToken);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    Console.WriteLine($"Poll loop error: {ex.Message}");
                    await Task.Delay(500, cancellationToken);
                }
            }
        }
        #endregion

        #region Coin dispensing
        public void DispenseCoin(int coinType, int quantity = 1)
        {
            var tube = tubes.FindOne(t => t.CoinType == coinType);
            if (tube == null)
            {
                Console.WriteLine($"No tube found for coin type {coinType}");
                return;
            }

            if (quantity <= 0)
            {
                Console.WriteLine("Quantity must be >= 1");
                return;
            }

            if (quantity > tube.Dispensable)
            {
                Console.WriteLine(
                    $"Not enough dispensable coins for type {coinType}. " +
                    $"Requested={quantity}, dispensable={tube.Dispensable}, count={tube.Count}");
                return;
            }

            int rawType = coinType - 1;
            if (rawType < 0 || rawType > 15)
            {
                Console.WriteLine($"Invalid coin type {coinType}");
                return;
            }

            byte y1 = (byte)(((quantity & 0x0F) << 4) | (rawType & 0x0F));
            string cmd = $"{CommandConstants.DISPENSE},{y1:X2}";
            serial.WriteLine(cmd);
            var resp = serial.ReadLine(800);

            Console.WriteLine($"Dispense command (type {coinType}, raw={rawType}, qty {quantity}) ACK → {resp}");

        }



        #endregion


        #region Tube management

        public bool CoinInputEnabled
        {
            get => coinInputEnabled;
            set
            {
                if (coinInputEnabled == value) return;
                coinInputEnabled = value;
                ApplyCoinInhibitState();
            }
        }

        private void ApplyCoinInhibitState()
        {
            try
            {
                if (coinInputEnabled)
                {
                    serial.WriteLine(CommandConstants.COIN_TYPE);
                    Console.WriteLine("Coin input ENABLED");
                }
                else
                {
                    serial.WriteLine(CommandConstants.INHIBIT_COIN_ACCEPTOR);
                    Console.WriteLine("Coin input DISABLED");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to apply coin inhibit state: {ex.Message}");
            }
        }


        // Returns a DTO-friendly summary of all coin tubes for UI or logging.
        public IEnumerable<CoinTubeSummary> GetTubeSummary()
        {
            return tubes.FindAll()
                .OrderBy(t => t.CoinType)
                .Select(t => new CoinTubeSummary
                {
                    CoinType = t.CoinType,
                    Value = t.Value,
                    Count = t.Count,
                    Dispensable = t.Dispensable,
                    Capacity = t.Capacity,
                    FullnessPercent = (int)(t.Fullness * 100),
                    Status = t.Count == 0 ? "Empty" :
                            t.Count == t.Capacity ? "Full" :
                            "OK"
                });
        }

        public event Action<string>? OnStateChanged;

        private void NotifyStateChanged(string message)
        {
            try { OnStateChanged?.Invoke(message); } catch { }
        }

        // Reset all tubes to 0 coins.
        public void ResetAllTubes()
        {
            foreach (var tube in tubes.FindAll())
            {
                tube.Count = 0;
                tube.Dispensable = Math.Max(0, tube.Count - SECURITY_STOCK);
                tubes.Update(tube);
            }
            Console.WriteLine("All tube counts reset to 0.");
        }

        // Manually set the coin count of a specific tube.
        public void SetTubeCount(int coinType, int newCount)
        {
            var tube = tubes.FindOne(t => t.CoinType == coinType);
            if (tube == null)
            {
                Console.WriteLine($"No tube found for coin type {coinType}");
                return;
            }

            tube.Count = Math.Max(0, Math.Min(newCount, tube.Capacity));
            tube.Dispensable = Math.Max(0, tube.Count - SECURITY_STOCK);
            tubes.Update(tube);

            Console.WriteLine($"Tube {coinType} updated to {tube.Count}/{tube.Capacity}");
        }

        private void RefreshTubeLevelsFromHardware()
        {
            try
            {
                serial.WriteLine(CommandConstants.TUBE_STATUS_REQUEST);
                var resp = serial.ReadLine(500);

                if (string.IsNullOrWhiteSpace(resp) || !resp.StartsWith("p,"))
                {
                    Console.WriteLine("Tube status: no valid response.");
                    return;
                }

                var payload = resp.Substring(2).Trim();
                var bytes = ParseHexBytes(payload);

                Console.WriteLine("TUBE STATUS RAW: " + resp);
                Console.WriteLine("TUBE STATUS BYTES: " + string.Join(" ", bytes.Select(b => b.ToString("X2"))));

                if (bytes.Count < 3)
                {
                    Console.WriteLine("Tube status: too few bytes.");
                    return;
                }

                const int FLAGS_BYTES = 2;
                const int MAX_TYPES = 16;

                for (int rawType = 0; rawType < MAX_TYPES; rawType++)
                {
                    int idx = FLAGS_BYTES + rawType;
                    if (idx >= bytes.Count)
                        break;

                    int approxCount = bytes[idx];
                    int coinType = rawType + 1;

                    var tube = tubes.FindOne(t => t.CoinType == coinType);
                    if (tube == null)
                    {
                        int value = coinTypeValues.TryGetValue(coinType, out var v) ? v : 0;
                        tube = new CoinTube
                        {
                            CoinType = coinType,
                            Value = value,
                            Capacity = 50
                        };
                    }

                    tube.Count = approxCount;
                    tube.Dispensable = Math.Max(0, tube.Count - SECURITY_STOCK);
                    tubes.Upsert(tube);
                }

                Console.WriteLine("Tube levels refreshed from hardware.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error refreshing tube levels from hardware: {ex.Message}");
            }
        }



        #endregion

        #region Parsing helpers

        private (CoinEventType type, string? message, int? coinType) ParseCoinEvent(string resp)
        {
            if (string.IsNullOrEmpty(resp) || !resp.StartsWith("p,"))
                return (CoinEventType.None, null, null);

            var payload = resp.Substring(2).Trim();
            var bytes = ParseHexBytes(payload);
            if (bytes.Count == 0)
                return (CoinEventType.None, null, null);

            byte b = bytes[0];
            int rawType = b & 0x0F;
            int coinType = rawType + 1;
            if (!coinTypeValues.ContainsKey(coinType))
                return (CoinEventType.None, null, null);

            byte upper = (byte)(b & 0xF0);
            CoinEventType evtType =
                upper == 0x50 ? CoinEventType.Accepted :
                upper == 0x90 ? CoinEventType.Dispensed :
                CoinEventType.None;

            string? msg = evtType switch
            {
                CoinEventType.Accepted => $"Accepted coin {coinType} ({coinTypeValues[coinType]})",
                CoinEventType.Dispensed => $"Dispensed coin {coinType}",
                _ => null
            };

            return (evtType, msg, coinType);
        }



        private void TryBuildCoinMapFromSetup(string setupResp)
        {
            Console.WriteLine("SETUP RAW: " + setupResp);

            if (string.IsNullOrWhiteSpace(setupResp) || !setupResp.StartsWith("p,"))
            {
                Console.WriteLine("Setup parse aborted: response does not start with 'p,'");
                return;
            }

            var payload = setupResp.Substring(2).Trim();
            var bytes = ParseHexBytes(payload);

            Console.WriteLine("SETUP BYTES: " + string.Join(" ", bytes.Select(b => b.ToString("X2"))));

            if (bytes.Count < 8)
            {
                Console.WriteLine($"Setup parse warning: expected at least 8 bytes, got {bytes.Count}.");
                return;
            }

            try
            {
                int scaling = bytes[3];
                int decimals = bytes[4];

                int startIndex = 7;
                int maxTypes = Math.Min(16, bytes.Count - startIndex);

                coinTypeValues.Clear();

                for (int rawType = 0; rawType < maxTypes; rawType++)
                {
                    int idx = startIndex + rawType;
                    if (idx >= bytes.Count)
                        break;

                    byte creditUnits = bytes[idx];

                    if (creditUnits == 0x00 || creditUnits == 0xFF)
                        continue;

                    int value = creditUnits * scaling;

                    int coinType = rawType + 1;

                    coinTypeValues[coinType] = value;
                }

                Console.WriteLine(
                    $"Coin map (scaling={scaling}, decimals={decimals}): " +
                    string.Join(", ", coinTypeValues.Select(kv => $"{kv.Key}={kv.Value}"))
                );
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to parse setup info: {ex.Message}");
            }

            if (coinTypeValues.Count == 0)
            {
                Console.WriteLine("Setup parse resulted in EMPTY coin map. Using fallback map.");
                BuildFallbackCoinMap();
            }
        }




        private void BuildFallbackCoinMap()
        {
            coinTypeValues.Clear();
            coinTypeValues[1] = 10;
            coinTypeValues[2] = 20;
            coinTypeValues[3] = 50;
            coinTypeValues[4] = 100;
            coinTypeValues[5] = 200;

            Console.WriteLine(
                "Fallback coin map: " +
                string.Join(", ", coinTypeValues.Select(kv => $"{kv.Key}={kv.Value}"))
            );
        }



        private void SyncTubesWithCoinMap()
        {
            if (coinTypeValues.Count == 0)
            {
                Console.WriteLine("Tube sync skipped: coinTypeValues is empty (no valid setup info).");
                return;
            }

            var validTypes = new HashSet<int>(coinTypeValues.Keys);

            // Remove tubes for coin types that no longer exist in setup
            tubes.DeleteMany(t => !validTypes.Contains(t.CoinType));

            // Insert or update all valid coin types
            foreach (var kv in coinTypeValues)
            {
                int coinType = kv.Key;
                int value = kv.Value;

                var tube = tubes.FindOne(t => t.CoinType == coinType);
                if (tube == null)
                {
                    tube = new CoinTube
                    {
                        CoinType = coinType,
                        Value = value,
                        Count = 0,
                        Capacity = 50,
                        Dispensable = 0
                    };
                    tubes.Insert(tube);
                }
                else
                {
                    // Update the denomination, keep Count/Dispensable as they are
                    tube.Value = value;
                    tubes.Update(tube);
                }
            }
        }


        private static List<byte> ParseHexBytes(string hexStr)
        {
            var result = new List<byte>();
            string hs = hexStr.Trim().Replace(",", "").Replace(" ", "");
            if (hs.Length % 2 != 0) hs = "0" + hs;
            for (int i = 0; i < hs.Length; i += 2)
                if (byte.TryParse(hs.Substring(i, 2), System.Globalization.NumberStyles.HexNumber, null, out byte b))
                    result.Add(b);
            return result;
        }

        private void ThreadShortDelay() => Thread.Sleep(50);

        #endregion
    }

    #region DTO classes
    internal class CoinTube
    {
        public int Id { get; set; }
        public int CoinType { get; set; }
        public int Value { get; set; }
        // The physical coin count in the tube
        public int Count { get; set; }
        // Number of coins allowed to be dispensed (Count - SECURITY_STOCK, >= 0)
        public int Dispensable { get; set; }
        public int Capacity { get; set; }
        public double Fullness => (double)Count / Capacity;
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

        public override string ToString()
        {
            return $"Type {CoinType}: {Value} units | {Count}/{Capacity} ({FullnessPercent}%) - {Status}";
        }
    }

    #endregion
}

