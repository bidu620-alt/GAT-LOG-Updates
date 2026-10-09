using System;
using System.Drawing;
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
	private readonly HttpClient _http = new HttpClient
	{
		Timeout = TimeSpan.FromSeconds(3.0)
	};

	private readonly Timer _timer = new Timer
	{
		Interval = 450
	};

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

	private double _toleranceValue = 3.0;

	private const int WM_NCLBUTTONDOWN_050 = 161;

	private const int HTCAPTION_050 = 2;

	private const int WM_NCHITTEST_045 = 132;

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

	internal TruckOverlay041()
	{
		Text = "ROADLIFE DASH • Sobreposição do caminhão";
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
		EnableDrag050(_header);
		EnableDrag050(_title);
		EnableDrag050(_sub);
		EnableDrag050(_connected);
		Resize += delegate
		{
			LayoutResponsive045();
			Invalidate();
		};
		Paint += PaintBorder045;
		_timer.Tick += async delegate
		{
			await PollAsync();
		};
		Shown += delegate
		{
			LayoutResponsive045();
			_timer.Start();
		};
		FormClosing += delegate
		{
			SaveOverlayBounds053("truck-overlay-bounds-v1.txt");
		};
		FormClosed += delegate
		{
			_timer.Stop();
			_timer.Dispose();
			_http.Dispose();
		};
	}

	private void RestoreOverlayBounds053(string fileName)
	{
		try
		{
			string path = Path.Combine(Application.LocalUserAppDataPath, fileName);
			if (!File.Exists(path))
			{
				return;
			}
			string[] array = File.ReadAllText(path).Split('|');
			if (array.Length != 4 || !int.TryParse(array[0], out var result) || !int.TryParse(array[1], out var result2) || !int.TryParse(array[2], out var result3) || !int.TryParse(array[3], out var result4))
			{
				return;
			}
			result3 = Math.Max(MinimumSize.Width, result3);
			result4 = Math.Max(MinimumSize.Height, result4);
			Rectangle rectangle = new Rectangle(result, result2, result3, result4);
			bool flag = false;
			Screen[] allScreens = Screen.AllScreens;
			for (int i = 0; i < allScreens.Length; i++)
			{
				Rectangle rectangle2 = Rectangle.Intersect(allScreens[i].WorkingArea, rectangle);
				if (rectangle2.Width >= 80 && rectangle2.Height >= 60)
				{
					flag = true;
					break;
				}
			}
			if (flag)
			{
				Bounds = rectangle;
			}
		}
		catch
		{
		}
	}

	private void SaveOverlayBounds053(string fileName)
	{
		try
		{
			Rectangle rectangle = ((WindowState == FormWindowState.Normal) ? Bounds : RestoreBounds);
			if (rectangle.Width >= MinimumSize.Width && rectangle.Height >= MinimumSize.Height)
			{
				File.WriteAllText(Path.Combine(Application.LocalUserAppDataPath, fileName), string.Join("|", rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height));
			}
		}
		catch
		{
		}
	}

	private void BuildUi()
	{
		_header.BackColor = Color.FromArgb(5, 22, 39);
		_header.Height = 70;
		_header.Dock = DockStyle.Top;
		Controls.Add(_header);
		_title.Text = "ROADLIFE DASH";
		_title.Left = 22;
		_title.Top = 11;
		_title.Width = 220;
		_title.Height = 30;
		_title.ForeColor = Color.FromArgb(44, 164, 255);
		_title.Font = new Font("Segoe UI Black", 18f, FontStyle.Bold | FontStyle.Italic);
		_header.Controls.Add(_title);
		_sub.Text = "ACOMPANHAMENTO EM TEMPO REAL";
		_sub.Left = 23;
		_sub.Top = 40;
		_sub.Width = 290;
		_sub.Height = 18;
		_sub.ForeColor = Color.FromArgb(165, 194, 222);
		_sub.Font = new Font("Segoe UI Semibold", 8.5f);
		_header.Controls.Add(_sub);
		_connected.Text = "● Conectando à telemetria";
		_connected.Width = 190;
		_connected.Height = 30;
		_connected.Top = 19;
		_connected.TextAlign = ContentAlignment.MiddleRight;
		_connected.ForeColor = Color.FromArgb(105, 230, 145);
		_connected.Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold);
		_connected.Anchor = AnchorStyles.Top | AnchorStyles.Right;
		_header.Controls.Add(_connected);
		_close.Text = "×";
		_close.Width = 38;
		_close.Height = 34;
		_close.Top = 17;
		_close.Anchor = AnchorStyles.Top | AnchorStyles.Right;
		_close.FlatStyle = FlatStyle.Flat;
		_close.FlatAppearance.BorderColor = Color.FromArgb(31, 82, 122);
		_close.BackColor = Color.FromArgb(6, 31, 52);
		_close.ForeColor = Color.FromArgb(222, 238, 252);
		_close.Font = new Font("Segoe UI Semibold", 13f, FontStyle.Bold);
		_close.Cursor = Cursors.Hand;
		_close.Click += delegate
		{
			Close();
		};
		_close.Visible = false;
		Controls.Add(_speedCaption);
		Controls.Add(_speed);
		Controls.Add(_limitCaption);
		Controls.Add(_limit);
		Controls.Add(_toleranceCaption);
		Controls.Add(_tolerance);
		Controls.Add(_saved);
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
		Label[] array = new Label[5] { _gear, _fuel, _cruise, _route, _condition };
		foreach (Label label in array)
		{
			StyleTile045(label);
			_basicPanel.Controls.Add(label);
		}
		_gear.Text = "MARCHA\r\nN";
		_fuel.Text = "COMBUSTÍVEL\r\n—";
		_cruise.Text = "CRUISE\r\nOFF";
		_route.Text = "ROTA\r\n—";
		_condition.Text = "CONDIÇÃO\r\n100%";
		_jobPanel.BackColor = Color.Transparent;
		Controls.Add(_jobPanel);
		array = new Label[4] { _cargo, _destination, _weight, _remaining };
		foreach (Label label2 in array)
		{
			StyleInfo045(label2);
			_jobPanel.Controls.Add(label2);
		}
		_cargo.Text = "CARGA\r\nSem carga";
		_destination.Text = "DESTINO\r\n—";
		_weight.Text = "PESO\r\n—";
		_remaining.Text = "DIST. RESTANTE\r\n—";
		_etaPanel.BackColor = Color.Transparent;
		Controls.Add(_etaPanel);
		array = new Label[2] { _eta, _average };
		foreach (Label label3 in array)
		{
			StyleInfo045(label3);
			_etaPanel.Controls.Add(label3);
		}
		_eta.Text = "TEMPO ESTIMADO\r\n—";
		_average.Text = "VELOCIDADE MÉDIA\r\n—";
		_damagePanel.BackColor = Color.Transparent;
		Controls.Add(_damagePanel);
		array = new Label[3] { _truckDamage, _trailerDamage, _cargoDamage };
		foreach (Label label4 in array)
		{
			StyleTile045(label4);
			_damagePanel.Controls.Add(label4);
		}
		_truckDamage.Text = "DANOS DO CAMINHÃO\r\n0%";
		_trailerDamage.Text = "DANOS DO REBOQUE\r\n0%";
		_cargoDamage.Text = "DANOS DA CARGA\r\n0%";
		_hint.Text = "Mova pela barra da janela • redimensione pelas bordas.";
		_hint.Height = 28;
		_hint.ForeColor = Color.FromArgb(125, 153, 182);
		_hint.TextAlign = ContentAlignment.MiddleRight;
		_hint.Font = new Font("Segoe UI", 8f);
		_hint.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
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

	[DllImport("user32.dll")]
	private static extern bool ReleaseCapture();

	[DllImport("user32.dll")]
	private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

	private void EnableDrag050(Control control)
	{
		if (control == null)
		{
			return;
		}
		control.Cursor = Cursors.SizeAll;
		control.MouseDown += (object sender, MouseEventArgs e) =>
		{
			if (e.Button == MouseButtons.Left)
			{
				ReleaseCapture();
				SendMessage(Handle, 161, (IntPtr)2, IntPtr.Zero);
			}
		};
	}

	private void LayoutResponsive045()
	{
		int num = Math.Max(300, ClientSize.Width);
		int num2 = Math.Max(220, ClientSize.Height);
		int num3 = 16;
		int num4 = num - num3 * 2;
		_connected.Left = Math.Max(260, num - 255);
		_close.Left = Math.Max(0, num - 52);
		int num5 = 82;
		bool flag = num < 520;
		int num6 = Math.Max(80, num4 / 3);
		if (!flag)
		{
			_speedCaption.SetBounds(num3, num5, num6 + 35, 20);
			_speed.SetBounds(num3, num5 + 20, num6 + 35, 70);
			_limitCaption.SetBounds(num3 + num6 + 38, num5, num6 - 12, 20);
			_limit.SetBounds(num3 + num6 + 38, num5 + 20, num6 - 12, 70);
			_toleranceCaption.SetBounds(num3 + num6 * 2 + 28, num5, num - (num3 + num6 * 2 + 28) - num3, 20);
			_tolerance.SetBounds(num3 + num6 * 2 + 28, num5 + 19, num - (num3 + num6 * 2 + 28) - num3, 44);
			_saved.SetBounds(num3 + num6 * 2 + 28, num5 + 59, num - (num3 + num6 * 2 + 28) - num3, 30);
			num5 += 106;
		}
		else
		{
			_speedCaption.SetBounds(num3, num5, num4 / 2, 20);
			_speed.SetBounds(num3, num5 + 18, num4 / 2, 64);
			_limitCaption.SetBounds(num3 + num4 / 2, num5, num4 / 2, 20);
			_limit.SetBounds(num3 + num4 / 2, num5 + 18, num4 / 2, 64);
			_toleranceCaption.SetBounds(num3, num5 + 83, num4 / 2, 19);
			_tolerance.SetBounds(num3, num5 + 101, num4 / 2, 37);
			_saved.SetBounds(num3 + num4 / 2, num5 + 90, num4 / 2, 45);
			num5 += 150;
		}
		_basicPanel.SetBounds(num3, num5, num4, flag ? 145 : 82);
		int num7 = 8;
		if (!flag)
		{
			int num8 = (num4 - num7 * 4) / 5;
			Label[] array = new Label[5] { _gear, _fuel, _cruise, _route, _condition };
			for (int i = 0; i < array.Length; i++)
			{
				array[i].SetBounds(i * (num8 + num7), 0, num8, 76);
			}
		}
		else
		{
			int num9 = (num4 - num7) / 2;
			_gear.SetBounds(0, 0, num9, 62);
			_fuel.SetBounds(num9 + num7, 0, num9, 62);
			_cruise.SetBounds(0, 70, num9, 62);
			_route.SetBounds(num9 + num7, 70, num9, 62);
			_condition.SetBounds(0, 140, num4, 62);
			_basicPanel.Height = 207;
		}
		num5 = _basicPanel.Bottom + 8;
		bool flag2 = num2 >= (flag ? 600 : 470);
		bool flag3 = num2 >= (flag ? 770 : 575);
		bool flag4 = num2 >= (flag ? 900 : 675);
		_jobPanel.Visible = flag2;
		_etaPanel.Visible = flag3;
		_damagePanel.Visible = flag4;
		if (flag2)
		{
			_jobPanel.SetBounds(num3, num5, num4, 124);
			int num10 = (num4 - num7) / 2;
			_cargo.SetBounds(0, 0, num10, 52);
			_destination.SetBounds(num10 + num7, 0, num10, 52);
			_weight.SetBounds(0, 60, num10, 60);
			_remaining.SetBounds(num10 + num7, 60, num10, 60);
			num5 = _jobPanel.Bottom + 8;
		}
		if (flag3)
		{
			_etaPanel.SetBounds(num3, num5, num4, 82);
			int num11 = (num4 - num7) / 2;
			_eta.SetBounds(0, 0, num11, 76);
			_average.SetBounds(num11 + num7, 0, num11, 76);
			num5 = _etaPanel.Bottom + 8;
		}
		if (flag4)
		{
			_damagePanel.SetBounds(num3, num5, num4, 80);
			int num12 = (num4 - num7 * 2) / 3;
			_truckDamage.SetBounds(0, 0, num12, 74);
			_trailerDamage.SetBounds(num12 + num7, 0, num12, 74);
			_cargoDamage.SetBounds((num12 + num7) * 2, 0, num4 - (num12 + num7) * 2, 74);
		}
		_hint.SetBounds(num3, Math.Max(num5 + 4, num2 - 35), num4, 26);
	}

	private async Task PollAsync()
	{
		if (_busy)
		{
			return;
		}
		_busy = true;
		try
		{
			JObject j = JObject.Parse(await _http.GetStringAsync("http://127.0.0.1:31377/api/ets2/telemetry"));
			double num = Math.Abs(D(j, "truck.speed", "Truck.Speed"));
			if (!IsValidRoadSpeed047(num))
			{
				num = 0.0;
			}
			double num2 = D(j, "navigation.speedLimit", "Navigation.SpeedLimit");
			double num3 = D(j, "truck.fuel", "Truck.Fuel");
			double num4 = D(j, "truck.fuelCapacity", "Truck.FuelCapacity");
			double num5 = D(j, "navigation.estimatedDistance", "Navigation.EstimatedDistance") / 1000.0;
			double num6 = D(j, "job.cargoMass", "Job.CargoMass");
			int num7 = (int)Math.Round(D(j, "truck.displayedGear", "Truck.DisplayedGear"));
			bool flag = B(j, "truck.cruiseControlOn", "Truck.CruiseControlOn");
			double a = D(j, "truck.cruiseControlSpeed", "Truck.CruiseControlSpeed");
			_toleranceValue = LoadTolerance045();
			if (IsValidRoadSpeed047(num) && num > 5.0)
			{
				_avgSpeed = ((_avgSpeed <= 0.0) ? num : (_avgSpeed * 0.94 + num * 0.06));
			}
			bool flag2 = num2 > 0.0 && num > num2 + _toleranceValue;
			_connected.Text = "● Conectado à telemetria";
			_connected.ForeColor = Color.FromArgb(87, 229, 140);
			_speed.Text = Math.Round(num) + " km/h";
			_speed.ForeColor = (flag2 ? Color.FromArgb(255, 119, 119) : Color.White);
			_limit.Text = ((num2 > 0.0) ? Math.Round(num2).ToString(CultureInfo.InvariantCulture) : "—") + " km/h";
			_limit.ForeColor = (flag2 ? Color.FromArgb(255, 76, 76) : Color.FromArgb(255, 132, 132));
			_tolerance.Text = "+" + Math.Round(_toleranceValue) + " km/h";
			Label gear = _gear;
			string text;
			if (num7 < 0)
			{
				text = "R" + Math.Abs(num7);
			}
			else
			{
				text = ((num7 == 0) ? "N" : ("D" + num7));
			}
			gear.Text = "MARCHA\r\n" + text;
			_fuel.Text = "COMBUSTÍVEL\r\n" + ((num4 > 0.0) ? (Math.Round(Clamp045(num3 / num4 * 100.0, 0.0, 100.0)) + "%") : "—");
			_cruise.Text = "CRUISE\r\n" + (flag ? (Math.Round(a) + " km/h") : "OFF");
			_route.Text = "ROTA\r\n" + ((num5 > 0.0) ? (Math.Round(num5) + " km") : "—");
			int num8 = Overall(j);
			_condition.Text = "CONDIÇÃO\r\n" + num8 + "%";
			string text2 = S(j, "job.cargo", "Job.Cargo");
			string text3 = S(j, "job.destinationCity", "Job.DestinationCity");
			_cargo.Text = "CARGA\r\n" + (string.IsNullOrWhiteSpace(text2) ? "Sem carga" : text2);
			_destination.Text = "DESTINO\r\n" + (string.IsNullOrWhiteSpace(text3) ? "—" : text3);
			_weight.Text = "PESO\r\n" + ((num6 > 0.0) ? FormatWeight045(num6) : "—");
			_remaining.Text = "DIST. RESTANTE\r\n" + ((num5 > 0.0) ? (Math.Round(num5) + " km") : "—");
			_average.Text = "VELOCIDADE MÉDIA\r\n" + ((IsValidRoadSpeed047(_avgSpeed) && _avgSpeed > 1.0) ? (Math.Round(_avgSpeed) + " km/h") : "—");
			_eta.Text = "TEMPO ESTIMADO\r\n" + Eta045(num5, _avgSpeed);
			double n = 100 - num8;
			double n2 = TrailerDamage045(j);
			double num9 = D(j, "job.cargoDamage", "Job.CargoDamage");
			if (num9 <= 1.001)
			{
				num9 *= 100.0;
			}
			_truckDamage.Text = "DANOS DO CAMINHÃO\r\n" + Math.Round(Clamp045(n, 0.0, 100.0)) + "%";
			_trailerDamage.Text = "DANOS DO REBOQUE\r\n" + Math.Round(Clamp045(n2, 0.0, 100.0)) + "%";
			_cargoDamage.Text = "DANOS DA CARGA\r\n" + Math.Round(Clamp045(num9, 0.0, 100.0)) + "%";
		}
		catch
		{
			_connected.Text = "● ETS2 desconectado";
			_connected.ForeColor = Color.FromArgb(255, 128, 128);
			_speed.Text = "0 km/h";
			_limit.Text = "— km/h";
			_route.Text = "ROTA\r\nETS2 OFF";
		}
		finally
		{
			_busy = false;
		}
	}

	private static string FormatWeight045(double kg)
	{
		if (kg >= 1000.0)
		{
			return (kg / 1000.0).ToString("0.#", CultureInfo.InvariantCulture) + " t";
		}
		return Math.Round(kg).ToString(CultureInfo.InvariantCulture) + " kg";
	}

	private static string Eta045(double remainKm, double avgSpeed)
	{
		if (remainKm <= 0.0 || !IsValidRoadSpeed047(avgSpeed) || avgSpeed < 10.0)
		{
			return "—";
		}
		int num = (int)Math.Ceiling(remainKm / avgSpeed * 60.0);
		if (num < 1)
		{
			return "< 1 min";
		}
		int num2 = num / 60;
		int num3 = num % 60;
		if (num2 <= 0)
		{
			return num3 + " min";
		}
		return num2 + " h " + num3.ToString("00") + " min";
	}

	private static double TrailerDamage045(JObject j)
	{
		double num = D(j, "trailer.wearBody", "Trailer.WearBody");
		double num2 = D(j, "trailer.wearChassis", "Trailer.WearChassis");
		double num3 = D(j, "trailer.wearWheels", "Trailer.WearWheels");
		if (num <= 1.001)
		{
			num *= 100.0;
		}
		if (num2 <= 1.001)
		{
			num2 *= 100.0;
		}
		if (num3 <= 1.001)
		{
			num3 *= 100.0;
		}
		return Clamp045((num + num2 + num3) / 3.0, 0.0, 100.0);
	}

	private static double LoadTolerance045()
	{
		try
		{
			string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GAT-LOG", "GAT-Telemetria", "gat-dash-settings.json");
			if (!File.Exists(path))
			{
				return 3.0;
			}
			return Clamp045(Convert.ToDouble(JObject.Parse(File.ReadAllText(path))["tolerance"] ?? ((JToken)3), CultureInfo.InvariantCulture), 0.0, 30.0);
		}
		catch
		{
			return 3.0;
		}
	}

	private static string S(JObject j, params string[] paths)
	{
		foreach (string path in paths)
		{
			try
			{
				JToken jToken = j.SelectToken(path);
				if (jToken != null)
				{
					return Convert.ToString(jToken) ?? "";
				}
			}
			catch
			{
			}
		}
		return "";
	}

	private static double D(JObject j, params string[] paths)
	{
		foreach (string path in paths)
		{
			try
			{
				JToken jToken = j.SelectToken(path);
				if (jToken != null)
				{
					if (jToken.Type == JTokenType.Integer || jToken.Type == JTokenType.Float)
					{
						return jToken.Value<double>();
					}
					string s = Convert.ToString(jToken, CultureInfo.InvariantCulture) ?? "";
					if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
					{
						return result;
					}
					if (double.TryParse(s, NumberStyles.Float, new CultureInfo("pt-BR"), out result))
					{
						return result;
					}
				}
			}
			catch
			{
			}
		}
		return 0.0;
	}

	private static bool IsValidRoadSpeed047(double value)
	{
		if (!double.IsNaN(value) && !double.IsInfinity(value) && value >= 0.0)
		{
			return value <= 300.0;
		}
		return false;
	}

	private static bool B(JObject j, params string[] paths)
	{
		foreach (string path in paths)
		{
			try
			{
				JToken jToken = j.SelectToken(path);
				if (jToken != null)
				{
					return Convert.ToBoolean(jToken);
				}
			}
			catch
			{
			}
		}
		return false;
	}

	private static int Overall(JObject j)
	{
		string[] array = new string[5] { "Engine", "Transmission", "Cabin", "Chassis", "Wheels" };
		double num = 0.0;
		int num2 = 0;
		string[] array2 = array;
		foreach (string text in array2)
		{
			double num3 = D(j, "truck.wear" + text, "Truck.Wear" + text);
			if (num3 <= 1.001)
			{
				num3 *= 100.0;
			}
			num += Clamp045(100.0 - num3, 0.0, 100.0);
			num2++;
		}
		if (num2 != 0)
		{
			return (int)Math.Round(num / (double)num2);
		}
		return 100;
	}

	private static double Clamp045(double n, double min, double max)
	{
		return Math.Max(min, Math.Min(max, n));
	}

	private void PaintBorder045(object sender, PaintEventArgs e)
	{
		using Pen pen = new Pen(Color.FromArgb(33, 154, 236), 1.2f);
		e.Graphics.DrawRectangle(pen, 0, 0, Math.Max(1, ClientSize.Width - 1), Math.Max(1, ClientSize.Height - 1));
	}

	protected override void WndProc(ref Message m)
	{
		base.WndProc(ref m);
		if (m.Msg == 132 && (int)m.Result == 1)
		{
			long num = m.LParam.ToInt64();
			int num2 = (short)(num & 0xFFFF);
			int num3 = (short)((num >> 16) & 0xFFFF);
			Point pt = PointToClient(new Point(num2, num3));
			bool flag = pt.X <= 8;
			bool flag2 = pt.X >= ClientSize.Width - 8;
			bool flag3 = pt.Y <= 8;
			bool flag4 = pt.Y >= ClientSize.Height - 8;
			if (flag & flag3)
			{
				m.Result = (IntPtr)13;
			}
			else if (flag2 & flag3)
			{
				m.Result = (IntPtr)14;
			}
			else if (flag & flag4)
			{
				m.Result = (IntPtr)16;
			}
			else if (flag2 & flag4)
			{
				m.Result = (IntPtr)17;
			}
			else if (flag)
			{
				m.Result = (IntPtr)10;
			}
			else if (flag2)
			{
				m.Result = (IntPtr)11;
			}
			else if (flag3)
			{
				m.Result = (IntPtr)12;
			}
			else if (flag4)
			{
				m.Result = (IntPtr)15;
			}
			else if (pt.Y <= _header.Bottom && !_close.Bounds.Contains(pt))
			{
				m.Result = (IntPtr)2;
			}
		}
	}
}
