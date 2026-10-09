using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace GatTelemetry;

internal sealed class DashOverlay060 : Form
{
	private const int DesignWidth = 1600;

	private const int DesignHeight = 700;

	private readonly WebView2 _web;

	private readonly EventHandler<CoreWebView2WebMessageReceivedEventArgs> _messageHandler;

	private bool _initializing;

	public bool IsReady { get; private set; }

	private string StatePath => Path.Combine(Application.LocalUserAppDataPath, "dash-full-overlay-1.0.60.txt");

	public DashOverlay060(EventHandler<CoreWebView2WebMessageReceivedEventArgs> messageHandler)
	{
		_messageHandler = messageHandler;
		Text = "ROADLIFE DASH • Completo";
		BackColor = Color.FromArgb(2, 10, 20);
		FormBorderStyle = FormBorderStyle.Sizable;
		MinimizeBox = true;
		MaximizeBox = true;
		ControlBox = true;
		ShowInTaskbar = true;
		TopMost = true;
		MinimumSize = new Size(660, 350);
		StartPosition = FormStartPosition.CenterScreen;
		ClientSize = new Size(1280, 560);
		_web = new WebView2
		{
			BackColor = Color.FromArgb(2, 10, 20)
		};
		Controls.Add(_web);
		Resize += delegate
		{
			LayoutDashboard();
		};
		ResizeEnd += delegate
		{
			SaveWindowState();
		};
		FormClosing += delegate
		{
			SaveWindowState();
		};
		LoadWindowState();
		LayoutDashboard();
	}

	public async Task InitializeAsync()
	{
		if (IsReady || _initializing)
		{
			return;
		}
		_initializing = true;
		try
		{
			string www = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "gatdash-www");
			if (!File.Exists(Path.Combine(www, "index.html")))
			{
				throw new FileNotFoundException("Arquivos do ROADLIFE DASH não foram encontrados.");
			}
			string text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GAT-LOG", "GAT-Telemetria", "DashFullWebView2-1.0.60");
			Directory.CreateDirectory(text);
			CoreWebView2Environment environment = await CoreWebView2Environment.CreateAsync(null, text);
			await _web.EnsureCoreWebView2Async(environment);
			_web.CoreWebView2.Settings.AreDevToolsEnabled = false;
			_web.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
			_web.CoreWebView2.Settings.IsZoomControlEnabled = false;
			_web.CoreWebView2.SetVirtualHostNameToFolderMapping("gatdash-full.local", www, CoreWebView2HostResourceAccessKind.Allow);
			if (_messageHandler != null)
			{
				_web.CoreWebView2.WebMessageReceived += _messageHandler;
			}
			IsReady = true;
			LayoutDashboard();
			_web.Source = new Uri("https://gatdash-full.local/index.html?full=1060");
		}
		finally
		{
			_initializing = false;
		}
	}

	public async Task ExecuteScriptAsync(string js)
	{
		if (IsReady && _web?.CoreWebView2 != null && !string.IsNullOrWhiteSpace(js))
		{
			await _web.CoreWebView2.ExecuteScriptAsync(js);
		}
	}

	private void LayoutDashboard()
	{
		if (WindowState == FormWindowState.Minimized || ClientSize.Width <= 0 || ClientSize.Height <= 0)
		{
			return;
		}
		double val = (double)ClientSize.Width / 1600.0;
		double val2 = (double)ClientSize.Height / 700.0;
		double val3 = Math.Min(val, val2);
		val3 = Math.Max(0.25, Math.Min(2.0, val3));
		int num = Math.Max(1, (int)Math.Round(1600.0 * val3));
		int num2 = Math.Max(1, (int)Math.Round(700.0 * val3));
		int num3 = Math.Max(0, (ClientSize.Width - num) / 2);
		int num4 = Math.Max(0, (ClientSize.Height - num2) / 2);
		_web.Bounds = new Rectangle(num3, num4, num, num2);
		try
		{
			if (Math.Abs(_web.ZoomFactor - val3) > 0.002)
			{
				_web.ZoomFactor = val3;
			}
		}
		catch
		{
		}
	}

	private void SaveWindowState()
	{
		try
		{
			if (WindowState == FormWindowState.Normal)
			{
				Rectangle bounds = Bounds;
				File.WriteAllText(StatePath, string.Join("|", bounds.X, bounds.Y, bounds.Width, bounds.Height));
			}
		}
		catch
		{
		}
	}

	private void LoadWindowState()
	{
		try
		{
			if (!File.Exists(StatePath))
			{
				return;
			}
			string[] array = File.ReadAllText(StatePath).Split('|');
			if (array.Length != 4 || !int.TryParse(array[0], out var result) || !int.TryParse(array[1], out var result2) || !int.TryParse(array[2], out var result3) || !int.TryParse(array[3], out var result4))
			{
				return;
			}
			result3 = Math.Max(MinimumSize.Width, result3);
			result4 = Math.Max(MinimumSize.Height, result4);
			Rectangle rectangle = new Rectangle(result, result2, result3, result4);
			bool flag = false;
			Screen[] allScreens = Screen.AllScreens;
			foreach (Screen screen in allScreens)
			{
				if (Rectangle.Intersect(rectangle, screen.WorkingArea).Width >= 120 && Rectangle.Intersect(rectangle, screen.WorkingArea).Height >= 80)
				{
					flag = true;
					break;
				}
			}
			if (flag)
			{
				StartPosition = FormStartPosition.Manual;
				Bounds = rectangle;
			}
		}
		catch
		{
		}
	}
}
