
// using System;
// using System.Collections.Generic;
// using System.Linq;

// namespace MDBControllerLib
// {
//     public class MDBController
//     {
//         private Dictionary<int, int> coinTypeValues = new Dictionary<int, int>();
//         private static readonly Dictionary<byte, (string label, string status)> FALLBACK_FIRST_BYTE_MAP = new Dictionary<byte, (string label, string status)>();

//         private static List<byte> ParseHexBytes(string hexStr)
//         {
//             var result = new List<byte>();
//             string hs = hexStr.Trim().Replace(",", "").Replace(" ", "");
//             if (string.IsNullOrEmpty(hs)) return result;
//             if (hs.Length % 2 != 0) hs = "0" + hs;

//             for (int i = 0; i < hs.Length; i += 2)
//             {
//                 if (byte.TryParse(hs.Substring(i, 2), System.Globalization.NumberStyles.HexNumber, null, out byte b))
//                     result.Add(b);
//                 else
//                     break;
//             }

//             return result;
//         }

//         private void TryBuildCoinMapFromSetup(string setupResp)
//         {
//             if (!setupResp.StartsWith("p,")) return;
//             var payload = setupResp.Substring(2).Trim();
//             var bytes = ParseHexBytes(payload);

//             try
//             {
//                 if (bytes.Count >= 13)
//                 {
//                     byte z4 = bytes[3]; // scaling
//                     for (int i = 0; i < 6; i++)
//                     {
//                         int zIndex = 7 + i;
//                         byte creditUnits = bytes[zIndex];
//                         if (z4 > 0 && creditUnits > 0)
//                         {
//                             int val = creditUnits * z4;
//                             coinTypeValues[i] = val;
//                         }
//                     }

//                     if (coinTypeValues.Count > 0)
//                         Console.WriteLine($"Mapped values: {string.Join(", ", coinTypeValues.Select(kv => $"{kv.Key}={kv.Value}"))}");
//                 }
//             }
//             catch { }
//         }

//         private (bool isCoin, string? message) IsCoinEvent(string resp)
//         {
//             if (string.IsNullOrEmpty(resp) || !resp.StartsWith("p,")) return (false, null);
//             var payload = resp.Substring(2).Trim();
//             if (payload.Equals("ACK", StringComparison.OrdinalIgnoreCase) || payload.Equals("NACK", StringComparison.OrdinalIgnoreCase))
//                 return (false, null);

//             var bytes = ParseHexBytes(payload);
//             if (bytes.Count == 0) return (false, null);

//             byte first = bytes[0];
//             if (FALLBACK_FIRST_BYTE_MAP.ContainsKey(first))
//             {
//                 var (label, status) = FALLBACK_FIRST_BYTE_MAP[first];
//                 return (true, $"{label} ({payload}) - {status}");
//             }

//             if (bytes.Any(b => b != 0x00))
//             {
//                 string msg = $"event ({payload})";
//                 int candidate = bytes[0] & 0x0F;
//                 if (coinTypeValues.ContainsKey(candidate))
//                     msg += $" -> coin-type {candidate} (~{coinTypeValues[candidate]} units)";
//                 return (true, msg);
//             }

//             return (false, null);
//         }
//     }
// }