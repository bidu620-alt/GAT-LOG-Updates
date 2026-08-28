using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GatTelemetry
{
    internal sealed class ServerEntry
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("endpoint")]
        public string Endpoint { get; set; }

        public override string ToString() => string.IsNullOrWhiteSpace(Name) ? Endpoint : Name;
    }

    internal sealed class CredentialEntry
    {
        [JsonProperty("endpoint")]
        public string Endpoint { get; set; }

        [JsonProperty("driver")]
        public string Driver { get; set; }

        [JsonProperty("token")]
        public string Token { get; set; }

        [JsonProperty("saved_at")]
        public string SavedAt { get; set; }
    }

    internal sealed class ClientSettings
    {
        [JsonProperty("auto_connect")]
        public bool AutoConnect { get; set; }

        [JsonProperty("last_server")]
        public string LastServer { get; set; }

        [JsonProperty("last_driver")]
        public string LastDriver { get; set; }

        [JsonProperty("updated_at")]
        public string UpdatedAt { get; set; }
    }

    internal sealed class ApiResponse
    {
        public int StatusCode { get; set; }
        public JObject Json { get; set; }
        public string Text { get; set; }
        public Exception Error { get; set; }
        public bool Ok => StatusCode >= 200 && StatusCode < 300 && Error == null;
    }

    internal sealed class ServerInfo
    {
        public bool Reachable { get; set; }
        public bool Supported { get; set; }
        public bool Online { get; set; }
        public string ServerName { get; set; }
        public string SessionId { get; set; }
        public int Players { get; set; }
        public int MaxPlayers { get; set; }
    }

    internal sealed class RemoteVersion
    {
        [JsonProperty("app")]
        public string App { get; set; }

        [JsonProperty("version")]
        public string Version { get; set; }

        [JsonProperty("display_version")]
        public string DisplayVersion { get; set; }

        [JsonProperty("notas")]
        public string Notes { get; set; }

        [JsonProperty("setup_url")]
        public string SetupUrl { get; set; }

        [JsonProperty("download_url")]
        public string DownloadUrl { get; set; }

        [JsonProperty("sha256")]
        public string Sha256 { get; set; }

        public string EffectiveUrl => !string.IsNullOrWhiteSpace(SetupUrl) ? SetupUrl : DownloadUrl;
    }

    internal sealed class TelemetryDisplay
    {
        public string Cargo { get; set; } = "Sem carga";
        public string Route { get; set; } = "-";
        public string Distance { get; set; } = "-";
        public string Speed { get; set; } = "0 km/h";
        public string Weight { get; set; } = "-";
    }

    internal sealed class PlayersResult
    {
        public bool Ok { get; set; }
        public List<string> Players { get; set; } = new List<string>();
    }
}
