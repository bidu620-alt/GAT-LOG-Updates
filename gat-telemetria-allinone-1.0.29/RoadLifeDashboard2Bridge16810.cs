using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Newtonsoft.Json.Linq;

namespace GatTelemetry;

internal sealed partial class MainForm
{
    private RoadLifeDashboard2Form _roadLifeDashboard2Form;

    private void RoadLifeDashboard2Initialize()
    {
        FormClosed += delegate
        {
            try { CloseRoadLifeDashboard2(); } catch { }
        };
    }

    private bool RoadLifeDashboard2Running()
    {
        return _roadLifeDashboard2Form != null && !_roadLifeDashboard2Form.IsDisposed && _roadLifeDashboard2Form.Visible;
    }

    private void ToggleRoadLifeDashboard2()
    {
        if (RoadLifeDashboard2Running()) CloseRoadLifeDashboard2();
        else OpenRoadLifeDashboard2();
    }

    private void OpenRoadLifeDashboard2()
    {
        try
        {
            if (_roadLifeDashboard2Form == null || _roadLifeDashboard2Form.IsDisposed)
            {
                _roadLifeDashboard2Form = new RoadLifeDashboard2Form();
                _roadLifeDashboard2Form.FormClosed += delegate { _roadLifeDashboard2Form = null; };
            }

            if (!_roadLifeDashboard2Form.Visible)
                _roadLifeDashboard2Form.Show();

            _roadLifeDashboard2Form.BringToFront();
            ClientStore.Log("RoadLife Dashboard2 aberto dentro do GAT Telemetria.");
        }
        catch (Exception ex)
        {
            ClientStore.Log("RoadLife Dashboard2 abrir: " + ex.Message);
            MessageBox.Show(ex.Message, "ROADLIFE DASHBOARD2", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void CloseRoadLifeDashboard2()
    {
        try
        {
            if (_roadLifeDashboard2Form != null && !_roadLifeDashboard2Form.IsDisposed)
                _roadLifeDashboard2Form.Close();
        }
        catch { }
        _roadLifeDashboard2Form = null;
    }

    // O JObject ja foi captado pelo GAT Telemetria.
    // O Dashboard2 recebe SOMENTE a informacao pronta e desenha o painel.
    private void RoadLifeDashboard2Push(JObject tele)
    {
        if (tele == null || !RoadLifeDashboard2Running()) return;
        try
        {
            JObject payload = RoadLifeDashboard2BuildPayload(tele);
            RoadLifeDashboard2Form target = _roadLifeDashboard2Form;
            if (target == null || target.IsDisposed) return;

            if (target.InvokeRequired)
                target.BeginInvoke(new Action(delegate { if (!target.IsDisposed) target.ApplyTelemetry(payload); }));
            else
                target.ApplyTelemetry(payload);
        }
        catch (Exception ex)
        {
            ClientStore.Log("RoadLife Dashboard2 dados: " + ex.Message);
        }
    }

    private static JObject RoadLifeDashboard2BuildPayload(JObject tele)
    {
        string cargo = RoadLifeText(tele, "cargo_name", "job.cargoName", "job.cargo", "Job.CargoName");
        string source = RoadLifeText(tele, "source_city", "job.sourceCity", "Job.SourceCity");
        string destination = RoadLifeText(tele, "destination_city", "job.destinationCity", "Job.DestinationCity");

        double massKg = RoadLifeNumber(tele, "mass_kg", "cargoMass", "cargo_mass", "job.cargoMass", "Job.CargoMass");
        double remaining = RoadLifeNumber(tele, "remaining_km", "remainingKm", "navigation.estimatedDistance", "Navigation.EstimatedDistance");
        double planned = RoadLifeNumber(tele, "planned_distance_km", "plannedDistanceKm", "job.plannedDistanceKm", "Job.PlannedDistanceKm");
        double travelled = !double.IsNaN(planned) && !double.IsNaN(remaining) ? Math.Max(0, planned - remaining) : 0;

        double fuel = RoadLifeNumber(tele, "truck.fuel", "Truck.Fuel", "fuel", "fuelAmount", "fuelLiters");
        double fuelCapacity = RoadLifeNumber(tele, "truck.fuelCapacity", "Truck.FuelCapacity", "fuelCapacity", "fuel_capacity");
        double fuelPct = !double.IsNaN(fuel) && !double.IsNaN(fuelCapacity) && fuelCapacity > 0
            ? fuel / fuelCapacity * 100.0
            : RoadLifeNumber(tele, "truck.fuelPercent", "fuelPercent", "fuel_percent");

        double speed = RoadLifeNumber(tele, "truck.speed", "Truck.Speed", "speed", "speed_kmh");
        double temperature = RoadLifeNumber(tele, "truck.waterTemperature", "Truck.WaterTemperature", "waterTemperature", "water_temperature", "truck.engineTemperature");
        double speedLimit = RoadLifeNumber(tele, "navigation.speedLimit", "Navigation.SpeedLimit", "speedLimit", "speed_limit_kmh");
        double cruiseSpeed = RoadLifeNumber(tele, "truck.cruiseControlSpeed", "Truck.CruiseControlSpeed", "cruiseControlSpeed", "cruise_speed");
        bool cruise = RoadLifeBool(tele, "truck.cruiseControl", "truck.cruiseControlOn", "cruiseControl", "cruise")
            || (!double.IsNaN(cruiseSpeed) && cruiseSpeed > 0.1);
        double odometer = RoadLifeNumber(tele, "truck.odometer", "Truck.Odometer", "odometer", "odometerKm", "odometer_km");

        string model = RoadLifeText(tele, "truck.model", "Truck.Model", "truck.name", "Truck.Name", "truckModel", "truck_model");
        string make = RoadLifeText(tele, "truck.make", "Truck.Make", "truck.brand", "Truck.Brand", "truckMake");
        if (string.IsNullOrWhiteSpace(model)) model = make;
        else if (!string.IsNullOrWhiteSpace(make) && model.IndexOf(make, StringComparison.OrdinalIgnoreCase) < 0) model = make + " " + model;

        double cargoDamage = RoadLifeDamagePct(RoadLifeNumber(tele, "cargoDamage", "cargo_damage", "job.cargoDamage", "Job.CargoDamage"));
        double trailerDamage = RoadLifeDamagePct(RoadLifeNumber(tele, "trailerDamage", "trailer_damage", "trailer.wear", "trailer.damage"));
        double truckDamage = RoadLifeTruckDamagePct(tele);

        return new JObject
        {
            ["online"] = true,
            ["route"] = new JObject
            {
                ["source"] = string.IsNullOrWhiteSpace(source) ? "—" : source,
                ["destination"] = string.IsNullOrWhiteSpace(destination) ? "—" : destination,
                ["distance_remaining_km"] = RoadLifeZero(remaining),
                ["distance_travelled_km"] = RoadLifeZero(travelled)
            },
            ["cargo"] = new JObject
            {
                ["name"] = string.IsNullOrWhiteSpace(cargo) ? "Sem carga" : cargo,
                ["weight_t"] = double.IsNaN(massKg) ? 0.0 : Math.Max(0, massKg / 1000.0)
            },
            ["fuel"] = new JObject
            {
                ["current_l"] = RoadLifeZero(fuel),
                ["capacity_l"] = RoadLifeZero(fuelCapacity),
                ["percent"] = double.IsNaN(fuelPct) ? 0.0 : Math.Max(0, Math.Min(100, fuelPct))
            },
            ["truck"] = new JObject
            {
                ["model"] = string.IsNullOrWhiteSpace(model) ? "—" : model,
                ["gear"] = RoadLifeGear(tele),
                ["temperature_c"] = RoadLifeZero(temperature),
                ["speed_kmh"] = RoadLifeZero(Math.Abs(speed)),
                ["speed_limit_kmh"] = RoadLifeZero(speedLimit),
                ["cruise"] = cruise,
                ["odometer_km"] = RoadLifeZero(odometer)
            },
            ["eta"] = RoadLifeEta(tele),
            ["damage"] = new JObject
            {
                ["truck_pct"] = truckDamage,
                ["trailer_pct"] = trailerDamage,
                ["cargo_pct"] = cargoDamage
            }
        };
    }

    private static string RoadLifeText(JObject root, params string[] paths)
    {
        if (root == null) return string.Empty;
        foreach (string path in paths ?? new string[0])
        {
            try
            {
                JToken t = root.SelectToken(path, false);
                if (t == null || t.Type == JTokenType.Null) continue;
                string s = (Convert.ToString(t, CultureInfo.InvariantCulture) ?? string.Empty).Trim();
                if (s.Length > 0) return s;
            }
            catch { }
        }
        return string.Empty;
    }

    private static double RoadLifeNumber(JObject root, params string[] paths)
    {
        double value;
        return double.TryParse(RoadLifeText(root, paths), NumberStyles.Any, CultureInfo.InvariantCulture, out value) ? value : double.NaN;
    }

    private static bool RoadLifeBool(JObject root, params string[] paths)
    {
        string s = RoadLifeText(root, paths).ToLowerInvariant();
        return s == "true" || s == "1" || s == "yes" || s == "sim" || s == "on";
    }

    private static double RoadLifeZero(double value)
    {
        return double.IsNaN(value) || double.IsInfinity(value) ? 0.0 : value;
    }

    private static double RoadLifeDamagePct(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value) || value < 0) return 0.0;
        if (value <= 1.5) value *= 100.0;
        return Math.Max(0, Math.Min(100, value));
    }

    private static double RoadLifeTruckDamagePct(JObject tele)
    {
        string[][] fields =
        {
            new[] { "wearEngine", "engineWear", "engineDamage", "engine_damage", "truck.wearEngine" },
            new[] { "wearTransmission", "transmissionWear", "transmissionDamage", "transmission_damage", "truck.wearTransmission" },
            new[] { "wearCabin", "cabinWear", "cabinDamage", "cabin_damage", "truck.wearCabin" },
            new[] { "wearChassis", "chassisWear", "chassisDamage", "chassis_damage", "truck.wearChassis" },
            new[] { "wearWheels", "wheelsWear", "wheelsDamage", "wheels_damage", "truck.wearWheels" }
        };
        double max = 0;
        foreach (string[] f in fields)
            max = Math.Max(max, RoadLifeDamagePct(RoadLifeNumber(tele, f)));
        return max;
    }

    private static string RoadLifeGear(JObject tele)
    {
        string raw = RoadLifeText(tele, "truck.displayedGear", "Truck.DisplayedGear", "truck.gearDashboard", "gearDashboard", "truck.gear", "Truck.Gear", "gear");
        if (string.IsNullOrWhiteSpace(raw)) return "N";
        int n;
        if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out n))
            return raw.ToUpperInvariant();
        if (n < 0) return "R" + Math.Abs(n).ToString(CultureInfo.InvariantCulture);
        if (n == 0) return "N";
        return "D" + n.ToString(CultureInfo.InvariantCulture);
    }

    private static string RoadLifeEta(JObject tele)
    {
        string raw = RoadLifeText(tele, "eta", "navigation.eta", "navigation.estimatedTime", "Navigation.EstimatedTime", "job.estimatedTime", "timeRemaining");
        if (string.IsNullOrWhiteSpace(raw)) return "—";
        double seconds;
        if (double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out seconds) && seconds > 0 && seconds < 604800)
            return DateTime.Now.AddSeconds(seconds).ToString("HH:mm", CultureInfo.InvariantCulture);
        return raw.Length > 12 ? raw.Substring(0, 12) : raw;
    }
}

internal sealed class RoadLifeDashboard2Form : Form
{
    private const float DW = 1332f;
    private const float DH = 330f;

    private bool _online;
    private string _source = "—";
    private string _destination = "—";
    private string _cargo = "SEM CARGA";
    private string _gear = "N";
    private string _truck = "—";
    private string _eta = "—";
    private double _remaining;
    private double _travelled;
    private double _weightT;
    private double _fuel;
    private double _fuelCapacity;
    private double _fuelPct;
    private double _temperature;
    private double _speed;
    private double _limit;
    private bool _cruise;
    private double _truckDamage;
    private double _trailerDamage;
    private double _cargoDamage;

    private readonly Color _key = Color.Magenta;
    private readonly Color _panel = Color.FromArgb(9, 28, 22);
    private readonly Color _panel2 = Color.FromArgb(11, 17, 20);
    private readonly Color _green = Color.FromArgb(0, 229, 163);
    private readonly Color _brightGreen = Color.FromArgb(0, 255, 136);
    private readonly Color _muted = Color.FromArgb(138, 146, 154);

    public RoadLifeDashboard2Form()
    {
        Text = "ROADLIFE Dashboard2";
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(1200, 297);
        MinimumSize = new Size(780, 193);
        TopMost = true;
        ShowInTaskbar = true;
        BackColor = _key;
        TransparencyKey = _key;
        KeyPreview = true;
        DoubleBuffered = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);

        MouseDown += delegate(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(Handle, 0xA1, (IntPtr)0x2, IntPtr.Zero);
            }
        };
        KeyDown += delegate(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.F9) { TopMost = !TopMost; e.Handled = true; }
            if (e.KeyCode == Keys.F10) { Close(); e.Handled = true; }
        };
    }

    public void ApplyTelemetry(JObject d)
    {
        if (d == null) return;
        try
        {
            _online = (bool?)d["online"] ?? false;
            JObject route = d["route"] as JObject;
            JObject cargo = d["cargo"] as JObject;
            JObject fuel = d["fuel"] as JObject;
            JObject truck = d["truck"] as JObject;
            JObject damage = d["damage"] as JObject;

            _source = Convert.ToString(route?["source"]) ?? "—";
            _destination = Convert.ToString(route?["destination"]) ?? "—";
            _remaining = D(route?["distance_remaining_km"]);
            _travelled = D(route?["distance_travelled_km"]);

            _cargo = (Convert.ToString(cargo?["name"]) ?? "Sem carga").ToUpperInvariant();
            _weightT = D(cargo?["weight_t"]);

            _fuel = D(fuel?["current_l"]);
            _fuelCapacity = D(fuel?["capacity_l"]);
            _fuelPct = Math.Max(0, Math.Min(100, D(fuel?["percent"])));

            _gear = Convert.ToString(truck?["gear"]) ?? "N";
            _truck = Convert.ToString(truck?["model"]) ?? "—";
            _temperature = D(truck?["temperature_c"]);
            _speed = D(truck?["speed_kmh"]);
            _limit = D(truck?["speed_limit_kmh"]);
            _cruise = (bool?)truck?["cruise"] ?? false;

            _eta = Convert.ToString(d["eta"]) ?? "—";
            _truckDamage = D(damage?["truck_pct"]);
            _trailerDamage = D(damage?["trailer_pct"]);
            _cargoDamage = D(damage?["cargo_pct"]);
            Invalidate();
        }
        catch { }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        float scale = Math.Min(ClientSize.Width / DW, ClientSize.Height / DH);
        float ox = (ClientSize.Width - DW * scale) / 2f;
        float oy = (ClientSize.Height - DH * scale) / 2f;

        GraphicsState st = g.Save();
        g.TranslateTransform(ox, oy);
        g.ScaleTransform(scale, scale);

        using (Pen edge = new Pen(_green, 1.6f))
        using (Pen edgeBright = new Pen(_brightGreen, 2.4f))
        using (Brush panel = new SolidBrush(_panel))
        using (Brush panel2 = new SolidBrush(_panel2))
        {
            Panel(g, panel2, edgeBright, 10, 10, 1312, 310, 20);
            Panel(g, panel, edge, 20, 20, 165, 290, 16);
            Panel(g, panel, edge, 195, 20, 335, 100, 14);
            Panel(g, panel, edge, 195, 128, 160, 82, 12);
            Panel(g, panel, edge, 362, 128, 168, 82, 12);
            Panel(g, panel, edge, 538, 20, 52, 190, 12);
            g.FillEllipse(panel, 598, 15, 220, 200);
            g.DrawEllipse(edgeBright, 598, 15, 220, 200);
            g.DrawEllipse(new Pen(Color.FromArgb(65, _brightGreen), 1), 608, 25, 200, 180);
            Panel(g, panel, edge, 826, 20, 82, 190, 12);
            Panel(g, panel, edge, 916, 20, 396, 190, 16);
            Panel(g, panel, edge, 195, 220, 1117, 90, 14);
        }

        DrawLeft(g);
        DrawRoute(g);
        DrawWeightFuel(g);
        DrawGearTemp(g);
        DrawSpeed(g);
        DrawLimitCruise(g);
        DrawCargo(g);
        DrawBottom(g);

        g.Restore(st);
    }

    private void DrawLeft(Graphics g)
    {
        DrawCentered(g, "ROAD", 20, 48, 165, 28, 23, FontStyle.Bold | FontStyle.Italic, Color.White);
        DrawCentered(g, "LIFE", 20, 76, 165, 28, 23, FontStyle.Bold | FontStyle.Italic, _brightGreen);
        DrawCentered(g, "ETS2", 20, 105, 165, 22, 12, FontStyle.Bold, _muted);
        DrawCentered(g, _online ? "● ONLINE" : "● OFFLINE", 20, 132, 165, 22, 14, FontStyle.Bold, _online ? _brightGreen : Color.OrangeRed);
        DrawCentered(g, DateTime.Now.ToString("dd/MM/yyyy"), 20, 205, 165, 22, 15, FontStyle.Bold, Color.White);
        DrawCentered(g, DateTime.Now.ToString("HH:mm:ss"), 20, 230, 165, 24, 16, FontStyle.Bold, Color.White);
        DrawCentered(g, "HORÁRIO DE BRASÍLIA", 20, 255, 165, 15, 8.5f, FontStyle.Bold, _muted);
    }

    private void DrawRoute(Graphics g)
    {
        DrawCentered(g, "ROTA ATUAL", 210, 27, 305, 18, 11, FontStyle.Bold, _green);
        DrawCentered(g, _source + "  →  " + _destination, 205, 48, 315, 27, 19, FontStyle.Bold, Color.White);

        float total = Math.Max(1, _remaining + _travelled);
        float pct = Math.Max(0, Math.Min(1, _travelled / total));
        using (Brush bg = new SolidBrush(Color.FromArgb(30, 40, 44)))
        using (Brush fg = new SolidBrush(_brightGreen))
        {
            g.FillRectangle(bg, 215, 80, 295, 6);
            g.FillRectangle(fg, 215, 80, 295 * pct, 6);
            g.FillEllipse(Brushes.White, 209 + 295 * pct, 76, 12, 12);
        }
        Draw(g, "0 km", 215, 92, 80, 16, 11, FontStyle.Bold, _muted, StringAlignment.Near);
        Draw(g, Km(_remaining), 430, 92, 80, 16, 11, FontStyle.Bold, _muted, StringAlignment.Far);
    }

    private void DrawWeightFuel(Graphics g)
    {
        DrawCentered(g, "PESO", 205, 136, 140, 16, 10, FontStyle.Bold, _muted);
        DrawCentered(g, _weightT.ToString("N1", Pt) + " t", 205, 158, 140, 31, 22, FontStyle.Bold, Color.White);

        DrawCentered(g, "COMBUSTÍVEL", 370, 136, 150, 16, 10, FontStyle.Bold, _muted);
        DrawCentered(g, Math.Round(_fuel).ToString("N0", Pt) + " / " + Math.Round(_fuelCapacity).ToString("N0", Pt) + " L", 370, 154, 150, 20, 14, FontStyle.Bold, Color.White);
        Draw(g, Math.Round(_fuelPct).ToString("0") + "%", 470, 175, 45, 16, 12, FontStyle.Bold, _brightGreen, StringAlignment.Center);
        using (Brush bg = new SolidBrush(Color.FromArgb(30, 40, 44)))
        using (Brush fg = new SolidBrush(_brightGreen))
        {
            g.FillRectangle(bg, 375, 184, 90, 6);
            g.FillRectangle(fg, 375, 184, (float)(90 * _fuelPct / 100.0), 6);
        }
    }

    private void DrawGearTemp(Graphics g)
    {
        DrawCentered(g, "MARCHA", 538, 35, 52, 16, 8.5f, FontStyle.Bold, _muted);
        DrawCentered(g, _gear, 538, 58, 52, 35, 20, FontStyle.Bold, _brightGreen);
        using (Pen p = new Pen(_green, 1)) g.DrawLine(p, 546, 112, 582, 112);
        DrawCentered(g, "TEMP", 538, 129, 52, 16, 8.5f, FontStyle.Bold, _muted);
        DrawCentered(g, Math.Round(_temperature).ToString("0") + "°C", 538, 153, 52, 28, 13, FontStyle.Bold, Color.White);
    }

    private void DrawSpeed(Graphics g)
    {
        DrawCentered(g, Math.Round(_speed).ToString("0"), 608, 60, 200, 88, 66, FontStyle.Bold, Color.White);
        DrawCentered(g, "km/h", 608, 151, 200, 24, 16, FontStyle.Bold, _brightGreen);
    }

    private void DrawLimitCruise(Graphics g)
    {
        float cx = 867;
        using (Brush white = new SolidBrush(Color.White))
        using (Pen red = new Pen(Color.FromArgb(255, 55, 55), 4))
        {
            g.FillEllipse(white, cx - 24, 35, 48, 48);
            g.DrawEllipse(red, cx - 24, 35, 48, 48);
        }
        DrawCentered(g, _limit > 0 ? Math.Round(_limit).ToString("0") : "—", cx - 24, 47, 48, 26, 18, FontStyle.Bold, Color.Black);
        DrawCentered(g, "LIMITE", 826, 89, 82, 15, 9, FontStyle.Bold, _muted);
        using (Pen p = new Pen(_green, 1)) g.DrawLine(p, 838, 112, 896, 112);
        DrawCentered(g, "CRUISE", 826, 128, 82, 18, 9, FontStyle.Bold, _muted);
        DrawCentered(g, _cruise ? "ON" : "OFF", 826, 153, 82, 28, 15, FontStyle.Bold, _cruise ? _brightGreen : Color.White);
    }

    private void DrawCargo(Graphics g)
    {
        Draw(g, "CARGA DINÂMICA", 935, 31, 170, 18, 11, FontStyle.Bold, _green, StringAlignment.Near);
        Draw(g, "Reconhecimento automático", 1110, 32, 180, 18, 8.5f, FontStyle.Bold, _muted, StringAlignment.Far);
        Draw(g, _cargo, 1080, 60, 205, 32, 22, FontStyle.Bold, Color.White, StringAlignment.Center);

        using (Pen p = new Pen(_brightGreen, 2))
        using (Brush b = new SolidBrush(Color.FromArgb(20, 255, 136)))
        {
            g.FillRectangle(b, 945, 82, 118, 86);
            g.DrawRectangle(p, 945, 82, 118, 86);
            g.DrawLine(p, 945, 105, 1063, 105);
            g.DrawLine(p, 975, 82, 975, 168);
            g.DrawLine(p, 1033, 82, 1033, 168);
            g.DrawEllipse(p, 959, 167, 18, 18);
            g.DrawEllipse(p, 1028, 167, 18, 18);
        }
        DrawCentered(g, "CARGA", 945, 111, 118, 22, 10, FontStyle.Bold, _muted);
    }

    private void DrawBottom(Graphics g)
    {
        DrawMetric(g, "DISTÂNCIA RESTANTE", Km(_remaining), 210, 145);
        DrawMetric(g, "PERCURSO DA VIAGEM", Km(_travelled), 380, 145);
        DrawMetric(g, "CHEGADA ESTIMADA", _eta, 550, 135);
        DrawMetric(g, "CAMINHÃO", _truck, 710, 170);

        using (Pen p = new Pen(_green, 1))
        {
            g.DrawLine(p, 365, 232, 365, 297);
            g.DrawLine(p, 535, 232, 535, 297);
            g.DrawLine(p, 695, 232, 695, 297);
            g.DrawLine(p, 890, 232, 890, 297);
        }

        DrawCentered(g, "DANOS", 905, 232, 70, 18, 11, FontStyle.Bold, _brightGreen);
        DrawDamage(g, "Caminhão", _truckDamage, 985);
        DrawDamage(g, "Reboque", _trailerDamage, 1085);
        DrawDamage(g, "Carga", _cargoDamage, 1185);
    }

    private void DrawMetric(Graphics g, string label, string value, float x, float w)
    {
        DrawCentered(g, label, x, 232, w, 16, 9.5f, FontStyle.Bold, _muted);
        DrawCentered(g, value, x, 258, w, 26, value.Length > 16 ? 13 : 18, FontStyle.Bold, Color.White);
    }

    private void DrawDamage(Graphics g, string label, double value, float x)
    {
        DrawCentered(g, label, x, 235, 90, 15, 9, FontStyle.Bold, _muted);
        DrawCentered(g, value.ToString("N1", Pt) + "%", x, 258, 90, 26, 16, FontStyle.Bold, Color.White);
    }

    private static double D(JToken t)
    {
        double v;
        return t != null && double.TryParse(Convert.ToString(t, CultureInfo.InvariantCulture), NumberStyles.Any, CultureInfo.InvariantCulture, out v) ? v : 0;
    }

    private static string Km(double v)
    {
        return Math.Round(v).ToString("N0", Pt) + " km";
    }

    private static CultureInfo Pt
    {
        get { return CultureInfo.GetCultureInfo("pt-BR"); }
    }

    private static void Panel(Graphics g, Brush fill, Pen border, float x, float y, float w, float h, float radius)
    {
        using (GraphicsPath path = RoundRect(new RectangleF(x, y, w, h), radius))
        {
            g.FillPath(fill, path);
            g.DrawPath(border, path);
        }
    }

    private static GraphicsPath RoundRect(RectangleF r, float radius)
    {
        float d = radius * 2;
        GraphicsPath p = new GraphicsPath();
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    private static void DrawCentered(Graphics g, string text, float x, float y, float w, float h, float size, FontStyle style, Color color)
    {
        Draw(g, text, x, y, w, h, size, style, color, StringAlignment.Center);
    }

    private static void Draw(Graphics g, string text, float x, float y, float w, float h, float size, FontStyle style, Color color, StringAlignment align)
    {
        using (Font f = new Font("Segoe UI", size, style, GraphicsUnit.Pixel))
        using (Brush b = new SolidBrush(color))
        using (StringFormat sf = new StringFormat { Alignment = align, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter })
        {
            g.DrawString(text ?? string.Empty, f, b, new RectangleF(x, y, w, h), sf);
        }
    }

    [DllImport("user32.dll")] private static extern bool ReleaseCapture();
    [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
}
