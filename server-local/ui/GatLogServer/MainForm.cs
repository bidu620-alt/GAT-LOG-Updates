using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace GatLogServer;

internal sealed class MainForm : Form
{
	private const string CurrentVersion = "1.0.39";

	private readonly AgentClient _agent = new AgentClient();

	private readonly Timer _timer = new Timer
	{
		Interval = 1000
	};

	private bool _refreshing;

	private DateTime _lastUpdateCheck = DateTime.MinValue;

	private ServerStatus _status = new ServerStatus();

	private ServerConfig _config = new ServerConfig();

	private readonly Dictionary<string, Panel> _pages = new Dictionary<string, Panel>(StringComparer.OrdinalIgnoreCase);

	private Panel _pageHost;

	private Label _lblServerState;

	private Label _lblSession;

	private Label _lblRoom;

	private Label _lblPorts;

	private Label _lblPlayers;

	private Label _lblAgent;

	private Label _lblPackages;

	private Label _lblFunnel;

	private Label _lblFooter;

	private DataGridView _bindingsGrid;

	private ListBox _modsList;

	private TextBox _cfgName;

	private TextBox _cfgDescription;

	private TextBox _cfgWelcome;

	private TextBox _cfgPassword;

	private TextBox _cfgMaxPlayers;

	private CheckBox _cfgTraffic;

	private CheckBox _cfgDamage;

	private CheckBox _cfgRegistration;

	private TextBox _moderatorId;

	private Label _moderatorState;

	private TextBox _accUser;

	private TextBox _accCurrent;

	private TextBox _accNew;

	private TextBox _accConfirm;

	private static readonly Color Bg = Color.FromArgb(3, 29, 44);

	private static readonly Color Card = Color.FromArgb(8, 54, 79);

	private static readonly Color Card2 = Color.FromArgb(8, 46, 67);

	private static readonly Color Blue = Color.FromArgb(31, 111, 211);

	private static readonly Color Blue2 = Color.FromArgb(14, 83, 119);

	private static readonly Color Green = Color.FromArgb(16, 185, 88);

	private static readonly Color Red = Color.FromArgb(210, 52, 43);

	private static readonly Color Purple = Color.FromArgb(92, 61, 177);

	private static readonly Color Orange = Color.FromArgb(227, 147, 0);

	private static readonly Color Cyan = Color.FromArgb(28, 132, 174);

	private static readonly Color Muted = Color.FromArgb(156, 195, 218);

	public MainForm()
	{
		Text = "GAT-LOG SERVER 1.0.39 | ETS2 + Telemetria";
		base.StartPosition = FormStartPosition.CenterScreen;
		MinimumSize = new Size(1180, 720);
		base.Size = new Size(1300, 830);
		BackColor = Bg;
		ForeColor = Color.White;
		Font = new Font("Segoe UI", 10f);
		DoubleBuffered = true;
		base.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
		BuildShell();
		BuildPages();
		ShowPage("home");
		_timer.Tick += async delegate
		{
			await RefreshStatusAsync();
			if ((DateTime.UtcNow - _lastUpdateCheck).TotalMinutes >= 30.0)
			{
				_lastUpdateCheck = DateTime.UtcNow;
				UpdateService.CheckAsync("1.0.39", this, silent: true);
			}
		};
		base.Shown += async delegate
		{
			await StartAsync();
		};
		base.FormClosed += delegate
		{
			_timer.Stop();
			_agent.Dispose();
		};
	}

	private async Task StartAsync()
	{
		_lblFooter.Text = "Conectando ao agente...";
		if (await _agent.EnsureAgentAsync())
		{
			await LoadConfigAsync();
			await RefreshStatusAsync();
		}
		else
		{
			_lblFooter.Text = "Agente 5055 não encontrado";
			MessageBox.Show(this, "Não foi possível iniciar ou localizar o GAT_LOG_AGENT.exe.\r\nA interface continuará aberta para diagnóstico.", "GAT-LOG", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
		}
		_timer.Start();
		UpdateService.CheckAsync("1.0.39", this, silent: true);
	}

	private void BuildShell()
	{
		Panel panel = new Panel
		{
			Dock = DockStyle.Left,
			Width = 205,
			BackColor = Color.FromArgb(2, 37, 55),
			Padding = new Padding(10)
		};
		base.Controls.Add(panel);
		PictureBox pictureBox = new PictureBox
		{
			Location = new Point(20, 16),
			Size = new Size(165, 145),
			SizeMode = PictureBoxSizeMode.Zoom,
			BackColor = Color.Black
		};
		pictureBox.Image = LoadAsset("logo.png");
		panel.Controls.Add(pictureBox);
		panel.Controls.Add(new Label
		{
			Text = "GAT LOG",
			Font = new Font("Segoe UI", 18f, FontStyle.Bold),
			AutoSize = true,
			Location = new Point(48, 168),
			ForeColor = Color.White
		});
		panel.AutoScroll = true;
		int y = 210;
		AddSideButton(panel, "INÍCIO", "home", ref y);
		AddSideButton(panel, "CONFIGURAÇÕES", "config", ref y);
		AddSideButton(panel, "MODERADOR", "moderator", ref y);
		AddSideButton(panel, "SISTEMA", "system", ref y);
		AddSideButton(panel, "CENTRAL DO SITE", "central", ref y);
		AddSideButton(panel, "CONTA / SENHA", "account", ref y);
		Button button = MakeButton("ATUALIZAR APP", Blue, 15, y, 175, 48);
		button.Click += async delegate
		{
			await UpdateService.CheckAsync("1.0.39", this, silent: false);
		};
		panel.Controls.Add(button);
		panel.Controls.Add(new Label
		{
			Text = "Proprietário do GAT-LOG:\r\nBiduzao",
			ForeColor = Color.Gold,
			Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
			TextAlign = ContentAlignment.MiddleCenter,
			Location = new Point(10, y + 60),
			Size = new Size(180, 55)
		});
		panel.Controls.Add(new Label
		{
			Text = "C# WinForms 1.0.39",
			ForeColor = Muted,
			Location = new Point(16, 760),
			AutoSize = true,
			Anchor = (AnchorStyles.Bottom | AnchorStyles.Left)
		});
		Panel panel2 = new Panel
		{
			Dock = DockStyle.Fill,
			BackColor = Bg,
			Padding = new Padding(10)
		};
		base.Controls.Add(panel2);
		panel2.BringToFront();
		Panel header = new Panel
		{
			Dock = DockStyle.Top,
			Height = 300,
			BackColor = Color.Black
		};
		panel2.Controls.Add(header);
		PictureBox pictureBox2 = new PictureBox
		{
			Dock = DockStyle.Fill,
			SizeMode = PictureBoxSizeMode.StretchImage,
			BackColor = Color.Black
		};
		pictureBox2.Image = LoadAsset("banner.png");
		header.Controls.Add(pictureBox2);
		Panel panel3 = new Panel
		{
			BackColor = Card2,
			Location = new Point(20, 35),
			Size = new Size(400, 225)
		};
		header.Controls.Add(panel3);
		panel3.BringToFront();
		panel3.Controls.Add(new Label
		{
			Text = "GAT-LOG SERVER",
			Font = new Font("Segoe UI", 22f, FontStyle.Bold),
			ForeColor = Color.White,
			Location = new Point(25, 20),
			Size = new Size(350, 46)
		});
		panel3.Controls.Add(new Label
		{
			Text = "Servidor dedicado autônomo - ETS2 | 128 jogadores",
			ForeColor = Color.FromArgb(225, 238, 247),
			Location = new Point(25, 72),
			Size = new Size(350, 26)
		});
		_lblServerState = new Label
		{
			Text = "SERVIDOR ...",
			Font = new Font("Segoe UI", 17f, FontStyle.Bold),
			ForeColor = Color.Gold,
			Location = new Point(25, 120),
			Size = new Size(350, 38)
		};
		_lblSession = new Label
		{
			Text = "Sessão: -",
			ForeColor = Color.White,
			Location = new Point(25, 174),
			Size = new Size(350, 28)
		};
		panel3.Controls.Add(_lblServerState);
		panel3.Controls.Add(_lblSession);
		Panel room = new Panel
		{
			BackColor = Card2,
			Width = 300,
			Height = 175,
			Anchor = (AnchorStyles.Top | AnchorStyles.Right),
			Location = new Point(header.Width - 320, 35)
		};
		room.Left = header.ClientSize.Width - room.Width - 20;
		header.SizeChanged += delegate
		{
			room.Left = Math.Max(450, header.ClientSize.Width - room.Width - 20);
		};
		header.Controls.Add(room);
		room.BringToFront();
		room.Controls.Add(new Label
		{
			Text = "ID DA SALA",
			Font = new Font("Segoe UI", 11f, FontStyle.Bold),
			Location = new Point(18, 16),
			Size = new Size(250, 26)
		});
		_lblRoom = new Label
		{
			Text = "-",
			BackColor = Color.White,
			ForeColor = Color.FromArgb(20, 40, 60),
			Location = new Point(18, 53),
			Size = new Size(188, 37),
			TextAlign = ContentAlignment.MiddleLeft,
			Padding = new Padding(6, 0, 0, 0)
		};
		room.Controls.Add(_lblRoom);
		Button button2 = MakeButton("COPIAR", Blue, 214, 53, 70, 37);
		button2.Click += delegate
		{
			if (!string.IsNullOrWhiteSpace(_status.SessionId))
			{
				Clipboard.SetText(_status.SessionId);
			}
		};
		room.Controls.Add(button2);
		_lblPorts = new Label
		{
			Text = "Portas: -",
			Location = new Point(18, 110),
			Size = new Size(260, 28),
			ForeColor = Color.White
		};
		room.Controls.Add(_lblPorts);
		_pageHost = new Panel
		{
			Dock = DockStyle.Fill,
			BackColor = Bg,
			Padding = new Padding(0, 12, 0, 0)
		};
		panel2.Controls.Add(_pageHost);
		_pageHost.BringToFront();
		_lblFooter = new Label
		{
			Dock = DockStyle.Bottom,
			Height = 22,
			ForeColor = Muted,
			TextAlign = ContentAlignment.MiddleRight,
			Text = "Pronto"
		};
		panel2.Controls.Add(_lblFooter);
	}

	private void AddSideButton(Panel sidebar, string text, string page, ref int y)
	{
		Button button = MakeButton(text, Blue2, 5, y, 185, 52);
		button.Click += async delegate
		{
			ShowPage(page);
			if (page == "system")
			{
				await RefreshSystemExtrasAsync();
			}
		};
		sidebar.Controls.Add(button);
		y += 54;
	}

	private void BuildPages()
	{
		BuildHome();
		BuildConfig();
		BuildModerator();
		BuildSystem();
		BuildAccount();
		NewPage("central").Controls.Add(new CentralPanel());
	}

	private Panel NewPage(string key)
	{
		Panel panel = new Panel
		{
			Dock = DockStyle.Fill,
			BackColor = Bg,
			AutoScroll = true
		};
		_pages[key] = panel;
		_pageHost.Controls.Add(panel);
		return panel;
	}

	private void ShowPage(string key)
	{
		foreach (Panel value2 in _pages.Values)
		{
			value2.Visible = false;
		}
		if (_pages.TryGetValue(key, out var value))
		{
			value.Visible = true;
			value.BringToFront();
		}
	}

	private void BuildHome()
	{
		Panel panel = NewPage("home");
		Panel panel2 = MakeCard(10, 5, 690, 225);
		panel.Controls.Add(panel2);
		panel2.Controls.Add(Title("HORÁRIO, CLIMA E TRÁFEGO", 20, 16, 600));
		Button button = MakeButton("DIA  06:00", Orange, 20, 62, 130, 66);
		button.Click += delegate
		{
			CopyCommand("/set_time 06:00", "Comando de DIA copiado.");
		};
		panel2.Controls.Add(button);
		Button button2 = MakeButton("NOITE  20:00", Purple, 160, 62, 130, 66);
		button2.Click += delegate
		{
			CopyCommand("/set_time 20:00", "Comando de NOITE copiado.");
		};
		panel2.Controls.Add(button2);
		Button button3 = MakeButton("SEM CHUVA", Color.FromArgb(38, 119, 190), 300, 62, 130, 66);
		button3.Click += delegate
		{
			CopyCommand("/set_rain_factor 0", "Comando SEM CHUVA copiado.");
		};
		panel2.Controls.Add(button3);
		Button button4 = MakeButton("CHUVA FORTE", Color.FromArgb(38, 119, 190), 440, 62, 130, 66);
		button4.Click += delegate
		{
			CopyCommand("/set_rain_factor 1", "Comando CHUVA FORTE copiado.");
		};
		panel2.Controls.Add(button4);
		Button button5 = MakeButton("SEM TRÁFEGO", Color.FromArgb(20, 145, 70), 20, 143, 180, 58);
		button5.Click += async delegate
		{
			await SetTrafficAsync(enabled: false);
		};
		panel2.Controls.Add(button5);
		Button button6 = MakeButton("COM TRÁFEGO", Cyan, 210, 143, 180, 58);
		button6.Click += async delegate
		{
			await SetTrafficAsync(enabled: true);
		};
		panel2.Controls.Add(button6);
		Button button7 = MakeButton("ACESSO DE MODERADOR", Purple, 400, 143, 250, 58);
		button7.Click += delegate
		{
			ShowPage("moderator");
		};
		panel2.Controls.Add(button7);
		Panel panel3 = MakeCard(715, 5, 360, 225);
		panel3.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
		panel.Controls.Add(panel3);
		panel3.Controls.Add(Title("STATUS DO SERVIDOR", 20, 16, 320));
		_lblAgent = InfoLabel("Agente/API 5055: verificando...", 20, 60, 320, Color.Gold);
		_lblPackages = InfoLabel("Pacotes: verificando...", 20, 95, 320, Color.Gold);
		_lblFunnel = InfoLabel("Funnel: -", 20, 145, 320, Color.DeepSkyBlue);
		panel3.Controls.Add(_lblAgent);
		panel3.Controls.Add(_lblPackages);
		panel3.Controls.Add(_lblFunnel);
		Panel panel4 = MakeCard(10, 242, 1065, 82);
		panel4.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
		panel.Controls.Add(panel4);
		Button button8 = MakeButton("INICIAR SERVIDOR", Color.FromArgb(16, 157, 66), 16, 15, 180, 52);
		button8.Click += async delegate
		{
			await DoActionAsync("start_server");
		};
		panel4.Controls.Add(button8);
		Button button9 = MakeButton("PARAR SERVIDOR", Red, 208, 15, 180, 52);
		button9.Click += async delegate
		{
			if (MessageBox.Show(this, "Deseja parar o servidor ETS2?", "GAT-LOG", MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation) == DialogResult.Yes)
			{
				await DoActionAsync("stop_server");
			}
		};
		panel4.Controls.Add(button9);
		Button button10 = MakeButton("ATUALIZAR MODS / PACOTES", Blue, 400, 15, 220, 52);
		button10.Click += async delegate
		{
			await DoActionAsync("prepare_mods");
		};
		panel4.Controls.Add(button10);
		Button button11 = MakeButton("VER MODS", Blue2, 632, 15, 130, 52);
		button11.Click += async delegate
		{
			ShowPage("system");
			await RefreshSystemExtrasAsync();
		};
		panel4.Controls.Add(button11);
		Button button12 = MakeButton("FIREWALL", Blue2, 774, 15, 130, 52);
		button12.Click += async delegate
		{
			await DoActionAsync("firewall");
		};
		panel4.Controls.Add(button12);
		Button button13 = MakeButton("REDETECTAR", Blue2, 916, 15, 130, 52);
		button13.Click += async delegate
		{
			await DoActionAsync("redetect");
		};
		panel4.Controls.Add(button13);
		Panel panel5 = MakeCard(10, 336, 1065, 145);
		panel5.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
		panel.Controls.Add(panel5);
		panel5.Controls.Add(Title("JOGADORES ONLINE", 20, 14, 500));
		_lblPlayers = new Label
		{
			Text = "0 / 128",
			Font = new Font("Segoe UI", 25f, FontStyle.Bold),
			ForeColor = Color.FromArgb(40, 137, 255),
			Location = new Point(20, 62),
			Size = new Size(190, 55)
		};
		panel5.Controls.Add(_lblPlayers);
		panel5.Controls.Add(new Label
		{
			Text = "Jogadores detectados no comboio atual. A telemetria de viagem e enviada pelo app do motorista direto para a Cloudflare.",
			ForeColor = Color.White,
			Location = new Point(220, 70),
			Size = new Size(700, 35)
		});
	}

	private void BuildConfig()
	{
		Panel panel = NewPage("config");
		Panel panel2 = MakeCard(10, 5, 920, 500);
		panel.Controls.Add(panel2);
		panel2.Controls.Add(Title("CONFIGURAÇÕES DO SERVIDOR", 20, 18, 700));
		_cfgName = AddField(panel2, "Nome do servidor", 30, 82, 390);
		_cfgDescription = AddField(panel2, "Descrição", 470, 82, 390);
		_cfgWelcome = AddField(panel2, "Mensagem de boas-vindas", 30, 160, 390);
		_cfgPassword = AddField(panel2, "Senha da sessão", 470, 160, 390);
		_cfgMaxPlayers = AddField(panel2, "Máximo de jogadores", 30, 238, 180);
		_cfgTraffic = AddCheck(panel2, "Tráfego", 270, 252);
		_cfgDamage = AddCheck(panel2, "Dano entre jogadores", 430, 252);
		_cfgRegistration = AddCheck(panel2, "Permitir novos clientes", 650, 252);
		Button save = MakeButton("SALVAR CONFIGURAÇÕES", Blue, 30, 330, 260, 52);
		save.Click += async delegate
		{
			await SaveConfigAsync(save);
		};
		panel2.Controls.Add(save);
		panel2.Controls.Add(new Label
		{
			Text = "O salvamento é assíncrono: a janela não espera leitura de logs, Tailscale ou servidor dedicado.",
			ForeColor = Muted,
			Location = new Point(30, 405),
			Size = new Size(800, 45)
		});
	}

	private void BuildModerator()
	{
		Panel panel = NewPage("moderator");
		Panel panel2 = MakeCard(10, 5, 880, 370);
		panel.Controls.Add(panel2);
		panel2.Controls.Add(Title("MODERADOR", 20, 18, 500));
		_moderatorId = AddField(panel2, "Steam ID64 do moderador", 30, 85, 430);
		Button save = MakeButton("SALVAR MODERADOR", Blue, 500, 108, 220, 44);
		save.Click += async delegate
		{
			await SaveModeratorAsync(save);
		};
		panel2.Controls.Add(save);
		_moderatorState = InfoLabel("Moderador: não configurado", 30, 175, 700, Color.Gold);
		panel2.Controls.Add(_moderatorState);
		Button button = MakeButton("DIA 06:00", Orange, 30, 230, 150, 55);
		button.Click += delegate
		{
			CopyCommand("/set_time 06:00", "Comando copiado.");
		};
		panel2.Controls.Add(button);
		Button button2 = MakeButton("NOITE 20:00", Purple, 195, 230, 150, 55);
		button2.Click += delegate
		{
			CopyCommand("/set_time 20:00", "Comando copiado.");
		};
		panel2.Controls.Add(button2);
		Button button3 = MakeButton("SEM CHUVA", Blue, 360, 230, 150, 55);
		button3.Click += delegate
		{
			CopyCommand("/set_rain_factor 0", "Comando copiado.");
		};
		panel2.Controls.Add(button3);
		Button button4 = MakeButton("CHUVA FORTE", Blue, 525, 230, 150, 55);
		button4.Click += delegate
		{
			CopyCommand("/set_rain_factor 1", "Comando copiado.");
		};
		panel2.Controls.Add(button4);
	}

	private void BuildSystem()
	{
		Panel panel = NewPage("system");
		Panel panel2 = MakeCard(10, 5, 1040, 190);
		panel.Controls.Add(panel2);
		panel2.Controls.Add(Title("SISTEMA / DIAGNÓSTICO", 20, 16, 600));
		Label value = new Label
		{
			Name = "systemInfo",
			ForeColor = Color.FromArgb(140, 232, 180),
			Location = new Point(20, 58),
			Size = new Size(990, 110),
			Font = new Font("Consolas", 9.5f)
		};
		panel2.Controls.Add(value);
		Panel panel3 = MakeCard(10, 208, 1040, 76);
		panel.Controls.Add(panel3);
		string[] array = new string[6] { "BACKUP", "ABRIR DADOS", "FUNNEL", "REINICIAR AGENTE", "FIREWALL", "REDETECTAR" };
		string[] acts = new string[6] { "backup", "open", "funnel", "restart", "firewall", "redetect" };
		for (int i = 0; i < array.Length; i++)
		{
			int idx = i;
			Button button = MakeButton(array[i], (idx == 2) ? Purple : Blue2, 12 + i * 168, 13, 155, 48);
			button.Click += async delegate
			{
				if (acts[idx] == "open")
				{
					OpenDataDir();
				}
				else if (acts[idx] == "restart")
				{
					await RestartAgentAsync();
				}
				else
				{
					await DoActionAsync(acts[idx]);
				}
			};
			panel3.Controls.Add(button);
		}
		Panel panel4 = MakeCard(10, 297, 505, 330);
		panel.Controls.Add(panel4);
		panel4.Controls.Add(Title("MODS DETECTADOS", 20, 14, 400));
		_modsList = new ListBox
		{
			Location = new Point(20, 55),
			Size = new Size(465, 250),
			BackColor = Color.FromArgb(4, 36, 54),
			ForeColor = Color.White,
			BorderStyle = BorderStyle.FixedSingle
		};
		panel4.Controls.Add(_modsList);
		Panel panel5 = MakeCard(530, 297, 520, 330);
		panel.Controls.Add(panel5);
		panel5.Controls.Add(Title("CLIENTES VINCULADOS", 20, 14, 400));
		_bindingsGrid = NewGrid(new string[4] { "MOTORISTA", "PC", "STATUS", "ÚLTIMO CONTATO" });
		_bindingsGrid.Location = new Point(20, 55);
		_bindingsGrid.Size = new Size(480, 250);
		panel5.Controls.Add(_bindingsGrid);
	}

	private void BuildAccount()
	{
		Panel panel = NewPage("account");
		Panel panel2 = MakeCard(10, 5, 760, 470);
		panel.Controls.Add(panel2);
		panel2.Controls.Add(Title("CONTA / SENHA", 20, 18, 500));
		_accUser = AddField(panel2, "Usuário", 30, 82, 330);
		_accCurrent = AddField(panel2, "Senha atual", 390, 82, 300);
		_accCurrent.UseSystemPasswordChar = true;
		_accNew = AddField(panel2, "Nova senha", 30, 170, 330);
		_accNew.UseSystemPasswordChar = true;
		_accConfirm = AddField(panel2, "Confirmar nova senha", 390, 170, 300);
		_accConfirm.UseSystemPasswordChar = true;
		Button button = MakeButton("SALVAR NOVA SENHA", Blue, 30, 280, 250, 50);
		button.Click += delegate
		{
			ChangePassword();
		};
		panel2.Controls.Add(button);
		panel2.Controls.Add(new Label
		{
			Text = "A senha continua usando o mesmo formato criptográfico das versões anteriores.",
			ForeColor = Muted,
			Location = new Point(30, 365),
			Size = new Size(650, 35)
		});
	}

	private async Task LoadConfigAsync()
	{
		try
		{
			_config = await _agent.GetConfigAsync();
			ApplyConfigToControls();
			_lblFooter.Text = "Configuração carregada";
		}
		catch (Exception ex)
		{
			_lblFooter.Text = "Configuração: " + ex.Message;
		}
	}

	private async Task RefreshStatusAsync()
	{
		if (_refreshing)
		{
			return;
		}
		_refreshing = true;
		try
		{
			if (!(await _agent.HealthAsync()))
			{
				_lblAgent.Text = "Agente/API 5055: INATIVO";
				_lblAgent.ForeColor = Color.OrangeRed;
				_lblFooter.Text = "Agente desconectado - tentando reconectar...";
				await _agent.EnsureAgentAsync();
			}
			else
			{
				_status = await _agent.GetStatusAsync();
				ApplyStatus();
			}
		}
		catch (Exception ex)
		{
			_lblFooter.Text = "Atualização: " + ex.Message;
		}
		finally
		{
			_refreshing = false;
		}
	}

	private void ApplyStatus()
	{
		_lblServerState.Text = (_status.ServerOnline ? "SERVIDOR ONLINE" : "SERVIDOR OFFLINE");
		_lblServerState.ForeColor = (_status.ServerOnline ? Color.FromArgb(38, 238, 122) : Color.OrangeRed);
		_lblSession.Text = "Sessão: " + (string.IsNullOrWhiteSpace(_status.ServerName) ? "-" : _status.ServerName);
		_lblRoom.Text = (string.IsNullOrWhiteSpace(_status.SessionId) ? "-" : _status.SessionId);
		_lblPorts.Text = "Portas: " + (string.IsNullOrWhiteSpace(_status.Ports) ? "27015 / 27016" : _status.Ports);
		_lblPlayers.Text = _status.PlayerCount + " / " + ((_status.MaxPlayers <= 0) ? 128 : _status.MaxPlayers);
		_lblAgent.Text = "Agente/API 5055: ATIVA | v" + (_status.AgentVersion ?? "-");
		_lblAgent.ForeColor = Color.FromArgb(24, 235, 123);
		_lblPackages.Text = "Pacotes: " + (_status.PackagesText ?? "-");
		_lblPackages.ForeColor = (_status.PackagesOk ? Color.FromArgb(24, 235, 123) : Color.Gold);
		_lblFunnel.Text = "Funnel: " + (string.IsNullOrWhiteSpace(_status.FunnelUrl) ? "-" : _status.FunnelUrl);
		_lblFooter.Text = "Última atualização: " + DateTime.Now.ToString("HH:mm:ss") + " | UI não bloqueante";
		if (_moderatorState != null)
		{
			bool flag = !string.IsNullOrWhiteSpace(_config.ModeratorSteamId);
			_moderatorState.Text = (flag ? ("Moderador ATIVO: " + _config.ModeratorSteamId) : "Moderador: NÃO CONFIGURADO");
			_moderatorState.ForeColor = (flag ? Color.FromArgb(24, 235, 123) : Color.OrangeRed);
		}
		UpdateSystemInfo();
	}

	private void ApplyConfigToControls()
	{
		if (_cfgName != null)
		{
			_cfgName.Text = _config.ServerName ?? "";
			_cfgDescription.Text = _config.Description ?? "";
			_cfgWelcome.Text = _config.WelcomeMessage ?? "";
			_cfgPassword.Text = _config.ServerPassword ?? "";
			_cfgMaxPlayers.Text = ((_config.MaxPlayers <= 0) ? 128 : _config.MaxPlayers).ToString();
			_cfgTraffic.Checked = _config.Traffic;
			_cfgDamage.Checked = _config.PlayerDamage;
			_cfgRegistration.Checked = _config.RegistrationOpen;
		}
		if (_moderatorId != null)
		{
			_moderatorId.Text = _config.ModeratorSteamId ?? "";
		}
		if (_accUser != null)
		{
			_accUser.Text = AuthService.EnsureAuth().User;
		}
	}

	private async Task SaveConfigAsync(Button button)
	{
		if (!int.TryParse(_cfgMaxPlayers.Text.Trim(), out var result) || result < 1)
		{
			result = 128;
		}
		_config.ServerName = _cfgName.Text.Trim();
		_config.Description = _cfgDescription.Text.Trim();
		_config.WelcomeMessage = _cfgWelcome.Text.Trim();
		_config.ServerPassword = _cfgPassword.Text;
		_config.MaxPlayers = result;
		_config.Traffic = _cfgTraffic.Checked;
		_config.PlayerDamage = _cfgDamage.Checked;
		_config.RegistrationOpen = _cfgRegistration.Checked;
		await WithButtonAsync(button, async delegate
		{
			await _agent.SaveConfigAsync(_config);
			await LoadConfigAsync();
			await RefreshStatusAsync();
			MessageBox.Show(this, "Configurações salvas.", "GAT-LOG", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
		});
	}

	private async Task SaveModeratorAsync(Button button)
	{
		string text = _moderatorId.Text.Trim();
		if (text.Length > 0 && (text.Length < 15 || text.Length > 20 || text.Any((char c) => !char.IsDigit(c))))
		{
			MessageBox.Show(this, "Informe um Steam ID64 válido ou deixe vazio para remover.", "GAT-LOG", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
			return;
		}
		_config.ModeratorSteamId = text;
		await WithButtonAsync(button, async delegate
		{
			await _agent.SaveConfigAsync(_config);
			await LoadConfigAsync();
			await RefreshStatusAsync();
			MessageBox.Show(this, "Moderador salvo.", "GAT-LOG", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
		});
	}

	private async Task SetTrafficAsync(bool enabled)
	{
		_ = 1;
		try
		{
			_config.Traffic = enabled;
			await _agent.SaveConfigAsync(_config);
			await RefreshStatusAsync();
			_lblFooter.Text = (enabled ? "Tráfego ativado" : "Tráfego desativado");
		}
		catch (Exception ex)
		{
			ShowError(ex);
		}
	}

	private async Task DoActionAsync(string action)
	{
		_ = 1;
		try
		{
			_lblFooter.Text = "Executando " + action + "...";
			string text = await _agent.ActionAsync(action);
			_lblFooter.Text = text;
			await RefreshStatusAsync();
		}
		catch (Exception ex)
		{
			ShowError(ex);
		}
	}

	private async Task RestartAgentAsync()
	{
		try
		{
			await _agent.ActionAsync("shutdown_agent");
		}
		catch
		{
		}
		await Task.Delay(400);
		bool flag = await _agent.EnsureAgentAsync();
		_lblFooter.Text = (flag ? "Agente reiniciado" : "Falha ao reiniciar agente");
		await RefreshStatusAsync();
	}

	private async Task RefreshSystemExtrasAsync()
	{
		try
		{
			Task<List<string>> modsTask = _agent.GetModsAsync();
			Task<List<BindingInfo>> bindTask = _agent.GetBindingsAsync();
			await Task.WhenAll(modsTask, bindTask);
			_modsList.Items.Clear();
			foreach (string item in modsTask.Result)
			{
				_modsList.Items.Add(item);
			}
			_bindingsGrid.Rows.Clear();
			foreach (BindingInfo item2 in bindTask.Result.OrderBy((BindingInfo x) => x.Driver, StringComparer.OrdinalIgnoreCase))
			{
				string text = (item2.Blocked ? "BLOQUEADO" : (item2.Disconnected ? "DESCONECTADO" : "ATIVO"));
				_bindingsGrid.Rows.Add(item2.Driver, string.IsNullOrWhiteSpace(item2.DeviceId) ? "-" : item2.DeviceId, text, item2.LastSeen);
			}
			UpdateSystemInfo();
		}
		catch (Exception ex)
		{
			_lblFooter.Text = "Sistema: " + ex.Message;
		}
	}

	private void UpdateSystemInfo()
	{
		if (_pages.TryGetValue("system", out var value) && value.Controls.Find("systemInfo", searchAllChildren: true).FirstOrDefault() is Label label)
		{
			label.Text = "Agente: " + (_status.AgentVersion ?? "-") + " | Uptime: " + _status.AgentUptimeSec + " s | API 5055: ATIVA\r\nServidor: " + (_status.ServerOnline ? "ONLINE" : "OFFLINE") + " | Executável: " + (_status.ServerExe ?? "-") + "\r\nLog: " + (_status.ServerLog ?? "-") + "\r\nDados: " + (_status.DataDir ?? AuthService.DataDir) + "\r\nFunnel: " + (string.IsNullOrWhiteSpace(_status.FunnelUrl) ? "-" : _status.FunnelUrl);
		}
	}

	private void ChangePassword()
	{
		try
		{
			if (!AuthService.Verify(_accUser.Text, _accCurrent.Text))
			{
				throw new InvalidOperationException("Senha atual incorreta.");
			}
			if (_accNew.Text != _accConfirm.Text)
			{
				throw new InvalidOperationException("A confirmação da nova senha não confere.");
			}
			AuthService.Change(_accUser.Text.Trim(), _accNew.Text);
			_accCurrent.Clear();
			_accNew.Clear();
			_accConfirm.Clear();
			MessageBox.Show(this, "Usuário/senha atualizados.", "GAT-LOG", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
		}
		catch (Exception ex)
		{
			ShowError(ex);
		}
	}

	private void CopyCommand(string command, string message)
	{
		Clipboard.SetText(command);
		_lblFooter.Text = message + " Cole no chat (Y) do ETS2.";
	}

	private void OpenDataDir()
	{
		Directory.CreateDirectory(AuthService.DataDir);
		Process.Start(new ProcessStartInfo("explorer.exe", "\"" + AuthService.DataDir + "\"")
		{
			UseShellExecute = true
		});
	}

	private async Task WithButtonAsync(Button b, Func<Task> action)
	{
		string old = b.Text;
		b.Enabled = false;
		b.Text = "AGUARDE...";
		try
		{
			await action();
		}
		catch (Exception ex)
		{
			ShowError(ex);
		}
		finally
		{
			b.Text = old;
			b.Enabled = true;
		}
	}

	private void ShowError(Exception ex)
	{
		_lblFooter.Text = ex.Message;
		MessageBox.Show(this, ex.Message, "GAT-LOG", MessageBoxButtons.OK, MessageBoxIcon.Hand);
	}

	private static Panel MakeCard(int x, int y, int w, int h)
	{
		return new Panel
		{
			Location = new Point(x, y),
			Size = new Size(w, h),
			BackColor = Card
		};
	}

	private static Label Title(string text, int x, int y, int w)
	{
		return new Label
		{
			Text = text,
			Font = new Font("Segoe UI", 14f, FontStyle.Bold),
			ForeColor = Color.White,
			Location = new Point(x, y),
			Size = new Size(w, 35)
		};
	}

	private static Label InfoLabel(string text, int x, int y, int w, Color color)
	{
		return new Label
		{
			Text = text,
			Location = new Point(x, y),
			Size = new Size(w, 42),
			ForeColor = color
		};
	}

	private static Button MakeButton(string text, Color color, int x, int y, int w, int h)
	{
		Button button = new Button();
		button.Text = text;
		button.Location = new Point(x, y);
		button.Size = new Size(w, h);
		button.BackColor = color;
		button.ForeColor = Color.White;
		button.FlatStyle = FlatStyle.Flat;
		button.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
		button.Cursor = Cursors.Hand;
		button.FlatAppearance.BorderSize = 0;
		return button;
	}

	private static TextBox AddField(Control parent, string label, int x, int y, int width)
	{
		parent.Controls.Add(new Label
		{
			Text = label,
			ForeColor = Color.FromArgb(210, 230, 242),
			Location = new Point(x, y),
			Size = new Size(width, 25)
		});
		TextBox textBox = new TextBox
		{
			Location = new Point(x, y + 28),
			Size = new Size(width, 28),
			BackColor = Color.White,
			ForeColor = Color.FromArgb(20, 40, 60)
		};
		parent.Controls.Add(textBox);
		return textBox;
	}

	private static CheckBox AddCheck(Control parent, string text, int x, int y)
	{
		CheckBox checkBox = new CheckBox
		{
			Text = text,
			Location = new Point(x, y),
			AutoSize = true,
			ForeColor = Color.White,
			BackColor = Color.Transparent
		};
		parent.Controls.Add(checkBox);
		return checkBox;
	}

	private static DataGridView NewGrid(string[] columns)
	{
		DataGridView dataGridView = new DataGridView
		{
			BackgroundColor = Color.FromArgb(5, 42, 62),
			BorderStyle = BorderStyle.None,
			ReadOnly = true,
			AllowUserToAddRows = false,
			AllowUserToDeleteRows = false,
			AllowUserToResizeRows = false,
			RowHeadersVisible = false,
			AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
			SelectionMode = DataGridViewSelectionMode.FullRowSelect,
			MultiSelect = false,
			EnableHeadersVisualStyles = false,
			GridColor = Color.FromArgb(20, 76, 101)
		};
		dataGridView.ColumnHeadersDefaultCellStyle.BackColor = Card2;
		dataGridView.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(87, 190, 255);
		dataGridView.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
		dataGridView.DefaultCellStyle.BackColor = Color.FromArgb(5, 42, 62);
		dataGridView.DefaultCellStyle.ForeColor = Color.White;
		dataGridView.DefaultCellStyle.SelectionBackColor = Color.FromArgb(17, 82, 117);
		dataGridView.DefaultCellStyle.SelectionForeColor = Color.White;
		dataGridView.RowTemplate.Height = 30;
		foreach (string text in columns)
		{
			dataGridView.Columns.Add(text.Replace(" ", "_"), text);
		}
		return dataGridView;
	}

	private static Image LoadAsset(string file)
	{
		try
		{
			string text = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", file);
			if (!File.Exists(text))
			{
				return null;
			}
			using Image original = Image.FromFile(text);
			return new Bitmap(original);
		}
		catch
		{
			return null;
		}
	}
}
