using MDBControllerLib.Domain;

namespace MDBControllerLib
{
    internal class MDBDevice
    {
        private readonly SerialManager serial;
        private readonly CancellationToken cancellationToken;
        private readonly Dictionary<int, int> coinTypeValues = new();
        private readonly Dictionary<int, CoinTube> tubes = new();

        public string? LastEvent => lastEventPayload;
        private string? lastEventPayload;

        // private bool coinInputEnabled = true;
        private int pollFailures;

        public MDBDevice(SerialManager serial, CancellationToken cancellationToken)
        {
            this.serial = serial ?? throw new ArgumentNullException(nameof(serial));
            this.cancellationToken = cancellationToken;
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

            TryBuildCoinMapFromSetup(setup);

            foreach (var keyValue in coinTypeValues)
            {
                int coinType = keyValue.Key;
                int value = keyValue.Value;

                tubes[coinType] = new CoinTube
                {
                    CoinType = coinType,
                    Value = value,
                    Count = 0,
                    Dispensable = 0,
                    Capacity = 50
                };
            }

            RefreshTubeLevelsFromHardware();

            if (tubes.Count == 0 || tubes.Values.All(t => t.Count == 0))
            {
                throw new TubeRefreshException("Tube initialization aborted: hardware expansion request failed.");
            }

            serial.WriteLine(CommandConstants.COIN_TYPE);
            serial.ReadLine(200);

            ApplyCoinInhibitState(false);
        }

        public IEnumerable<CoinTube> CoinTubes => tubes.Values;

        public Task StartPollingAsync() => Task.Run(PollLoop);

        #endregion

        #region Polling loop
        private async Task PollLoop()
        {
            while (!cancellationToken.IsCancellationRequested && pollFailures <= 10)
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

                        tubes.TryGetValue(coinType.Value, out var tube);

                        switch (eventType)
                        {
                            case CoinEventType.Accepted when tube != null:
                                tube.Count = Math.Min(tube.Count + 1, tube.Capacity);
                                tube.Dispensable = Math.Max(0, tube.Count);

                                coinTypeValues.TryGetValue(coinType.Value, out var val);

                                NotifyStateChanged(System.Text.Json.JsonSerializer.Serialize(
                                    new
                                    {
                                        eventType = "coin",
                                        coinType,
                                        value = val,
                                        newCount = tube.Count,
                                        dispensable = tube.Dispensable
                                    }));
                                break;

                            case CoinEventType.Dispensed when tube != null:
                                tube.Count = Math.Max(0, tube.Count - 1);
                                tube.Dispensable = Math.Max(0, tube.Count);

                                coinTypeValues.TryGetValue(coinType.Value, out var dval);

                                NotifyStateChanged(System.Text.Json.JsonSerializer.Serialize(
                                    new
                                    {
                                        eventType = "dispense",
                                        coinType,
                                        value = dval,
                                        newCount = tube.Count,
                                        dispensable = tube.Dispensable
                                    }));
                                break;

                            case CoinEventType.Cashbox:
                                coinTypeValues.TryGetValue(coinType.Value, out var cval);

                                NotifyStateChanged(System.Text.Json.JsonSerializer.Serialize(
                                    new
                                    {
                                        eventType = "cashbox",
                                        coinType,
                                        value = cval
                                    }));
                                break;

                            case CoinEventType.Returned:
                                coinTypeValues.TryGetValue(coinType.Value, out var rval);

                                NotifyStateChanged(System.Text.Json.JsonSerializer.Serialize(
                                    new
                                    {
                                        eventType = "returned",
                                        coinType,
                                        value = rval
                                    }));
                                break;
                        }
                    }

                    await Task.Delay(250, cancellationToken);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    pollFailures++;
                    Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] Poll loop error: {ex.Message}");
                    await Task.Delay(500, cancellationToken);
                }
            }
            if (pollFailures > 10)
            {
                throw new MDBDeviceException("No response from device. check device and restart");
            }
        }

        #endregion

        #region Coin dispensing
        public void DispenseCoin(int coinType, int quantity = 1)
        {
            try
            {
                RefreshTubeLevelsFromHardware();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Failed to refresh hardware levels before dispense: {ex.Message}");
            }

            if (!tubes.TryGetValue(coinType, out var tube))
                throw new CoinOperationException($"No tube found for coin type {coinType}");

            if (quantity <= 0)
                throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be >= 1");

            if (quantity > tube.Dispensable)
                throw new CoinOperationException($"Not enough dispensable coins for type {coinType}. Requested={quantity}, dispensable={tube.Dispensable}, count={tube.Count}");

            int rawType = coinType - 1;
            if (rawType < 0 || rawType > 15)
                throw new ArgumentOutOfRangeException(nameof(coinType), $"Invalid coin type {coinType}");

            byte y1 = (byte)(((quantity & 0x0F) << 4) | (rawType & 0x0F));
            string cmd = $"{CommandConstants.DISPENSE},{y1:X2}";
            serial.WriteLine(cmd);
            var resp = serial.ReadLine(800);

            Console.WriteLine($"Dispense command (type {coinType}, raw={rawType}, qty {quantity}) ACK → {resp}");
        }



        #endregion

        #region Tube management

        // public bool CoinInputEnabled
        // {
        //     get => coinInputEnabled;
        //     set
        //     {
        //         if (coinInputEnabled == value) return;
        //         coinInputEnabled = value;
        //         ApplyCoinInhibitState();
        //     }
        // }

        public void ApplyCoinInhibitState(bool enabled)
        {
            try
            {
                if (enabled)
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
                throw new MDBDeviceException("Failed to apply coin inhibit state.", ex);
            }
        }


        // Returns a DTO-friendly summary of all coin tubes for UI or logging.
        public IEnumerable<CoinTubeSummary> GetTubeSummary()
        {
            return tubes.Values
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
            foreach (var tube in tubes.Values)
            {
                tube.Count = 0;
                tube.Dispensable = 0;
            }
            Console.WriteLine("Tube count reset");
        }

        private void RefreshTubeLevelsFromHardware()
        {
            try
            {
                serial.WriteLine(CommandConstants.TUBE_STATUS_REQUEST);
                var resp = serial.ReadLine(500);

                if (string.IsNullOrWhiteSpace(resp) || !resp.StartsWith("p,"))
                {
                    throw new TubeRefreshException("Tube status: no valid response.");
                }

                var payload = resp.Substring(2).Trim();
                var bytes = ParseHexBytes(payload);

                // Console.WriteLine("TUBE STATUS RAW: " + resp);
                // Console.WriteLine("TUBE STATUS BYTES: " + string.Join(" ", bytes.Select(b => b.ToString("X2"))));

                if (bytes.Count < 3)
                {
                    throw new TubeRefreshException("Tube status: too few bytes.");
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

                    if (!tubes.TryGetValue(coinType, out var tube))
                    {
                        int value = coinTypeValues.TryGetValue(coinType, out var v) ? v : 0;
                        tube = new CoinTube
                        {
                            CoinType = coinType,
                            Value = value,
                            Capacity = 50
                        };
                        tubes[coinType] = tube;
                    }

                    tube.Count = approxCount;
                    tube.Dispensable = Math.Max(0, tube.Count);
                }
            }
            catch (Exception ex)
            {
                throw new TubeRefreshException("Error refreshing tube levels from hardware.", ex);
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

            CoinEventType evtType = upper switch
            {
                0x50 => CoinEventType.Accepted,
                0x90 => CoinEventType.Dispensed,
                0x40 => CoinEventType.Cashbox,
                0x70 => CoinEventType.Returned,
                _ => CoinEventType.None
            };

            string? msg = evtType switch
            {
                CoinEventType.Accepted => $"Accepted coin {coinType} ({coinTypeValues[coinType]})",
                CoinEventType.Dispensed => $"Dispensed coin {coinType}",
                CoinEventType.Cashbox => $"Cashbox coin {coinType} ({coinTypeValues[coinType]})",
                CoinEventType.Returned => $"Returned coin {coinType}",
                _ => null
            };


            return (evtType, msg, coinType);
        }




        private void TryBuildCoinMapFromSetup(string setupResp)
        {
            Console.WriteLine("SETUP RAW: " + setupResp);

            if (string.IsNullOrWhiteSpace(setupResp) || !setupResp.StartsWith("p,"))
                throw new SetupParseException("Setup parse aborted: response does not start with 'p,'");

            var payload = setupResp.Substring(2).Trim();
            var bytes = ParseHexBytes(payload);

            Console.WriteLine("SETUP BYTES: " + string.Join(" ", bytes.Select(b => b.ToString("X2"))));

            if (bytes.Count < 8)
                throw new SetupParseException($"Setup parse warning: expected at least 8 bytes, got {bytes.Count}.");

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
                throw new SetupParseException("Failed to parse setup info.", ex);
            }

            if (coinTypeValues.Count == 0)
                throw new SetupParseException("Setup parse resulted in EMPTY coin map.");
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
        #endregion Parsing helpers
    }
}

