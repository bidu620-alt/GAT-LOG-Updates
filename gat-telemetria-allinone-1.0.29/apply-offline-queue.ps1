param([Parameter(Mandatory=$true)][string]$Root)
$main = Get-ChildItem $Root -Filter 'MainForm.cs' -Recurse | Select-Object -First 1
if (-not $main) { throw 'MainForm.cs nao encontrado para aplicar fila offline' }
$s = Get-Content $main.FullName -Raw

function Replace-One([string]$old,[string]$new) {
  $count = ([regex]::Matches($script:s,[regex]::Escape($old))).Count
  if ($count -ne 1) { throw "Esperava 1 ocorrencia e encontrei ${count}: $($old.Substring(0,[Math]::Min(80,$old.Length)))" }
  $script:s = $script:s.Replace($old,$new)
}

Replace-One 'private DateTime _lastTripFlush = DateTime.MinValue;' @'
private DateTime _lastTripFlush = DateTime.MinValue;

	private const int MaxQueuedTelemetryPackets = 7200;

	private string CentralTelemetryQueueFile => Path.Combine(ClientStore.DataDir, "central-telemetry-queue.ndjson");
'@

$helpers = @'
	private void StampCentralTelemetry(JObject tele)
	{
		if (tele == null) return;
		if (tele["gat_collected_at"] == null) tele["gat_collected_at"] = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
		if (tele["gat_packet_id"] == null) tele["gat_packet_id"] = Guid.NewGuid().ToString("N");
		string tripId = TextAny(tele, "gat_job_event_key", "job_latch_key", "gat_trip_id");
		if (string.IsNullOrWhiteSpace(tripId) && _latchedJob != null) tripId = _latchedJobKey;
		if (!string.IsNullOrWhiteSpace(tripId)) tele["gat_trip_id"] = tripId;
	}

	private void QueueCentralTelemetry(JObject tele)
	{
		if (tele == null) return;
		try
		{
			ClientStore.Ensure();
			StampCentralTelemetry(tele);
			File.AppendAllText(CentralTelemetryQueueFile, tele.ToString(Formatting.None) + Environment.NewLine, Encoding.UTF8);
			string[] lines = File.ReadAllLines(CentralTelemetryQueueFile, Encoding.UTF8).Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
			if (lines.Length > MaxQueuedTelemetryPackets)
			{
				File.WriteAllLines(CentralTelemetryQueueFile, lines.Skip(lines.Length - MaxQueuedTelemetryPackets), Encoding.UTF8);
			}
			ClientStore.Log("telemetria salva localmente para reenvio: " + TextAny(tele, "gat_packet_id"));
		}
		catch (Exception ex)
		{
			ClientStore.Log("fila local de telemetria: " + ex.Message);
		}
	}

	private List<JObject> LoadCentralTelemetryQueue()
	{
		List<JObject> result = new List<JObject>();
		try
		{
			if (!File.Exists(CentralTelemetryQueueFile)) return result;
			foreach (string line in File.ReadAllLines(CentralTelemetryQueueFile, Encoding.UTF8))
			{
				if (string.IsNullOrWhiteSpace(line)) continue;
				try { result.Add(JObject.Parse(line)); } catch { }
			}
		}
		catch (Exception ex) { ClientStore.Log("leitura fila local: " + ex.Message); }
		return result;
	}

	private void SaveCentralTelemetryQueue(IEnumerable<JObject> packets)
	{
		try
		{
			JObject[] rows = (packets ?? Enumerable.Empty<JObject>()).ToArray();
			if (rows.Length == 0)
			{
				if (File.Exists(CentralTelemetryQueueFile)) File.Delete(CentralTelemetryQueueFile);
				return;
			}
			string temp = CentralTelemetryQueueFile + ".tmp";
			File.WriteAllLines(temp, rows.Select(x => x.ToString(Formatting.None)), Encoding.UTF8);
			if (File.Exists(CentralTelemetryQueueFile)) File.Delete(CentralTelemetryQueueFile);
			File.Move(temp, CentralTelemetryQueueFile);
		}
		catch (Exception ex) { ClientStore.Log("gravacao fila local: " + ex.Message); }
	}

	private async Task<int> FlushCentralTelemetryQueueAsync(string driver, string clientToken)
	{
		List<JObject> packets = LoadCentralTelemetryQueue();
		if (packets.Count == 0) return 0;
		lblTelemetry.Text = "Central GAT: enviando viagem pendente...";
		int sent = 0;
		int limit = Math.Min(120, packets.Count);
		for (int i = 0; i < limit; i++)
		{
			JObject packet = packets[i];
			ApiResponse response = await _api.SendTelemetryAsync("https://api.gatlogets2.com.br", driver, _deviceId, clientToken, packet);
			if (response.StatusCode != 200 || response.Json == null || !ApiClient.Bool(response.Json["ok"])) break;
			sent++;
		}
		if (sent > 0)
		{
			packets.RemoveRange(0, sent);
			SaveCentralTelemetryQueue(packets);
			ClientStore.Log("telemetria pendente reenviada: " + sent + " pacote(s)");
		}
		return packets.Count;
	}

'@
Replace-One 'private async Task SendCentralTelemetryAsync()' ($helpers + "`tprivate async Task SendCentralTelemetryAsync()")

Replace-One 'tele["gat_map_label"] = CurrentMapModeLabel;' @'
tele["gat_map_label"] = CurrentMapModeLabel;
		StampCentralTelemetry(tele);
'@

$anchor = 'ApiResponse apiResponse2 = await _api.SendTelemetryAsync("https://api.gatlogets2.com.br", centralDriver, _deviceId, centralClientToken, tele);'
$replacement = @'
int pendingBeforeCurrent = await FlushCentralTelemetryQueueAsync(centralDriver, centralClientToken);
		if (pendingBeforeCurrent > 0)
		{
			QueueCentralTelemetry(tele);
			lblTelemetry.Text = "Central GAT: viagem salva • aguardando servidor";
			return;
		}
		ApiResponse apiResponse2 = await _api.SendTelemetryAsync("https://api.gatlogets2.com.br", centralDriver, _deviceId, centralClientToken, tele);
'@
Replace-One $anchor $replacement

Replace-One @'
else if (apiResponse2.StatusCode == 0)
		{
			lblTelemetry.Text = "Central GAT: reconectando...";
		}
		else if (apiResponse2.StatusCode == 404)
		{
			lblTelemetry.Text = "Central GAT: atualize o servidor central";
		}
		else
		{
			lblTelemetry.Text = "Central GAT: falha HTTP " + apiResponse2.StatusCode;
		}
'@ @'
else if (apiResponse2.StatusCode == 0 || apiResponse2.StatusCode == 404 || apiResponse2.StatusCode == 429 || apiResponse2.StatusCode >= 500)
		{
			QueueCentralTelemetry(tele);
			lblTelemetry.Text = "Central GAT: viagem salva • aguardando servidor";
		}
		else
		{
			lblTelemetry.Text = "Central GAT: falha HTTP " + apiResponse2.StatusCode;
		}
'@

Set-Content $main.FullName $s -Encoding UTF8
Write-Host 'Fila offline aplicada: coleta continua local, reenvio ordenado e identificador unico por pacote.'
