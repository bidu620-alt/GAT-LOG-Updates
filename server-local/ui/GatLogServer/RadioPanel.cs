using System;
using System.Drawing;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GatLogServer;

internal sealed class RadioPanel : UserControl
{
    private readonly HttpClient _http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
    private readonly TextBox _user = new TextBox();
    private readonly TextBox _password = new TextBox();
    private readonly TextBox _playlist = new TextBox();
    private readonly TextBox _label = new TextBox();
    private readonly CheckBox _enabled = new CheckBox();
    private readonly Label _state = new Label();
    private readonly Label _details = new Label();
    private bool _busy;

    private const string ApiBase = "http://127.0.0.1:5056";
    private static readonly Color Bg = Color.FromArgb(3, 29, 44);
    private static readonly Color Card = Color.FromArgb(8, 54, 79);
    private static readonly Color Blue = Color.FromArgb(31, 111, 211);
    private static readonly Color Red = Color.FromArgb(210, 52, 43);
    private static readonly Color Muted = Color.FromArgb(156, 195, 218);

    internal RadioPanel()
    {
        Dock = DockStyle.Fill;
        AutoScroll = true;
        BackColor = Bg;
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 10f);

        var card = new Panel
        {
            Location = new Point(10, 5),
            Size = new Size(980, 545),
            BackColor = Card,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        Controls.Add(card);

        card.Controls.Add(new Label
        {
            Text = "RÁDIO GAT",
            Font = new Font("Segoe UI", 22f, FontStyle.Bold),
            Location = new Point(24, 18),
            Size = new Size(420, 44),
            ForeColor = Color.White
        });

        card.Controls.Add(new Label
        {
            Text = "Controle a playlist que toca somente no GAT Telemetria dos motoristas.",
            Location = new Point(28, 64),
            Size = new Size(760, 28),
            ForeColor = Muted
        });

        _state.Location = new Point(28, 102);
        _state.Size = new Size(890, 30);
        _state.Font = new Font("Segoe UI", 11f, FontStyle.Bold);
        _state.ForeColor = Color.Gold;
        _state.Text = "Rádio: carregando...";
        card.Controls.Add(_state);

        _details.Location = new Point(28, 132);
        _details.Size = new Size(900, 42);
        _details.ForeColor = Muted;
        _details.Text = "A Central precisa estar ativa na porta 5056.";
        card.Controls.Add(_details);

        AddLabel(card, "Usuário Admin/Moderador do site", 28, 188, 300);
        SetupTextBox(_user, 28, 214, 330, false);
        card.Controls.Add(_user);

        AddLabel(card, "Senha do site", 390, 188, 220);
        SetupTextBox(_password, 390, 214, 300, true);
        card.Controls.Add(_password);

        AddLabel(card, "Nome da rádio", 28, 270, 220);
        SetupTextBox(_label, 28, 296, 330, false);
        _label.Text = "Radio GAT";
        card.Controls.Add(_label);

        AddLabel(card, "Playlist do YouTube", 390, 270, 220);
        SetupTextBox(_playlist, 390, 296, 520, false);
        card.Controls.Add(_playlist);

        _enabled.Text = "Rádio ligada para os motoristas";
        _enabled.AutoSize = true;
        _enabled.Location = new Point(28, 354);
        _enabled.ForeColor = Color.White;
        card.Controls.Add(_enabled);

        var load = MakeButton("CARREGAR ATUAL", Color.FromArgb(14, 83, 119), 28, 402, 190, 50);
        load.Click += async (_, _) => await LoadStateAsync(showErrors: true);
        card.Controls.Add(load);

        var save = MakeButton("SALVAR RÁDIO", Blue, 232, 402, 190, 50);
        save.Click += async (_, _) => await SaveAsync(save, turnOff: false);
        card.Controls.Add(save);

        var off = MakeButton("DESLIGAR RÁDIO", Red, 436, 402, 190, 50);
        off.Click += async (_, _) => await SaveAsync(off, turnOff: true);
        card.Controls.Add(off);

        card.Controls.Add(new Label
        {
            Text = "A senha é usada somente para autenticar esta alteração e não é salva pelo GAT Server. O servidor grava apenas o estado da rádio, nome e ID da playlist.",
            Location = new Point(28, 472),
            Size = new Size(890, 50),
            ForeColor = Muted
        });

        Disposed += (_, _) => _http.Dispose();
        _ = LoadStateAsync(showErrors: false);
    }

    private static void AddLabel(Control parent, string text, int x, int y, int width)
    {
        parent.Controls.Add(new Label
        {
            Text = text,
            Location = new Point(x, y),
            Size = new Size(width, 24),
            ForeColor = Color.White
        });
    }

    private static void SetupTextBox(TextBox box, int x, int y, int width, bool password)
    {
        box.Location = new Point(x, y);
        box.Size = new Size(width, 30);
        box.BackColor = Color.White;
        box.ForeColor = Color.FromArgb(20, 40, 60);
        box.BorderStyle = BorderStyle.FixedSingle;
        box.UseSystemPasswordChar = password;
    }

    private static Button MakeButton(string text, Color color, int x, int y, int width, int height)
    {
        return new Button
        {
            Text = text,
            Location = new Point(x, y),
            Size = new Size(width, height),
            BackColor = color,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 10f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
    }

    private async Task<JObject> GetJsonAsync(string path)
    {
        using var response = await _http.GetAsync(ApiBase + path);
        var text = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException("HTTP " + (int)response.StatusCode + (string.IsNullOrWhiteSpace(text) ? "" : ": " + text));
        return JObject.Parse(text);
    }

    private async Task<JObject> PostJsonAsync(string path, object payload)
    {
        var json = JsonConvert.SerializeObject(payload);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await _http.PostAsync(ApiBase + path, content);
        var text = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            try
            {
                var error = (string)JObject.Parse(text)["error"];
                if (!string.IsNullOrWhiteSpace(error))
                    throw new InvalidOperationException(ErrorMessage(error));
            }
            catch (JsonReaderException)
            {
            }
            throw new InvalidOperationException("HTTP " + (int)response.StatusCode + (string.IsNullOrWhiteSpace(text) ? "" : ": " + text));
        }
        return JObject.Parse(text);
    }

    private static string ErrorMessage(string code)
    {
        switch (code)
        {
            case "invalid_credentials": return "Usuário ou senha do site incorretos.";
            case "forbidden": return "Esta conta não é Admin nem Moderador.";
            case "invalid_session": return "Sessão inválida. Entre novamente.";
            case "invalid_youtube_playlist": return "Link de playlist do YouTube inválido.";
            case "playlist_required": return "Informe uma playlist antes de ligar a rádio.";
            case "too_many_attempts": return "Muitas tentativas de login. Aguarde alguns minutos.";
            default: return "Erro da Central: " + code;
        }
    }

    private async Task LoadStateAsync(bool showErrors)
    {
        if (_busy || IsDisposed) return;
        _busy = true;
        try
        {
            var root = await GetJsonAsync("/api/public/radio");
            var radio = root["radio"] as JObject ?? new JObject();
            _enabled.Checked = (bool?)radio["enabled"] == true;
            _playlist.Text = (string)radio["playlist_url"] ?? "";
            _label.Text = (string)radio["label"] ?? "Radio GAT";
            var updatedBy = (string)radio["updated_by"] ?? "";
            var updatedAt = (string)radio["updated_at"] ?? "";
            _state.Text = _enabled.Checked ? "Rádio: LIGADA" : "Rádio: DESLIGADA";
            _state.ForeColor = _enabled.Checked ? Color.LightGreen : Color.Gold;
            _details.Text = string.IsNullOrWhiteSpace(updatedBy)
                ? "Nenhuma programação foi salva ainda."
                : "Última alteração por " + updatedBy + (string.IsNullOrWhiteSpace(updatedAt) ? "" : " em " + updatedAt);
        }
        catch (Exception ex)
        {
            _state.Text = "Rádio: Central indisponível";
            _state.ForeColor = Color.Orange;
            _details.Text = "Inicie a CENTRAL DO SITE e tente novamente.";
            if (showErrors)
                MessageBox.Show(this, "Não foi possível carregar a Rádio GAT.\r\n\r\n" + ex.Message, "Rádio GAT", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            _busy = false;
        }
    }

    private async Task SaveAsync(Button button, bool turnOff)
    {
        if (_busy) return;
        var user = (_user.Text ?? "").Trim();
        var password = _password.Text ?? "";
        if (user.Length == 0 || password.Length == 0)
        {
            MessageBox.Show(this, "Informe o usuário e a senha do site de uma conta Admin ou Moderador.", "Rádio GAT", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var playlist = (_playlist.Text ?? "").Trim();
        var enabled = turnOff ? false : _enabled.Checked;
        if (enabled && playlist.Length == 0)
        {
            MessageBox.Show(this, "Cole o link de uma playlist do YouTube antes de ligar a rádio.", "Rádio GAT", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        _busy = true;
        button.Enabled = false;
        try
        {
            var login = await PostJsonAsync("/api/account/login", new { user, password });
            var role = ((string)login["role"] ?? "").Trim().ToLowerInvariant();
            if (role != "owner" && role != "admin" && role != "moderator")
                throw new InvalidOperationException("Esta conta não tem permissão para controlar a Rádio GAT.");

            var token = (string)login["token"] ?? "";
            if (string.IsNullOrWhiteSpace(token))
                throw new InvalidOperationException("A Central não retornou uma sessão válida.");

            var result = await PostJsonAsync("/api/site/admin/radio", new
            {
                token,
                playlist_url = playlist,
                label = string.IsNullOrWhiteSpace(_label.Text) ? "Radio GAT" : _label.Text.Trim(),
                enabled
            });

            var radio = result["radio"] as JObject;
            _enabled.Checked = (bool?)radio?["enabled"] == true;
            _playlist.Text = (string)radio?["playlist_url"] ?? _playlist.Text;
            _label.Text = (string)radio?["label"] ?? _label.Text;
            _password.Clear();
            _state.Text = _enabled.Checked ? "Rádio: LIGADA" : "Rádio: DESLIGADA";
            _state.ForeColor = _enabled.Checked ? Color.LightGreen : Color.Gold;
            _details.Text = "Configuração salva. Os GAT Telemetria receberão a programação pela Central.";
            MessageBox.Show(this, _enabled.Checked ? "Rádio GAT salva e ligada." : "Rádio GAT salva e desligada.", "Rádio GAT", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Não foi possível salvar a Rádio GAT.\r\n\r\n" + ex.Message, "Rádio GAT", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            _busy = false;
            button.Enabled = true;
        }
    }
}
