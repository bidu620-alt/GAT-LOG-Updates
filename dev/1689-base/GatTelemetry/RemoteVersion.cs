using Newtonsoft.Json;

namespace GatTelemetry;

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

	public string EffectiveUrl
	{
		get
		{
			if (string.IsNullOrWhiteSpace(SetupUrl))
			{
				return DownloadUrl;
			}
			return SetupUrl;
		}
	}
}
