using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json.Linq;

namespace GatTelemetry;

internal sealed class TruckOverlay041 : Form
{
    private readonly HttpClient _http = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
    private readonly Timer _timer = new Timer { Interval = 450 };

    private readonly Panel _header = new Panel();
    private readonly Label _title = new Label();
    private readonly Label _sub = new Label();
    private readonly Label _connected = new Label();
    private readonly Button _close = new Button();

    private readonly Label _speedCaption = new Label();
    private readonly Label _speed = new Label();
    private readonly Label _limitCaption = new Label();
    private readonly Label _limit = new Label();
    private readonly Label _toleranceCaption = new Label();
    private readonly Label _tolerance = new Label();
    private readonly Label _saved = new Label();

    private readonly Panel _basicPanel = new Panel();
    private readonly Label _gear = new Label();
    private readonly Label _fuel = new Label();
    private readonly Label _cruise = new Label();
    private readonly Label _route = new Label();
    private readonly Label _condition = new Label();

    private readonly Panel _jobPanel = new Panel();
    private readonly Label _cargo = new Label();
    private readonly Label _destination = new Label();
    private readonly Label _weight = new Label();
    private readonly Label _remaining = new Label();

    private readonly Panel _etaPanel = new Panel();
    private readonly Label _eta = new Label();
    private readonly Label _average = new Label();

    private readonly Panel _damagePanel = new Panel();
    private readonly Label _truckDamage = new Label();
    private readonly Label _trailerDamage = new Label();
    private readonly Label _cargoDamage = new Label();

    private readonly Label _hint = new Label();
    private bool _busy;
    private double _avgSpeed;
    private double _toleranceValue = 3;

    internal TruckOverlay041()
    {
        Text = "GAT DASH • Sobreposição do caminhão";
        StartPosition = FormStartPosition.Manual;
        Size = new Size(650, 720);
        MinimumSize = new Size(330, 250);
        Location = new Point(Math.Max(0, Screen.PrimaryScreen.WorkingArea.Right - 690), 70);
        RestoreOverlayBounds053("truck-overlay-bounds-v1.txt");
        BackColor = Color.FromArgb(3, 13, 25);
        ForeColor = Color.White;
        TopMost = true;
        ShowInTaskbar = true;
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimizeBox = true;
        MaximizeBox = false;
        ControlBox = true;
        Padding = new Padding(1);
        Font = new Font("Segoe UI", 9f);
        DoubleBuffered = true;
        _toleranceValue = LoadTolerance045();
        BuildUi();
        // GAT_DRAG_050: permite arrastar pelo topo mesmo quando o clique cai nos Labels filhos.
        EnableDrag050(_header);
        EnableDrag050(_title);
        EnableDrag050(_sub);
        EnableDrag050(_connected);
        Resize += delegate { LayoutResponsive045(); Invalidate(); };
        Paint += PaintBorder045;
        _timer.Tick += async delegate { await PollAsync(); };
        Shown += delegate { LayoutResponsive045(); _timer.Start(); };
        FormClosing += delegate { SaveOverlayBounds053("truck-overlay-bounds-v1.txt"); };
        FormClosed += delegate { _timer.Stop(); _timer.Dispose(); _http.Dispose(); };
    }

    // GAT_OVERLAY_BOUNDS_053
    private void RestoreOverlayBounds053(string fileName)
    {
        try
        {
            string path = Path.Combine(Application.LocalUserAppDataPath, fileName);
            if (!File.Exists(path)) return;
            string[] p = File.ReadAllText(path).Split('|');
            if (p.Length != 4) return;
            int x, y, w, h;
            if (!int.TryParse(p[0], out x) || !int.TryParse(p[1], out y) || !int.TryParse(p[2], out w) || !int.TryParse(p[3], out h)) return;
            w = Math.Max(MinimumSize.Width, w);
            h = Math.Max(MinimumSize.Height, h);
            Rectangle wanted = new Rectangle(x, y, w, h);
            bool visible = false;
            foreach (Screen s in Screen.AllScreens)
            {
                Rectangle hit = Rectangle.Intersect(s.WorkingArea, wanted);
                if (hit.Width >= 80 && hit.Height >= 60) { visible = true; break; }
            }
            if (visible) Bounds = wanted;
        }
        catch { }
    }

    private void SaveOverlayBounds053(string fileName)
    {
        try
        {
            Rectangle b = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
            if (b.Width < MinimumSize.Width || b.Height < MinimumSize.Height) return;
            string path = Path.Combine(Application.LocalUserAppDataPath, fileName);
            File.WriteAllText(path, string.Join("|", b.X, b.Y, b.Width, b.Height));
        }
        catch { }
    }
    private void BuildUi()
    {
        _header.BackColor = Color.FromArgb(5, 22, 39);
        _header.Height = 70;
        _header.Dock = DockStyle.Top;
        Controls.Add(_header);

        _title.Text = "GAT DASH";
        _title.Left = 22; _title.Top = 11; _title.Width = 220; _title.Height = 30;
        _title.ForeColor = Color.FromArgb(44, 164, 255);
        _title.Font = new Font("Segoe UI Black", 18f, FontStyle.Bold | FontStyle.Italic);
        _header.Controls.Add(_title);

        _sub.Text = "ACOMPANHAMENTO EM TEMPO REAL";
        _sub.Left = 23; _sub.Top = 40; _sub.Width = 290; _sub.Height = 18;
        _sub.ForeColor = Color.FromArgb(165, 194, 222);
        _sub.Font = new Font("Segoe UI Semibold", 8.5f);
        _header.Controls.Add(_sub);

        _connected.Text = "● Conectando ao GAT";
        _connected.Width = 190; _connected.Height = 30; _connected.Top = 19;
        _connected.TextAlign = ContentAlignment.MiddleRight;
        _connected.ForeColor = Color.FromArgb(105, 230, 145);
        _connected.Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold);
        _connected.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _header.Controls.Add(_connected);

        _close.Text = "×";
        _close.Width = 38; _close.Height = 34; _close.Top = 17;
        _close.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _close.FlatStyle = FlatStyle.Flat;
        _close.FlatAppearance.BorderColor = Color.FromArgb(31, 82, 122);
        _close.BackColor = Color.FromArgb(6, 31, 52);
        _close.ForeColor = Color.FromArgb(222, 238, 252);
        _close.Font = new Font("Segoe UI Semibold", 13f, FontStyle.Bold);
        _close.Cursor = Cursors.Hand;
        _close.Click += delegate { Close(); };
        _close.Visible = false;

        Controls.Add(_speedCaption); Controls.Add(_speed);
        Controls.Add(_limitCaption); Controls.Add(_limit);
        Controls.Add(_toleranceCaption); Controls.Add(_tolerance); Controls.Add(_saved);

        Caption045(_speedCaption, "VELOCIDADE ATUAL");
        _speed.Text = "0 km/h";
        _speed.ForeColor = Color.White;
        _speed.Font = new Font("Segoe UI Semibold", 38f, FontStyle.Bold);
        _speed.TextAlign = ContentAlignment.MiddleLeft;

        Caption045(_limitCaption, "LIMITE DE VELOCIDADE");
        _limit.Text = "— km/h";
        _limit.ForeColor = Color.FromArgb(255, 111, 111);
        _limit.Font = new Font("Segoe UI Semibold", 22f, FontStyle.Bold);
        _limit.TextAlign = ContentAlignment.MiddleCenter;

        Caption045(_toleranceCaption, "TOLERÂNCIA");
        _tolerance.Text = "+3 km/h";
        _tolerance.ForeColor = Color.White;
        _tolerance.Font = new Font("Segoe UI Semibold", 18f, FontStyle.Bold);
        _tolerance.TextAlign = ContentAlignment.MiddleCenter;
        _saved.Text = "✓ Configurações salvas";
        _saved.ForeColor = Color.FromArgb(87, 229, 140);
        _saved.Font = new Font("Segoe UI Semibold", 8.5f);
        _saved.TextAlign = ContentAlignment.MiddleCenter;

        _basicPanel.BackColor = Color.Transparent;
        Controls.Add(_basicPanel);
        foreach (var l in new[] { _gear, _fuel, _cruise, _route, _condition })
        {
            StyleTile045(l);
            _basicPanel.Controls.Add(l);
        }
        _gear.Text = "MARCHA\r\nN";
        _fuel.Text = "COMBUSTÍVEL\r\n—";
        _cruise.Text = "CRUISE\r\nOFF";
        _route.Text = "ROTA\r\n—";
        _condition.Text = "CONDIÇÃO\r\n100%";

        _jobPanel.BackColor = Color.Transparent;
        Controls.Add(_jobPanel);
        foreach (var l in new[] { _cargo, _destination, _weight, _remaining })
        {
            StyleInfo045(l);
            _jobPanel.Controls.Add(l);
        }
        _cargo.Text = "CARGA\r\nSem carga";
        _destination.Text = "DESTINO\r\n—";
        _weight.Text = "PESO\r\n—";
        _remaining.Text = "DIST. RESTANTE\r\n—";

        _etaPanel.BackColor = Color.Transparent;
        Controls.Add(_etaPanel);
        foreach (var l in new[] { _eta, _average })
        {
            StyleInfo045(l);
            _etaPanel.Controls.Add(l);
        }
        _eta.Text = "TEMPO ESTIMADO\r\n—";
        _average.Text = "VELOCIDADE MÉDIA\r\n—";

        _damagePanel.BackColor = Color.Transparent;
        Controls.Add(_damagePanel);
        foreach (var l in new[] { _truckDamage, _trailerDamage, _cargoDamage })
        {
            StyleTile045(l);
            _damagePanel.Controls.Add(l);
        }
        _truckDamage.Text = "DANOS DO CAMINHÃO\r\n0%";
        _trailerDamage.Text = "DANOS DO REBOQUE\r\n0%";
        _cargoDamage.Text = "DANOS DA CARGA\r\n0%";

        _hint.Text = "Mova pela barra da janela • redimensione pelas bordas.";
        _hint.Height = 28;
        _hint.ForeColor = Color.FromArgb(125, 153, 182);
        _hint.TextAlign = ContentAlignment.MiddleRight;
        _hint.Font = new Font("Segoe UI", 8f);
        _hint.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
        Controls.Add(_hint);

        LayoutResponsive045();
    }

    private static void Caption045(Label l, string text)
    {
        l.Text = text;
        l.ForeColor = Color.FromArgb(158, 194, 224);
        l.Font = new Font("Segoe UI Semibold", 8.5f);
        l.TextAlign = ContentAlignment.MiddleCenter;
    }

    private static void StyleTile045(Label l)
    {
        l.BackColor = Color.FromArgb(7, 29, 50);
        l.ForeColor = Color.FromArgb(220, 235, 250);
        l.TextAlign = ContentAlignment.MiddleCenter;
        l.Font = new Font("Segoe UI Semibold", 9.2f, FontStyle.Bold);
        l.BorderStyle = BorderStyle.FixedSingle;
    }

    private static void StyleInfo045(Label l)
    {
        l.BackColor = Color.FromArgb(6, 27, 47);
        l.ForeColor = Color.FromArgb(226, 238, 250);
        l.TextAlign = ContentAlignment.MiddleLeft;
        l.Font = new Font("Segoe UI Semibold", 9.4f, FontStyle.Bold);
        l.Padding = new Padding(14, 0, 10, 0);
        l.BorderStyle = BorderStyle.FixedSingle;
    }

    // GAT_DRAG_050
    private const int WM_NCLBUTTONDOWN_050 = 0x00A1;
    private const int HTCAPTION_050 = 2;

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    private void EnableDrag050(Control control)
    {
        if (control == null) return;
        control.Cursor = Cursors.SizeAll;
        control.MouseDown += delegate(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            ReleaseCapture();
            SendMessage(Handle, WM_NCLBUTTONDOWN_050, (IntPtr)HTCAPTION_050, IntPtr.Zero);
        };
    }
    private void LayoutResponsive045()
    {
        int w = Math.Max(300, ClientSize.Width);
        int h = Math.Max(220, ClientSize.Height);
        int pad = 16;
        int usable = w - pad * 2;

        _connected.Left = Math.Max(260, w - 255);
        _close.Left = Math.Max(0, w - 52);

        int top = 82;
        bool narrow = w < 520;
        int third = Math.Max(80, usable / 3);

        if (!narrow)
        {
            _speedCaption.SetBounds(pad, top, third + 35, 20);
            _speed.SetBounds(pad, top + 20, third + 35, 70);
            _limitCaption.SetBounds(pad + third + 38, top, third - 12, 20);
            _limit.SetBounds(pad + third + 38, top + 20, third - 12, 70);
            _toleranceCaption.SetBounds(pad + third * 2 + 28, top, w - (pad + third * 2 + 28) - pad, 20);
            _tolerance.SetBounds(pad + third * 2 + 28, top + 19, w - (pad + third * 2 + 28) - pad, 44);
            _saved.SetBounds(pad + third * 2 + 28, top + 59, w - (pad + third * 2 + 28) - pad, 30);
            top += 106;
        }
        else
        {
            _speedCaption.SetBounds(pad, top, usable / 2, 20);
            _speed.SetBounds(pad, top + 18, usable / 2, 64);
            _limitCaption.SetBounds(pad + usable / 2, top, usable / 2, 20);
            _limit.SetBounds(pad + usable / 2, top + 18, usable / 2, 64);
            _toleranceCaption.SetBounds(pad, top + 83, usable / 2, 19);
            _tolerance.SetBounds(pad, top + 101, usable / 2, 37);
            _saved.SetBounds(pad + usable / 2, top + 90, usable / 2, 45);
            top += 150;
        }

        _basicPanel.SetBounds(pad, top, usable, narrow ? 145 : 82);
        int gap = 8;
        if (!narrow)
        {
            int bw = (usable - gap * 4) / 5;
            Label[] basics = { _gear, _fuel, _cruise, _route, _condition };
            for (int i = 0; i < basics.Length; i++) basics[i].SetBounds(i * (bw + gap), 0, bw, 76);
        }
        else
        {
            int bw = (usable - gap) / 2;
            _gear.SetBounds(0, 0, bw, 62);
            _fuel.SetBounds(bw + gap, 0, bw, 62);
            _cruise.SetBounds(0, 70, bw, 62);
            _route.SetBounds(bw + gap, 70, bw, 62);
            _condition.SetBounds(0, 140, usable, 62);
            _basicPanel.Height = 207;
        }
        top = _basicPanel.Bottom + 8;

        bool showJob = h >= (narrow ? 600 : 470);
        bool showEta = h >= (narrow ? 770 : 575);
        bool showDamage = h >= (narrow ? 900 : 675);

        _jobPanel.Visible = showJob;
        _etaPanel.Visible = showEta;
        _damagePanel.Visible = showDamage;

        if (showJob)
        {
            _jobPanel.SetBounds(pad, top, usable, 124);
            int half = (usable - gap) / 2;
            _cargo.SetBounds(0, 0, half, 52);
            _destination.SetBounds(half + gap, 0, half, 52);
            _weight.SetBounds(0, 60, half, 60);
            _remaining.SetBounds(half + gap, 60, half, 60);
            top = _jobPanel.Bottom + 8;
        }
        if (showEta)
        {
            _etaPanel.SetBounds(pad, top, usable, 82);
            int half = (usable - gap) / 2;
            _eta.SetBounds(0, 0, half, 76);
            _average.SetBounds(half + gap, 0, half, 76);
            top = _etaPanel.Bottom + 8;
        }
        if (showDamage)
        {
            _damagePanel.SetBounds(pad, top, usable, 80);
            int dw = (usable - gap * 2) / 3;
            _truckDamage.SetBounds(0, 0, dw, 74);
            _trailerDamage.SetBounds(dw + gap, 0, dw, 74);
            _cargoDamage.SetBounds((dw + gap) * 2, 0, usable - (dw + gap) * 2, 74);
        }

        _hint.SetBounds(pad, Math.Max(top + 4, h - 35), usable, 26);
    }

    private async Task PollAsync()
    {
        if (_busy) return;
        _busy = true;
        try
        {
            string text = await _http.GetStringAsync("http://127.0.0.1:31377/api/ets2/telemetry");
            JObject j = JObject.Parse(text);
            double speed = Math.Abs(D(j, "truck.speed", "Truck.Speed")); if (!IsValidRoadSpeed047(speed)) speed = 0;
            double limit = D(j, "navigation.speedLimit", "Navigation.SpeedLimit");
            double fuel = D(j, "truck.fuel", "Truck.Fuel");
            double cap = D(j, "truck.fuelCapacity", "Truck.FuelCapacity");
            double remain = D(j, "navigation.estimatedDistance", "Navigation.EstimatedDistance") / 1000.0;
            double cargoMass = D(j, "job.cargoMass", "Job.CargoMass");
            int gear = (int)Math.Round(D(j, "truck.displayedGear", "Truck.DisplayedGear"));
            bool cruiseOn = B(j, "truck.cruiseControlOn", "Truck.CruiseControlOn");
            double cruise = D(j, "truck.cruiseControlSpeed", "Truck.CruiseControlSpeed");

            _toleranceValue = LoadTolerance045();
            if (IsValidRoadSpeed047(speed) && speed > 5) _avgSpeed = _avgSpeed <= 0 ? speed : (_avgSpeed * 0.94 + speed * 0.06);

            bool over = limit > 0 && speed > limit + _toleranceValue;
            _connected.Text = "● Conectado ao GAT";
            _connected.ForeColor = Color.FromArgb(87, 229, 140);
            _speed.Text = Math.Round(speed) + " km/h";
            _speed.ForeColor = over ? Color.FromArgb(255, 119, 119) : Color.White;
            _limit.Text = (limit > 0 ? Math.Round(limit).ToString(CultureInfo.InvariantCulture) : "—") + " km/h";
            _limit.ForeColor = over ? Color.FromArgb(255, 76, 76) : Color.FromArgb(255, 132, 132);
            _tolerance.Text = "+" + Math.Round(_toleranceValue) + " km/h";
            _gear.Text = "MARCHA\r\n" + (gear < 0 ? "R" + Math.Abs(gear) : gear == 0 ? "N" : "D" + gear);
            _fuel.Text = "COMBUSTÍVEL\r\n" + (cap > 0 ? Math.Round(Clamp045(fuel / cap * 100, 0, 100)) + "%" : "—");
            _cruise.Text = "CRUISE\r\n" + (cruiseOn ? Math.Round(cruise) + " km/h" : "OFF");
            _route.Text = "ROTA\r\n" + (remain > 0 ? Math.Round(remain) + " km" : "—");
            int overall = Overall(j);
            _condition.Text = "CONDIÇÃO\r\n" + overall + "%";

            string cargo = S(j, "job.cargo", "Job.Cargo");
            string destination = S(j, "job.destinationCity", "Job.DestinationCity");
            _cargo.Text = "CARGA\r\n" + (string.IsNullOrWhiteSpace(cargo) ? "Sem carga" : cargo);
            _destination.Text = "DESTINO\r\n" + (string.IsNullOrWhiteSpace(destination) ? "—" : destination);
            _weight.Text = "PESO\r\n" + (cargoMass > 0 ? FormatWeight045(cargoMass) : "—");
            _remaining.Text = "DIST. RESTANTE\r\n" + (remain > 0 ? Math.Round(remain) + " km" : "—");
            _average.Text = "VELOCIDADE MÉDIA\r\n" + (IsValidRoadSpeed047(_avgSpeed) && _avgSpeed > 1 ? Math.Round(_avgSpeed) + " km/h" : "—");
            _eta.Text = "TEMPO ESTIMADO\r\n" + Eta045(remain, _avgSpeed);

            double truckDamage = 100 - overall;
            double trailerDamage = TrailerDamage045(j);
            double cargoDamage = D(j, "job.cargoDamage", "Job.CargoDamage");
            if (cargoDamage <= 1.001) cargoDamage *= 100;
            _truckDamage.Text = "DANOS DO CAMINHÃO\r\n" + Math.Round(Clamp045(truckDamage, 0, 100)) + "%";
            _trailerDamage.Text = "DANOS DO REBOQUE\r\n" + Math.Round(Clamp045(trailerDamage, 0, 100)) + "%";
            _cargoDamage.Text = "DANOS DA CARGA\r\n" + Math.Round(Clamp045(cargoDamage, 0, 100)) + "%";
        }
        catch
        {
            _connected.Text = "● ETS2 desconectado";
            _connected.ForeColor = Color.FromArgb(255, 128, 128);
            _speed.Text = "0 km/h";
            _limit.Text = "— km/h";
            _route.Text = "ROTA\r\nETS2 OFF";
        }
        finally { _busy = false; }
    }

    private static string FormatWeight045(double kg)
    {
        if (kg >= 1000) return (kg / 1000.0).ToString("0.#", CultureInfo.InvariantCulture) + " t";
        return Math.Round(kg).ToString(CultureInfo.InvariantCulture) + " kg";
    }

    private static string Eta045(double remainKm, double avgSpeed)
    {
        if (remainKm <= 0 || !IsValidRoadSpeed047(avgSpeed) || avgSpeed < 10) return "—";
        double hours = remainKm / avgSpeed;
        int totalMinutes = (int)Math.Ceiling(hours * 60.0);
        if (totalMinutes < 1) return "< 1 min";
        int h = totalMinutes / 60;
        int m = totalMinutes % 60;
        return h > 0 ? h + " h " + m.ToString("00") + " min" : m + " min";
    }

    private static double TrailerDamage045(JObject j)
    {
        double body = D(j, "trailer.wearBody", "Trailer.WearBody");
        double chassis = D(j, "trailer.wearChassis", "Trailer.WearChassis");
        double wheels = D(j, "trailer.wearWheels", "Trailer.WearWheels");
        if (body <= 1.001) body *= 100;
        if (chassis <= 1.001) chassis *= 100;
        if (wheels <= 1.001) wheels *= 100;
        return Clamp045((body + chassis + wheels) / 3.0, 0, 100);
    }

    private static double LoadTolerance045()
    {
        try
        {
            string f = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GAT-LOG", "GAT-Telemetria", "gat-dash-settings.json");
            if (!File.Exists(f)) return 3;
            JObject o = JObject.Parse(File.ReadAllText(f));
            double t = Convert.ToDouble(o["tolerance"] ?? 3, CultureInfo.InvariantCulture);
            return Clamp045(t, 0, 30);
        }
        catch { return 3; }
    }

    private static string S(JObject j, params string[] paths)
    {
        foreach (string p in paths)
        {
            try { JToken t = j.SelectToken(p); if (t != null) return Convert.ToString(t) ?? ""; }
            catch { }
        }
        return "";
    }

    private static double D(JObject j, params string[] paths)
    {
        foreach (string p in paths)
        {
            try
            {
                JToken t = j.SelectToken(p);
                if (t == null) continue;

                if (t.Type == JTokenType.Integer || t.Type == JTokenType.Float)
                    return t.Value<double>();

                string raw = Convert.ToString(t, CultureInfo.InvariantCulture) ?? "";
                double v;
                if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out v)) return v;
                if (double.TryParse(raw, NumberStyles.Float, new CultureInfo("pt-BR"), out v)) return v;
            }
            catch { }
        }
        return 0;
    }

    private static bool IsValidRoadSpeed047(double value)
    {
        return !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0 && value <= 300;
    }

    private static bool B(JObject j, params string[] paths)
    {
        foreach (string p in paths)
        {
            try { JToken t = j.SelectToken(p); if (t != null) return Convert.ToBoolean(t); }
            catch { }
        }
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
            total += Clamp045(100 - wear, 0, 100); count++;
        }
        return count == 0 ? 100 : (int)Math.Round(total / count);
    }

    private static double Clamp045(double n, double min, double max)
    {
        return Math.Max(min, Math.Min(max, n));
    }

    private void PaintBorder045(object sender, PaintEventArgs e)
    {
        using (var p = new Pen(Color.FromArgb(33, 154, 236), 1.2f))
            e.Graphics.DrawRectangle(p, 0, 0, Math.Max(1, ClientSize.Width - 1), Math.Max(1, ClientSize.Height - 1));
    }

    private const int WM_NCHITTEST_045 = 0x0084;
    private const int HTCLIENT_045 = 1;
    private const int HTCAPTION_045 = 2;
    private const int HTLEFT_045 = 10;
    private const int HTRIGHT_045 = 11;
    private const int HTTOP_045 = 12;
    private const int HTTOPLEFT_045 = 13;
    private const int HTTOPRIGHT_045 = 14;
    private const int HTBOTTOM_045 = 15;
    private const int HTBOTTOMLEFT_045 = 16;
    private const int HTBOTTOMRIGHT_045 = 17;

    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);
        if (m.Msg != WM_NCHITTEST_045 || (int)m.Result != HTCLIENT_045) return;
        long raw = m.LParam.ToInt64();
        int sx = unchecked((short)(raw & 0xffff));
        int sy = unchecked((short)((raw >> 16) & 0xffff));
        Point p = PointToClient(new Point(sx, sy));
        const int grip = 8;
        bool left = p.X <= grip, right = p.X >= ClientSize.Width - grip;
        bool top = p.Y <= grip, bottom = p.Y >= ClientSize.Height - grip;
        if (left && top) m.Result = (IntPtr)HTTOPLEFT_045;
        else if (right && top) m.Result = (IntPtr)HTTOPRIGHT_045;
        else if (left && bottom) m.Result = (IntPtr)HTBOTTOMLEFT_045;
        else if (right && bottom) m.Result = (IntPtr)HTBOTTOMRIGHT_045;
        else if (left) m.Result = (IntPtr)HTLEFT_045;
        else if (right) m.Result = (IntPtr)HTRIGHT_045;
        else if (top) m.Result = (IntPtr)HTTOP_045;
        else if (bottom) m.Result = (IntPtr)HTBOTTOM_045;
        else if (p.Y <= _header.Bottom && !_close.Bounds.Contains(p)) m.Result = (IntPtr)HTCAPTION_045;
    }
}






