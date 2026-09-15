using System;
using System.Drawing;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json.Linq;

namespace GatTelemetry;

internal sealed class TruckOverlay041 : Form
{
    private readonly HttpClient _http = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
    private readonly Timer _timer = new Timer { Interval = 450 };
    private readonly Label _speed = new Label();
    private readonly Label _limit = new Label();
    private readonly Label _gear = new Label();
    private readonly Label _fuel = new Label();
    private readonly Label _cruise = new Label();
    private readonly Label _route = new Label();
    private readonly Label _condition = new Label();
    private bool _busy;

    internal TruckOverlay041()
    {
        Text = "GAT DASH • Informações do caminhão";
        StartPosition = FormStartPosition.Manual;
        Size = new Size(330, 360);
        MinimumSize = new Size(250, 220);
        Location = new Point(Math.Max(0, Screen.PrimaryScreen.WorkingArea.Right - 360), 80);
        BackColor = Color.FromArgb(3, 13, 25);
        ForeColor = Color.White;
        TopMost = true;
        ShowInTaskbar = false;
        FormBorderStyle = FormBorderStyle.SizableToolWindow;
        Font = new Font("Segoe UI", 9f);
        BuildUi();
        _timer.Tick += async delegate { await PollAsync(); };
        Shown += delegate { _timer.Start(); };
        FormClosed += delegate { _timer.Stop(); _timer.Dispose(); _http.Dispose(); };
    }

    private void BuildUi()
    {
        Label title = new Label { Text = "GAT DASH", Left = 14, Top = 10, Width = 150, Height = 25, ForeColor = Color.FromArgb(81, 159, 255), Font = new Font("Segoe UI Semibold", 12f, FontStyle.Bold) };
        Label sub = new Label { Text = "OVERLAY • CAMINHÃO", Left = 15, Top = 34, Width = 200, Height = 18, ForeColor = Color.FromArgb(130, 153, 180), Font = new Font("Segoe UI", 8f) };
        Controls.Add(title); Controls.Add(sub);

        _speed.Left = 14; _speed.Top = 61; _speed.Width = 170; _speed.Height = 72; _speed.Text = "0 km/h"; _speed.ForeColor = Color.White; _speed.Font = new Font("Segoe UI Semibold", 29f, FontStyle.Bold); Controls.Add(_speed);
        _limit.Left = 190; _limit.Top = 68; _limit.Width = 110; _limit.Height = 55; _limit.Text = "LIMITE\n—"; _limit.TextAlign = ContentAlignment.MiddleCenter; _limit.ForeColor = Color.FromArgb(255, 111, 111); _limit.Font = new Font("Segoe UI Semibold", 11f, FontStyle.Bold); _limit.Anchor = AnchorStyles.Top | AnchorStyles.Right; Controls.Add(_limit);

        _gear.Left = 15; _gear.Top = 143; _gear.Width = 125; _gear.Height = 42; _gear.Text = "MARCHA  N";
        _fuel.Left = 151; _fuel.Top = 143; _fuel.Width = 145; _fuel.Height = 42; _fuel.Text = "COMBUSTÍVEL  —"; _fuel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        _cruise.Left = 15; _cruise.Top = 190; _cruise.Width = 125; _cruise.Height = 42; _cruise.Text = "CRUISE  OFF";
        _route.Left = 151; _route.Top = 190; _route.Width = 145; _route.Height = 42; _route.Text = "ROTA  —"; _route.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        _condition.Left = 15; _condition.Top = 242; _condition.Width = 281; _condition.Height = 47; _condition.Text = "CONDIÇÃO  100%"; _condition.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        foreach (Label l in new[] { _gear, _fuel, _cruise, _route, _condition })
        {
            l.BackColor = Color.FromArgb(7, 29, 50); l.ForeColor = Color.FromArgb(219, 231, 247); l.TextAlign = ContentAlignment.MiddleCenter; l.Font = new Font("Segoe UI Semibold", 9f, FontStyle.Bold); l.BorderStyle = BorderStyle.FixedSingle; Controls.Add(l);
        }
        Label hint = new Label { Text = "Arraste a janela e use as bordas para redimensionar.", Left = 15, Top = 302, Width = 285, Height = 32, Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom, ForeColor = Color.FromArgb(121, 148, 180), TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 8f) };
        Controls.Add(hint);
        Resize += delegate
        {
            _limit.Left = Math.Max(145, ClientSize.Width - _limit.Width - 14);
            _fuel.Width = Math.Max(90, ClientSize.Width - _fuel.Left - 14);
            _route.Width = Math.Max(90, ClientSize.Width - _route.Left - 14);
            _condition.Width = Math.Max(180, ClientSize.Width - 30);
            hint.Width = Math.Max(180, ClientSize.Width - 30);
            hint.Top = Math.Max(292, ClientSize.Height - hint.Height - 8);
        };
    }

    private async Task PollAsync()
    {
        if (_busy) return;
        _busy = true;
        try
        {
            string text = await _http.GetStringAsync("http://127.0.0.1:31377/api/ets2/telemetry");
            JObject j = JObject.Parse(text);
            double speed = Math.Abs(D(j, "truck.speed", "Truck.Speed"));
            double limit = D(j, "navigation.speedLimit", "Navigation.SpeedLimit");
            double fuel = D(j, "truck.fuel", "Truck.Fuel");
            double cap = D(j, "truck.fuelCapacity", "Truck.FuelCapacity");
            double remain = D(j, "navigation.estimatedDistance", "Navigation.EstimatedDistance") / 1000.0;
            int gear = (int)Math.Round(D(j, "truck.displayedGear", "Truck.DisplayedGear"));
            bool cruiseOn = B(j, "truck.cruiseControlOn", "Truck.CruiseControlOn");
            double cruise = D(j, "truck.cruiseControlSpeed", "Truck.CruiseControlSpeed");
            _speed.Text = Math.Round(speed) + " km/h";
            _limit.Text = "LIMITE\n" + (limit > 0 ? Math.Round(limit).ToString() : "—");
            _gear.Text = "MARCHA  " + (gear < 0 ? "R" + Math.Abs(gear) : gear == 0 ? "N" : "D" + gear);
            _fuel.Text = "COMBUSTÍVEL  " + (cap > 0 ? Math.Round(Math.Max(0, Math.Min(100, fuel / cap * 100))) + "%" : "—");
            _cruise.Text = "CRUISE  " + (cruiseOn ? Math.Round(cruise) + " km/h" : "OFF");
            _route.Text = "ROTA  " + (remain > 0 ? Math.Round(remain) + " km" : "—");
            _condition.Text = "CONDIÇÃO  " + Overall(j) + "%";
        }
        catch
        {
            _speed.Text = "0 km/h"; _limit.Text = "LIMITE\n—"; _route.Text = "ETS2 OFF";
        }
        finally { _busy = false; }
    }

    private static double D(JObject j, params string[] paths)
    {
        foreach (string p in paths)
        {
            try { JToken t = j.SelectToken(p); if (t != null && double.TryParse(Convert.ToString(t), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double v)) return v; }
            catch { }
        }
        return 0;
    }
    private static bool B(JObject j, params string[] paths)
    {
        foreach (string p in paths) { try { JToken t = j.SelectToken(p); if (t != null) return Convert.ToBoolean(t); } catch { } }
        return false;
    }
    private static int Overall(JObject j)
    {
        string[] names = { "Engine", "Transmission", "Cabin", "Chassis", "Wheels" };
        double total = 0; int count = 0;
        foreach (string n in names)
        {
            double wear = D(j, "truck.wear" + n, "Truck.Wear" + n);
            if (wear <= 1.001) wear *= 100;
            total += Math.Max(0, Math.Min(100, 100 - wear)); count++;
        }
        return count == 0 ? 100 : (int)Math.Round(total / count);
    }
}
