using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace MDBControllerLib
{
    internal class WebUI
    {
        private readonly MDBDevice device;
    private readonly CoinRefundingManager refundManager;
        private readonly HttpListener listener;
        private readonly List<WebSocket> clients = new();
        private readonly CancellationTokenSource cts = new();

        public WebUI(MDBDevice device, int port = 8080)
        {
            this.device = device;
            listener = new HttpListener();
            listener.Prefixes.Add($"http://localhost:{port}/");

            // Ensure refund manager is available to handle dispense_amount messages.
            // Copy the device's coin type map so the manager has a stable view.
            this.refundManager = new CoinRefundingManager(device, new System.Collections.Generic.Dictionary<int, int>(device.CoinTypeValues));

            device.OnStateChanged += BroadcastAsync;
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

        public void Stop()
        {
            cts.Cancel();
            listener.Stop();
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
</style>
</head>
<body>
<h2>MDB Cash Changer - Live View</h2>

<div id='controls'>
    <button onclick='resetTubes()' style='background:#b22222'>Reset All Tubes</button>

    <!-- New dispense amount controls -->
    <span style='margin-left:1em;'>
        <input type='number' id='amountInput' min='1' placeholder='Amount to dispense' />
        <button onclick='dispenseAmount()' style='background:#228B22'>Dispense Amount</button>
    </span>
</div>
<label>
    <input type='checkbox' id='acceptToggle' checked onchange='toggleCoinInput(this.checked)'>
    Accept Coins
</label>


<table id='coinTable'>
<thead><tr><th>Type</th><th>Value</th><th>Count</th><th>Dispensable</th><th>Capacity</th><th>Full%</th><th>Status</th><th>Action</th></tr></thead>
<tbody></tbody>
</table>

<script>
let ws = new WebSocket('ws://localhost:8080/ws');
ws.onopen = () => { console.log('WebSocket connected'); ws.send('get_state'); };

ws.onmessage = (msg) => {
    const data = JSON.parse(msg.data);
    if (data.type === 'state') renderTable(data.tubes);
    else if (data.eventType) updateSingle(data);
    else if (data.type === 'reset') ws.send('get_state');
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
    if (cEl && (e.newCount !== undefined)) cEl.textContent = e.newCount;
    else if (cEl && (e.quantity !== undefined)) cEl.textContent = String(Math.max(0, parseInt(cEl.textContent || '0') - e.quantity));
    if (dEl && (e.dispensable !== undefined)) dEl.textContent = e.dispensable;
}

function dispense(type) {
    ws.send(JSON.stringify({ action:'dispense', coinType:type }));
}

function dispenseAmount() {
    const input = document.getElementById('amountInput');
    const val = parseInt(input.value, 10);
    if (!val || val <= 0) {
        alert('Please enter a positive amount to dispense.');
        return;
    }
    ws.send(JSON.stringify({ action:'dispense_amount', amount: val }));
    input.value = '';
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
                        int coinType = json.RootElement.GetProperty("coinType").GetInt32();
                        device.DispenseCoin(coinType, 1);
                        break;
                    case "dispense_amount":
                        int amount = json.RootElement.GetProperty("amount").GetInt32();
                        refundManager.RefundAmount(amount);
                        break;
                    case "reset":
                        device.ResetAllTubes();
                        BroadcastAsync(JsonSerializer.Serialize(new { type = "reset" }));
                        break;
                    case "toggle_accept":
                        bool enabled = json.RootElement.GetProperty("enabled").GetBoolean();
                        device.CoinInputEnabled = enabled;
                        break;


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

        private async void BroadcastAsync(string message)
        {
            var bytes = Encoding.UTF8.GetBytes(message);
            List<WebSocket> targets;
            lock (clients) targets = clients.ToList();

            foreach (var ws in targets)
            {
                if (ws.State == WebSocketState.Open)
                {
                    try { await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None); }
                    catch { }
                }
            }
        }
    }
}
