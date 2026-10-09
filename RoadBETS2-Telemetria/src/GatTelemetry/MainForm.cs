using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Speech.Synthesis;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Xml;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
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

	private sealed class ModernCard : System.Windows.Forms.Panel
	{
		public string Caption { get; set; } = string.Empty;

		public System.Drawing.Color BorderColor { get; set; } = System.Drawing.Color.FromArgb(38, 111, 205);

		public ModernCard()
		{
			SetStyle(ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, value: true);
			BackColor = System.Drawing.Color.FromArgb(7, 20, 36);
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			base.OnPaint(e);
			e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
			using (GraphicsPath path = RoundedRect(new System.Drawing.Rectangle(1, 1, Math.Max(1, Width - 3), Math.Max(1, Height - 3)), 12))
			{
				using System.Drawing.Pen pen = new System.Drawing.Pen(BorderColor, 1f);
				e.Graphics.DrawPath(pen, path);
			}
			if (string.IsNullOrWhiteSpace(Caption))
			{
				return;
			}
			using Font font = new Font("Segoe UI Semibold", 11.5f, System.Drawing.FontStyle.Bold);
			using System.Drawing.Brush brush = new SolidBrush(System.Drawing.Color.FromArgb(232, 239, 249));
			using System.Drawing.Brush brush2 = new SolidBrush(System.Drawing.Color.FromArgb(31, 107, 220));
			e.Graphics.FillEllipse(brush2, 18, 15, 25, 25);
			e.Graphics.DrawString(Caption, font, brush, 52f, 15f);
		}

		private static GraphicsPath RoundedRect(System.Drawing.Rectangle bounds, int radius)
		{
			int num = radius * 2;
			GraphicsPath graphicsPath = new GraphicsPath();
			System.Drawing.Rectangle rect = new System.Drawing.Rectangle(bounds.X, bounds.Y, num, num);
			graphicsPath.AddArc(rect, 180f, 90f);
			rect.X = bounds.Right - num;
			graphicsPath.AddArc(rect, 270f, 90f);
			rect.Y = bounds.Bottom - num;
			graphicsPath.AddArc(rect, 0f, 90f);
			rect.X = bounds.Left;
			graphicsPath.AddArc(rect, 90f, 90f);
			graphicsPath.CloseFigure();
			return graphicsPath;
		}
	}

	private sealed class TruckOutline : System.Windows.Forms.Control
	{
		public TruckOutline()
		{
			SetStyle(ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, value: true);
			BackColor = System.Drawing.Color.Transparent;
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			base.OnPaint(e);
			e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
			using System.Drawing.Pen pen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(85, 91, 159, 255), 1.4f);
			int num = Math.Max(80, Width - 8);
			int num2 = Math.Max(40, Height - 8) / 2;
			e.Graphics.DrawRectangle(pen, 8, num2 - 14, num / 2, 24);
			e.Graphics.DrawLine(pen, num / 2 + 8, num2 - 14, num * 3 / 4, num2 - 14);
			e.Graphics.DrawLine(pen, num * 3 / 4, num2 - 14, num - 4, num2 - 2);
			e.Graphics.DrawLine(pen, num - 4, num2 - 2, num - 4, num2 + 10);
			e.Graphics.DrawLine(pen, num - 4, num2 + 10, 8, num2 + 10);
			e.Graphics.DrawRectangle(pen, num * 3 / 4, num2 - 10, Math.Max(14, num / 7), 10);
			e.Graphics.DrawEllipse(pen, 22, num2 + 4, 16, 16);
			e.Graphics.DrawEllipse(pen, num * 3 / 4, num2 + 4, 16, 16);
			e.Graphics.DrawEllipse(pen, num * 3 / 4 + 20, num2 + 4, 16, 16);
		}
	}

	private sealed class DriverAvatar042 : System.Windows.Forms.Control
	{
		public string Initials { get; set; } = "GAT";

		public DriverAvatar042()
		{
			SetStyle(ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, value: true);
			BackColor = System.Drawing.Color.Transparent;
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			base.OnPaint(e);
			e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
			int num = Math.Max(20, Math.Min(Width, Height) - 6);
			System.Drawing.Rectangle rect = new System.Drawing.Rectangle((Width - num) / 2, (Height - num) / 2, num, num);
			using (SolidBrush brush = new SolidBrush(System.Drawing.Color.FromArgb(12, 91, 164)))
			{
				e.Graphics.FillEllipse(brush, rect);
			}
			using (System.Drawing.Pen pen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(36, 167, 255), 2f))
			{
				e.Graphics.DrawEllipse(pen, rect);
			}
			string text = (string.IsNullOrWhiteSpace(Initials) ? "RDL" : Initials.Trim().ToUpperInvariant());
			if (text.Length > 2)
			{
				text = text.Substring(0, 2);
			}
			using Font font = new Font("Segoe UI Semibold", 22f, System.Drawing.FontStyle.Bold);
			using SolidBrush brush2 = new SolidBrush(System.Drawing.Color.White);
			SizeF sizeF = e.Graphics.MeasureString(text, font);
			e.Graphics.DrawString(text, font, brush2, (float)rect.Left + ((float)rect.Width - sizeF.Width) / 2f, (float)rect.Top + ((float)rect.Height - sizeF.Height) / 2f);
		}
	}

	private sealed class RoadLifeMark044 : System.Windows.Forms.Control
	{
		public RoadLifeMark044()
		{
			SetStyle(ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, value: true);
			BackColor = System.Drawing.Color.Transparent;
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			base.OnPaint(e);
			e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
			using System.Drawing.Pen pen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(54, 242, 122), 5f);
			using System.Drawing.Pen pen2 = new System.Drawing.Pen(System.Drawing.Color.FromArgb(225, 255, 238), 1.5f);
			pen.StartCap = LineCap.Round;
			pen.EndCap = LineCap.Round;
			pen2.StartCap = LineCap.Round;
			pen2.EndCap = LineCap.Round;
			GraphicsPath graphicsPath = new GraphicsPath();
			graphicsPath.AddBezier(7, Height - 8, 7, Height / 2, Width - 5, Height / 2, Width - 5, 8);
			e.Graphics.DrawPath(pen, graphicsPath);
			pen2.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;
			e.Graphics.DrawPath(pen2, graphicsPath);
			graphicsPath.Dispose();
		}
	}

	private sealed class DriverAvatar044 : System.Windows.Forms.Control
	{
		public string Initials { get; set; } = "GAT";

		public DriverAvatar044()
		{
			SetStyle(ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, value: true);
			BackColor = System.Drawing.Color.Transparent;
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			base.OnPaint(e);
			e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
			int num = Math.Max(24, Math.Min(Width, Height) - 8);
			System.Drawing.Rectangle rect = new System.Drawing.Rectangle((Width - num) / 2, (Height - num) / 2, num, num);
			using (SolidBrush brush = new SolidBrush(System.Drawing.Color.FromArgb(38, 54, 242, 122)))
			{
				e.Graphics.FillEllipse(brush, new System.Drawing.Rectangle(rect.X - 5, rect.Y - 5, rect.Width + 10, rect.Height + 10));
			}
			using (System.Drawing.Drawing2D.LinearGradientBrush brush2 = new System.Drawing.Drawing2D.LinearGradientBrush(rect, System.Drawing.Color.FromArgb(8, 48, 31), System.Drawing.Color.FromArgb(5, 18, 14), 90f))
			{
				e.Graphics.FillEllipse(brush2, rect);
			}
			using (System.Drawing.Pen pen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(54, 242, 122), 2f))
			{
				e.Graphics.DrawEllipse(pen, rect);
			}
			string text = (string.IsNullOrWhiteSpace(Initials) ? "GAT" : Initials.Trim().ToUpperInvariant());
			if (text.Length > 2)
			{
				text = text.Substring(0, 2);
			}
			using Font font = new Font("Segoe UI Semibold", 24f, System.Drawing.FontStyle.Bold);
			using SolidBrush brush3 = new SolidBrush(System.Drawing.Color.White);
			SizeF sizeF = e.Graphics.MeasureString(text, font);
			e.Graphics.DrawString(text, font, brush3, (float)rect.Left + ((float)rect.Width - sizeF.Width) / 2f, (float)rect.Top + ((float)rect.Height - sizeF.Height) / 2f);
		}
	}

	private sealed class GlassCard044 : System.Windows.Forms.Panel
	{
		public string Caption { get; set; } = "";

		public GlassCard044()
		{
			SetStyle(ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, value: true);
			BackColor = System.Drawing.Color.FromArgb(7, 27, 20);
			Padding = new Padding(18, 50, 18, 16);
			Margin = new Padding(7);
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			base.OnPaint(e);
			e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
			using (GraphicsPath path = Round044(new System.Drawing.Rectangle(1, 1, Math.Max(2, Width - 3), Math.Max(2, Height - 3)), 14))
			{
				using System.Drawing.Pen pen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(38, 120, 77), 1.2f);
				e.Graphics.DrawPath(pen, path);
			}
			using (System.Drawing.Pen pen2 = new System.Drawing.Pen(System.Drawing.Color.FromArgb(31, 76, 52), 1f))
			{
				e.Graphics.DrawLine(pen2, 18, 42, Math.Max(19, Width - 18), 42);
			}
			using Font font = new Font("Segoe UI Semibold", 10.5f, System.Drawing.FontStyle.Bold);
			using SolidBrush brush = new SolidBrush(System.Drawing.Color.FromArgb(225, 239, 255));
			e.Graphics.DrawString(Caption, font, brush, 18f, 15f);
		}
	}

	private sealed class HeroPanel044 : System.Windows.Forms.Panel
	{
		public System.Drawing.Image HeroImage { get; set; }

		public HeroPanel044()
		{
			SetStyle(ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, value: true);
			BackColor = System.Drawing.Color.FromArgb(5, 17, 12);
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			base.OnPaint(e);
			e.Graphics.SmoothingMode = SmoothingMode.HighQuality;
			e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
			System.Drawing.Rectangle clientRectangle = ClientRectangle;
			if (HeroImage != null && clientRectangle.Width > 0 && clientRectangle.Height > 0)
			{
				float num = Math.Max((float)clientRectangle.Width / (float)HeroImage.Width, (float)clientRectangle.Height / (float)HeroImage.Height);
				int num2 = (int)Math.Ceiling((float)HeroImage.Width * num);
				int num3 = (int)Math.Ceiling((float)HeroImage.Height * num);
				int num4 = (clientRectangle.Width - num2) / 2;
				int num5 = (clientRectangle.Height - num3) / 2;
				e.Graphics.DrawImage(HeroImage, new System.Drawing.Rectangle(num4, num5, num2, num3));
			}
			using (SolidBrush brush = new SolidBrush(System.Drawing.Color.FromArgb(82, 4, 67, 37)))
			{
				e.Graphics.FillRectangle(brush, clientRectangle);
			}
			using (System.Drawing.Drawing2D.LinearGradientBrush brush2 = new System.Drawing.Drawing2D.LinearGradientBrush(clientRectangle, System.Drawing.Color.FromArgb(205, 1, 11, 23), System.Drawing.Color.FromArgb(35, 1, 11, 23), 0f))
			{
				e.Graphics.FillRectangle(brush2, clientRectangle);
			}
			using (System.Drawing.Drawing2D.LinearGradientBrush brush3 = new System.Drawing.Drawing2D.LinearGradientBrush(clientRectangle, System.Drawing.Color.FromArgb(0, 1, 9, 18), System.Drawing.Color.FromArgb(185, 1, 9, 18), 90f))
			{
				e.Graphics.FillRectangle(brush3, clientRectangle);
			}
			float num6 = 30f;
			float num7 = Math.Max(25f, (float)Height * 0.18f);
			using (Font font = new Font("Segoe UI Semibold", 11f, System.Drawing.FontStyle.Bold))
			{
				using SolidBrush brush4 = new SolidBrush(System.Drawing.Color.FromArgb(215, 232, 249));
				e.Graphics.DrawString("ESTRADAS QUE NOS UNEM", font, brush4, num6 + 4f, num7);
			}
			using (Font font2 = new Font("Segoe UI", 9.5f))
			{
				using SolidBrush brush5 = new SolidBrush(System.Drawing.Color.FromArgb(197, 219, 240));
				e.Graphics.DrawString("● COMUNIDADE ATIVA", font2, brush5, num6 + 4f, num7 + 36f);
				e.Graphics.DrawString("★ VIAGENS REAIS • AMIZADES VERDADEIRAS", font2, brush5, num6 + 4f, num7 + 64f);
				e.Graphics.DrawString("▣ SEMPRE EM FRENTE", font2, brush5, num6 + 4f, num7 + 92f);
			}
			using Font font3 = new Font("Segoe UI Semibold", 10f, System.Drawing.FontStyle.Italic);
			using SolidBrush brush6 = new SolidBrush(System.Drawing.Color.FromArgb(235, 243, 252));
			e.Graphics.DrawString("“Na estrada, cada quilômetro conta uma história.”", font3, brush6, 32f, Math.Max(40, Height - 54));
		}

		private static GraphicsPath Round044(System.Drawing.Rectangle bounds, int radius)
		{
			int num = radius * 2;
			GraphicsPath graphicsPath = new GraphicsPath();
			System.Drawing.Rectangle rect = new System.Drawing.Rectangle(bounds.X, bounds.Y, num, num);
			graphicsPath.AddArc(rect, 180f, 90f);
			rect.X = bounds.Right - num;
			graphicsPath.AddArc(rect, 270f, 90f);
			rect.Y = bounds.Bottom - num;
			graphicsPath.AddArc(rect, 0f, 90f);
			rect.X = bounds.Left;
			graphicsPath.AddArc(rect, 90f, 90f);
			graphicsPath.CloseFigure();
			return graphicsPath;
		}
	}

	private sealed class GatMapDetection
	{
		public string Key = "base";

		public string Label = "Mapa Base";

		public string PackageName = string.Empty;

		public string Source = "game.log";
	}

	private sealed class GatMountedMod
	{
		public string Name = string.Empty;

		public string PackageName = string.Empty;

		public string Raw = string.Empty;
	}

	private static class RoadLifeNative
	{
		[DllImport("user32.dll")]
		internal static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

		[DllImport("user32.dll")]
		internal static extern bool UnregisterHotKey(IntPtr hWnd, int id);

		[DllImport("user32.dll")]
		internal static extern int GetWindowLong(IntPtr hWnd, int nIndex);

		[DllImport("user32.dll")]
		internal static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
	}

	private const string CurrentVersion = "1.0.68.21";

	private const string VersionUrl = "https://raw.githubusercontent.com/bidu620-alt/GAT-LOG-Updates/main/client_dotnet_version.json";

	private const string AccountAuthority = "https://api.gatlogets2.com.br";

	private readonly ApiClient _api = new ApiClient();

	private readonly TelemetryEngine _telemetry = new TelemetryEngine();

	private readonly TripJournal _tripJournal = new TripJournal();

	private readonly System.Windows.Forms.Timer _timer = new System.Windows.Forms.Timer
	{
		Interval = 1000
	};

	private readonly System.Windows.Forms.Timer _updateTimer = new System.Windows.Forms.Timer
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

	private DateTime _lastTruckSimEnsure = DateTime.MinValue;

	private DateTime _lastTripCapture = DateTime.MinValue;

	private DateTime _lastTripFlush = DateTime.MinValue;

	private const int MaxQueuedTelemetryPackets = 72000;

	private const int MaxBlackBoxPackets = 100000;

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

	private System.Windows.Forms.ComboBox cmbServers;

	private System.Windows.Forms.ComboBox cmbMapMode;

	private System.Windows.Forms.TextBox txtAccountUser;

	private System.Windows.Forms.TextBox txtAccountPassword;

	private System.Windows.Forms.Button btnAccountLogin;

	private System.Windows.Forms.Label lblAccount;

	private System.Windows.Forms.Button btnRemove;

	private System.Windows.Forms.Button btnEnter;

	private System.Windows.Forms.Button btnUpdate;

	private System.Windows.Forms.CheckBox chkAuto;

	private System.Windows.Forms.Label lblServer;

	private System.Windows.Forms.Label lblRoom;

	private System.Windows.Forms.Label lblDriver;

	private System.Windows.Forms.Label lblSession;

	private System.Windows.Forms.Label lblTruck;

	private System.Windows.Forms.Label lblTelemetry;

	private System.Windows.Forms.Label lblCargo;

	private System.Windows.Forms.Label lblRoute;

	private System.Windows.Forms.Label lblDistance;

	private System.Windows.Forms.Label lblSpeed;

	private System.Windows.Forms.Label lblWeight;

	private System.Windows.Forms.Label lblDamage;

	private System.Windows.Forms.Label lblDamageCargo;

	private System.Windows.Forms.Label lblDamageEngine;

	private System.Windows.Forms.Label lblDamageTransmission;

	private System.Windows.Forms.Label lblDamageCabin;

	private System.Windows.Forms.Label lblDamageChassis;

	private System.Windows.Forms.Label lblDamageWheels;

	private System.Windows.Forms.Label lblDamageTrailer;

	private System.Windows.Forms.Label lblPcRegister;

	private System.Windows.Forms.Label lblPcRegisterDetail;

	private System.Windows.Forms.Label lblWorkStatus;

	private System.Windows.Forms.Label lblVersion;

	private readonly Dictionary<string, System.Windows.Forms.Panel> _hubPages041 = new Dictionary<string, System.Windows.Forms.Panel>();

	private readonly Dictionary<string, System.Windows.Forms.Button> _hubNav041 = new Dictionary<string, System.Windows.Forms.Button>();

	private readonly HttpClient _hubHttp041 = new HttpClient
	{
		Timeout = TimeSpan.FromSeconds(4.0)
	};

	private readonly System.Windows.Forms.Timer _hubStatusTimer041 = new System.Windows.Forms.Timer
	{
		Interval = 700
	};

	private readonly System.Windows.Forms.Timer _hubDashTimer041 = new System.Windows.Forms.Timer
	{
		Interval = 450
	};

	private System.Windows.Forms.Panel _hubBody041;

	private System.Windows.Forms.Panel _hubRadioHost041;

	private RadioForm _hubRadio041;

	private WebView2 _hubDash041;

	private ModernCard _hubAccountCard041;

	private ModernCard _hubServerCard041;

	private System.Windows.Forms.Label _hAccount041;

	private System.Windows.Forms.Label _hEts041;

	private System.Windows.Forms.Label _hCentral041;

	private System.Windows.Forms.Label _hServer041;

	private bool _hubDashReady041;

	private bool _hubDashBusy041;

	private bool _hubApplied041;

	private TruckOverlay041 _truckOverlay041;

	private VideoOverlay041 _videoOverlay041;

	private DashOverlay060 _dashOverlay060;

	private TrackBar _overlayOpacity041;

	private System.Windows.Forms.Label _profileName042;

	private System.Windows.Forms.Label _profileMeta042;

	private System.Windows.Forms.Label _trip042;

	private System.Windows.Forms.Label _radio042;

	private System.Windows.Forms.Label _connect042;

	private DriverAvatar042 _avatar042;

	private string _hubCurrentPage042 = "home";

	private System.Drawing.Size _lastDashSize042 = System.Drawing.Size.Empty;

	private bool _hub042Applied;

	private System.Windows.Forms.Label _homeProfileName044;

	private System.Windows.Forms.Label _homeProfileMeta044;

	private System.Windows.Forms.Label _homeTrip044;

	private System.Windows.Forms.Label _homeRadio044;

	private System.Windows.Forms.Label _homeSystem044;

	private DriverAvatar044 _homeAvatar044;

	private System.Drawing.Image _homeHeroImage044;

	private bool _hub044Applied;

	private System.Windows.Forms.Label _homeProfileName045;

	private System.Windows.Forms.Label _homeProfileMeta045;

	private System.Windows.Forms.Label _homeTrip045;

	private System.Windows.Forms.Label _homeSystem045;

	private System.Windows.Forms.Label _homeDriversCount045;

	private DriverAvatar044 _homeAvatar045;

	private DataGridView _homeDrivers045;

	private DateTime _homeDriversLast045 = DateTime.MinValue;

	private bool _homeDriversBusy045;

	private bool _hub045Applied;

	private DateTime _gatMapLastScanUtc = DateTime.MinValue;

	private string _gatMapLastLogPath = string.Empty;

	private long _gatMapLastLogLength = -1L;

	private DateTime _gatMapLastLogWriteUtc = DateTime.MinValue;

	private GatMapDetection _gatMapCached = new GatMapDetection();

	private Window _roadLifeOverlay;

	private DispatcherTimer _roadLifeClock;

	private IntPtr _roadLifeHwnd = IntPtr.Zero;

	private HwndSource _roadLifeSource;

	private HwndSourceHook _roadLifeHook;

	private bool _roadLifeClickThrough;

	private bool _roadLifeFastPollBusy;

	private Dictionary<string, string> _roadLifeCargoAliases;

	private bool _roadLifeCargoAliasesLoaded;

	private static readonly int[] VoiceCleanLimits = new int[12]
	{
		20, 30, 40, 50, 60, 70, 80, 90, 100, 110,
		120, 130
	};

	private HttpClient _voiceCleanHttp;

	private System.Windows.Forms.Timer _voiceCleanTimer;

	private bool _voiceCleanBusy;

	private bool _voiceCleanInitialized;

	private bool _voiceCleanMuted;

	private int _voiceCleanVolume = 80;

	private int _voiceCleanSerial;

	private string _voiceCleanAlias = string.Empty;

	private bool _voiceCleanOver;

	private int _voiceCleanLastEventLimit;

	private double _voiceCleanLastRealLimit = double.NaN;

	private DateTime _voiceCleanLastAlert = DateTime.MinValue;

	private DateTime _voiceCleanLastError = DateTime.MinValue;

	private bool _voiceCleanConnectedLogged;

	private SpeechSynthesizer _voiceCleanSpeech;

	private TrackBar _voiceCleanVolumeBar;

	private System.Windows.Forms.Label _voiceCleanVolumeLabel;

	private System.Windows.Forms.Label _voiceCleanStatusLabel;

	private string LegacyCentralTelemetryQueueFile => System.IO.Path.Combine(ClientStore.DataDir, "central-telemetry-queue.ndjson");

	private string CentralTelemetryQueueFile => System.IO.Path.Combine(ClientStore.DataDir, "central-telemetry-queue.sec");

	private string CentralTripBlackBoxFile => System.IO.Path.Combine(ClientStore.DataDir, "central-trip-blackbox.sec");

	private string CentralTelemetryKeyFile => System.IO.Path.Combine(ClientStore.DataDir, "central-telemetry-key.dpapi");

	private string CentralJournalStateFile => System.IO.Path.Combine(ClientStore.DataDir, "central-telemetry-chain.json");

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

	private string MapModeFile => System.IO.Path.Combine(ClientStore.DataDir, "map_mode.txt");

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
			string.Equals(a, "Outro mapa", StringComparison.OrdinalIgnoreCase);
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
		Text = "ROADBETS2 TELEMETRIA BETA";
		StartPosition = FormStartPosition.CenterScreen;
		MinimumSize = new System.Drawing.Size(900, 700);
		Size = new System.Drawing.Size(940, 740);
		BackColor = System.Drawing.Color.FromArgb(4, 13, 25);
		ForeColor = System.Drawing.Color.WhiteSmoke;
		Font = new Font("Segoe UI", 9f);
		Icon = Icon.ExtractAssociatedIcon(System.Windows.Forms.Application.ExecutablePath);
		ClientStore.Ensure();
		_servers = ClientStore.LoadServers();
		_settings = ClientStore.LoadSettings();
		_deviceId = ClientStore.GetDeviceId();
		BuildUi();
		ApplyHub041();
		try
		{
			ApplyHub042();
			ApplyHub044();
			ApplyHub045();
		}
		catch (Exception ex)
		{
			try
			{
				string text = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GAT-LOG", "GAT-Telemetria");
				Directory.CreateDirectory(text);
				File.AppendAllText(System.IO.Path.Combine(text, "startup-error.log"), DateTime.Now.ToString("s") + " HUB 1.0.68.21: " + ex?.ToString() + Environment.NewLine);
			}
			catch
			{
			}
		}
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
		Shown += async delegate
		{
			EnsureTruckSimGpsRunning(force: true);
			await RestoreAccountAsync();
			await ValidatePcRegistrationAsync();
			await RefreshServerInfoAsync(force: true);
			await CheckUpdateAsync(showNoUpdate: false);
			if (_settings.AutoConnect && AccountReady && cmbServers.SelectedItem is ServerEntry)
			{
				BeginWaiting(manual: false);
			}
		};
		FormClosed += delegate
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

	private bool EnsureTruckSimGpsRunning(bool force = false)
	{
		try
		{
			if (!force && DateTime.UtcNow - _lastTruckSimEnsure < TimeSpan.FromSeconds(5.0))
			{
				return Process.GetProcessesByName("TruckSimGPS_Server").Any();
			}
			_lastTruckSimEnsure = DateTime.UtcNow;
			if (Process.GetProcessesByName("TruckSimGPS_Server").Any())
			{
				return true;
			}
			string text = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TruckSimGPS");
			string text2 = System.IO.Path.Combine(text, "TruckSimGPS_Server.exe");
			if (!File.Exists(text2))
			{
				return false;
			}
			Process.Start(new ProcessStartInfo
			{
				FileName = text2,
				Arguments = "-minimized",
				WorkingDirectory = text,
				UseShellExecute = true,
				WindowStyle = ProcessWindowStyle.Minimized
			});
			return true;
		}
		catch
		{
			return false;
		}
	}

	private void BuildUi()
	{
		SuspendLayout();
		System.Drawing.Color foreColor = System.Drawing.Color.FromArgb(67, 139, 255);
		System.Drawing.Color foreColor2 = System.Drawing.Color.FromArgb(130, 224, 69);
		System.Drawing.Color foreColor3 = System.Drawing.Color.FromArgb(168, 181, 199);
		System.Drawing.Color backColor = System.Drawing.Color.FromArgb(7, 18, 31);
		System.Windows.Forms.Label value = new System.Windows.Forms.Label
		{
			Text = "GAT",
			Font = new Font("Segoe UI Black", 27f, System.Drawing.FontStyle.Bold | System.Drawing.FontStyle.Italic),
			AutoSize = true,
			ForeColor = System.Drawing.Color.White,
			Location = new System.Drawing.Point(28, 18)
		};
		Controls.Add(value);
		System.Windows.Forms.Label value2 = new System.Windows.Forms.Label
		{
			Text = "GAT TELEMETRIA BETA",
			Font = new Font("Segoe UI Semibold", 19f, System.Drawing.FontStyle.Bold),
			AutoSize = true,
			ForeColor = System.Drawing.Color.White,
			Location = new System.Drawing.Point(128, 20)
		};
		Controls.Add(value2);
		System.Windows.Forms.Label value3 = new System.Windows.Forms.Label
		{
			Text = "Cliente ETS2  •  conexão automática",
			Font = new Font("Segoe UI", 10f),
			AutoSize = true,
			ForeColor = foreColor3,
			Location = new System.Drawing.Point(131, 55)
		};
		Controls.Add(value3);
		TruckOutline value4 = new TruckOutline
		{
			Width = 145,
			Height = 62,
			Left = ClientSize.Width - 170,
			Top = 10,
			Anchor = (AnchorStyles.Top | AnchorStyles.Right)
		};
		Controls.Add(value4);
		ModernCard modernCard = NewCard("CONTA ROADBETS2", 24, 88, ClientSize.Width - 48, 172);
		modernCard.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
		Controls.Add(modernCard);
		modernCard.Controls.Add(NewCaption("Usuário", 28, 48, 205));
		modernCard.Controls.Add(NewCaption("Senha", 264, 48, 205));
		txtAccountUser = NewInput(28, 68, 215, password: false);
		txtAccountPassword = NewInput(264, 68, 215, password: true);
		btnAccountLogin = MakeButton("ENTRAR NA CONTA", 496, 66, 160, 34, async delegate
		{
			await AccountLoginClickedAsync();
		});
		modernCard.Controls.Add(txtAccountUser);
		modernCard.Controls.Add(txtAccountPassword);
		modernCard.Controls.Add(btnAccountLogin);
		lblAccount = MakeValue("Conta: não conectada", 28, 111, 610);
		lblAccount.Font = new Font("Segoe UI Semibold", 9.5f, System.Drawing.FontStyle.Bold);
		lblAccount.ForeColor = foreColor2;
		modernCard.Controls.Add(lblAccount);
		lblDriver = MakeValue("Motorista: -", 28, 139, 610);
		lblDriver.Font = new Font("Segoe UI Semibold", 9.5f, System.Drawing.FontStyle.Bold);
		lblDriver.ForeColor = foreColor;
		modernCard.Controls.Add(lblDriver);
		System.Windows.Forms.Panel value5 = new System.Windows.Forms.Panel
		{
			Left = modernCard.Width - 216,
			Top = 42,
			Width = 1,
			Height = 110,
			BackColor = System.Drawing.Color.FromArgb(61, 77, 98),
			Anchor = (AnchorStyles.Top | AnchorStyles.Right)
		};
		modernCard.Controls.Add(value5);
		lblPcRegister = new System.Windows.Forms.Label
		{
			Text = "Registro do PC",
			Left = modernCard.Width - 202,
			Top = 56,
			Width = 180,
			Height = 24,
			Anchor = (AnchorStyles.Top | AnchorStyles.Right),
			ForeColor = foreColor3,
			Font = new Font("Segoe UI Semibold", 10f, System.Drawing.FontStyle.Bold)
		};
		modernCard.Controls.Add(lblPcRegister);
		System.Windows.Forms.Label value6 = new System.Windows.Forms.Label
		{
			Text = "Primeira vez: confirmar dispositivo",
			Left = modernCard.Width - 202,
			Top = 84,
			Width = 180,
			Height = 22,
			Anchor = (AnchorStyles.Top | AnchorStyles.Right),
			ForeColor = System.Drawing.Color.Gainsboro
		};
		modernCard.Controls.Add(value6);
		lblPcRegisterDetail = new System.Windows.Forms.Label
		{
			Text = "Entre na conta para validar este PC.",
			Left = modernCard.Width - 202,
			Top = 113,
			Width = 180,
			Height = 44,
			Anchor = (AnchorStyles.Top | AnchorStyles.Right),
			ForeColor = foreColor3
		};
		modernCard.Controls.Add(lblPcRegisterDetail);
		cmbMapMode = new System.Windows.Forms.ComboBox
		{
			DropDownStyle = ComboBoxStyle.DropDownList,
			Left = 0,
			Top = 0,
			Width = 1,
			Visible = false
		};
		cmbMapMode.Items.AddRange(new object[6] { "Mapa Base", "ProMods", "RBR", "Rotas Brasil", "EAA", "Outro mapa" });
		cmbMapMode.SelectedIndexChanged += MapModeChanged;
		modernCard.Controls.Add(cmbMapMode);
		ModernCard modernCard2 = NewCard("COMBOIO / SERVIDOR  (OPCIONAL)", 24, 272, ClientSize.Width - 48, 132);
		modernCard2.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
		Controls.Add(modernCard2);
		cmbServers = new System.Windows.Forms.ComboBox
		{
			DropDownStyle = ComboBoxStyle.DropDownList,
			Left = 28,
			Top = 52,
			Width = 385,
			Height = 30,
			FlatStyle = FlatStyle.Flat,
			BackColor = backColor,
			ForeColor = System.Drawing.Color.White
		};
		cmbServers.SelectedIndexChanged += async delegate
		{
			await SelectedServerChangedAsync();
		};
		modernCard2.Controls.Add(cmbServers);
		modernCard2.Controls.Add(MakeButton("ADICIONAR", 426, 49, 105, 34, AddServerClicked));
		btnRemove = MakeButton("REMOVER", 540, 49, 100, 34, RemoveServerClicked);
		modernCard2.Controls.Add(btnRemove);
		modernCard2.Controls.Add(MakeButton("ATUALIZAR", 649, 49, 108, 34, async delegate
		{
			await RefreshServerInfoAsync(force: true);
		}));
		modernCard2.Controls.Add(MakeButton("COPIAR ID", modernCard2.Width - 132, 49, 106, 34, CopyRoomClicked));
		modernCard2.Controls[modernCard2.Controls.Count - 1].Anchor = AnchorStyles.Top | AnchorStyles.Right;
		lblServer = MakeValue("Servidor: aguardando", 28, 92, 335);
		lblServer.ForeColor = System.Drawing.Color.Gainsboro;
		lblRoom = MakeValue("Sala: -", 365, 92, 420);
		lblRoom.ForeColor = foreColor;
		modernCard2.Controls.Add(lblServer);
		modernCard2.Controls.Add(lblRoom);
		chkAuto = new System.Windows.Forms.CheckBox
		{
			Checked = true,
			Visible = false
		};
		chkAuto.CheckedChanged += AutoChanged;
		Controls.Add(chkAuto);
		btnEnter = MakeButton("ENTRAR / AGUARDAR", 0, 0, 1, 1, EnterClicked);
		btnEnter.Visible = false;
		Controls.Add(btnEnter);
		lblSession = MakeValue("ROADBETS2: parado", 0, 0, 1);
		lblSession.Visible = false;
		Controls.Add(lblSession);
		ModernCard modernCard3 = NewCard("TELEMETRIA", 24, 416, ClientSize.Width - 48, 154);
		modernCard3.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
		Controls.Add(modernCard3);
		System.Windows.Forms.Panel value7 = NewSeparator(304, 48, 84);
		System.Windows.Forms.Panel value8 = NewSeparator(604, 48, 84);
		modernCard3.Controls.Add(value7);
		modernCard3.Controls.Add(value8);
		lblTruck = MakeValue("TruckSim GPS: aguardando", 28, 50, 260);
		lblCargo = MakeValue("Carga: Sem carga", 28, 81, 260);
		lblDistance = MakeValue("Restante: -", 28, 112, 260);
		lblTelemetry = MakeValue("Central RoadBETS2: aguardando", 330, 50, 260);
		lblRoute = MakeValue("Rota: -", 330, 81, 260);
		lblSpeed = MakeValue("Velocidade: 0 km/h", 330, 112, 260);
		lblWeight = MakeValue("Peso: -", 630, 50, 225);
		System.Windows.Forms.Label value9 = MakeValue("Tempo estimado: -", 630, 81, 225);
		lblTruck.ForeColor = System.Drawing.Color.Gainsboro;
		lblTelemetry.ForeColor = System.Drawing.Color.Gainsboro;
		modernCard3.Controls.Add(lblTruck);
		modernCard3.Controls.Add(lblCargo);
		modernCard3.Controls.Add(lblDistance);
		modernCard3.Controls.Add(lblTelemetry);
		modernCard3.Controls.Add(lblRoute);
		modernCard3.Controls.Add(lblSpeed);
		modernCard3.Controls.Add(lblWeight);
		modernCard3.Controls.Add(value9);
		lblWorkStatus = new System.Windows.Forms.Label
		{
			Text = string.Empty,
			Left = 610,
			Top = 111,
			Width = 245,
			Height = 24,
			ForeColor = foreColor2,
			Font = new Font("Segoe UI Semibold", 9.5f, System.Drawing.FontStyle.Bold),
			TextAlign = ContentAlignment.MiddleLeft,
			Visible = false
		};
		modernCard3.Controls.Add(lblWorkStatus);
		ModernCard modernCard4 = NewCard(string.Empty, 24, 582, ClientSize.Width - 48, 58);
		modernCard4.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
		Controls.Add(modernCard4);
		int num = 120;
		lblDamageCargo = AddDamageItem(modernCard4, "Carga", 20, num);
		lblDamageEngine = AddDamageItem(modernCard4, "Motor", 140, num);
		lblDamageTransmission = AddDamageItem(modernCard4, "Câmbio", 260, num);
		lblDamageCabin = AddDamageItem(modernCard4, "Cabine", 380, num);
		lblDamageChassis = AddDamageItem(modernCard4, "Chassi", 500, num);
		lblDamageWheels = AddDamageItem(modernCard4, "Rodas", 620, num);
		lblDamageTrailer = AddDamageItem(modernCard4, "Reboque", 740, num);
		lblDamage = new System.Windows.Forms.Label
		{
			Visible = false
		};
		modernCard4.Controls.Add(lblDamage);
		RadioForm radioForm = null;
		DashMediaBridge.Start();
		System.Windows.Forms.Application.ApplicationExit += delegate
		{
			DashMediaBridge.Stop();
		};
		System.Windows.Forms.Button button = MakeButton("RÁDIO ROADBETS2", 278, 654, 190, 36, delegate
		{
			if (radioForm == null || radioForm.IsDisposed)
			{
				radioForm = new RadioForm();
			}
			if (!radioForm.Visible)
			{
				radioForm.Show();
			}
			radioForm.WindowState = FormWindowState.Normal;
			radioForm.BringToFront();
		});
		button.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
		Controls.Add(button);
		btnUpdate = MakeButton("↻  VERIFICAR ATUALIZAÇÃO", 24, 654, 240, 36, async delegate
		{
			await UpdateClickedAsync();
		});
		btnUpdate.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
		Controls.Add(btnUpdate);
		lblVersion = new System.Windows.Forms.Label
		{
			Text = "Cliente 1.0.68.21",
			AutoSize = true,
			ForeColor = System.Drawing.Color.FromArgb(105, 118, 136),
			Anchor = (AnchorStyles.Bottom | AnchorStyles.Right),
			Location = new System.Drawing.Point(ClientSize.Width - 235, 665)
		};
		Controls.Add(lblVersion);
		Resize += delegate
		{
			lblVersion.Left = Math.Max(24, ClientSize.Width - lblVersion.Width - 28);
		};
		ResumeLayout(performLayout: false);
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
			BackColor = System.Drawing.Color.FromArgb(5, 18, 33)
		};
	}

	private System.Windows.Forms.Label NewCaption(string text, int x, int y, int width)
	{
		return new System.Windows.Forms.Label
		{
			Text = text,
			Left = x,
			Top = y,
			Width = width,
			Height = 18,
			ForeColor = System.Drawing.Color.FromArgb(186, 198, 214)
		};
	}

	private System.Windows.Forms.TextBox NewInput(int x, int y, int width, bool password)
	{
		return new System.Windows.Forms.TextBox
		{
			Left = x,
			Top = y,
			Width = width,
			Height = 28,
			BorderStyle = BorderStyle.FixedSingle,
			BackColor = System.Drawing.Color.FromArgb(7, 18, 31),
			ForeColor = System.Drawing.Color.White,
			UseSystemPasswordChar = password
		};
	}

	private System.Windows.Forms.Panel NewSeparator(int x, int y, int height)
	{
		return new System.Windows.Forms.Panel
		{
			Left = x,
			Top = y,
			Width = 1,
			Height = height,
			BackColor = System.Drawing.Color.FromArgb(57, 74, 96)
		};
	}

	private System.Windows.Forms.Label AddDamageItem(System.Windows.Forms.Control parent, string name, int x, int width)
	{
		System.Windows.Forms.Panel value = new System.Windows.Forms.Panel
		{
			Left = x,
			Top = 17,
			Width = 3,
			Height = 23,
			BackColor = System.Drawing.Color.FromArgb(67, 139, 255)
		};
		System.Windows.Forms.Label value2 = new System.Windows.Forms.Label
		{
			Text = name,
			Left = x + 11,
			Top = 18,
			Width = width - 62,
			Height = 22,
			ForeColor = System.Drawing.Color.Gainsboro,
			Font = new Font("Segoe UI", 8.75f)
		};
		System.Windows.Forms.Label label = new System.Windows.Forms.Label
		{
			Text = "—",
			Left = x + width - 49,
			Top = 18,
			Width = 48,
			Height = 22,
			ForeColor = System.Drawing.Color.FromArgb(130, 224, 69),
			Font = new Font("Segoe UI Semibold", 8.5f, System.Drawing.FontStyle.Bold),
			TextAlign = ContentAlignment.MiddleRight
		};
		parent.Controls.Add(value);
		parent.Controls.Add(value2);
		parent.Controls.Add(label);
		if (x > 20)
		{
			parent.Controls.Add(new System.Windows.Forms.Panel
			{
				Left = x - 8,
				Top = 12,
				Width = 1,
				Height = 34,
				BackColor = System.Drawing.Color.FromArgb(55, 71, 92)
			});
		}
		return label;
	}

	private System.Windows.Forms.GroupBox NewGroup(string text, int x, int y, int width, int height)
	{
		return new System.Windows.Forms.GroupBox
		{
			Text = text,
			Left = x,
			Top = y,
			Width = width,
			Height = height,
			ForeColor = System.Drawing.Color.Gainsboro,
			BackColor = System.Drawing.Color.FromArgb(28, 34, 45)
		};
	}

	private System.Windows.Forms.Label MakeValue(string text, int x, int y, int width)
	{
		return new System.Windows.Forms.Label
		{
			Text = text,
			Left = x,
			Top = y,
			Width = width,
			Height = 24,
			ForeColor = System.Drawing.Color.FromArgb(210, 220, 234)
		};
	}

	private System.Windows.Forms.Button MakeButton(string text, int x, int y, int width, int height, EventHandler handler)
	{
		System.Windows.Forms.Button button = new System.Windows.Forms.Button();
		button.Text = text;
		button.Left = x;
		button.Top = y;
		button.Width = width;
		button.Height = height;
		button.FlatStyle = FlatStyle.Flat;
		button.BackColor = System.Drawing.Color.FromArgb(11, 43, 82);
		button.ForeColor = System.Drawing.Color.FromArgb(215, 229, 249);
		button.Font = new Font("Segoe UI Semibold", 8.5f, System.Drawing.FontStyle.Regular);
		button.FlatAppearance.BorderSize = 1;
		button.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(60, 137, 245);
		button.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(18, 59, 111);
		button.FlatAppearance.MouseDownBackColor = System.Drawing.Color.FromArgb(25, 72, 132);
		button.Cursor = System.Windows.Forms.Cursors.Hand;
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
			lblAccount.ForeColor = System.Drawing.Color.Gold;
		}
	}

	private async Task AccountLoginClickedAsync()
	{
		string text = (txtAccountUser.Text ?? string.Empty).Trim();
		string text2 = txtAccountPassword.Text ?? string.Empty;
		if (text.Length == 0 || text2.Length == 0)
		{
			System.Windows.Forms.MessageBox.Show("Informe o usuário e a senha criados no site ROADBETS2.", "Conta RoadBETS2", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
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
				System.Windows.Forms.MessageBox.Show("Usuário ou senha inválidos. Use a mesma conta cadastrada no site.", "Conta RoadBETS2", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
				return;
			}
			string text3 = ApiClient.Str(apiResponse.Json["user"]);
			string text4 = ApiClient.Str(apiResponse.Json["token"]);
			if (string.IsNullOrWhiteSpace(text3) || string.IsNullOrWhiteSpace(text4))
			{
				throw new InvalidOperationException("O servidor não retornou a sessão da Conta RoadBETS2.");
			}
			ClientStore.SaveAccountCredential(text3, text4);
			txtAccountUser.Text = text3;
			txtAccountPassword.Clear();
			SetAccountState(text3, text4);
			_waiting = false;
			_loggedIn = false;
			_driver = string.Empty;
			_token = string.Empty;
			lblSession.Text = "ROADBETS2: conta reconhecida, aguardando sessão";
			if (chkAuto.Checked && cmbServers.SelectedItem is ServerEntry)
			{
				BeginWaiting(manual: false);
			}
		}
		catch (Exception ex)
		{
			ClientStore.Log("Conta RoadBETS2: " + ex);
			System.Windows.Forms.MessageBox.Show("Falha ao entrar na Conta RoadBETS2: " + ex.Message, "Conta RoadBETS2", MessageBoxButtons.OK, MessageBoxIcon.Hand);
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
			lblAccount.ForeColor = System.Drawing.Color.FromArgb(130, 224, 69);
			if (lblDriver != null && (string.IsNullOrWhiteSpace(_driver) || lblDriver.Text == "Motorista: -"))
			{
				lblDriver.Text = "Motorista: " + _accountUser.ToUpperInvariant();
			}
			if (lblPcRegister != null)
			{
				lblPcRegister.Text = "Validando registro do PC";
				lblPcRegister.ForeColor = System.Drawing.Color.Gold;
			}
			if (lblPcRegisterDetail != null)
			{
				lblPcRegisterDetail.Text = "A Central RoadBETS2 confirma o vínculo automaticamente.";
			}
			return;
		}
		lblAccount.Text = "Conta: não conectada";
		lblAccount.ForeColor = System.Drawing.Color.Gold;
		if (lblDriver != null)
		{
			lblDriver.Text = "Motorista: -";
		}
		if (lblPcRegister != null)
		{
			lblPcRegister.Text = "Registro do PC";
			lblPcRegister.ForeColor = System.Drawing.Color.FromArgb(168, 181, 199);
		}
		if (lblPcRegisterDetail != null)
		{
			lblPcRegisterDetail.Text = "Entre na conta para validar este PC.";
		}
		lblSession.Text = "ROADBETS2: entre na Conta RoadBETS2";
		lblTelemetry.Text = "Central RoadBETS2: aguardando conta";
	}

	private void SetPcRegistrationState(bool linked, string pairingCode)
	{
		if (lblPcRegister != null && lblPcRegisterDetail != null)
		{
			if (linked)
			{
				lblPcRegister.Text = "Registro autenticado do PC";
				lblPcRegister.ForeColor = System.Drawing.Color.FromArgb(130, 224, 69);
				lblPcRegisterDetail.Text = "Sua conta está vinculada a este PC para esta instalação.";
			}
			else
			{
				lblPcRegister.Text = "PC aguardando vínculo";
				lblPcRegister.ForeColor = System.Drawing.Color.Gold;
				lblPcRegisterDetail.Text = (string.IsNullOrWhiteSpace(pairingCode) ? "Confirme este dispositivo na primeira utilização." : ("Código de vínculo: " + pairingCode));
			}
		}
	}

	private async Task ValidatePcRegistrationAsync()
	{
		if (!AccountReady)
		{
			return;
		}
		try
		{
			string driver = _accountUser;
			string plainToken = ClientStore.GetPlainToken(ClientStore.FindCredential("https://api.gatlogets2.com.br", driver));
			ApiResponse apiResponse = await _api.LoginAsync("https://api.gatlogets2.com.br", driver, _deviceId, plainToken, _accountUser, _accountToken);
			if (apiResponse.StatusCode == 200 && apiResponse.Json != null && ApiClient.Bool(apiResponse.Json["ok"]))
			{
				string text = ApiClient.Str(apiResponse.Json["token"]);
				if (!string.IsNullOrWhiteSpace(text))
				{
					ClientStore.SaveCredential("https://api.gatlogets2.com.br", driver, text);
				}
				lblAccount.Text = "Conta: @" + _accountUser + " • PC vinculado";
				lblAccount.ForeColor = System.Drawing.Color.FromArgb(130, 224, 69);
				SetPcRegistrationState(linked: true, string.Empty);
			}
			else if (apiResponse.StatusCode == 428 && apiResponse.Json != null)
			{
				string pairingCode = ApiClient.Str(apiResponse.Json["pairing_code"]);
				SetPcRegistrationState(linked: false, pairingCode);
			}
			else
			{
				lblPcRegister.Text = "Validando registro do PC";
				lblPcRegister.ForeColor = System.Drawing.Color.Gold;
				lblPcRegisterDetail.Text = "A Central RoadBETS2 vai tentar novamente automaticamente.";
			}
		}
		catch (Exception ex)
		{
			ClientStore.Log("validacao inicial do PC: " + ex.Message);
			if (lblPcRegister != null)
			{
				lblPcRegister.Text = "Validando registro do PC";
				lblPcRegister.ForeColor = System.Drawing.Color.Gold;
			}
			if (lblPcRegisterDetail != null)
			{
				lblPcRegisterDetail.Text = "A Central RoadBETS2 vai tentar novamente automaticamente.";
			}
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
			if (VoiceCleanHandleLegacy(text, logLabel))
			{
				return true;
			}
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
			System.Windows.Forms.Label label = lblWorkStatus;
			string text;
			if (flag2)
			{
				text = "TRABALHO CONCLUÍDO";
			}
			else
			{
				text = (flag ? "TRABALHO EM ANDAMENTO" : string.Empty);
			}
			label.Text = text;
			lblWorkStatus.ForeColor = System.Drawing.Color.LimeGreen;
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
				lblTelemetry.Text = ((num2 > 0) ? ("Central RoadBETS2: ENTREGA " + num + " XP (-" + num2 + ")") : ("Central RoadBETS2: ENTREGA " + num + " XP"));
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
			{
				_tripJournal.MarkSent(receipt.TripId);
				System.Windows.Forms.Label label = lblTelemetry;
				label.Text = text switch
				{
					"integrity_not_verified" => "Central RoadBETS2: ENTREGA NAO VALIDADA - INTEGRIDADE", 
					"integrity_mod_blocked" => "Central RoadBETS2: ENTREGA NAO VALIDADA - MOD PROIBIDO", 
					"actual_distance_below_minimum" => "Central RoadBETS2: ENTREGA NAO VALIDADA - KM REAL INSUFICIENTE", 
					_ => "Central RoadBETS2: ENTREGA NAO VALIDADA - ODOMETRO/VEICULO", 
				};
				ClientStore.Log("entrega nao validada pela Central RoadBETS2: " + text + " / " + receipt.TripId);
				return;
			}
			}
		}
		if (apiResponse.StatusCode != 0 && apiResponse.StatusCode != 401)
		{
			ClientStore.Log("recibo pendente " + receipt.TripId + ": HTTP " + apiResponse.StatusCode + " " + apiResponse.Text);
		}
	}

	private static byte[] JoinBytes(params byte[][] parts)
	{
		byte[] array = new byte[parts.Where((byte[] x) => x != null).Sum((byte[] x) => x.Length)];
		int num = 0;
		foreach (byte[] array2 in parts)
		{
			if (array2 != null)
			{
				Buffer.BlockCopy(array2, 0, array, num, array2.Length);
				num += array2.Length;
			}
		}
		return array;
	}

	private static bool FixedBytesEqual(byte[] a, byte[] b)
	{
		if (a == null || b == null || a.Length != b.Length)
		{
			return false;
		}
		int num = 0;
		for (int i = 0; i < a.Length; i++)
		{
			num |= a[i] ^ b[i];
		}
		return num == 0;
	}

	private static string Sha256Hex(string value)
	{
		using SHA256 sHA = SHA256.Create();
		return BitConverter.ToString(sHA.ComputeHash(Encoding.UTF8.GetBytes(value ?? string.Empty))).Replace("-", string.Empty).ToLowerInvariant();
	}

	private byte[] LoadOrCreateJournalMasterKey()
	{
		ClientStore.Ensure();
		byte[] bytes = Encoding.UTF8.GetBytes("GAT-TELEMETRIA-LOCAL-JOURNAL-V1");
		if (File.Exists(CentralTelemetryKeyFile))
		{
			return ProtectedData.Unprotect(File.ReadAllBytes(CentralTelemetryKeyFile), bytes, DataProtectionScope.CurrentUser);
		}
		byte[] array = new byte[32];
		using (RandomNumberGenerator randomNumberGenerator = RandomNumberGenerator.Create())
		{
			randomNumberGenerator.GetBytes(array);
		}
		byte[] bytes2 = ProtectedData.Protect(array, bytes, DataProtectionScope.CurrentUser);
		File.WriteAllBytes(CentralTelemetryKeyFile, bytes2);
		return array;
	}

	private static byte[] DeriveJournalKey(byte[] master, string purpose)
	{
		using HMACSHA256 hMACSHA = new HMACSHA256(master);
		return hMACSHA.ComputeHash(Encoding.UTF8.GetBytes("GAT-JOURNAL-" + purpose));
	}

	private string EncryptJournalPacket(JObject packet)
	{
		byte[] master = LoadOrCreateJournalMasterKey();
		byte[] key = DeriveJournalKey(master, "ENC");
		byte[] key2 = DeriveJournalKey(master, "MAC");
		byte[] bytes = Encoding.UTF8.GetBytes(packet.ToString(Newtonsoft.Json.Formatting.None));
		byte[] iV;
		byte[] array;
		using (Aes aes = Aes.Create())
		{
			aes.Key = key;
			aes.Mode = CipherMode.CBC;
			aes.Padding = PaddingMode.PKCS7;
			aes.GenerateIV();
			iV = aes.IV;
			using ICryptoTransform cryptoTransform = aes.CreateEncryptor();
			array = cryptoTransform.TransformFinalBlock(bytes, 0, bytes.Length);
		}
		byte[] array2 = new byte[1] { 1 };
		byte[] buffer = JoinBytes(array2, iV, array);
		byte[] array3;
		using (HMACSHA256 hMACSHA = new HMACSHA256(key2))
		{
			array3 = hMACSHA.ComputeHash(buffer);
		}
		return Convert.ToBase64String(JoinBytes(array2, iV, array3, array));
	}

	private JObject DecryptJournalPacket(string line)
	{
		byte[] array = Convert.FromBase64String(line.Trim());
		if (array.Length < 50 || array[0] != 1)
		{
			throw new InvalidDataException("registro local invalido");
		}
		byte[] array2 = new byte[16];
		byte[] array3 = new byte[32];
		byte[] array4 = new byte[array.Length - 49];
		Buffer.BlockCopy(array, 1, array2, 0, array2.Length);
		Buffer.BlockCopy(array, 17, array3, 0, array3.Length);
		Buffer.BlockCopy(array, 49, array4, 0, array4.Length);
		byte[] master = LoadOrCreateJournalMasterKey();
		byte[] key = DeriveJournalKey(master, "ENC");
		byte[] b;
		using (HMACSHA256 hMACSHA = new HMACSHA256(DeriveJournalKey(master, "MAC")))
		{
			b = hMACSHA.ComputeHash(JoinBytes(new byte[1] { 1 }, array2, array4));
		}
		if (!FixedBytesEqual(array3, b))
		{
			throw new InvalidDataException("integridade da caixa-preta local falhou");
		}
		byte[] bytes;
		using (Aes aes = Aes.Create())
		{
			aes.Key = key;
			aes.IV = array2;
			aes.Mode = CipherMode.CBC;
			aes.Padding = PaddingMode.PKCS7;
			using ICryptoTransform cryptoTransform = aes.CreateDecryptor();
			bytes = cryptoTransform.TransformFinalBlock(array4, 0, array4.Length);
		}
		return JObject.Parse(Encoding.UTF8.GetString(bytes));
	}

	private void StampCentralTelemetry(JObject tele)
	{
		if (tele != null)
		{
			if (tele["gat_collected_at"] == null)
			{
				tele["gat_collected_at"] = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
			}
			if (tele["gat_packet_id"] == null)
			{
				tele["gat_packet_id"] = Guid.NewGuid().ToString("N");
			}
			string text = TextAny(tele, "gat_job_event_key", "job_latch_key", "gat_trip_id");
			if (string.IsNullOrWhiteSpace(text) && _latchedJob != null)
			{
				text = _latchedJobKey;
			}
			if (!string.IsNullOrWhiteSpace(text))
			{
				tele["gat_trip_id"] = text;
			}
		}
	}

	private void SealCentralTelemetry(JObject tele, string clientToken)
	{
		if (tele == null || string.IsNullOrWhiteSpace(clientToken))
		{
			return;
		}
		StampCentralTelemetry(tele);
		if (tele["gat_journal_chain"] != null)
		{
			return;
		}
		long num = 0L;
		string text = string.Empty;
		try
		{
			if (File.Exists(CentralJournalStateFile))
			{
				JObject jObject = JObject.Parse(File.ReadAllText(CentralJournalStateFile, Encoding.UTF8));
				num = Math.Max(0L, Convert.ToInt64(jObject["seq"] ?? ((JToken)0L), CultureInfo.InvariantCulture));
				text = Convert.ToString(jObject["chain"], CultureInfo.InvariantCulture) ?? string.Empty;
			}
		}
		catch
		{
			num = 0L;
			text = string.Empty;
		}
		num++;
		JObject jObject2 = (JObject)tele.DeepClone();
		string[] array = new string[7] { "gat_journal_seq", "gat_journal_prev", "gat_journal_chain", "gat_journal_payload_sha256", "gat_journal_version", "gat_journal_verified", "gat_journal_invalid" };
		foreach (string propertyName in array)
		{
			jObject2.Remove(propertyName);
		}
		string text2 = Sha256Hex(jObject2.ToString(Newtonsoft.Json.Formatting.None));
		string text3 = TextAny(tele, "gat_packet_id");
		string text4 = TextAny(tele, "gat_collected_at");
		string text5 = TextAny(tele, "gat_trip_id");
		string s = text3 + "|" + text4 + "|" + text5 + "|" + num.ToString(CultureInfo.InvariantCulture) + "|" + text + "|" + text2;
		byte[] key;
		using (SHA256 sHA = SHA256.Create())
		{
			key = sHA.ComputeHash(Encoding.UTF8.GetBytes("GAT-JOURNAL-V1|" + clientToken + "|" + _deviceId));
		}
		string text6;
		using (HMACSHA256 hMACSHA = new HMACSHA256(key))
		{
			text6 = BitConverter.ToString(hMACSHA.ComputeHash(Encoding.UTF8.GetBytes(s))).Replace("-", string.Empty).ToLowerInvariant();
		}
		tele["gat_journal_version"] = "1";
		tele["gat_journal_seq"] = num;
		tele["gat_journal_prev"] = text;
		tele["gat_journal_payload_sha256"] = text2;
		tele["gat_journal_chain"] = text6;
		File.WriteAllText(CentralJournalStateFile, new JObject
		{
			["seq"] = num,
			["chain"] = text6,
			["packet_id"] = text3,
			["updated_at"] = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)
		}.ToString(Newtonsoft.Json.Formatting.None), Encoding.UTF8);
	}

	private void AppendCentralBlackBox(JObject tele)
	{
		if (tele == null)
		{
			return;
		}
		try
		{
			ClientStore.Ensure();
			if (File.Exists(CentralTripBlackBoxFile) && new FileInfo(CentralTripBlackBoxFile).Length >= 134217728)
			{
				string text = CentralTripBlackBoxFile + ".previous";
				try
				{
					if (File.Exists(text))
					{
						File.Delete(text);
					}
				}
				catch
				{
				}
				try
				{
					File.Move(CentralTripBlackBoxFile, text);
				}
				catch
				{
				}
			}
			File.AppendAllText(CentralTripBlackBoxFile, EncryptJournalPacket(tele) + Environment.NewLine, Encoding.ASCII);
		}
		catch (Exception ex)
		{
			ClientStore.Log("caixa-preta local: " + ex.Message);
		}
	}

	private void QueueCentralTelemetry(JObject tele)
	{
		if (tele == null)
		{
			return;
		}
		try
		{
			ClientStore.Ensure();
			StampCentralTelemetry(tele);
			File.AppendAllText(CentralTelemetryQueueFile, EncryptJournalPacket(tele) + Environment.NewLine, Encoding.ASCII);
			ClientStore.Log("telemetria criptografada salva para reenvio: " + TextAny(tele, "gat_packet_id"));
		}
		catch (Exception ex)
		{
			ClientStore.Log("fila local segura: " + ex.Message);
		}
	}

	private List<JObject> LoadCentralTelemetryQueue()
	{
		List<JObject> list = new List<JObject>();
		if (!File.Exists(CentralTelemetryQueueFile))
		{
			return list;
		}
		string[] array = File.ReadAllLines(CentralTelemetryQueueFile, Encoding.ASCII);
		foreach (string text in array)
		{
			if (!string.IsNullOrWhiteSpace(text))
			{
				list.Add(DecryptJournalPacket(text));
			}
		}
		return list;
	}

	private void SaveCentralTelemetryQueue(IEnumerable<JObject> packets)
	{
		JObject[] array = (packets ?? Enumerable.Empty<JObject>()).ToArray();
		if (array.Length == 0)
		{
			if (File.Exists(CentralTelemetryQueueFile))
			{
				File.Delete(CentralTelemetryQueueFile);
			}
			return;
		}
		string text = CentralTelemetryQueueFile + ".tmp";
		File.WriteAllLines(text, array.Select(EncryptJournalPacket), Encoding.ASCII);
		if (File.Exists(CentralTelemetryQueueFile))
		{
			File.Delete(CentralTelemetryQueueFile);
		}
		File.Move(text, CentralTelemetryQueueFile);
	}

	private void MigrateLegacyCentralTelemetryQueue(string clientToken)
	{
		if (!File.Exists(LegacyCentralTelemetryQueueFile))
		{
			return;
		}
		try
		{
			List<JObject> list = new List<JObject>();
			string[] array = File.ReadAllLines(LegacyCentralTelemetryQueueFile, Encoding.UTF8);
			foreach (string text in array)
			{
				if (!string.IsNullOrWhiteSpace(text))
				{
					JObject jObject = JObject.Parse(text);
					StampCentralTelemetry(jObject);
					SealCentralTelemetry(jObject, clientToken);
					list.Add(jObject);
					AppendCentralBlackBox(jObject);
				}
			}
			if (list.Count > 0)
			{
				List<JObject> list2 = LoadCentralTelemetryQueue();
				list2.AddRange(list);
				SaveCentralTelemetryQueue(list2);
			}
			File.Delete(LegacyCentralTelemetryQueueFile);
			ClientStore.Log("fila antiga migrada para caixa-preta criptografada: " + list.Count + " pacote(s)");
		}
		catch (Exception ex)
		{
			ClientStore.Log("migracao da fila antiga: " + ex.Message);
		}
	}

	private async Task<int> FlushCentralTelemetryQueueAsync(string driver, string clientToken)
	{
		MigrateLegacyCentralTelemetryQueue(clientToken);
		List<JObject> packets;
		try
		{
			packets = LoadCentralTelemetryQueue();
		}
		catch (Exception ex)
		{
			ClientStore.Log("fila local recusada por integridade: " + ex.Message);
			lblTelemetry.Text = "Central RoadBETS2: caixa-preta local com erro de integridade";
			return 1;
		}
		if (packets.Count == 0)
		{
			return 0;
		}
		lblTelemetry.Text = "Central RoadBETS2: enviando viagem pendente...";
		int sent = 0;
		int limit = Math.Min(240, packets.Count);
		for (int i = 0; i < limit; i++)
		{
			JObject telemetry = packets[i];
			ApiResponse apiResponse = await _api.SendTelemetryAsync("https://api.gatlogets2.com.br", driver, _deviceId, clientToken, telemetry);
			if (apiResponse.StatusCode != 200 || apiResponse.Json == null || !ApiClient.Bool(apiResponse.Json["ok"]))
			{
				break;
			}
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
			lblTelemetry.Text = "Central RoadBETS2: aguardando ETS2";
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
		GatMapDetection gatMapDetection = DetectMapFromGameLog();
		tele["gat_map"] = gatMapDetection.Key;
		tele["gat_map_label"] = gatMapDetection.Label;
		tele["gat_map_package"] = gatMapDetection.PackageName;
		tele["gat_map_source"] = gatMapDetection.Source;
		StampCentralTelemetry(tele);
		lblTruck.Text = "TruckSim GPS: CONECTADO";
		UpdateTelemetryDisplay(TelemetryEngine.BuildDisplay(tele));
		RoadLifeOverlayPush(tele);
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
				lblAccount.ForeColor = System.Drawing.Color.Gold;
				SetPcRegistrationState(linked: false, text);
				lblTelemetry.Text = (string.IsNullOrWhiteSpace(text) ? "Central RoadBETS2: computador ainda nao vinculado" : ("Central RoadBETS2: digite o codigo " + text + " no site"));
				return;
			}
			if (apiResponse.StatusCode == 200 && apiResponse.Json != null && ApiClient.Bool(apiResponse.Json["ok"]))
			{
				centralClientToken = ApiClient.Str(apiResponse.Json["token"]);
				if (!string.IsNullOrWhiteSpace(centralClientToken))
				{
					ClientStore.SaveCredential("https://api.gatlogets2.com.br", centralDriver, centralClientToken);
					lblAccount.Text = "Conta: @" + _accountUser + " • PC vinculado";
					lblAccount.ForeColor = System.Drawing.Color.FromArgb(130, 224, 69);
					SetPcRegistrationState(linked: true, string.Empty);
				}
			}
			if (string.IsNullOrWhiteSpace(centralClientToken))
			{
				lblTelemetry.Text = ((apiResponse.StatusCode == 0) ? "Central RoadBETS2: reconectando..." : ("Central RoadBETS2: falha ao vincular HTTP " + apiResponse.StatusCode));
				return;
			}
		}
		SealCentralTelemetry(tele, centralClientToken);
		AppendCentralBlackBox(tele);
		if (await FlushCentralTelemetryQueueAsync(centralDriver, centralClientToken) > 0)
		{
			QueueCentralTelemetry(tele);
			lblTelemetry.Text = "Central RoadBETS2: viagem salva • aguardando servidor";
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
			lblAccount.ForeColor = System.Drawing.Color.FromArgb(130, 224, 69);
			SetPcRegistrationState(linked: true, string.Empty);
			bool flag = ApiClient.Bool(apiResponse2.Json["started"]);
			bool flag2 = ApiClient.Bool(apiResponse2.Json["completed_now"]);
			CheckMissionVoice(apiResponse2.Json, flag, flag2);
			UpdateWorkStatus(apiResponse2.Json);
			if (flag2)
			{
				lblTelemetry.Text = "Central RoadBETS2: ONLINE • MISSÃO CONCLUÍDA";
			}
			else if (flag)
			{
				lblTelemetry.Text = "Central RoadBETS2: ONLINE • MISSÃO INICIADA";
			}
			else if (BoolAny(tele, "job_latched") || BoolAny(tele, "on_job"))
			{
				lblTelemetry.Text = "Central RoadBETS2: ONLINE • TRABALHO EM ANDAMENTO";
			}
			else
			{
				lblTelemetry.Text = "Central RoadBETS2: ONLINE";
			}
		}
		else if (apiResponse2.StatusCode == 401)
		{
			ApiResponse apiResponse3 = await _api.LoginAsync("https://api.gatlogets2.com.br", centralDriver, _deviceId, centralClientToken, _accountUser, _accountToken);
			if (apiResponse3.StatusCode == 428 && apiResponse3.Json != null)
			{
				string text2 = ApiClient.Str(apiResponse3.Json["pairing_code"]);
				lblAccount.Text = "Vincular PC: " + text2;
				lblAccount.ForeColor = System.Drawing.Color.Gold;
				SetPcRegistrationState(linked: false, text2);
				lblTelemetry.Text = "Central RoadBETS2: digite o codigo " + text2 + " no site";
			}
			else
			{
				lblTelemetry.Text = "Central RoadBETS2: dispositivo precisa ser vinculado";
			}
		}
		else if (apiResponse2.StatusCode == 0)
		{
			QueueCentralTelemetry(tele);
			lblTelemetry.Text = "Central RoadBETS2: viagem salva • aguardando servidor";
		}
		else if (apiResponse2.StatusCode == 429 || apiResponse2.StatusCode >= 500)
		{
			QueueCentralTelemetry(tele);
			lblTelemetry.Text = "Central RoadBETS2: viagem salva • aguardando servidor";
		}
		else if (apiResponse2.StatusCode == 404)
		{
			lblTelemetry.Text = "Central RoadBETS2: atualize o servidor central";
		}
		else
		{
			lblTelemetry.Text = "Central RoadBETS2: falha HTTP " + apiResponse2.StatusCode;
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
		System.Windows.Forms.ComboBox comboBox = cmbMapMode;
		comboBox.SelectedIndex = text switch
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
			System.Windows.Forms.MessageBox.Show("Entre primeiro com a mesma conta criada no site ROADBETS2.", "ROADBETS2 TELEMETRIA", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
		}
		else if (!(cmbServers.SelectedItem is ServerEntry))
		{
			System.Windows.Forms.MessageBox.Show("Adicione ou selecione um servidor primeiro.", "ROADBETS2 TELEMETRIA", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
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
			lblSession.Text = "ROADBETS2: entre na Conta RoadBETS2";
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
			lblSession.Text = "ROADBETS2: aguardando sessão...";
			lblTelemetry.Text = "Envio: aguardando motorista";
			ClientStore.Log("aguardando sessao em " + _endpoint);
		}
	}

	private JObject StabilizeJobTelemetry(JObject tele)
	{
		if (tele == null)
		{
			return null;
		}
		string value = TextAny(tele, "cargo_name", "job.cargoName", "job.cargo");
		string text = TextAny(tele, "cargo_id", "job.cargoId", "Job.CargoId");
		string text2 = TextAny(tele, "source_city", "job.sourceCity", "Job.SourceCity");
		string text3 = TextAny(tele, "destination_city", "job.destinationCity", "Job.DestinationCity");
		double num = NumberAny(tele, "mass_kg", "cargoMass", "cargo_mass", "job.cargoMass", "Job.CargoMass");
		double num2 = NumberAny(tele, "planned_distance_km", "job.plannedDistanceKm", "Job.PlannedDistanceKm");
		double num3 = NumberAny(tele, "remaining_km");
		if (BoolAny(tele, "gameplay.onJob", "onJob", "job.onJob", "job.active") && (!string.IsNullOrWhiteSpace(value) || !string.IsNullOrWhiteSpace(text)) && num > 0.0)
		{
			string text4 = ((_latchedJob == null) ? string.Empty : TextAny(_latchedJob, "cargo_id"));
			string text5 = ((_latchedJob == null) ? string.Empty : TextAny(_latchedJob, "source_city"));
			string text6 = ((_latchedJob == null) ? string.Empty : TextAny(_latchedJob, "destination_city"));
			if (_latchedJob == null || (!string.IsNullOrWhiteSpace(text) && !string.IsNullOrWhiteSpace(text4) && !string.Equals(text, text4, StringComparison.OrdinalIgnoreCase)) || (!string.IsNullOrWhiteSpace(text2) && !string.IsNullOrWhiteSpace(text5) && !string.Equals(text2, text5, StringComparison.OrdinalIgnoreCase)) || (!string.IsNullOrWhiteSpace(text3) && !string.IsNullOrWhiteSpace(text6) && !string.Equals(text3, text6, StringComparison.OrdinalIgnoreCase)))
			{
				_latchedJob = new JObject();
				CopyValue(tele, _latchedJob, "cargo_name", "cargo_name", "job.cargoName", "job.cargo");
				CopyValue(tele, _latchedJob, "cargo_id", "cargo_id", "job.cargoId", "Job.CargoId");
				CopyValue(tele, _latchedJob, "mass_kg", "mass_kg", "cargoMass", "cargo_mass", "job.cargoMass", "Job.CargoMass");
				CopyValue(tele, _latchedJob, "source_city", "source_city", "job.sourceCity", "Job.SourceCity");
				CopyValue(tele, _latchedJob, "source_city_id", "source_city_id", "job.sourceCityId", "Job.SourceCityId");
				CopyValue(tele, _latchedJob, "destination_city", "destination_city", "job.destinationCity", "Job.DestinationCity");
				CopyValue(tele, _latchedJob, "destination_city_id", "destination_city_id", "job.destinationCityId", "Job.DestinationCityId");
				_latchedJob["planned_distance_km"] = ((num2 > 0.0) ? num2 : num3);
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
		if (_latchedJob != null)
		{
			string latchedJobKey = _latchedJobKey;
			tele["gat_schema"] = "job-v2";
			tele["gat_job_state"] = "idle";
			tele["gat_job_event"] = string.Empty;
			tele["gat_trip_id"] = latchedJobKey;
			tele["gat_previous_cargo_name"] = TextAny(_latchedJob, "cargo_name");
			tele["gat_previous_cargo_id"] = TextAny(_latchedJob, "cargo_id");
			tele["job_latched"] = false;
			tele["job_latch_key"] = latchedJobKey;
			tele["on_job"] = false;
			ClientStore.Log("JOB OBSERVED END | trip=" + latchedJobKey + " | Central decidira o resultado");
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
		EnsureTruckSimGpsRunning();
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
				lblTelemetry.Text = "Central RoadBETS2: aguardando conta";
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
				lblSession.Text = "ROADBETS2: servidor indisponível";
				return;
			}
			if (_serverInfo.Supported && !_serverInfo.Online)
			{
				_loggedIn = false;
				lblSession.Text = "ROADBETS2: servidor ETS2 offline";
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
					lblSession.Text = ((players.Players.Count == 0) ? "ROADBETS2: aguardando você entrar na sessão" : "ROADBETS2: aguardando motorista conhecido");
					return;
				}
				if (!string.Equals(_driver, text, StringComparison.OrdinalIgnoreCase))
				{
					_driver = text;
					_loggedIn = false;
				}
			}
			bool flag = !_loggedIn;
			if (flag)
			{
				bool flag2 = string.IsNullOrWhiteSpace(_driver);
				if (!flag2)
				{
					flag2 = !(await LoginAsync(_driver));
				}
				flag = flag2;
			}
			if (flag)
			{
				return;
			}
			ApiResponse hb;
			bool flag3;
			if ((DateTime.UtcNow - _lastHeartbeat).TotalSeconds >= 3.0)
			{
				hb = await _api.HeartbeatAsync(_endpoint, _driver, _deviceId, _token);
				_lastHeartbeat = DateTime.UtcNow;
				if (!IsAccepted(hb))
				{
					flag3 = NeedsTokenRenewal(hb);
					if (flag3)
					{
						flag3 = await LoginAsync(_driver, forceNewToken: true);
					}
					if (flag3)
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
			RoadLifeOverlayPush(identityTelemetry);
			hb = await _api.SendTelemetryAsync(_endpoint, _driver, _deviceId, _token, identityTelemetry);
			flag3 = !IsAccepted(hb) && NeedsTokenRenewal(hb);
			if (flag3)
			{
				flag3 = await LoginAsync(_driver, forceNewToken: true);
			}
			if (flag3)
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
			List<string> list2 = players.Where((string x) =>
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
		string text = value.Trim().Normalize(NormalizationForm.FormD);
		StringBuilder stringBuilder = new StringBuilder();
		bool flag = false;
		string text2 = text;
		foreach (char c in text2)
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
				list.Add(System.IO.Path.Combine(folderPath, "Euro Truck Simulator 2", "game.log.txt"));
			}
			string folderPath2 = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
			if (!string.IsNullOrWhiteSpace(folderPath2))
			{
				list.Add(System.IO.Path.Combine(folderPath2, "Documents", "Euro Truck Simulator 2", "game.log.txt"));
				list.Add(System.IO.Path.Combine(folderPath2, "OneDrive", "Documents", "Euro Truck Simulator 2", "game.log.txt"));
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
								text = System.IO.Path.GetDirectoryName(text2) ?? string.Empty;
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
			text = text.Replace('/', System.IO.Path.DirectorySeparatorChar);
			string path = System.IO.Path.Combine(text, "config", "loginusers.vdf");
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
			lblSession.Text = "ROADBETS2: login recusado";
			ClientStore.Log("login recusado " + apiResponse.StatusCode + " " + apiResponse.Text);
			return false;
		}
		if (!string.Equals(ApiClient.Str(apiResponse.Json?["account_user"]), _accountUser, StringComparison.OrdinalIgnoreCase))
		{
			lblSession.Text = "ROADBETS2: servidor precisa da versão 1.0.12";
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
		lblSession.Text = "ROADBETS2: CONECTADO";
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

	private static void SetDamageValue(System.Windows.Forms.Label label, string value)
	{
		if (label == null)
		{
			return;
		}
		string text = (label.Text = (string.IsNullOrWhiteSpace(value) ? "—" : value.Trim()));
		if (double.TryParse(text.TrimEnd('%'), NumberStyles.Any, CultureInfo.InvariantCulture, out var result))
		{
			if (result <= 5.0)
			{
				label.ForeColor = System.Drawing.Color.FromArgb(130, 224, 69);
			}
			else if (result <= 20.0)
			{
				label.ForeColor = System.Drawing.Color.Gold;
			}
			else
			{
				label.ForeColor = System.Drawing.Color.FromArgb(255, 105, 97);
			}
		}
		else
		{
			label.ForeColor = System.Drawing.Color.FromArgb(145, 158, 176);
		}
	}

	private async Task RefreshServerInfoAsync(bool force)
	{
		object selectedItem = cmbServers.SelectedItem;
		if (!(selectedItem is ServerEntry selected))
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
			System.Windows.Forms.Clipboard.SetText(_serverInfo.SessionId);
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
				System.Windows.Forms.MessageBox.Show("Esse servidor já está cadastrado.", "ROADBETS2 TELEMETRIA", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
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
		if (selectedIndex >= 0 && selectedIndex < _servers.Count && System.Windows.Forms.MessageBox.Show("Remover este servidor da lista?", "ROADBETS2 TELEMETRIA", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
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
				if (remoteVersion != null && IsNewer(remoteVersion.Version, "1.0.68.21") && !string.IsNullOrWhiteSpace(remoteVersion.EffectiveUrl))
				{
					_availableUpdate = remoteVersion;
					btnUpdate.Text = "ATUALIZAR CLIENTE 1.0.32";
					btnUpdate.BackColor = System.Drawing.Color.FromArgb(32, 132, 91);
					return;
				}
			}
			_availableUpdate = null;
			btnUpdate.Text = "VERIFICAR ATUALIZAÇÃO";
			if (showNoUpdate)
			{
				System.Windows.Forms.MessageBox.Show("Você já está na versão mais recente.", "ROADBETS2 TELEMETRIA", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
			}
		}
		catch (Exception ex)
		{
			if (showNoUpdate)
			{
				System.Windows.Forms.MessageBox.Show("Não foi possível verificar atualização.\r\n\r\n" + ex.Message, "ROADBETS2 TELEMETRIA", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
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
			if (System.Windows.Forms.MessageBox.Show("Instalar atualizacao do ROADBETS2 TELEMETRIA BETA?\r\n\r\n" + (_availableUpdate.Notes ?? string.Empty), "Atualizacao ROADBETS2 TELEMETRIA BETA", MessageBoxButtons.YesNo, MessageBoxIcon.Asterisk) != DialogResult.Yes)
			{
				return;
			}
			btnUpdate.Enabled = false;
			btnUpdate.Text = "BAIXANDO...";
			try
			{
				string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "GAT_TELEMETRIA_DOTNET_SETUP_" + _availableUpdate.Version + ".exe");
				using (HttpClient http = new HttpClient
				{
					Timeout = TimeSpan.FromMinutes(3.0)
				})
				{
					string path2 = path;
					File.WriteAllBytes(path2, await http.GetByteArrayAsync(_availableUpdate.EffectiveUrl));
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
				System.Windows.Forms.Application.Exit();
			}
			catch (Exception ex)
			{
				btnUpdate.Enabled = true;
				btnUpdate.Text = "TENTAR ATUALIZAÇÃO";
				System.Windows.Forms.MessageBox.Show("Falha ao atualizar:\r\n\r\n" + ex.Message, "ROADBETS2 TELEMETRIA", MessageBoxButtons.OK, MessageBoxIcon.Hand);
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

	private void ApplyHub041()
	{
		if (_hubApplied041)
		{
			return;
		}
		_hubApplied041 = true;
		SuspendLayout();
		List<System.Windows.Forms.Control> source = Controls.Cast<System.Windows.Forms.Control>().ToList();
		_hubAccountCard041 = source.OfType<ModernCard>().FirstOrDefault((ModernCard x) => x.Caption == "CONTA ROADBETS2");
		_hubServerCard041 = source.OfType<ModernCard>().FirstOrDefault((ModernCard x) => (x.Caption ?? "").StartsWith("COMBOIO / SERVIDOR"));
		Controls.Clear();
		Text = "ROADBETS2 TELEMETRIA 1.0.68.21";
		MinimumSize = new System.Drawing.Size(740, 500);
		AutoScaleMode = AutoScaleMode.Dpi;
		System.Drawing.Rectangle workingArea = Screen.FromControl(this).WorkingArea;
		Size = new System.Drawing.Size(Math.Min(1220, Math.Max(720, workingArea.Width - 24)), Math.Min(820, Math.Max(480, workingArea.Height - 36)));
		BackColor = System.Drawing.Color.FromArgb(3, 11, 22);
		TableLayoutPanel tableLayoutPanel = new TableLayoutPanel
		{
			Name = "hubShell051",
			Dock = DockStyle.Fill,
			ColumnCount = 1,
			RowCount = 4,
			BackColor = BackColor,
			Margin = Padding.Empty,
			Padding = Padding.Empty
		};
		tableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
		tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 76f));
		tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 50f));
		tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
		tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 28f));
		Controls.Add(tableLayoutPanel);
		tableLayoutPanel.Controls.Add(HubHeader041(), 0, 0);
		tableLayoutPanel.Controls.Add(HubNav041(), 0, 1);
		_hubBody041 = new System.Windows.Forms.Panel
		{
			Dock = DockStyle.Fill,
			Padding = new Padding(18, 14, 18, 14),
			BackColor = BackColor
		};
		tableLayoutPanel.Controls.Add(_hubBody041, 0, 2);
		tableLayoutPanel.Controls.Add(new System.Windows.Forms.Label
		{
			Dock = DockStyle.Fill,
			Text = "ROADBETS2 ETS2  •  Cliente 1.0.68.21  •  conexão que move distâncias",
			Padding = new Padding(20, 0, 0, 0),
			TextAlign = ContentAlignment.MiddleLeft,
			ForeColor = System.Drawing.Color.FromArgb(104, 128, 155),
			BackColor = System.Drawing.Color.FromArgb(2, 8, 17)
		}, 0, 3);
		AddHubPage041("home", Home041());
		AddHubPage041("dash", Dash041());
		AddHubPage041("radio", Radio041());
		AddHubPage041("gps", Simple041("GPS", "GPS por voz e alertas ficarão aqui. A seleção de novas vozes será adicionada depois."));
		AddHubPage041("server", Server041());
		AddHubPage041("updates", Updates041());
		AddHubPage041("settings", Settings041());
		ShowHubPage041("home");
		_hubStatusTimer041.Tick += delegate
		{
			SyncHub041();
		};
		_hubStatusTimer041.Start();
		_hubDashTimer041.Tick += async delegate
		{
			await PollDash041();
		};
		Shown += async delegate
		{
			try
			{
				System.Drawing.Rectangle workingArea2 = Screen.FromControl(this).WorkingArea;
				if (workingArea2.Width < 1400 || workingArea2.Height < 800)
				{
					int num = Math.Max(MinimumSize.Width, Math.Min(1220, workingArea2.Width - 20));
					int num2 = Math.Max(MinimumSize.Height, Math.Min(820, workingArea2.Height - 24));
					Size = new System.Drawing.Size(num, num2);
					Location = new System.Drawing.Point(workingArea2.Left + Math.Max(0, (workingArea2.Width - num) / 2), workingArea2.Top + Math.Max(0, (workingArea2.Height - num2) / 2));
				}
			}
			catch
			{
			}
			SyncHub041();
			await InitDash041();
			ApplyResponsive051();
		};
		Resize += delegate
		{
			ApplyResponsive051();
		};
		FormClosed += delegate
		{
			try
			{
				_hubStatusTimer041.Stop();
				_hubDashTimer041.Stop();
				_hubStatusTimer041.Dispose();
				_hubDashTimer041.Dispose();
			}
			catch
			{
			}
			try
			{
				_truckOverlay041?.Close();
				_videoOverlay041?.Close();
				_dashOverlay060?.Close();
			}
			catch
			{
			}
			try
			{
				_hubRadio041?.Dispose();
				_hubDash041?.Dispose();
				_hubHttp041.Dispose();
			}
			catch
			{
			}
		};
		VoiceCleanInitialize();
		RoadLifeOverlayInitialize();
		ResumeLayout(performLayout: true);
	}

	private void ApplyResponsive051()
	{
		try
		{
			int num = Math.Max(1, ClientSize.Width);
			int num2 = Math.Max(1, ClientSize.Height);
			bool flag = num < 1180 || num2 < 760;
			bool flag2 = num < 1024 || num2 < 650;
			bool flag3 = num < 850 || num2 < 560;
			if (_hubBody041 != null)
			{
				System.Windows.Forms.Panel hubBody = _hubBody041;
				Padding padding;
				if (flag3)
				{
					padding = new Padding(5, 4, 5, 4);
				}
				else
				{
					padding = (flag ? new Padding(9, 7, 9, 7) : new Padding(18, 14, 18, 14));
				}
				hubBody.Padding = padding;
			}
			TableLayoutPanel tableLayoutPanel = FindControl051<TableLayoutPanel>(this, "hubShell051");
			if (tableLayoutPanel != null && tableLayoutPanel.RowStyles.Count >= 4)
			{
				tableLayoutPanel.RowStyles[0].Height = (flag3 ? 50 : (flag2 ? 56 : (flag ? 64 : 76)));
				tableLayoutPanel.RowStyles[1].Height = (flag3 ? 36 : (flag2 ? 40 : (flag ? 44 : 50)));
				tableLayoutPanel.RowStyles[3].Height = (flag3 ? 19 : (flag ? 22 : 28));
			}
			FlowLayoutPanel flowLayoutPanel = FindControl051<FlowLayoutPanel>(this, "hubNav051");
			if (flowLayoutPanel != null)
			{
				FlowLayoutPanel flowLayoutPanel2 = flowLayoutPanel;
				Padding padding2;
				if (flag2)
				{
					padding2 = new Padding(8, 4, 0, 3);
				}
				else
				{
					padding2 = (flag ? new Padding(12, 5, 0, 4) : new Padding(18, 6, 0, 4));
				}
				flowLayoutPanel2.Padding = padding2;
				foreach (System.Windows.Forms.Button item in flowLayoutPanel.Controls.OfType<System.Windows.Forms.Button>())
				{
					System.Windows.Forms.Button button = item;
					int num3;
					if (flag2)
					{
						num3 = 30;
					}
					else
					{
						num3 = (flag ? 33 : 36);
					}
					button.Height = num3;
					System.Windows.Forms.Button button2 = item;
					float emSize;
					if (flag3)
					{
						emSize = 6.5f;
					}
					else if (flag2)
					{
						emSize = 7.2f;
					}
					else
					{
						emSize = (flag ? 8f : 8.5f);
					}
					button2.Font = new Font("Segoe UI Semibold", emSize, System.Drawing.FontStyle.Bold);
				}
				List<System.Windows.Forms.Button> list = flowLayoutPanel.Controls.OfType<System.Windows.Forms.Button>().ToList();
				int num4 = 885;
				int num5 = Math.Max(560, flowLayoutPanel.ClientSize.Width - flowLayoutPanel.Padding.Horizontal - 42);
				double num6 = Math.Min(1.0, (double)num5 / (double)num4);
				int[] array = new int[7] { 105, 120, 125, 80, 175, 135, 145 };
				for (int i = 0; i < list.Count && i < array.Length; i++)
				{
					list[i].Width = Math.Max(66, (int)Math.Round((double)array[i] * num6));
				}
				foreach (System.Windows.Forms.Button item2 in list)
				{
					item2.Margin = new Padding(0, 0, flag3 ? 3 : 6, 0);
				}
				if (flag3)
				{
					flowLayoutPanel.Padding = new Padding(5, 3, 0, 2);
				}
				System.Windows.Forms.Button[] array2 = new System.Windows.Forms.Button[0];
				for (int j = 0; j < array2.Length; j++)
				{
					_ = array2[j];
				}
			}
			System.Windows.Forms.Label label = FindControl051<System.Windows.Forms.Label>(this, "hubBrand055");
			System.Windows.Forms.Label label2 = FindControl051<System.Windows.Forms.Label>(this, "hubTitle055");
			System.Windows.Forms.Label label3 = FindControl051<System.Windows.Forms.Label>(this, "hubSub055");
			if (label != null)
			{
				label.Left = (flag3 ? 10 : 22);
				label.Top = (flag3 ? 5 : 10);
				System.Windows.Forms.Label label4 = label;
				float emSize2;
				if (flag3)
				{
					emSize2 = 18f;
				}
				else
				{
					emSize2 = (flag2 ? 21f : 25f);
				}
				label4.Font = new Font("Segoe UI Black", emSize2, System.Drawing.FontStyle.Bold | System.Drawing.FontStyle.Italic);
			}
			if (label2 != null)
			{
				System.Windows.Forms.Label label5 = label2;
				int left;
				if (flag3)
				{
					left = 78;
				}
				else
				{
					left = (flag2 ? 98 : 118);
				}
				label5.Left = left;
				label2.Top = (flag3 ? 7 : 13);
				System.Windows.Forms.Label label6 = label2;
				float emSize3;
				if (flag3)
				{
					emSize3 = 12f;
				}
				else
				{
					emSize3 = (flag2 ? 15f : 18f);
				}
				label6.Font = new Font("Segoe UI Semibold", emSize3, System.Drawing.FontStyle.Bold);
			}
			if (label3 != null)
			{
				label3.Visible = !flag3;
				label3.Left = (flag2 ? 101 : 121);
				label3.Top = (flag2 ? 37 : 47);
				label3.Font = new Font("Segoe UI", flag2 ? 7.5f : 9f);
			}
			System.Windows.Forms.Panel panel = FindControl051<System.Windows.Forms.Panel>(this, "dashTools051");
			if (panel != null)
			{
				ArrangeDashTools051(panel, flag2);
			}
			ApplyAllPages056();
			ApplyDashZoom051();
		}
		catch
		{
		}
	}

	private static T FindControl051<T>(System.Windows.Forms.Control root, string name) where T : System.Windows.Forms.Control
	{
		if (root == null)
		{
			return null;
		}
		foreach (System.Windows.Forms.Control control in root.Controls)
		{
			if (control is T result && string.Equals(control.Name, name, StringComparison.Ordinal))
			{
				return result;
			}
			T val = FindControl051<T>(control, name);
			if (val != null)
			{
				return val;
			}
		}
		return null;
	}

	private void ArrangeDashTools051(System.Windows.Forms.Panel tools, bool veryCompact)
	{
		List<System.Windows.Forms.Button> list = tools.Controls.OfType<System.Windows.Forms.Button>().ToList();
		System.Windows.Forms.Label label = tools.Controls.OfType<System.Windows.Forms.Label>().FirstOrDefault((System.Windows.Forms.Label x) => string.Equals(x.Text, "Transparência", StringComparison.Ordinal));
		int num = Math.Max(1, tools.ClientSize.Width);
		int num2 = 8;
		int num3 = 8;
		int num4 = (veryCompact ? 112 : 150);
		int num5 = (veryCompact ? 136 : 180);
		if (list.Count > 0)
		{
			list[0].Left = num3;
			list[0].Width = num4;
			list[0].Font = new Font("Segoe UI Semibold", veryCompact ? 7f : 8f, System.Drawing.FontStyle.Bold);
			num3 += num4 + num2;
		}
		if (list.Count > 1)
		{
			list[1].Left = num3;
			list[1].Width = num5;
			list[1].Font = new Font("Segoe UI Semibold", veryCompact ? 7f : 8f, System.Drawing.FontStyle.Bold);
			num3 += num5 + num2;
		}
		if (label != null)
		{
			label.Visible = num >= 700;
			label.Left = num3 + 4;
			label.Width = 88;
			if (label.Visible)
			{
				num3 += 94;
			}
		}
		if (_overlayOpacity041 != null)
		{
			_overlayOpacity041.Left = num3;
			_overlayOpacity041.Width = Math.Max(90, num - num3 - 8);
		}
	}

	private void ApplyDashZoom051()
	{
		try
		{
			if (_hubDash041 != null && !_hubDash041.IsDisposed && Math.Abs(_hubDash041.ZoomFactor - 1.0) > 0.001)
			{
				_hubDash041.ZoomFactor = 1.0;
			}
		}
		catch
		{
		}
	}

	private void ApplyAllPages056()
	{
		try
		{
			foreach (KeyValuePair<string, System.Windows.Forms.Panel> item in _hubPages041.ToList())
			{
				LayoutPage056(item.Key, item.Value);
			}
		}
		catch
		{
		}
	}

	private void LayoutPage056(string key056, System.Windows.Forms.Panel page056)
	{
		if (page056 == null || page056.IsDisposed)
		{
			return;
		}
		int num = Math.Max(1, page056.ClientSize.Width);
		int num2 = Math.Max(1, page056.ClientSize.Height);
		bool flag = num < 980 || num2 < 620;
		bool flag2 = num < 820 || num2 < 540;
		foreach (System.Windows.Forms.Label item in from x in page056.Controls.OfType<System.Windows.Forms.Label>()
			where x.Top <= 8 && x.Height >= 45
			select x)
		{
			item.Width = Math.Max(120, num - 6);
			System.Windows.Forms.Label label = item;
			float emSize;
			if (flag2)
			{
				emSize = 9.5f;
			}
			else
			{
				emSize = (flag ? 10.5f : 12f);
			}
			label.Font = new Font("Segoe UI Semibold", emSize, System.Drawing.FontStyle.Bold);
		}
		if (string.Equals(key056, "home", StringComparison.OrdinalIgnoreCase))
		{
			page056.AutoScroll = false;
			TableLayoutPanel tableLayoutPanel = page056.Controls.OfType<TableLayoutPanel>().FirstOrDefault((TableLayoutPanel x) => x.Dock == DockStyle.Fill);
			if (tableLayoutPanel != null && tableLayoutPanel.RowStyles.Count >= 2)
			{
				tableLayoutPanel.RowStyles[0].Height = (flag2 ? 158 : (flag ? 178 : 205));
			}
			if (_homeAvatar045 != null)
			{
				int num3;
				if (flag2)
				{
					num3 = 56;
				}
				else
				{
					num3 = (flag ? 70 : 86);
				}
				_homeAvatar045.SetBounds(flag2 ? 9 : 16, flag2 ? 48 : 54, num3, num3);
			}
			if (_homeProfileName045 != null)
			{
				System.Windows.Forms.Label homeProfileName = _homeProfileName045;
				int left;
				if (flag2)
				{
					left = 76;
				}
				else
				{
					left = (flag ? 98 : 116);
				}
				homeProfileName.Left = left;
				_homeProfileName045.Top = (flag2 ? 48 : 55);
				_homeProfileName045.Width = Math.Max(90, (_homeProfileName045.Parent?.ClientSize.Width ?? 260) - _homeProfileName045.Left - 8);
				System.Windows.Forms.Label homeProfileName2 = _homeProfileName045;
				float emSize2;
				if (flag2)
				{
					emSize2 = 10f;
				}
				else
				{
					emSize2 = (flag ? 12f : 14f);
				}
				homeProfileName2.Font = new Font("Segoe UI Semibold", emSize2, System.Drawing.FontStyle.Bold);
			}
			if (_homeProfileMeta045 != null)
			{
				System.Windows.Forms.Label homeProfileMeta = _homeProfileMeta045;
				int left2;
				if (flag2)
				{
					left2 = 76;
				}
				else
				{
					left2 = (flag ? 98 : 116);
				}
				homeProfileMeta.Left = left2;
				_homeProfileMeta045.Top = (flag2 ? 76 : 89);
				_homeProfileMeta045.Width = Math.Max(90, (_homeProfileMeta045.Parent?.ClientSize.Width ?? 280) - _homeProfileMeta045.Left - 8);
				_homeProfileMeta045.Height = (flag2 ? 70 : 92);
				System.Windows.Forms.Label homeProfileMeta2 = _homeProfileMeta045;
				float emSize3;
				if (flag2)
				{
					emSize3 = 7.3f;
				}
				else
				{
					emSize3 = (flag ? 8.2f : 9.2f);
				}
				homeProfileMeta2.Font = new Font("Segoe UI", emSize3);
			}
			if (_homeTrip045 != null)
			{
				System.Windows.Forms.Label homeTrip = _homeTrip045;
				float emSize4;
				if (flag2)
				{
					emSize4 = 7.5f;
				}
				else
				{
					emSize4 = (flag ? 8.5f : 9.7f);
				}
				homeTrip.Font = new Font("Segoe UI Semibold", emSize4);
			}
			if (_homeSystem045 != null)
			{
				System.Windows.Forms.Label homeSystem = _homeSystem045;
				float emSize5;
				if (flag2)
				{
					emSize5 = 7.5f;
				}
				else
				{
					emSize5 = (flag ? 8.5f : 9.6f);
				}
				homeSystem.Font = new Font("Segoe UI Semibold", emSize5);
			}
			if (_homeDrivers045 != null)
			{
				DataGridView homeDrivers = _homeDrivers045;
				int columnHeadersHeight;
				if (flag2)
				{
					columnHeadersHeight = 26;
				}
				else
				{
					columnHeadersHeight = (flag ? 30 : 35);
				}
				homeDrivers.ColumnHeadersHeight = columnHeadersHeight;
				DataGridViewRow rowTemplate = _homeDrivers045.RowTemplate;
				int num4;
				if (flag2)
				{
					num4 = 28;
				}
				else
				{
					num4 = (flag ? 32 : 36);
				}
				rowTemplate.Height = num4;
				DataGridViewCellStyle defaultCellStyle = _homeDrivers045.DefaultCellStyle;
				float emSize6;
				if (flag2)
				{
					emSize6 = 7.2f;
				}
				else
				{
					emSize6 = (flag ? 8f : 8.7f);
				}
				defaultCellStyle.Font = new Font("Segoe UI", emSize6);
				DataGridViewCellStyle columnHeadersDefaultCellStyle = _homeDrivers045.ColumnHeadersDefaultCellStyle;
				float emSize7;
				if (flag2)
				{
					emSize7 = 6.8f;
				}
				else
				{
					emSize7 = (flag ? 7.5f : 8.2f);
				}
				columnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", emSize7, System.Drawing.FontStyle.Bold);
			}
			return;
		}
		if (string.Equals(key056, "dash", StringComparison.OrdinalIgnoreCase))
		{
			page056.AutoScroll = false;
			page056.AutoScrollMinSize = System.Drawing.Size.Empty;
			System.Windows.Forms.Panel panel = FindControl051<System.Windows.Forms.Panel>(page056, "dashTools051");
			if (panel != null)
			{
				int num5 = (flag2 ? 49 : 55);
				int num6 = (flag2 ? 49 : 55);
				panel.SetBounds(0, num5, num, num6);
				ArrangeDashTools051(panel, flag | flag2);
				if (_hubDash041 != null && !_hubDash041.IsDisposed)
				{
					_hubDash041.SetBounds(0, num5 + num6 + 8, num, Math.Max(1, num2 - (num5 + num6 + 8)));
				}
			}
			else if (_hubDash041 != null && !_hubDash041.IsDisposed)
			{
				_hubDash041.SetBounds(0, 112, num, Math.Max(1, num2 - 112));
			}
			return;
		}
		if (string.Equals(key056, "radio", StringComparison.OrdinalIgnoreCase))
		{
			page056.AutoScroll = false;
			int num7 = (flag2 ? 48 : 55);
			if (_hubRadioHost041 != null && !_hubRadioHost041.IsDisposed)
			{
				_hubRadioHost041.SetBounds(0, num7, num, Math.Max(1, num2 - num7));
			}
			if (_hubRadio041 != null && !_hubRadio041.IsDisposed)
			{
				_hubRadio041.AutoScroll = true;
				_hubRadio041.AutoScrollMinSize = new System.Drawing.Size(Math.Min(720, Math.Max(560, num - 8)), Math.Min(520, Math.Max(390, num2 - num7 - 8)));
				_hubRadio041.Dock = DockStyle.Fill;
			}
			return;
		}
		int num8 = 240;
		if (string.Equals(key056, "settings", StringComparison.OrdinalIgnoreCase))
		{
			num8 = 390;
		}
		else if (string.Equals(key056, "server", StringComparison.OrdinalIgnoreCase))
		{
			num8 = 330;
		}
		else if (string.Equals(key056, "updates", StringComparison.OrdinalIgnoreCase))
		{
			num8 = 190;
		}
		else if (string.Equals(key056, "gps", StringComparison.OrdinalIgnoreCase))
		{
			num8 = 150;
		}
		page056.AutoScroll = num2 < num8;
		page056.AutoScrollMinSize = (page056.AutoScroll ? new System.Drawing.Size(0, num8) : System.Drawing.Size.Empty);
		if (string.Equals(key056, "server", StringComparison.OrdinalIgnoreCase) && _hubServerCard041 != null)
		{
			_hubServerCard041.Left = 0;
			_hubServerCard041.Top = (flag2 ? 52 : 65);
			_hubServerCard041.Width = Math.Max(220, num - (page056.VerticalScroll.Visible ? (SystemInformation.VerticalScrollBarWidth + 4) : 0));
		}
		if (string.Equals(key056, "settings", StringComparison.OrdinalIgnoreCase))
		{
			if (_hubAccountCard041 != null)
			{
				_hubAccountCard041.Left = 0;
				_hubAccountCard041.Top = (flag2 ? 52 : 65);
				_hubAccountCard041.Width = Math.Max(220, num - (page056.VerticalScroll.Visible ? (SystemInformation.VerticalScrollBarWidth + 4) : 0));
			}
			foreach (System.Windows.Forms.Label item2 in from x in page056.Controls.OfType<System.Windows.Forms.Label>()
				where x.Top > 120
				select x)
			{
				item2.Width = Math.Max(200, num - 12);
				item2.Font = new Font("Segoe UI", flag2 ? 8f : 9f);
			}
		}
		if (!string.Equals(key056, "updates", StringComparison.OrdinalIgnoreCase))
		{
			return;
		}
		foreach (System.Windows.Forms.Button item3 in page056.Controls.OfType<System.Windows.Forms.Button>())
		{
			item3.Left = 0;
			item3.Top = (flag2 ? 82 : 100);
			item3.Width = Math.Min(260, Math.Max(180, num - 12));
			item3.Height = (flag2 ? 32 : 36);
		}
	}

	private System.Windows.Forms.Control HubHeader041()
	{
		System.Windows.Forms.Panel panel = new System.Windows.Forms.Panel
		{
			Dock = DockStyle.Fill,
			BackColor = System.Drawing.Color.FromArgb(7, 24, 18)
		};
		PictureBox pictureBox = new PictureBox
		{
			Left = 18,
			Top = 10,
			Width = 54,
			Height = 54,
			SizeMode = PictureBoxSizeMode.Zoom,
			BackColor = System.Drawing.Color.Transparent
		};
		string text = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "RoadLifeDash", "Assets", "roadbets2-logo.png");
		if (File.Exists(text))
		{
			using System.Drawing.Image original = System.Drawing.Image.FromFile(text);
			pictureBox.Image = new Bitmap(original);
		}
		else
		{
			pictureBox.Controls.Add(new RoadLifeMark044
			{
				Dock = DockStyle.Fill
			});
		}
		panel.Controls.Add(pictureBox);
		panel.Controls.Add(new System.Windows.Forms.Label
		{
			Text = "ROADBETS2",
			Left = 80,
			Top = 7,
			AutoSize = true,
			ForeColor = System.Drawing.Color.FromArgb(54, 242, 122),
			Font = new Font("Segoe UI Black", 26f, System.Drawing.FontStyle.Bold | System.Drawing.FontStyle.Italic)
		});
		panel.Controls.Add(new System.Windows.Forms.Label
		{
			Text = "TELEMETRIA",
			Left = 267,
			Top = 20,
			AutoSize = true,
			ForeColor = System.Drawing.Color.White,
			Font = new Font("Segoe UI Semibold", 15f, System.Drawing.FontStyle.Bold)
		});
		return panel;
	}

	private System.Windows.Forms.Control HubNav041()
	{
		FlowLayoutPanel flowLayoutPanel = new FlowLayoutPanel
		{
			Name = "hubNav051",
			Dock = DockStyle.Fill,
			FlowDirection = System.Windows.Forms.FlowDirection.LeftToRight,
			WrapContents = false,
			AutoScroll = true,
			Padding = new Padding(18, 6, 0, 4),
			BackColor = System.Drawing.Color.FromArgb(5, 18, 33)
		};
		Nav041(flowLayoutPanel, "home", "INÍCIO", 105);
		Nav041(flowLayoutPanel, "dash", "DASHBOARD", 120);
		Nav041(flowLayoutPanel, "radio", "RÁDIO ROADBETS2", 125);
		Nav041(flowLayoutPanel, "gps", "GPS", 80);
		Nav041(flowLayoutPanel, "server", "COMBOIO / SERVIDOR", 175);
		Nav041(flowLayoutPanel, "updates", "ATUALIZAÇÕES", 135);
		Nav041(flowLayoutPanel, "settings", "CONFIGURAÇÕES", 145);
		return flowLayoutPanel;
	}

	private void Nav041(FlowLayoutPanel p, string key, string text, int width)
	{
		System.Windows.Forms.Button button = HubButton041(text, width);
		button.Margin = new Padding(0, 0, 7, 0);
		button.Click += delegate
		{
			ShowHubPage041(key);
		};
		_hubNav041[key] = button;
		p.Controls.Add(button);
	}

	private System.Windows.Forms.Button HubButton041(string text, int width)
	{
		System.Windows.Forms.Button button = new System.Windows.Forms.Button();
		button.Text = text;
		button.Width = width;
		button.Height = 36;
		button.FlatStyle = FlatStyle.Flat;
		button.BackColor = System.Drawing.Color.FromArgb(7, 38, 26);
		button.ForeColor = System.Drawing.Color.FromArgb(210, 225, 242);
		button.Font = new Font("Segoe UI Semibold", 8.5f, System.Drawing.FontStyle.Bold);
		button.Cursor = System.Windows.Forms.Cursors.Hand;
		button.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(35, 119, 75);
		return button;
	}

	private void AddHubPage041(string key, System.Windows.Forms.Panel p)
	{
		p.Dock = DockStyle.Fill;
		p.Visible = false;
		_hubPages041[key] = p;
		_hubBody041.Controls.Add(p);
	}

	private void ShowHubPage041(string key)
	{
		bool flag = _hubRadio041 != null && !_hubRadio041.IsDisposed;
		foreach (KeyValuePair<string, System.Windows.Forms.Panel> item in _hubPages041)
		{
			if ((item.Key == "radio") & flag)
			{
				item.Value.Visible = true;
				if (key != "radio")
				{
					item.Value.SendToBack();
				}
			}
			else
			{
				item.Value.Visible = item.Key == key;
			}
		}
		foreach (KeyValuePair<string, System.Windows.Forms.Button> item2 in _hubNav041)
		{
			bool flag2 = item2.Key == key;
			item2.Value.BackColor = (flag2 ? System.Drawing.Color.FromArgb(24, 111, 65) : System.Drawing.Color.FromArgb(7, 38, 26));
			item2.Value.ForeColor = (flag2 ? System.Drawing.Color.White : System.Drawing.Color.FromArgb(210, 225, 242));
		}
		if (key == "radio")
		{
			EnsureRadio041();
			if (_hubPages041.TryGetValue("radio", out var value))
			{
				value.Visible = true;
			}
		}
		if (_hubPages041.TryGetValue(key, out var value2))
		{
			value2.BringToFront();
		}
	}

	private System.Windows.Forms.Panel Home041()
	{
		System.Windows.Forms.Panel panel = Page041();
		panel.Controls.Add(Head041("INÍCIO", "Status essencial e acesso aos módulos GAT."));
		TableLayoutPanel tableLayoutPanel = new TableLayoutPanel
		{
			Left = 0,
			Top = 58,
			Height = 180,
			Width = panel.Width,
			Anchor = (AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right),
			ColumnCount = 4,
			RowCount = 1
		};
		for (int i = 0; i < 4; i++)
		{
			tableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
		}
		_hAccount041 = StatusCard041(tableLayoutPanel, 0, "CONTA ROADBETS2", "Conta não conectada");
		_hEts041 = StatusCard041(tableLayoutPanel, 1, "ETS2 / TELEMETRIA", "Aguardando TruckSim GPS");
		_hCentral041 = StatusCard041(tableLayoutPanel, 2, "CENTRAL ROADBETS2", "Aguardando conexão");
		_hServer041 = StatusCard041(tableLayoutPanel, 3, "COMBOIO / SERVIDOR", "Opcional");
		panel.Controls.Add(tableLayoutPanel);
		FlowLayoutPanel flowLayoutPanel = new FlowLayoutPanel
		{
			Left = 0,
			Top = 265,
			Width = panel.Width,
			Height = 155,
			Anchor = (AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right),
			WrapContents = false,
			AutoScroll = true
		};
		Module041(flowLayoutPanel, "DASHBOARD", "RoadBETS2 Dash e sobreposição de vídeo", "dash");
		Module041(flowLayoutPanel, "RÁDIO ROADBETS2", "Canal RoadBETS2 e Meu Vídeo", "radio");
		Module041(flowLayoutPanel, "GPS", "Navegação e alertas", "gps");
		Module041(flowLayoutPanel, "COMBOIO", "Servidor e sala", "server");
		Module041(flowLayoutPanel, "CONFIGURAÇÕES", "Conta e preferências", "settings");
		panel.Controls.Add(flowLayoutPanel);
		panel.Controls.Add(new System.Windows.Forms.Label
		{
			Left = 0,
			Top = 445,
			Width = panel.Width,
			Height = 85,
			Anchor = (AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right),
			Text = "TUDO EM UM SÓ LUGAR\r\nNovos recursos passam a abrir como páginas dentro do RoadBETS2 Telemetria, sem precisar criar outro aplicativo.",
			ForeColor = System.Drawing.Color.FromArgb(122, 164, 210),
			Font = new Font("Segoe UI Semibold", 11f)
		});
		return panel;
	}

	private System.Windows.Forms.Label StatusCard041(TableLayoutPanel grid, int col, string title, string text)
	{
		System.Windows.Forms.Panel panel = new System.Windows.Forms.Panel
		{
			Dock = DockStyle.Fill,
			Margin = new Padding(6),
			Padding = new Padding(15),
			BackColor = System.Drawing.Color.FromArgb(6, 23, 41)
		};
		panel.Controls.Add(new System.Windows.Forms.Label
		{
			Text = title,
			Dock = DockStyle.Top,
			Height = 35,
			ForeColor = System.Drawing.Color.FromArgb(89, 166, 255),
			Font = new Font("Segoe UI Semibold", 9f, System.Drawing.FontStyle.Bold)
		});
		System.Windows.Forms.Label label = new System.Windows.Forms.Label
		{
			Text = text,
			Dock = DockStyle.Fill,
			ForeColor = System.Drawing.Color.Gainsboro,
			Font = new Font("Segoe UI Semibold", 10f),
			TextAlign = ContentAlignment.MiddleLeft
		};
		panel.Controls.Add(label);
		grid.Controls.Add(panel, col, 0);
		return label;
	}

	private void Module041(FlowLayoutPanel p, string title, string sub, string page)
	{
		System.Windows.Forms.Button button = HubButton041(title + "\r\n" + sub, 205);
		button.Height = 105;
		button.TextAlign = ContentAlignment.MiddleLeft;
		button.Padding = new Padding(14, 0, 4, 0);
		button.Click += delegate
		{
			ShowHubPage041(page);
		};
		p.Controls.Add(button);
	}

	private System.Windows.Forms.Panel Dash041()
	{
		System.Windows.Forms.Panel panel = Page041();
		panel.Controls.Add(Head041("DASHBOARD", "ROADBETS2 TELEMETRIA • escolha um dos overlays."));
		System.Windows.Forms.Panel panel2 = new System.Windows.Forms.Panel
		{
			Left = 0,
			Top = 62,
			Width = panel.Width,
			Height = 58,
			Anchor = (AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right),
			BackColor = System.Drawing.Color.FromArgb(5, 24, 17)
		};
		System.Windows.Forms.Button button = HubButton041("ROADBETS2 DASH", 190);
		button.Left = 10;
		button.Top = 9;
		button.Height = 40;
		button.Click += delegate
		{
			ToggleRoadLifeOverlay();
		};
		panel2.Controls.Add(button);
		System.Windows.Forms.Button button2 = HubButton041("SOBREPOR VÍDEO", 175);
		button2.Left = 212;
		button2.Top = 9;
		button2.Height = 40;
		button2.Click += delegate
		{
			OpenVideoOverlay041();
		};
		panel2.Controls.Add(button2);
		panel.Controls.Add(panel2);
		System.Windows.Forms.Label value = new System.Windows.Forms.Label
		{
			Left = 12,
			Top = 145,
			Width = 760,
			Height = 62,
			Text = "RoadBETS2 Dash mostra os dados recebidos pelo RoadBETS2 Telemetria.\r\nA sobreposição de vídeo abre em uma janela flutuante.",
			ForeColor = System.Drawing.Color.FromArgb(176, 205, 188),
			Font = new Font("Segoe UI", 10.5f)
		};
		panel.Controls.Add(value);
		return panel;
	}

	private System.Windows.Forms.Panel Radio041()
	{
		System.Windows.Forms.Panel panel = Page041();
		panel.Controls.Add(Head041("RÁDIO ROADBETS2", "Rádio RoadBETS2 integrada ao aplicativo e pelo overlay de vídeo."));
		_hubRadioHost041 = new System.Windows.Forms.Panel
		{
			Left = 0,
			Top = 55,
			Width = panel.Width,
			Height = Math.Max(350, panel.Height - 55),
			Anchor = (AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right),
			BackColor = System.Drawing.Color.FromArgb(4, 13, 25)
		};
		panel.Controls.Add(_hubRadioHost041);
		return panel;
	}

	private System.Windows.Forms.Panel Server041()
	{
		System.Windows.Forms.Panel panel = Page041();
		panel.Controls.Add(Head041("COMBOIO / SERVIDOR", "Servidor continua opcional e usa a configuração que já existe."));
		if (_hubServerCard041 != null)
		{
			_hubServerCard041.Left = 0;
			_hubServerCard041.Top = 65;
			_hubServerCard041.Width = panel.Width;
			_hubServerCard041.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
			panel.Controls.Add(_hubServerCard041);
		}
		return panel;
	}

	private System.Windows.Forms.Panel Settings041()
	{
		System.Windows.Forms.Panel panel = Page041();
		panel.Controls.Add(Head041("CONFIGURAÇÕES", "Conta RoadBETS2 e preferências. Novas opções de voz entram aqui depois."));
		if (_hubAccountCard041 != null)
		{
			_hubAccountCard041.Left = 0;
			_hubAccountCard041.Top = 65;
			_hubAccountCard041.Width = panel.Width;
			_hubAccountCard041.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
			panel.Controls.Add(_hubAccountCard041);
		}
		BuildVoiceCleanSettings(panel);
		return panel;
	}

	private System.Windows.Forms.Panel Updates041()
	{
		System.Windows.Forms.Panel panel = Simple041("ATUALIZAÇÕES", "Verifique novas versões do RoadBETS2 Telemetria sem sair do aplicativo.");
		System.Windows.Forms.Button button = HubButton041("VERIFICAR ATUALIZAÇÃO", 230);
		button.Left = 0;
		button.Top = 100;
		button.Click += async delegate
		{
			await UpdateClickedAsync();
		};
		panel.Controls.Add(button);
		return panel;
	}

	private System.Windows.Forms.Panel Simple041(string title, string sub)
	{
		System.Windows.Forms.Panel panel = Page041();
		panel.Controls.Add(Head041(title, sub));
		return panel;
	}

	private System.Windows.Forms.Panel Page041()
	{
		return new System.Windows.Forms.Panel
		{
			BackColor = System.Drawing.Color.FromArgb(3, 11, 22),
			AutoScroll = true,
			AutoScrollMinSize = new System.Drawing.Size(680, 400)
		};
	}

	private System.Windows.Forms.Label Head041(string title, string sub)
	{
		return new System.Windows.Forms.Label
		{
			Text = title + "\r\n" + sub,
			Left = 0,
			Top = 0,
			Width = 900,
			Height = 52,
			ForeColor = System.Drawing.Color.White,
			Font = new Font("Segoe UI Semibold", 12f, System.Drawing.FontStyle.Bold)
		};
	}

	private void SyncHub041()
	{
		if (_hAccount041 != null)
		{
			_hAccount041.Text = (AccountReady ? (_accountUser + "\r\nConta conectada") : "Conta não conectada\r\nAbra Configurações");
		}
		if (_hEts041 != null)
		{
			_hEts041.Text = Safe041(lblTruck, "TruckSim GPS: aguardando");
		}
		if (_hCentral041 != null)
		{
			_hCentral041.Text = Safe041(lblTelemetry, "Central RoadBETS2: aguardando");
		}
		if (_hServer041 != null)
		{
			_hServer041.Text = Safe041(lblServer, "Servidor: opcional") + "\r\n" + Safe041(lblRoom, "Sala: -");
		}
	}

	private static string Safe041(System.Windows.Forms.Label l, string fallback)
	{
		try
		{
			return (l != null && !string.IsNullOrWhiteSpace(l.Text)) ? l.Text : fallback;
		}
		catch
		{
			return fallback;
		}
	}

	private void EnsureRadio041()
	{
		if (_hubRadioHost041 == null)
		{
			return;
		}
		if (_hubRadio041 != null && !_hubRadio041.IsDisposed)
		{
			_hubRadio041.ConfigureAccount049(_accountUser, _accountToken);
			return;
		}
		try
		{
			_hubRadio041 = new RadioForm
			{
				TopLevel = false,
				FormBorderStyle = FormBorderStyle.None,
				Dock = DockStyle.Fill,
				TopMost = false,
				ShowInTaskbar = true
			};
			_hubRadioHost041.Controls.Clear();
			_hubRadioHost041.Controls.Add(_hubRadio041);
			_hubRadio041.Show();
			_hubRadio041.ConfigureAccount049(_accountUser, _accountToken);
		}
		catch (Exception ex)
		{
			_hubRadioHost041.Controls.Clear();
			_hubRadioHost041.Controls.Add(new System.Windows.Forms.Label
			{
				Dock = DockStyle.Fill,
				Text = "Rádio RoadBETS2 indisponível.\r\n" + ex.Message,
				TextAlign = ContentAlignment.MiddleCenter,
				ForeColor = System.Drawing.Color.OrangeRed
			});
		}
	}

	private async Task InitDash041()
	{
		if (_hubDashReady041 || _hubDash041 == null)
		{
			return;
		}
		try
		{
			string www = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "gatdash-www");
			if (!File.Exists(System.IO.Path.Combine(www, "index.html")))
			{
				_hubDash041.Visible = false;
				return;
			}
			string text = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GAT-LOG", "GAT-Telemetria", "DashWebView2-1.0.41");
			Directory.CreateDirectory(text);
			CoreWebView2Environment environment = await CoreWebView2Environment.CreateAsync(null, text);
			await _hubDash041.EnsureCoreWebView2Async(environment);
			_hubDash041.CoreWebView2.Settings.AreDevToolsEnabled = false;
			_hubDash041.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
			_hubDash041.CoreWebView2.SetVirtualHostNameToFolderMapping("gatdash.local", www, CoreWebView2HostResourceAccessKind.Allow);
			_hubDash041.CoreWebView2.WebMessageReceived += DashMessage041;
			_hubDash041.Source = new Uri("https://gatdash.local/index.html?hub=1041");
			_hubDashReady041 = true;
			_hubDashTimer041.Start();
		}
		catch
		{
			_hubDash041.Visible = false;
		}
	}

	private async void DashMessage041(object sender, CoreWebView2WebMessageReceivedEventArgs e)
	{
		_ = 5;
		try
		{
			JObject jObject = JObject.Parse(e.WebMessageAsJson);
			switch (Convert.ToString(jObject["type"]) ?? "")
			{
			case "ready":
				await DashJs041("window.gatDashNativeReady('windows'," + DashSettingsJson045() + ")");
				await DashSession041();
				break;
			case "login":
				await DashSession041();
				break;
			case "mediaRefresh":
				await DashMedia041();
				break;
			case "mediaMode":
				SaveMediaMode041(Convert.ToString(jObject["mode"]));
				try
				{
					_hubRadio041?.HubSyncMode042();
				}
				catch
				{
				}
				await DashMedia041();
				break;
			case "setSettings":
				SaveDashSettings045(jObject);
				await DashJs041("window.gatDashSettings(" + DashSettingsJson045() + ")");
				break;
			case "speak":
				break;
			case "stopSpeech":
				break;
			}
		}
		catch
		{
		}
	}

	private async Task DashSession041()
	{
		if (DashTargetReady060())
		{
			if (!AccountReady)
			{
				await DashJs041("window.gatDashLoginTransportError('Entre primeiro na Conta RoadBETS2 em Configurações.')");
				return;
			}
			JObject jObject = new JObject
			{
				["ok"] = true,
				["user"] = _accountUser,
				["role"] = "driver"
			};
			await DashJs041("window.gatDashLoginResult(" + jObject.ToString(Newtonsoft.Json.Formatting.None) + ")");
			await DashMedia041();
		}
	}

	private async Task PollDash041()
	{
		if (!DashTargetReady060() || _hubDashBusy041)
		{
			return;
		}
		_hubDashBusy041 = true;
		try
		{
			await DashJs041("window.gatDashPushTelemetry(" + JsonConvert.SerializeObject(await _hubHttp041.GetStringAsync("http://127.0.0.1:31377/api/ets2/telemetry")) + ")");
		}
		catch (Exception ex)
		{
			await DashJs041("window.gatDashTelemetryError(" + JsonConvert.SerializeObject((ex.Message.Length > 80) ? ex.Message.Substring(0, 80) : ex.Message) + ")");
		}
		finally
		{
			_hubDashBusy041 = false;
		}
	}

	private async Task DashMedia041()
	{
		try
		{
			await DashJs041("window.gatDashPushMedia(" + JsonConvert.SerializeObject(await _hubHttp041.GetStringAsync("http://127.0.0.1:31378/api/gat/media?t=" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())) + ")");
		}
		catch
		{
			await DashJs041("window.gatDashMediaError('Rádio RoadBETS2 indisponível')");
		}
	}

	private async Task DashJs041(string js)
	{
		try
		{
			if (_hubDashReady041 && _hubDash041?.CoreWebView2 != null)
			{
				await _hubDash041.CoreWebView2.ExecuteScriptAsync(js);
			}
		}
		catch
		{
		}
		try
		{
			if (_dashOverlay060 != null && !_dashOverlay060.IsDisposed && _dashOverlay060.IsReady)
			{
				await _dashOverlay060.ExecuteScriptAsync(js);
			}
		}
		catch
		{
		}
	}

	private static void SaveMediaMode041(string mode)
	{
		try
		{
			mode = (mode ?? "").Trim().ToLowerInvariant();
			switch (mode)
			{
			case "gat":
			case "mine":
				File.WriteAllText(System.IO.Path.Combine(System.Windows.Forms.Application.LocalUserAppDataPath, "radio-active-mode.txt"), mode);
				break;
			case "web":
				File.WriteAllText(System.IO.Path.Combine(System.Windows.Forms.Application.LocalUserAppDataPath, "radio-active-mode.txt"), "gat");
				break;
			}
		}
		catch
		{
		}
	}

	private bool DashTargetReady060()
	{
		bool num = _hubDashReady041 && _hubDash041 != null && !_hubDash041.IsDisposed && _hubDash041.CoreWebView2 != null;
		bool flag = _dashOverlay060 != null && !_dashOverlay060.IsDisposed && _dashOverlay060.IsReady;
		return num | flag;
	}

	private async Task OpenDashOverlay060()
	{
		_ = 2;
		try
		{
			if (_dashOverlay060 == null || _dashOverlay060.IsDisposed)
			{
				_dashOverlay060 = new DashOverlay060(DashMessage041);
				_dashOverlay060.FormClosed += delegate
				{
					_dashOverlay060 = null;
					_hubDashReady041 = _hubDash041 != null && !_hubDash041.IsDisposed && _hubDash041.CoreWebView2 != null;
					if (!_hubDashReady041)
					{
						_hubDashTimer041.Stop();
					}
				};
			}
			if (!_dashOverlay060.Visible)
			{
				_dashOverlay060.Show();
			}
			await _dashOverlay060.InitializeAsync();
			_hubDashReady041 = true;
			_hubDashTimer041.Start();
			ApplyOverlayOpacity041();
			_dashOverlay060.BringToFront();
			await DashSession041();
			await PollDash041();
		}
		catch (Exception ex)
		{
			System.Windows.Forms.MessageBox.Show("Não foi possível abrir o ROADBETS2 DASH completo.\r\n" + ex.Message, "ROADBETS2 DASH", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
		}
	}

	private void OpenTruckOverlay041()
	{
		if (_truckOverlay041 == null || _truckOverlay041.IsDisposed)
		{
			_truckOverlay041 = new TruckOverlay041();
			_truckOverlay041.FormClosed += delegate
			{
				_truckOverlay041 = null;
			};
		}
		ApplyOverlayOpacity041();
		_truckOverlay041.TopMost = true;
		_truckOverlay041.ShowInTaskbar = true;
		if (!_truckOverlay041.Visible)
		{
			_truckOverlay041.Show();
		}
		_truckOverlay041.BringToFront();
	}

	private void OpenVideoOverlay041()
	{
		if (_videoOverlay041 == null || _videoOverlay041.IsDisposed)
		{
			_videoOverlay041 = new VideoOverlay041();
			_videoOverlay041.FormClosed += delegate
			{
				_videoOverlay041 = null;
			};
		}
		ApplyOverlayOpacity041();
		_videoOverlay041.TopMost = true;
		_videoOverlay041.ShowInTaskbar = true;
		if (!_videoOverlay041.Visible)
		{
			_videoOverlay041.Show();
		}
		_videoOverlay041.BringToFront();
	}

	private void CloseOverlays041()
	{
		try
		{
			_truckOverlay041?.Close();
		}
		catch
		{
		}
		try
		{
			_videoOverlay041?.Close();
		}
		catch
		{
		}
	}

	private void ApplyOverlayOpacity041()
	{
		double opacity = (double)((_overlayOpacity041 == null) ? 94 : _overlayOpacity041.Value) / 100.0;
		try
		{
			if (_truckOverlay041 != null && !_truckOverlay041.IsDisposed)
			{
				_truckOverlay041.Opacity = opacity;
			}
		}
		catch
		{
		}
		try
		{
			if (_videoOverlay041 != null && !_videoOverlay041.IsDisposed)
			{
				_videoOverlay041.Opacity = opacity;
			}
		}
		catch
		{
		}
		try
		{
			if (_dashOverlay060 != null && !_dashOverlay060.IsDisposed)
			{
				_dashOverlay060.Opacity = opacity;
			}
		}
		catch
		{
		}
	}

	private void ApplyHub042()
	{
		if (_hub042Applied)
		{
			return;
		}
		_hub042Applied = true;
		Text = "ROADBETS2 TELEMETRIA BETA 1.0.44";
		RebuildHome042();
		ImproveDash042();
		WireMediaRouting042();
		_hubStatusTimer041.Tick += delegate
		{
			SyncHome042();
			if (_hubCurrentPage042 == "dash" && _hubDashReady041 && _hubDash041 != null && _hubDash041.ClientSize != _lastDashSize042)
			{
				FitDash042();
			}
			try
			{
				_hubRadio041?.HubSyncMode042();
			}
			catch
			{
			}
		};
		Shown += async delegate
		{
			SyncHome042();
			await Task.Delay(700);
			await SetDashMediaActive042(active: false);
			await FitDash042();
		};
	}

	private System.Windows.Forms.Panel Card042(string title)
	{
		System.Windows.Forms.Panel p = new System.Windows.Forms.Panel
		{
			Dock = DockStyle.Fill,
			Margin = new Padding(6),
			Padding = new Padding(16),
			BackColor = System.Drawing.Color.FromArgb(6, 23, 41)
		};
		p.Paint += (object sender, PaintEventArgs e) =>
		{
			using System.Drawing.Pen pen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(26, 91, 145), 1f);
			e.Graphics.DrawRectangle(pen, 0, 0, Math.Max(1, p.Width - 1), Math.Max(1, p.Height - 1));
		};
		p.Controls.Add(new System.Windows.Forms.Label
		{
			Text = title,
			Dock = DockStyle.Top,
			Height = 32,
			ForeColor = System.Drawing.Color.FromArgb(61, 174, 255),
			Font = new Font("Segoe UI Semibold", 9.5f, System.Drawing.FontStyle.Bold)
		});
		return p;
	}

	private void RebuildHome042()
	{
		if (!_hubPages041.TryGetValue("home", out var value) || value == null)
		{
			return;
		}
		value.Controls.Clear();
		value.Padding = Padding.Empty;
		TableLayoutPanel tableLayoutPanel = new TableLayoutPanel
		{
			Dock = DockStyle.Fill,
			ColumnCount = 1,
			RowCount = 3,
			Margin = Padding.Empty,
			Padding = Padding.Empty,
			BackColor = System.Drawing.Color.FromArgb(3, 11, 22)
		};
		tableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
		tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 58f));
		tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 270f));
		tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
		value.Controls.Add(tableLayoutPanel);
		tableLayoutPanel.Controls.Add(new System.Windows.Forms.Label
		{
			Dock = DockStyle.Fill,
			Text = "INÍCIO\r\nSeu perfil, sua viagem e o ecossistema ROADBETS2 em um só lugar.",
			ForeColor = System.Drawing.Color.White,
			Font = new Font("Segoe UI Semibold", 12f, System.Drawing.FontStyle.Bold),
			TextAlign = ContentAlignment.MiddleLeft
		}, 0, 0);
		TableLayoutPanel tableLayoutPanel2 = new TableLayoutPanel
		{
			Dock = DockStyle.Fill,
			ColumnCount = 3,
			RowCount = 1,
			Margin = Padding.Empty,
			Padding = Padding.Empty
		};
		tableLayoutPanel2.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 31f));
		tableLayoutPanel2.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38f));
		tableLayoutPanel2.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 31f));
		tableLayoutPanel.Controls.Add(tableLayoutPanel2, 0, 1);
		System.Windows.Forms.Panel panel = Card042("PERFIL DO MOTORISTA");
		_avatar042 = new DriverAvatar042
		{
			Left = 16,
			Top = 54,
			Width = 86,
			Height = 86
		};
		_profileName042 = new System.Windows.Forms.Label
		{
			Left = 118,
			Top = 57,
			Width = 220,
			Height = 32,
			ForeColor = System.Drawing.Color.White,
			Font = new Font("Segoe UI Semibold", 16f, System.Drawing.FontStyle.Bold),
			Text = "Motorista GAT"
		};
		_profileMeta042 = new System.Windows.Forms.Label
		{
			Left = 118,
			Top = 94,
			Width = 230,
			Height = 88,
			ForeColor = System.Drawing.Color.FromArgb(159, 187, 214),
			Font = new Font("Segoe UI", 9.5f),
			Text = "Conta RoadBETS2 aguardando..."
		};
		panel.Controls.Add(_avatar042);
		panel.Controls.Add(_profileName042);
		panel.Controls.Add(_profileMeta042);
		panel.Controls.Add(new System.Windows.Forms.Label
		{
			Left = 16,
			Top = 157,
			Width = 92,
			Height = 27,
			Text = "ROADBETS2",
			TextAlign = ContentAlignment.MiddleCenter,
			BackColor = System.Drawing.Color.FromArgb(10, 76, 130),
			ForeColor = System.Drawing.Color.FromArgb(177, 224, 255),
			Font = new Font("Segoe UI Semibold", 8.5f, System.Drawing.FontStyle.Bold)
		});
		tableLayoutPanel2.Controls.Add(panel, 0, 0);
		System.Windows.Forms.Panel panel2 = Card042("ETS2 / VIAGEM ATUAL");
		_trip042 = new System.Windows.Forms.Label
		{
			Dock = DockStyle.Fill,
			Padding = new Padding(0, 8, 0, 0),
			ForeColor = System.Drawing.Color.Gainsboro,
			Font = new Font("Segoe UI Semibold", 10f),
			Text = "Aguardando telemetria do ETS2..."
		};
		panel2.Controls.Add(_trip042);
		tableLayoutPanel2.Controls.Add(panel2, 1, 0);
		System.Windows.Forms.Panel panel3 = Card042("RÁDIO / TV ROADBETS2 • TOCANDO AGORA");
		_radio042 = new System.Windows.Forms.Label
		{
			Dock = DockStyle.Fill,
			Padding = new Padding(0, 8, 0, 0),
			ForeColor = System.Drawing.Color.Gainsboro,
			Font = new Font("Segoe UI Semibold", 10f),
			Text = "Canal RoadBETS2\r\nAguardando mídia..."
		};
		panel3.Controls.Add(_radio042);
		tableLayoutPanel2.Controls.Add(panel3, 2, 0);
		System.Windows.Forms.Panel banner = new System.Windows.Forms.Panel
		{
			Dock = DockStyle.Fill,
			Margin = new Padding(6, 14, 6, 6),
			BackColor = System.Drawing.Color.FromArgb(4, 18, 33)
		};
		banner.Paint += (object sender, PaintEventArgs e) =>
		{
			using System.Drawing.Pen pen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(23, 92, 148), 1f);
			e.Graphics.DrawRectangle(pen, 0, 0, Math.Max(1, banner.Width - 1), Math.Max(1, banner.Height - 1));
		};
		banner.Controls.Add(new System.Windows.Forms.Label
		{
			Text = "ROADBETS2 ETS2\r\nCONEXÃO QUE MOVE DISTÂNCIAS",
			Left = 28,
			Top = 36,
			Width = 500,
			Height = 86,
			ForeColor = System.Drawing.Color.White,
			Font = new Font("Segoe UI Semibold", 20f, System.Drawing.FontStyle.Bold)
		});
		banner.Controls.Add(new System.Windows.Forms.Label
		{
			Text = "Telemetria • comunidade • rádio • dashboard • GPS • comboios\r\nTudo integrado no mesmo ROADBETS2 TELEMETRIA.",
			Left = 30,
			Top = 132,
			Width = 540,
			Height = 50,
			ForeColor = System.Drawing.Color.FromArgb(112, 171, 220),
			Font = new Font("Segoe UI", 10f)
		});
		TruckOutline truck = new TruckOutline
		{
			Width = 330,
			Height = 130,
			Top = 28,
			Anchor = (AnchorStyles.Top | AnchorStyles.Right)
		};
		System.Windows.Forms.Label gatTruck = new System.Windows.Forms.Label
		{
			Text = "ROADBETS2",
			Width = 150,
			Height = 34,
			Top = 150,
			TextAlign = ContentAlignment.MiddleCenter,
			Anchor = (AnchorStyles.Top | AnchorStyles.Right),
			ForeColor = System.Drawing.Color.FromArgb(70, 182, 255),
			Font = new Font("Segoe UI Black", 16f, System.Drawing.FontStyle.Bold | System.Drawing.FontStyle.Italic)
		};
		_connect042 = new System.Windows.Forms.Label
		{
			Width = 300,
			Height = 62,
			Top = 52,
			TextAlign = ContentAlignment.MiddleRight,
			Anchor = (AnchorStyles.Top | AnchorStyles.Right),
			ForeColor = System.Drawing.Color.FromArgb(143, 180, 214),
			Font = new Font("Segoe UI Semibold", 9.5f)
		};
		banner.Controls.Add(truck);
		banner.Controls.Add(gatTruck);
		banner.Controls.Add(_connect042);
		banner.Resize += delegate
		{
			truck.Left = Math.Max(560, banner.ClientSize.Width - 360);
			gatTruck.Left = Math.Max(640, banner.ClientSize.Width - 270);
			_connect042.Left = Math.Max(500, banner.ClientSize.Width - 330);
		};
		tableLayoutPanel.Controls.Add(banner, 0, 2);
	}

	private void SyncHome042()
	{
		try
		{
			string text = ((!string.IsNullOrWhiteSpace(_accountUser)) ? _accountUser : "Motorista GAT");
			string text2 = ((!string.IsNullOrWhiteSpace(_driver)) ? _driver : text);
			if (_profileName042 != null)
			{
				_profileName042.Text = text2;
			}
			if (_avatar042 != null)
			{
				_avatar042.Initials = Initials042(text2);
				_avatar042.Invalidate();
			}
			if (_profileMeta042 != null)
			{
				_profileMeta042.Text = (AccountReady ? ("@" + text + "\r\nConta RoadBETS2 conectada\r\nPC vinculado a esta instalação") : "Conta RoadBETS2 não conectada\r\nAbra Configurações para entrar.");
			}
			if (_trip042 != null)
			{
				_trip042.Text = Safe041(lblTruck, "TruckSim GPS: aguardando") + "\r\n\r\n" + Safe041(lblCargo, "Carga: Sem carga") + "\r\n" + Safe041(lblRoute, "Rota: -") + "\r\n" + Safe041(lblDistance, "Restante: -") + "\r\n" + Safe041(lblSpeed, "Velocidade: 0 km/h");
			}
			if (_radio042 != null)
			{
				string text3 = ReadMediaMode042();
				string text4;
				if (text3 == "mine")
				{
					text4 = "MEU VÍDEO";
				}
				else
				{
					text4 = ((text3 == "web") ? "CANAL WEB" : "CANAL ROADBETS2");
				}
				string text5 = "";
				try
				{
					if (_hubRadio041 != null && !_hubRadio041.IsDisposed)
					{
						text5 = _hubRadio041.HubNowPlaying042;
					}
				}
				catch
				{
				}
				if (string.IsNullOrWhiteSpace(text5) || text5.EndsWith("—"))
				{
					text5 = "Fonte selecionada: " + text4;
				}
				_radio042.Text = text4 + "\r\n\r\n" + text5 + "\r\n\r\nSom único: ao mudar de página, o player anterior é pausado.";
			}
			if (_connect042 != null)
			{
				_connect042.Text = Safe041(lblTelemetry, "Central RoadBETS2: aguardando") + "\r\n" + Safe041(lblServer, "Servidor: opcional") + "\r\nCliente 1.0.44 TESTE";
			}
		}
		catch
		{
		}
	}

	private static string Initials042(string text)
	{
		if (string.IsNullOrWhiteSpace(text))
		{
			return "GAT";
		}
		string[] array = text.Trim().Split(new char[3] { ' ', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
		if (array.Length >= 2)
		{
			return (array[0].Substring(0, 1) + array[array.Length - 1].Substring(0, 1)).ToUpperInvariant();
		}
		string text2 = ((array.Length == 0) ? text.Trim() : array[0]);
		if (text2.Length < 2)
		{
			return text2.ToUpperInvariant();
		}
		return text2.Substring(0, 2).ToUpperInvariant();
	}

	private static string ReadMediaMode042()
	{
		try
		{
			string path = System.IO.Path.Combine(System.Windows.Forms.Application.LocalUserAppDataPath, "radio-active-mode.txt");
			if (!File.Exists(path))
			{
				return "gat";
			}
			string text = (File.ReadAllText(path) ?? "").Trim().ToLowerInvariant();
			return (text == "mine" || text == "web") ? text : "gat";
		}
		catch
		{
			return "gat";
		}
	}

	private void ImproveDash042()
	{
		if (!_hubPages041.TryGetValue("dash", out var page) || page == null || _hubDash041 == null)
		{
			return;
		}
		foreach (System.Windows.Forms.Control control2 in page.Controls)
		{
			if (!(control2 is System.Windows.Forms.Panel { Top: >=50, Top: <=70 } panel))
			{
				continue;
			}
			panel.Top = 50;
			panel.Height = 46;
			foreach (System.Windows.Forms.Control control3 in panel.Controls)
			{
				if (control3 is System.Windows.Forms.Button)
				{
					control3.Top = 5;
					control3.Height = 34;
				}
			}
		}
		_hubDash041.Top = 102;
		_hubDash041.Height = Math.Max(280, page.ClientSize.Height - 102);
		page.Resize += delegate
		{
			_hubDash041.Height = Math.Max(280, page.ClientSize.Height - 102);
			if (_hubCurrentPage042 == "dash")
			{
				FitDash042();
			}
		};
	}

	private async Task FitDash042()
	{
		if (_hubDashReady041 && _hubDash041 != null && _hubDash041.CoreWebView2 != null)
		{
			int num = Math.Max(320, _hubDash041.ClientSize.Height);
			double num2 = Math.Max(0.66, Math.Min(1.0, (double)num / 700.0));
			double num3 = 100.0 / num2;
			string text = num2.ToString("0.000", CultureInfo.InvariantCulture);
			string text2 = num3.ToString("0.0", CultureInfo.InvariantCulture);
			await DashJs041("document.documentElement.style.overflow='hidden';document.body.style.zoom='" + text + "';document.body.style.width='" + text2 + "%';document.body.style.height='" + text2 + "%';");
			_lastDashSize042 = _hubDash041.ClientSize;
		}
	}

	private void WireMediaRouting042()
	{
		foreach (KeyValuePair<string, System.Windows.Forms.Button> item in _hubNav041.ToList())
		{
			string key = item.Key;
			item.Value.Click += async delegate
			{
				_hubCurrentPage042 = key;
				await RouteMedia042(key);
				if (key == "dash")
				{
					await FitDash042();
				}
			};
		}
		if (!_hubPages041.TryGetValue("dash", out var value) || value == null)
		{
			return;
		}
		foreach (System.Windows.Forms.Control control in value.Controls)
		{
			foreach (System.Windows.Forms.Button item2 in control.Controls.OfType<System.Windows.Forms.Button>())
			{
				if (!string.Equals(item2.Text, "SOBREPOR VÍDEO", StringComparison.OrdinalIgnoreCase))
				{
					continue;
				}
				item2.Click += async delegate
				{
					try
					{
						_hubRadio041?.HubPause042();
					}
					catch
					{
					}
					await SetDashMediaActive042(active: false);
					if (_videoOverlay041 != null && !_videoOverlay041.IsDisposed)
					{
						_videoOverlay041.FormClosed += async delegate
						{
							if (_hubCurrentPage042 == "dash")
							{
								await SetDashMediaActive042(active: true);
							}
						};
					}
				};
			}
		}
	}

	private async Task RouteMedia042(string key)
	{
		if (key == "dash")
		{
			try
			{
				_hubRadio041?.HubPause042();
			}
			catch
			{
			}
			await SetDashMediaActive042(active: true);
			return;
		}
		if (key == "radio")
		{
			await SetDashMediaActive042(active: false);
			try
			{
				_hubRadio041?.HubSyncMode042();
				return;
			}
			catch
			{
				return;
			}
		}
		try
		{
			_hubRadio041?.HubPause042();
		}
		catch
		{
		}
		await SetDashMediaActive042(active: false);
	}

	private async Task SetDashMediaActive042(bool active)
	{
		if (_hubDashReady041)
		{
			await DashJs041("if(window.gatDashSetMediaActive042)window.gatDashSetMediaActive042(" + (active ? "true" : "false") + ");");
		}
	}

	private void ApplyHub044()
	{
		if (_hub044Applied)
		{
			return;
		}
		_hub044Applied = true;
		Text = "ROADBETS2 TELEMETRIA BETA 1.0.50";
		MinimumSize = new System.Drawing.Size(1100, 720);
		if (Width < 1240 || Height < 820)
		{
			Size = new System.Drawing.Size(1280, 840);
		}
		StyleHub044();
		BuildHome044();
		ReplaceTextRecursive044(this, "Cliente 1.0.43 TESTE", "Cliente 1.0.50 TESTE");
		ReplaceTextRecursive044(this, "Central principal do ecossistema ROADBETS2 ETS2 • tudo em um só lugar", "Conectando motoristas, estradas e amizades • ROADBETS2 ETS2");
		_hubStatusTimer041.Tick += delegate
		{
			SyncHome044();
		};
		Shown += delegate
		{
			SyncHome044();
		};
		FormClosed += delegate
		{
			try
			{
				_homeHeroImage044?.Dispose();
			}
			catch
			{
			}
		};
	}

	private void StyleHub044()
	{
		foreach (KeyValuePair<string, System.Windows.Forms.Button> item in _hubNav041)
		{
			System.Windows.Forms.Button value = item.Value;
			if (value != null)
			{
				value.FlatStyle = FlatStyle.Flat;
				value.FlatAppearance.BorderSize = 0;
				value.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(14, 64, 39);
				value.FlatAppearance.MouseDownBackColor = System.Drawing.Color.FromArgb(15, 91, 51);
				value.Height = 38;
				value.Font = new Font("Segoe UI Semibold", 9.2f, System.Drawing.FontStyle.Bold);
				value.ForeColor = System.Drawing.Color.FromArgb(218, 233, 248);
				value.Cursor = System.Windows.Forms.Cursors.Hand;
			}
		}
	}

	private void BuildHome044()
	{
		if (!_hubPages041.TryGetValue("home", out var value) || value == null)
		{
			return;
		}
		value.SuspendLayout();
		value.Controls.Clear();
		value.Padding = Padding.Empty;
		value.BackColor = System.Drawing.Color.FromArgb(5, 17, 12);
		try
		{
			string text = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "gat-home-hero-1.0.44.jpg");
			if (File.Exists(text))
			{
				using System.Drawing.Image original = System.Drawing.Image.FromFile(text);
				_homeHeroImage044 = new Bitmap(original);
			}
		}
		catch
		{
			_homeHeroImage044 = null;
		}
		TableLayoutPanel tableLayoutPanel = new TableLayoutPanel
		{
			Dock = DockStyle.Fill,
			ColumnCount = 1,
			RowCount = 2,
			Margin = Padding.Empty,
			Padding = Padding.Empty,
			BackColor = value.BackColor
		};
		tableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
		tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 244f));
		tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
		value.Controls.Add(tableLayoutPanel);
		TableLayoutPanel tableLayoutPanel2 = new TableLayoutPanel
		{
			Dock = DockStyle.Fill,
			ColumnCount = 3,
			RowCount = 1,
			Margin = Padding.Empty,
			Padding = Padding.Empty,
			BackColor = value.BackColor
		};
		tableLayoutPanel2.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 31f));
		tableLayoutPanel2.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38f));
		tableLayoutPanel2.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 31f));
		tableLayoutPanel.Controls.Add(tableLayoutPanel2, 0, 0);
		GlassCard044 glassCard = new GlassCard044
		{
			Caption = "PERFIL DO MOTORISTA",
			Dock = DockStyle.Fill
		};
		_homeAvatar044 = new DriverAvatar044
		{
			Left = 18,
			Top = 61,
			Width = 92,
			Height = 92
		};
		_homeProfileName044 = new System.Windows.Forms.Label
		{
			Left = 126,
			Top = 62,
			Width = 260,
			Height = 30,
			Text = "Motorista GAT",
			ForeColor = System.Drawing.Color.White,
			Font = new Font("Segoe UI Semibold", 15f, System.Drawing.FontStyle.Bold)
		};
		_homeProfileMeta044 = new System.Windows.Forms.Label
		{
			Left = 126,
			Top = 98,
			Width = 270,
			Height = 105,
			Text = "Conta RoadBETS2 aguardando...",
			ForeColor = System.Drawing.Color.FromArgb(161, 193, 222),
			Font = new Font("Segoe UI", 9.5f)
		};
		System.Windows.Forms.Label value2 = new System.Windows.Forms.Label
		{
			Left = 18,
			Top = 165,
			Width = 92,
			Height = 28,
			Text = "MOTORISTA GAT",
			TextAlign = ContentAlignment.MiddleCenter,
			BackColor = System.Drawing.Color.FromArgb(8, 68, 38),
			ForeColor = System.Drawing.Color.FromArgb(135, 235, 171),
			Font = new Font("Segoe UI Semibold", 8f, System.Drawing.FontStyle.Bold)
		};
		glassCard.Controls.Add(_homeAvatar044);
		glassCard.Controls.Add(_homeProfileName044);
		glassCard.Controls.Add(_homeProfileMeta044);
		glassCard.Controls.Add(value2);
		tableLayoutPanel2.Controls.Add(glassCard, 0, 0);
		GlassCard044 glassCard2 = new GlassCard044
		{
			Caption = "VIAGEM ATUAL",
			Dock = DockStyle.Fill
		};
		_homeTrip044 = new System.Windows.Forms.Label
		{
			Dock = DockStyle.Fill,
			Text = "Aguardando telemetria do ETS2...",
			ForeColor = System.Drawing.Color.FromArgb(225, 236, 248),
			Font = new Font("Segoe UI Semibold", 10.2f),
			TextAlign = ContentAlignment.MiddleLeft,
			Padding = new Padding(4, 0, 4, 0)
		};
		glassCard2.Controls.Add(_homeTrip044);
		tableLayoutPanel2.Controls.Add(glassCard2, 1, 0);
		GlassCard044 glassCard3 = new GlassCard044
		{
			Caption = "RÁDIO ROADBETS2 • TOCANDO AGORA",
			Dock = DockStyle.Fill
		};
		_homeRadio044 = new System.Windows.Forms.Label
		{
			Dock = DockStyle.Fill,
			Text = "Canal RoadBETS2\r\nAguardando mídia...",
			ForeColor = System.Drawing.Color.FromArgb(225, 236, 248),
			Font = new Font("Segoe UI Semibold", 10.2f),
			TextAlign = ContentAlignment.MiddleLeft,
			Padding = new Padding(4, 0, 4, 0)
		};
		glassCard3.Controls.Add(_homeRadio044);
		tableLayoutPanel2.Controls.Add(glassCard3, 2, 0);
		HeroPanel044 hero = new HeroPanel044
		{
			Dock = DockStyle.Fill,
			Margin = new Padding(7, 4, 7, 7),
			HeroImage = _homeHeroImage044
		};
		tableLayoutPanel.Controls.Add(hero, 0, 1);
		_homeSystem044 = new System.Windows.Forms.Label
		{
			Width = 330,
			Height = 58,
			Top = 18,
			Anchor = (AnchorStyles.Top | AnchorStyles.Right),
			TextAlign = ContentAlignment.MiddleRight,
			ForeColor = System.Drawing.Color.FromArgb(154, 207, 244),
			BackColor = System.Drawing.Color.FromArgb(135, 2, 14, 26),
			Font = new Font("Segoe UI Semibold", 9f)
		};
		hero.Controls.Add(_homeSystem044);
		System.Windows.Forms.Button video = OverlayAction044("SOBREPOSIÇÃO DE VÍDEO\r\nVídeo flutuante sem barra branca", 222, 72);
		System.Windows.Forms.Button truck = OverlayAction044("SOBREPOSIÇÃO DO CAMINHÃO\r\nPainel compacto e redimensionável", 244, 72);
		video.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
		truck.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
		video.Click += delegate
		{
			OpenVideoOverlay041();
		};
		truck.Click += delegate
		{
			OpenTruckOverlay041();
		};
		hero.Controls.Add(video);
		hero.Controls.Add(truck);
		hero.Resize += delegate
		{
			_homeSystem044.Left = Math.Max(20, hero.ClientSize.Width - _homeSystem044.Width - 20);
			truck.Left = Math.Max(20, hero.ClientSize.Width - truck.Width - 20);
			truck.Top = Math.Max(20, hero.ClientSize.Height - truck.Height - 22);
			video.Left = Math.Max(20, truck.Left - video.Width - 12);
			video.Top = truck.Top;
		};
		value.ResumeLayout(performLayout: true);
		SyncHome044();
	}

	private System.Windows.Forms.Button OverlayAction044(string text, int width, int height)
	{
		System.Windows.Forms.Button button = new System.Windows.Forms.Button();
		button.Text = text;
		button.Width = width;
		button.Height = height;
		button.FlatStyle = FlatStyle.Flat;
		button.BackColor = System.Drawing.Color.FromArgb(220, 5, 27, 47);
		button.ForeColor = System.Drawing.Color.White;
		button.Font = new Font("Segoe UI Semibold", 9f, System.Drawing.FontStyle.Bold);
		button.TextAlign = ContentAlignment.MiddleLeft;
		button.Padding = new Padding(14, 0, 8, 0);
		button.Cursor = System.Windows.Forms.Cursors.Hand;
		button.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(42, 136, 80);
		button.FlatAppearance.BorderSize = 1;
		button.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(235, 8, 45, 28);
		return button;
	}

	private void SyncHome044()
	{
		try
		{
			string text = ((!string.IsNullOrWhiteSpace(_accountUser)) ? _accountUser : "Motorista GAT");
			string text2 = ((!string.IsNullOrWhiteSpace(_driver)) ? _driver : text);
			if (_homeProfileName044 != null)
			{
				_homeProfileName044.Text = text2;
			}
			if (_homeAvatar044 != null)
			{
				_homeAvatar044.Initials = Initials044(text2);
				_homeAvatar044.Invalidate();
			}
			if (_homeProfileMeta044 != null)
			{
				_homeProfileMeta044.Text = (AccountReady ? ("● Conta ativa\r\n● PC vinculado\r\n@" + text + "\r\nROADLIFE ETS2") : "○ Conta não conectada\r\n○ PC aguardando vínculo\r\nAbra Configurações para entrar.");
				_homeProfileMeta044.ForeColor = (AccountReady ? System.Drawing.Color.FromArgb(124, 231, 154) : System.Drawing.Color.FromArgb(198, 178, 132));
			}
			if (_homeTrip044 != null)
			{
				_homeTrip044.Text = Safe041(lblTruck, "TruckSim GPS: aguardando") + "\r\n\r\n" + Safe041(lblCargo, "Carga: Sem carga") + "\r\n" + Safe041(lblRoute, "Rota: -") + "\r\n" + Safe041(lblDistance, "Distância restante: -") + "\r\n" + Safe041(lblSpeed, "Velocidade: 0 km/h");
			}
			if (_homeRadio044 != null)
			{
				string text3 = ReadMediaMode042();
				string text4;
				if (text3 == "mine")
				{
					text4 = "MEU VÍDEO";
				}
				else
				{
					text4 = ((text3 == "web") ? "CANAL WEB" : "CANAL ROADBETS2");
				}
				string text5 = "";
				try
				{
					if (_hubRadio041 != null && !_hubRadio041.IsDisposed)
					{
						text5 = _hubRadio041.HubNowPlaying042;
					}
				}
				catch
				{
				}
				if (string.IsNullOrWhiteSpace(text5) || text5.EndsWith("—"))
				{
					text5 = "Fonte selecionada: " + text4;
				}
				_homeRadio044.Text = text4 + "\r\n\r\n" + text5 + "\r\n\r\nMídia compartilhada com o ROADBETS2 DASH e o overlay.";
			}
			if (_homeSystem044 != null)
			{
				_homeSystem044.Text = "● SISTEMA GAT\r\n" + Safe041(lblTelemetry, "Central RoadBETS2: aguardando") + "   •   " + Safe041(lblServer, "Servidor: opcional") + "\r\nCliente 1.0.50 TESTE";
			}
		}
		catch
		{
		}
	}

	private static string Initials044(string text)
	{
		if (string.IsNullOrWhiteSpace(text))
		{
			return "GAT";
		}
		string[] array = text.Trim().Split(new char[3] { ' ', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
		if (array.Length >= 2)
		{
			return (array[0].Substring(0, 1) + array[array.Length - 1].Substring(0, 1)).ToUpperInvariant();
		}
		string text2 = ((array.Length == 0) ? text.Trim() : array[0]);
		if (text2.Length < 2)
		{
			return text2.ToUpperInvariant();
		}
		return text2.Substring(0, 2).ToUpperInvariant();
	}

	private static void ReplaceTextRecursive044(System.Windows.Forms.Control root, string from, string to)
	{
		foreach (System.Windows.Forms.Control control in root.Controls)
		{
			if (!string.IsNullOrEmpty(control.Text) && control.Text.Contains(from))
			{
				control.Text = control.Text.Replace(from, to);
			}
			if (control.HasChildren)
			{
				ReplaceTextRecursive044(control, from, to);
			}
		}
	}

	private static GraphicsPath Round044(System.Drawing.Rectangle bounds, int radius)
	{
		int num = radius * 2;
		GraphicsPath graphicsPath = new GraphicsPath();
		System.Drawing.Rectangle rect = new System.Drawing.Rectangle(bounds.X, bounds.Y, num, num);
		graphicsPath.AddArc(rect, 180f, 90f);
		rect.X = bounds.Right - num;
		graphicsPath.AddArc(rect, 270f, 90f);
		rect.Y = bounds.Bottom - num;
		graphicsPath.AddArc(rect, 0f, 90f);
		rect.X = bounds.Left;
		graphicsPath.AddArc(rect, 90f, 90f);
		graphicsPath.CloseFigure();
		return graphicsPath;
	}

	private void ApplyHub045()
	{
		if (!_hub045Applied)
		{
			_hub045Applied = true;
			Text = "ROADBETS2 TELEMETRIA 1.0.68.21";
			ReplaceTextRecursive044(this, "1.0.44", "1.0.45");
			BuildHome045();
			_hubStatusTimer041.Tick += async delegate
			{
				SyncHome045();
				await RefreshDrivers045(force: false);
			};
			Shown += async delegate
			{
				SyncHome045();
				await RefreshDrivers045(force: true);
			};
		}
	}

	private void BuildHome045()
	{
		if (!_hubPages041.TryGetValue("home", out var value) || value == null)
		{
			return;
		}
		value.SuspendLayout();
		value.Controls.Clear();
		value.Padding = Padding.Empty;
		value.BackColor = System.Drawing.Color.FromArgb(5, 17, 12);
		TableLayoutPanel tableLayoutPanel = new TableLayoutPanel
		{
			Dock = DockStyle.Fill,
			ColumnCount = 1,
			RowCount = 2,
			Margin = Padding.Empty,
			Padding = Padding.Empty,
			BackColor = value.BackColor
		};
		tableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
		tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 205f));
		tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
		value.Controls.Add(tableLayoutPanel);
		TableLayoutPanel tableLayoutPanel2 = new TableLayoutPanel
		{
			Dock = DockStyle.Fill,
			ColumnCount = 3,
			RowCount = 1,
			Margin = Padding.Empty,
			Padding = Padding.Empty,
			BackColor = value.BackColor
		};
		tableLayoutPanel2.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 31f));
		tableLayoutPanel2.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 36f));
		tableLayoutPanel2.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33f));
		tableLayoutPanel.Controls.Add(tableLayoutPanel2, 0, 0);
		GlassCard044 glassCard = new GlassCard044
		{
			Caption = "PERFIL DO MOTORISTA",
			Dock = DockStyle.Fill
		};
		_homeAvatar045 = new DriverAvatar044
		{
			Left = 16,
			Top = 54,
			Width = 86,
			Height = 86
		};
		_homeProfileName045 = new System.Windows.Forms.Label
		{
			Left = 116,
			Top = 55,
			Width = 250,
			Height = 30,
			Text = "Motorista GAT",
			ForeColor = System.Drawing.Color.White,
			Font = new Font("Segoe UI Semibold", 14f, System.Drawing.FontStyle.Bold)
		};
		_homeProfileMeta045 = new System.Windows.Forms.Label
		{
			Left = 116,
			Top = 89,
			Width = 270,
			Height = 92,
			Text = "Conta RoadBETS2 aguardando...",
			ForeColor = System.Drawing.Color.FromArgb(161, 193, 222),
			Font = new Font("Segoe UI", 9.2f)
		};
		System.Windows.Forms.Label value2 = new System.Windows.Forms.Label
		{
			Left = 16,
			Top = 147,
			Width = 88,
			Height = 25,
			Text = "MOTORISTA GAT",
			TextAlign = ContentAlignment.MiddleCenter,
			BackColor = System.Drawing.Color.FromArgb(8, 68, 38),
			ForeColor = System.Drawing.Color.FromArgb(135, 235, 171),
			Font = new Font("Segoe UI Semibold", 7.7f, System.Drawing.FontStyle.Bold)
		};
		glassCard.Controls.Add(_homeAvatar045);
		glassCard.Controls.Add(_homeProfileName045);
		glassCard.Controls.Add(_homeProfileMeta045);
		glassCard.Controls.Add(value2);
		tableLayoutPanel2.Controls.Add(glassCard, 0, 0);
		GlassCard044 glassCard2 = new GlassCard044
		{
			Caption = "VIAGEM ATUAL",
			Dock = DockStyle.Fill
		};
		_homeTrip045 = new System.Windows.Forms.Label
		{
			Dock = DockStyle.Fill,
			Text = "Aguardando telemetria do ETS2...",
			ForeColor = System.Drawing.Color.FromArgb(225, 236, 248),
			Font = new Font("Segoe UI Semibold", 9.7f),
			TextAlign = ContentAlignment.MiddleLeft,
			Padding = new Padding(4, 0, 4, 0)
		};
		glassCard2.Controls.Add(_homeTrip045);
		tableLayoutPanel2.Controls.Add(glassCard2, 1, 0);
		GlassCard044 glassCard3 = new GlassCard044
		{
			Caption = "SISTEMA GAT",
			Dock = DockStyle.Fill
		};
		_homeSystem045 = new System.Windows.Forms.Label
		{
			Dock = DockStyle.Fill,
			Text = "Central RoadBETS2: aguardando...",
			ForeColor = System.Drawing.Color.FromArgb(191, 219, 242),
			Font = new Font("Segoe UI Semibold", 9.6f),
			TextAlign = ContentAlignment.MiddleLeft,
			Padding = new Padding(4, 0, 4, 0)
		};
		glassCard3.Controls.Add(_homeSystem045);
		tableLayoutPanel2.Controls.Add(glassCard3, 2, 0);
		TableLayoutPanel tableLayoutPanel3 = new TableLayoutPanel
		{
			Dock = DockStyle.Fill,
			ColumnCount = 2,
			RowCount = 1,
			Margin = Padding.Empty,
			Padding = Padding.Empty,
			BackColor = value.BackColor
		};
		tableLayoutPanel3.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 43f));
		tableLayoutPanel3.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 57f));
		tableLayoutPanel.Controls.Add(tableLayoutPanel3, 0, 1);
		HeroPanel044 hero = new HeroPanel044
		{
			Dock = DockStyle.Fill,
			Margin = new Padding(7, 3, 4, 7),
			HeroImage = _homeHeroImage044
		};
		tableLayoutPanel3.Controls.Add(hero, 0, 0);
		System.Windows.Forms.Button video = OverlayAction044("SOBREPOSIÇÃO DE VÍDEO\r\nVídeo flutuante sem barra branca", 222, 66);
		System.Windows.Forms.Button truck = OverlayAction044("SOBREPOSIÇÃO DO CAMINHÃO\r\nPainel completo e redimensionável", 250, 66);
		video.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
		truck.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
		video.Click += delegate
		{
			OpenVideoOverlay041();
		};
		truck.Click += delegate
		{
			OpenTruckOverlay041();
		};
		hero.Controls.Add(video);
		hero.Controls.Add(truck);
		hero.Resize += delegate
		{
			truck.Left = Math.Max(16, hero.ClientSize.Width - truck.Width - 16);
			truck.Top = Math.Max(16, hero.ClientSize.Height - truck.Height - 18);
			video.Left = Math.Max(16, truck.Left - video.Width - 10);
			video.Top = truck.Top;
			if (video.Left <= 18)
			{
				video.Width = Math.Max(170, (hero.ClientSize.Width - 42) / 2);
				truck.Width = video.Width;
				video.Left = 14;
				truck.Left = video.Right + 8;
			}
		};
		GlassCard044 drivers = new GlassCard044
		{
			Caption = "MOTORISTAS ONLINE EM ROTA",
			Dock = DockStyle.Fill,
			Margin = new Padding(4, 3, 7, 7)
		};
		_homeDriversCount045 = new System.Windows.Forms.Label
		{
			Text = "0 online  •  0 em rota",
			Width = 220,
			Height = 25,
			Top = 12,
			Anchor = (AnchorStyles.Top | AnchorStyles.Right),
			TextAlign = ContentAlignment.MiddleRight,
			ForeColor = System.Drawing.Color.FromArgb(92, 211, 133),
			BackColor = System.Drawing.Color.Transparent,
			Font = new Font("Segoe UI Semibold", 9f, System.Drawing.FontStyle.Bold)
		};
		drivers.Controls.Add(_homeDriversCount045);
		drivers.Resize += delegate
		{
			if (_homeDriversCount045 != null)
			{
				_homeDriversCount045.Left = Math.Max(20, drivers.ClientSize.Width - _homeDriversCount045.Width - 18);
			}
		};
		_homeDrivers045 = CreateDriversGrid045();
		drivers.Controls.Add(_homeDrivers045);
		tableLayoutPanel3.Controls.Add(drivers, 1, 0);
		value.ResumeLayout(performLayout: true);
		SyncHome045();
	}

	private DataGridView CreateDriversGrid045()
	{
		DataGridView dataGridView = new DataGridView();
		dataGridView.Dock = DockStyle.Fill;
		dataGridView.BackgroundColor = System.Drawing.Color.FromArgb(5, 18, 13);
		dataGridView.BorderStyle = BorderStyle.None;
		dataGridView.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
		dataGridView.GridColor = System.Drawing.Color.FromArgb(24, 66, 41);
		dataGridView.RowHeadersVisible = false;
		dataGridView.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
		dataGridView.AllowUserToAddRows = false;
		dataGridView.AllowUserToDeleteRows = false;
		dataGridView.AllowUserToResizeRows = false;
		dataGridView.ReadOnly = true;
		dataGridView.MultiSelect = false;
		dataGridView.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
		dataGridView.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
		dataGridView.EnableHeadersVisualStyles = false;
		dataGridView.ColumnHeadersDefaultCellStyle.BackColor = System.Drawing.Color.FromArgb(10, 43, 28);
		dataGridView.ColumnHeadersDefaultCellStyle.ForeColor = System.Drawing.Color.FromArgb(220, 235, 250);
		dataGridView.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 8.2f, System.Drawing.FontStyle.Bold);
		dataGridView.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
		dataGridView.ColumnHeadersHeight = 35;
		dataGridView.DefaultCellStyle.BackColor = System.Drawing.Color.FromArgb(5, 18, 13);
		dataGridView.DefaultCellStyle.ForeColor = System.Drawing.Color.FromArgb(220, 232, 245);
		dataGridView.DefaultCellStyle.SelectionBackColor = System.Drawing.Color.FromArgb(10, 55, 31);
		dataGridView.DefaultCellStyle.SelectionForeColor = System.Drawing.Color.White;
		dataGridView.DefaultCellStyle.Font = new Font("Segoe UI", 8.7f);
		dataGridView.RowTemplate.Height = 36;
		dataGridView.Columns.Add(new DataGridViewTextBoxColumn
		{
			Name = "driver",
			HeaderText = "MOTORISTA",
			FillWeight = 120f
		});
		dataGridView.Columns.Add(new DataGridViewTextBoxColumn
		{
			Name = "status",
			HeaderText = "STATUS",
			FillWeight = 85f
		});
		dataGridView.Columns.Add(new DataGridViewTextBoxColumn
		{
			Name = "cargo",
			HeaderText = "CARGA",
			FillWeight = 125f
		});
		dataGridView.Columns.Add(new DataGridViewTextBoxColumn
		{
			Name = "destination",
			HeaderText = "DESTINO",
			FillWeight = 105f
		});
		dataGridView.Columns.Add(new DataGridViewTextBoxColumn
		{
			Name = "speed",
			HeaderText = "VELOCIDADE",
			FillWeight = 90f
		});
		dataGridView.Columns.Add(new DataGridViewTextBoxColumn
		{
			Name = "remaining",
			HeaderText = "RESTANTE",
			FillWeight = 80f
		});
		dataGridView.CellFormatting += (object sender, DataGridViewCellFormattingEventArgs e) =>
		{
			if (e.RowIndex >= 0 && e.ColumnIndex == 1 && e.Value != null)
			{
				string text = Convert.ToString(e.Value) ?? "";
				if (text.IndexOf("Em rota", StringComparison.OrdinalIgnoreCase) >= 0)
				{
					e.CellStyle.ForeColor = System.Drawing.Color.FromArgb(72, 235, 132);
				}
				else if (text.IndexOf("Online", StringComparison.OrdinalIgnoreCase) >= 0)
				{
					e.CellStyle.ForeColor = System.Drawing.Color.FromArgb(112, 203, 150);
				}
				else
				{
					e.CellStyle.ForeColor = System.Drawing.Color.FromArgb(255, 121, 121);
				}
			}
		};
		return dataGridView;
	}

	private void SyncHome045()
	{
		try
		{
			string text = ((!string.IsNullOrWhiteSpace(_accountUser)) ? _accountUser : "Motorista GAT");
			string text2 = ((!string.IsNullOrWhiteSpace(_driver)) ? _driver : text);
			if (_homeProfileName045 != null)
			{
				_homeProfileName045.Text = text2;
			}
			if (_homeAvatar045 != null)
			{
				_homeAvatar045.Initials = Initials044(text2);
				_homeAvatar045.Invalidate();
			}
			if (_homeProfileMeta045 != null)
			{
				_homeProfileMeta045.Text = (AccountReady ? ("● Conta ativa\r\n● PC vinculado\r\n@" + text + "\r\nROADLIFE ETS2") : "○ Conta não conectada\r\n○ PC aguardando vínculo\r\nAbra Configurações para entrar.");
				_homeProfileMeta045.ForeColor = (AccountReady ? System.Drawing.Color.FromArgb(124, 231, 154) : System.Drawing.Color.FromArgb(198, 178, 132));
			}
			if (_homeTrip045 != null)
			{
				_homeTrip045.Text = Safe041(lblTruck, "TruckSim GPS: aguardando") + "\r\n\r\n" + Safe041(lblCargo, "Carga: Sem carga") + "\r\n" + Safe041(lblRoute, "Rota: -") + "\r\n" + Safe041(lblDistance, "Restante: -") + "\r\n" + Safe041(lblSpeed, "Velocidade: 0 km/h");
			}
			if (_homeSystem045 != null)
			{
				string text3 = Safe041(lblTelemetry, "Central RoadBETS2: aguardando");
				string text4 = Safe041(lblServer, "Servidor: opcional");
				_homeSystem045.Text = "● " + text3 + "\r\n● " + text4 + "\r\n● Cliente: 1.0.68.9\r\n\r\n" + (AccountReady ? "✓ Ecossistema GAT conectado." : "Aguardando Conta RoadBETS2.");
				_homeSystem045.ForeColor = (AccountReady ? System.Drawing.Color.FromArgb(125, 231, 154) : System.Drawing.Color.FromArgb(191, 219, 242));
			}
		}
		catch
		{
		}
	}

	private async Task RefreshDrivers045(bool force)
	{
		if (_homeDrivers045 == null || _homeDrivers045.IsDisposed || _homeDriversBusy045 || (!force && (DateTime.UtcNow - _homeDriversLast045).TotalSeconds < 3.0))
		{
			return;
		}
		_homeDriversBusy045 = true;
		_homeDriversLast045 = DateTime.UtcNow;
		try
		{
			List<JObject> liveRows = new List<JObject>();
			try
			{
				string requestUri = "https://api.gatlogets2.com.br/api/public/account-live?t=" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
				JObject jObject = JObject.Parse(await _hubHttp041.GetStringAsync(requestUri));
				JArray jArray = jObject["telemetry"] as JArray;
				if (jObject.Value<bool?>("ok") == true && jArray != null)
				{
					foreach (JToken item2 in jArray.Take(64))
					{
						if (item2 is JObject item)
						{
							liveRows.Add(item);
						}
					}
				}
			}
			catch
			{
			}
			string current = ((!string.IsNullOrWhiteSpace(_driver)) ? _driver : _accountUser);
			_homeDrivers045.Rows.Clear();
			int num = 0;
			int num2 = 0;
			HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			liveRows = (from x in liveRows
				orderby string.Equals(Convert.ToString(x["driver"]), current, StringComparison.OrdinalIgnoreCase) descending, x.Value<bool?>("on_job") == true descending
				select x).ThenBy((JObject x) => Convert.ToString(x["driver"]) ?? string.Empty, StringComparer.OrdinalIgnoreCase).ToList();
			foreach (JObject item3 in liveRows)
			{
				string text = (Convert.ToString(item3["driver"]) ?? string.Empty).Trim();
				if (string.IsNullOrWhiteSpace(text))
				{
					text = (Convert.ToString(item3["account_user"]) ?? string.Empty).Trim();
				}
				if (!string.IsNullOrWhiteSpace(text) && hashSet.Add(text))
				{
					string text2 = (Convert.ToString(item3["cargo_name"]) ?? string.Empty).Trim();
					string text3 = (Convert.ToString(item3["destination_city"]) ?? string.Empty).Trim();
					bool flag = item3.Value<bool?>("on_job") == true || (!string.IsNullOrWhiteSpace(text2) && text2.IndexOf("Sem carga", StringComparison.OrdinalIgnoreCase) < 0 && text2 != "-" && text2 != "—");
					double num3 = item3.Value<double?>("speed_kmh").GetValueOrDefault();
					if (double.IsNaN(num3) || double.IsInfinity(num3) || num3 < 0.0 || num3 > 250.0)
					{
						num3 = 0.0;
					}
					double num4 = item3.Value<double?>("remaining_km").GetValueOrDefault();
					if (double.IsNaN(num4) || double.IsInfinity(num4) || num4 < 0.0 || num4 > 20000.0)
					{
						num4 = 0.0;
					}
					string text4 = num3.ToString("0") + " km/h";
					string text5 = ((num4 > 0.0) ? (num4.ToString((num4 >= 100.0) ? "0" : "0.0") + " km") : "—");
					if (string.IsNullOrWhiteSpace(text2))
					{
						text2 = (flag ? "Carga detectada" : "Sem carga");
					}
					if (string.IsNullOrWhiteSpace(text3))
					{
						text3 = "—";
					}
					num++;
					if (flag)
					{
						num2++;
					}
					_homeDrivers045.Rows.Add(text, flag ? "● Em rota" : "● Online", text2, text3, text4, text5);
				}
			}
			if (num == 0 && !string.IsNullOrWhiteSpace(current))
			{
				string text6 = ValueAfter045(Safe041(lblCargo, "Carga: Sem carga"), "Carga:");
				string text7 = Destination045(ValueAfter045(Safe041(lblRoute, "Rota: -"), "Rota:"));
				string text8 = ValueAfter045(Safe041(lblSpeed, "Velocidade: 0 km/h"), "Velocidade:");
				string text9 = ValueAfter045(Safe041(lblDistance, "Restante: -"), "Distância restante:", "Restante:");
				bool flag2 = !string.IsNullOrWhiteSpace(text6) && text6.IndexOf("Sem carga", StringComparison.OrdinalIgnoreCase) < 0 && text6 != "-" && text6 != "—";
				_homeDrivers045.Rows.Add(current, flag2 ? "● Em rota" : "● Online", string.IsNullOrWhiteSpace(text6) ? "Sem carga" : text6, string.IsNullOrWhiteSpace(text7) ? "—" : text7, string.IsNullOrWhiteSpace(text8) ? "0 km/h" : text8, string.IsNullOrWhiteSpace(text9) ? "—" : text9);
				num = 1;
				if (flag2)
				{
					num2 = 1;
				}
			}
			if (_homeDriversCount045 != null)
			{
				_homeDriversCount045.Text = num + " online  •  " + num2 + " em rota";
			}
		}
		catch
		{
		}
		finally
		{
			_homeDriversBusy045 = false;
		}
	}

	private static string ValueAfter045(string text, params string[] prefixes)
	{
		string text2 = text ?? "";
		foreach (string text3 in prefixes)
		{
			int num = text2.IndexOf(text3, StringComparison.OrdinalIgnoreCase);
			if (num >= 0)
			{
				return text2.Substring(num + text3.Length).Trim();
			}
		}
		return text2.Trim();
	}

	private static string Destination045(string route)
	{
		if (string.IsNullOrWhiteSpace(route))
		{
			return "";
		}
		string[] array = new string[3] { "→", "->", ">" };
		foreach (string text in array)
		{
			int num = route.LastIndexOf(text, StringComparison.Ordinal);
			if (num >= 0 && num + text.Length < route.Length)
			{
				return route.Substring(num + text.Length).Trim();
			}
		}
		return route.Trim('-', ' ', '—');
	}

	private static string DashSettingsFile045()
	{
		string text = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GAT-LOG", "GAT-Telemetria");
		Directory.CreateDirectory(text);
		return System.IO.Path.Combine(text, "gat-dash-settings.json");
	}

	private string DashSettingsJson045()
	{
		JObject jObject = new JObject
		{
			["host"] = "",
			["voice"] = true,
			["tolerance"] = 3
		};
		try
		{
			string path = DashSettingsFile045();
			if (File.Exists(path))
			{
				JObject jObject2 = JObject.Parse(File.ReadAllText(path));
				if (jObject2["host"] != null)
				{
					jObject["host"] = Convert.ToString(jObject2["host"]) ?? "";
				}
				if (jObject2["voice"] != null)
				{
					jObject["voice"] = Convert.ToBoolean(jObject2["voice"]);
				}
				if (jObject2["tolerance"] != null)
				{
					double val = Convert.ToDouble(jObject2["tolerance"], CultureInfo.InvariantCulture);
					jObject["tolerance"] = Math.Max(0.0, Math.Min(30.0, val));
				}
			}
		}
		catch
		{
		}
		return jObject.ToString(Newtonsoft.Json.Formatting.None);
	}

	private void SaveDashSettings045(JObject message)
	{
		try
		{
			JObject jObject = JObject.Parse(DashSettingsJson045());
			if (message["host"] != null)
			{
				jObject["host"] = (Convert.ToString(message["host"]) ?? "").Trim();
			}
			if (message["voice"] != null)
			{
				jObject["voice"] = Convert.ToBoolean(message["voice"]);
			}
			if (message["tolerance"] != null)
			{
				double val = Convert.ToDouble(message["tolerance"], CultureInfo.InvariantCulture);
				jObject["tolerance"] = Math.Max(0.0, Math.Min(30.0, val));
			}
			string text = DashSettingsFile045();
			string text2 = text + ".tmp";
			File.WriteAllText(text2, jObject.ToString(Newtonsoft.Json.Formatting.Indented));
			if (File.Exists(text))
			{
				File.Delete(text);
			}
			File.Move(text2, text);
		}
		catch
		{
		}
	}

	private GatMapDetection DetectMapFromGameLog()
	{
		try
		{
			if ((DateTime.UtcNow - _gatMapLastScanUtc).TotalSeconds < 8.0 && _gatMapCached != null)
			{
				return _gatMapCached;
			}
			_gatMapLastScanUtc = DateTime.UtcNow;
			string text = GatFindGameLog();
			if (string.IsNullOrWhiteSpace(text) || !File.Exists(text))
			{
				return GatRememberMap(new GatMapDetection
				{
					Key = "base",
					Label = "Mapa Base",
					Source = "game.log:not-found"
				}, text);
			}
			FileInfo fileInfo = new FileInfo(text);
			if (string.Equals(text, _gatMapLastLogPath, StringComparison.OrdinalIgnoreCase) && fileInfo.Length == _gatMapLastLogLength && fileInfo.LastWriteTimeUtc == _gatMapLastLogWriteUtc && _gatMapCached != null)
			{
				return _gatMapCached;
			}
			_gatMapLastLogPath = text;
			_gatMapLastLogLength = fileInfo.Length;
			_gatMapLastLogWriteUtc = fileInfo.LastWriteTimeUtc;
			string[] array = File.ReadAllLines(text);
			int num = Math.Max(0, array.Length - 14000);
			List<string> list = new List<string>(array.Length - num);
			for (int i = num; i < array.Length; i++)
			{
				list.Add(array[i] ?? string.Empty);
			}
			GatMountedMod gatMountedMod = GatChooseMapMod(GatParseMountedMods(list), list);
			if (gatMountedMod == null)
			{
				return GatRememberMap(new GatMapDetection
				{
					Key = "base",
					Label = "Mapa Base",
					Source = "game.log:no-map-mod"
				}, text);
			}
			string text2 = GatCleanMapLabel(gatMountedMod.Name, gatMountedMod.PackageName);
			string text3 = GatSlug(text2);
			if (string.IsNullOrWhiteSpace(text3))
			{
				text3 = GatSlug(gatMountedMod.PackageName);
			}
			if (string.IsNullOrWhiteSpace(text3) || text3 == "map" || text3 == "mapa")
			{
				text3 = "other";
			}
			return GatRememberMap(new GatMapDetection
			{
				Key = text3,
				Label = (string.IsNullOrWhiteSpace(text2) ? gatMountedMod.PackageName : text2),
				PackageName = gatMountedMod.PackageName,
				Source = "game.log:auto"
			}, text);
		}
		catch (Exception ex)
		{
			try
			{
				ClientStore.Log("mapa auto: falha ao ler game.log: " + ex.Message);
			}
			catch
			{
			}
			return _gatMapCached ?? new GatMapDetection();
		}
	}

	private GatMapDetection GatRememberMap(GatMapDetection result, string path)
	{
		if (result == null)
		{
			result = new GatMapDetection();
		}
		bool num = _gatMapCached == null || !string.Equals(_gatMapCached.Key, result.Key, StringComparison.OrdinalIgnoreCase) || !string.Equals(_gatMapCached.Label, result.Label, StringComparison.OrdinalIgnoreCase) || !string.Equals(_gatMapCached.PackageName, result.PackageName, StringComparison.OrdinalIgnoreCase);
		_gatMapCached = result;
		if (num)
		{
			try
			{
				ClientStore.Log("mapa auto detectado: key=" + result.Key + " | label=" + result.Label + " | package=" + result.PackageName + " | source=" + result.Source + (string.IsNullOrWhiteSpace(path) ? string.Empty : (" | log=" + path)));
			}
			catch
			{
			}
		}
		return result;
	}

	private static string GatFindGameLog()
	{
		List<string> list = new List<string>();
		string folderPath = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
		if (!string.IsNullOrWhiteSpace(folderPath))
		{
			list.Add(System.IO.Path.Combine(folderPath, "Euro Truck Simulator 2", "game.log.txt"));
		}
		string folderPath2 = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
		if (!string.IsNullOrWhiteSpace(folderPath2))
		{
			list.Add(System.IO.Path.Combine(folderPath2, "Documents", "Euro Truck Simulator 2", "game.log.txt"));
			list.Add(System.IO.Path.Combine(folderPath2, "OneDrive", "Documents", "Euro Truck Simulator 2", "game.log.txt"));
		}
		return (from p in list.Distinct(StringComparer.OrdinalIgnoreCase).Where(File.Exists)
			select new FileInfo(p) into f
			orderby f.LastWriteTimeUtc descending
			select f.FullName).FirstOrDefault() ?? string.Empty;
	}

	private static List<GatMountedMod> GatParseMountedMods(List<string> lines)
	{
		List<GatMountedMod> list = new List<GatMountedMod>();
		HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		Regex regex = new Regex("\\[mod_package_manager\\]\\s+Mod\\s+\"(?<name>[^\"]+)\"\\s+has been mounted\\.\\s*\\(package_name:\\s*(?<pkg>[^,\\)]+)", RegexOptions.IgnoreCase);
		Regex regex2 = new Regex("\\[mods\\]\\s+Active (?:local|workshop) mod\\s+(?<pkg>[^\\s]+)\\s+\\(name:\\s*(?<name>[^,\\)]+)", RegexOptions.IgnoreCase);
		foreach (string line in lines)
		{
			Match match = regex.Match(line);
			if (!match.Success)
			{
				match = regex2.Match(line);
			}
			if (match.Success)
			{
				string text = (match.Groups["name"].Value ?? string.Empty).Trim();
				string text2 = (match.Groups["pkg"].Value ?? string.Empty).Trim();
				string item = (text2 + "|" + text).ToLowerInvariant();
				if (hashSet.Add(item))
				{
					list.Add(new GatMountedMod
					{
						Name = text,
						PackageName = text2,
						Raw = line
					});
				}
			}
		}
		return list;
	}

	private static GatMountedMod GatChooseMapMod(List<GatMountedMod> mods, List<string> lines)
	{
		GatMountedMod result = null;
		int num = int.MinValue;
		foreach (GatMountedMod mod in mods)
		{
			string text = ((mod.Name ?? string.Empty) + " " + (mod.PackageName ?? string.Empty)).ToLowerInvariant();
			int num2 = 0;
			if (Regex.IsMatch(text, "(^|[^a-z0-9])(map|maps|mapa|mapas)([^a-z0-9]|$)"))
			{
				num2 += 90;
			}
			if (text.Contains("defmap") || text.Contains("def-map") || text.Contains("def_map"))
			{
				num2 += 95;
			}
			if (text.Contains("-map-") || text.Contains("_map_") || text.EndsWith("-map") || text.EndsWith("_map"))
			{
				num2 += 80;
			}
			if (text.Contains("road") || text.Contains("roads") || text.Contains("route") || text.Contains("rota"))
			{
				num2 += 35;
			}
			if (text.Contains("region") || text.Contains("territory") || text.Contains("expansion") || text.Contains("rebuild"))
			{
				num2 += 30;
			}
			if (text.Contains("connector") || text.Contains("connection"))
			{
				num2 += 18;
			}
			if (Regex.IsMatch(text, "\\b(asset|assets|model|models|media|sound|sounds|engine|truck|trailer|skin|paint|wheel|wheels|interior|radio|weather|physics|traffic|cargo)\\b"))
			{
				num2 -= 55;
			}
			string text2 = (mod.PackageName ?? string.Empty).Trim().ToLowerInvariant();
			if (text2.Length > 2)
			{
				foreach (string line in lines)
				{
					string text3 = line.ToLowerInvariant();
					if (text3.Contains(text2) && (text3.Contains("/map/") || text3.Contains("\\map\\") || text3.Contains(".mbd") || text3.Contains("map_data.sii")))
					{
						num2 += 130;
						break;
					}
				}
			}
			if (num2 > num)
			{
				num = num2;
				result = mod;
			}
		}
		if (num < 60)
		{
			return null;
		}
		return result;
	}

	private static string GatCleanMapLabel(string name, string packageName)
	{
		string text = (name ?? string.Empty).Trim();
		if (string.IsNullOrWhiteSpace(text))
		{
			text = (packageName ?? string.Empty).Trim();
		}
		text = Regex.Replace(text, "\\s*\\((?:map|mapa|def(?:inition)?(?:\\s*&\\s*map)?|package)[^\\)]*\\)\\s*$", "", RegexOptions.IgnoreCase);
		text = Regex.Replace(text, "\\s+(?:def(?:inition)?\\s*&\\s*map|def\\s*&\\s*map|map|mapa)\\s+package\\s*$", "", RegexOptions.IgnoreCase);
		text = Regex.Replace(text, "\\s+map\\s*$", "", RegexOptions.IgnoreCase);
		text = Regex.Replace(text, "\\s+mapa\\s*$", "", RegexOptions.IgnoreCase);
		return text.Trim();
	}

	private static string GatSlug(string value)
	{
		string text = (value ?? string.Empty).Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
		StringBuilder stringBuilder = new StringBuilder();
		string text2 = text;
		foreach (char c in text2)
		{
			if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
			{
				if (char.IsLetterOrDigit(c))
				{
					stringBuilder.Append(c);
				}
				else if (stringBuilder.Length > 0 && stringBuilder[stringBuilder.Length - 1] != '_')
				{
					stringBuilder.Append('_');
				}
			}
		}
		return Regex.Replace(Regex.Replace(Regex.Replace(Regex.Replace(stringBuilder.ToString().Trim('_'), "_(?:map|mapa)_package$", ""), "_(?:def|definition)(?:_and)?_(?:map|mapa)_package$", ""), "_package$", ""), "_(?:map|mapa)$", "").Trim('_');
	}

	private static string RoadLifeOverlayRoot()
	{
		return System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "RoadLifeDash");
	}

	private static string RoadLifeOverlayXaml()
	{
		return System.IO.Path.Combine(RoadLifeOverlayRoot(), "RoadLifeDash.xaml");
	}

	private void RoadLifeOverlayInitialize()
	{
		FormClosed += delegate
		{
			try
			{
				CloseRoadLifeOverlay();
			}
			catch
			{
			}
		};
	}

	private bool RoadLifeOverlayRunning()
	{
		if (_roadLifeOverlay != null)
		{
			return _roadLifeOverlay.IsVisible;
		}
		return false;
	}

	private void ToggleRoadLifeOverlay()
	{
		if (RoadLifeOverlayRunning())
		{
			CloseRoadLifeOverlay();
		}
		else
		{
			OpenRoadLifeOverlay();
		}
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
			string text = RoadLifeOverlayXaml();
			if (!File.Exists(text))
			{
				System.Windows.Forms.MessageBox.Show("RoadLifeDash.xaml não encontrado.\r\n\r\n" + text, "ROADBETS2 DASH", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
				return;
			}
			string xaml = File.ReadAllText(text);
			xaml = RoadLifeThemePrepareXaml(xaml);
			object obj;
			using (StringReader input = new StringReader(xaml))
			{
				using XmlReader reader = XmlReader.Create(input);
				obj = XamlReader.Load(reader);
			}
			_roadLifeOverlay = obj as Window;
			if (_roadLifeOverlay == null)
			{
				throw new InvalidOperationException("RoadLifeDash.xaml não contém uma Window válida.");
			}
			if (_roadLifeOverlay.FindName("Root") is FrameworkElement frameworkElement)
			{
				frameworkElement.MouseLeftButtonDown += (object sender, MouseButtonEventArgs e) =>
				{
					if (!_roadLifeClickThrough && e.LeftButton == MouseButtonState.Pressed)
					{
						try
						{
							_roadLifeOverlay.DragMove();
						}
						catch
						{
						}
					}
				};
			}
			LoadRoadLifeImage("StructureImage", System.IO.Path.Combine(RoadLifeOverlayRoot(), "Assets", "dashboard-structure-green.png"));
			LoadRoadLifeImage("RoadLifeLogo", System.IO.Path.Combine(RoadLifeOverlayRoot(), "Assets", "roadbets2-logo.png"));
			RoadLifeThemeWire();
			_roadLifeOverlay.SourceInitialized += delegate
			{
				try
				{
					WindowInteropHelper windowInteropHelper = new WindowInteropHelper(_roadLifeOverlay);
					_roadLifeHwnd = windowInteropHelper.Handle;
					RoadLifeNative.RegisterHotKey(_roadLifeHwnd, 1001, 0u, 119u);
					RoadLifeNative.RegisterHotKey(_roadLifeHwnd, 1002, 0u, 120u);
					RoadLifeNative.RegisterHotKey(_roadLifeHwnd, 1003, 0u, 121u);
					_roadLifeSource = HwndSource.FromHwnd(_roadLifeHwnd);
					_roadLifeHook = RoadLifeWndProc;
					if (_roadLifeSource != null)
					{
						_roadLifeSource.AddHook(_roadLifeHook);
					}
				}
				catch
				{
				}
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
					{
						_roadLifeSource.RemoveHook(_roadLifeHook);
					}
				}
				catch
				{
				}
				try
				{
					_roadLifeClock?.Stop();
				}
				catch
				{
				}
				_roadLifeClock = null;
				_roadLifeSource = null;
				_roadLifeHook = null;
				_roadLifeHwnd = IntPtr.Zero;
				_roadLifeClickThrough = false;
				_roadLifeOverlay = null;
			};
			_roadLifeClock = new DispatcherTimer();
			_roadLifeClock.Interval = TimeSpan.FromMilliseconds(100.0);
			_roadLifeClock.Tick += async delegate
			{
				SetRoadLifeText("DateText", DateTime.Now.ToString("dd/MM/yy"));
				SetRoadLifeText("TimeText", DateTime.Now.ToString("HH:mm:ss"));
				if (_roadLifeFastPollBusy)
				{
					return;
				}
				_roadLifeFastPollBusy = true;
				try
				{
					JObject jObject = await _telemetry.ReadAsync();
					if (jObject != null && RoadLifeOverlayRunning())
					{
						RoadLifeOverlayPush(jObject);
					}
				}
				catch
				{
				}
				finally
				{
					_roadLifeFastPollBusy = false;
				}
			};
			_roadLifeClock.Start();
			_roadLifeOverlay.Show();
			SetRoadLifeText("DateText", DateTime.Now.ToString("dd/MM/yy"));
			SetRoadLifeText("TimeText", DateTime.Now.ToString("HH:mm:ss"));
			ClientStore.Log("RoadBETS2 Dashboard2 aberto como overlay interno do GAT Telemetria.");
		}
		catch (Exception ex)
		{
			ClientStore.Log("RoadBETS2 Dashboard2 abrir: " + ex);
			System.Windows.Forms.MessageBox.Show(ex.Message, "ROADBETS2 DASH", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
		}
	}

	private void CloseRoadLifeOverlay()
	{
		try
		{
			if (_roadLifeOverlay != null)
			{
				_roadLifeOverlay.Close();
			}
		}
		catch
		{
		}
	}

	private IntPtr RoadLifeWndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
	{
		if (msg == 786)
		{
			switch (wParam.ToInt32())
			{
			case 1001:
				SetRoadLifeClickThrough(!_roadLifeClickThrough);
				handled = true;
				break;
			case 1002:
				if (_roadLifeOverlay != null)
				{
					_roadLifeOverlay.Topmost = !_roadLifeOverlay.Topmost;
				}
				handled = true;
				break;
			case 1003:
				CloseRoadLifeOverlay();
				handled = true;
				break;
			}
		}
		return IntPtr.Zero;
	}

	private void SetRoadLifeClickThrough(bool enabled)
	{
		if (!(_roadLifeHwnd == IntPtr.Zero))
		{
			int windowLong = RoadLifeNative.GetWindowLong(_roadLifeHwnd, -20);
			windowLong = ((!enabled) ? (windowLong & -33) : (windowLong | 0x20));
			RoadLifeNative.SetWindowLong(_roadLifeHwnd, -20, windowLong);
			_roadLifeClickThrough = enabled;
		}
	}

	private void RoadLifeOverlayPush(JObject tele)
	{
		if (tele == null || !RoadLifeOverlayRunning())
		{
			return;
		}
		try
		{
			JObject payload = RoadLifeOverlayBuildPayload(tele);
			Window roadLifeOverlay = _roadLifeOverlay;
			if (roadLifeOverlay == null)
			{
				return;
			}
			if (!roadLifeOverlay.Dispatcher.CheckAccess())
			{
				roadLifeOverlay.Dispatcher.BeginInvoke((Action)(() =>
				{
					ApplyRoadLifePayload(payload);
				}));
			}
			else
			{
				ApplyRoadLifePayload(payload);
			}
		}
		catch (Exception ex)
		{
			ClientStore.Log("RoadBETS2 Dashboard2 atualizar: " + ex.Message);
		}
	}

	private void ApplyRoadLifePayload(JObject d)
	{
		if (_roadLifeOverlay != null && d != null)
		{
			JObject obj = (d["route"] as JObject) ?? new JObject();
			JObject jObject = (d["cargo"] as JObject) ?? new JObject();
			JObject jObject2 = (d["fuel"] as JObject) ?? new JObject();
			JObject jObject3 = (d["truck"] as JObject) ?? new JObject();
			JObject jObject4 = (d["damage"] as JObject) ?? new JObject();
			string text = RoadLifeString(obj["source"], "—");
			string text2 = RoadLifeString(obj["destination"], "—");
			double v = RoadLifeDouble(obj["distance_remaining_km"]);
			double v2 = RoadLifeDouble(obj["distance_travelled_km"]);
			SetRoadLifeText("RouteText", text + " → " + text2);
			SetRoadLifeText("StartKmText", "0 km");
			SetRoadLifeText("RemainKmTop", RoadLifeInt(v) + " km");
			SetRoadLifeText("RemainBottom", RoadLifeInt(v) + " km");
			SetRoadLifeText("TravelBottom", RoadLifeInt(v2) + " km");
			string text3 = RoadLifeString(jObject["name"], "Sem carga");
			string cargoId = RoadLifeString(jObject["id"], "");
			SetRoadLifeText("CargoWeight", RoadLifeDec(RoadLifeDouble(jObject["weight_t"]), 1) + " t");
			SetRoadLifeText("CargoRightName", text3.ToUpperInvariant());
			SetRoadLifeCargoImage(cargoId, text3);
			double v3 = RoadLifeDouble(jObject2["current_l"]);
			double v4 = RoadLifeDouble(jObject2["capacity_l"]);
			double num = Math.Max(0.0, Math.Min(100.0, RoadLifeDouble(jObject2["percent"])));
			SetRoadLifeText("FuelText", RoadLifeInt(v3) + " / " + RoadLifeInt(v4) + " L");
			SetRoadLifeText("FuelPct", RoadLifeInt(num) + "%");
			if (_roadLifeOverlay.FindName("FuelBar") is Border border)
			{
				border.Width = 125.0 * num / 100.0;
			}
			SetRoadLifeText("TempText", RoadLifeInt(RoadLifeDouble(jObject3["temperature_c"])) + "°C");
			SetRoadLifeText("SpeedText", RoadLifeInt(RoadLifeDouble(jObject3["speed_kmh"])));
			SetRoadLifeText("GearText", RoadLifeString(jObject3["gear"], "N"));
			double num2 = RoadLifeDouble(jObject3["speed_limit_kmh"]);
			SetRoadLifeText("LimitText", (num2 > 0.0) ? RoadLifeInt(num2) : "—");
			RoadLifeOverlaySetSpeedColor(RoadLifeDouble(jObject3["speed_kmh"]), num2);
			double num3 = RoadLifeDouble(jObject3["cruise_speed_kmh"]);
			SetRoadLifeText("CruiseText", (RoadLifeBool(jObject3["cruise"]) && num3 > 0.0) ? (RoadLifeInt(num3) + " km/h") : "—");
			SetRoadLifeText("EtaText", RoadLifeString(d["driver"], "—"));
			SetRoadLifeText("TruckText", RoadLifeString(jObject3["model"], "—"));
			SetRoadLifeText("TruckDamageText", RoadLifeDec(RoadLifeDouble(jObject4["truck_pct"]), 1) + "%");
			SetRoadLifeText("TrailerDamageText", RoadLifeDec(RoadLifeDouble(jObject4["trailer_pct"]), 1) + "%");
			SetRoadLifeText("CargoDamageText", RoadLifeDec(RoadLifeDouble(jObject4["cargo_pct"]), 1) + "%");
		}
	}

	private void SetRoadLifeText(string name, string value)
	{
		if (_roadLifeOverlay != null && _roadLifeOverlay.FindName(name) is TextBlock textBlock)
		{
			textBlock.Text = value ?? string.Empty;
		}
	}

	private void RoadLifeOverlaySetSpeedColor(double speed, double limit)
	{
		System.Windows.Media.Color color = System.Windows.Media.Color.FromRgb(byte.MaxValue, byte.MaxValue, byte.MaxValue);
		if (!double.IsNaN(speed) && !double.IsNaN(limit) && limit > 0.1)
		{
			double num = Math.Abs(speed);
			if (num > limit + 1.0)
			{
				color = System.Windows.Media.Color.FromRgb(byte.MaxValue, 65, 82);
			}

		}
		SolidColorBrush solidColorBrush = new SolidColorBrush(color);
		if (_roadLifeOverlay.FindName("SpeedText") is TextBlock textBlock)
		{
			textBlock.Foreground = solidColorBrush;
		}
		if (_roadLifeOverlay.FindName("SpeedRing") is Ellipse ellipse)
		{
			ellipse.Stroke = solidColorBrush;
		}
	}

	private void LoadRoadLifeImage(string controlName, string path)
	{
		if (_roadLifeOverlay == null || !File.Exists(path))
		{
			return;
		}
		try
		{
			if (_roadLifeOverlay.FindName(controlName) is System.Windows.Controls.Image image)
			{
				BitmapImage bitmapImage = new BitmapImage();
				bitmapImage.BeginInit();
				bitmapImage.UriSource = new Uri(path, UriKind.Absolute);
				bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
				bitmapImage.EndInit();
				image.Source = bitmapImage;
			}
		}
		catch
		{
		}
	}

	private void SetRoadLifeCargoImage(string cargoId, string cargoName)
	{
		if (_roadLifeOverlay == null || !(_roadLifeOverlay.FindName("CargoPreview") is System.Windows.Controls.Image image))
		{
			return;
		}
		string text = System.IO.Path.Combine(RoadLifeOverlayRoot(), "Assets", "cargo");
		string path = System.IO.Path.Combine(text, "generated");
		string text2 = RoadLifeCargoKey(cargoName);
		string text3 = RoadLifeCargoKey(cargoId);
		if (string.IsNullOrWhiteSpace(text2) || text2 == "semcarga" || text2 == "nocargo")
		{
			image.Source = null;
			image.Visibility = Visibility.Collapsed;
			return;
		}
		RoadLifeLoadCargoAliases(text);
		string value = null;
		if (_roadLifeCargoAliases != null)
		{
			if (!string.IsNullOrWhiteSpace(text3))
			{
				_roadLifeCargoAliases.TryGetValue(text3, out value);
			}
			if (string.IsNullOrWhiteSpace(value) && !string.IsNullOrWhiteSpace(text2))
			{
				_roadLifeCargoAliases.TryGetValue(text2, out value);
			}
		}
		if (!string.IsNullOrWhiteSpace(value))
		{
			string path2 = System.IO.Path.Combine(path, value + ".png");
			if (File.Exists(path2))
			{
				LoadRoadLifeImage("CargoPreview", path2);
				image.Visibility = Visibility.Visible;
				return;
			}
		}
		string text4 = RoadLifeSlug(cargoName);
		string[] array = new string[4] { ".png", ".jpg", ".jpeg", ".webp" };
		foreach (string text5 in array)
		{
			string path3 = System.IO.Path.Combine(text, text4 + text5);
			if (!string.IsNullOrWhiteSpace(text4) && File.Exists(path3))
			{
				LoadRoadLifeImage("CargoPreview", path3);
				image.Visibility = Visibility.Visible;
				return;
			}
		}
		image.Source = null;
		image.Visibility = Visibility.Collapsed;
	}

	private void RoadLifeLoadCargoAliases(string folder)
	{
		if (_roadLifeCargoAliasesLoaded)
		{
			return;
		}
		_roadLifeCargoAliasesLoaded = true;
		_roadLifeCargoAliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		try
		{
			string path = System.IO.Path.Combine(folder, "cargo-name-map.json");
			if (!File.Exists(path) || !(JObject.Parse(File.ReadAllText(path))["aliases"] is JObject jObject))
			{
				return;
			}
			foreach (JProperty item in jObject.Properties())
			{
				string text = RoadLifeCargoKey(item.Name);
				string value = Convert.ToString(item.Value, CultureInfo.InvariantCulture) ?? string.Empty;
				if (!string.IsNullOrWhiteSpace(text) && !string.IsNullOrWhiteSpace(value))
				{
					_roadLifeCargoAliases[text] = value;
				}
			}
		}
		catch (Exception ex)
		{
			ClientStore.Log("RoadBETS2 cargo aliases: " + ex.Message);
		}
	}

	private static string RoadLifeCargoKey(string value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return string.Empty;
		}
		string text = value.Normalize(NormalizationForm.FormD);
		StringBuilder stringBuilder = new StringBuilder();
		string text2 = text;
		foreach (char c in text2)
		{
			if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark && char.IsLetterOrDigit(c))
			{
				stringBuilder.Append(char.ToLowerInvariant(c));
			}
		}
		return stringBuilder.ToString();
	}

	private static string RoadLifeSlug(string value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return string.Empty;
		}
		string text = value.Normalize(NormalizationForm.FormD);
		StringBuilder stringBuilder = new StringBuilder();
		string text2 = text;
		foreach (char c in text2)
		{
			if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
			{
				if (char.IsLetterOrDigit(c))
				{
					stringBuilder.Append(char.ToLowerInvariant(c));
				}
				else if (stringBuilder.Length > 0 && stringBuilder[stringBuilder.Length - 1] != '-')
				{
					stringBuilder.Append('-');
				}
			}
		}
		return stringBuilder.ToString().Trim('-');
	}

	private JObject RoadLifeOverlayBuildPayload(JObject tele)
	{
		string text = RoadLifeOverlayText(tele, "cargo_name", "job.cargoName", "job.cargo", "Job.CargoName");
		string text2 = RoadLifeOverlayText(tele, "cargo_id", "cargoId", "cargoID", "cargo_token", "cargoToken", "job.cargoId", "job.cargoID", "job.cargo_id", "job.cargoToken", "Job.CargoId", "Job.CargoID", "Job.CargoToken");
		string text3 = RoadLifeOverlayText(tele, "source_city", "job.sourceCity", "Job.SourceCity");
		string text4 = RoadLifeOverlayText(tele, "destination_city", "job.destinationCity", "Job.DestinationCity");
		double num = RoadLifeOverlayNumber(tele, "mass_kg", "cargoMass", "cargo_mass", "job.cargoMass", "Job.CargoMass");
		double num2 = RoadLifeOverlayNumber(tele, "remaining_km", "remainingKm", "navigation.estimatedDistance", "Navigation.EstimatedDistance");
		double num3 = RoadLifeOverlayNumber(tele, "planned_distance_km", "plannedDistanceKm", "job.plannedDistanceKm", "Job.PlannedDistanceKm");
		double v = ((!double.IsNaN(num3) && !double.IsNaN(num2)) ? Math.Max(0.0, num3 - num2) : 0.0);
		double num4 = RoadLifeOverlayNumber(tele, "truck.fuel", "Truck.Fuel", "fuel", "fuelAmount", "fuelLiters");
		double num5 = RoadLifeOverlayNumber(tele, "truck.fuelCapacity", "Truck.FuelCapacity", "fuelCapacity", "fuel_capacity");
		double num6 = ((!double.IsNaN(num4) && !double.IsNaN(num5) && num5 > 0.0) ? (num4 / num5 * 100.0) : RoadLifeOverlayNumber(tele, "truck.fuelPercent", "fuelPercent", "fuel_percent"));
		double num7 = RoadLifeOverlayNumber(tele, "truck.speed", "Truck.Speed", "speed", "speed_kmh");
		if (!double.IsNaN(num7) && Math.Abs(num7) < 3.0)
		{
			num7 *= 3.6;
		}
		double v2 = RoadLifeOverlayNumber(tele, "truck.waterTemperature", "Truck.WaterTemperature", "waterTemperature", "water_temperature", "truck.engineTemperature");
		double num8 = RoadLifeOverlayNumber(tele, "speed_limit_kmh", "navigation.speedLimit", "Navigation.SpeedLimit", "speedLimit");
		if (!double.IsNaN(num8) && num8 > 0.0 && num8 < 3.0)
		{
			num8 *= 3.6;
		}
		if ((double.IsNaN(num8) || double.IsInfinity(num8) || num8 <= 0.0 || num8 > 250.0) && RoadLifeOverlayBoolean(tele, "game.connected", "gameConnected", "connected"))
		{
			num8 = 60.0;
		}
		double num9 = RoadLifeOverlayNumber(tele, "truck.cruiseControlSpeed", "Truck.CruiseControlSpeed", "cruiseControlSpeed", "cruise_speed");
		if (!double.IsNaN(num9) && num9 > 0.0 && num9 < 3.0)
		{
			num9 *= 3.6;
		}
		bool flag = RoadLifeOverlayBoolean(tele, "truck.cruiseControl", "truck.cruiseControlOn", "cruiseControl", "cruise") || (!double.IsNaN(num9) && num9 > 0.1);
		string text5 = RoadLifeOverlayText(tele, "truck.model", "Truck.Model", "truck.name", "Truck.Name", "truckModel", "truck_model");
		string text6 = RoadLifeOverlayText(tele, "truck.make", "Truck.Make", "truck.brand", "Truck.Brand", "truckMake");
		if (string.IsNullOrWhiteSpace(text5))
		{
			text5 = text6;
		}
		else if (!string.IsNullOrWhiteSpace(text6) && text5.IndexOf(text6, StringComparison.OrdinalIgnoreCase) < 0)
		{
			text5 = text6 + " " + text5;
		}
		JObject jObject = new JObject();
		jObject["online"] = true;
		jObject["route"] = new JObject
		{
			["source"] = (string.IsNullOrWhiteSpace(text3) ? "—" : text3),
			["destination"] = (string.IsNullOrWhiteSpace(text4) ? "—" : text4),
			["distance_remaining_km"] = RoadLifeOverlayZero(num2),
			["distance_travelled_km"] = RoadLifeOverlayZero(v)
		};
		jObject["cargo"] = new JObject
		{
			["id"] = text2 ?? string.Empty,
			["name"] = (string.IsNullOrWhiteSpace(text) ? "Sem carga" : text),
			["weight_t"] = (double.IsNaN(num) ? 0.0 : Math.Max(0.0, num / 1000.0))
		};
		jObject["fuel"] = new JObject
		{
			["current_l"] = RoadLifeOverlayZero(num4),
			["capacity_l"] = RoadLifeOverlayZero(num5),
			["percent"] = (double.IsNaN(num6) ? 0.0 : Math.Max(0.0, Math.Min(100.0, num6)))
		};
		jObject["truck"] = new JObject
		{
			["model"] = (string.IsNullOrWhiteSpace(text5) ? "—" : text5),
			["gear"] = RoadLifeOverlayGear(tele),
			["temperature_c"] = RoadLifeOverlayZero(v2),
			["speed_kmh"] = RoadLifeOverlayZero(Math.Abs(num7)),
			["speed_limit_kmh"] = RoadLifeOverlayZero(num8),
			["cruise"] = flag,
			["cruise_speed_kmh"] = RoadLifeOverlayZero(num9)
		};
		JObject jObject2 = jObject;
		string text7;
		if (string.IsNullOrWhiteSpace(_driver))
		{
			text7 = (string.IsNullOrWhiteSpace(_accountUser) ? "—" : _accountUser);
		}
		else
		{
			text7 = _driver;
		}
		jObject2["driver"] = text7;
		jObject["eta"] = RoadLifeOverlayEta(tele);
		jObject["damage"] = new JObject
		{
			["truck_pct"] = RoadLifeOverlayTruckDamage(tele),
			["trailer_pct"] = RoadLifeOverlayDamage(RoadLifeOverlayNumber(tele, "trailerDamage", "trailer_damage", "trailer.wear", "trailer.damage")),
			["cargo_pct"] = RoadLifeOverlayDamage(RoadLifeOverlayNumber(tele, "cargoDamage", "cargo_damage", "job.cargoDamage", "Job.CargoDamage"))
		};
		return jObject;
	}

	private static string RoadLifeOverlayText(JObject root, params string[] paths)
	{
		if (root == null)
		{
			return string.Empty;
		}
		string[] array = paths ?? new string[0];
		foreach (string path in array)
		{
			try
			{
				JToken jToken = root.SelectToken(path, errorWhenNoMatch: false);
				if (jToken != null && jToken.Type != JTokenType.Null)
				{
					string text = (Convert.ToString(jToken, CultureInfo.InvariantCulture) ?? string.Empty).Trim();
					if (text.Length > 0)
					{
						return text;
					}
				}
			}
			catch
			{
			}
		}
		return string.Empty;
	}

	private static double RoadLifeOverlayNumber(JObject root, params string[] paths)
	{
		if (!double.TryParse(RoadLifeOverlayText(root, paths), NumberStyles.Any, CultureInfo.InvariantCulture, out var result))
		{
			return double.NaN;
		}
		return result;
	}

	private static bool RoadLifeOverlayBoolean(JObject root, params string[] paths)
	{
		string text = RoadLifeOverlayText(root, paths).ToLowerInvariant();
		switch (text)
		{
		default:
			return text == "on";
		case "true":
		case "1":
		case "yes":
		case "sim":
			return true;
		}
	}

	private static double RoadLifeOverlayZero(double v)
	{
		if (!double.IsNaN(v) && !double.IsInfinity(v))
		{
			return v;
		}
		return 0.0;
	}

	private static double RoadLifeOverlayDamage(double v)
	{
		if (double.IsNaN(v) || double.IsInfinity(v) || v < 0.0)
		{
			return 0.0;
		}
		if (v <= 1.5)
		{
			v *= 100.0;
		}
		return Math.Max(0.0, Math.Min(100.0, v));
	}

	private static double RoadLifeOverlayTruckDamage(JObject tele)
	{
		string[][] array = new string[5][]
		{
			new string[5] { "wearEngine", "engineWear", "engineDamage", "engine_damage", "truck.wearEngine" },
			new string[5] { "wearTransmission", "transmissionWear", "transmissionDamage", "transmission_damage", "truck.wearTransmission" },
			new string[5] { "wearCabin", "cabinWear", "cabinDamage", "cabin_damage", "truck.wearCabin" },
			new string[5] { "wearChassis", "chassisWear", "chassisDamage", "chassis_damage", "truck.wearChassis" },
			new string[5] { "wearWheels", "wheelsWear", "wheelsDamage", "wheels_damage", "truck.wearWheels" }
		};
		double num = 0.0;
		string[][] array2 = array;
		foreach (string[] paths in array2)
		{
			num = Math.Max(num, RoadLifeOverlayDamage(RoadLifeOverlayNumber(tele, paths)));
		}
		return num;
	}

	private static string RoadLifeOverlayGear(JObject tele)
	{
		string text = RoadLifeOverlayText(tele, "truck.displayedGear", "Truck.DisplayedGear", "truck.gearDashboard", "gearDashboard", "truck.gear", "Truck.Gear", "gear");
		if (string.IsNullOrWhiteSpace(text))
		{
			return "N";
		}
		if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
		{
			return text.ToUpperInvariant();
		}
		if (result < 0)
		{
			return "R" + Math.Abs(result).ToString(CultureInfo.InvariantCulture);
		}
		if (result == 0)
		{
			return "N";
		}
		return "D" + result.ToString(CultureInfo.InvariantCulture);
	}

	private static string RoadLifeOverlayEta(JObject tele)
	{
		string text = RoadLifeOverlayText(tele, "eta", "navigation.eta", "navigation.estimatedTime", "Navigation.EstimatedTime", "job.estimatedTime", "timeRemaining");
		if (string.IsNullOrWhiteSpace(text))
		{
			return "—";
		}
		if (double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var result))
		{
			if (result > 0.0 && result < 604800.0)
			{
				return DateTime.Now.AddSeconds(result).ToString("HH:mm");
			}
			return "—";
		}
		if (DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var result2) && result2.Year >= 2000)
		{
			return result2.ToString("HH:mm");
		}
		if (text.Length <= 12)
		{
			return text;
		}
		return text.Substring(0, 12);
	}

	private static string RoadLifeString(JToken token, string fallback)
	{
		string text = ((token == null) ? string.Empty : Convert.ToString(token, CultureInfo.InvariantCulture));
		if (!string.IsNullOrWhiteSpace(text))
		{
			return text;
		}
		return fallback;
	}

	private static double RoadLifeDouble(JToken token)
	{
		if (token == null || !double.TryParse(Convert.ToString(token, CultureInfo.InvariantCulture), NumberStyles.Any, CultureInfo.InvariantCulture, out var result))
		{
			return 0.0;
		}
		return result;
	}

	private static bool RoadLifeBool(JToken token)
	{
		if (token != null && bool.TryParse(Convert.ToString(token, CultureInfo.InvariantCulture), out var result))
		{
			return result;
		}
		return RoadLifeDouble(token) != 0.0;
	}

	private static string RoadLifeInt(double v)
	{
		return Math.Round(v).ToString("N0", CultureInfo.GetCultureInfo("pt-BR"));
	}

	private static string RoadLifeDec(double v, int digits)
	{
		return v.ToString("N" + digits, CultureInfo.GetCultureInfo("pt-BR"));
	}

	private string RoadLifeThemePrepareXaml(string xaml)
	{
		if (string.IsNullOrEmpty(xaml))
		{
			return xaml;
		}
		xaml = xaml.Replace("Value=\"{StaticResource SoftEdgeBrush}\"", "Value=\"{DynamicResource SoftEdgeBrush}\"");
		xaml = xaml.Replace("Stroke=\"{StaticResource EdgeBrush}\"", "Stroke=\"{DynamicResource EdgeBrush}\"");
		xaml = xaml.Replace("BorderBrush=\"#5536F27A\"", "BorderBrush=\"{DynamicResource SeparatorBrush}\"");
		xaml = xaml.Replace("<SolidColorBrush x:Key=\"SoftEdgeBrush\" Color=\"#B836F27A\"/>", "<SolidColorBrush x:Key=\"SoftEdgeBrush\" Color=\"#B836F27A\"/>\r\n    <SolidColorBrush x:Key=\"SeparatorBrush\" Color=\"#5536F27A\"/>");
		string oldValue = "      <Ellipse Width=\"6\" Height=\"6\" Fill=\"{StaticResource EdgeBrush}\" Margin=\"2,0\"/>\r\n      <Ellipse Width=\"6\" Height=\"6\" Fill=\"{StaticResource EdgeBrush}\" Margin=\"2,0\"/>\r\n      <Ellipse Width=\"6\" Height=\"6\" Fill=\"{StaticResource EdgeBrush}\" Margin=\"2,0\"/>\r\n      <Ellipse Width=\"6\" Height=\"6\" Fill=\"{StaticResource EdgeBrush}\" Margin=\"2,0\"/>";
		string newValue = "      <Ellipse Name=\"ThemeGreen\" Width=\"10\" Height=\"10\" Fill=\"#FF36F27A\" Stroke=\"White\" StrokeThickness=\"0.5\" Margin=\"3,0\" Cursor=\"Hand\" ToolTip=\"Verde\"/>\r\n      <Ellipse Name=\"ThemeBlue\" Width=\"10\" Height=\"10\" Fill=\"#FF2196F3\" Stroke=\"White\" StrokeThickness=\"0.5\" Margin=\"3,0\" Cursor=\"Hand\" ToolTip=\"Azul\"/>\r\n      <Ellipse Name=\"ThemeYellow\" Width=\"10\" Height=\"10\" Fill=\"#FFFFD43B\" Stroke=\"White\" StrokeThickness=\"0.5\" Margin=\"3,0\" Cursor=\"Hand\" ToolTip=\"Amarelo\"/>\r\n      <Ellipse Name=\"ThemeRed\" Width=\"10\" Height=\"10\" Fill=\"#FFFF4545\" Stroke=\"White\" StrokeThickness=\"0.5\" Margin=\"3,0\" Cursor=\"Hand\" ToolTip=\"Vermelho\"/>";
		return xaml.Replace(oldValue, newValue);
	}

	private void RoadLifeThemeWire()
	{
		WireRoadLifeTheme("ThemeGreen", "#FF36F27A");
		WireRoadLifeTheme("ThemeBlue", "#FF2196F3");
		WireRoadLifeTheme("ThemeYellow", "#FFFFD43B");
		WireRoadLifeTheme("ThemeRed", "#FFFF4545");
	}

	private void WireRoadLifeTheme(string name, string color)
	{
		if (_roadLifeOverlay != null && _roadLifeOverlay.FindName(name) is Ellipse ellipse)
		{
			ellipse.PreviewMouseLeftButtonDown += (object sender, MouseButtonEventArgs e) =>
			{
				e.Handled = true;
				SetRoadLifeTheme(color);
			};
		}
	}

	private void SetRoadLifeTheme(string edgeHex)
	{
		if (_roadLifeOverlay != null)
		{
			System.Windows.Media.Color color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(edgeHex);
			_roadLifeOverlay.Resources["EdgeBrush"] = new SolidColorBrush(color);
			_roadLifeOverlay.Resources["FuelFillBrush"] = new SolidColorBrush(color);
			_roadLifeOverlay.Resources["SoftEdgeBrush"] = new SolidColorBrush(System.Windows.Media.Color.FromArgb(184, color.R, color.G, color.B));
			_roadLifeOverlay.Resources["SeparatorBrush"] = new SolidColorBrush(System.Windows.Media.Color.FromArgb(85, color.R, color.G, color.B));
		}
	}

	[DllImport("winmm.dll", CharSet = CharSet.Auto)]
	private static extern int mciSendString(string command, StringBuilder returnValue, int returnLength, IntPtr winHandle);

	private static string VoiceCleanRoot()
	{
		return System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "voz");
	}

	private static string VoiceCleanLimitsDir()
	{
		return System.IO.Path.Combine(VoiceCleanRoot(), "limites");
	}

	private static string VoiceCleanEventsDir()
	{
		return System.IO.Path.Combine(VoiceCleanRoot(), "eventos");
	}

	private static string VoiceCleanSettingsFile()
	{
		string text = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GAT-LOG", "GAT-Telemetria-CleanVoice");
		Directory.CreateDirectory(text);
		return System.IO.Path.Combine(text, "voice-clean-v1.txt");
	}

	private void VoiceCleanInitialize()
	{
		if (_voiceCleanInitialized)
		{
			return;
		}
		_voiceCleanInitialized = true;
		VoiceCleanLoadSettings();
		_voiceCleanHttp = new HttpClient
		{
			Timeout = TimeSpan.FromSeconds(2.0)
		};
		_voiceCleanTimer = new System.Windows.Forms.Timer
		{
			Interval = 350
		};
		_voiceCleanTimer.Tick += async delegate
		{
			await VoiceCleanPoll();
		};
		_voiceCleanTimer.Start();
		FormClosed += delegate
		{
			try
			{
				_voiceCleanTimer?.Stop();
				_voiceCleanTimer?.Dispose();
			}
			catch
			{
			}
			try
			{
				_voiceCleanHttp?.Dispose();
			}
			catch
			{
			}
			VoiceCleanStop();
			try
			{
				_voiceCleanSpeech?.Dispose();
				_voiceCleanSpeech = null;
			}
			catch
			{
			}
		};
		ClientStore.Log("VOZ NOVA 1.0.68.8 iniciada; DASH visual; pasta=" + VoiceCleanRoot());
	}

	private void VoiceCleanLoadSettings()
	{
		_voiceCleanMuted = false;
		_voiceCleanVolume = 80;
		try
		{
			string path = VoiceCleanSettingsFile();
			if (File.Exists(path))
			{
				string[] array = File.ReadAllText(path).Trim().Split('|');
				if (array.Length != 0 && int.TryParse(array[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
				{
					_voiceCleanVolume = Math.Max(0, Math.Min(100, result));
				}
				if (array.Length > 1)
				{
					_voiceCleanMuted = string.Equals(array[1], "1", StringComparison.Ordinal);
				}
			}
		}
		catch
		{
			_voiceCleanMuted = false;
			_voiceCleanVolume = 80;
		}
	}

	private void VoiceCleanSaveSettings()
	{
		try
		{
			File.WriteAllText(VoiceCleanSettingsFile(), _voiceCleanVolume.ToString(CultureInfo.InvariantCulture) + "|" + (_voiceCleanMuted ? "1" : "0"));
		}
		catch
		{
		}
	}

	private async Task VoiceCleanPoll()
	{
		if (_voiceCleanBusy || _voiceCleanHttp == null)
		{
			return;
		}
		_voiceCleanBusy = true;
		try
		{
			JObject jObject = JObject.Parse(await _voiceCleanHttp.GetStringAsync("http://127.0.0.1:31377/api/ets2/telemetry"));
			TelemetryEngine.Normalize(jObject);
			VoiceCleanObserveRoad(jObject, DateTime.UtcNow);
			if (!_voiceCleanConnectedLogged)
			{
				_voiceCleanConnectedLogged = true;
				ClientStore.Log("VOZ NOVA 1.0.68.8 TruckSim GPS conectado diretamente.");
			}
		}
		catch (Exception ex)
		{
			if ((DateTime.UtcNow - _voiceCleanLastError).TotalSeconds >= 15.0)
			{
				_voiceCleanLastError = DateTime.UtcNow;
				ClientStore.Log("VOZ NOVA 1.0.68.8 TruckSim indisponivel: " + ex.Message);
			}
		}
		finally
		{
			_voiceCleanBusy = false;
		}
	}

	private static double VoiceCleanNumber(JObject root, params string[] paths)
	{
		if (root == null)
		{
			return double.NaN;
		}
		string[] array = paths ?? new string[0];
		foreach (string path in array)
		{
			try
			{
				JToken jToken = root.SelectToken(path, errorWhenNoMatch: false);
				if (jToken == null || jToken.Type == JTokenType.Null || !double.TryParse(Convert.ToString(jToken, CultureInfo.InvariantCulture), NumberStyles.Any, CultureInfo.InvariantCulture, out var result))
				{
					continue;
				}
				return result;
			}
			catch
			{
			}
		}
		return double.NaN;
	}

	private static int VoiceCleanEventLimit(double realLimit)
	{
		if (double.IsNaN(realLimit) || double.IsInfinity(realLimit) || realLimit <= 0.0)
		{
			return 0;
		}
		int num = (int)Math.Round(realLimit, MidpointRounding.AwayFromZero);
		int[] voiceCleanLimits = VoiceCleanLimits;
		foreach (int num2 in voiceCleanLimits)
		{
			if (num == num2)
			{
				return num2;
			}
		}
		int num3 = (int)Math.Round(realLimit / 10.0, MidpointRounding.AwayFromZero) * 10;
		if (num3 < 20 || num3 > 130)
		{
			return 0;
		}
		if (Math.Abs(realLimit - (double)num3) <= 5.1)
		{
			return num3;
		}
		return 0;
	}

	private void VoiceCleanObserveRoad(JObject tele, DateTime now)
	{
		if (tele == null || _voiceCleanMuted || _voiceCleanVolume <= 0)
		{
			return;
		}
		double num = VoiceCleanNumber(tele, "truck.speed", "Truck.Speed");
		double num2 = VoiceCleanNumber(tele, "speed_limit_kmh", "navigation.speedLimit", "Navigation.SpeedLimit");
		bool flag = VoiceCleanBoolean(tele, "speed_limit_is_fallback");
		if (double.IsNaN(num) || double.IsNaN(num2) || num2 <= 0.0)
		{
			_voiceCleanOver = false;
			_voiceCleanLastEventLimit = 0;
			_voiceCleanLastRealLimit = double.NaN;
			return;
		}
		num = Math.Abs(num);
		int num3 = VoiceCleanEventLimit(num2);
		if (num3 <= 0)
		{
			_voiceCleanOver = false;
			return;
		}
		bool flag2 = num > num2 + 1.0;
		bool flag3 = _voiceCleanLastEventLimit > 0 && num3 != _voiceCleanLastEventLimit;
		bool flag4 = !double.IsNaN(_voiceCleanLastRealLimit) && Math.Abs(num2 - _voiceCleanLastRealLimit) >= 1.0;
		if (!flag2)
		{
			_voiceCleanOver = false;
			_voiceCleanLastEventLimit = num3;
			_voiceCleanLastRealLimit = num2;
			return;
		}
		if (flag3 | flag4)
		{
			_voiceCleanOver = false;
		}
		if (!_voiceCleanOver || (now - _voiceCleanLastAlert).TotalSeconds >= 15.0)
		{
			string text = "limite_" + num3.ToString("D3", CultureInfo.InvariantCulture);
			if (VoiceCleanPlayEvent(text, interrupt: true))
			{
				_voiceCleanOver = true;
				_voiceCleanLastAlert = now;
				ClientStore.Log("VOZ NOVA excesso: velocidade=" + Math.Round(num).ToString(CultureInfo.InvariantCulture) + " limiteReal=" + Math.Round(num2).ToString(CultureInfo.InvariantCulture) + " evento=" + text);
			}
			else
			{
				ClientStore.Log("VOZ NOVA arquivo ausente/falhou: " + text + ".mp3");
			}
		}
		_voiceCleanLastEventLimit = num3;
		_voiceCleanLastRealLimit = num2;
	}

	private static bool VoiceCleanBoolean(JObject root, params string[] paths)
	{
		if (root == null)
		{
			return false;
		}
		string[] array = paths ?? new string[0];
		foreach (string path in array)
		{
			try
			{
				JToken jToken = root.SelectToken(path, errorWhenNoMatch: false);
				if (jToken != null && jToken.Type != JTokenType.Null)
				{
					if (jToken.Type == JTokenType.Boolean)
					{
						return jToken.Value<bool>();
					}
					if (bool.TryParse(jToken.ToString(), out var result))
					{
						return result;
					}
				}
			}
			catch
			{
			}
		}
		return false;
	}

	private static string VoiceCleanEventFile(string eventName)
	{
		if (string.IsNullOrWhiteSpace(eventName))
		{
			return string.Empty;
		}
		string text = eventName.Trim().ToLowerInvariant();
		if (text.StartsWith("limite_", StringComparison.Ordinal))
		{
			return System.IO.Path.Combine(VoiceCleanLimitsDir(), text + ".mp3");
		}
		return System.IO.Path.Combine(VoiceCleanEventsDir(), text + ".mp3");
	}

	private bool VoiceCleanPlayEvent(string eventName, bool interrupt)
	{
		if (_voiceCleanMuted || _voiceCleanVolume <= 0)
		{
			return false;
		}
		string text = VoiceCleanEventFile(eventName);
		if (string.IsNullOrWhiteSpace(text) || !File.Exists(text))
		{
			return false;
		}
		try
		{
			if (interrupt)
			{
				VoiceCleanStop();
			}
			else if (!string.IsNullOrWhiteSpace(_voiceCleanAlias))
			{
				return false;
			}
			string alias = "gatcleanvoice_" + Interlocked.Increment(ref _voiceCleanSerial).ToString(CultureInfo.InvariantCulture);
			string text2 = text.Replace("\"", "");
			int num = mciSendString("open \"" + text2 + "\" type mpegvideo alias " + alias, null, 0, IntPtr.Zero);
			if (num != 0)
			{
				ClientStore.Log("VOZ NOVA MCI open erro=" + num + " arquivo=" + System.IO.Path.GetFileName(text));
				return false;
			}
			mciSendString("setaudio " + alias + " volume to " + _voiceCleanVolume * 10, null, 0, IntPtr.Zero);
			int num2 = mciSendString("play " + alias, null, 0, IntPtr.Zero);
			if (num2 != 0)
			{
				mciSendString("close " + alias, null, 0, IntPtr.Zero);
				ClientStore.Log("VOZ NOVA MCI play erro=" + num2 + " arquivo=" + System.IO.Path.GetFileName(text));
				return false;
			}
			_voiceCleanAlias = alias;
			ClientStore.Log("VOZ NOVA tocando evento=" + eventName + " arquivo=" + System.IO.Path.GetFileName(text));
			Task.Run(async () =>
			{
				await Task.Delay(10000);
				try
				{
					mciSendString("close " + alias, null, 0, IntPtr.Zero);
					if (_voiceCleanAlias == alias)
					{
						_voiceCleanAlias = string.Empty;
					}
				}
				catch
				{
				}
			});
			return true;
		}
		catch (Exception ex)
		{
			ClientStore.Log("VOZ NOVA falha player: " + ex.Message);
			return false;
		}
	}

	private void VoiceCleanStop()
	{
		try
		{
			_voiceCleanSpeech?.SpeakAsyncCancelAll();
			string voiceCleanAlias = _voiceCleanAlias;
			_voiceCleanAlias = string.Empty;
			if (!string.IsNullOrWhiteSpace(voiceCleanAlias))
			{
				mciSendString("stop " + voiceCleanAlias, null, 0, IntPtr.Zero);
				mciSendString("close " + voiceCleanAlias, null, 0, IntPtr.Zero);
			}
		}
		catch
		{
		}
	}

	private bool VoiceCleanHandleLegacy(string text, string logLabel)
	{
		string text2 = ((logLabel ?? string.Empty) + " " + (text ?? string.Empty)).ToLowerInvariant();
		if ((text2.Contains("trabalho") || text2.Contains("carga")) && (text2.Contains("iniciado") || text2.Contains("iniciad") || text2.Contains("pegou")))
		{
			return VoiceCleanPlayEvent("trabalho_iniciado", interrupt: true);
		}
		if ((text2.Contains("trabalho") || text2.Contains("entrega") || text2.Contains("carga")) && (text2.Contains("finalizado") || text2.Contains("conclu") || text2.Contains("entreg")))
		{
			return VoiceCleanPlayEvent("trabalho_finalizado", interrupt: true);
		}
		return false;
	}

	private void BuildVoiceCleanSettings(System.Windows.Forms.Panel p)
	{
		VoiceCleanLoadSettings();
		System.Windows.Forms.Panel panel = new System.Windows.Forms.Panel
		{
			Left = 0,
			Top = 252,
			Width = Math.Max(520, p.Width),
			Height = 132,
			Anchor = (AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right),
			BackColor = System.Drawing.Color.FromArgb(5, 20, 36)
		};
		panel.Controls.Add(new System.Windows.Forms.Label
		{
			Text = "VOZ LOCAL • MOTOR NOVO LIMPO",
			Left = 14,
			Top = 8,
			Width = 290,
			Height = 22,
			ForeColor = System.Drawing.Color.FromArgb(100, 180, 255),
			Font = new Font("Segoe UI Semibold", 9.5f, System.Drawing.FontStyle.Bold)
		});
		_voiceCleanVolumeLabel = new System.Windows.Forms.Label
		{
			Text = "Volume: " + _voiceCleanVolume + "%",
			Left = 14,
			Top = 39,
			Width = 115,
			Height = 24,
			ForeColor = System.Drawing.Color.Gainsboro
		};
		panel.Controls.Add(_voiceCleanVolumeLabel);
		_voiceCleanVolumeBar = new TrackBar
		{
			Left = 125,
			Top = 30,
			Width = 240,
			Minimum = 0,
			Maximum = 100,
			Value = _voiceCleanVolume,
			TickFrequency = 10,
			SmallChange = 5,
			LargeChange = 10
		};
		_voiceCleanVolumeBar.Scroll += delegate
		{
			_voiceCleanVolume = _voiceCleanVolumeBar.Value;
			_voiceCleanMuted = _voiceCleanVolume <= 0;
			VoiceCleanSaveSettings();
			VoiceCleanRefreshUi();
			try
			{
				if (!string.IsNullOrWhiteSpace(_voiceCleanAlias))
				{
					mciSendString("setaudio " + _voiceCleanAlias + " volume to " + _voiceCleanVolume * 10, null, 0, IntPtr.Zero);
				}
			}
			catch
			{
			}
		};
		panel.Controls.Add(_voiceCleanVolumeBar);
		System.Windows.Forms.Button button = HubButton041("TESTAR VOZ", 120);
		button.Left = 380;
		button.Top = 31;
		button.Click += delegate
		{
			_voiceCleanMuted = false;
			if (_voiceCleanVolume <= 0)
			{
				_voiceCleanVolume = 80;
			}
			VoiceCleanSaveSettings();
			VoiceCleanRefreshUi();
			if (!VoiceCleanPlayEvent("limite_080", interrupt: true))
			{
				System.Windows.Forms.MessageBox.Show("Nao foi possivel tocar voz\\limites\\limite_080.mp3.", "Voz GAT", MessageBoxButtons.OK, MessageBoxIcon.Hand);
			}
		};
		panel.Controls.Add(button);
		System.Windows.Forms.Button button2 = HubButton041("ABRIR PASTA VOZ", 150);
		button2.Left = 510;
		button2.Top = 31;
		button2.Click += delegate
		{
			try
			{
				Process.Start("explorer.exe", VoiceCleanRoot());
			}
			catch (Exception ex)
			{
				System.Windows.Forms.MessageBox.Show(ex.Message, "Voz GAT", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
			}
		};
		panel.Controls.Add(button2);
		_voiceCleanStatusLabel = new System.Windows.Forms.Label
		{
			Left = 14,
			Top = 78,
			Width = Math.Max(480, panel.Width - 28),
			Height = 45,
			Anchor = (AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right),
			ForeColor = System.Drawing.Color.FromArgb(145, 175, 205)
		};
		panel.Controls.Add(_voiceCleanStatusLabel);
		p.Controls.Add(panel);
		VoiceCleanRefreshUi();
	}

	private void VoiceCleanRefreshUi()
	{
		try
		{
			if (_voiceCleanVolumeLabel != null)
			{
				_voiceCleanVolumeLabel.Text = "Volume: " + _voiceCleanVolume + "%";
			}
			if (_voiceCleanVolumeBar != null && _voiceCleanVolumeBar.Value != _voiceCleanVolume)
			{
				_voiceCleanVolumeBar.Value = Math.Max(0, Math.Min(100, _voiceCleanVolume));
			}
			int num = 0;
			int[] voiceCleanLimits = VoiceCleanLimits;
			foreach (int num2 in voiceCleanLimits)
			{
				if (File.Exists(System.IO.Path.Combine(VoiceCleanLimitsDir(), "limite_" + num2.ToString("D3", CultureInfo.InvariantCulture) + ".mp3")))
				{
					num++;
				}
			}
			if (_voiceCleanStatusLabel != null)
			{
				_voiceCleanStatusLabel.Text = num + "/12 limites instalados • TruckSim GPS direto • DASH somente visual\r\nPasta oficial: " + VoiceCleanRoot();
			}
		}
		catch
		{
		}
	}
}

