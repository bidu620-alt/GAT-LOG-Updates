from pathlib import Path

root=Path('.')
main=root/'client-dotnet/GatTelemetry/MainForm.cs'
proj=root/'client-dotnet/GatTelemetry/GatTelemetry.csproj'
installer=root/'client-dotnet/GatTelemetryInstaller/Program.cs'
installer_proj=root/'client-dotnet/GatTelemetryInstaller/GatTelemetryInstaller.csproj'


def method_bounds(src, signature):
    start=src.find(signature)
    if start < 0:
        raise SystemExit('metodo nao encontrado: '+signature)
    brace=src.find('{', start)
    if brace < 0:
        raise SystemExit('abertura do metodo nao encontrada: '+signature)
    depth=0
    i=brace
    while i < len(src):
        c=src[i]
        if c=='{': depth+=1
        elif c=='}':
            depth-=1
            if depth==0:
                end=i+1
                while end < len(src) and src[end] in '\r\n': end+=1
                return start,end
        i+=1
    raise SystemExit('fechamento do metodo nao encontrado: '+signature)

s=main.read_text(encoding='utf-8')

# Mantem uma chave separada para conclusao. O aviso de inicio ja possui seu proprio ID.
field='        private string _lastAnnouncedMissionId = string.Empty;\n'
if 'private string _lastAnnouncedCompletedMissionId' not in s:
    if field not in s:
        raise SystemExit('campo de voz da missao nao encontrado')
    s=s.replace(field,field+'        private string _lastAnnouncedCompletedMissionId = string.Empty;\n',1)

# Troca apenas o metodo antigo de inicio, preservando diario, recibos e demais metodos.
start,end=method_bounds(s,'        private void AnnounceWorkStarted(string missionId)')
new_voice=r'''        private SpeechSynthesizer EnsureVoice()
        {
            if (_voice != null) return _voice;
            _voice = new SpeechSynthesizer();
            _voice.SetOutputToDefaultAudioDevice();
            _voice.Volume = 100;
            _voice.Rate = 0;
            return _voice;
        }

        private bool SpeakGat(string text, string logLabel)
        {
            try
            {
                var voice = EnsureVoice();
                // Mantem a fila do sintetizador; nao cancela um aviso que acabou de ser agendado.
                voice.SpeakAsync(text);
                ClientStore.Log("voz: " + logLabel);
                return true;
            }
            catch (Exception ex)
            {
                ClientStore.Log("voz indisponivel: " + ex.Message);
                try { _voice?.Dispose(); } catch { }
                _voice = null;
                return false;
            }
        }

        private void AnnounceWorkStarted(string missionId)
        {
            string key = string.IsNullOrWhiteSpace(missionId) ? "work-active" : missionId;
            if (string.Equals(_lastAnnouncedMissionId, key, StringComparison.OrdinalIgnoreCase)) return;
            if (SpeakGat("Trabalho iniciado.", "trabalho iniciado" + (string.IsNullOrWhiteSpace(missionId) ? string.Empty : " • " + missionId)))
                _lastAnnouncedMissionId = key;
        }

        private void AnnounceWorkCompleted(string missionId)
        {
            string key = string.IsNullOrWhiteSpace(missionId) ? "work-completed" : missionId;
            if (string.Equals(_lastAnnouncedCompletedMissionId, key, StringComparison.OrdinalIgnoreCase)) return;
            if (SpeakGat("Trabalho concluído.", "trabalho concluido" + (string.IsNullOrWhiteSpace(missionId) ? string.Empty : " • " + missionId)))
                _lastAnnouncedCompletedMissionId = key;
        }

'''
s=s[:start]+new_voice+s[end:]

# O inicio nao depende mais de um pulso unico 'started'. Enquanto a Central confirmar
# state=active, o cliente tenta anunciar uma vez para aquele missionId. Assim, se o
# primeiro pacote/resposta chegar em uma transicao ruim, o proximo pacote recupera a voz.
start,end=method_bounds(s,'        private void CheckMissionVoice(JObject progress, bool startedNow)')
new_check=r'''        private void CheckMissionVoice(JObject progress, bool startedNow, bool completedNow)
        {
            if (progress == null) return;
            var mission = progress["mission"] as JObject;
            string missionId = mission == null ? string.Empty : Convert.ToString(mission["id"] ?? string.Empty).Trim();
            string state = mission == null ? string.Empty : Convert.ToString(mission["state"] ?? string.Empty).Trim().ToLowerInvariant();

            // delivery_completed normalmente ja chega sem uma missao ativa no payload.
            // Nesse caso usamos o ID da ultima missao ativa conhecida para nao repetir a voz.
            if (completedNow)
            {
                string completedId = !string.IsNullOrWhiteSpace(missionId) ? missionId : _lastMissionId;
                AnnounceWorkCompleted(completedId);
            }

            if (mission == null)
            {
                _lastMissionState = string.Empty;
                _lastMissionId = string.Empty;
                _missionStateKnown = true;
                return;
            }

            bool active = string.Equals(state, "active", StringComparison.OrdinalIgnoreCase);
            bool sameMission = !string.IsNullOrWhiteSpace(missionId) &&
                string.Equals(_lastMissionId, missionId, StringComparison.OrdinalIgnoreCase);
            bool becameActive = _missionStateKnown && sameMission &&
                !string.Equals(_lastMissionState, "active", StringComparison.OrdinalIgnoreCase) && active;

            // startedNow cobre a resposta imediata da API; active e o fallback confiavel.
            if (startedNow || becameActive || active) AnnounceWorkStarted(missionId);

            _lastMissionId = missionId;
            _lastMissionState = state;
            _missionStateKnown = true;
        }

'''
s=s[:start]+new_check+s[end:]

# Integra de forma tolerante com o bloco atual da Central.
start_send=s.find('        private async Task SendCentralTelemetryAsync()')
end_send=s.find('        private void EnterClicked(', start_send)
if start_send < 0 or end_send < 0:
    raise SystemExit('segmento SendCentralTelemetryAsync nao encontrado')
segment=s[start_send:end_send]

started_line='                bool startedNow = ApiClient.Bool(progress.Json["started"]);\n'
if 'bool completedNow = ApiClient.Bool(progress.Json["completed_now"]);' not in segment:
    if started_line not in segment:
        raise SystemExit('startedNow da resposta Central nao encontrado')
    segment=segment.replace(started_line,started_line+'                bool completedNow = ApiClient.Bool(progress.Json["completed_now"]);\n',1)

old_call='                CheckMissionVoice(progress.Json, startedNow);\n'
new_call='                CheckMissionVoice(progress.Json, startedNow, completedNow);\n'
if old_call in segment:
    segment=segment.replace(old_call,new_call,1)
elif new_call not in segment:
    raise SystemExit('chamada CheckMissionVoice da Central nao encontrada')

old_completed='                if (ApiClient.Bool(progress.Json["completed_now"]))\n'
new_completed='                if (completedNow)\n'
if old_completed in segment:
    segment=segment.replace(old_completed,new_completed,1)
elif new_completed not in segment:
    raise SystemExit('condicao completed_now da Central nao encontrada')

s=s[:start_send]+segment+s[end_send:]

# Mostra tambem a conclusao no indicador verde no exato retorno de entrega.
old_update='''            bool active = string.Equals(state, "active", StringComparison.OrdinalIgnoreCase);\n            lblWorkStatus.Text = active ? "TRABALHO EM ANDAMENTO" : string.Empty;\n            lblWorkStatus.ForeColor = Color.LimeGreen;\n            lblWorkStatus.Visible = active;\n'''
new_update='''            bool active = string.Equals(state, "active", StringComparison.OrdinalIgnoreCase);\n            bool completed = progress != null && ApiClient.Bool(progress["completed_now"]);\n            lblWorkStatus.Text = completed ? "TRABALHO CONCLUÍDO" : (active ? "TRABALHO EM ANDAMENTO" : string.Empty);\n            lblWorkStatus.ForeColor = Color.LimeGreen;\n            lblWorkStatus.Visible = active || completed;\n'''
if old_update not in s:
    raise SystemExit('UpdateWorkStatus esperado nao encontrado')
s=s.replace(old_update,new_update,1)

if 'private const string CurrentVersion = "1.0.25";' not in s:
    raise SystemExit('versao 1.0.25 nao encontrada')
s=s.replace('private const string CurrentVersion = "1.0.25";','private const string CurrentVersion = "1.0.26";',1)
s=s.replace('GAT Telemetria C# 1.0.25 TESTE','GAT Telemetria C# 1.0.26 TESTE')
s=s.replace('C# WinForms 1.0.25','C# WinForms 1.0.26')
main.write_text(s,encoding='utf-8')

p=proj.read_text(encoding='utf-8').replace('1.0.25.0','1.0.26.0')
proj.write_text(p,encoding='utf-8')

i=installer.read_text(encoding='utf-8')
i=i.replace('Atualizar GAT Telemetria para 1.0.25?','Atualizar GAT Telemetria para 1.0.26?')
i=i.replace('GAT Telemetria C# 1.0.25 atualizado.','GAT Telemetria C# 1.0.26 atualizado.')
installer.write_text(i,encoding='utf-8')

ip=installer_proj.read_text(encoding='utf-8')
ip=ip.replace('GAT_TELEMETRIA_DOTNET_UPDATE_1.0.25_JOB_CONTRACT_TESTE','GAT_TELEMETRIA_DOTNET_UPDATE_1.0.26_VOZ_ESTAVEL_TESTE')
installer_proj.write_text(ip,encoding='utf-8')

text=main.read_text(encoding='utf-8')
checks=[
    'CurrentVersion = "1.0.26"',
    'SetOutputToDefaultAudioDevice()',
    'SpeakGat("Trabalho iniciado."',
    'SpeakGat("Trabalho concluído."',
    'CheckMissionVoice(progress.Json, startedNow, completedNow)',
    'startedNow || becameActive || active',
    'TRABALHO CONCLUÍDO',
    'FlushTripReceiptsAsync',
    'CaptureTripJournalAsync',
]
for value in checks:
    if value not in text:
        raise SystemExit('patch 1.0.26 incompleto: '+value)
if 'SpeakAsyncCancelAll' in text:
    raise SystemExit('cancelamento antigo da fila de voz ainda presente')

print('GAT Telemetria 1.0.26: voz de inicio confiavel + voz/indicador de trabalho concluido')
