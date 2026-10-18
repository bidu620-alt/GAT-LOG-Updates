using System;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json.Linq;

namespace GatTelemetry;

internal sealed partial class MainForm
{
    private readonly ToolTip _goldTip = new ToolTip();
    private Label _goldBar, _garageState, _garageBalance, _garageDetails;
    private ListBox _garageList;
    private Button _garageSelect;
    private JObject _economyState;
    private string _economyUser = "", _economyError = "Central ainda não atualizada para a garagem.";
    private TruckModelEvidence _truckEvidence = TruckModelEvidence.Read(null);
    private DateTime _truckEvidenceAt = DateTime.MinValue, _economyAt = DateTime.MinValue, _economyPollAt = DateTime.MinValue;
    private bool _economyBusy;
    private sealed class GarageChoice {
        public string Key, Label;
        public override string ToString() => Label;
    }
    private void InitializeEconomy()
    {
        AddHubPage041("garage", BuildGarage());
        var nav = FindControl051<FlowLayoutPanel>(this, "hubNav051");
        if (nav != null) Nav041(nav, "garage", "GARAGEM / GOLD", 135);
        var shell = FindControl051<TableLayoutPanel>(this, "hubShell051");
        if (shell != null) {
            var oldFooter = shell.GetControlFromPosition(0, 3);
            if (oldFooter != null) { shell.Controls.Remove(oldFooter); oldFooter.Dispose(); }
            _goldBar = new Label { Dock = DockStyle.Fill, Padding = new Padding(14, 3, 10, 3),
                AutoEllipsis = true, Font = new Font("Segoe UI Semibold", 9f), BackColor = Color.FromArgb(42, 16, 18),
                ForeColor = Color.FromArgb(255, 100, 100), TextAlign = ContentAlignment.MiddleLeft, Cursor = Cursors.Hand };
            _goldBar.Click += delegate { ShowHubPage041("garage"); };
            shell.Controls.Add(_goldBar, 0, 3);
            shell.RowStyles[3].Height = 28;
            Resize += delegate { shell.RowStyles[3].Height = 28; };
        }
        _hubStatusTimer041.Tick += delegate { RenderEconomy(); };
        RenderEconomy();
    }
    private Panel BuildGarage()
    {
        var page = new Panel { BackColor = Color.FromArgb(5, 17, 12), Padding = new Padding(12) };
        var rows = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 7, Padding = new Padding(8) };
        foreach (int h in new[] { 42, 72, 42, 76, 0, 44, 62 }) rows.RowStyles.Add(new RowStyle(h == 0 ? SizeType.Percent : SizeType.Absolute, h == 0 ? 100 : h));
        rows.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        rows.Controls.Add(new Label { Text = "MINHA GARAGEM • BIDUTRUCK", Dock = DockStyle.Fill, ForeColor = Color.White,
            Font = new Font("Segoe UI Semibold", 15f), TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
        _garageState = new Label { Dock = DockStyle.Fill, Padding = new Padding(10), Font = new Font("Segoe UI Semibold", 11f) };
        rows.Controls.Add(_garageState, 0, 1);
        _garageBalance = new Label { Dock = DockStyle.Fill, ForeColor = Color.Gold, Font = new Font("Segoe UI Semibold", 12f), TextAlign = ContentAlignment.MiddleLeft };
        rows.Controls.Add(_garageBalance, 0, 2);
        _garageDetails = new Label { Dock = DockStyle.Fill, ForeColor = Color.Silver, Font = new Font("Segoe UI", 9f), AutoEllipsis = true };
        rows.Controls.Add(_garageDetails, 0, 3);
        _garageList = new ListBox { Dock = DockStyle.Fill, BackColor = Color.FromArgb(17, 30, 24), ForeColor = Color.White,
            Font = new Font("Segoe UI", 11f), IntegralHeight = false };
        rows.Controls.Add(_garageList, 0, 4);
        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, WrapContents = false };
        _garageSelect = HubButton041("SELECIONAR MARCA", 200);
        _garageSelect.Click += async delegate { await SelectGarageTruck(); };
        var refresh = HubButton041("ATUALIZAR", 110);
        refresh.Click += async delegate { _economyPollAt = DateTime.MinValue; await PollEconomy(); };
        actions.Controls.Add(_garageSelect); actions.Controls.Add(refresh);
        var diagnosis = HubButton041("SALVAR DIAGNÓSTICO", 180);
        diagnosis.Click += delegate {
            using (var dialog = new SaveFileDialog { Filter = "Diagnóstico JSON|*.json", FileName = "bidutruck-caminhao-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".json" }) {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                try {
                    var data = new JObject { ["client_version"] = CurrentVersion, ["observed_at"] = _truckEvidenceAt.ToString("o"),
                        ["connected"] = _truckEvidence.Connected, ["paused"] = _truckEvidence.Paused,
                        ["make"] = _truckEvidence.Make, ["model"] = _truckEvidence.Model, ["raw_id"] = _truckEvidence.RawId,
                        ["signature"] = _truckEvidence.Signature, ["message"] = _garageState.Text,
                        ["central_validation"] = _economyState?["validation"]?.DeepClone() };
                    System.IO.File.WriteAllText(dialog.FileName, data.ToString());
                } catch (Exception ex) { MessageBox.Show("Não foi possível salvar: " + ex.Message, "Diagnóstico BiduTruck"); }
            }
        };
        actions.Controls.Add(diagnosis);
        rows.Controls.Add(actions, 0, 5);
        rows.Controls.Add(new Label { Dock = DockStyle.Fill, ForeColor = Color.Silver, Font = new Font("Segoe UI", 9f),
            Text = "Uma marca libera todos os seus modelos. A Central confere a marca real no ETS2.\r\n1 Ponto GAT final = 1 Gold em entregas válidas. XP, pontos e eventos seguem suas regras." }, 0, 6);
        page.Controls.Add(rows);
        return page;
    }
    private void ObserveEconomyTruck(JObject raw)
    {
        _truckEvidence = TruckModelEvidence.Read(raw);
        _truckEvidenceAt = DateTime.UtcNow;
        RenderEconomy();
    }
    private async Task PollEconomy()
    {
        if (_economyBusy || IsDisposed) return;
        if (!AccountReady || _economyUser != _accountUser) {
            _economyState = null; _economyAt = DateTime.MinValue; _economyUser = _accountUser;
        }
        if (!AccountReady) { _economyError = "Entre na sua conta BiduTruck."; RenderEconomy(); return; }
        if ((DateTime.UtcNow - _economyPollAt).TotalSeconds < 8) return;
        _economyPollAt = DateTime.UtcNow;
        _economyBusy = true;
        string user = _accountUser, token = _accountToken;
        try {
            var response = await _api.PostBearerAsync(AccountAuthority + "/api/economy/state", token, new { }, 5);
            if (IsDisposed || user != _accountUser || token != _accountToken) return;
            if (response.StatusCode == 200 && response.Json?["ok"]?.Value<bool>() == true) {
                _economyState = response.Json; _economyAt = DateTime.UtcNow; _economyError = "";
                var old = (_garageList.SelectedItem as GarageChoice)?.Key;
                _garageList.Items.Clear();
                foreach (JObject truck in (response.Json["garage"] as JArray ?? new JArray()).OfType<JObject>()) {
                    var c = new GarageChoice { Key = (string)truck["model_key"], Label = (string)truck["label"] + ((bool?)truck["selected"] == true ? " • selecionado" : "") };
                    _garageList.Items.Add(c);
                    if (c.Key == old || (old == null && (bool?)truck["selected"] == true)) _garageList.SelectedItem = c;
                }
            } else {
                _economyState = null; _economyAt = DateTime.MinValue; _garageList.Items.Clear();
                _economyError = response.StatusCode == 404 ? "Central ainda não atualizada para a garagem." :
                    response.StatusCode == 401 ? "Sessão expirada. Entre novamente." : "Sem confirmação da Central. Tente novamente.";
            }
        } catch (Exception ex) {
            _economyState = null; _economyAt = DateTime.MinValue;
            _economyError = "Confirmação da Central indisponível.";
            ClientStore.Log("Garagem: " + ex.GetType().Name);
        } finally { _economyBusy = false; if (!IsDisposed) RenderEconomy(); }
    }
    private async Task SelectGarageTruck()
    {
        var choice = _garageList.SelectedItem as GarageChoice;
        if (choice == null || _economyBusy || !AccountReady) return;
        string user = _accountUser, token = _accountToken;
        _economyBusy = true; _garageSelect.Enabled = false;
        try {
            var r = await _api.PostBearerAsync(AccountAuthority + "/api/economy/select-truck", token, new { model_key = choice.Key, op_id = Guid.NewGuid().ToString("N") }, 5);
            if (IsDisposed || user != _accountUser || token != _accountToken) return;
            if (r.StatusCode != 200 || (bool?)r.Json?["ok"] != true) {
                MessageBox.Show("A Central não confirmou a seleção. Ela pode estar bloqueada durante uma entrega.", "Garagem BiduTruck");
            }
            _economyState = null; _economyPollAt = DateTime.MinValue;
        } finally { _economyBusy = false; }
        await PollEconomy();
    }
    private void UpdateGoldDashboard(string status, bool eligible)
    {
        var window = _roadLifeOverlay;
        if(window == null)return;
        Action update = () => {
            if(window.FindName("GoldText") is System.Windows.Controls.TextBlock text){
                text.Text = eligible ? ((string)_economyState?["mode"] == "official" ? "MARCA HABILITADA" : "MARCA HABILITADA • TESTE") : "MARCA NÃO HABILITADA";
                text.ToolTip = status;
                text.Foreground = eligible ? System.Windows.Media.Brushes.LightGreen : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 100, 100));
            }
            if(window.FindName("GoldBadge") is System.Windows.Controls.Border badge)
                badge.Background = new System.Windows.Media.SolidColorBrush(eligible ? System.Windows.Media.Color.FromArgb(230,9,65,32) : System.Windows.Media.Color.FromArgb(230,70,16,20));
        };
        if(window.Dispatcher.CheckAccess())update();else window.Dispatcher.BeginInvoke(update);
    }
    private void RenderEconomy()
    {
        if (_garageState == null || IsDisposed) return;
        bool fresh = (DateTime.UtcNow - _truckEvidenceAt).TotalSeconds < 4 && _truckEvidence.Identified;
        bool serverFresh = AccountReady && _economyUser == _accountUser && _economyState != null && (DateTime.UtcNow - _economyAt).TotalSeconds < 15;
        string status = "Caminhão não identificado — aguardando o ETS2.";
        bool eligible = false;
        if (fresh) {
            if (!serverFresh) status = "Aguardando confirmação da garagem — " + _economyError;
            else {
                string brand = TruckModelEvidence.BrandKey(_truckEvidence.Make);
                eligible = (_economyState["garage"] as JArray ?? new JArray()).OfType<JObject>()
                    .Any(truck => string.Equals((string)truck["brand_key"] ?? (string)truck["model_key"], brand, StringComparison.Ordinal));
                status = eligible
                    ? "Marca habilitada na garagem. A Central valida a viagem e calcula o Gold na entrega."
                    : "Esta marca não está habilitada na sua garagem.";
            }
        }
        UpdateGoldDashboard(status, eligible);
        var color = eligible ? Color.LightGreen : Color.FromArgb(255, 100, 100);
        var bg = eligible ? Color.FromArgb(10, 47, 26) : Color.FromArgb(55, 19, 22);
        _garageState.Text = status; _garageState.ForeColor = color; _garageState.BackColor = bg;
        string balance = serverFresh ? ((decimal?)_economyState["gold_balance"] ?? 0m).ToString("N2", CultureInfo.GetCultureInfo("pt-BR")) : "—";
        _garageBalance.Text = "SALDO: " + balance + " Gold" + (serverFresh ? ((string)_economyState["mode"] == "official" ? " • carteira confirmada" : " • teste sem créditos") : " • aguardando Central");
        _garageDetails.Text = "ETS2: " + (_truckEvidence.Make + " " + _truckEvidence.Model).Trim() + "\r\nID bruto: " + _truckEvidence.RawId + " • origem: amostra atual TruckSimGPS\r\n" +
            (serverFresh ? ((string)_economyState["last_reward_message"] ?? "Nenhuma simulação de entrega registrada.") : "Sem saldo ou propriedade presumidos localmente.");
        _garageSelect.Enabled = serverFresh && !_economyBusy && (bool?)_economyState["selection_locked"] != true && _garageList.Items.Count > 0;
        if (_goldBar != null) { _goldBar.Text = (serverFresh && (string)_economyState["mode"] == "official" ? "Gold: " : "Gold (teste): ") + balance + "  |  " + status; _goldTip?.SetToolTip(_goldBar, status + "\r\nClique para abrir a garagem e consultar a carteira."); _goldBar.ForeColor = color; _goldBar.BackColor = bg; }
    }
}
