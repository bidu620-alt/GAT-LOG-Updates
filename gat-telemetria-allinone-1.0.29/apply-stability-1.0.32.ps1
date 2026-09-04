param([Parameter(Mandatory=$true)][string]$Root)

$main = Get-ChildItem $Root -Filter 'MainForm.cs' -Recurse | Select-Object -First 1
if (-not $main) { throw 'MainForm.cs nao encontrado para aplicar estabilidade 1.0.32' }
$s = Get-Content $main.FullName -Raw

function Replace-One([string]$old,[string]$new) {
  $count = ([regex]::Matches($script:s,[regex]::Escape($old))).Count
  if ($count -ne 1) { throw "Esperava 1 ocorrencia e encontrei ${count}: $($old.Substring(0,[Math]::Min(90,$old.Length)))" }
  $script:s = $script:s.Replace($old,$new)
}

# Valida o registro do PC assim que a conta e restaurada, sem depender do ETS2/telemetria estar conectado.
Replace-One @'
			await RestoreAccountAsync();
			await RefreshServerInfoAsync(force: true);
'@ @'
			await RestoreAccountAsync();
			await ValidatePcRegistrationAsync();
			await RefreshServerInfoAsync(force: true);
'@

$pcValidation = @'
	private async Task ValidatePcRegistrationAsync()
	{
		if (!AccountReady)
		{
			return;
		}
		try
		{
			string driver = _accountUser;
			CredentialEntry credential = ClientStore.FindCredential(AccountAuthority, driver);
			string savedClientToken = ClientStore.GetPlainToken(credential);
			ApiResponse response = await _api.LoginAsync(AccountAuthority, driver, _deviceId, savedClientToken, _accountUser, _accountToken);
			if (response.StatusCode == 200 && response.Json != null && ApiClient.Bool(response.Json["ok"]))
			{
				string returnedToken = ApiClient.Str(response.Json["token"]);
				if (!string.IsNullOrWhiteSpace(returnedToken))
				{
					ClientStore.SaveCredential(AccountAuthority, driver, returnedToken);
				}
				lblAccount.Text = "Conta: @" + _accountUser + " • PC vinculado";
				lblAccount.ForeColor = Color.FromArgb(130, 224, 69);
				SetPcRegistrationState(true, string.Empty);
				return;
			}
			if (response.StatusCode == 428 && response.Json != null)
			{
				string pairingCode = ApiClient.Str(response.Json["pairing_code"]);
				SetPcRegistrationState(false, pairingCode);
				return;
			}
			lblPcRegister.Text = "Validando registro do PC";
			lblPcRegister.ForeColor = Color.Gold;
			lblPcRegisterDetail.Text = "A Central GAT vai tentar novamente automaticamente.";
		}
		catch (Exception ex)
		{
			ClientStore.Log("validacao inicial do PC: " + ex.Message);
			if (lblPcRegister != null)
			{
				lblPcRegister.Text = "Validando registro do PC";
				lblPcRegister.ForeColor = Color.Gold;
			}
			if (lblPcRegisterDetail != null)
			{
				lblPcRegisterDetail.Text = "A Central GAT vai tentar novamente automaticamente.";
			}
		}
	}

'@
Replace-One "`tprivate SpeechSynthesizer EnsureVoice()" ($pcValidation + "`tprivate SpeechSynthesizer EnsureVoice()")

# A 1.0.32 lia TODA a caixa-preta a cada pacote apenas para contar/limitar linhas.
# Isso cresce em O(n^2) e, como roda no fluxo da UI, fazia a janela ficar 'Nao esta respondendo'.
# Agora a gravacao e somente append; a rotacao por tamanho e uma operacao de sistema de arquivos, sem reler/decriptar tudo.
$oldBlackBox = @'
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
'@
$newBlackBox = @'
	private void AppendCentralBlackBox(JObject tele)
	{
		if (tele == null) return;
		try
		{
			ClientStore.Ensure();
			const long RotateAtBytes = 128L * 1024L * 1024L;
			if (File.Exists(CentralTripBlackBoxFile) && new FileInfo(CentralTripBlackBoxFile).Length >= RotateAtBytes)
			{
				string archive = CentralTripBlackBoxFile + ".previous";
				try { if (File.Exists(archive)) File.Delete(archive); } catch { }
				try { File.Move(CentralTripBlackBoxFile, archive); } catch { }
			}
			File.AppendAllText(CentralTripBlackBoxFile, EncryptJournalPacket(tele) + Environment.NewLine, Encoding.ASCII);
		}
		catch (Exception ex) { ClientStore.Log("caixa-preta local: " + ex.Message); }
	}
'@
Replace-One $oldBlackBox $newBlackBox

# A fila offline tinha o mesmo problema: relia o arquivo inteiro a cada segundo enquanto a Central estava indisponivel.
$oldQueue = @'
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
'@
$newQueue = @'
	private void QueueCentralTelemetry(JObject tele)
	{
		if (tele == null) return;
		try
		{
			ClientStore.Ensure();
			StampCentralTelemetry(tele);
			File.AppendAllText(CentralTelemetryQueueFile, EncryptJournalPacket(tele) + Environment.NewLine, Encoding.ASCII);
			ClientStore.Log("telemetria criptografada salva para reenvio: " + TextAny(tele, "gat_packet_id"));
		}
		catch (Exception ex) { ClientStore.Log("fila local segura: " + ex.Message); }
	}
'@
Replace-One $oldQueue $newQueue

Set-Content $main.FullName $s -Encoding UTF8
Write-Host 'Patch de estabilidade 1.0.32 aplicado: validacao imediata do PC + caixa-preta/fila sem releitura por pacote.'
