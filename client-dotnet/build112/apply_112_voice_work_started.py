from pathlib import Path

main=Path('client-dotnet/GatTelemetry/MainForm.cs')
proj=Path('client-dotnet/GatTelemetry/GatTelemetry.csproj')
installer=Path('client-dotnet/GatTelemetryInstaller/Program.cs')
installer_proj=Path('client-dotnet/GatTelemetryInstaller/GatTelemetryInstaller.csproj')

s=main.read_text(encoding='utf-8')

# System.Speech para aviso por voz no Windows.
if 'using System.Speech.Synthesis;' not in s:
    s=s.replace('using System.Security.Cryptography;\n', 'using System.Security.Cryptography;\nusing System.Speech.Synthesis;\n', 1)

# Estado do aviso: evita repetição a cada envio de telemetria.
field_marker='        private RemoteVersion _availableUpdate;\n'
fields='''        private RemoteVersion _availableUpdate;\n        private SpeechSynthesizer _voice;\n        private string _lastMissionState = string.Empty;\n        private string _lastMissionId = string.Empty;\n        private string _lastAnnouncedMissionId = string.Empty;\n        private bool _missionStateKnown;\n'''
if '_lastAnnouncedMissionId' not in s:
    if field_marker not in s: raise SystemExit('campo RemoteVersion não encontrado')
    s=s.replace(field_marker,fields,1)

# Libera o sintetizador junto com o app.
old='''                _api.Dispose();\n                _telemetry.Dispose();\n            };'''
new='''                _api.Dispose();\n                _telemetry.Dispose();\n                try { _voice?.Dispose(); } catch { }\n            };'''
if old in s and '_voice?.Dispose()' not in s:
    s=s.replace(old,new,1)

# Métodos do anúncio. A Central GAT é a fonte de verdade: fala apenas quando a missão
# é aceita agora (started=true) ou muda de ATRIBUÍDA para EM ANDAMENTO no mesmo ID.
if 'private void AnnounceWorkStarted(' not in s:
    marker='        private async Task SendCentralTelemetryAsync()\n'
    pos=s.find(marker)
    if pos < 0: raise SystemExit('SendCentralTelemetryAsync não encontrado')
    methods=r'''        private void AnnounceWorkStarted(string missionId)
        {
            string key = string.IsNullOrWhiteSpace(missionId) ? "work-active" : missionId;
            if (string.Equals(_lastAnnouncedMissionId, key, StringComparison.OrdinalIgnoreCase)) return;
            _lastAnnouncedMissionId = key;
            try
            {
                if (_voice == null)
                {
                    _voice = new SpeechSynthesizer();
                    _voice.Volume = 100;
                    _voice.Rate = 0;
                }
                _voice.SpeakAsyncCancelAll();
                _voice.SpeakAsync("Trabalho iniciado.");
                ClientStore.Log("voz: trabalho iniciado" + (string.IsNullOrWhiteSpace(missionId) ? string.Empty : " • " + missionId));
            }
            catch (Exception ex)
            {
                ClientStore.Log("voz indisponível: " + ex.Message);
            }
        }

        private void CheckMissionVoice(JObject progress, bool startedNow)
        {
            if (progress == null) return;
            var mission = progress["mission"] as JObject;
            if (mission == null)
            {
                _lastMissionState = string.Empty;
                _lastMissionId = string.Empty;
                _missionStateKnown = true;
                return;
            }

            string missionId = Convert.ToString(mission["id"] ?? string.Empty).Trim();
            string state = Convert.ToString(mission["state"] ?? string.Empty).Trim().ToLowerInvariant();
            bool sameMission = !string.IsNullOrWhiteSpace(missionId) &&
                string.Equals(_lastMissionId, missionId, StringComparison.OrdinalIgnoreCase);
            bool becameActive = _missionStateKnown && sameMission &&
                !string.Equals(_lastMissionState, "active", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(state, "active", StringComparison.OrdinalIgnoreCase);

            if (startedNow || becameActive) AnnounceWorkStarted(missionId);

            _lastMissionId = missionId;
            _lastMissionState = state;
            _missionStateKnown = true;
        }

'''
    s=s[:pos]+methods+s[pos:]

# Integra com a resposta de progresso da Central.
old='''            if (progress.StatusCode == 200 && progress.Json != null && ApiClient.Bool(progress.Json["ok"]))\n            {\n                if (ApiClient.Bool(progress.Json["completed_now"]))\n                    lblTelemetry.Text = "Central GAT: ONLINE • MISSÃO CONCLUÍDA";\n                else if (ApiClient.Bool(progress.Json["started"]))\n                    lblTelemetry.Text = "Central GAT: ONLINE • MISSÃO INICIADA";\n                else\n                    lblTelemetry.Text = "Central GAT: ONLINE";\n                return;\n            }'''
new='''            if (progress.StatusCode == 200 && progress.Json != null && ApiClient.Bool(progress.Json["ok"]))\n            {\n                bool startedNow = ApiClient.Bool(progress.Json["started"]);\n                CheckMissionVoice(progress.Json, startedNow);\n                if (ApiClient.Bool(progress.Json["completed_now"]))\n                    lblTelemetry.Text = "Central GAT: ONLINE • MISSÃO CONCLUÍDA";\n                else if (startedNow)\n                    lblTelemetry.Text = "Central GAT: ONLINE • MISSÃO INICIADA";\n                else\n                    lblTelemetry.Text = "Central GAT: ONLINE";\n                return;\n            }'''
if old in s:
    s=s.replace(old,new,1)
elif 'CheckMissionVoice(progress.Json, startedNow);' not in s:
    raise SystemExit('bloco de resposta da Central não encontrado')

# Versão 1.0.12
s=s.replace('private const string CurrentVersion = "1.0.11";', 'private const string CurrentVersion = "1.0.12";')
s=s.replace('GAT Telemetria C# 1.0.11 TESTE','GAT Telemetria C# 1.0.12 TESTE')
s=s.replace('C# WinForms 1.0.11','C# WinForms 1.0.12')
main.write_text(s,encoding='utf-8')

s=proj.read_text(encoding='utf-8')
if '<Reference Include="System.Speech" />' not in s:
    s=s.replace('<Reference Include="System.Security" />','<Reference Include="System.Security" />\n    <Reference Include="System.Speech" />',1)
s=s.replace('<Version>1.0.11.0</Version>','<Version>1.0.12.0</Version>')
s=s.replace('<FileVersion>1.0.11.0</FileVersion>','<FileVersion>1.0.12.0</FileVersion>')
s=s.replace('<AssemblyVersion>1.0.11.0</AssemblyVersion>','<AssemblyVersion>1.0.12.0</AssemblyVersion>')
proj.write_text(s,encoding='utf-8')

s=installer.read_text(encoding='utf-8')
s=s.replace('Atualizar GAT Telemetria para 1.0.11?','Atualizar GAT Telemetria para 1.0.12?')
s=s.replace('GAT Telemetria C# 1.0.11 atualizado.','GAT Telemetria C# 1.0.12 atualizado.')
installer.write_text(s,encoding='utf-8')

s=installer_proj.read_text(encoding='utf-8')
s=s.replace('GAT_TELEMETRIA_DOTNET_UPDATE_1.0.11_MAPAS_EAA_TESTE','GAT_TELEMETRIA_DOTNET_UPDATE_1.0.12_VOZ_TESTE')
installer_proj.write_text(s,encoding='utf-8')

print('GAT Telemetria 1.0.12 voice work-started applied')
