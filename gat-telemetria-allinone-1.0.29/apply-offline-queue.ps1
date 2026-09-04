param([Parameter(Mandatory=$true)][string]$Root)
$main = Get-ChildItem $Root -Filter 'MainForm.cs' -Recurse | Select-Object -First 1
if (-not $main) { throw 'MainForm.cs nao encontrado para aplicar fila offline segura' }
$s = Get-Content $main.FullName -Raw

function Replace-One([string]$old,[string]$new) {
  $count = ([regex]::Matches($script:s,[regex]::Escape($old))).Count
  if ($count -ne 1) { throw "Esperava 1 ocorrencia e encontrei ${count}: $($old.Substring(0,[Math]::Min(80,$old.Length)))" }
  $script:s = $script:s.Replace($old,$new)
}

Replace-One 'private DateTime _lastTripFlush = DateTime.MinValue;' @'
private DateTime _lastTripFlush = DateTime.MinValue;

	private const int MaxQueuedTelemetryPackets = 72000;
	private const int MaxBlackBoxPackets = 100000;

	private string LegacyCentralTelemetryQueueFile => Path.Combine(ClientStore.DataDir, "central-telemetry-queue.ndjson");
	private string CentralTelemetryQueueFile => Path.Combine(ClientStore.DataDir, "central-telemetry-queue.sec");
	private string CentralTripBlackBoxFile => Path.Combine(ClientStore.DataDir, "central-trip-blackbox.sec");
	private string CentralTelemetryKeyFile => Path.Combine(ClientStore.DataDir, "central-telemetry-key.dpapi");
	private string CentralJournalStateFile => Path.Combine(ClientStore.DataDir, "central-telemetry-chain.json");
'@

$helpers = @'
	private static byte[] JoinBytes(params byte[][] parts)
	{
		int total = parts.Where(x => x != null).Sum(x => x.Length);
		byte[] result = new byte[total];
		int offset = 0;
		foreach (byte[] part in parts)
		{
			if (part == null) continue;
			Buffer.BlockCopy(part, 0, result, offset, part.Length);
			offset += part.Length;
		}
		return result;
	}

	private static bool FixedBytesEqual(byte[] a, byte[] b)
	{
		if (a == null || b == null || a.Length != b.Length) return false;
		int diff = 0;
		for (int i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
		return diff == 0;
	}

	private static string Sha256Hex(string value)
	{
		using (SHA256 sha = SHA256.Create())
		{
			return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value ?? string.Empty))).Replace("-", string.Empty).ToLowerInvariant();
		}
	}

	private byte[] LoadOrCreateJournalMasterKey()
	{
		ClientStore.Ensure();
		byte[] entropy = Encoding.UTF8.GetBytes("GAT-TELEMETRIA-LOCAL-JOURNAL-V1");
		if (File.Exists(CentralTelemetryKeyFile))
		{
			byte[] protectedBytes = File.ReadAllBytes(CentralTelemetryKeyFile);
			return ProtectedData.Unprotect(protectedBytes, entropy, DataProtectionScope.CurrentUser);
		}
		byte[] key = new byte[32];
		using (RandomNumberGenerator rng = RandomNumberGenerator.Create()) rng.GetBytes(key);
		byte[] saved = ProtectedData.Protect(key, entropy, DataProtectionScope.CurrentUser);
		File.WriteAllBytes(CentralTelemetryKeyFile, saved);
		return key;
	}

	private static byte[] DeriveJournalKey(byte[] master, string purpose)
	{
		using (HMACSHA256 h = new HMACSHA256(master)) return h.ComputeHash(Encoding.UTF8.GetBytes("GAT-JOURNAL-" + purpose));
	}

	private string EncryptJournalPacket(JObject packet)
	{
		byte[] master = LoadOrCreateJournalMasterKey();
		byte[] encKey = DeriveJournalKey(master, "ENC");
		byte[] macKey = DeriveJournalKey(master, "MAC");
		byte[] plain = Encoding.UTF8.GetBytes(packet.ToString(Formatting.None));
		byte[] iv;
		byte[] cipher;
		using (Aes aes = Aes.Create())
		{
			aes.Key = encKey;
			aes.Mode = CipherMode.CBC;
			aes.Padding = PaddingMode.PKCS7;
			aes.GenerateIV();
			iv = aes.IV;
			using (ICryptoTransform transform = aes.CreateEncryptor()) cipher = transform.TransformFinalBlock(plain, 0, plain.Length);
		}
		byte[] version = new byte[] { 1 };
		byte[] macData = JoinBytes(version, iv, cipher);
		byte[] mac;
		using (HMACSHA256 h = new HMACSHA256(macKey)) mac = h.ComputeHash(macData);
		return Convert.ToBase64String(JoinBytes(version, iv, mac, cipher));
	}

	private JObject DecryptJournalPacket(string line)
	{
		byte[] blob = Convert.FromBase64String(line.Trim());
		if (blob.Length < 1 + 16 + 32 + 1 || blob[0] != 1) throw new InvalidDataException("registro local invalido");
		byte[] iv = new byte[16], mac = new byte[32], cipher = new byte[blob.Length - 49];
		Buffer.BlockCopy(blob, 1, iv, 0, iv.Length);
		Buffer.BlockCopy(blob, 17, mac, 0, mac.Length);
		Buffer.BlockCopy(blob, 49, cipher, 0, cipher.Length);
		byte[] master = LoadOrCreateJournalMasterKey();
		byte[] encKey = DeriveJournalKey(master, "ENC");
		byte[] macKey = DeriveJournalKey(master, "MAC");
		byte[] expected;
		using (HMACSHA256 h = new HMACSHA256(macKey)) expected = h.ComputeHash(JoinBytes(new byte[] { 1 }, iv, cipher));
		if (!FixedBytesEqual(mac, expected)) throw new InvalidDataException("integridade da caixa-preta local falhou");
		byte[] plain;
		using (Aes aes = Aes.Create())
		{
			aes.Key = encKey;
			aes.IV = iv;
			aes.Mode = CipherMode.CBC;
			aes.Padding = PaddingMode.PKCS7;
			using (ICryptoTransform transform = aes.CreateDecryptor()) plain = transform.TransformFinalBlock(cipher, 0, cipher.Length);
		}
		return JObject.Parse(Encoding.UTF8.GetString(plain));
	}

	private void StampCentralTelemetry(JObject tele)
	{
		if (tele == null) return;
		if (tele["gat_collected_at"] == null) tele["gat_collected_at"] = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
		if (tele["gat_packet_id"] == null) tele["gat_packet_id"] = Guid.NewGuid().ToString("N");
		string tripId = TextAny(tele, "gat_job_event_key", "job_latch_key", "gat_trip_id");
		if (string.IsNullOrWhiteSpace(tripId) && _latchedJob != null) tripId = _latchedJobKey;
		if (!string.IsNullOrWhiteSpace(tripId)) tele["gat_trip_id"] = tripId;
	}

	private void SealCentralTelemetry(JObject tele, string clientToken)
	{
		if (tele == null || string.IsNullOrWhiteSpace(clientToken)) return;
		StampCentralTelemetry(tele);
		if (tele["gat_journal_chain"] != null) return;
		long seq = 0;
		string previous = string.Empty;
		try
		{
			if (File.Exists(CentralJournalStateFile))
			{
				JObject state = JObject.Parse(File.ReadAllText(CentralJournalStateFile, Encoding.UTF8));
				seq = Math.Max(0L, Convert.ToInt64(state["seq"] ?? 0L, CultureInfo.InvariantCulture));
				previous = Convert.ToString(state["chain"], CultureInfo.InvariantCulture) ?? string.Empty;
			}
		}
		catch { seq = 0; previous = string.Empty; }
		seq++;
		JObject unsigned = (JObject)tele.DeepClone();
		foreach (string key in new[] { "gat_journal_seq", "gat_journal_prev", "gat_journal_chain", "gat_journal_payload_sha256", "gat_journal_version", "gat_journal_verified", "gat_journal_invalid" }) unsigned.Remove(key);
		string payloadHash = Sha256Hex(unsigned.ToString(Formatting.None));
		string packetId = TextAny(tele, "gat_packet_id");
		string collectedAt = TextAny(tele, "gat_collected_at");
		string tripId = TextAny(tele, "gat_trip_id");
		string canonical = packetId + "|" + collectedAt + "|" + tripId + "|" + seq.ToString(CultureInfo.InvariantCulture) + "|" + previous + "|" + payloadHash;
		byte[] signingKey;
		using (SHA256 sha = SHA256.Create()) signingKey = sha.ComputeHash(Encoding.UTF8.GetBytes("GAT-JOURNAL-V1|" + clientToken + "|" + _deviceId));
		string chain;
		using (HMACSHA256 h = new HMACSHA256(signingKey)) chain = BitConverter.ToString(h.ComputeHash(Encoding.UTF8.GetBytes(canonical))).Replace("-", string.Empty).ToLowerInvariant();
		tele["gat_journal_version"] = "1";
		tele["gat_journal_seq"] = seq;
		tele["gat_journal_prev"] = previous;
		tele["gat_journal_payload_sha256"] = payloadHash;
		tele["gat_journal_chain"] = chain;
		File.WriteAllText(CentralJournalStateFile, new JObject { ["seq"] = seq, ["chain"] = chain, ["packet_id"] = packetId, ["updated_at"] = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture) }.ToString(Formatting.None), Encoding.UTF8);
	}

	private void AppendCentralBlackBox(JObject tele)
	{
		if (tele == null) return;
		try
		{
			ClientStore.Ensure();
			File.AppendAllText(CentralTripBlackBoxFile, EncryptJournalPacket(tele) + Environment.NewLine, Encoding.ASCII);
			string[] lines = File.ReadAllLines(CentralTripBlackBoxFile, Encoding.ASCII).Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
			if (lines.Length > MaxBlackBoxPackets) File.WriteAllLines(CentralTripBlackBoxFile, lines.Skip(lines.Length - MaxBlackBoxPackets), Encoding.ASCII);
		}
		catch (Exception ex) { ClientStore.Log("caixa-preta local: " + ex.Message); }
	}

	private void QueueCentralTelemetry(JObject tele)
	{
		if (tele == null) return;
		try
		{
			ClientStore.Ensure();
			StampCentralTelemetry(tele);
			File.AppendAllText(CentralTelemetryQueueFile, EncryptJournalPacket(tele) + Environment.NewLine, Encoding.ASCII);
			string[] lines = File.ReadAllLines(CentralTelemetryQueueFile, Encoding.ASCII).Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
			if (lines.Length > MaxQueuedTelemetryPackets) File.WriteAllLines(CentralTelemetryQueueFile, lines.Skip(lines.Length - MaxQueuedTelemetryPackets), Encoding.ASCII);
			ClientStore.Log("telemetria criptografada salva para reenvio: " + TextAny(tele, "gat_packet_id"));
		}
		catch (Exception ex) { ClientStore.Log("fila local segura: " + ex.Message); }
	}

	private List<JObject> LoadCentralTelemetryQueue()
	{
		List<JObject> result = new List<JObject>();
		if (!File.Exists(CentralTelemetryQueueFile)) return result;
		foreach (string line in File.ReadAllLines(CentralTelemetryQueueFile, Encoding.ASCII))
		{
			if (string.IsNullOrWhiteSpace(line)) continue;
			result.Add(DecryptJournalPacket(line));
		}
		return result;
	}

	private void SaveCentralTelemetryQueue(IEnumerable<JObject> packets)
	{
		JObject[] rows = (packets ?? Enumerable.Empty<JObject>()).ToArray();
		if (rows.Length == 0)
		{
			if (File.Exists(CentralTelemetryQueueFile)) File.Delete(CentralTelemetryQueueFile);
			return;
		}
		string temp = CentralTelemetryQueueFile + ".tmp";
		File.WriteAllLines(temp, rows.Select(EncryptJournalPacket), Encoding.ASCII);
		if (File.Exists(CentralTelemetryQueueFile)) File.Delete(CentralTelemetryQueueFile);
		File.Move(temp, CentralTelemetryQueueFile);
	}

	private void MigrateLegacyCentralTelemetryQueue(string clientToken)
	{
		if (!File.Exists(LegacyCentralTelemetryQueueFile)) return;
		try
		{
			List<JObject> rows = new List<JObject>();
			foreach (string line in File.ReadAllLines(LegacyCentralTelemetryQueueFile, Encoding.UTF8))
			{
				if (string.IsNullOrWhiteSpace(line)) continue;
				JObject packet = JObject.Parse(line);
				StampCentralTelemetry(packet);
				SealCentralTelemetry(packet, clientToken);
				rows.Add(packet);
				AppendCentralBlackBox(packet);
			}
			if (rows.Count > 0)
			{
				List<JObject> existing = LoadCentralTelemetryQueue();
				existing.AddRange(rows);
				SaveCentralTelemetryQueue(existing);
			}
			File.Delete(LegacyCentralTelemetryQueueFile);
			ClientStore.Log("fila antiga migrada para caixa-preta criptografada: " + rows.Count + " pacote(s)");
		}
		catch (Exception ex) { ClientStore.Log("migracao da fila antiga: " + ex.Message); }
	}

	private async Task<int> FlushCentralTelemetryQueueAsync(string driver, string clientToken)
	{
		MigrateLegacyCentralTelemetryQueue(clientToken);
		List<JObject> packets;
		try { packets = LoadCentralTelemetryQueue(); }
		catch (Exception ex) { ClientStore.Log("fila local recusada por integridade: " + ex.Message); lblTelemetry.Text = "Central GAT: caixa-preta local com erro de integridade"; return 1; }
		if (packets.Count == 0) return 0;
		lblTelemetry.Text = "Central GAT: enviando viagem pendente...";
		int sent = 0;
		int limit = Math.Min(240, packets.Count);
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
			ClientStore.Log("telemetria pendente confirmada pela Central: " + sent + " pacote(s)");
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
SealCentralTelemetry(tele, centralClientToken);
		AppendCentralBlackBox(tele);
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

Replace-One 'lblTelemetry.Text = "Central GAT: reconectando...";' @'
QueueCentralTelemetry(tele);
			lblTelemetry.Text = "Central GAT: viagem salva • aguardando servidor";
'@

Replace-One 'else if (apiResponse2.StatusCode == 404)' @'
else if (apiResponse2.StatusCode == 429 || apiResponse2.StatusCode >= 500)
		{
			QueueCentralTelemetry(tele);
			lblTelemetry.Text = "Central GAT: viagem salva • aguardando servidor";
		}
		else if (apiResponse2.StatusCode == 404)
'@

Set-Content $main.FullName $s -Encoding UTF8
Write-Host 'Fila offline segura aplicada: caixa-preta AES/HMAC, DPAPI, cadeia assinada e reenvio ordenado.'
