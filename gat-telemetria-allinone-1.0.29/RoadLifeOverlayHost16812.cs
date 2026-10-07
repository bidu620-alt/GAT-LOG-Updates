using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using Newtonsoft.Json.Linq;

namespace GatTelemetry;

internal sealed partial class MainForm
{
    private System.Windows.Window _roadLifeOverlay;
    private System.Windows.Threading.DispatcherTimer _roadLifeClock;
    private IntPtr _roadLifeHwnd = IntPtr.Zero;
    private System.Windows.Interop.HwndSource _roadLifeSource;
    private System.Windows.Interop.HwndSourceHook _roadLifeHook;
    private bool _roadLifeClickThrough;
    private Dictionary<string,string> _roadLifeCargoAliases;
    private bool _roadLifeCargoAliasesLoaded;

    private static string RoadLifeOverlayRoot()
    {
        return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "RoadLifeDash");
    }

    private static string RoadLifeOverlayXaml()
    {
        return Path.Combine(RoadLifeOverlayRoot(), "RoadLifeDash.xaml");
    }

    private void RoadLifeOverlayInitialize()
    {
        FormClosed += delegate
        {
            try { CloseRoadLifeOverlay(); } catch { }
        };
    }

    private bool RoadLifeOverlayRunning()
    {
        return _roadLifeOverlay != null && _roadLifeOverlay.IsVisible;
    }

    private void ToggleRoadLifeOverlay()
    {
        if (RoadLifeOverlayRunning()) CloseRoadLifeOverlay();
        else OpenRoadLifeOverlay();
    }

    private void OpenRoadLifeOverlay()
    {
        try
        {
            if (RoadLifeOverlayRunning())
            {
                _roadLifeOverlay.Activate();
                return;
            }

            string xamlPath = RoadLifeOverlayXaml();
            if (!File.Exists(xamlPath))
            {
                System.Windows.Forms.MessageBox.Show(
                    "RoadLifeDash.xaml não encontrado.\r\n\r\n" + xamlPath,
                    "ROADLIFE DASH",
                    System.Windows.Forms.MessageBoxButtons.OK,
                    System.Windows.Forms.MessageBoxIcon.Warning);
                return;
            }

            object loaded;
            using (FileStream fs = File.OpenRead(xamlPath))
                loaded = System.Windows.Markup.XamlReader.Load(fs);

            _roadLifeOverlay = loaded as System.Windows.Window;
            if (_roadLifeOverlay == null)
                throw new InvalidOperationException("RoadLifeDash.xaml não contém uma Window válida.");

            System.Windows.FrameworkElement root = _roadLifeOverlay.FindName("Root") as System.Windows.FrameworkElement;
            if (root != null)
            {
                root.MouseLeftButtonDown += delegate(object sender, System.Windows.Input.MouseButtonEventArgs e)
                {
                    if (!_roadLifeClickThrough && e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
                    {
                        try { _roadLifeOverlay.DragMove(); } catch { }
                    }
                };
            }

            LoadRoadLifeImage("StructureImage", Path.Combine(RoadLifeOverlayRoot(), "Assets", "dashboard-structure-green.png"));

            _roadLifeOverlay.SourceInitialized += delegate
            {
                try
                {
                    var helper = new System.Windows.Interop.WindowInteropHelper(_roadLifeOverlay);
                    _roadLifeHwnd = helper.Handle;
                    RoadLifeNative.RegisterHotKey(_roadLifeHwnd, 1001, 0, 0x77); // F8
                    RoadLifeNative.RegisterHotKey(_roadLifeHwnd, 1002, 0, 0x78); // F9
                    RoadLifeNative.RegisterHotKey(_roadLifeHwnd, 1003, 0, 0x79); // F10
                    _roadLifeSource = System.Windows.Interop.HwndSource.FromHwnd(_roadLifeHwnd);
                    _roadLifeHook = RoadLifeWndProc;
                    if (_roadLifeSource != null)
                        _roadLifeSource.AddHook(_roadLifeHook);
                }
                catch { }
            };

            _roadLifeOverlay.Closed += delegate
            {
                try
                {
                    if (_roadLifeHwnd != IntPtr.Zero)
                    {
                        RoadLifeNative.UnregisterHotKey(_roadLifeHwnd, 1001);
                        RoadLifeNative.UnregisterHotKey(_roadLifeHwnd, 1002);
                        RoadLifeNative.UnregisterHotKey(_roadLifeHwnd, 1003);
                    }
                    if (_roadLifeSource != null && _roadLifeHook != null)
                        _roadLifeSource.RemoveHook(_roadLifeHook);
                }
                catch { }

                try { _roadLifeClock?.Stop(); } catch { }
                _roadLifeClock = null;
                _roadLifeSource = null;
                _roadLifeHook = null;
                _roadLifeHwnd = IntPtr.Zero;
                _roadLifeClickThrough = false;
                _roadLifeOverlay = null;
            };

            _roadLifeClock = new System.Windows.Threading.DispatcherTimer();
            _roadLifeClock.Interval = TimeSpan.FromMilliseconds(500);
            _roadLifeClock.Tick += delegate
            {
                SetRoadLifeText("DateText", DateTime.Now.ToString("dd/MM/yy"));
                SetRoadLifeText("TimeText", DateTime.Now.ToString("HH:mm:ss"));
            };
            _roadLifeClock.Start();

            _roadLifeOverlay.Show();
            SetRoadLifeText("DateText", DateTime.Now.ToString("dd/MM/yy"));
            SetRoadLifeText("TimeText", DateTime.Now.ToString("HH:mm:ss"));
            ClientStore.Log("RoadLife Dashboard2 aberto como overlay interno do GAT Telemetria.");
        }
        catch (Exception ex)
        {
            ClientStore.Log("RoadLife Dashboard2 abrir: " + ex);
            System.Windows.Forms.MessageBox.Show(
                ex.Message,
                "ROADLIFE DASH",
                System.Windows.Forms.MessageBoxButtons.OK,
                System.Windows.Forms.MessageBoxIcon.Warning);
        }
    }

    private void CloseRoadLifeOverlay()
    {
        try
        {
            if (_roadLifeOverlay != null)
                _roadLifeOverlay.Close();
        }
        catch { }
    }

    private IntPtr RoadLifeWndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == 0x0312)
        {
            int id = wParam.ToInt32();
            if (id == 1001)
            {
                SetRoadLifeClickThrough(!_roadLifeClickThrough);
                handled = true;
            }
            else if (id == 1002)
            {
                if (_roadLifeOverlay != null)
                    _roadLifeOverlay.Topmost = !_roadLifeOverlay.Topmost;
                handled = true;
            }
            else if (id == 1003)
            {
                CloseRoadLifeOverlay();
                handled = true;
            }
        }
        return IntPtr.Zero;
    }

    private void SetRoadLifeClickThrough(bool enabled)
    {
        if (_roadLifeHwnd == IntPtr.Zero) return;
        int style = RoadLifeNative.GetWindowLong(_roadLifeHwnd, -20);
        if (enabled) style |= 0x20;
        else style &= ~0x20;
        RoadLifeNative.SetWindowLong(_roadLifeHwnd, -20, style);
        _roadLifeClickThrough = enabled;
    }

    private void RoadLifeOverlayPush(JObject tele)
    {
        if (tele == null || !RoadLifeOverlayRunning()) return;

        try
        {
            JObject payload = RoadLifeOverlayBuildPayload(tele);
            System.Windows.Window target = _roadLifeOverlay;
            if (target == null) return;

            if (!target.Dispatcher.CheckAccess())
            {
                target.Dispatcher.BeginInvoke(new Action(delegate { ApplyRoadLifePayload(payload); }));
                return;
            }

            ApplyRoadLifePayload(payload);
        }
        catch (Exception ex)
        {
            ClientStore.Log("RoadLife Dashboard2 atualizar: " + ex.Message);
        }
    }

    private void ApplyRoadLifePayload(JObject d)
    {
        if (_roadLifeOverlay == null || d == null) return;

        JObject route = d["route"] as JObject ?? new JObject();
        JObject cargo = d["cargo"] as JObject ?? new JObject();
        JObject fuel = d["fuel"] as JObject ?? new JObject();
        JObject truck = d["truck"] as JObject ?? new JObject();
        JObject damage = d["damage"] as JObject ?? new JObject();

        string source = RoadLifeString(route["source"], "—");
        string destination = RoadLifeString(route["destination"], "—");
        double remaining = RoadLifeDouble(route["distance_remaining_km"]);
        double travelled = RoadLifeDouble(route["distance_travelled_km"]);

        SetRoadLifeText("RouteText", source + " → " + destination);
        SetRoadLifeText("StartKmText", "0 km");
        SetRoadLifeText("RemainKmTop", RoadLifeInt(remaining) + " km");
        SetRoadLifeText("RemainBottom", RoadLifeInt(remaining) + " km");
        SetRoadLifeText("TravelBottom", RoadLifeInt(travelled) + " km");

        string cargoName = RoadLifeString(cargo["name"], "Sem carga");
        string cargoId = RoadLifeString(cargo["id"], "");
        SetRoadLifeText("CargoWeight", RoadLifeDec(RoadLifeDouble(cargo["weight_t"]), 1) + " t");
        SetRoadLifeText("CargoRightName", cargoName.ToUpperInvariant());
        SetRoadLifeCargoImage(cargoId, cargoName);

        double fuelCurrent = RoadLifeDouble(fuel["current_l"]);
        double fuelCapacity = RoadLifeDouble(fuel["capacity_l"]);
        double fuelPct = Math.Max(0, Math.Min(100, RoadLifeDouble(fuel["percent"])));
        SetRoadLifeText("FuelText", RoadLifeInt(fuelCurrent) + " / " + RoadLifeInt(fuelCapacity) + " L");
        SetRoadLifeText("FuelPct", RoadLifeInt(fuelPct) + "%");
        System.Windows.Controls.Border fuelBar = _roadLifeOverlay.FindName("FuelBar") as System.Windows.Controls.Border;
        if (fuelBar != null) fuelBar.Width = 125.0 * fuelPct / 100.0;

        SetRoadLifeText("TempText", RoadLifeInt(RoadLifeDouble(truck["temperature_c"])) + "°C");
        SetRoadLifeText("SpeedText", RoadLifeInt(RoadLifeDouble(truck["speed_kmh"])));
        SetRoadLifeText("GearText", RoadLifeString(truck["gear"], "N"));

        double limit = RoadLifeDouble(truck["speed_limit_kmh"]);
        SetRoadLifeText("LimitText", limit > 0 ? RoadLifeInt(limit) : "—");
        double cruiseSet = RoadLifeDouble(truck["cruise_speed_kmh"]);
        SetRoadLifeText("CruiseText", RoadLifeBool(truck["cruise"]) && cruiseSet > 0 ? RoadLifeInt(cruiseSet) + " km/h" : "—");
        SetRoadLifeText("EtaText", RoadLifeString(d["driver"], "—"));
        SetRoadLifeText("TruckText", RoadLifeString(truck["model"], "—"));

        SetRoadLifeText("TruckDamageText", RoadLifeDec(RoadLifeDouble(damage["truck_pct"]), 1) + "%");
        SetRoadLifeText("TrailerDamageText", RoadLifeDec(RoadLifeDouble(damage["trailer_pct"]), 1) + "%");
        SetRoadLifeText("CargoDamageText", RoadLifeDec(RoadLifeDouble(damage["cargo_pct"]), 1) + "%");
        SetRoadLifeText("OnlineText", RoadLifeBool(d["online"]) ? "● ONLINE" : "● OFFLINE");
    }

    private void SetRoadLifeText(string name, string value)
    {
        if (_roadLifeOverlay == null) return;
        System.Windows.Controls.TextBlock tb = _roadLifeOverlay.FindName(name) as System.Windows.Controls.TextBlock;
        if (tb != null) tb.Text = value ?? string.Empty;
    }

    private void LoadRoadLifeImage(string controlName, string path)
    {
        if (_roadLifeOverlay == null || !File.Exists(path)) return;
        try
        {
            System.Windows.Controls.Image image = _roadLifeOverlay.FindName(controlName) as System.Windows.Controls.Image;
            if (image == null) return;

            var bmp = new System.Windows.Media.Imaging.BitmapImage();
            bmp.BeginInit();
            bmp.UriSource = new Uri(path, UriKind.Absolute);
            bmp.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
            bmp.EndInit();
            image.Source = bmp;
        }
        catch { }
    }

    private void SetRoadLifeCargoImage(string cargoId, string cargoName)
    {
        if (_roadLifeOverlay == null) return;

        System.Windows.Controls.Image image = _roadLifeOverlay.FindName("CargoPreview") as System.Windows.Controls.Image;
        if (image == null) return;

        string folder = Path.Combine(RoadLifeOverlayRoot(), "Assets", "cargo");
        string generated = Path.Combine(folder, "generated");

        string nameKey = RoadLifeCargoKey(cargoName);
        string idKey = RoadLifeCargoKey(cargoId);

        if (string.IsNullOrWhiteSpace(nameKey)
            || nameKey == "semcarga"
            || nameKey == "nocargo")
        {
            image.Source = null;
            image.Visibility = System.Windows.Visibility.Collapsed;
            return;
        }

        RoadLifeLoadCargoAliases(folder);

        string icon = null;
        if (_roadLifeCargoAliases != null)
        {
            if (!string.IsNullOrWhiteSpace(idKey))
                _roadLifeCargoAliases.TryGetValue(idKey, out icon);

            if (string.IsNullOrWhiteSpace(icon) && !string.IsNullOrWhiteSpace(nameKey))
                _roadLifeCargoAliases.TryGetValue(nameKey, out icon);
        }

        if (!string.IsNullOrWhiteSpace(icon))
        {
            string generatedFile = Path.Combine(generated, icon + ".png");
            if (File.Exists(generatedFile))
            {
                LoadRoadLifeImage("CargoPreview", generatedFile);
                image.Visibility = System.Windows.Visibility.Visible;
                return;
            }
        }

        // Compatibilidade com imagens individuais adicionadas manualmente.
        string slug = RoadLifeSlug(cargoName);
        string[] exts = { ".png", ".jpg", ".jpeg", ".webp" };
        foreach (string ext in exts)
        {
            string p = Path.Combine(folder, slug + ext);
            if (!string.IsNullOrWhiteSpace(slug) && File.Exists(p))
            {
                LoadRoadLifeImage("CargoPreview", p);
                image.Visibility = System.Windows.Visibility.Visible;
                return;
            }
        }

        image.Source = null;
        image.Visibility = System.Windows.Visibility.Collapsed;
    }

    private void RoadLifeLoadCargoAliases(string folder)
    {
        if (_roadLifeCargoAliasesLoaded) return;
        _roadLifeCargoAliasesLoaded = true;
        _roadLifeCargoAliases = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            string mapFile = Path.Combine(folder, "cargo-name-map.json");
            if (!File.Exists(mapFile)) return;

            JObject root = JObject.Parse(File.ReadAllText(mapFile));
            JObject aliases = root["aliases"] as JObject;
            if (aliases == null) return;

            foreach (JProperty p in aliases.Properties())
            {
                string key = RoadLifeCargoKey(p.Name);
                string value = Convert.ToString(p.Value, CultureInfo.InvariantCulture) ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(value))
                    _roadLifeCargoAliases[key] = value;
            }
        }
        catch (Exception ex)
        {
            ClientStore.Log("RoadLife cargo aliases: " + ex.Message);
        }
    }

    private static string RoadLifeCargoKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;

        string normalized = value.Normalize(System.Text.NormalizationForm.FormD);
        var sb = new System.Text.StringBuilder();

        foreach (char c in normalized)
        {
            UnicodeCategory cat = CharUnicodeInfo.GetUnicodeCategory(c);
            if (cat == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsLetterOrDigit(c))
                sb.Append(char.ToLowerInvariant(c));
        }

        return sb.ToString();
    }

    private static string RoadLifeSlug(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        string normalized = value.Normalize(System.Text.NormalizationForm.FormD);
        var sb = new System.Text.StringBuilder();

        foreach (char c in normalized)
        {
            UnicodeCategory cat = CharUnicodeInfo.GetUnicodeCategory(c);
            if (cat == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsLetterOrDigit(c)) sb.Append(char.ToLowerInvariant(c));
            else if (sb.Length > 0 && sb[sb.Length - 1] != '-') sb.Append('-');
        }

        return sb.ToString().Trim('-');
    }

    private JObject RoadLifeOverlayBuildPayload(JObject tele)
    {
        string cargo = RoadLifeOverlayText(tele, "cargo_name", "job.cargoName", "job.cargo", "Job.CargoName");
        string cargoId = RoadLifeOverlayText(
            tele,
            "cargo_id", "cargoId", "cargoID", "cargo_token", "cargoToken",
            "job.cargoId", "job.cargoID", "job.cargo_id", "job.cargoToken",
            "Job.CargoId", "Job.CargoID", "Job.CargoToken");
        string source = RoadLifeOverlayText(tele, "source_city", "job.sourceCity", "Job.SourceCity");
        string destination = RoadLifeOverlayText(tele, "destination_city", "job.destinationCity", "Job.DestinationCity");

        double massKg = RoadLifeOverlayNumber(tele, "mass_kg", "cargoMass", "cargo_mass", "job.cargoMass", "Job.CargoMass");
        double remaining = RoadLifeOverlayNumber(tele, "remaining_km", "remainingKm", "navigation.estimatedDistance", "Navigation.EstimatedDistance");
        double planned = RoadLifeOverlayNumber(tele, "planned_distance_km", "plannedDistanceKm", "job.plannedDistanceKm", "Job.PlannedDistanceKm");
        double travelled = !double.IsNaN(planned) && !double.IsNaN(remaining) ? Math.Max(0, planned - remaining) : 0;

        double fuelCurrent = RoadLifeOverlayNumber(tele, "truck.fuel", "Truck.Fuel", "fuel", "fuelAmount", "fuelLiters");
        double fuelCapacity = RoadLifeOverlayNumber(tele, "truck.fuelCapacity", "Truck.FuelCapacity", "fuelCapacity", "fuel_capacity");
        double fuelPct = !double.IsNaN(fuelCurrent) && !double.IsNaN(fuelCapacity) && fuelCapacity > 0
            ? fuelCurrent / fuelCapacity * 100.0
            : RoadLifeOverlayNumber(tele, "truck.fuelPercent", "fuelPercent", "fuel_percent");

        double speed = RoadLifeOverlayNumber(tele, "truck.speed", "Truck.Speed", "speed", "speed_kmh");
        if (!double.IsNaN(speed) && Math.Abs(speed) < 3.0)
            speed *= 3.6;

        double temperature = RoadLifeOverlayNumber(tele, "truck.waterTemperature", "Truck.WaterTemperature", "waterTemperature", "water_temperature", "truck.engineTemperature");
        double speedLimit = RoadLifeOverlayNumber(tele, "navigation.speedLimit", "Navigation.SpeedLimit", "speedLimit", "speed_limit_kmh");
        if (!double.IsNaN(speedLimit) && speedLimit > 0 && speedLimit < 3.0)
            speedLimit *= 3.6;

        double cruiseSpeed = RoadLifeOverlayNumber(tele, "truck.cruiseControlSpeed", "Truck.CruiseControlSpeed", "cruiseControlSpeed", "cruise_speed");
        if (!double.IsNaN(cruiseSpeed) && cruiseSpeed > 0 && cruiseSpeed < 3.0)
            cruiseSpeed *= 3.6;
        bool cruise = RoadLifeOverlayBoolean(tele, "truck.cruiseControl", "truck.cruiseControlOn", "cruiseControl", "cruise")
            || (!double.IsNaN(cruiseSpeed) && cruiseSpeed > 0.1);

        string model = RoadLifeOverlayText(tele, "truck.model", "Truck.Model", "truck.name", "Truck.Name", "truckModel", "truck_model");
        string make = RoadLifeOverlayText(tele, "truck.make", "Truck.Make", "truck.brand", "Truck.Brand", "truckMake");
        if (string.IsNullOrWhiteSpace(model)) model = make;
        else if (!string.IsNullOrWhiteSpace(make) && model.IndexOf(make, StringComparison.OrdinalIgnoreCase) < 0)
            model = make + " " + model;

        return new JObject
        {
            ["online"] = true,
            ["route"] = new JObject
            {
                ["source"] = string.IsNullOrWhiteSpace(source) ? "—" : source,
                ["destination"] = string.IsNullOrWhiteSpace(destination) ? "—" : destination,
                ["distance_remaining_km"] = RoadLifeOverlayZero(remaining),
                ["distance_travelled_km"] = RoadLifeOverlayZero(travelled)
            },
            ["cargo"] = new JObject
            {
                ["id"] = cargoId ?? string.Empty,
                ["name"] = string.IsNullOrWhiteSpace(cargo) ? "Sem carga" : cargo,
                ["weight_t"] = double.IsNaN(massKg) ? 0.0 : Math.Max(0, massKg / 1000.0)
            },
            ["fuel"] = new JObject
            {
                ["current_l"] = RoadLifeOverlayZero(fuelCurrent),
                ["capacity_l"] = RoadLifeOverlayZero(fuelCapacity),
                ["percent"] = double.IsNaN(fuelPct) ? 0.0 : Math.Max(0, Math.Min(100, fuelPct))
            },
            ["truck"] = new JObject
            {
                ["model"] = string.IsNullOrWhiteSpace(model) ? "—" : model,
                ["gear"] = RoadLifeOverlayGear(tele),
                ["temperature_c"] = RoadLifeOverlayZero(temperature),
                ["speed_kmh"] = RoadLifeOverlayZero(Math.Abs(speed)),
                ["speed_limit_kmh"] = RoadLifeOverlayZero(speedLimit),
                ["cruise"] = cruise,
                ["cruise_speed_kmh"] = RoadLifeOverlayZero(cruiseSpeed)
            },
            ["driver"] = string.IsNullOrWhiteSpace(_driver) ? (string.IsNullOrWhiteSpace(_accountUser) ? "—" : _accountUser) : _driver,
            ["eta"] = RoadLifeOverlayEta(tele),
            ["damage"] = new JObject
            {
                ["truck_pct"] = RoadLifeOverlayTruckDamage(tele),
                ["trailer_pct"] = RoadLifeOverlayDamage(RoadLifeOverlayNumber(tele, "trailerDamage", "trailer_damage", "trailer.wear", "trailer.damage")),
                ["cargo_pct"] = RoadLifeOverlayDamage(RoadLifeOverlayNumber(tele, "cargoDamage", "cargo_damage", "job.cargoDamage", "Job.CargoDamage"))
            }
        };
    }

    private static string RoadLifeOverlayText(JObject root, params string[] paths)
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

    private static double RoadLifeOverlayNumber(JObject root, params string[] paths)
    {
        double v;
        return double.TryParse(RoadLifeOverlayText(root, paths), NumberStyles.Any, CultureInfo.InvariantCulture, out v) ? v : double.NaN;
    }

    private static bool RoadLifeOverlayBoolean(JObject root, params string[] paths)
    {
        string s = RoadLifeOverlayText(root, paths).ToLowerInvariant();
        return s == "true" || s == "1" || s == "yes" || s == "sim" || s == "on";
    }

    private static double RoadLifeOverlayZero(double v)
    {
        return double.IsNaN(v) || double.IsInfinity(v) ? 0.0 : v;
    }

    private static double RoadLifeOverlayDamage(double v)
    {
        if (double.IsNaN(v) || double.IsInfinity(v) || v < 0) return 0.0;
        if (v <= 1.5) v *= 100.0;
        return Math.Max(0, Math.Min(100, v));
    }

    private static double RoadLifeOverlayTruckDamage(JObject tele)
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
            max = Math.Max(max, RoadLifeOverlayDamage(RoadLifeOverlayNumber(tele, f)));
        return max;
    }

    private static string RoadLifeOverlayGear(JObject tele)
    {
        string raw = RoadLifeOverlayText(tele, "truck.displayedGear", "Truck.DisplayedGear", "truck.gearDashboard", "gearDashboard", "truck.gear", "Truck.Gear", "gear");
        if (string.IsNullOrWhiteSpace(raw)) return "N";

        int n;
        if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out n))
            return raw.ToUpperInvariant();
        if (n < 0) return "R" + Math.Abs(n).ToString(CultureInfo.InvariantCulture);
        if (n == 0) return "N";
        return "D" + n.ToString(CultureInfo.InvariantCulture);
    }

    private static string RoadLifeOverlayEta(JObject tele)
    {
        string raw = RoadLifeOverlayText(tele, "eta", "navigation.eta", "navigation.estimatedTime", "Navigation.EstimatedTime", "job.estimatedTime", "timeRemaining");
        if (string.IsNullOrWhiteSpace(raw)) return "—";

        double seconds;
        if (double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out seconds))
        {
            if (seconds > 0 && seconds < 604800)
                return DateTime.Now.AddSeconds(seconds).ToString("HH:mm");
            return "—";
        }

        DateTime dt;
        if (DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out dt) && dt.Year >= 2000)
            return dt.ToString("HH:mm");

        return raw.Length > 12 ? raw.Substring(0, 12) : raw;
    }

    private static string RoadLifeString(JToken token, string fallback)
    {
        string s = token == null ? string.Empty : Convert.ToString(token, CultureInfo.InvariantCulture);
        return string.IsNullOrWhiteSpace(s) ? fallback : s;
    }

    private static double RoadLifeDouble(JToken token)
    {
        double v;
        return token != null && double.TryParse(Convert.ToString(token, CultureInfo.InvariantCulture), NumberStyles.Any, CultureInfo.InvariantCulture, out v) ? v : 0.0;
    }

    private static bool RoadLifeBool(JToken token)
    {
        bool b;
        if (token != null && bool.TryParse(Convert.ToString(token, CultureInfo.InvariantCulture), out b))
            return b;
        return RoadLifeDouble(token) != 0;
    }

    private static string RoadLifeInt(double v)
    {
        return Math.Round(v).ToString("N0", CultureInfo.GetCultureInfo("pt-BR"));
    }

    private static string RoadLifeDec(double v, int digits)
    {
        return v.ToString("N" + digits, CultureInfo.GetCultureInfo("pt-BR"));
    }

    private static class RoadLifeNative
    {
        [DllImport("user32.dll")] internal static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
        [DllImport("user32.dll")] internal static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        [DllImport("user32.dll")] internal static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll")] internal static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
    }
}

