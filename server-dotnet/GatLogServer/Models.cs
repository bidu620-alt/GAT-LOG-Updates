using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace GatLogServer
{
    public sealed class ServerConfig
    {
        [JsonProperty("server_name")] public string ServerName { get; set; } = "GAT AMIGOS";
        [JsonProperty("description")] public string Description { get; set; } = "";
        [JsonProperty("welcome_message")] public string WelcomeMessage { get; set; } = "";
        [JsonProperty("server_password")] public string ServerPassword { get; set; } = "";
        [JsonProperty("max_players")] public int MaxPlayers { get; set; } = 128;
        [JsonProperty("traffic")] public bool Traffic { get; set; }
        [JsonProperty("player_damage")] public bool PlayerDamage { get; set; }
        [JsonProperty("moderator_steam_id")] public string ModeratorSteamId { get; set; } = "";
        [JsonProperty("funnel_url")] public string FunnelUrl { get; set; } = "";
        [JsonProperty("server_exe")] public string ServerExe { get; set; } = "";
        [JsonProperty("documents_home")] public string DocumentsHome { get; set; } = "";
        [JsonProperty("registration_open")] public bool RegistrationOpen { get; set; } = true;
        [JsonProperty("updated_at")] public string UpdatedAt { get; set; } = "";
    }

    public sealed class TelemetryRecord
    {
        [JsonProperty("driver")] public string Driver { get; set; } = "";
        [JsonProperty("device_id")] public string DeviceId { get; set; } = "";
        [JsonProperty("updated_at")] public string UpdatedAt { get; set; } = "";
        [JsonProperty("status")] public string Status { get; set; } = "";
        [JsonProperty("cargo")] public string Cargo { get; set; } = "";
        [JsonProperty("cargo_mass_kg")] public double CargoMassKg { get; set; }
        [JsonProperty("source")] public string Source { get; set; } = "";
        [JsonProperty("destination")] public string Destination { get; set; } = "";
        [JsonProperty("remaining_km")] public double RemainingKm { get; set; }
        [JsonProperty("speed_kmh")] public double SpeedKmh { get; set; }
        [JsonProperty("on_job")] public bool OnJob { get; set; }
    }

    public sealed class ServerStatus
    {
        [JsonProperty("ok")] public bool Ok { get; set; }
        [JsonProperty("agent_version")] public string AgentVersion { get; set; } = "";
        [JsonProperty("agent_uptime_sec")] public long AgentUptimeSec { get; set; }
        [JsonProperty("server_online")] public bool ServerOnline { get; set; }
        [JsonProperty("server_name")] public string ServerName { get; set; } = "";
        [JsonProperty("session_id")] public string SessionId { get; set; } = "";
        [JsonProperty("ports")] public string Ports { get; set; } = "";
        [JsonProperty("players")] public List<string> Players { get; set; } = new List<string>();
        [JsonProperty("player_count")] public int PlayerCount { get; set; }
        [JsonProperty("max_players")] public int MaxPlayers { get; set; } = 128;
        [JsonProperty("server_exe")] public string ServerExe { get; set; } = "";
        [JsonProperty("server_log")] public string ServerLog { get; set; } = "";
        [JsonProperty("packages_ok")] public bool PackagesOk { get; set; }
        [JsonProperty("packages_text")] public string PackagesText { get; set; } = "";
        [JsonProperty("funnel_url")] public string FunnelUrl { get; set; } = "";
        [JsonProperty("data_dir")] public string DataDir { get; set; } = "";
        [JsonProperty("telemetry")] public List<TelemetryRecord> Telemetry { get; set; } = new List<TelemetryRecord>();
        [JsonProperty("registration_open")] public bool RegistrationOpen { get; set; }
        [JsonProperty("last_refresh")] public string LastRefresh { get; set; } = "";
    }

    public sealed class BindingInfo
    {
        [JsonProperty("driver")] public string Driver { get; set; } = "";
        [JsonProperty("device_id")] public string DeviceId { get; set; } = "";
        [JsonProperty("blocked")] public bool Blocked { get; set; }
        [JsonProperty("disconnected")] public bool Disconnected { get; set; }
        [JsonProperty("last_seen")] public string LastSeen { get; set; } = "";
    }

    public sealed class NativeAuth
    {
        [JsonProperty("user")] public string User { get; set; } = "gatlog";
        [JsonProperty("salt")] public string Salt { get; set; } = "";
        [JsonProperty("hash")] public string Hash { get; set; } = "";
        [JsonProperty("updated_at")] public string UpdatedAt { get; set; } = "";
    }

    public sealed class RemoteVersion
    {
        [JsonProperty("version")] public string Version { get; set; } = "";
        [JsonProperty("setup_url")] public string SetupUrl { get; set; } = "";
        [JsonProperty("sha256")] public string Sha256 { get; set; } = "";
        [JsonProperty("notas")] public string Notes { get; set; } = "";
    }
}
