using System;
using System.Collections.Generic;
using System.Diagnostics;
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

internal sealed class RadioForm : Form
{
	private const string RadioEndpoint = "https://api.gatlogets2.com.br/api/public/radio";

	private const string VirtualHost = "radio.gatlogets2.local";

	private readonly HttpClient _http = new HttpClient
	{
		Timeout = TimeSpan.FromSeconds(7.0)
	};

	private const string ChannelAdminEndpoint049 = "https://api.gatlogets2.com.br/api/site/admin/radio";

	private const string AccountSessionEndpoint049 = "https://api.gatlogets2.com.br/api/account/session";

	private string _accountUser049 = string.Empty;

	private string _accountToken049 = string.Empty;

	private string _accountRole049 = string.Empty;

	private readonly Timer _pollTimer = new Timer
	{
		Interval = 15000
	};

	private readonly WebView2 _web = new WebView2();

	private readonly Label _title = new Label();

	private readonly Label _description = new Label();

	private readonly Label _state = new Label();

	private readonly Label _track = new Label();

	private readonly Label _source = new Label();

	private readonly Label _personalLabel = new Label();

	private readonly Button _channelGat = new Button();

	private readonly Button _myRadio = new Button();

	private readonly Button _siteWeb = new Button();

	private readonly Button _loadPersonal = new Button();

	private readonly Button _toggle = new Button();

	private readonly Button _openYoutube = new Button();

	private readonly Button _fullScreen = new Button();

	private readonly Button _overlay = new Button();

	private readonly TextBox _personalInput = new TextBox();

	private readonly TrackBar _volume = new TrackBar();

	private bool _browserReady;

	private bool _playerReady;

	private bool _listening;

	private bool _fullScreenMode;

	private bool _overlayMode;

	private bool _personalMode;

	private bool _webMode;

	private Rectangle _normalBounds;

	private bool _serverEnabled;

	private string _serverSourceType = string.Empty;

	private string _serverSourceId = string.Empty;

	private string _serverSourceUrl = string.Empty;

	private long _serverRevision = -1L;

	private string _personalSourceType = string.Empty;

	private string _personalSourceId = string.Empty;

	private string _personalSourceUrl = string.Empty;

	private string _webUrl = string.Empty;

	internal string HubNowPlaying042
	{
		get
		{
			try
			{
				return (_track == null) ? string.Empty : (_track.Text ?? string.Empty);
			}
			catch
			{
				return string.Empty;
			}
		}
	}

	public RadioForm()
	{
		Text = "Rádio / TV BiduTruck";
		StartPosition = FormStartPosition.CenterScreen;
		MinimumSize = new Size(760, 680);
		Size = new Size(840, 740);
		BackColor = Color.FromArgb(4, 13, 25);
		ForeColor = Color.WhiteSmoke;
		Font = new Font("Segoe UI", 9f);
		Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
		KeyPreview = true;
		TopMost = true;
		ShowInTaskbar = true;
		BuildUi();
		LoadSavedPersonalSource();
		LoadSavedWebUrl();
		string text = ReadSharedMediaMode041();
		_personalMode = text == "mine";
		_webMode = false;
		if (_personalMode)
		{
			_personalInput.Text = _personalSourceUrl;
		}
		ApplyModeUi();
		Shown += async delegate
		{
			await InitializePlayerAsync();
			await RefreshRadioAsync(force: true);
			_pollTimer.Start();
		};
		KeyDown += (object sender, KeyEventArgs e) =>
		{
			if (e.KeyCode == Keys.Escape && _fullScreenMode)
			{
				ToggleFullScreen();
			}
		};
		_web.KeyDown += (object sender, KeyEventArgs e) =>
		{
			if (e.KeyCode == Keys.Escape && _fullScreenMode)
			{
				ToggleFullScreen();
			}
		};
		FormClosing += async delegate
		{
			_pollTimer.Stop();
			try
			{
				await ExecutePlayerAsync("gatPause()");
			}
			catch
			{
			}
		};
		FormClosed += delegate
		{
			_pollTimer.Dispose();
			_http.Dispose();
			_web.Dispose();
		};
		_pollTimer.Tick += async delegate
		{
			await RefreshRadioAsync(force: false);
		};
	}

	private void BuildUi()
	{
		_title.Text = "RÁDIO / TV BIDUTRUCK";
		_title.Left = 24;
		_title.Top = 16;
		_title.Width = 360;
		_title.Height = 38;
		_title.Font = new Font("Segoe UI Semibold", 20f, FontStyle.Bold);
		_title.ForeColor = Color.White;
		Controls.Add(_title);
		_description.Text = "Escolha o Canal BiduTruck para todos ou o MEU VÍDEO somente para você.";
		_description.Left = 26;
		_description.Top = 53;
		_description.Width = 720;
		_description.Height = 28;
		_description.ForeColor = Color.FromArgb(168, 181, 199);
		Controls.Add(_description);
		SetupButton(_channelGat, "\ud83d\udce1 CANAL BIDUTRUCK", 24, 82, 145);
		_channelGat.Click += async delegate
		{
			await SwitchModeAsync(personal: false);
		};
		Controls.Add(_channelGat);
		SetupButton(_myRadio, "\ud83c\udfac MEU VÍDEO", 178, 82, 155);
		_myRadio.Click += async delegate
		{
			await SwitchModeAsync(personal: true);
		};
		Controls.Add(_myRadio);
		SetupButton(_siteWeb, "\ud83c\udf10 CANAL WEB", 342, 82, 145);
		_siteWeb.Click += delegate
		{
		};
		Controls.Add(_siteWeb);
		_siteWeb.Visible = false;
		_siteWeb.Enabled = false;
		SetupButton(_overlay, "MODO JOGO • SOBREPOSTO", ClientSize.Width - 224, 18, 200);
		_overlay.Anchor = AnchorStyles.Top | AnchorStyles.Right;
		_overlay.Click += delegate
		{
			ToggleOverlayMode();
		};
		Controls.Add(_overlay);
		_personalLabel.Text = "Seu vídeo/playlist do YouTube (fica disponível também no BIDUTRUCK DASH):";
		_personalLabel.Left = 24;
		_personalLabel.Top = 126;
		_personalLabel.Width = 560;
		_personalLabel.Height = 22;
		_personalLabel.ForeColor = Color.FromArgb(168, 181, 199);
		Controls.Add(_personalLabel);
		_personalInput.Left = 24;
		_personalInput.Top = 149;
		_personalInput.Width = ClientSize.Width - 190;
		_personalInput.Height = 27;
		_personalInput.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
		_personalInput.BackColor = Color.FromArgb(12, 26, 43);
		_personalInput.ForeColor = Color.WhiteSmoke;
		_personalInput.BorderStyle = BorderStyle.FixedSingle;
		Controls.Add(_personalInput);
		SetupButton(_loadPersonal, "CARREGAR", ClientSize.Width - 154, 145, 130);
		_loadPersonal.Anchor = AnchorStyles.Top | AnchorStyles.Right;
		_loadPersonal.Click += async delegate
		{
			if (_personalMode)
			{
				await LoadPersonalFromInputAsync();
			}
			else
			{
				await SaveChannel049Async();
			}
		};
		Controls.Add(_loadPersonal);
		_web.Left = 24;
		_web.Top = 190;
		_web.Width = ClientSize.Width - 48;
		_web.Height = 350;
		_web.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
		_web.BackColor = Color.Black;
		Controls.Add(_web);
		_state.Text = "Rádio: conectando à Central BiduTruck...";
		_state.Left = 26;
		_state.Top = 555;
		_state.Width = 760;
		_state.Height = 22;
		_state.ForeColor = Color.FromArgb(130, 224, 69);
		_state.Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold);
		Controls.Add(_state);
		_track.Text = "Tocando agora: —";
		_track.Left = 26;
		_track.Top = 580;
		_track.Width = 760;
		_track.Height = 34;
		_track.ForeColor = Color.Gainsboro;
		Controls.Add(_track);
		_source.Text = "Fonte: —";
		_source.Left = 26;
		_source.Top = 613;
		_source.Width = 760;
		_source.Height = 22;
		_source.ForeColor = Color.FromArgb(143, 158, 178);
		Controls.Add(_source);
		SetupButton(_toggle, "OUVIR RÁDIO", 24, 650, 155);
		_toggle.Click += async delegate
		{
			await ToggleListeningAsync();
		};
		Controls.Add(_toggle);
		SetupButton(_openYoutube, "ABRIR FONTE", 188, 650, 165);
		_openYoutube.Enabled = false;
		_openYoutube.Click += delegate
		{
			OpenYoutube();
		};
		Controls.Add(_openYoutube);
		SetupButton(_fullScreen, "TELA CHEIA", 362, 650, 125);
		_fullScreen.Click += delegate
		{
			ToggleFullScreen();
		};
		Controls.Add(_fullScreen);
		Label label = new Label
		{
			Text = "VOLUME",
			Left = 504,
			Top = 657,
			Width = 65,
			Height = 22,
			ForeColor = Color.FromArgb(168, 181, 199)
		};
		label.Name = "volumeLabel";
		Controls.Add(label);
		_volume.Left = 565;
		_volume.Top = 644;
		_volume.Width = 220;
		_volume.Height = 45;
		_volume.Minimum = 0;
		_volume.Maximum = 100;
		_volume.TickFrequency = 10;
		_volume.Value = LoadVolume();
		_volume.Scroll += async delegate
		{
			SaveVolume(_volume.Value);
			if (_playerReady)
			{
				await ExecutePlayerAsync("gatVolume(" + _volume.Value + ")");
			}
		};
		Controls.Add(_volume);
		Resize += delegate
		{
			if (!_fullScreenMode && !_overlayMode)
			{
				_web.Width = Math.Max(420, ClientSize.Width - 48);
				_personalInput.Width = Math.Max(350, ClientSize.Width - 190);
				_loadPersonal.Left = ClientSize.Width - 154;
				_state.Width = Math.Max(420, ClientSize.Width - 52);
				_track.Width = Math.Max(420, ClientSize.Width - 52);
				_source.Width = Math.Max(420, ClientSize.Width - 52);
			}
		};
	}

	private static void SetupButton(Button button, string text, int left, int top, int width)
	{
		button.Text = text;
		button.Left = left;
		button.Top = top;
		button.Width = width;
		button.Height = 38;
		button.FlatStyle = FlatStyle.Flat;
		button.BackColor = Color.FromArgb(11, 43, 82);
		button.ForeColor = Color.FromArgb(215, 229, 249);
		button.Font = new Font("Segoe UI Semibold", 8.5f, FontStyle.Bold);
		button.FlatAppearance.BorderColor = Color.FromArgb(60, 137, 245);
		button.Cursor = Cursors.Hand;
	}

	private void ApplyModeUi()
	{
		_webMode = false;
		bool flag = !_personalMode;
		_channelGat.BackColor = (flag ? Color.FromArgb(18, 76, 130) : Color.FromArgb(11, 43, 82));
		_myRadio.BackColor = (_personalMode ? Color.FromArgb(18, 76, 130) : Color.FromArgb(11, 43, 82));
		_siteWeb.Visible = false;
		_siteWeb.Enabled = false;
		Control control = Controls["volumeLabel"];
		_personalLabel.Visible = true;
		_personalInput.Visible = true;
		_loadPersonal.Visible = true;
		_toggle.Visible = true;
		_openYoutube.Visible = true;
		_fullScreen.Visible = true;
		_volume.Visible = true;
		if (control != null)
		{
			control.Visible = true;
		}
		if (flag)
		{
			_description.Text = "CANAL BIDUTRUCK toca para todos os motoristas. Admin/Moderador define a programação aqui no Telemetria.";
			_personalLabel.Text = (CanEditChannel049() ? "Link do CANAL BIDUTRUCK para todos (YouTube vídeo/playlist ou Rádio Online MP3/AAC):" : "CANAL BIDUTRUCK • programação compartilhada com todos os motoristas:");
			_personalInput.Enabled = CanEditChannel049();
			_loadPersonal.Enabled = CanEditChannel049();
			_loadPersonal.Text = "SALVAR P/ TODOS";
			if (!_personalInput.Focused)
			{
				_personalInput.Text = _serverSourceUrl ?? string.Empty;
			}
		}
		else
		{
			_description.Text = "MEU VÍDEO é individual: a fonte fica somente neste PC e não altera o Canal BiduTruck dos outros motoristas.";
			_personalLabel.Text = "Seu vídeo/playlist ou Rádio Online (somente neste PC):";
			_personalInput.Enabled = true;
			_loadPersonal.Enabled = true;
			_loadPersonal.Text = "CARREGAR MEU VÍDEO";
			if (!_personalInput.Focused)
			{
				_personalInput.Text = _personalSourceUrl ?? string.Empty;
			}
		}
		_openYoutube.Text = "ABRIR FONTE";
		UpdateActiveSourceUi();
	}

	private bool ActiveAvailable()
	{
		if (_personalMode)
		{
			return !string.IsNullOrWhiteSpace(_personalSourceId);
		}
		if (_serverEnabled)
		{
			return !string.IsNullOrWhiteSpace(_serverSourceId);
		}
		return false;
	}

	private string ActiveSourceType()
	{
		if (!_personalMode)
		{
			return _serverSourceType;
		}
		return _personalSourceType;
	}

	private string ActiveSourceId()
	{
		if (!_personalMode)
		{
			return _serverSourceId;
		}
		return _personalSourceId;
	}

	private string ActiveSourceUrl()
	{
		if (!_personalMode)
		{
			return _serverSourceUrl;
		}
		return _personalSourceUrl;
	}

	private static string SourceDescription(string type)
	{
		if (string.Equals(type, "stream", StringComparison.OrdinalIgnoreCase))
		{
			return "rádio online MP3/AAC";
		}
		if (string.Equals(type, "playlist", StringComparison.OrdinalIgnoreCase))
		{
			return "playlist do YouTube";
		}
		if (string.Equals(type, "video", StringComparison.OrdinalIgnoreCase))
		{
			return "vídeo do YouTube";
		}
		return "fonte desconhecida";
	}

	private void UpdateActiveSourceUi()
	{
		if (_personalMode)
		{
			bool flag = !string.IsNullOrWhiteSpace(_personalSourceId);
			_toggle.Enabled = flag && _browserReady;
			_openYoutube.Enabled = flag;
			Label label = _state;
			string text;
			if (flag)
			{
				text = (_listening ? "Meu Vídeo: tocando sua programação" : "Meu Vídeo: pronta • clique em OUVIR RÁDIO");
			}
			else
			{
				text = "Meu Vídeo: cole YouTube ou URL direta de rádio online acima";
			}
			label.Text = text;
			_state.ForeColor = (flag ? Color.FromArgb(130, 224, 69) : Color.FromArgb(168, 181, 199));
			_source.Text = (flag ? ("Fonte local: " + SourceDescription(_personalSourceType) + " • somente neste PC") : "Fonte local: nenhuma configurada");
			return;
		}
		bool flag2 = _serverEnabled && !string.IsNullOrWhiteSpace(_serverSourceId);
		_toggle.Enabled = flag2 && _browserReady;
		_openYoutube.Enabled = !string.IsNullOrWhiteSpace(_serverSourceUrl);
		if (!flag2)
		{
			_state.Text = "Canal BiduTruck: DESLIGADO pela Central";
			_state.ForeColor = Color.FromArgb(168, 181, 199);
			_source.Text = "Fonte oficial: nenhuma programação ativa";
		}
		else
		{
			_state.Text = (_listening ? "Canal BiduTruck: AO VIVO • ouvindo" : "Canal BiduTruck: AO VIVO • clique em OUVIR RÁDIO");
			_state.ForeColor = Color.FromArgb(130, 224, 69);
			_source.Text = "Fonte oficial: " + SourceDescription(_serverSourceType) + " • revisão " + _serverRevision;
		}
	}

	private async Task SwitchModeAsync(bool personal)
	{
		_webMode = false;
		bool flag = _personalMode != personal;
		_personalMode = personal;
		SaveSharedMediaMode041(personal ? "mine" : "gat");
		if (personal)
		{
			_personalInput.Text = _personalSourceUrl ?? string.Empty;
		}
		else
		{
			_personalInput.Text = _serverSourceUrl ?? string.Empty;
		}
		ApplyModeUi();
		if (!flag || !_listening)
		{
			return;
		}
		if (ActiveAvailable() && _playerReady)
		{
			await LoadActiveSourceAsync();
			return;
		}
		_listening = false;
		_toggle.Text = "OUVIR RÁDIO";
		try
		{
			await ExecutePlayerAsync("gatPause()");
		}
		catch
		{
		}
		UpdateActiveSourceUi();
	}

	internal void ConfigureAccount049(string user, string token)
	{
		string text = (user ?? string.Empty).Trim();
		string text2 = token ?? string.Empty;
		bool num = !string.Equals(_accountUser049, text, StringComparison.OrdinalIgnoreCase) || _accountToken049 != text2;
		_accountUser049 = text;
		_accountToken049 = text2;
		if (num)
		{
			RefreshAccountRole049();
		}
		else
		{
			ApplyModeUi();
		}
	}

	private bool CanEditChannel049()
	{
		if (!string.Equals(_accountRole049, "owner", StringComparison.OrdinalIgnoreCase) && !string.Equals(_accountRole049, "admin", StringComparison.OrdinalIgnoreCase))
		{
			return string.Equals(_accountRole049, "moderator", StringComparison.OrdinalIgnoreCase);
		}
		return true;
	}

	private async Task RefreshAccountRole049()
	{
		_accountRole049 = string.Empty;
		if (string.IsNullOrWhiteSpace(_accountToken049))
		{
			try
			{
				if (!IsDisposed)
				{
					BeginInvoke((Action)(() =>
					{
						ApplyModeUi();
					}));
				}
				return;
			}
			catch
			{
				return;
			}
		}
		try
		{
			JObject jObject = new JObject { ["token"] = _accountToken049 };
			using StringContent content = new StringContent(jObject.ToString(Formatting.None), Encoding.UTF8, "application/json");
			using HttpResponseMessage response = await _http.PostAsync("https://api.gatlogets2.com.br/api/account/session", content);
			JObject jObject2 = JObject.Parse(await response.Content.ReadAsStringAsync());
			if (response.IsSuccessStatusCode && jObject2.Value<bool?>("ok") == true)
			{
				_accountRole049 = (Convert.ToString(jObject2["role"]) ?? string.Empty).Trim().ToLowerInvariant();
			}
		}
		catch
		{
			_accountRole049 = string.Empty;
		}
		try
		{
			if (!IsDisposed)
			{
				BeginInvoke((Action)(() =>
				{
					ApplyModeUi();
				}));
			}
		}
		catch
		{
		}
	}

	private async Task SaveChannel049Async()
	{
		if (!CanEditChannel049())
		{
			_state.Text = "Canal BiduTruck: sua conta não tem permissão para alterar a programação global.";
			_state.ForeColor = Color.OrangeRed;
			return;
		}
		string text = (_personalInput.Text ?? string.Empty).Trim();
		string sourceType = string.Empty;
		string sourceId = string.Empty;
		string canonical = string.Empty;
		bool enabled = !string.IsNullOrWhiteSpace(text);
		if (enabled && !TryParseMediaSource(text, out sourceType, out sourceId, out canonical))
		{
			_state.Text = "Canal BiduTruck: link inválido. Use YouTube ou URL direta de Rádio Online MP3/AAC.";
			_state.ForeColor = Color.OrangeRed;
			return;
		}
		try
		{
			_loadPersonal.Enabled = false;
			_state.Text = (enabled ? "Canal BiduTruck: salvando para todos..." : "Canal BiduTruck: desligando programação global...");
			_state.ForeColor = Color.FromArgb(168, 181, 199);
			JObject jObject = new JObject
			{
				["token"] = _accountToken049,
				["enabled"] = enabled,
				["playlist_url"] = (enabled ? canonical : string.Empty),
				["label"] = "Canal BiduTruck"
			};
			using (StringContent content = new StringContent(jObject.ToString(Formatting.None), Encoding.UTF8, "application/json"))
			{
				using HttpResponseMessage response = await _http.PostAsync("https://api.gatlogets2.com.br/api/site/admin/radio", content);
				string json = await response.Content.ReadAsStringAsync();
				JObject jObject2 = null;
				try
				{
					jObject2 = JObject.Parse(json);
				}
				catch
				{
				}
				if (!response.IsSuccessStatusCode || jObject2 == null || jObject2.Value<bool?>("ok") != true)
				{
					string text2 = ((jObject2 == null) ? ("HTTP " + (int)response.StatusCode) : (Convert.ToString(jObject2["error"]) ?? ("HTTP " + (int)response.StatusCode)));
					_state.Text = "Canal BiduTruck: não foi possível salvar • " + text2;
					_state.ForeColor = Color.OrangeRed;
					return;
				}
			}
			_personalInput.Text = (enabled ? canonical : string.Empty);
			await RefreshRadioAsync(force: true);
			_state.Text = (enabled ? "Canal BiduTruck: programação salva para todos os motoristas ✓" : "Canal BiduTruck: programação global desligada ✓");
			_state.ForeColor = Color.FromArgb(130, 224, 69);
		}
		catch (Exception ex)
		{
			_state.Text = "Canal BiduTruck: falha ao salvar • " + ex.Message;
			_state.ForeColor = Color.OrangeRed;
		}
		finally
		{
			_loadPersonal.Enabled = CanEditChannel049();
		}
	}

	private async Task LoadPersonalFromInputAsync()
	{
		if (!TryParseMediaSource(_personalInput.Text, out var sourceType, out var sourceId, out var canonicalUrl))
		{
			_state.Text = "Meu Vídeo: fonte inválida. Use YouTube ou URL direta de rádio online (MP3/AAC).";
			_state.ForeColor = Color.OrangeRed;
			return;
		}
		_personalSourceType = sourceType;
		_personalSourceId = sourceId;
		_personalSourceUrl = canonicalUrl;
		_personalInput.Text = canonicalUrl;
		SavePersonalSource(canonicalUrl);
		_personalMode = true;
		ApplyModeUi();
		if (_listening && _playerReady)
		{
			await LoadActiveSourceAsync();
		}
	}

	private async Task LoadWebFromInputAsync()
	{
		if (!TryParseWebUrl(_personalInput.Text, out var canonicalUrl))
		{
			_state.Text = "CANAL WEB: endereço inválido. Use somente HTTP ou HTTPS.";
			_state.ForeColor = Color.OrangeRed;
			return;
		}
		_webUrl = canonicalUrl;
		_personalInput.Text = canonicalUrl;
		SaveWebUrl(canonicalUrl);
		_personalMode = false;
		_webMode = true;
		SaveSharedMediaMode041("web");
		ApplyModeUi();
		await NavigateWebAsync(canonicalUrl);
	}

	private async Task NavigateWebAsync(string url)
	{
		if (!_browserReady || _web.CoreWebView2 == null)
		{
			_state.Text = "CANAL WEB: navegador interno indisponível neste PC.";
			_state.ForeColor = Color.OrangeRed;
			return;
		}
		if (!TryParseWebUrl(url, out var canonicalUrl))
		{
			_state.Text = "CANAL WEB: endereço inválido.";
			_state.ForeColor = Color.OrangeRed;
			return;
		}
		_webUrl = canonicalUrl;
		SaveWebUrl(canonicalUrl);
		_state.Text = "CANAL WEB: carregando...";
		_state.ForeColor = Color.FromArgb(168, 181, 199);
		_track.Text = "Página atual: " + canonicalUrl;
		_web.CoreWebView2.Navigate(canonicalUrl);
		await Task.Delay(1);
	}

	private void ShowWebStartPage()
	{
		if (_browserReady && _web.CoreWebView2 != null)
		{
			_web.CoreWebView2.NavigateToString("<!doctype html><html><head><meta charset='utf-8'><style>html,body{height:100%;margin:0;background:#020711;color:#eaf2ff;font-family:Segoe UI,Arial,sans-serif}.box{height:100%;display:flex;align-items:center;justify-content:center;text-align:center}.card{max-width:620px;padding:32px}.icon{font-size:58px}.title{font-size:26px;font-weight:700;margin-top:8px}.sub{color:#9eb6d1;margin-top:10px;line-height:1.5}</style></head><body><div class='box'><div class='card'><div class='icon'>\ud83c\udf10</div><div class='title'>CANAL WEB</div><div class='sub'>Cole um endereço HTTP/HTTPS acima e clique em ABRIR NO GAT.<br>Alguns sites podem bloquear vídeo incorporado, autoplay, login ou conteúdo protegido.</div></div></div></body></html>");
		}
	}

	private void WebNavigationStarting(object sender, CoreWebView2NavigationStartingEventArgs e)
	{
		if (_webMode)
		{
			if (!Uri.TryCreate(e.Uri, UriKind.Absolute, out var result))
			{
				e.Cancel = true;
			}
			else if (!(result.Scheme == Uri.UriSchemeHttp) && !(result.Scheme == Uri.UriSchemeHttps) && !(result.Scheme == "about"))
			{
				e.Cancel = true;
			}
		}
	}

	private void WebNewWindowRequested(object sender, CoreWebView2NewWindowRequestedEventArgs e)
	{
		if (_webMode)
		{
			e.Handled = true;
			if (TryParseWebUrl(e.Uri, out var canonicalUrl) && _web.CoreWebView2 != null)
			{
				_web.CoreWebView2.Navigate(canonicalUrl);
			}
		}
	}

	private void WebNavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
	{
		if (!_webMode)
		{
			return;
		}
		if (e.IsSuccess)
		{
			_state.Text = "CANAL WEB: página carregada";
			_state.ForeColor = Color.FromArgb(130, 224, 69);
			if (_web.CoreWebView2 != null)
			{
				string source = _web.CoreWebView2.Source;
				if (!string.IsNullOrWhiteSpace(source) && !source.StartsWith("about:", StringComparison.OrdinalIgnoreCase))
				{
					_webUrl = source;
					SaveWebUrl(source);
					_personalInput.Text = source;
					_track.Text = "Página atual: " + source;
				}
			}
		}
		else
		{
			_state.Text = "CANAL WEB: não foi possível carregar esta página.";
			_state.ForeColor = Color.OrangeRed;
		}
	}

	private async Task InitializePlayerAsync()
	{
		if (_browserReady)
		{
			return;
		}
		try
		{
			string path = Path.Combine(Path.GetTempPath(), "GAT-LOG", "Telemetria", "Radio-1.0.50");
			string text = Path.Combine(path, "WebView2");
			string pageFolder = Path.Combine(path, "player");
			Directory.CreateDirectory(text);
			Directory.CreateDirectory(pageFolder);
			string path2 = Path.Combine(text, "write-test.tmp");
			File.WriteAllText(path2, "ok");
			File.Delete(path2);
			CoreWebView2EnvironmentOptions coreWebView2EnvironmentOptions = new CoreWebView2EnvironmentOptions();
			coreWebView2EnvironmentOptions.AdditionalBrowserArguments = "--autoplay-policy=no-user-gesture-required --allow-running-insecure-content";
			CoreWebView2Environment environment = await CoreWebView2Environment.CreateAsync(null, text, coreWebView2EnvironmentOptions);
			await _web.EnsureCoreWebView2Async(environment);
			_web.CoreWebView2.AddWebResourceRequestedFilter("https://www.youtube.com/*", CoreWebView2WebResourceContext.All);
			_web.CoreWebView2.AddWebResourceRequestedFilter("https://youtube.com/*", CoreWebView2WebResourceContext.All);
			_web.CoreWebView2.AddWebResourceRequestedFilter("https://m.youtube.com/*", CoreWebView2WebResourceContext.All);
			_web.CoreWebView2.AddWebResourceRequestedFilter("https://music.youtube.com/*", CoreWebView2WebResourceContext.All);
			_web.CoreWebView2.AddWebResourceRequestedFilter("https://youtu.be/*", CoreWebView2WebResourceContext.All);
			_web.CoreWebView2.AddWebResourceRequestedFilter("https://www.youtube-nocookie.com/*", CoreWebView2WebResourceContext.All);
			_web.CoreWebView2.WebResourceRequested += (object sender, CoreWebView2WebResourceRequestedEventArgs e) =>
			{
				try
				{
					if (Uri.TryCreate(e.Request.Uri, UriKind.Absolute, out var result))
					{
						string text2 = (result.Host ?? string.Empty).ToLowerInvariant();
						if (text2 == "youtube.com" || text2.EndsWith(".youtube.com", StringComparison.Ordinal) || text2 == "youtu.be" || text2 == "youtube-nocookie.com" || text2.EndsWith(".youtube-nocookie.com", StringComparison.Ordinal))
						{
							e.Request.Headers.SetHeader("Referer", "https://radio.gatlogets2.local/");
						}
					}
				}
				catch
				{
				}
			};
			File.WriteAllText(Path.Combine(pageFolder, "index.html"), PlayerHtml());
			_web.CoreWebView2.SetVirtualHostNameToFolderMapping("radio.gatlogets2.local", pageFolder, CoreWebView2HostResourceAccessKind.Allow);
			_web.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
			_web.CoreWebView2.Settings.AreDevToolsEnabled = false;
			_web.CoreWebView2.Settings.IsZoomControlEnabled = false;
			_web.CoreWebView2.WebMessageReceived += WebMessageReceived;
			_web.CoreWebView2.NavigationStarting += WebNavigationStarting;
			_web.CoreWebView2.NavigationCompleted += WebNavigationCompleted;
			_web.CoreWebView2.NewWindowRequested += WebNewWindowRequested;
			_web.Source = new Uri("https://radio.gatlogets2.local/index.html?v=149");
			_browserReady = true;
			ApplyModeUi();
		}
		catch (Exception ex)
		{
			_browserReady = false;
			_state.Text = "Rádio: player interno indisponível neste PC.";
			_state.ForeColor = Color.OrangeRed;
			_track.Text = "Detalhe: " + ex.Message;
			_toggle.Enabled = false;
		}
	}

	private void WebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
	{
		if (_webMode)
		{
			return;
		}
		try
		{
			JObject jObject = JObject.Parse(e.WebMessageAsJson);
			switch (Convert.ToString(jObject["type"]))
			{
			case "ready":
				_playerReady = true;
				ExecutePlayerAsync("gatVolume(" + _volume.Value + ")");
				if (_listening && ActiveAvailable())
				{
					LoadActiveSourceAsync();
				}
				break;
			case "track":
			{
				string text = Convert.ToString(jObject["title"]);
				if (!string.IsNullOrWhiteSpace(text))
				{
					_track.Text = "Tocando agora: " + text;
				}
				break;
			}
			case "error":
			{
				int result = 0;
				int.TryParse(Convert.ToString(jObject["code"]), out result);
				_track.Text = YoutubeErrorText(result);
				break;
			}
			}
		}
		catch
		{
		}
	}

	private static string YoutubeErrorText(int code)
	{
		switch (code)
		{
		case 100:
			return "Este vídeo foi removido, é privado ou não está disponível. Em playlist, a Rádio BiduTruck tenta pular para o próximo.";
		case 101:
		case 150:
			return "Este videoclipe bloqueia reprodução incorporada. Em playlist ele será pulado; para vídeo único use ABRIR FONTE.";
		case 153:
			return "O YouTube recusou o player incorporado neste vídeo. Use ABRIR FONTE.";
		case 2:
			return "Link/ID de vídeo inválido.";
		case 5:
			return "O player HTML5 do YouTube não conseguiu reproduzir este vídeo.";
		case 900:
			return "Não foi possível tocar esta rádio online. Confirme se o link é o stream direto MP3/AAC; HTTPS é recomendado.";
		case 901:
			return "A rádio online foi interrompida. O servidor da estação pode estar offline ou ter recusado a conexão.";
		default:
			return "Player do YouTube informou erro " + code + ".";
		}
	}

	private static string SharedMediaModeFile041()
	{
		return Path.Combine(Application.LocalUserAppDataPath, "radio-active-mode.txt");
	}

	private static void SaveSharedMediaMode041(string mode)
	{
		try
		{
			mode = (mode ?? string.Empty).Trim().ToLowerInvariant();
			if (!(mode != "gat") || !(mode != "mine"))
			{
				File.WriteAllText(SharedMediaModeFile041(), mode);
			}
		}
		catch
		{
		}
	}

	private static string ReadSharedMediaMode041()
	{
		try
		{
			string path = SharedMediaModeFile041();
			if (!File.Exists(path))
			{
				return "gat";
			}
			return ((File.ReadAllText(path) ?? string.Empty).Trim().ToLowerInvariant() == "mine") ? "mine" : "gat";
		}
		catch
		{
			return "gat";
		}
	}

	private async Task SyncSharedMediaMode041()
	{
		if (ReadSharedMediaMode041() == "mine")
		{
			if (!_personalMode || _webMode)
			{
				await SwitchModeAsync(personal: true);
			}
		}
		else if (_personalMode || _webMode)
		{
			await SwitchModeAsync(personal: false);
		}
	}

	internal async void HubPause042()
	{
		_ = 1;
		try
		{
			_listening = false;
			_toggle.Text = "OUVIR RÁDIO";
			if (_webMode && _web.CoreWebView2 != null)
			{
				try
				{
					await _web.CoreWebView2.ExecuteScriptAsync("document.querySelectorAll('video,audio').forEach(function(x){try{x.pause()}catch(e){}})");
				}
				catch
				{
				}
			}
			else
			{
				try
				{
					await ExecutePlayerAsync("gatPause()");
				}
				catch
				{
				}
			}
			UpdateActiveSourceUi();
		}
		catch
		{
		}
	}

	internal async void HubSyncMode042()
	{
		try
		{
			await SyncSharedMediaMode041();
		}
		catch
		{
		}
	}

	private async Task RefreshRadioAsync(bool force)
	{
		await SyncSharedMediaMode041();
		try
		{
			JObject jObject = JObject.Parse(await _http.GetStringAsync("https://api.gatlogets2.com.br/api/public/radio?t=" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));
			if (!(jObject["radio"] is JObject jObject2) || jObject.Value<bool?>("ok") != true)
			{
				throw new InvalidDataException("Resposta inválida da Central BiduTruck.");
			}
			bool valueOrDefault = jObject2.Value<bool?>("enabled") == true;
			string text = (Convert.ToString(jObject2["source_type"]) ?? string.Empty).Trim().ToLowerInvariant();
			string text2 = Convert.ToString(jObject2["source_url"]) ?? Convert.ToString(jObject2["playlist_url"]) ?? string.Empty;
			_ = string.Empty;
			string text3;
			if (text == "video")
			{
				text3 = Convert.ToString(jObject2["video_id"]) ?? string.Empty;
			}
			else if (text == "stream")
			{
				text3 = text2;
			}
			else
			{
				text3 = Convert.ToString(jObject2["playlist_id"]) ?? string.Empty;
				if (string.IsNullOrWhiteSpace(text) && !string.IsNullOrWhiteSpace(text3))
				{
					text = "playlist";
				}
			}
			long valueOrDefault2 = jObject2.Value<long?>("revision").GetValueOrDefault();
			bool changed = force || valueOrDefault2 != _serverRevision || !string.Equals(text, _serverSourceType, StringComparison.Ordinal) || !string.Equals(text3, _serverSourceId, StringComparison.Ordinal);
			_serverEnabled = valueOrDefault;
			_serverSourceType = text;
			_serverSourceId = text3;
			_serverSourceUrl = text2;
			_serverRevision = valueOrDefault2;
			if (!_personalMode && !_webMode)
			{
				if ((!valueOrDefault || string.IsNullOrWhiteSpace(text3)) && _listening)
				{
					_listening = false;
					_toggle.Text = "OUVIR RÁDIO";
					await ExecutePlayerAsync("gatPause()");
				}
				UpdateActiveSourceUi();
				if (changed && _listening && _playerReady && ActiveAvailable())
				{
					await LoadActiveSourceAsync();
				}
			}
		}
		catch (Exception ex)
		{
			if (!_personalMode && !_webMode)
			{
				_state.Text = "Canal BiduTruck: Central temporariamente indisponível.";
				_state.ForeColor = Color.Orange;
				_track.Text = "Detalhe: " + ex.Message;
			}
		}
	}

	private async Task ToggleListeningAsync()
	{
		if (!ActiveAvailable())
		{
			return;
		}
		if (!_listening)
		{
			_listening = true;
			_toggle.Text = "PARAR RÁDIO";
			UpdateActiveSourceUi();
			if (_playerReady)
			{
				await LoadActiveSourceAsync();
			}
		}
		else
		{
			_listening = false;
			_toggle.Text = "OUVIR RÁDIO";
			await ExecutePlayerAsync("gatPause()");
			UpdateActiveSourceUi();
		}
	}

	private async Task LoadActiveSourceAsync()
	{
		string value = ActiveSourceId();
		if (_playerReady && !string.IsNullOrWhiteSpace(value))
		{
			string text = ActiveSourceType();
			string text3;
			if (text == "stream")
			{
				string text2 = JsonConvert.SerializeObject(ActiveSourceUrl());
				text3 = "gatLoadStream(" + text2 + ")";
			}
			else
			{
				string text4 = JsonConvert.SerializeObject(value);
				text3 = ((text == "video") ? ("gatLoadVideo(" + text4 + ")") : ("gatLoadPlaylist(" + text4 + ")"));
			}
			await ExecutePlayerAsync(text3 + ";gatVolume(" + _volume.Value + ")");
		}
	}

	private async Task ExecutePlayerAsync(string script)
	{
		if (!_browserReady || _web.CoreWebView2 == null)
		{
			return;
		}
		try
		{
			await _web.CoreWebView2.ExecuteScriptAsync(script);
		}
		catch
		{
		}
	}

	private void OpenYoutube()
	{
		string text = (_webMode ? _webUrl : ActiveSourceUrl());
		if (string.IsNullOrWhiteSpace(text))
		{
			return;
		}
		try
		{
			Process.Start(new ProcessStartInfo(text)
			{
				UseShellExecute = true
			});
		}
		catch (Exception ex)
		{
			MessageBox.Show(this, "Não foi possível abrir no navegador.\r\n\r\n" + ex.Message, "Rádio BiduTruck", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
		}
	}

	private void ToggleFullScreen()
	{
		if (!_fullScreenMode)
		{
			if (_overlayMode)
			{
				ToggleOverlayMode();
			}
			_fullScreenMode = true;
			_normalBounds = Bounds;
			WindowState = FormWindowState.Maximized;
			foreach (Control control in Controls)
			{
				control.Visible = false;
			}
			_web.Visible = true;
			_fullScreen.Visible = true;
			_web.Left = 8;
			_web.Top = 8;
			_web.Width = ClientSize.Width - 16;
			_web.Height = Math.Max(350, ClientSize.Height - 62);
			_web.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
			_fullScreen.Text = "SAIR TELA CHEIA";
			_fullScreen.Left = Math.Max(8, ClientSize.Width - 145);
			_fullScreen.Top = Math.Max(8, ClientSize.Height - 48);
			_fullScreen.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
			_fullScreen.BringToFront();
			return;
		}
		_fullScreenMode = false;
		WindowState = FormWindowState.Normal;
		if (_normalBounds.Width >= 760 && _normalBounds.Height >= 680)
		{
			Bounds = _normalBounds;
		}
		else
		{
			Size = new Size(840, 740);
		}
		foreach (Control control2 in Controls)
		{
			control2.Visible = true;
		}
		_web.Left = 24;
		_web.Top = 190;
		_web.Width = ClientSize.Width - 48;
		_web.Height = 350;
		_web.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
		_fullScreen.Text = "TELA CHEIA";
		_fullScreen.Left = 362;
		_fullScreen.Top = 650;
		_fullScreen.Anchor = AnchorStyles.Top | AnchorStyles.Left;
		RestoreNormalLayout();
		ApplyModeUi();
	}

	private void ToggleOverlayMode()
	{
		if (!_overlayMode)
		{
			if (_fullScreenMode)
			{
				ToggleFullScreen();
			}
			_overlayMode = true;
			_normalBounds = Bounds;
			TopMost = true;
			ShowInTaskbar = true;
			FormBorderStyle = FormBorderStyle.SizableToolWindow;
			MinimumSize = new Size(360, 240);
			Size = new Size(520, 340);
			foreach (Control control in Controls)
			{
				if (control != _web && control != _overlay)
				{
					control.Visible = false;
				}
			}
			_web.Visible = true;
			_overlay.Visible = true;
			_web.Left = 8;
			_web.Top = 8;
			_web.Width = Math.Max(320, ClientSize.Width - 16);
			_web.Height = Math.Max(160, ClientSize.Height - 60);
			_web.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
			_overlay.Text = "VOLTAR";
			_overlay.Width = 160;
			_overlay.Height = 36;
			_overlay.Left = Math.Max(8, ClientSize.Width - _overlay.Width - 8);
			_overlay.Top = Math.Max(8, ClientSize.Height - _overlay.Height - 8);
			_overlay.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
			_overlay.BringToFront();
			Rectangle workingArea = Screen.FromControl(this).WorkingArea;
			Location = new Point(Math.Max(workingArea.Left, workingArea.Right - Width - 16), Math.Max(workingArea.Top, workingArea.Bottom - Height - 16));
			foreach (Form openForm in Application.OpenForms)
			{
				if (openForm != this && string.Equals(openForm.GetType().Name, "MainForm", StringComparison.Ordinal))
				{
					openForm.WindowState = FormWindowState.Minimized;
					break;
				}
			}
			Activate();
			BringToFront();
			return;
		}
		_overlayMode = false;
		FormBorderStyle = FormBorderStyle.Sizable;
		MinimumSize = new Size(760, 680);
		foreach (Control control2 in Controls)
		{
			control2.Visible = true;
		}
		if (_normalBounds.Width >= 760 && _normalBounds.Height >= 680)
		{
			Bounds = _normalBounds;
		}
		else
		{
			Size = new Size(840, 740);
		}
		RestoreNormalLayout();
		ApplyModeUi();
	}

	private void RestoreNormalLayout()
	{
		_web.Left = 24;
		_web.Top = 190;
		_web.Width = ClientSize.Width - 48;
		_web.Height = 350;
		_web.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
		_state.Top = 555;
		_track.Top = 580;
		_source.Top = 613;
		_toggle.Left = 24;
		_toggle.Top = 650;
		_openYoutube.Left = 188;
		_openYoutube.Top = 650;
		_fullScreen.Left = 362;
		_fullScreen.Top = 650;
		_overlay.Text = "MODO JOGO • SOBREPOSTO";
		_overlay.Width = 200;
		_overlay.Height = 38;
		_overlay.Left = ClientSize.Width - 224;
		_overlay.Top = 18;
		_overlay.Anchor = AnchorStyles.Top | AnchorStyles.Right;
		_personalInput.Width = Math.Max(350, ClientSize.Width - 190);
		_loadPersonal.Left = ClientSize.Width - 154;
		_overlay.BringToFront();
	}

	private void LoadSavedPersonalSource()
	{
		try
		{
			string path = PersonalSourceFile();
			if (File.Exists(path) && TryParseMediaSource(File.ReadAllText(path).Trim(), out var sourceType, out var sourceId, out var canonicalUrl))
			{
				_personalSourceType = sourceType;
				_personalSourceId = sourceId;
				_personalSourceUrl = canonicalUrl;
				_personalInput.Text = canonicalUrl;
			}
		}
		catch
		{
		}
	}

	private static void SavePersonalSource(string value)
	{
		try
		{
			Directory.CreateDirectory(Application.LocalUserAppDataPath);
			File.WriteAllText(PersonalSourceFile(), value ?? string.Empty);
		}
		catch
		{
		}
	}

	private static string PersonalSourceFile()
	{
		return Path.Combine(Application.LocalUserAppDataPath, "radio-personal-source.txt");
	}

	private void LoadSavedWebUrl()
	{
		try
		{
			string path = WebSourceFile();
			if (File.Exists(path) && TryParseWebUrl(File.ReadAllText(path).Trim(), out var canonicalUrl))
			{
				_webUrl = canonicalUrl;
			}
		}
		catch
		{
		}
	}

	private static void SaveWebUrl(string value)
	{
		try
		{
			Directory.CreateDirectory(Application.LocalUserAppDataPath);
			File.WriteAllText(WebSourceFile(), value ?? string.Empty);
		}
		catch
		{
		}
	}

	private static string WebSourceFile()
	{
		return Path.Combine(Application.LocalUserAppDataPath, "radio-web-url.txt");
	}

	private static int LoadVolume()
	{
		try
		{
			if (int.TryParse(File.ReadAllText(Path.Combine(Application.LocalUserAppDataPath, "radio-volume.txt")), out var result))
			{
				return Math.Max(0, Math.Min(100, result));
			}
		}
		catch
		{
		}
		return 55;
	}

	private static void SaveVolume(int value)
	{
		try
		{
			Directory.CreateDirectory(Application.LocalUserAppDataPath);
			File.WriteAllText(Path.Combine(Application.LocalUserAppDataPath, "radio-volume.txt"), value.ToString());
		}
		catch
		{
		}
	}

	private static bool TryParseMediaSource(string input, out string sourceType, out string sourceId, out string canonicalUrl)
	{
		sourceType = string.Empty;
		sourceId = string.Empty;
		canonicalUrl = string.Empty;
		string text = (input ?? string.Empty).Trim();
		if (string.IsNullOrWhiteSpace(text))
		{
			return false;
		}
		if (IsSafeYoutubeId(text))
		{
			if (text.Length == 11)
			{
				sourceType = "video";
				sourceId = text;
				canonicalUrl = "https://www.youtube.com/watch?v=" + text;
				return true;
			}
			if (LooksLikePlaylistId(text))
			{
				sourceType = "playlist";
				sourceId = text;
				canonicalUrl = "https://www.youtube.com/playlist?list=" + text;
				return true;
			}
		}
		if (text.StartsWith("www.", StringComparison.OrdinalIgnoreCase) || text.StartsWith("youtube.com", StringComparison.OrdinalIgnoreCase) || text.StartsWith("youtu.be", StringComparison.OrdinalIgnoreCase))
		{
			text = "https://" + text;
		}
		if (!Uri.TryCreate(text, UriKind.Absolute, out var result))
		{
			return false;
		}
		if (result.Scheme != Uri.UriSchemeHttp && result.Scheme != Uri.UriSchemeHttps)
		{
			return false;
		}
		if (!string.IsNullOrWhiteSpace(result.UserInfo))
		{
			return false;
		}
		string text2 = (result.Host ?? string.Empty).ToLowerInvariant();
		switch (text2)
		{
		default:
			if (!(text2 == "youtu.be"))
			{
				canonicalUrl = result.AbsoluteUri;
				sourceType = "stream";
				sourceId = canonicalUrl;
				return true;
			}
			goto case "youtube.com";
		case "youtube.com":
		case "www.youtube.com":
		case "m.youtube.com":
		case "music.youtube.com":
		{
			Dictionary<string, string> dictionary = ParseQuery(result.Query);
			if (dictionary.TryGetValue("list", out var value) && IsSafeYoutubeId(value) && LooksLikePlaylistId(value))
			{
				sourceType = "playlist";
				sourceId = value;
				canonicalUrl = "https://www.youtube.com/playlist?list=" + value;
				return true;
			}
			string text3 = string.Empty;
			if (text2 == "youtu.be")
			{
				text3 = result.AbsolutePath.Trim('/');
			}
			else if (dictionary.ContainsKey("v"))
			{
				text3 = dictionary["v"];
			}
			else
			{
				string[] array = result.AbsolutePath.Trim('/').Split('/');
				if (array.Length >= 2 && (array[0].Equals("shorts", StringComparison.OrdinalIgnoreCase) || array[0].Equals("embed", StringComparison.OrdinalIgnoreCase) || array[0].Equals("live", StringComparison.OrdinalIgnoreCase)))
				{
					text3 = array[1];
				}
			}
			if (text3.Length == 11 && IsSafeYoutubeId(text3))
			{
				sourceType = "video";
				sourceId = text3;
				canonicalUrl = "https://www.youtube.com/watch?v=" + text3;
				return true;
			}
			return false;
		}
		}
	}

	private static bool TryParseWebUrl(string input, out string canonicalUrl)
	{
		canonicalUrl = string.Empty;
		string text = (input ?? string.Empty).Trim();
		if (string.IsNullOrWhiteSpace(text))
		{
			return false;
		}
		if (text.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
		{
			text = "https://" + text;
		}
		if (!Uri.TryCreate(text, UriKind.Absolute, out var result))
		{
			return false;
		}
		if (result.Scheme != Uri.UriSchemeHttp && result.Scheme != Uri.UriSchemeHttps)
		{
			return false;
		}
		if (!string.IsNullOrWhiteSpace(result.UserInfo))
		{
			return false;
		}
		canonicalUrl = result.AbsoluteUri;
		return true;
	}

	private static Dictionary<string, string> ParseQuery(string query)
	{
		Dictionary<string, string> dictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		string text = (query ?? string.Empty).TrimStart('?');
		if (string.IsNullOrWhiteSpace(text))
		{
			return dictionary;
		}
		string[] array = text.Split('&');
		foreach (string text2 in array)
		{
			if (!string.IsNullOrWhiteSpace(text2))
			{
				int num = text2.IndexOf('=');
				string text3 = ((num >= 0) ? text2.Substring(0, num) : text2);
				string text4 = ((num >= 0) ? text2.Substring(num + 1) : string.Empty);
				try
				{
					text3 = Uri.UnescapeDataString(text3.Replace("+", " "));
					text4 = Uri.UnescapeDataString(text4.Replace("+", " "));
				}
				catch
				{
				}
				if (!dictionary.ContainsKey(text3))
				{
					dictionary[text3] = text4;
				}
			}
		}
		return dictionary;
	}

	private static bool IsSafeYoutubeId(string value)
	{
		if (string.IsNullOrWhiteSpace(value) || value.Length > 100)
		{
			return false;
		}
		foreach (char c in value)
		{
			if (!char.IsLetterOrDigit(c) && c != '_' && c != '-')
			{
				return false;
			}
		}
		return true;
	}

	private static bool LooksLikePlaylistId(string value)
	{
		if (string.IsNullOrWhiteSpace(value) || value.Length < 12)
		{
			return false;
		}
		string text = value.ToUpperInvariant();
		if (!text.StartsWith("PL") && !text.StartsWith("UU") && !text.StartsWith("LL") && !text.StartsWith("FL") && !text.StartsWith("OL"))
		{
			return text.StartsWith("RD");
		}
		return true;
	}

	private static string PlayerHtml()
	{
		return "<!doctype html>\n<html><head><meta charset='utf-8'><meta name='referrer' content='strict-origin-when-cross-origin'>\n<style>\nhtml,body{margin:0;width:100%;height:100%;background:#020711;overflow:hidden;font-family:Segoe UI,Arial,sans-serif;color:#eaf2ff}\n#player,#streamPane{width:100%;height:100%}#streamPane{display:none;align-items:center;justify-content:center;background:radial-gradient(circle at 50% 35%,#0d3155 0,#06192c 45%,#020711 100%)}\n.radioCard{text-align:center;max-width:86%;padding:28px}.radioIcon{font-size:64px;margin-bottom:14px}.radioTitle{font-size:25px;font-weight:700}.radioSub{margin-top:8px;color:#9eb6d1;font-size:14px}.radioUrl{margin-top:16px;color:#6f8daa;font-size:11px;white-space:nowrap;overflow:hidden;text-overflow:ellipsis;max-width:680px}\n</style></head>\n<body><div id='player'></div><div id='streamPane'><div class='radioCard'><div class='radioIcon'>\ud83d\udcfb</div><div class='radioTitle'>RÁDIO ONLINE • AO VIVO</div><div class='radioSub'>Stream direto MP3/AAC reproduzido somente neste BIDUTRUCK TELEMETRIA.</div><div id='streamUrl' class='radioUrl'></div></div><audio id='streamAudio' preload='none'></audio></div>\n<script src='https://www.youtube.com/iframe_api'></script><script>\nlet player=null,ytReady=false,pendingType='',pendingId='',currentType='';\nconst audio=document.getElementById('streamAudio'),playerBox=document.getElementById('player'),streamPane=document.getElementById('streamPane'),streamUrl=document.getElementById('streamUrl');\nfunction post(o){try{chrome.webview.postMessage(o)}catch(e){}}\nfunction showYoutube(){playerBox.style.display='block';streamPane.style.display='none'}\nfunction showStream(url){playerBox.style.display='none';streamPane.style.display='flex';streamUrl.textContent=url||''}\nfunction stopYoutube(){if(ytReady&&player){try{player.pauseVideo()}catch(e){}}}\nfunction stopStream(clear){try{audio.pause();if(clear){audio.removeAttribute('src');audio.load()}}catch(e){}}\nfunction loadPending(){if(!ytReady||!pendingId)return;let t=pendingType,id=pendingId;pendingType='';pendingId='';if(t==='video')gatLoadVideo(id);else if(t==='playlist')gatLoadPlaylist(id)}\nfunction onYouTubeIframeAPIReady(){player=new YT.Player('player',{width:'100%',height:'100%',playerVars:{controls:1,disablekb:0,fs:1,playsinline:1,rel:0,origin:'https://radio.gatlogets2.local',widget_referrer:'https://radio.gatlogets2.local/'},events:{onReady:function(){ytReady=true;loadPending()},onStateChange:function(e){if(e.data===YT.PlayerState.PLAYING){let d=player.getVideoData()||{};post({type:'track',title:d.title||''})}},onError:function(e){post({type:'error',code:e.data});if(currentType==='playlist'&&(e.data===100||e.data===101||e.data===150)){setTimeout(function(){try{player.nextVideo()}catch(x){}},700)}}}})}\nfunction gatLoadPlaylist(id){currentType='playlist';stopStream(true);showYoutube();if(!ytReady){pendingType='playlist';pendingId=id;return}try{player.loadPlaylist({listType:'playlist',list:id,index:0,startSeconds:0})}catch(e){post({type:'error',code:'load'})}}\nfunction gatLoadVideo(id){currentType='video';stopStream(true);showYoutube();if(!ytReady){pendingType='video';pendingId=id;return}try{player.loadVideoById(id)}catch(e){post({type:'error',code:'load'})}}\nfunction gatLoadStream(url){currentType='stream';pendingType='';pendingId='';stopYoutube();showStream(url);try{audio.pause();audio.src=url;audio.load();audio.play().catch(function(){post({type:'error',code:900})})}catch(e){post({type:'error',code:900})}}\nfunction gatPause(){if(currentType==='stream')stopStream(false);else stopYoutube()}\nfunction gatVolume(v){let n=Math.max(0,Math.min(100,Number(v)||0));if(ytReady&&player){try{player.setVolume(n)}catch(e){}}audio.volume=n/100}\naudio.addEventListener('playing',function(){post({type:'track',title:'Rádio online • AO VIVO'})});\naudio.addEventListener('waiting',function(){post({type:'track',title:'Rádio online • conectando...'})});\naudio.addEventListener('stalled',function(){post({type:'error',code:901})});\naudio.addEventListener('error',function(){post({type:'error',code:900})});\nsetTimeout(function(){post({type:'ready'})},0);\n</script></body></html>";
	}
}
