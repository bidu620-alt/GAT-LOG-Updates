using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Speech.Synthesis;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GatTelemetry;

internal sealed class MainForm : Form
{
	private sealed class SteamIdentity
	{
		public string SteamId { get; set; } = string.Empty;

		public string PersonaName { get; set; } = string.Empty;
	}


	private sealed class ModernCard : Panel
	{
		public string Caption { get; set; } = string.Empty;

		public Color BorderColor { get; set; } = Color.FromArgb(38, 111, 205);

		public ModernCard()
		{
			SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
			BackColor = Color.FromArgb(7, 20, 36);
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			base.OnPaint(e);
			e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
			Rectangle rectangle = new Rectangle(1, 1, Math.Max(1, Width - 3), Math.Max(1, Height - 3));
			using (GraphicsPath graphicsPath = RoundedRect(rectangle, 12))
			using (Pen pen = new Pen(BorderColor, 1f))
			{
				e.Graphics.DrawPath(pen, graphicsPath);
			}
			if (!string.IsNullOrWhiteSpace(Caption))
			{
				using (Font font = new Font("Segoe UI Semibold", 11.5f, FontStyle.Bold))
				using (Brush brush = new SolidBrush(Color.FromArgb(232, 239, 249)))
				using (Brush dot = new SolidBrush(Color.FromArgb(31, 107, 220)))
				{
					e.Graphics.FillEllipse(dot, 18, 15, 25, 25);
					e.Graphics.DrawString(Caption, font, brush, 52f, 15f);
				}
			}
		}

		private static GraphicsPath RoundedRect(Rectangle bounds, int radius)
		{
			int diameter = radius * 2;
			GraphicsPath path = new GraphicsPath();
			Rectangle arc = new Rectangle(bounds.X, bounds.Y, diameter, diameter);
			path.AddArc(arc, 180f, 90f);
			arc.X = bounds.Right - diameter;
			path.AddArc(arc, 270f, 90f);
			arc.Y = bounds.Bottom - diameter;
			path.AddArc(arc, 0f, 90f);
			arc.X = bounds.Left;
			path.AddArc(arc, 90f, 90f);
			path.CloseFigure();
			return path;
		}
	}

	private sealed class TruckOutline : Control
	{
		public TruckOutline()
		{
			SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
			BackColor = Color.Transparent;
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			base.OnPaint(e);
			e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
			using (Pen pen = new Pen(Color.FromArgb(85, 91, 159, 255), 1.4f))
			{
				int w = Math.Max(80, Width - 8);
				int h = Math.Max(40, Height - 8);
				int y = h / 2;
				e.Graphics.DrawRectangle(pen, 8, y - 14, w / 2, 24);
				e.Graphics.DrawLine(pen, w / 2 + 8, y - 14, w * 3 / 4, y - 14);
				e.Graphics.DrawLine(pen, w * 3 / 4, y - 14, w - 4, y - 2);
				e.Graphics.DrawLine(pen, w - 4, y - 2, w - 4, y + 10);
				e.Graphics.DrawLine(pen, w - 4, y + 10, 8, y + 10);
				e.Graphics.DrawRectangle(pen, w * 3 / 4, y - 10, Math.Max(14, w / 7), 10);
				e.Graphics.DrawEllipse(pen, 22, y + 4, 16, 16);
				e.Graphics.DrawEllipse(pen, w * 3 / 4, y + 4, 16, 16);
				e.Graphics.DrawEllipse(pen, w * 3 / 4 + 20, y + 4, 16, 16);
			}
		}
	}

	private const string CurrentVersion = "1.0.32";

	private const string VersionUrl = "https://raw.githubusercontent.com/bidu620-alt/GAT-LOG-Updates/main/client_dotnet_version.json";

	private const string AccountAuthority = "https://api.gatlogets2.com.br";

	private readonly ApiClient _api = new ApiClient();

	private readonly TelemetryEngine _telemetry = new TelemetryEngine();

	private readonly TripJournal _tripJournal = new TripJournal();

	private readonly Timer _timer = new Timer
	{
		Interval = 1000
	};

	private readonly Timer _updateTimer = new Timer
	{
		Interval = 1800000
	};

	private List<ServerEntry> _servers;

	private ClientSettings _settings;

	private readonly string _deviceId;

	private bool _busy;

	private bool _waiting;

	private bool _loggedIn;

	private string _endpoint = string.Empty;

	private string _driver = string.Empty;

	private string _token = string.Empty;

	private string _accountUser = string.Empty;

	private string _accountToken = string.Empty;

	private DateTime _lastServerProbe = DateTime.MinValue;

	private DateTime _lastPlayersProbe = DateTime.MinValue;

	private DateTime _lastHeartbeat = DateTime.MinValue;

	private DateTime _lastTelemetry = DateTime.MinValue;

	private DateTime _lastAccountTelemetry = DateTime.MinValue;

	private DateTime _lastTripCapture = DateTime.MinValue;

	private DateTime _lastTripFlush = DateTime.MinValue;

	private const int MaxQueuedTelemetryPackets = 72000;
	private const int MaxBlackBoxPackets = 100000;

	private string LegacyCentralTelemetryQueueFile => Path.Combine(ClientStore.DataDir, "central-telemetry-queue.ndjson");
	private string CentralTelemetryQueueFile => Path.Combine(ClientStore.DataDir, "central-telemetry-queue.sec");
	private string CentralTripBlackBoxFile => Path.Combine(ClientStore.DataDir, "central-trip-blackbox.sec");
	private string CentralTelemetryKeyFile => Path.Combine(ClientStore.DataDir, "central-telemetry-key.dpapi");
	private string CentralJournalStateFile => Path.Combine(ClientStore.DataDir, "central-telemetry-chain.json");

	private ServerInfo _serverInfo = new ServerInfo();

	private RemoteVersion _availableUpdate;

	private JObject _latchedJob;

	private string _latchedJobKey = string.Empty;

	private SpeechSynthesizer _voice;

	private string _lastMissionState = string.Empty;

	private string _lastMissionId = string.Empty;

	private string _lastAnnouncedMissionId = string.Empty;

	private string _lastAnnouncedCompletedMissionId = string.Empty;

	private bool _missionStateKnown;

	private ComboBox cmbServers;

	private ComboBox cmbMapMode;

	private TextBox txtAccountUser;

	private TextBox txtAccountPassword;

	private Button btnAccountLogin;

	private Label lblAccount;

	private Button btnRemove;

	private Button btnEnter;

	private Button btnUpdate;

	private CheckBox chkAuto;

	private Label lblServer;

	private Label lblRoom;

	private Label lblDriver;

	private Label lblSession;

	private Label lblTruck;

	private Label lblTelemetry;

	private Label lblCargo;

	private Label lblRoute;

	private Label lblDistance;

	private Label lblSpeed;

	private Label lblWeight;

	private Label lblDamage;
	private Label lblDamageCargo;

	private Label lblDamageEngine;

	private Label lblDamageTransmission;

	private Label lblDamageCabin;

	private Label lblDamageChassis;

	private Label lblDamageWheels;

	private Label lblDamageTrailer;

	private Label lblPcRegister;

	private Label lblPcRegisterDetail;

	private Label lblWorkStatus;

	private Label lblVersion;

	private bool AccountReady
	{
		get
		{
			if (!string.IsNullOrWhiteSpace(_accountUser))
			{
				return !string.IsNullOrWhiteSpace(_accountToken);
			}
			return false;
		}
	}

	private string MapModeFile => Path.Combine(ClientStore.DataDir, "map_mode.txt");

	private string CurrentMapModeKey
	{
		get
		{
			string a = ((cmbMapMode == null) ? string.Empty : Convert.ToString(cmbMapMode.SelectedItem));
			if (string.Equals(a, "ProMods", StringComparison.OrdinalIgnoreCase))
			{
				return "promods";
			}
			if (string.Equals(a, "RBR", StringComparison.OrdinalIgnoreCase))
			{
				return "rbr";
			}
			if (string.Equals(a, "Rotas Brasil", StringComparison.OrdinalIgnoreCase))
			{
				return "rotas_brasil";
			}
			if (string.Equals(a, "EAA", StringComparison.OrdinalIgnoreCase))
			{
				return "eaa";
			}
			if (string.Equals(a, "Outro mapa", StringComparison.OrdinalIgnoreCase))
			{
				return "other";
			}
			return "base";
		}
	}

	private string CurrentMapModeLabel
	{
		get
		{
			string text = ((cmbMapMode == null) ? string.Empty : Convert.ToString(cmbMapMode.SelectedItem));
			if (!string.IsNullOrWhiteSpace(text))
			{
				return text;
			}
			return "Mapa Base";
		}
	}

	public MainForm()
	{
		Text = "GAT Telemetria C# 1.0.32";
		base.StartPosition = FormStartPosition.CenterScreen;
		MinimumSize = new Size(900, 700);
		base.Size = new Size(940, 740);
		BackColor = Color.FromArgb(4, 13, 25);
		ForeColor = Color.WhiteSmoke;
		Font = new Font("Segoe UI", 9f);
		base.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
		ClientStore.Ensure();
		_servers = ClientStore.LoadServers();
		_settings = ClientStore.LoadSettings();
		_deviceId = ClientStore.GetDeviceId();
		BuildUi();
		LoadMapMode();
		LoadServerList();
		chkAuto.Checked = _settings.AutoConnect;
		SelectLastServer();
		_timer.Tick += async delegate
		{
			await TickAsync();
		};
		_timer.Start();
		_updateTimer.Tick += async delegate
		{
			await CheckUpdateAsync(showNoUpdate: false);
		};
		_updateTimer.Start();
		base.Shown += async delegate
		{
			await RestoreAccountAsync();
			await RefreshServerInfoAsync(force: true);
			await CheckUpdateAsync(showNoUpdate: false);
			if (_settings.AutoConnect && AccountReady && cmbServers.SelectedItem is ServerEntry)
			{
				BeginWaiting(manual: false);
			}
		};
		base.FormClosed += delegate
		{
			_timer.Stop();
			_updateTimer.Stop();
			_api.Dispose();
			_telemetry.Dispose();
			try
			{
				_voice?.Dispose();
			}
			catch
			{
			}
		};
	}

	private void BuildUi()
	{
		SuspendLayout();
		Color accent = Color.FromArgb(67, 139, 255);
		Color green = Color.FromArgb(130, 224, 69);
		Color muted = Color.FromArgb(168, 181, 199);
		Color inputBack = Color.FromArgb(7, 18, 31);

		Label brand = new Label
		{
			Text = "GAT",
			Font = new Font("Segoe UI Black", 27f, FontStyle.Bold | FontStyle.Italic),
			AutoSize = true,
			ForeColor = Color.White,
			Location = new Point(28, 18)
		};
		Controls.Add(brand);

		Label title = new Label
		{
			Text = "GAT TELEMETRIA",
			Font = new Font("Segoe UI Semibold", 19f, FontStyle.Bold),
			AutoSize = true,
			ForeColor = Color.White,
			Location = new Point(128, 20)
		};
		Controls.Add(title);

		Label subtitle = new Label
		{
			Text = "Cliente ETS2  •  conexão automática",
			Font = new Font("Segoe UI", 10f),
			AutoSize = true,
			ForeColor = muted,
			Location = new Point(131, 55)
		};
		Controls.Add(subtitle);

		TruckOutline truckOutline = new TruckOutline
		{
			Width = 145,
			Height = 62,
			Left = ClientSize.Width - 170,
			Top = 10,
			Anchor = AnchorStyles.Top | AnchorStyles.Right
		};
		Controls.Add(truckOutline);

		ModernCard accountBox = NewCard("CONTA GAT", 24, 88, ClientSize.Width - 48, 172);
		accountBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
		Controls.Add(accountBox);

		accountBox.Controls.Add(NewCaption("Usuário", 28, 48, 205));
		accountBox.Controls.Add(NewCaption("Senha", 264, 48, 205));
		txtAccountUser = NewInput(28, 68, 215, false);
		txtAccountPassword = NewInput(264, 68, 215, true);
		btnAccountLogin = MakeButton("ENTRAR NA CONTA", 496, 66, 160, 34, async delegate
		{
			await AccountLoginClickedAsync();
		});
		accountBox.Controls.Add(txtAccountUser);
		accountBox.Controls.Add(txtAccountPassword);
		accountBox.Controls.Add(btnAccountLogin);

		lblAccount = MakeValue("Conta: não conectada", 28, 111, 610);
		lblAccount.Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold);
		lblAccount.ForeColor = green;
		accountBox.Controls.Add(lblAccount);

		lblDriver = MakeValue("Motorista: -", 28, 139, 610);
		lblDriver.Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold);
		lblDriver.ForeColor = accent;
		accountBox.Controls.Add(lblDriver);

		Panel accountDivider = new Panel
		{
			Left = accountBox.Width - 216,
			Top = 42,
			Width = 1,
			Height = 110,
			BackColor = Color.FromArgb(61, 77, 98),
			Anchor = AnchorStyles.Top | AnchorStyles.Right
		};
		accountBox.Controls.Add(accountDivider);

		lblPcRegister = new Label
		{
			Text = "Registro do PC",
			Left = accountBox.Width - 202,
			Top = 56,
			Width = 180,
			Height = 24,
			Anchor = AnchorStyles.Top | AnchorStyles.Right,
			ForeColor = muted,
			Font = new Font("Segoe UI Semibold", 10f, FontStyle.Bold)
		};
		accountBox.Controls.Add(lblPcRegister);

		Label firstTime = new Label
		{
			Text = "Primeira vez: confirmar dispositivo",
			Left = accountBox.Width - 202,
			Top = 84,
			Width = 180,
			Height = 22,
			Anchor = AnchorStyles.Top | AnchorStyles.Right,
			ForeColor = Color.Gainsboro
		};
		accountBox.Controls.Add(firstTime);

		lblPcRegisterDetail = new Label
		{
			Text = "Entre na conta para validar este PC.",
			Left = accountBox.Width - 202,
			Top = 113,
			Width = 180,
			Height = 44,
			Anchor = AnchorStyles.Top | AnchorStyles.Right,
			ForeColor = muted
		};
		accountBox.Controls.Add(lblPcRegisterDetail);

		cmbMapMode = new ComboBox
		{
			DropDownStyle = ComboBoxStyle.DropDownList,
			Left = 0,
			Top = 0,
			Width = 1,
			Visible = false
		};
		cmbMapMode.Items.AddRange(new object[6] { "Mapa Base", "ProMods", "RBR", "Rotas Brasil", "EAA", "Outro mapa" });
		cmbMapMode.SelectedIndexChanged += MapModeChanged;
		accountBox.Controls.Add(cmbMapMode);

		ModernCard serverBox = NewCard("COMBOIO / SERVIDOR  (OPCIONAL)", 24, 272, ClientSize.Width - 48, 132);
		serverBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
		Controls.Add(serverBox);

		cmbServers = new ComboBox
		{
			DropDownStyle = ComboBoxStyle.DropDownList,
			Left = 28,
			Top = 52,
			Width = 385,
			Height = 30,
			FlatStyle = FlatStyle.Flat,
			BackColor = inputBack,
			ForeColor = Color.White
		};
		cmbServers.SelectedIndexChanged += async delegate
		{
			await SelectedServerChangedAsync();
		};
		serverBox.Controls.Add(cmbServers);
		serverBox.Controls.Add(MakeButton("ADICIONAR", 426, 49, 105, 34, AddServerClicked));
		btnRemove = MakeButton("REMOVER", 540, 49, 100, 34, RemoveServerClicked);
		serverBox.Controls.Add(btnRemove);
		serverBox.Controls.Add(MakeButton("ATUALIZAR", 649, 49, 108, 34, async delegate
		{
			await RefreshServerInfoAsync(force: true);
		}));
		serverBox.Controls.Add(MakeButton("COPIAR ID", serverBox.Width - 132, 49, 106, 34, CopyRoomClicked));
		serverBox.Controls[serverBox.Controls.Count - 1].Anchor = AnchorStyles.Top | AnchorStyles.Right;

		lblServer = MakeValue("Servidor: aguardando", 28, 92, 335);
		lblServer.ForeColor = Color.Gainsboro;
		lblRoom = MakeValue("Sala: -", 365, 92, 420);
		lblRoom.ForeColor = accent;
		serverBox.Controls.Add(lblServer);
		serverBox.Controls.Add(lblRoom);

		// Componentes da conexão opcional continuam ativos, apenas não ocupam espaço na interface.
		chkAuto = new CheckBox
		{
			Checked = true,
			Visible = false
		};
		chkAuto.CheckedChanged += AutoChanged;
		Controls.Add(chkAuto);
		btnEnter = MakeButton("ENTRAR / AGUARDAR", 0, 0, 1, 1, EnterClicked);
		btnEnter.Visible = false;
		Controls.Add(btnEnter);
		lblSession = MakeValue("GAT LOG: parado", 0, 0, 1);
		lblSession.Visible = false;
		Controls.Add(lblSession);

		ModernCard telBox = NewCard("TELEMETRIA", 24, 416, ClientSize.Width - 48, 154);
		telBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
		Controls.Add(telBox);

		Panel sep1 = NewSeparator(304, 48, 84);
		Panel sep2 = NewSeparator(604, 48, 84);
		telBox.Controls.Add(sep1);
		telBox.Controls.Add(sep2);

		lblTruck = MakeValue("TruckSim GPS: aguardando", 28, 50, 260);
		lblCargo = MakeValue("Carga: Sem carga", 28, 81, 260);
		lblDistance = MakeValue("Restante: -", 28, 112, 260);
		lblTelemetry = MakeValue("Central GAT: aguardando", 330, 50, 260);
		lblRoute = MakeValue("Rota: -", 330, 81, 260);
		lblSpeed = MakeValue("Velocidade: 0 km/h", 330, 112, 260);
		lblWeight = MakeValue("Peso: -", 630, 50, 225);
		Label lblEta = MakeValue("Tempo estimado: -", 630, 81, 225);
		lblTruck.ForeColor = Color.Gainsboro;
		lblTelemetry.ForeColor = Color.Gainsboro;
		telBox.Controls.Add(lblTruck);
		telBox.Controls.Add(lblCargo);
		telBox.Controls.Add(lblDistance);
		telBox.Controls.Add(lblTelemetry);
		telBox.Controls.Add(lblRoute);
		telBox.Controls.Add(lblSpeed);
		telBox.Controls.Add(lblWeight);
		telBox.Controls.Add(lblEta);

		lblWorkStatus = new Label
		{
			Text = string.Empty,
			Left = 610,
			Top = 111,
			Width = 245,
			Height = 24,
			ForeColor = green,
			Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold),
			TextAlign = ContentAlignment.MiddleLeft,
			Visible = false
		};
		telBox.Controls.Add(lblWorkStatus);

		ModernCard damageBox = NewCard(string.Empty, 24, 582, ClientSize.Width - 48, 58);
		damageBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
		Controls.Add(damageBox);
		int damageWidth = 120;
		lblDamageCargo = AddDamageItem(damageBox, "Carga", 20, damageWidth);
		lblDamageEngine = AddDamageItem(damageBox, "Motor", 140, damageWidth);
		lblDamageTransmission = AddDamageItem(damageBox, "Câmbio", 260, damageWidth);
		lblDamageCabin = AddDamageItem(damageBox, "Cabine", 380, damageWidth);
		lblDamageChassis = AddDamageItem(damageBox, "Chassi", 500, damageWidth);
		lblDamageWheels = AddDamageItem(damageBox, "Rodas", 620, damageWidth);
		lblDamageTrailer = AddDamageItem(damageBox, "Reboque", 740, damageWidth);
		lblDamage = new Label { Visible = false };
		damageBox.Controls.Add(lblDamage);

		btnUpdate = MakeButton("↻  VERIFICAR ATUALIZAÇÃO", 24, 654, 240, 36, async delegate
		{
			await UpdateClickedAsync();
		});
		btnUpdate.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
		Controls.Add(btnUpdate);

		lblVersion = new Label
		{
			Text = "GAT Telemetria C# 1.0.32",
			AutoSize = true,
			ForeColor = Color.FromArgb(105, 118, 136),
			Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
			Location = new Point(ClientSize.Width - 235, 665)
		};
		Controls.Add(lblVersion);

		Resize += delegate
		{
			lblVersion.Left = Math.Max(24, ClientSize.Width - lblVersion.Width - 28);
		};

		ResumeLayout(false);
		PerformLayout();
	}

	private ModernCard NewCard(string caption, int x, int y, int width, int height)
	{
		return new ModernCard
		{
			Caption = caption,
			Left = x,
			Top = y,
			Width = width,
			Height = height,
			BackColor = Color.FromArgb(5, 18, 33)
		};
	}

	private Label NewCaption(string text, int x, int y, int width)
	{
		return new Label
		{
			Text = text,
			Left = x,
			Top = y,
			Width = width,
			Height = 18,
			ForeColor = Color.FromArgb(186, 198, 214)
		};
	}

	private TextBox NewInput(int x, int y, int width, bool password)
	{
		return new TextBox
		{
			Left = x,
			Top = y,
			Width = width,
			Height = 28,
			BorderStyle = BorderStyle.FixedSingle,
			BackColor = Color.FromArgb(7, 18, 31),
			ForeColor = Color.White,
			UseSystemPasswordChar = password
		};
	}

	private Panel NewSeparator(int x, int y, int height)
	{
		return new Panel
		{
			Left = x,
			Top = y,
			Width = 1,
			Height = height,
			BackColor = Color.FromArgb(57, 74, 96)
		};
	}

	private Label AddDamageItem(Control parent, string name, int x, int width)
	{
		Panel accentBar = new Panel
		{
			Left = x,
			Top = 17,
			Width = 3,
			Height = 23,
			BackColor = Color.FromArgb(67, 139, 255)
		};
		Label nameLabel = new Label
		{
			Text = name,
			Left = x + 11,
			Top = 18,
			Width = width - 62,
			Height = 22,
			ForeColor = Color.Gainsboro,
			Font = new Font("Segoe UI", 8.75f)
		};
		Label valueLabel = new Label
		{
			Text = "—",
			Left = x + width - 49,
			Top = 18,
			Width = 48,
			Height = 22,
			ForeColor = Color.FromArgb(130, 224, 69),
			Font = new Font("Segoe UI Semibold", 8.5f, FontStyle.Bold),
			TextAlign = ContentAlignment.MiddleRight
		};
		parent.Controls.Add(accentBar);
		parent.Controls.Add(nameLabel);
		parent.Controls.Add(valueLabel);
		if (x > 20)
		{
			parent.Controls.Add(new Panel
			{
				Left = x - 8,
				Top = 12,
				Width = 1,
				Height = 34,
				BackColor = Color.FromArgb(55, 71, 92)
			});
		}
		return valueLabel;
	}


	private GroupBox NewGroup(string text, int x, int y, int width, int height)
	{
		return new GroupBox
		{
			Text = text,
			Left = x,
			Top = y,
			Width = width,
			Height = height,
			ForeColor = Color.Gainsboro,
			BackColor = Color.FromArgb(28, 34, 45)
		};
	}

	private Label MakeValue(string text, int x, int y, int width)
	{
		return new Label
		{
			Text = text,
			Left = x,
			Top = y,
			Width = width,
			Height = 24,
			ForeColor = Color.FromArgb(210, 220, 234)
		};
	}

	private Button MakeButton(string text, int x, int y, int width, int height, EventHandler handler)
	{
		Button button = new Button();
		button.Text = text;
		button.Left = x;
		button.Top = y;
		button.Width = width;
		button.Height = height;
		button.FlatStyle = FlatStyle.Flat;
		button.BackColor = Color.FromArgb(11, 43, 82);
		button.ForeColor = Color.FromArgb(215, 229, 249);
		button.Font = new Font("Segoe UI Semibold", 8.5f, FontStyle.Regular);
		button.FlatAppearance.BorderSize = 1;
		button.FlatAppearance.BorderColor = Color.FromArgb(60, 137, 245);
		button.FlatAppearance.MouseOverBackColor = Color.FromArgb(18, 59, 111);
		button.FlatAppearance.MouseDownBackColor = Color.FromArgb(25, 72, 132);
		button.Cursor = Cursors.Hand;
		button.Click += handler;
		return button;
	}

	private void LoadServerList()
	{
		cmbServers.BeginUpdate();
		cmbServers.Items.Clear();
		foreach (ServerEntry server in _servers)
		{
			cmbServers.Items.Add(server);
		}
		cmbServers.EndUpdate();
		btnRemove.Enabled = _servers.Count > 0;
	}

	private void SelectLastServer()
	{
		if (_servers.Count == 0)
		{
			return;
		}
		int num = -1;
		if (!string.IsNullOrWhiteSpace(_settings.LastServer))
		{
			num = _servers.FindIndex((ServerEntry x) => string.Equals(ClientStore.NormalizeEndpoint(x.Endpoint), ClientStore.NormalizeEndpoint(_settings.LastServer), StringComparison.OrdinalIgnoreCase) || string.Equals(x.Name, _settings.LastServer, StringComparison.OrdinalIgnoreCase));
		}
		cmbServers.SelectedIndex = ((num >= 0) ? num : 0);
	}

	private async Task SelectedServerChangedAsync()
	{
		if (cmbServers.SelectedItem is ServerEntry serverEntry)
		{
			_settings.LastServer = serverEntry.Endpoint;
			ClientStore.SaveSettings(_settings);
			if (_loggedIn || _waiting)
			{
				_loggedIn = false;
				_waiting = chkAuto.Checked;
				_driver = string.Empty;
				_token = string.Empty;
			}
			_serverInfo = new ServerInfo();
			_lastServerProbe = DateTime.MinValue;
			await RefreshServerInfoAsync(force: true);
		}
	}

	private void AutoChanged(object sender, EventArgs e)
	{
		_settings.AutoConnect = chkAuto.Checked;
		ClientStore.SaveSettings(_settings);
		if (chkAuto.Checked && AccountReady && cmbServers.SelectedItem is ServerEntry)
		{
			BeginWaiting(manual: false);
		}
	}

	private async Task RestoreAccountAsync()
	{
		GatAccountCredential saved = ClientStore.LoadAccountCredential();
		if (saved == null)
		{
			SetAccountState(string.Empty, string.Empty);
			return;
		}
		txtAccountUser.Text = saved.User ?? string.Empty;
		ApiResponse apiResponse = await _api.AccountSessionAsync("https://api.gatlogets2.com.br", saved.Token);
		if (apiResponse.StatusCode == 200 && apiResponse.Json != null && ApiClient.Bool(apiResponse.Json["ok"]))
		{
			string text = ApiClient.Str(apiResponse.Json["user"]);
			if (string.IsNullOrWhiteSpace(text))
			{
				text = saved.User;
			}
			SetAccountState(text, saved.Token);
		}
		else if (apiResponse.StatusCode == 401)
		{
			ClientStore.ClearAccountCredential();
			SetAccountState(string.Empty, string.Empty);
		}
		else
		{
			SetAccountState(saved.User, saved.Token);
			lblAccount.Text = "Conta: @" + saved.User + " • central reconectando";
			lblAccount.ForeColor = Color.Gold;
		}
	}

	private async Task AccountLoginClickedAsync()
	{
		string text = (txtAccountUser.Text ?? string.Empty).Trim();
		string text2 = txtAccountPassword.Text ?? string.Empty;
		if (text.Length == 0 || text2.Length == 0)
		{
			MessageBox.Show("Informe o usuário e a senha criados no site GAT LOG.", "Conta GAT", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
			return;
		}
		btnAccountLogin.Enabled = false;
		btnAccountLogin.Text = "ENTRANDO...";
		try
		{
			ApiResponse apiResponse = await _api.AccountLoginAsync("https://api.gatlogets2.com.br", text, text2);
			if (apiResponse.StatusCode != 200 || apiResponse.Json == null || !ApiClient.Bool(apiResponse.Json["ok"]))
			{
				lblAccount.Text = "Conta: login inválido";
				MessageBox.Show("Usuário ou senha inválidos. Use a mesma conta cadastrada no site.", "Conta GAT", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
				return;
			}
			string text3 = ApiClient.Str(apiResponse.Json["user"]);
			string text4 = ApiClient.Str(apiResponse.Json["token"]);
			if (string.IsNullOrWhiteSpace(text3) || string.IsNullOrWhiteSpace(text4))
			{
				throw new InvalidOperationException("O servidor não retornou a sessão da Conta GAT.");
			}
			ClientStore.SaveAccountCredential(text3, text4);
			txtAccountUser.Text = text3;
			txtAccountPassword.Clear();
			SetAccountState(text3, text4);
			_waiting = false;
			_loggedIn = false;
			_driver = string.Empty;
			_token = string.Empty;
			lblSession.Text = "GAT LOG: conta reconhecida, aguardando sessão";
			if (chkAuto.Checked && cmbServers.SelectedItem is ServerEntry)
			{
				BeginWaiting(manual: false);
			}
		}
		catch (Exception ex)
		{
			ClientStore.Log("Conta GAT: " + ex);
			MessageBox.Show("Falha ao entrar na Conta GAT: " + ex.Message, "Conta GAT", MessageBoxButtons.OK, MessageBoxIcon.Hand);
		}
		finally
		{
			btnAccountLogin.Enabled = true;
			btnAccountLogin.Text = "ENTRAR NA CONTA";
		}
	}

	private void SetAccountState(string user, string token)
	{
		_accountUser = (user ?? string.Empty).Trim();
		_accountToken = token ?? string.Empty;
		if (AccountReady)
		{
			lblAccount.Text = "Conta: @" + _accountUser + " • PC";
			lblAccount.ForeColor = Color.FromArgb(130, 224, 69);
			if (lblDriver != null && (string.IsNullOrWhiteSpace(_driver) || lblDriver.Text == "Motorista: -"))
			{
				lblDriver.Text = "Motorista: " + _accountUser.ToUpperInvariant();
			}
			if (lblPcRegister != null)
			{
				lblPcRegister.Text = "Validando registro do PC";
				lblPcRegister.ForeColor = Color.Gold;
			}
			if (lblPcRegisterDetail != null)
			{
				lblPcRegisterDetail.Text = "A Central GAT confirma o vínculo automaticamente.";
			}
			return;
		}
		lblAccount.Text = "Conta: não conectada";
		lblAccount.ForeColor = Color.Gold;
		if (lblDriver != null)
		{
			lblDriver.Text = "Motorista: -";
		}
		if (lblPcRegister != null)
		{
			lblPcRegister.Text = "Registro do PC";
			lblPcRegister.ForeColor = Color.FromArgb(168, 181, 199);
		}
		if (lblPcRegisterDetail != null)
		{
			lblPcRegisterDetail.Text = "Entre na conta para validar este PC.";
		}
		lblSession.Text = "GAT LOG: entre na Conta GAT";
		lblTelemetry.Text = "Central GAT: aguardando conta";
	}

	private void SetPcRegistrationState(bool linked, string pairingCode)
	{
		if (lblPcRegister == null || lblPcRegisterDetail == null)
		{
			return;
		}
		if (linked)
		{
			lblPcRegister.Text = "Registro autenticado do PC";
			lblPcRegister.ForeColor = Color.FromArgb(130, 224, 69);
			lblPcRegisterDetail.Text = "Sua conta está vinculada a este PC para esta instalação.";
		}
		else
		{
			lblPcRegister.Text = "PC aguardando vínculo";
			lblPcRegister.ForeColor = Color.Gold;
			lblPcRegisterDetail.Text = string.IsNullOrWhiteSpace(pairingCode) ? "Confirme este dispositivo na primeira utilização." : ("Código de vínculo: " + pairingCode);
		}
	}

	private SpeechSynthesizer EnsureVoice()
	{
		if (_voice != null)
		{
			return _voice;
		}
		_voice = new SpeechSynthesizer();
		_voice.SetOutputToDefaultAudioDevice();
		_voice.Volume = 100;
		_voice.Rate = 0;
		return _voice;
	}

	private bool SpeakGat(string text, string logLabel)
	{
		try
		{
			EnsureVoice().SpeakAsync(text);
			ClientStore.Log("voz: " + logLabel);
			return true;
		}
		catch (Exception ex)
		{
			ClientStore.Log("voz indisponivel: " + ex.Message);
			try
			{
				_voice?.Dispose();
			}
			catch
			{
			}
			_voice = null;
			return false;
		}
	}

	private void AnnounceWorkStarted(string missionId)
	{
		string text = (string.IsNullOrWhiteSpace(missionId) ? "work-active" : missionId);
		if (!string.Equals(_lastAnnouncedMissionId, text, StringComparison.OrdinalIgnoreCase) && SpeakGat("Trabalho iniciado.", "trabalho iniciado" + (string.IsNullOrWhiteSpace(missionId) ? string.Empty : (" • " + missionId))))
		{
			_lastAnnouncedMissionId = text;
		}
	}

	private void AnnounceWorkCompleted(string missionId)
	{
		string text = (string.IsNullOrWhiteSpace(missionId) ? "work-completed" : missionId);
		if (!string.Equals(_lastAnnouncedCompletedMissionId, text, StringComparison.OrdinalIgnoreCase) && SpeakGat("Trabalho concluído.", "trabalho concluido" + (string.IsNullOrWhiteSpace(missionId) ? string.Empty : (" • " + missionId))))
		{
			_lastAnnouncedCompletedMissionId = text;
		}
	}

	private void UpdateWorkStatus(JObject progress)
	{
		if (lblWorkStatus != null)
		{
			JObject jObject = ((progress == null) ? null : (progress["mission"] as JObject));
			bool flag = string.Equals((jObject == null) ? string.Empty : Convert.ToString(jObject["state"] ?? ((JToken)string.Empty)).Trim().ToLowerInvariant(), "active", StringComparison.OrdinalIgnoreCase);
			bool flag2 = progress != null && ApiClient.Bool(progress["completed_now"]);
			lblWorkStatus.Text = (flag2 ? "TRABALHO CONCLUÍDO" : (flag ? "TRABALHO EM ANDAMENTO" : string.Empty));
			lblWorkStatus.ForeColor = Color.LimeGreen;
			lblWorkStatus.Visible = flag | flag2;
		}
	}

	private void CheckMissionVoice(JObject progress, bool startedNow, bool completedNow)
	{
		if (progress == null)
		{
			return;
		}
		JObject jObject = progress["mission"] as JObject;
		string text = ((jObject == null) ? string.Empty : Convert.ToString(jObject["id"] ?? ((JToken)string.Empty)).Trim());
		string text2 = ((jObject == null) ? string.Empty : Convert.ToString(jObject["state"] ?? ((JToken)string.Empty)).Trim().ToLowerInvariant());
		if (completedNow)
		{
			string missionId = ((!string.IsNullOrWhiteSpace(text)) ? text : _lastMissionId);
			AnnounceWorkCompleted(missionId);
		}
		if (jObject == null)
		{
			_lastMissionState = string.Empty;
			_lastMissionId = string.Empty;
			_missionStateKnown = true;
			return;
		}
		bool flag = string.Equals(text2, "active", StringComparison.OrdinalIgnoreCase);
		bool flag2 = !string.IsNullOrWhiteSpace(text) && string.Equals(_lastMissionId, text, StringComparison.OrdinalIgnoreCase);
		bool flag3 = ((_missionStateKnown & flag2) && !string.Equals(_lastMissionState, "active", StringComparison.OrdinalIgnoreCase)) & flag;
		if (startedNow | flag3 | flag)
		{
			AnnounceWorkStarted(text);
		}
		_lastMissionId = text;
		_lastMissionState = text2;
		_missionStateKnown = true;
	}

	private async Task CaptureTripJournalAsync()
	{
		if (!((DateTime.UtcNow - _lastTripCapture).TotalMilliseconds < 850.0))
		{
			JObject jObject = await _telemetry.ReadAsync();
			_lastTripCapture = DateTime.UtcNow;
			if (jObject != null)
			{
				_tripJournal.Observe(jObject);
			}
		}
	}

	private async Task FlushTripReceiptsAsync()
	{
		if (!AccountReady || (DateTime.UtcNow - _lastTripFlush).TotalSeconds < 4.0)
		{
			return;
		}
		_lastTripFlush = DateTime.UtcNow;
		TripReceipt receipt = _tripJournal.PeekPending();
		if (receipt == null)
		{
			return;
		}
		string driver = (string.IsNullOrWhiteSpace(_driver) ? _accountUser : _driver);
		ApiResponse apiResponse = await _api.SendTripReceiptAsync("https://api.gatlogets2.com.br", _accountToken, driver, receipt);
		if (apiResponse.StatusCode == 200 && apiResponse.Json != null && ApiClient.Bool(apiResponse.Json["ok"]))
		{
			_tripJournal.MarkSent(receipt.TripId);
			if (ApiClient.Bool(apiResponse.Json["completed_now"]) || ApiClient.Bool(apiResponse.Json["already_counted"]))
			{
				int num = ((apiResponse.Json["xp_awarded"] != null) ? apiResponse.Json["xp_awarded"].Value<int>() : 0);
				int num2 = ((apiResponse.Json["penalty_xp"] != null) ? apiResponse.Json["penalty_xp"].Value<int>() : 0);
				lblTelemetry.Text = ((num2 > 0) ? ("Central GAT: ENTREGA " + num + " XP (-" + num2 + ")") : ("Central GAT: ENTREGA " + num + " XP"));
			}
			ClientStore.Log("recibo de viagem confirmado: " + receipt.TripId);
			return;
		}
		if (apiResponse.StatusCode == 409 && apiResponse.Json != null)
		{
			string text = ApiClient.Str(apiResponse.Json["error"]);
			switch (text)
			{
			case "actual_distance_below_minimum":
			case "distance_not_verified":
			case "vehicle_changed":
			case "odometer_discontinuity":
			case "integrity_mod_blocked":
			case "integrity_not_verified":
				_tripJournal.MarkSent(receipt.TripId);
				lblTelemetry.Text = text switch
				{
					"integrity_not_verified" => "Central GAT: ENTREGA NAO VALIDADA - INTEGRIDADE", 
					"integrity_mod_blocked" => "Central GAT: ENTREGA NAO VALIDADA - MOD PROIBIDO", 
					"actual_distance_below_minimum" => "Central GAT: ENTREGA NAO VALIDADA - KM REAL INSUFICIENTE", 
					_ => "Central GAT: ENTREGA NAO VALIDADA - ODOMETRO/VEICULO", 
				};
				ClientStore.Log("entrega nao validada pela Central GAT: " + text + " / " + receipt.TripId);
				return;
			}
		}
		if (apiResponse.StatusCode != 0 && apiResponse.StatusCode != 401)
		{
			ClientStore.Log("recibo pendente " + receipt.TripId + ": HTTP " + apiResponse.StatusCode + " " + apiResponse.Text);
		}
	}

		private static byte[] JoinBytes(params byte[][] parts)
	{
		int total = parts.Where(x => x != null).Sum(x => x.Length);
		byte[] result = new byte[total];
		int offset = 0;
		foreach (byte[] part in parts)
		{
			if (part == null) continue;
			Buffer.BlockCopy(part, 0, result, offset, part.Length);
			offset += part.Length;
		}
		return result;
	}

	private static bool FixedBytesEqual(byte[] a, byte[] b)
	{
		if (a == null || b == null || a.Length != b.Length) return false;
		int diff = 0;
		for (int i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
		return diff == 0;
	}

	private static string Sha256Hex(string value)
	{
		using (SHA256 sha = SHA256.Create())
		{
			return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value ?? string.Empty))).Replace("-", string.Empty).ToLowerInvariant();
		}
	}

	private byte[] LoadOrCreateJournalMasterKey()
	{
		ClientStore.Ensure();
		byte[] entropy = Encoding.UTF8.GetBytes("GAT-TELEMETRIA-LOCAL-JOURNAL-V1");
		if (File.Exists(CentralTelemetryKeyFile))
		{
			byte[] protectedBytes = File.ReadAllBytes(CentralTelemetryKeyFile);
			return ProtectedData.Unprotect(protectedBytes, entropy, DataProtectionScope.CurrentUser);
		}
		byte[] key = new byte[32];
		using (RandomNumberGenerator rng = RandomNumberGenerator.Create()) rng.GetBytes(key);
		byte[] saved = ProtectedData.Protect(key, entropy, DataProtectionScope.CurrentUser);
		File.WriteAllBytes(CentralTelemetryKeyFile, saved);
		return key;
	}

	private static byte[] DeriveJournalKey(byte[] master, string purpose)
	{
		using (HMACSHA256 h = new HMACSHA256(master)) return h.ComputeHash(Encoding.UTF8.GetBytes("GAT-JOURNAL-" + purpose));
	}

	private string EncryptJournalPacket(JObject packet)
	{
		byte[] master = LoadOrCreateJournalMasterKey();
		byte[] encKey = DeriveJournalKey(master, "ENC");
		byte[] macKey = DeriveJournalKey(master, "MAC");
		byte[] plain = Encoding.UTF8.GetBytes(packet.ToString(Formatting.None));
		byte[] iv;
		byte[] cipher;
		using (Aes aes = Aes.Create())
		{
			aes.Key = encKey;
			aes.Mode = CipherMode.CBC;
			aes.Padding = PaddingMode.PKCS7;
			aes.GenerateIV();
			iv = aes.IV;
			using (ICryptoTransform transform = aes.CreateEncryptor()) cipher = transform.TransformFinalBlock(plain, 0, plain.Length);
		}
		byte[] version = new byte[] { 1 };
		byte[] macData = JoinBytes(version, iv, cipher);
		byte[] mac;
		using (HMACSHA256 h = new HMACSHA256(macKey)) mac = h.ComputeHash(macData);
		return Convert.ToBase64String(JoinBytes(version, iv, mac, cipher));
	}

	private JObject DecryptJournalPacket(string line)
	{
		byte[] blob = Convert.FromBase64String(line.Trim());
		if (blob.Length < 1 + 16 + 32 + 1 || blob[0] != 1) throw new InvalidDataException("registro local invalido");
		byte[] iv = new byte[16], mac = new byte[32], cipher = new byte[blob.Length - 49];
		Buffer.BlockCopy(blob, 1, iv, 0, iv.Length);
		Buffer.BlockCopy(blob, 17, mac, 0, mac.Length);
		Buffer.BlockCopy(blob, 49, cipher, 0, cipher.Length);
		byte[] master = LoadOrCreateJournalMasterKey();
		byte[] encKey = DeriveJournalKey(master, "ENC");
		byte[] macKey = DeriveJournalKey(master, "MAC");
		byte[] expected;
		using (HMACSHA256 h = new HMACSHA256(macKey)) expected = h.ComputeHash(JoinBytes(new byte[] { 1 }, iv, cipher));
		if (!FixedBytesEqual(mac, expected)) throw new InvalidDataException("integridade da caixa-preta local falhou");
		byte[] plain;
		using (Aes aes = Aes.Create())
		{
			aes.Key = encKey;
			aes.IV = iv;
			aes.Mode = CipherMode.CBC;
			aes.Padding = PaddingMode.PKCS7;
			using (ICryptoTransform transform = aes.CreateDecryptor()) plain = transform.TransformFinalBlock(cipher, 0, cipher.Length);
		}
		return JObject.Parse(Encoding.UTF8.GetString(plain));
	}

	private void StampCentralTelemetry(JObject tele)
	{
		if (tele == null) return;
		if (tele["gat_collected_at"] == null) tele["gat_collected_at"] = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
		if (tele["gat_packet_id"] == null) tele["gat_packet_id"] = Guid.NewGuid().ToString("N");
		string tripId = TextAny(tele, "gat_job_event_key", "job_latch_key", "gat_trip_id");
		if (string.IsNullOrWhiteSpace(tripId) && _latchedJob != null) tripId = _latchedJobKey;
		if (!string.IsNullOrWhiteSpace(tripId)) tele["gat_trip_id"] = tripId;
	}

	private void SealCentralTelemetry(JObject tele, string clientToken)
	{
		if (tele == null || string.IsNullOrWhiteSpace(clientToken)) return;
		StampCentralTelemetry(tele);
		if (tele["gat_journal_chain"] != null) return;
		long seq = 0;
		string previous = string.Empty;
		try
		{
			if (File.Exists(CentralJournalStateFile))
			{
				JObject state = JObject.Parse(File.ReadAllText(CentralJournalStateFile, Encoding.UTF8));
				seq = Math.Max(0L, Convert.ToInt64(state["seq"] ?? 0L, CultureInfo.InvariantCulture));
				previous = Convert.ToString(state["chain"], CultureInfo.InvariantCulture) ?? string.Empty;
			}
		}
		catch { seq = 0; previous = string.Empty; }
		seq++;
		JObject unsigned = (JObject)tele.DeepClone();
		foreach (string key in new[] { "gat_journal_seq", "gat_journal_prev", "gat_journal_chain", "gat_journal_payload_sha256", "gat_journal_version", "gat_journal_verified", "gat_journal_invalid" }) unsigned.Remove(key);
		string payloadHash = Sha256Hex(unsigned.ToString(Formatting.None));
		string packetId = TextAny(tele, "gat_packet_id");
		string collectedAt = TextAny(tele, "gat_collected_at");
		string tripId = TextAny(tele, "gat_trip_id");
		string canonical = packetId + "|" + collectedAt + "|" + tripId + "|" + seq.ToString(CultureInfo.InvariantCulture) + "|" + previous + "|" + payloadHash;
		byte[] signingKey;
		using (SHA256 sha = SHA256.Create()) signingKey = sha.ComputeHash(Encoding.UTF8.GetBytes("GAT-JOURNAL-V1|" + clientToken + "|" + _deviceId));
		string chain;
		using (HMACSHA256 h = new HMACSHA256(signingKey)) chain = BitConverter.ToString(h.ComputeHash(Encoding.UTF8.GetBytes(canonical))).Replace("-", string.Empty).ToLowerInvariant();
		tele["gat_journal_version"] = "1";
		tele["gat_journal_seq"] = seq;
		tele["gat_journal_prev"] = previous;
		tele["gat_journal_payload_sha256"] = payloadHash;
		tele["gat_journal_chain"] = chain;
		File.WriteAllText(CentralJournalStateFile, new JObject { ["seq"] = seq, ["chain"] = chain, ["packet_id"] = packetId, ["updated_at"] = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture) }.ToString(Formatting.None), Encoding.UTF8);
	}

	private void AppendCentralBlackBox(JObject tele)
	{
		if (tele == null) return;
		try
		{
			ClientStore.Ensure();
			File.AppendAllText(CentralTripBlackBoxFile, EncryptJournalPacket(tele) + Environment.NewLine, Encoding.ASCII);
			string[] lines = File.ReadAllLines(CentralTripBlackBoxFile, Encoding.ASCII).Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
			if (lines.Length > MaxBlackBoxPackets) File.WriteAllLines(CentralTripBlackBoxFile, lines.Skip(lines.Length - MaxBlackBoxPackets), Encoding.ASCII);
		}
		catch (Exception ex) { ClientStore.Log("caixa-preta local: " + ex.Message); }
	}

	private void QueueCentralTelemetry(JObject tele)
	{
		if (tele == null) return;
		try
		{
			ClientStore.Ensure();
			StampCentralTelemetry(tele);
			File.AppendAllText(CentralTelemetryQueueFile, EncryptJournalPacket(tele) + Environment.NewLine, Encoding.ASCII);
			string[] lines = File.ReadAllLines(CentralTelemetryQueueFile, Encoding.ASCII).Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
			if (lines.Length > MaxQueuedTelemetryPackets) File.WriteAllLines(CentralTelemetryQueueFile, lines.Skip(lines.Length - MaxQueuedTelemetryPackets), Encoding.ASCII);
			ClientStore.Log("telemetria criptografada salva para reenvio: " + TextAny(tele, "gat_packet_id"));
		}
		catch (Exception ex) { ClientStore.Log("fila local segura: " + ex.Message); }
	}

	private List<JObject> LoadCentralTelemetryQueue()
	{
		List<JObject> result = new List<JObject>();
		if (!File.Exists(CentralTelemetryQueueFile)) return result;
		foreach (string line in File.ReadAllLines(CentralTelemetryQueueFile, Encoding.ASCII))
		{
			if (string.IsNullOrWhiteSpace(line)) continue;
			result.Add(DecryptJournalPacket(line));
		}
		return result;
	}

	private void SaveCentralTelemetryQueue(IEnumerable<JObject> packets)
	{
		JObject[] rows = (packets ?? Enumerable.Empty<JObject>()).ToArray();
		if (rows.Length == 0)
		{
			if (File.Exists(CentralTelemetryQueueFile)) File.Delete(CentralTelemetryQueueFile);
			return;
		}
		string temp = CentralTelemetryQueueFile + ".tmp";
		File.WriteAllLines(temp, rows.Select(EncryptJournalPacket), Encoding.ASCII);
		if (File.Exists(CentralTelemetryQueueFile)) File.Delete(CentralTelemetryQueueFile);
		File.Move(temp, CentralTelemetryQueueFile);
	}

	private void MigrateLegacyCentralTelemetryQueue(string clientToken)
	{
		if (!File.Exists(LegacyCentralTelemetryQueueFile)) return;
		try
		{
			List<JObject> rows = new List<JObject>();
			foreach (string line in File.ReadAllLines(LegacyCentralTelemetryQueueFile, Encoding.UTF8))
			{
				if (string.IsNullOrWhiteSpace(line)) continue;
				JObject packet = JObject.Parse(line);
				StampCentralTelemetry(packet);
				SealCentralTelemetry(packet, clientToken);
				rows.Add(packet);
				AppendCentralBlackBox(packet);
			}
			if (rows.Count > 0)
			{
				List<JObject> existing = LoadCentralTelemetryQueue();
				existing.AddRange(rows);
				SaveCentralTelemetryQueue(existing);
			}
			File.Delete(LegacyCentralTelemetryQueueFile);
			ClientStore.Log("fila antiga migrada para caixa-preta criptografada: " + rows.Count + " pacote(s)");
		}
		catch (Exception ex) { ClientStore.Log("migracao da fila antiga: " + ex.Message); }
	}

	private async Task<int> FlushCentralTelemetryQueueAsync(string driver, string clientToken)
	{
		MigrateLegacyCentralTelemetryQueue(clientToken);
		List<JObject> packets;
		try { packets = LoadCentralTelemetryQueue(); }
		catch (Exception ex) { ClientStore.Log("fila local recusada por integridade: " + ex.Message); lblTelemetry.Text = "Central GAT: caixa-preta local com erro de integridade"; return 1; }
		if (packets.Count == 0) return 0;
		lblTelemetry.Text = "Central GAT: enviando viagem pendente...";
		int sent = 0;
		int limit = Math.Min(240, packets.Count);
		for (int i = 0; i < limit; i++)
		{
			JObject packet = packets[i];
			ApiResponse response = await _api.SendTelemetryAsync("https://api.gatlogets2.com.br", driver, _deviceId, clientToken, packet);
			if (response.StatusCode != 200 || response.Json == null || !ApiClient.Bool(response.Json["ok"])) break;
			sent++;
		}
		if (sent > 0)
		{
			packets.RemoveRange(0, sent);
			SaveCentralTelemetryQueue(packets);
			ClientStore.Log("telemetria pendente confirmada pela Central: " + sent + " pacote(s)");
		}
		return packets.Count;
	}
	private async Task SendCentralTelemetryAsync()
	{
		if (!AccountReady)
		{
			return;
		}
		await FlushTripReceiptsAsync();
		if ((DateTime.UtcNow - _lastAccountTelemetry).TotalMilliseconds < 1200.0)
		{
			return;
		}
		JObject tele = await _telemetry.ReadAsync();
		_lastAccountTelemetry = DateTime.UtcNow;
		tele = StabilizeJobTelemetry(tele);
		if (tele == null)
		{
			lblTruck.Text = "TruckSim GPS: aguardando";
			lblTelemetry.Text = "Central GAT: aguardando ETS2";
			return;
		}
		tele["gat_account_user"] = _accountUser;
		tele["gat_client_version"] = "1.0.32";
		ModIntegrityResult modIntegrityResult = ModIntegrityScanner.Check();
		tele["gat_integrity_status"] = modIntegrityResult.Status ?? "unknown";
		tele["gat_integrity_reason"] = modIntegrityResult.Reason ?? string.Empty;
		tele["gat_integrity_evidence_hash"] = modIntegrityResult.EvidenceHash ?? string.Empty;
		if (modIntegrityResult.Matches != null && modIntegrityResult.Matches.Length != 0)
		{
			tele["gat_integrity_matches"] = JArray.FromObject(modIntegrityResult.Matches);
		}
		tele["gat_map"] = CurrentMapModeKey;
		tele["gat_map_label"] = CurrentMapModeLabel;
		StampCentralTelemetry(tele);
		lblTruck.Text = "TruckSim GPS: CONECTADO";
		UpdateTelemetryDisplay(TelemetryEngine.BuildDisplay(tele));
		string centralDriver = _accountUser;
		CredentialEntry credential = ClientStore.FindCredential("https://api.gatlogets2.com.br", centralDriver);
		string centralClientToken = ClientStore.GetPlainToken(credential);
		if (string.IsNullOrWhiteSpace(centralClientToken))
		{
			ApiResponse apiResponse = await _api.LoginAsync("https://api.gatlogets2.com.br", centralDriver, _deviceId, string.Empty, _accountUser, _accountToken);
			if (apiResponse.StatusCode == 428 && apiResponse.Json != null)
			{
				string text = ApiClient.Str(apiResponse.Json["pairing_code"]);
				lblAccount.Text = (string.IsNullOrWhiteSpace(text) ? ("Conta: @" + _accountUser) : ("Vincular PC: " + text));
				lblAccount.ForeColor = Color.Gold;
				SetPcRegistrationState(false, text);
				lblTelemetry.Text = (string.IsNullOrWhiteSpace(text) ? "Central GAT: computador ainda nao vinculado" : ("Central GAT: digite o codigo " + text + " no site"));
				return;
			}
			if (apiResponse.StatusCode == 200 && apiResponse.Json != null && ApiClient.Bool(apiResponse.Json["ok"]))
			{
				centralClientToken = ApiClient.Str(apiResponse.Json["token"]);
				if (!string.IsNullOrWhiteSpace(centralClientToken))
				{
					ClientStore.SaveCredential("https://api.gatlogets2.com.br", centralDriver, centralClientToken);
					lblAccount.Text = "Conta: @" + _accountUser + " • PC vinculado";
					lblAccount.ForeColor = Color.FromArgb(130, 224, 69);
					SetPcRegistrationState(true, string.Empty);
				}
			}
			if (string.IsNullOrWhiteSpace(centralClientToken))
			{
				lblTelemetry.Text = ((apiResponse.StatusCode == 0) ? "Central GAT: reconectando..." : ("Central GAT: falha ao vincular HTTP " + apiResponse.StatusCode));
				return;
			}
		}
		SealCentralTelemetry(tele, centralClientToken);
		AppendCentralBlackBox(tele);
		int pendingBeforeCurrent = await FlushCentralTelemetryQueueAsync(centralDriver, centralClientToken);
		if (pendingBeforeCurrent > 0)
		{
			QueueCentralTelemetry(tele);
			lblTelemetry.Text = "Central GAT: viagem salva â€¢ aguardando servidor";
			return;
		}
		ApiResponse apiResponse2 = await _api.SendTelemetryAsync("https://api.gatlogets2.com.br", centralDriver, _deviceId, centralClientToken, tele);
		if (apiResponse2.StatusCode == 200 && apiResponse2.Json != null && ApiClient.Bool(apiResponse2.Json["ok"]))
		{
			if (apiResponse2.Json["mission_event"] is JObject jObject)
			{
				string a = ApiClient.Str(jObject["type"]);
				if (string.Equals(a, "mission_in_progress", StringComparison.OrdinalIgnoreCase))
				{
					apiResponse2.Json["started"] = true;
					if (jObject["mission"] != null)
					{
						apiResponse2.Json["mission"] = jObject["mission"];
					}
				}
				if (string.Equals(a, "delivery_completed", StringComparison.OrdinalIgnoreCase))
				{
					apiResponse2.Json["completed_now"] = true;
				}
			}
			lblAccount.Text = "Conta: @" + _accountUser + " • PC vinculado";
			lblAccount.ForeColor = Color.FromArgb(130, 224, 69);
			SetPcRegistrationState(true, string.Empty);
			bool flag = ApiClient.Bool(apiResponse2.Json["started"]);
			bool flag2 = ApiClient.Bool(apiResponse2.Json["completed_now"]);
			CheckMissionVoice(apiResponse2.Json, flag, flag2);
			UpdateWorkStatus(apiResponse2.Json);
			if (flag2)
			{
				lblTelemetry.Text = "Central GAT: ONLINE • MISSÃO CONCLUÍDA";
			}
			else if (flag)
			{
				lblTelemetry.Text = "Central GAT: ONLINE • MISSÃO INICIADA";
			}
			else if (BoolAny(tele, "job_latched") || BoolAny(tele, "on_job"))
			{
				lblTelemetry.Text = "Central GAT: ONLINE • TRABALHO EM ANDAMENTO";
			}
			else
			{
				lblTelemetry.Text = "Central GAT: ONLINE";
			}
		}
		else if (apiResponse2.StatusCode == 401)
		{
			ApiResponse apiResponse3 = await _api.LoginAsync("https://api.gatlogets2.com.br", centralDriver, _deviceId, centralClientToken, _accountUser, _accountToken);
			if (apiResponse3.StatusCode == 428 && apiResponse3.Json != null)
			{
				string text2 = ApiClient.Str(apiResponse3.Json["pairing_code"]);
				lblAccount.Text = "Vincular PC: " + text2;
				lblAccount.ForeColor = Color.Gold;
				SetPcRegistrationState(false, text2);
				lblTelemetry.Text = "Central GAT: digite o codigo " + text2 + " no site";
			}
			else
			{
				lblTelemetry.Text = "Central GAT: dispositivo precisa ser vinculado";
			}
		}
		else if (apiResponse2.StatusCode == 0)
		{
			QueueCentralTelemetry(tele);
			lblTelemetry.Text = "Central GAT: viagem salva â€¢ aguardando servidor";
		}
		else if (apiResponse2.StatusCode == 429 || apiResponse2.StatusCode >= 500)
		{
			QueueCentralTelemetry(tele);
			lblTelemetry.Text = "Central GAT: viagem salva â€¢ aguardando servidor";
		}
		else if (apiResponse2.StatusCode == 404)
		{
			lblTelemetry.Text = "Central GAT: atualize o servidor central";
		}
		else
		{
			lblTelemetry.Text = "Central GAT: falha HTTP " + apiResponse2.StatusCode;
		}
	}

	private void LoadMapMode()
	{
		if (cmbMapMode == null)
		{
			return;
		}
		string text = "base";
		try
		{
			if (File.Exists(MapModeFile))
			{
				text = (File.ReadAllText(MapModeFile) ?? string.Empty).Trim().ToLowerInvariant();
			}
		}
		catch
		{
		}
		cmbMapMode.SelectedIndex = text switch
		{
			"other" => 5, 
			"eaa" => 4, 
			"rotas_brasil" => 3, 
			"rbr" => 2, 
			"promods" => 1, 
			_ => 0, 
		};
	}

	private void MapModeChanged(object sender, EventArgs e)
	{
		try
		{
			ClientStore.Ensure();
			File.WriteAllText(MapModeFile, CurrentMapModeKey);
			ClientStore.Log("mapa em uso: " + CurrentMapModeLabel);
		}
		catch
		{
		}
	}

	private void EnterClicked(object sender, EventArgs e)
	{
		if (!AccountReady)
		{
			MessageBox.Show("Entre primeiro com a mesma conta criada no site GAT LOG.", "GAT Telemetria", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
		}
		else if (!(cmbServers.SelectedItem is ServerEntry))
		{
			MessageBox.Show("Adicione ou selecione um servidor primeiro.", "GAT Telemetria", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
		}
		else
		{
			BeginWaiting(manual: true);
		}
	}

	private void BeginWaiting(bool manual)
	{
		if (!AccountReady)
		{
			lblSession.Text = "GAT LOG: entre na Conta GAT";
			lblTelemetry.Text = "Envio: aguardando conta";
		}
		else if (cmbServers.SelectedItem is ServerEntry serverEntry)
		{
			_endpoint = ClientStore.NormalizeEndpoint(serverEntry.Endpoint);
			_settings.LastServer = _endpoint;
			ClientStore.SaveSettings(_settings);
			_waiting = true;
			if (manual)
			{
				_loggedIn = false;
			}
			lblSession.Text = "GAT LOG: aguardando sessão...";
			lblTelemetry.Text = "Envio: aguardando motorista";
			ClientStore.Log("aguardando sessao em " + _endpoint);
		}
	}

	private JObject StabilizeJobTelemetry(JObject tele)
	{
		// job-v2: o GAT Telemetria apenas observa o ETS2.
		// A Central GAT e a unica autoridade que decide entregue x cancelado.
		if (tele == null)
		{
			return null;
		}

		string cargo = TextAny(tele, "cargo_name", "job.cargoName", "job.cargo");
		string cargoId = TextAny(tele, "cargo_id", "job.cargoId", "Job.CargoId");
		string source = TextAny(tele, "source_city", "job.sourceCity", "Job.SourceCity");
		string destination = TextAny(tele, "destination_city", "job.destinationCity", "Job.DestinationCity");
		double mass = NumberAny(tele, "mass_kg", "cargoMass", "cargo_mass", "job.cargoMass", "Job.CargoMass");
		double planned = NumberAny(tele, "planned_distance_km", "job.plannedDistanceKm", "Job.PlannedDistanceKm");
		double remaining = NumberAny(tele, "remaining_km");
		bool rawOnJob = BoolAny(tele, "gameplay.onJob", "onJob", "job.onJob", "job.active");
		bool rawHasJob = rawOnJob && (!string.IsNullOrWhiteSpace(cargo) || !string.IsNullOrWhiteSpace(cargoId)) && mass > 0.0;

		if (rawHasJob)
		{
			string oldCargoId = (_latchedJob == null) ? string.Empty : TextAny(_latchedJob, "cargo_id");
			string oldSource = (_latchedJob == null) ? string.Empty : TextAny(_latchedJob, "source_city");
			string oldDestination = (_latchedJob == null) ? string.Empty : TextAny(_latchedJob, "destination_city");
			bool sameRawJob = _latchedJob != null &&
				(string.IsNullOrWhiteSpace(cargoId) || string.IsNullOrWhiteSpace(oldCargoId) || string.Equals(cargoId, oldCargoId, StringComparison.OrdinalIgnoreCase)) &&
				(string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(oldSource) || string.Equals(source, oldSource, StringComparison.OrdinalIgnoreCase)) &&
				(string.IsNullOrWhiteSpace(destination) || string.IsNullOrWhiteSpace(oldDestination) || string.Equals(destination, oldDestination, StringComparison.OrdinalIgnoreCase));

			if (!sameRawJob)
			{
				_latchedJob = new JObject();
				CopyValue(tele, _latchedJob, "cargo_name", "cargo_name", "job.cargoName", "job.cargo");
				CopyValue(tele, _latchedJob, "cargo_id", "cargo_id", "job.cargoId", "Job.CargoId");
				CopyValue(tele, _latchedJob, "mass_kg", "mass_kg", "cargoMass", "cargo_mass", "job.cargoMass", "Job.CargoMass");
				CopyValue(tele, _latchedJob, "source_city", "source_city", "job.sourceCity", "Job.SourceCity");
				CopyValue(tele, _latchedJob, "source_city_id", "source_city_id", "job.sourceCityId", "Job.SourceCityId");
				CopyValue(tele, _latchedJob, "destination_city", "destination_city", "job.destinationCity", "Job.DestinationCity");
				CopyValue(tele, _latchedJob, "destination_city_id", "destination_city_id", "job.destinationCityId", "Job.DestinationCityId");
				_latchedJob["planned_distance_km"] = (planned > 0.0) ? planned : remaining;
				_latchedJobKey = Guid.NewGuid().ToString("N");
				ClientStore.Log("JOB OBSERVED START | trip=" + _latchedJobKey + " | " + JobSummary(_latchedJob));
			}

			tele["gat_schema"] = "job-v2";
			tele["gat_job_state"] = "active";
			tele["gat_job_event"] = string.Empty;
			tele["gat_trip_id"] = _latchedJobKey;
			tele["job_latched"] = true;
			tele["job_latch_key"] = _latchedJobKey;
			tele["on_job"] = true;
			return tele;
		}

		// O ETS2 nao possui mais trabalho carregado. Nao reaproveitamos carga/rota antigas
		// nos campos normais. Enviamos somente o trip_id anterior para a Central fechar
		// a viagem usando os sinais brutos e o recibo jobDeliveredDetails.
		if (_latchedJob != null)
		{
			string endedTrip = _latchedJobKey;
			tele["gat_schema"] = "job-v2";
			tele["gat_job_state"] = "idle";
			tele["gat_job_event"] = string.Empty;
			tele["gat_trip_id"] = endedTrip;
			tele["gat_previous_cargo_name"] = TextAny(_latchedJob, "cargo_name");
			tele["gat_previous_cargo_id"] = TextAny(_latchedJob, "cargo_id");
			tele["job_latched"] = false;
			tele["job_latch_key"] = endedTrip;
			tele["on_job"] = false;
			ClientStore.Log("JOB OBSERVED END | trip=" + endedTrip + " | Central decidira o resultado");
			_latchedJob = null;
			_latchedJobKey = string.Empty;
			return tele;
		}

		tele["gat_schema"] = "job-v2";
		tele["gat_job_state"] = "idle";
		tele["gat_job_event"] = string.Empty;
		tele["job_latched"] = false;
		tele["on_job"] = false;
		return tele;
	}
private static void CopyValue(JObject a, JObject b, string output, params string[] paths)
	{
		foreach (string path in paths)
		{
			JToken jToken = a.SelectToken(path, errorWhenNoMatch: false);
			if (jToken != null && jToken.Type != JTokenType.Null && !string.IsNullOrWhiteSpace(jToken.ToString()))
			{
				b[output] = jToken.DeepClone();
				break;
			}
		}
	}

	private static string TextAny(JObject a, params string[] paths)
	{
		foreach (string path in paths)
		{
			JToken jToken = a.SelectToken(path, errorWhenNoMatch: false);
			if (jToken != null && jToken.Type != JTokenType.Null && !string.IsNullOrWhiteSpace(jToken.ToString()))
			{
				return jToken.ToString().Trim();
			}
		}
		return string.Empty;
	}

	private static double NumberAny(JObject a, params string[] paths)
	{
		foreach (string path in paths)
		{
			JToken jToken = a.SelectToken(path, errorWhenNoMatch: false);
			if (jToken != null && double.TryParse(jToken.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var result))
			{
				return result;
			}
		}
		return 0.0;
	}

	private static bool BoolAny(JObject a, params string[] paths)
	{
		foreach (string path in paths)
		{
			JToken jToken = a.SelectToken(path, errorWhenNoMatch: false);
			if (jToken != null && ((bool.TryParse(jToken.ToString(), out var result) & result) || jToken.ToString() == "1"))
			{
				return true;
			}
		}
		return false;
	}

	private static string JobSummary(JObject a)
	{
		return TextAny(a, "cargo_name") + " | " + NumberAny(a, "mass_kg").ToString("0") + " kg | " + NumberAny(a, "planned_distance_km").ToString("0.0") + " km | " + TextAny(a, "source_city") + " > " + TextAny(a, "destination_city");
	}

	private async Task TickAsync()
	{
		if (_busy)
		{
			return;
		}
		_busy = true;
		try
		{
			await CaptureTripJournalAsync();
			if (!AccountReady)
			{
				_loggedIn = false;
				_waiting = false;
				lblTelemetry.Text = "Central GAT: aguardando conta";
				return;
			}
			await SendCentralTelemetryAsync();
			if (cmbServers.SelectedItem is ServerEntry serverEntry)
			{
				_endpoint = ClientStore.NormalizeEndpoint(serverEntry.Endpoint);
				if ((DateTime.UtcNow - _lastServerProbe).TotalSeconds >= 4.0)
				{
					await RefreshServerInfoAsync(force: false);
				}
			}
			if ((!_waiting && !_loggedIn) || string.IsNullOrWhiteSpace(_endpoint))
			{
				return;
			}
			if (!_serverInfo.Reachable)
			{
				_loggedIn = false;
				lblSession.Text = "GAT LOG: servidor indisponível";
				return;
			}
			if (_serverInfo.Supported && !_serverInfo.Online)
			{
				_loggedIn = false;
				lblSession.Text = "GAT LOG: servidor ETS2 offline";
				return;
			}
			PlayersResult players = null;
			if ((DateTime.UtcNow - _lastPlayersProbe).TotalSeconds >= 2.0)
			{
				players = await _api.GetPlayersAsync(_endpoint);
				_lastPlayersProbe = DateTime.UtcNow;
			}
			JObject identityTelemetry;
			if (players != null && players.Ok)
			{
				identityTelemetry = null;
				try
				{
					identityTelemetry = await _telemetry.ReadAsync();
				}
				catch
				{
				}
				string text = ChooseDriver(players.Players, identityTelemetry);
				if (string.IsNullOrWhiteSpace(text))
				{
					_loggedIn = false;
					lblDriver.Text = "Motorista: -";
					lblSession.Text = ((players.Players.Count == 0) ? "GAT LOG: aguardando você entrar na sessão" : "GAT LOG: aguardando motorista conhecido");
					return;
				}
				if (!string.Equals(_driver, text, StringComparison.OrdinalIgnoreCase))
				{
					_driver = text;
					_loggedIn = false;
				}
			}
			if (!_loggedIn && (string.IsNullOrWhiteSpace(_driver) || !(await LoginAsync(_driver))))
			{
				return;
			}
			ApiResponse hb;
			bool flag;
			if ((DateTime.UtcNow - _lastHeartbeat).TotalSeconds >= 3.0)
			{
				hb = await _api.HeartbeatAsync(_endpoint, _driver, _deviceId, _token);
				_lastHeartbeat = DateTime.UtcNow;
				if (!IsAccepted(hb))
				{
					flag = NeedsTokenRenewal(hb);
					if (flag)
					{
						flag = await LoginAsync(_driver, forceNewToken: true);
					}
					if (flag)
					{
						hb = await _api.HeartbeatAsync(_endpoint, _driver, _deviceId, _token);
						_lastHeartbeat = DateTime.UtcNow;
					}
					if (!IsAccepted(hb))
					{
						_loggedIn = false;
						return;
					}
				}
			}
			if (!((DateTime.UtcNow - _lastTelemetry).TotalMilliseconds >= 900.0))
			{
				return;
			}
			identityTelemetry = await _telemetry.ReadAsync();
			_lastTelemetry = DateTime.UtcNow;
			identityTelemetry = StabilizeJobTelemetry(identityTelemetry);
			if (identityTelemetry == null)
			{
				lblTruck.Text = "TruckSim GPS: aguardando";
				lblTelemetry.Text = "Envio: conectado, sem telemetria";
				return;
			}
			lblTruck.Text = "TruckSim GPS: CONECTADO";
			UpdateTelemetryDisplay(TelemetryEngine.BuildDisplay(identityTelemetry));
			hb = await _api.SendTelemetryAsync(_endpoint, _driver, _deviceId, _token, identityTelemetry);
			flag = !IsAccepted(hb) && NeedsTokenRenewal(hb);
			if (flag)
			{
				flag = await LoginAsync(_driver, forceNewToken: true);
			}
			if (flag)
			{
				hb = await _api.SendTelemetryAsync(_endpoint, _driver, _deviceId, _token, identityTelemetry);
			}
			if (!IsAccepted(hb))
			{
				ClientStore.Log("telemetria opcional do comboio falhou: " + hb.StatusCode + " " + hb.Text);
			}
		}
		catch (Exception ex)
		{
			ClientStore.Log("tick: " + ex);
		}
		finally
		{
			_busy = false;
		}
	}

	private string ChooseDriver(List<string> players, JObject localTelemetry)
	{
		if (players == null || players.Count == 0)
		{
			return string.Empty;
		}
		if (!string.IsNullOrWhiteSpace(_driver))
		{
			string text = MatchPlayer(_driver, players);
			if (!string.IsNullOrWhiteSpace(text))
			{
				return text;
			}
		}
		CredentialEntry credentialEntry = ClientStore.FindCredential(_endpoint, _settings.LastDriver);
		if (credentialEntry != null)
		{
			string text2 = MatchPlayer(credentialEntry.Driver, players);
			if (!string.IsNullOrWhiteSpace(text2))
			{
				return text2;
			}
		}
		if (!string.IsNullOrWhiteSpace(_settings.LastDriver))
		{
			string text3 = MatchPlayer(_settings.LastDriver, players);
			if (!string.IsNullOrWhiteSpace(text3))
			{
				return text3;
			}
		}
		string text4 = FindTelemetryDriverHint(localTelemetry, players, 0);
		if (!string.IsNullOrWhiteSpace(text4))
		{
			ClientStore.Log("motorista identificado pela telemetria local: " + text4);
			return text4;
		}
		SteamIdentity localSteamIdentity = GetLocalSteamIdentity();
		if (localSteamIdentity != null && !string.IsNullOrWhiteSpace(localSteamIdentity.PersonaName))
		{
			string text5 = MatchPlayer(localSteamIdentity.PersonaName, players);
			if (!string.IsNullOrWhiteSpace(text5))
			{
				ClientStore.Log("motorista identificado pela Steam: " + localSteamIdentity.SteamId + " -> " + text5);
				return text5;
			}
		}
		string text6 = FindDriverInGameLog(players);
		if (!string.IsNullOrWhiteSpace(text6))
		{
			ClientStore.Log("motorista identificado pelo game.log: " + text6);
			return text6;
		}
		if (players.Count != 1)
		{
			return string.Empty;
		}
		return players[0];
	}

	private static string MatchPlayer(string hint, List<string> players)
	{
		if (string.IsNullOrWhiteSpace(hint) || players == null)
		{
			return string.Empty;
		}
		string normalized = NormalizeDriverName(hint);
		if (normalized.Length == 0)
		{
			return string.Empty;
		}
		string text = players.FirstOrDefault((string x) => !string.IsNullOrWhiteSpace(x) && NormalizeDriverName(x) == normalized);
		if (!string.IsNullOrWhiteSpace(text))
		{
			return text;
		}
		string compact = CompactDriverName(hint);
		if (compact.Length >= 3)
		{
			List<string> list = players.Where((string x) => !string.IsNullOrWhiteSpace(x) && CompactDriverName(x) == compact).ToList();
			if (list.Count == 1)
			{
				return list[0];
			}
		}
		if (compact.Length >= 5)
		{
			List<string> list2 = players.Where(delegate(string x)
			{
				string text2 = CompactDriverName(x);
				if (text2.Length < 5)
				{
					return false;
				}
				if (Math.Abs(text2.Length - compact.Length) > 8)
				{
					return false;
				}
				return text2.Contains(compact) || compact.Contains(text2);
			}).ToList();
			if (list2.Count == 1)
			{
				return list2[0];
			}
		}
		return string.Empty;
	}

	private static string NormalizeDriverName(string value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return string.Empty;
		}
		string obj = value.Trim().Normalize(NormalizationForm.FormD);
		StringBuilder stringBuilder = new StringBuilder();
		bool flag = false;
		string text = obj;
		foreach (char c in text)
		{
			if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
			{
				if (char.IsLetterOrDigit(c))
				{
					stringBuilder.Append(char.ToLowerInvariant(c));
					flag = false;
				}
				else if (char.IsWhiteSpace(c) && !flag && stringBuilder.Length > 0)
				{
					stringBuilder.Append(' ');
					flag = true;
				}
			}
		}
		return stringBuilder.ToString().Trim();
	}

	private static string CompactDriverName(string value)
	{
		return NormalizeDriverName(value).Replace(" ", string.Empty);
	}

	private static string FindTelemetryDriverHint(JToken token, List<string> players, int depth)
	{
		if (token == null || depth > 6)
		{
			return string.Empty;
		}
		if (token is JObject jObject)
		{
			foreach (JProperty item in jObject.Properties())
			{
				switch (item.Name.Replace("_", string.Empty).Replace("-", string.Empty).ToLowerInvariant())
				{
				case "playername":
				case "profilename":
				case "steamname":
				case "username":
				case "multiplayername":
				case "drivername":
				{
					string text = MatchPlayer((item.Value.Type == JTokenType.String) ? item.Value.ToString() : string.Empty, players);
					if (!string.IsNullOrWhiteSpace(text))
					{
						return text;
					}
					break;
				}
				}
				string text2 = FindTelemetryDriverHint(item.Value, players, depth + 1);
				if (!string.IsNullOrWhiteSpace(text2))
				{
					return text2;
				}
			}
			return string.Empty;
		}
		if (token is JArray jArray)
		{
			foreach (JToken item2 in jArray)
			{
				string text3 = FindTelemetryDriverHint(item2, players, depth + 1);
				if (!string.IsNullOrWhiteSpace(text3))
				{
					return text3;
				}
			}
		}
		return string.Empty;
	}

	private static string FindDriverInGameLog(List<string> players)
	{
		try
		{
			List<string> list = new List<string>();
			string folderPath = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
			if (!string.IsNullOrWhiteSpace(folderPath))
			{
				list.Add(Path.Combine(folderPath, "Euro Truck Simulator 2", "game.log.txt"));
			}
			string folderPath2 = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
			if (!string.IsNullOrWhiteSpace(folderPath2))
			{
				list.Add(Path.Combine(folderPath2, "Documents", "Euro Truck Simulator 2", "game.log.txt"));
				list.Add(Path.Combine(folderPath2, "OneDrive", "Documents", "Euro Truck Simulator 2", "game.log.txt"));
			}
			foreach (string item in list.Distinct(StringComparer.OrdinalIgnoreCase))
			{
				if (!File.Exists(item))
				{
					continue;
				}
				string[] array;
				try
				{
					array = File.ReadAllLines(item);
				}
				catch
				{
					continue;
				}
				int num = Math.Max(0, array.Length - 3500);
				for (int num2 = array.Length - 1; num2 >= num; num2--)
				{
					string text = array[num2] ?? string.Empty;
					string text2 = text.ToLowerInvariant();
					if (text2.Contains("player") || text2.Contains("profile") || text2.Contains("steam") || text2.Contains("multiplayer") || text2.Contains("convoy") || text2.Contains("connected"))
					{
						string text3 = FindUniquePlayerMention(text, players);
						if (!string.IsNullOrWhiteSpace(text3))
						{
							return text3;
						}
					}
				}
			}
		}
		catch
		{
		}
		return string.Empty;
	}

	private static string FindUniquePlayerMention(string text, List<string> players)
	{
		if (string.IsNullOrWhiteSpace(text) || players == null)
		{
			return string.Empty;
		}
		string text2 = NormalizeDriverName(text);
		if (text2.Length == 0)
		{
			return string.Empty;
		}
		List<string> list = new List<string>();
		foreach (string player in players)
		{
			string text3 = NormalizeDriverName(player);
			if (text3.Length >= 3 && text2.Contains(text3))
			{
				list.Add(player);
			}
		}
		if (list.Distinct(StringComparer.OrdinalIgnoreCase).Count() != 1)
		{
			return string.Empty;
		}
		return list.First();
	}

	private static SteamIdentity GetLocalSteamIdentity()
	{
		try
		{
			string text = string.Empty;
			uint num = 0u;
			try
			{
				using (RegistryKey registryKey = Registry.CurrentUser.OpenSubKey("Software\\Valve\\Steam"))
				{
					text = Convert.ToString(registryKey?.GetValue("SteamPath")) ?? string.Empty;
				}
				using RegistryKey registryKey2 = Registry.CurrentUser.OpenSubKey("Software\\Valve\\Steam\\ActiveProcess");
				object obj = registryKey2?.GetValue("ActiveUser");
				if (obj != null)
				{
					num = Convert.ToUInt32(obj);
				}
			}
			catch
			{
			}
			if (string.IsNullOrWhiteSpace(text))
			{
				try
				{
					Process[] processesByName = Process.GetProcessesByName("steam");
					foreach (Process process in processesByName)
					{
						try
						{
							string text2 = process.MainModule?.FileName;
							if (!string.IsNullOrWhiteSpace(text2))
							{
								text = Path.GetDirectoryName(text2) ?? string.Empty;
								break;
							}
						}
						catch
						{
						}
						finally
						{
							process.Dispose();
						}
					}
				}
				catch
				{
				}
			}
			if (string.IsNullOrWhiteSpace(text))
			{
				return null;
			}
			text = text.Replace('/', Path.DirectorySeparatorChar);
			string path = Path.Combine(text, "config", "loginusers.vdf");
			if (!File.Exists(path))
			{
				return null;
			}
			MatchCollection matchCollection = Regex.Matches(File.ReadAllText(path), "\\\"(?<id>7656119[0-9]+)\\\"\\s*\\{(?<body>.*?)\\n\\s*\\}", RegexOptions.Singleline);
			SteamIdentity result = null;
			foreach (Match item in matchCollection)
			{
				if (!ulong.TryParse(item.Groups["id"].Value, out var result2))
				{
					continue;
				}
				string value = item.Groups["body"].Value;
				Match match2 = Regex.Match(value, "\\\"PersonaName\\\"\\s+\\\"(?<v>(?:\\\\.|[^\\\"])*)\\\"");
				if (!match2.Success)
				{
					continue;
				}
				string text3 = match2.Groups["v"].Value.Replace("\\\\\\\"", "\\\"").Replace("\\\\\\\\", "\\\\").Trim();
				if (!string.IsNullOrWhiteSpace(text3))
				{
					SteamIdentity steamIdentity = new SteamIdentity
					{
						SteamId = result2.ToString(),
						PersonaName = text3
					};
					if (num != 0 && result2 >= 76561197960265728L && result2 - 76561197960265728L == num)
					{
						return steamIdentity;
					}
					if (Regex.Match(value, "\\\"MostRecent\\\"\\s+\\\"1\\\"").Success)
					{
						result = steamIdentity;
					}
				}
			}
			return result;
		}
		catch
		{
			return null;
		}
	}

	private async Task<bool> LoginAsync(string driver, bool forceNewToken = false)
	{
		CredentialEntry credential = ClientStore.FindCredential(_endpoint, driver);
		string tok = (forceNewToken ? string.Empty : ClientStore.GetPlainToken(credential));
		ApiResponse apiResponse = await _api.LoginAsync(_endpoint, driver, _deviceId, tok, _accountUser, _accountToken);
		if (!IsAccepted(apiResponse) && !forceNewToken && !string.IsNullOrWhiteSpace(tok))
		{
			apiResponse = await _api.LoginAsync(_endpoint, driver, _deviceId, string.Empty, _accountUser, _accountToken);
		}
		if (!IsAccepted(apiResponse))
		{
			lblSession.Text = "GAT LOG: login recusado";
			ClientStore.Log("login recusado " + apiResponse.StatusCode + " " + apiResponse.Text);
			return false;
		}
		if (!string.Equals(ApiClient.Str(apiResponse.Json?["account_user"]), _accountUser, StringComparison.OrdinalIgnoreCase))
		{
			lblSession.Text = "GAT LOG: servidor precisa da versão 1.0.12";
			lblTelemetry.Text = "Envio: conta não vinculada";
			return false;
		}
		string text = ApiClient.Str(apiResponse.Json?["driver"]);
		if (string.IsNullOrWhiteSpace(text))
		{
			text = driver;
		}
		string text2 = ApiClient.Str(apiResponse.Json?["token"]);
		if (string.IsNullOrWhiteSpace(text2))
		{
			text2 = tok;
		}
		_driver = text;
		_token = text2;
		_loggedIn = true;
		_waiting = true;
		_settings.LastDriver = text;
		ClientStore.SaveSettings(_settings);
		if (!string.IsNullOrWhiteSpace(text2))
		{
			ClientStore.SaveCredential(_endpoint, text, text2);
		}
		lblDriver.Text = "Motorista: " + text;
		lblSession.Text = "GAT LOG: CONECTADO";
		lblTelemetry.Text = "Envio: iniciando telemetria";
		ClientStore.Log("login ok: " + text);
		return true;
	}

	private static bool IsAccepted(ApiResponse r)
	{
		if (r == null || r.StatusCode != 200 || r.Json == null)
		{
			return false;
		}
		JToken jToken = r.Json["ok"];
		if (jToken != null)
		{
			return ApiClient.Bool(jToken);
		}
		return true;
	}

	private static bool NeedsTokenRenewal(ApiResponse r)
	{
		if (r == null)
		{
			return false;
		}
		if (r.StatusCode == 401)
		{
			return true;
		}
		string a = ApiClient.Str(r.Json?["error"]);
		if (!string.Equals(a, "token_required", StringComparison.OrdinalIgnoreCase))
		{
			return string.Equals(a, "invalid_token", StringComparison.OrdinalIgnoreCase);
		}
		return true;
	}

	private void UpdateTelemetryDisplay(TelemetryDisplay d)
	{
		lblCargo.Text = "Carga: " + d.Cargo;
		lblRoute.Text = "Rota: " + d.Route;
		lblDistance.Text = "Restante: " + d.Distance;
		lblSpeed.Text = "Velocidade: " + d.Speed;
		lblWeight.Text = "Peso: " + d.Weight;
		SetDamageValue(lblDamageCargo, d.CargoDamage);
		SetDamageValue(lblDamageEngine, d.EngineDamage);
		SetDamageValue(lblDamageTransmission, d.TransmissionDamage);
		SetDamageValue(lblDamageCabin, d.CabinDamage);
		SetDamageValue(lblDamageChassis, d.ChassisDamage);
		SetDamageValue(lblDamageWheels, d.WheelsDamage);
		SetDamageValue(lblDamageTrailer, d.TrailerDamage);
		if (lblDamage != null)
		{
			lblDamage.Text = "Danos: Carga " + d.CargoDamage + " | Motor " + d.EngineDamage + " | Câmbio " + d.TransmissionDamage + " | Cabine " + d.CabinDamage + " | Chassi " + d.ChassisDamage + " | Rodas " + d.WheelsDamage + " | Reboque " + d.TrailerDamage;
		}
	}

	private static void SetDamageValue(Label label, string value)
	{
		if (label == null)
		{
			return;
		}
		string text = string.IsNullOrWhiteSpace(value) ? "—" : value.Trim();
		label.Text = text;
		double pct;
		if (double.TryParse(text.TrimEnd('%'), NumberStyles.Any, CultureInfo.InvariantCulture, out pct))
		{
			if (pct <= 5.0)
			{
				label.ForeColor = Color.FromArgb(130, 224, 69);
			}
			else if (pct <= 20.0)
			{
				label.ForeColor = Color.Gold;
			}
			else
			{
				label.ForeColor = Color.FromArgb(255, 105, 97);
			}
		}
		else
		{
			label.ForeColor = Color.FromArgb(145, 158, 176);
		}
	}

	private async Task RefreshServerInfoAsync(bool force)
	{
		if (!(cmbServers.SelectedItem is ServerEntry selected))
		{
			lblServer.Text = "Servidor: nenhum cadastrado";
			lblRoom.Text = "Sala: -";
		}
		else if (force || !((DateTime.UtcNow - _lastServerProbe).TotalSeconds < 3.0))
		{
			string endpoint = ClientStore.NormalizeEndpoint(selected.Endpoint);
			_serverInfo = await _api.GetServerInfoAsync(endpoint);
			_lastServerProbe = DateTime.UtcNow;
			if (!_serverInfo.Reachable)
			{
				lblServer.Text = "Servidor: OFFLINE";
				lblRoom.Text = "Sala: -";
				return;
			}
			if (!_serverInfo.Supported)
			{
				lblServer.Text = "Servidor: acessível, API antiga";
				lblRoom.Text = "Sala: -";
				return;
			}
			string text = (string.IsNullOrWhiteSpace(_serverInfo.ServerName) ? selected.Name : _serverInfo.ServerName);
			lblServer.Text = "Servidor: " + (string.IsNullOrWhiteSpace(text) ? "ONLINE" : text) + " • " + _serverInfo.Players + "/" + _serverInfo.MaxPlayers;
			lblRoom.Text = "Sala: " + (string.IsNullOrWhiteSpace(_serverInfo.SessionId) ? "-" : _serverInfo.SessionId);
		}
	}

	private void CopyRoomClicked(object sender, EventArgs e)
	{
		if (!string.IsNullOrWhiteSpace(_serverInfo.SessionId))
		{
			Clipboard.SetText(_serverInfo.SessionId);
		}
	}

	private void AddServerClicked(object sender, EventArgs e)
	{
		using AddServerForm addServerForm = new AddServerForm();
		if (addServerForm.ShowDialog(this) != DialogResult.OK)
		{
			return;
		}
		string endpoint = ClientStore.NormalizeEndpoint(addServerForm.Endpoint);
		if (!string.IsNullOrWhiteSpace(endpoint))
		{
			string name = (string.IsNullOrWhiteSpace(addServerForm.ServerName) ? endpoint : addServerForm.ServerName.Trim());
			if (_servers.Any((ServerEntry x) => string.Equals(ClientStore.NormalizeEndpoint(x.Endpoint), endpoint, StringComparison.OrdinalIgnoreCase)))
			{
				MessageBox.Show("Esse servidor já está cadastrado.", "GAT Telemetria", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
				return;
			}
			_servers.Add(new ServerEntry
			{
				Name = name,
				Endpoint = endpoint
			});
			ClientStore.SaveServers(_servers);
			LoadServerList();
			cmbServers.SelectedIndex = _servers.Count - 1;
		}
	}

	private void RemoveServerClicked(object sender, EventArgs e)
	{
		int selectedIndex = cmbServers.SelectedIndex;
		if (selectedIndex >= 0 && selectedIndex < _servers.Count && MessageBox.Show("Remover este servidor da lista?", "GAT Telemetria", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
		{
			_servers.RemoveAt(selectedIndex);
			ClientStore.SaveServers(_servers);
			_loggedIn = false;
			_waiting = false;
			_driver = string.Empty;
			_token = string.Empty;
			LoadServerList();
			if (_servers.Count > 0)
			{
				cmbServers.SelectedIndex = Math.Min(selectedIndex, _servers.Count - 1);
			}
		}
	}

	private async Task CheckUpdateAsync(bool showNoUpdate)
	{
		try
		{
			using (HttpClient http = new HttpClient
			{
				Timeout = TimeSpan.FromSeconds(8.0)
			})
			{
				RemoteVersion remoteVersion = JsonConvert.DeserializeObject<RemoteVersion>(await http.GetStringAsync("https://raw.githubusercontent.com/bidu620-alt/GAT-LOG-Updates/main/client_dotnet_version.json"));
				if (remoteVersion != null && IsNewer(remoteVersion.Version, CurrentVersion) && !string.IsNullOrWhiteSpace(remoteVersion.EffectiveUrl))
				{
					_availableUpdate = remoteVersion;
					btnUpdate.Text = "ATUALIZAR PARA " + remoteVersion.Version;
					btnUpdate.BackColor = Color.FromArgb(32, 132, 91);
					return;
				}
			}
			_availableUpdate = null;
			btnUpdate.Text = "VERIFICAR ATUALIZAÇÃO";
			if (showNoUpdate)
			{
				MessageBox.Show("Você já está na versão mais recente.", "GAT Telemetria", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
			}
		}
		catch (Exception ex)
		{
			if (showNoUpdate)
			{
				MessageBox.Show("Não foi possível verificar atualização.\r\n\r\n" + ex.Message, "GAT Telemetria", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
			}
		}
	}

	private async Task UpdateClickedAsync()
	{
		if (_availableUpdate == null)
		{
			await CheckUpdateAsync(showNoUpdate: true);
		}
		else
		{
			if (MessageBox.Show("Instalar GAT Telemetria " + _availableUpdate.Version + "?\r\n\r\n" + (_availableUpdate.Notes ?? string.Empty), "Atualização GAT Telemetria", MessageBoxButtons.YesNo, MessageBoxIcon.Asterisk) != DialogResult.Yes)
			{
				return;
			}
			btnUpdate.Enabled = false;
			btnUpdate.Text = "BAIXANDO...";
			try
			{
				string path = Path.Combine(Path.GetTempPath(), "GAT_TELEMETRIA_DOTNET_SETUP_" + _availableUpdate.Version + ".exe");
				using (HttpClient http = new HttpClient
				{
					Timeout = TimeSpan.FromMinutes(3.0)
				})
				{
					File.WriteAllBytes(path, await http.GetByteArrayAsync(_availableUpdate.EffectiveUrl));
				}
				if (!string.IsNullOrWhiteSpace(_availableUpdate.Sha256))
				{
					string a;
					using (SHA256 sHA = SHA256.Create())
					{
						a = BitConverter.ToString(sHA.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant();
					}
					if (!string.Equals(a, _availableUpdate.Sha256.Trim(), StringComparison.OrdinalIgnoreCase))
					{
						throw new InvalidDataException("SHA256 do instalador não confere.");
					}
				}
				Process.Start(new ProcessStartInfo(path)
				{
					UseShellExecute = true
				});
				Application.Exit();
			}
			catch (Exception ex)
			{
				btnUpdate.Enabled = true;
				btnUpdate.Text = "TENTAR ATUALIZAÇÃO";
				MessageBox.Show("Falha ao atualizar:\r\n\r\n" + ex.Message, "GAT Telemetria", MessageBoxButtons.OK, MessageBoxIcon.Hand);
			}
		}
	}

	private static bool IsNewer(string remote, string local)
	{
		if (Version.TryParse(remote, out var result) && Version.TryParse(local, out var result2))
		{
			return result > result2;
		}
		return false;
	}
}



