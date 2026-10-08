using System;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GatTelemetry;

internal sealed class VideoOverlay041 : Form
{
	private readonly HttpClient _http = new HttpClient
	{
		Timeout = TimeSpan.FromSeconds(4.0)
	};

	private readonly WebView2 _web = new WebView2();

	private bool _ready;

	private bool _dragging044;

	private Point _dragOffset044;

	private const int WM_NCHITTEST_044 = 132;

	private const int HTCLIENT_044 = 1;

	private const int HTLEFT_044 = 10;

	private const int HTRIGHT_044 = 11;

	private const int HTTOP_044 = 12;

	private const int HTTOPLEFT_044 = 13;

	private const int HTTOPRIGHT_044 = 14;

	private const int HTBOTTOM_044 = 15;

	private const int HTBOTTOMLEFT_044 = 16;

	private const int HTBOTTOMRIGHT_044 = 17;

	internal VideoOverlay041()
	{
		Text = "ROADLIFE DASH • Sobreposição de vídeo";
		StartPosition = FormStartPosition.Manual;
		Size = new Size(500, 320);
		MinimumSize = new Size(260, 160);
		Location = new Point(Math.Max(0, Screen.PrimaryScreen.WorkingArea.Right - 530), 80);
		RestoreVideoBounds053();
		BackColor = Color.Black;
		TopMost = true;
		ShowInTaskbar = true;
		FormBorderStyle = FormBorderStyle.None;
		MinimizeBox = true;
		MaximizeBox = false;
		ControlBox = true;
		Padding = new Padding(5);
		_web.Dock = DockStyle.Fill;
		_web.BackColor = Color.Black;
		Controls.Add(_web);
		Shown += async delegate
		{
			await InitAsync();
		};
		FormClosing += delegate
		{
			SaveVideoBounds053();
		};
		FormClosed += delegate
		{
			try
			{
				_web.Dispose();
				_http.Dispose();
			}
			catch
			{
			}
		};
	}

	private void RestoreVideoBounds053()
	{
		try
		{
			string path = Path.Combine(Application.LocalUserAppDataPath, "video-overlay-bounds-v1.txt");
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

	private void SaveVideoBounds053()
	{
		try
		{
			Rectangle rectangle = ((WindowState == FormWindowState.Normal) ? Bounds : RestoreBounds);
			if (rectangle.Width >= MinimumSize.Width && rectangle.Height >= MinimumSize.Height)
			{
				File.WriteAllText(Path.Combine(Application.LocalUserAppDataPath, "video-overlay-bounds-v1.txt"), string.Join("|", rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height));
			}
		}
		catch
		{
		}
	}

	private async Task InitAsync()
	{
		if (_ready)
		{
			return;
		}
		try
		{
			string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GAT-LOG", "GAT-Telemetria", "VideoOverlay-1.0.44");
			string www = Path.Combine(path, "www");
			Directory.CreateDirectory(www);
			File.WriteAllText(Path.Combine(www, "index.html"), Html(), Encoding.UTF8);
			CoreWebView2Environment environment = await CoreWebView2Environment.CreateAsync(null, Path.Combine(path, "WebView2"));
			await _web.EnsureCoreWebView2Async(environment);
			_web.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
			_web.CoreWebView2.Settings.AreDevToolsEnabled = false;
			_web.CoreWebView2.SetVirtualHostNameToFolderMapping("gatoverlay.local", www, CoreWebView2HostResourceAccessKind.Allow);
			_web.CoreWebView2.WebMessageReceived += async (object sender, CoreWebView2WebMessageReceivedEventArgs e) =>
			{
				_ = 1;
				try
				{
					JObject jObject = JObject.Parse(e.WebMessageAsJson);
					string text = Convert.ToString(jObject["type"]) ?? string.Empty;
					switch (text)
					{
					case "windowMinimize061":
						WindowState = FormWindowState.Minimized;
						return;
					case "windowToggleMax061":
						WindowState = ((WindowState != FormWindowState.Maximized) ? FormWindowState.Maximized : FormWindowState.Normal);
						return;
					case "mediaRefresh":
						await PushMediaAsync();
						return;
					case "dragStart044":
						_dragging044 = true;
						_dragOffset044 = new Point(Cursor.Position.X - Left, Cursor.Position.Y - Top);
						return;
					case "dragMove044":
						if (_dragging044)
						{
							Location = new Point(Cursor.Position.X - _dragOffset044.X, Cursor.Position.Y - _dragOffset044.Y);
							return;
						}
						break;
					}
					switch (text)
					{
					case "dragEnd044":
						_dragging044 = false;
						break;
					case "closeWindow044":
						Close();
						break;
					case "mediaMode":
					{
						string text2 = Convert.ToString(jObject["mode"]) ?? string.Empty;
						text2 = text2.Trim().ToLowerInvariant();
						if (text2 == "gat" || text2 == "mine")
						{
							File.WriteAllText(Path.Combine(Application.LocalUserAppDataPath, "radio-active-mode.txt"), text2);
						}
						await PushMediaAsync();
						break;
					}
					}
				}
				catch
				{
				}
			};
			_web.Source = new Uri("https://gatoverlay.local/index.html?v=1044");
			_ready = true;
		}
		catch
		{
		}
	}

	private async Task PushMediaAsync()
	{
		if (!_ready || _web.CoreWebView2 == null)
		{
			return;
		}
		try
		{
			string value = await _http.GetStringAsync("http://127.0.0.1:31378/api/gat/media?t=" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
			await _web.CoreWebView2.ExecuteScriptAsync("window.pushMedia(" + JsonConvert.SerializeObject(value) + ")");
		}
		catch
		{
			try
			{
				await _web.CoreWebView2.ExecuteScriptAsync("window.mediaError()");
			}
			catch
			{
			}
		}
	}

	private static string Html()
	{
		return "<!doctype html><html><head><meta charset='utf-8'><meta name='viewport' content='width=device-width,initial-scale=1'><style>\r\n*{box-sizing:border-box}html,body{margin:0;width:100%;height:100%;background:#02060c;color:#eaf2ff;font-family:Segoe UI,Arial,sans-serif;overflow:hidden;user-select:none}.app{height:100%;display:grid;grid-template-rows:40px 1fr}.top{display:flex;align-items:center;gap:7px;padding:0 7px 0 10px;background:linear-gradient(90deg,#061525,#071c31);border-bottom:1px solid #17683e;cursor:move}.brand{font-weight:800;color:#36f27a;margin-right:auto;letter-spacing:.2px;pointer-events:none}.tab,.win{border:1px solid #28784a;background:#0a2b1b;color:#d4e8ff;border-radius:5px;height:25px;padding:0 10px;font-size:11px;cursor:pointer}.tab.on{background:#17683e;color:#fff}.win{width:30px;padding:0;font-size:16px;line-height:22px}.win:hover{background:#143b5e}.win.close:hover{background:#b4232c;border-color:#e14951;color:#fff}.stage{position:relative;background:#000}.stage iframe{border:0;width:100%;height:100%}.empty{position:absolute;inset:0;display:flex;align-items:center;justify-content:center;text-align:center;color:#8ca8c7;padding:20px}.hidden{display:none}</style></head><body><div class='app'><div id='bar' class='top'><span class='brand'>ROADLIFE • VÍDEO</span><button id='gat' class='tab'>Canal RoadLife</button><button id='mine' class='tab'>Meu Vídeo</button><button id='min' class='win' title='Minimizar'>—</button><button id='close' class='win close' title='Fechar'>×</button></div><div class='stage'><iframe id='frame' allow='autoplay; encrypted-media; picture-in-picture' allowfullscreen></iframe><div id='empty' class='empty'>Aguardando Rádio/TV RoadLife...</div></div></div><script>\r\nconst state={mode:'gat',media:null,embed:''};const post=o=>{try{chrome.webview.postMessage(o)}catch{}};function yt(url){try{const u=new URL(url),h=u.hostname.toLowerCase(),list=u.searchParams.get('list');const s='&autoplay=1&rel=0&playsinline=1';if(list)return 'https://www.youtube.com/embed/videoseries?list='+encodeURIComponent(list)+s;let id=h==='youtu.be'?u.pathname.slice(1).split('/')[0]:u.searchParams.get('v')||'';if(!id){const p=u.pathname.split('/').filter(Boolean);if(p.length>1)id=p[1]}return /^[A-Za-z0-9_-]{11}$/.test(id)?'https://www.youtube.com/embed/'+id+'?autoplay=1&rel=0&playsinline=1':''}catch{return ''}}function pick(){const m=state.media||{};if(state.mode==='mine')return(m.myVideo||{}).url||'';return(m.channelGat&&m.channelGat.enabled)?m.channelGat.url||'':''}function render(){['gat','mine'].forEach(x=>document.getElementById(x).classList.toggle('on',x===state.mode));const url=String(pick()||'').trim(),f=document.getElementById('frame'),e=document.getElementById('empty');if(!url){f.classList.add('hidden');e.classList.remove('hidden');e.textContent='Nenhuma fonte ativa na Rádio/TV RoadLife.';return}const src=yt(url)||url;if(src!==state.embed){state.embed=src;f.src=src}f.classList.remove('hidden');e.classList.add('hidden')}window.pushMedia=p=>{let m=p;if(typeof m==='string'){try{m=JSON.parse(m)}catch{return}}state.media=m;const mode=String(m.activeMode||'');state.mode=mode==='mine'?'mine':'gat';render()};window.mediaError=()=>{document.getElementById('empty').textContent='Abra o ROADLIFE TELEMETRIA e confira a Rádio RoadLife.'};['gat','mine'].forEach(x=>document.getElementById(x).onclick=()=>{state.mode=x;post({type:'mediaMode',mode:x});render()});document.getElementById('min').onclick=e=>{e.stopPropagation();post({type:'windowMinimize061'})};document.getElementById('close').onclick=e=>{e.stopPropagation();post({type:'closeWindow044'})};const bar=document.getElementById('bar');bar.addEventListener('mousedown',e=>{if(e.button===0&&!e.target.closest('button'))post({type:'dragStart044'})});window.addEventListener('mousemove',()=>post({type:'dragMove044'}));window.addEventListener('mouseup',()=>post({type:'dragEnd044'}));bar.addEventListener('dblclick',e=>{if(!e.target.closest('button'))post({type:'windowToggleMax061'})});post({type:'mediaRefresh'});setInterval(()=>post({type:'mediaRefresh'}),3000);\r\n</script></body></html>";
	}

	protected override void WndProc(ref Message m)
	{
		base.WndProc(ref m);
		if (m.Msg == 132 && (int)m.Result == 1)
		{
			long num = m.LParam.ToInt64();
			int num2 = (short)(num & 0xFFFF);
			int num3 = (short)((num >> 16) & 0xFFFF);
			Point point = PointToClient(new Point(num2, num3));
			bool flag = point.X <= 7;
			bool flag2 = point.X >= ClientSize.Width - 7;
			bool flag3 = point.Y <= 7;
			bool flag4 = point.Y >= ClientSize.Height - 7;
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
		}
	}
}
