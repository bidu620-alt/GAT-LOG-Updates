from pathlib import Path

main=Path('client-dotnet/GatTelemetry/MainForm.cs')
proj=Path('client-dotnet/GatTelemetry/GatTelemetry.csproj')
installer=Path('client-dotnet/GatTelemetryInstaller/Program.cs')
installer_proj=Path('client-dotnet/GatTelemetryInstaller/GatTelemetryInstaller.csproj')

s=main.read_text(encoding='utf-8')

# Campo visual de trabalho em andamento.
if 'private Label lblWorkStatus;' not in s:
    marker='        private Label lblWeight;\n'
    if marker not in s: raise SystemExit('lblWeight não encontrado')
    s=s.replace(marker, marker+'        private Label lblWorkStatus;\n', 1)

# Linha verde na parte inferior da caixa TELEMETRIA.
if 'TRABALHO EM ANDAMENTO' not in s:
    marker='''            telBox.Controls.Add(lblWeight);\n            Controls.Add(telBox);\n'''
    repl='''            telBox.Controls.Add(lblWeight);\n\n            lblWorkStatus = new Label\n            {\n                Text = string.Empty,\n                Left = 18,\n                Top = 118,\n                Width = 757,\n                Height = 22,\n                ForeColor = Color.LimeGreen,\n                Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold),\n                TextAlign = ContentAlignment.MiddleCenter,\n                Visible = false\n            };\n            telBox.Controls.Add(lblWorkStatus);\n            Controls.Add(telBox);\n'''
    if marker not in s: raise SystemExit('marcador da caixa TELEMETRIA não encontrado')
    s=s.replace(marker,repl,1)

# Atualiza o indicador usando o estado real devolvido pela Central GAT.
if 'private void UpdateWorkStatus(JObject progress)' not in s:
    marker='        private void CheckMissionVoice(JObject progress, bool startedNow)\n'
    pos=s.find(marker)
    if pos < 0: raise SystemExit('CheckMissionVoice não encontrado')
    method=r'''        private void UpdateWorkStatus(JObject progress)
        {
            if (lblWorkStatus == null) return;
            var mission = progress == null ? null : progress["mission"] as JObject;
            string state = mission == null ? string.Empty : Convert.ToString(mission["state"] ?? string.Empty).Trim().ToLowerInvariant();
            bool active = string.Equals(state, "active", StringComparison.OrdinalIgnoreCase);
            lblWorkStatus.Text = active ? "TRABALHO EM ANDAMENTO" : string.Empty;
            lblWorkStatus.ForeColor = Color.LimeGreen;
            lblWorkStatus.Visible = active;
        }

'''
    s=s[:pos]+method+s[pos:]

old='''                bool startedNow = ApiClient.Bool(progress.Json["started"]);\n                CheckMissionVoice(progress.Json, startedNow);\n                if (ApiClient.Bool(progress.Json["completed_now"]))'''
new='''                bool startedNow = ApiClient.Bool(progress.Json["started"]);\n                CheckMissionVoice(progress.Json, startedNow);\n                UpdateWorkStatus(progress.Json);\n                if (ApiClient.Bool(progress.Json["completed_now"]))'''
if old in s:
    s=s.replace(old,new,1)
elif 'UpdateWorkStatus(progress.Json);' not in s:
    raise SystemExit('integração do status com a Central não encontrada')

# Versão 1.0.13
s=s.replace('private const string CurrentVersion = "1.0.12";', 'private const string CurrentVersion = "1.0.13";')
s=s.replace('GAT Telemetria C# 1.0.12 TESTE','GAT Telemetria C# 1.0.13 TESTE')
s=s.replace('C# WinForms 1.0.12','C# WinForms 1.0.13')
main.write_text(s,encoding='utf-8')

s=proj.read_text(encoding='utf-8')
s=s.replace('<Version>1.0.12.0</Version>','<Version>1.0.13.0</Version>')
s=s.replace('<FileVersion>1.0.12.0</FileVersion>','<FileVersion>1.0.13.0</FileVersion>')
s=s.replace('<AssemblyVersion>1.0.12.0</AssemblyVersion>','<AssemblyVersion>1.0.13.0</AssemblyVersion>')
proj.write_text(s,encoding='utf-8')

s=installer.read_text(encoding='utf-8')
s=s.replace('Atualizar GAT Telemetria para 1.0.12?','Atualizar GAT Telemetria para 1.0.13?')
s=s.replace('GAT Telemetria C# 1.0.12 atualizado.','GAT Telemetria C# 1.0.13 atualizado.')
installer.write_text(s,encoding='utf-8')

s=installer_proj.read_text(encoding='utf-8')
s=s.replace('GAT_TELEMETRIA_DOTNET_UPDATE_1.0.12_VOZ_TESTE','GAT_TELEMETRIA_DOTNET_UPDATE_1.0.13_STATUS_TESTE')
installer_proj.write_text(s,encoding='utf-8')

print('GAT Telemetria 1.0.13 status de trabalho aplicado')
