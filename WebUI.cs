using System;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace MDBControllerLib
{
    // Very small embedded HTTP UI (no external deps). Serves a single page and two API endpoints.
    internal class WebUI : IDisposable
    {
        private readonly HttpListener listener;
        private readonly MDBDevice device;
        private bool running = false;

        public WebUI(MDBDevice device, string prefix = "http://localhost:8080/")
        {
            this.device = device ?? throw new ArgumentNullException(nameof(device));
            listener = new HttpListener();
            listener.Prefixes.Add(prefix);
        }

        public void Start()
        {
            running = true;
            listener.Start();
            Task.Run(() => Loop());
            Console.WriteLine("Web UI available at http://localhost:8080/");
        }

        private async Task Loop()
        {
            while (running)
            {
                try
                {
                    var ctx = await listener.GetContextAsync();
                    _ = Task.Run(() => Handle(ctx));
                }
                catch (HttpListenerException) { break; }
                catch (Exception) { break; }
            }
        }

        private void Handle(HttpListenerContext ctx)
        {
            try
            {
                var req = ctx.Request;
                var resp = ctx.Response;

                if (req.HttpMethod == "GET" && req.Url.AbsolutePath == "/")
                {
                    var html = GetHTML();
                    var data = Encoding.UTF8.GetBytes(html);
                    resp.ContentType = "text/html; charset=utf-8";
                    resp.OutputStream.Write(data, 0, data.Length);
                    resp.Close();
                    return;
                }

                if (req.HttpMethod == "GET" && (req.Url?.AbsolutePath ?? "/") == "/status")
                {
                    var status = new {
                        coins = device.CoinTypeValues,
                        tubes = device.TubeCounts,
                        lastEvent = device.LastEvent
                    };
                    var json = JsonSerializer.Serialize(status);
                    var data = Encoding.UTF8.GetBytes(json);
                    resp.ContentType = "application/json";
                    resp.OutputStream.Write(data, 0, data.Length);
                    resp.Close();
                    return;
                }

                if (req.HttpMethod == "POST" && (req.Url?.AbsolutePath ?? "/") == "/refund")
                {
                    using var sr = new System.IO.StreamReader(req.InputStream);
                    var body = sr.ReadToEnd();
                    try
                    {
                        var doc = JsonDocument.Parse(body);
                        if (doc.RootElement.TryGetProperty("amount", out var amt))
                        {
                            int amount = amt.GetInt32();
                            if (device.TryRefund(amount, out var sel))
                            {
                                var json = JsonSerializer.Serialize(new { ok = true, selection = sel });
                                var data = Encoding.UTF8.GetBytes(json);
                                resp.ContentType = "application/json";
                                resp.OutputStream.Write(data, 0, data.Length);
                                resp.Close();
                                return;
                            }
                            else
                            {
                                var json = JsonSerializer.Serialize(new { ok = false, error = "cannot make exact change or no coin map" });
                                var data = Encoding.UTF8.GetBytes(json);
                                resp.ContentType = "application/json";
                                resp.OutputStream.Write(data, 0, data.Length);
                                resp.StatusCode = 400;
                                resp.Close();
                                return;
                            }
                        }
                    }
                    catch { }

                    resp.StatusCode = 400;
                    resp.Close();
                    return;
                }

                resp.StatusCode = 404;
                resp.Close();
            }
            catch { }
        }

        private string GetHTML()
        {
            return @"<!doctype html>
<html>
<head>
  <meta charset='utf-8'/>
  <title>MDB Changer UI</title>
  <style>body{font-family:Arial,Helvetica,sans-serif;margin:20px}label{display:block;margin-top:8px}</style>
</head>
<body>
  <h2>MDB Cash Changer</h2>
  <div id='status'>Loading...</div>

  <h3>Refund</h3>
  <p>Enter amount in device units (see coin mapping above).</p>
  <label>Amount <input id='amount' type='number' min='1'/></label>
  <button id='refund'>Refund</button>
  <div id='result'></div>

  <script>
        async function refresh(){
            let r = await fetch('/status');
            let j = await r.json();
            let out = '<strong>Coin mappings:</strong><ul>';
            for (const [k,v] of Object.entries(j.coins)){
                out += `<li>type ${k} -> ${v} units</li>`;
            }
            out += '</ul>';

            // Tubes (if reported)
            if (j.tubes) {
                out += '<strong>Tube counts:</strong><ul>';
                let totalUnits = 0;
                for (const [k,c] of Object.entries(j.tubes)){
                    const coinVal = j.coins && j.coins[k] ? j.coins[k] : 0;
                    out += `<li>type ${k} -> ${c} coins (value ${coinVal} each)</li>`;
                    totalUnits += coinVal * (Number(c) || 0);
                }
                out += '</ul>';
                out += `<div><strong>Total available units:</strong> ${totalUnits}</div>`;
            }

            out += '<strong>Last event:</strong> ' + (j.lastEvent ?? 'none');
            document.getElementById('status').innerHTML = out;
        }

    document.getElementById('refund').addEventListener('click', async ()=>{
      let amount = parseInt(document.getElementById('amount').value || '0');
      document.getElementById('result').textContent = 'Processing...';
      let r = await fetch('/refund',{method:'POST',headers:{'content-type':'application/json'},body:JSON.stringify({amount:amount})});
      let j = await r.json();
      if (r.ok){
        document.getElementById('result').textContent = 'Refunded using: ' + JSON.stringify(j.selection);
      } else {
        document.getElementById('result').textContent = 'Error: ' + (j.error || 'unknown');
      }
      await refresh();
    });

    refresh();
    setInterval(refresh,3000);
  </script>
</body>
</html>";
        }

        public void Dispose()
        {
            running = false;
            try { listener.Stop(); } catch { }
            listener.Close();
        }
    }
}
