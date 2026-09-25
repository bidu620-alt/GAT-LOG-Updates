using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GatTelemetry;

internal sealed partial class RadioForm
{
    private const string ChannelPositionEndpoint1689 = "https://api.gatlogets2.com.br/api/site/admin/radio/position";
    private string _serverLiveVideoId1689 = string.Empty;
    private double _serverLivePosition1689;
    private DateTime _serverLivePositionAt1689 = DateTime.MinValue;
    private DateTime _lastPositionPush1689 = DateTime.MinValue;
    private bool _hubMuted1689;
    private bool _hubDucked1689;

    internal string HubRole1689 => (_accountRole049 ?? string.Empty).Trim().ToLowerInvariant();
    internal bool HubCanGoLive1689 =>
        string.Equals(HubRole1689, "owner", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(HubRole1689, "admin", StringComparison.OrdinalIgnoreCase);
    internal bool HubChannelAvailable1689 => _serverEnabled && !string.IsNullOrWhiteSpace(_serverSourceId);
    internal bool HubListening1689 => _listening;
    internal int HubVolume1689 => _volume == null ? 55 : _volume.Value;

    internal string HubState1689
    {
        get
        {
            if (!_serverEnabled || string.IsNullOrWhiteSpace(_serverSourceId)) return "RÁDIO OFF";
            return _listening ? "AO VIVO" : "RECONECTANDO";
        }
    }

    internal async Task HubStartShared1689Async()
    {
        try
        {
            _webMode = false;
            _personalMode = false;
            SaveSharedMediaMode041("gat");
            ApplyModeUi();
            await InitializePlayerAsync();
            await RefreshRadioAsync(true);
            if (HubChannelAvailable1689)
            {
                _listening = true;
                _toggle.Text = "PARAR RÁDIO";
                UpdateActiveSourceUi();
                if (_playerReady) await LoadActiveSourceAsync();
            }
        }
        catch { }
    }

    internal async Task HubRefresh1689Async()
    {
        try { await RefreshRadioAsync(false); } catch { }
    }

    internal void HubSetVolume1689(int value)
    {
        try
        {
            value = Math.Max(0, Math.Min(100, value));
            if (_volume.Value != value) _volume.Value = value;
            SaveVolume(value);
            _ = ApplyEffectiveVolume1689Async();
        }
        catch { }
    }

    internal void HubToggleMute1689()
    {
        _hubMuted1689 = !_hubMuted1689;
        _ = ApplyEffectiveVolume1689Async();
    }

    internal bool HubMuted1689 => _hubMuted1689;

    internal void HubSetDucked1689(bool ducked)
    {
        if (_hubDucked1689 == ducked) return;
        _hubDucked1689 = ducked;
        _ = ApplyEffectiveVolume1689Async();
    }

    private async Task ApplyEffectiveVolume1689Async()
    {
        int volume = _hubMuted1689 ? 0 : (_hubDucked1689 ? Math.Min(18, HubVolume1689) : HubVolume1689);
        try { await ExecutePlayerAsync("gatVolume(" + volume + ")"); } catch { }
    }

    private void ReadLiveRadioState1689(JObject radio)
    {
        try
        {
            _serverLiveVideoId1689 = (Convert.ToString(radio["live_video_id"]) ?? string.Empty).Trim();
            _serverLivePosition1689 = Math.Max(0, radio.Value<double?>("live_position_seconds") ?? 0);
            DateTime when;
            _serverLivePositionAt1689 = DateTime.TryParse(
                Convert.ToString(radio["live_position_at"]),
                null,
                System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal,
                out when) ? when.ToUniversalTime() : DateTime.MinValue;
        }
        catch
        {
            _serverLiveVideoId1689 = string.Empty;
            _serverLivePosition1689 = 0;
            _serverLivePositionAt1689 = DateTime.MinValue;
        }
    }

    private string BuildSyncedLoadCommand1689(string sourceType)
    {
        if (_personalMode || _webMode) return string.Empty;
        if (sourceType != "playlist" && sourceType != "video") return string.Empty;
        if (string.IsNullOrWhiteSpace(_serverLiveVideoId1689)) return string.Empty;
        double seconds = _serverLivePosition1689;
        if (_serverLivePositionAt1689 != DateTime.MinValue)
        {
            double elapsed = (DateTime.UtcNow - _serverLivePositionAt1689).TotalSeconds;
            if (elapsed > 0 && elapsed < 60) seconds += elapsed;
        }
        string jsId = JsonConvert.SerializeObject(_serverLiveVideoId1689);
        return "gatLoadVideoAt(" + jsId + "," + Math.Max(0, seconds).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + ")";
    }

    private async Task SaveChannelPosition1689Async(JObject msg)
    {
        if (!CanEditChannel049() || string.IsNullOrWhiteSpace(_accountToken049)) return;
        if ((DateTime.UtcNow - _lastPositionPush1689).TotalSeconds < 3.0) return;
        string videoId = (Convert.ToString(msg["videoId"]) ?? string.Empty).Trim();
        double seconds = msg.Value<double?>("seconds") ?? -1;
        if (videoId.Length != 11 || seconds < 0) return;
        _lastPositionPush1689 = DateTime.UtcNow;
        try
        {
            JObject payload = new JObject
            {
                ["token"] = _accountToken049,
                ["video_id"] = videoId,
                ["position_seconds"] = seconds
            };
            using (var content = new StringContent(payload.ToString(Formatting.None), Encoding.UTF8, "application/json"))
            using (HttpResponseMessage response = await _http.PostAsync(ChannelPositionEndpoint1689, content))
            {
                if (!response.IsSuccessStatusCode) return;
            }
        }
        catch { }
    }
}
