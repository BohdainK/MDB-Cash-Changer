using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace MDBControllerLib
{
    internal class WebUI
    {
        private readonly MDBDevice device;
        private readonly CoinRefundingManager refundManager;
        private readonly HttpListener listener;
        private readonly List<WebSocket> clients = new();
        private readonly CancellationTokenSource cts = new();

        private readonly Dictionary<int, int> coinMap = new();
        private int requestedAmountCents = 0;
        private int insertedAmountCents = 0;
        private bool requestActive = false;

        public WebUI(MDBDevice device, int port = 8080)
        {
            this.device = device;
            listener = new HttpListener();
            listener.Prefixes.Add($"http://localhost:{port}/");

            foreach (var t in device.GetTubeSummary())
            {
                coinMap[t.CoinType] = t.Value;
            }

            refundManager = new CoinRefundingManager(device, coinMap);

            device.OnStateChanged += HandleDeviceEvent;
        }

        public async Task StartAsync()
        {
            listener.Start();
            Console.WriteLine("🌐 WebUI running on http://localhost:8080/");
            while (!cts.Token.IsCancellationRequested)
            {
                var ctx = await listener.GetContextAsync();

                if (ctx.Request.IsWebSocketRequest)
                    _ = HandleWebSocketAsync(ctx);
                else
                    await ServeHtmlAsync(ctx);
            }
        }

        private async Task ServeHtmlAsync(HttpListenerContext ctx)
        {
            const string html = @"
<!DOCTYPE html>
<html lang='en'>
<head>
<meta charset='UTF-8'>
<title>MDB Cash Changer - Live View</title>
<style>
body { font-family: Arial, sans-serif; background:#f8f8f8; margin:2em; }
table { border-collapse: collapse; width: 100%; margin-top: 1em; background:white; }
th, td { border: 1px solid #ccc; padding: 8px; text-align:center; }
th { background-color:#eee; }
button { padding:6px 10px; background:#0078d7; color:white; border:none; border-radius:5px; cursor:pointer; }
button:hover { background:#005fa3; }
.status-ok { color:green; font-weight:bold; }
.status-full { color:orange; font-weight:bold; }
.status-empty { color:red; font-weight:bold; }
#controls { margin-bottom: 1em; }
input[type='number'] { padding:6px; border:1px solid #ccc; border-radius:4px; }
#amountPanel { margin-top:1em; padding:1em; background:white; border:1px solid #ccc; }
#amountMessage { margin-top:0.5em; font-weight:bold; }
</style>
</head>
<body>
<h2>MDB Cash Changer - Live View</h2>

<div id='controls'>
    <button onclick='resetTubes()' style='background:#b22222'>Reset All Tubes</button>
</div>

<label>
    <input type='checkbox' id='acceptToggle' checked onchange='toggleCoinInput(this.checked)'>
    Accept Coins
</label>

<!-- NEW: Amount request panel -->
<div id='amountPanel'>
    <h3>Amount Request</h3>
    <div>
        <input type='number' id='amountInput' min='1' placeholder='Amount in cents' />
        <button onclick='startAmountRequest()' style='background:#228B22'>Start Request</button>
        <button onclick='cancelRequest()' style='background:#b22222'>Cancel &amp; Refund</button>
    </div>
    <div style='margin-top:0.5em;'>
        Requested: <span id='reqAmount'>0 ct</span> |
        Inserted: <span id='insAmount'>0 ct</span> |
        Remaining: <span id='remAmount'>0 ct</span>
    </div>
    <div id='amountMessage'></div>
</div>

<table id='coinTable'>
<thead><tr><th>Type</th><th>Value</th><th>Count</th><th>Dispensable</th><th>Capacity</th><th>Full%</th><th>Status</th><th>Action</th></tr></thead>
<tbody></tbody>
</table>

<script>
let ws = new WebSocket('ws://localhost:8080/ws');
ws.onopen = () => {
    console.log('WebSocket connected');
    ws.send('get_state');
};

ws.onmessage = (msg) => {
    const data = JSON.parse(msg.data);
    if (data.type === 'state') {
        renderTable(data.tubes);
    } else if (data.type === 'reset') {
        ws.send('get_state');
    } else if (data.type === 'amount_state') {
        updateAmountState(data);
    } else if (data.eventType) {
        updateSingle(data);
    }
};

function renderTable(tubes) {
    const tbody = document.querySelector('#coinTable tbody');
    tbody.innerHTML = '';
    tubes.forEach(t =>{
        const tr = document.createElement('tr');
        const cls = t.Status === 'Full' ? 'status-full' : t.Status === 'Empty' ? 'status-empty' : 'status-ok';
        tr.innerHTML = `
            <td>${t.CoinType}</td>
            <td>${t.Value}</td>
            <td id='count-${t.CoinType}'>${t.Count}</td>
            <td id='dispensable-${t.CoinType}'>${t.Dispensable ?? 0}</td>
            <td>${t.Capacity}</td>
            <td>${t.FullnessPercent}%</td>
            <td class='${cls}'>${t.Status}</td>
            <td><button onclick='dispense(${t.CoinType})'>Dispense</button></td>`;
        tbody.appendChild(tr);
    });
}

function updateSingle(e) {
    const cEl = document.getElementById('count-' + e.coinType);
    const dEl = document.getElementById('dispensable-' + e.coinType);
    if (cEl && (e.newCount !== undefined)) {
        cEl.textContent = e.newCount;
    } else if (cEl && (e.quantity !== undefined)) {
        cEl.textContent = String(Math.max(0, parseInt(cEl.textContent || '0') - e.quantity));
    }
    if (dEl && (e.dispensable !== undefined)) {
        dEl.textContent = e.dispensable;
    }
}

// NEW: update amount state panel
function updateAmountState(s) {
    const req = document.getElementById('reqAmount');
    const ins = document.getElementById('insAmount');
    const rem = document.getElementById('remAmount');
    const msg = document.getElementById('amountMessage');
    if (!req) return;

    req.textContent = (s.requested || 0) + ' ct';
    ins.textContent = (s.inserted || 0) + ' ct';
    rem.textContent = (s.remaining || 0) + ' ct';

    if (s.status === 'success') {
        msg.textContent = 'Requested amount received. Any overpay has been refunded.';
        msg.style.color = 'green';
    } else if (s.status === 'cancelled') {
        msg.textContent = 'Request cancelled. Refunding inserted coins.';
        msg.style.color = 'red';
    } else if (s.status === 'active') {
        msg.textContent = 'Insert coins until the requested amount is reached.';
        msg.style.color = 'black';
    } else {
        msg.textContent = '';
    }
}

function dispense(type) {
    ws.send(JSON.stringify({ action:'dispense', coinType:type }));
}

// NEW: start / cancel amount request
function startAmountRequest() {
    const input = document.getElementById('amountInput');
    const val = parseInt(input.value, 10);
    if (!val || val <= 0) {
        alert('Please enter a positive amount in cents.');
        return;
    }
    ws.send(JSON.stringify({ action:'start_request', amount: val }));
}

function cancelRequest() {
    ws.send(JSON.stringify({ action:'cancel_request' }));
}

function resetTubes() {
    if (confirm('Reset all tube counts to 0?')) {
        ws.send(JSON.stringify({ action:'reset' }));
    }
}

function toggleCoinInput(enabled) {
    ws.send(JSON.stringify({ action:'toggle_accept', enabled }));
}
</script>
</body>
</html>";

            byte[] buf = Encoding.UTF8.GetBytes(html);
            ctx.Response.ContentType = "text/html";
            ctx.Response.ContentLength64 = buf.Length;
            await ctx.Response.OutputStream.WriteAsync(buf, 0, buf.Length);
            ctx.Response.Close();
        }

        private async Task HandleWebSocketAsync(HttpListenerContext ctx)
        {
            var wsContext = await ctx.AcceptWebSocketAsync(subProtocol: null);
            var ws = wsContext.WebSocket;
            lock (clients) clients.Add(ws);

            await SendStateAsync(ws);
            await SendAmountStateAsync(ws);

            try
            {
                var buffer = new byte[1024];
                while (ws.State == WebSocketState.Open)
                {
                    var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                    if (result.MessageType == WebSocketMessageType.Close)
                        break;

                    var msg = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    if (msg == "get_state")
                        await SendStateAsync(ws);
                    else
                        await HandleClientMessageAsync(ws, msg);
                }
            }
            finally
            {
                lock (clients) clients.Remove(ws);
                await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed", CancellationToken.None);
            }
        }

        private async Task HandleClientMessageAsync(WebSocket ws, string msg)
        {
            try
            {
                var json = JsonDocument.Parse(msg);
                if (!json.RootElement.TryGetProperty("action", out var act)) return;

                string action = act.GetString() ?? "";
                switch (action)
                {
                    case "dispense":
                        {
                            int coinType = json.RootElement.GetProperty("coinType").GetInt32();
                            device.DispenseCoin(coinType, 1);
                            break;
                        }
                    case "reset":
                        {
                            device.ResetAllTubes();
                            BroadcastAsync(JsonSerializer.Serialize(new { type = "reset" }));
                            break;
                        }
                    case "toggle_accept":
                        {
                            bool enabled = json.RootElement.GetProperty("enabled").GetBoolean();
                            device.CoinInputEnabled = enabled;
                            break;
                        }

                    case "start_request":
                        {
                            int amount = json.RootElement.GetProperty("amount").GetInt32();
                            if (amount <= 0)
                                return;

                            requestedAmountCents = amount;
                            insertedAmountCents = 0;
                            requestActive = true;
                            Console.WriteLine($"Amount request started: {requestedAmountCents} cents");
                            BroadcastAmountState("active");
                            break;
                        }

                    case "cancel_request":
                        {
                            CancelCurrentRequest();
                            break;
                        }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"WebUI message error: {ex.Message}");
            }
        }

        private async Task SendStateAsync(WebSocket ws)
        {
            var tubes = device.GetTubeSummary();
            var payload = JsonSerializer.Serialize(new { type = "state", tubes });
            var bytes = Encoding.UTF8.GetBytes(payload);
            await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
        }

        private async Task SendAmountStateAsync(WebSocket ws)
        {
            var payload = JsonSerializer.Serialize(new
            {
                type = "amount_state",
                status = requestActive ? "active" : "idle",
                requested = requestedAmountCents,
                inserted = insertedAmountCents,
                remaining = Math.Max(0, requestedAmountCents - insertedAmountCents)
            });
            var bytes = Encoding.UTF8.GetBytes(payload);
            await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
        }

        private void BroadcastAmountState(string status)
        {
            var msg = JsonSerializer.Serialize(new
            {
                type = "amount_state",
                status,
                requested = requestedAmountCents,
                inserted = insertedAmountCents,
                remaining = Math.Max(0, requestedAmountCents - insertedAmountCents)
            });
            BroadcastAsync(msg);
        }

        private void CancelCurrentRequest()
        {
            if (!requestActive)
            {
                requestedAmountCents = 0;
                insertedAmountCents = 0;
                BroadcastAmountState("idle");
                return;
            }

            Console.WriteLine($"Cancelling request. Refunding {insertedAmountCents} cents.");
            if (insertedAmountCents > 0)
            {
                try
                {
                    refundManager.RefundAmount(insertedAmountCents);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Refund error on cancel: {ex.Message}");
                }
            }

            requestedAmountCents = 0;
            insertedAmountCents = 0;
            requestActive = false;
            BroadcastAmountState("cancelled");
        }

        private void HandleDeviceEvent(string message)
        {
            BroadcastAsync(message);

            if (!requestActive)
                return;

            try
            {
                using var doc = JsonDocument.Parse(message);
                var root = doc.RootElement;

                if (!root.TryGetProperty("eventType", out var evtProp))
                    return;

                var evtType = evtProp.GetString();
                if (string.IsNullOrEmpty(evtType))
                    return;

                if (!root.TryGetProperty("coinType", out var ctProp))
                    return;

                int coinType = ctProp.GetInt32();
                if (!coinMap.TryGetValue(coinType, out var value) || value <= 0)
                    return;

                switch (evtType)
                {
                    case "coin":
                    case "cashbox":
                        insertedAmountCents += value;
                        Console.WriteLine($"Inserted +{value} ct ({evtType}), total {insertedAmountCents} / {requestedAmountCents}");
                        break;

                    case "dispense":
                        insertedAmountCents = Math.Max(0, insertedAmountCents - value);
                        Console.WriteLine($"Dispensed {value} ct, total {insertedAmountCents} / {requestedAmountCents}");
                        break;

                    default:
                        return;
                }

                EvaluateAmountState();
            }
            catch
            {
                // ignore parsing errors
            }
        }


        private void EvaluateAmountState()
        {
            if (!requestActive || requestedAmountCents <= 0)
            {
                BroadcastAmountState("idle");
                return;
            }

            if (insertedAmountCents < requestedAmountCents)
            {
                BroadcastAmountState("active");
                return;
            }

            int overpay = insertedAmountCents - requestedAmountCents;
            if (overpay > 0)
            {
                Console.WriteLine($"Overpay: {overpay} cents.");
                try
                {
                    refundManager.RefundAmount(overpay);
                    insertedAmountCents -= overpay;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Refund error on overpay: {ex.Message}");
                }
            }

            requestActive = false;
            Console.WriteLine($"Amount request completed. inserted: {insertedAmountCents} ct.");
            BroadcastAmountState("success");
        }

        private async void BroadcastAsync(string message)
        {
            var bytes = Encoding.UTF8.GetBytes(message);
            List<WebSocket> targets;
            lock (clients) targets = clients.ToList();

            foreach (var ws in targets)
            {
                if (ws.State == WebSocketState.Open)
                {
                    try
                    {
                        await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
                    }
                    catch
                    {
                        // ignore send errors
                    }
                }
            }
        }
    }
}
