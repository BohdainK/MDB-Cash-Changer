using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LiteDB;

namespace MDBControllerLib
{
    internal class MDBDevice
    {
        private readonly SerialManager serial;
        private readonly CancellationToken cancellationToken;
        private readonly Dictionary<int, int> coinTypeValues = new(); // type → value

        private const string DatabasePath = "coins.db";
        private readonly LiteDatabase db;
        private readonly ILiteCollection<CoinTube> tubes;

        public IReadOnlyDictionary<int, int> CoinTypeValues => coinTypeValues;
        public string? LastEvent => lastEventPayload;

        private enum CoinEventType { None, Accepted, Dispensed }

        private string? lastEventPayload;



        public MDBDevice(SerialManager serial, CancellationToken cancellationToken)
        {
            this.serial = serial ?? throw new ArgumentNullException(nameof(serial));
            this.cancellationToken = cancellationToken;

            db = new LiteDatabase(DatabasePath);
            tubes = db.GetCollection<CoinTube>("coin_tubes");
            tubes.EnsureIndex(x => x.CoinType);
        }

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
                TryBuildCoinMapFromSetup(setup);

            // Initialize or ensure tube records exist in the DB
            foreach (var type in coinTypeValues.Keys)
            {
                if (!tubes.Exists(t => t.CoinType == type))
                    tubes.Insert(new CoinTube { CoinType = type, Value = coinTypeValues[type], Count = 0, Capacity = 50 });
            }

            serial.WriteLine(CommandConstants.COIN_TYPE);
            serial.ReadLine(200);
        }

        public IEnumerable<CoinTube> CoinTubes => tubes.FindAll();

        public Task StartPollingAsync() => Task.Run(PollLoop);

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
                                    NotifyStateChanged(System.Text.Json.JsonSerializer.Serialize(
                                        new { eventType = "coin", coinType, newCount = tube.Count }));
                                    break;

                                case CoinEventType.Dispensed:
                                    tube.Count = Math.Max(0, tube.Count - 1);
                                    NotifyStateChanged(System.Text.Json.JsonSerializer.Serialize(
                                        new { eventType = "dispense", coinType, newCount = tube.Count }));
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

        public void DispenseCoin(int coinType, int quantity = 1)
        {
            byte y1 = (byte)(((quantity & 0x0F) << 4) | (coinType & 0x0F));
            string cmd = $"{CommandConstants.DISPENSE},{y1:X2}";
            serial.WriteLine(cmd);
            var resp = serial.ReadLine(800);

            Console.WriteLine($"Dispense (type {coinType}, qty {quantity}) → {resp}");

            var tube = tubes.FindOne(t => t.CoinType == coinType);
            if (tube != null)
            {
                tube.Count = Math.Max(0, tube.Count - quantity);
                tubes.Update(tube);
                NotifyStateChanged(System.Text.Json.JsonSerializer.Serialize(new { eventType = "dispense", coinType, quantity }));
            }
        }

        public bool TryRefund(int amount, out Dictionary<int, int> selection)
        {
            selection = new();
            if (amount <= 0) return false;
            if (coinTypeValues.Count == 0) return false;

            var allTubes = tubes.FindAll()
                .OrderByDescending(t => t.Value)
                .ThenByDescending(t => t.Fullness)
                .ToList();

            int remaining = amount;
            foreach (var t in allTubes)
            {
                int usable = Math.Min(t.Count, remaining / t.Value);
                if (usable > 0)
                {
                    selection[t.CoinType] = usable;
                    remaining -= usable * t.Value;
                }
                if (remaining == 0) break;
            }

            if (remaining != 0)
            {
                Console.WriteLine($"Refund failed: not enough coins to reach {amount}");
                return false;
            }

            foreach (var kv in selection)
                DispenseCoin(kv.Key, kv.Value);

            return true;
        }

        #region --- UI-Friendly Methods ---


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
            tubes.Update(tube);

            Console.WriteLine($"Tube {coinType} updated to {tube.Count}/{tube.Capacity}");
        }

        #endregion

        #region --- Parsing helpers ---

        private (CoinEventType type, string? message, int? coinType) ParseCoinEvent(string resp)
        {
            if (string.IsNullOrEmpty(resp) || !resp.StartsWith("p,"))
                return (CoinEventType.None, null, null);

            var payload = resp.Substring(2).Trim();
            var bytes = ParseHexBytes(payload);
            if (bytes.Count == 0)
                return (CoinEventType.None, null, null);

            byte b = bytes[0];
            int coinType = b & 0x0F;
            if (!coinTypeValues.ContainsKey(coinType))
                return (CoinEventType.None, null, null);

            // Distinguish by upper nibble
            byte upper = (byte)(b & 0xF0);

            CoinEventType evtType;
            if (upper == 0x50) evtType = CoinEventType.Accepted;     // e.g., 0x51 = coin type 1 accepted
            else if (upper == 0x90) evtType = CoinEventType.Dispensed; // e.g., 0x91 = coin type 1 dispensed
            else evtType = CoinEventType.None;

            string msg = evtType switch
            {
                CoinEventType.Accepted => $"Accepted coin {coinType} ({coinTypeValues[coinType]})",
                CoinEventType.Dispensed => $"Dispensed coin {coinType}",
                _ => null
            };

            return (evtType, msg, coinType);
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

                int startIndex = 8;
                int maxTypes = Math.Min(16, bytes.Count - startIndex);

                coinTypeValues.Clear();
                for (int i = 0; i < maxTypes; i++)
                {
                    byte creditUnits = bytes[startIndex + i];
                    if (creditUnits == 0) continue;

                    int value = creditUnits * scaling;
                    coinTypeValues[i + 1] = value; // keep 1-based numbering for display and protocol
                }

                Console.WriteLine($"Coin map (scaling={scaling}): {string.Join(", ", coinTypeValues.Select(kv => $"{kv.Key}={kv.Value}"))}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to parse setup info: {ex.Message}");
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

    internal class CoinTube
    {
        public int Id { get; set; }
        public int CoinType { get; set; }
        public int Value { get; set; }
        public int Count { get; set; }
        public int Capacity { get; set; }
        public double Fullness => (double)Count / Capacity;
    }

    internal class CoinTubeSummary
    {
        public int CoinType { get; set; }
        public int Value { get; set; }
        public int Count { get; set; }
        public int Capacity { get; set; }
        public int FullnessPercent { get; set; }
        public string Status { get; set; } = "OK";

        public override string ToString()
        {
            return $"Type {CoinType}: {Value} units | {Count}/{Capacity} ({FullnessPercent}%) - {Status}";
        }
    }
}
