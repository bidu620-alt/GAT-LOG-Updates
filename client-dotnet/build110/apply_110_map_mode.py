from pathlib import Path

main=Path('client-dotnet/GatTelemetry/MainForm.cs')
proj=Path('client-dotnet/GatTelemetry/GatTelemetry.csproj')
installer=Path('client-dotnet/GatTelemetryInstaller/Program.cs')
installer_proj=Path('client-dotnet/GatTelemetryInstaller/GatTelemetryInstaller.csproj')

s=main.read_text(encoding='utf-8')

# Version
s=s.replace('private const string CurrentVersion = "1.0.9";', 'private const string CurrentVersion = "1.0.10";')
s=s.replace('GAT Telemetria C# 1.0.9 TESTE','GAT Telemetria C# 1.0.10 TESTE')
s=s.replace('C# WinForms 1.0.9','C# WinForms 1.0.10')

# Map selector field.
if 'private ComboBox cmbMapMode;' not in s:
    s=s.replace('        private ComboBox cmbServers;\n', '        private ComboBox cmbServers;\n        private ComboBox cmbMapMode;\n', 1)

# More room for the second row in Conta GAT.
s=s.replace('            MinimumSize = new Size(780, 700);\n            Size = new Size(860, 735);',
            '            MinimumSize = new Size(780, 735);\n            Size = new Size(860, 770);')
s=s.replace('var accountBox = NewGroup("CONTA GAT", 24, 88, 800, 90);',
            'var accountBox = NewGroup("CONTA GAT", 24, 88, 800, 126);')
s=s.replace('var serverBox = NewGroup("COMBOIO / SERVIDOR (OPCIONAL)", 24, 190, 800, 150);',
            'var serverBox = NewGroup("COMBOIO / SERVIDOR (OPCIONAL)", 24, 226, 800, 150);')
s=s.replace('var sessionBox = NewGroup("COMBOIO (OPCIONAL)", 24, 352, 800, 145);',
            'var sessionBox = NewGroup("COMBOIO (OPCIONAL)", 24, 388, 800, 145);')
s=s.replace('var telBox = NewGroup("TELEMETRIA", 24, 509, 800, 148);',
            'var telBox = NewGroup("TELEMETRIA", 24, 545, 800, 148);')
s=s.replace('btnUpdate = MakeButton("VERIFICAR ATUALIZAÇÃO", 24, 672, 220, 32',
            'btnUpdate = MakeButton("VERIFICAR ATUALIZAÇÃO", 24, 708, 220, 32')
s=s.replace('Location = new Point(625, 680)', 'Location = new Point(625, 716)')

# Add map selector below account login row.
if 'MAPA EM USO' not in s:
    marker='''            accountBox.Controls.Add(lblAccount);\n            Controls.Add(accountBox);\n'''
    repl='''            accountBox.Controls.Add(lblAccount);\n\n            var lblMapMode = new Label\n            {\n                Text = "MAPA EM USO",\n                Left = 18,\n                Top = 78,\n                Width = 92,\n                Height = 24,\n                ForeColor = Color.Silver\n            };\n            cmbMapMode = new ComboBox\n            {\n                DropDownStyle = ComboBoxStyle.DropDownList,\n                Left = 116,\n                Top = 72,\n                Width = 250\n            };\n            cmbMapMode.Items.AddRange(new object[] { "Mapa Base", "ProMods", "RBR", "Rotas Brasil", "Outro mapa" });\n            cmbMapMode.SelectedIndexChanged += MapModeChanged;\n            var lblMapHint = new Label\n            {\n                Text = "Define em qual aba do mapa ao vivo seu caminhão aparece.",\n                Left = 382,\n                Top = 78,\n                Width = 392,\n                Height = 24,\n                ForeColor = Color.Gray\n            };\n            accountBox.Controls.Add(lblMapMode);\n            accountBox.Controls.Add(cmbMapMode);\n            accountBox.Controls.Add(lblMapHint);\n            Controls.Add(accountBox);\n'''
    if marker not in s: raise SystemExit('Conta GAT UI marker not found')
    s=s.replace(marker,repl,1)

# Load saved map right after UI is built.
if 'LoadMapMode();' not in s:
    s=s.replace('            BuildUi();\n            LoadServerList();', '            BuildUi();\n            LoadMapMode();\n            LoadServerList();', 1)

# Helpers; separate file avoids changing the existing settings model.
if 'private string CurrentMapModeKey' not in s:
    marker='        private void EnterClicked(object sender, EventArgs e)\n'
    pos=s.find(marker)
    if pos < 0: raise SystemExit('EnterClicked marker not found')
    methods=r'''        private string MapModeFile => Path.Combine(ClientStore.DataDir, "map_mode.txt");

        private string CurrentMapModeKey
        {
            get
            {
                string text = cmbMapMode == null ? string.Empty : Convert.ToString(cmbMapMode.SelectedItem);
                if (string.Equals(text, "ProMods", StringComparison.OrdinalIgnoreCase)) return "promods";
                if (string.Equals(text, "RBR", StringComparison.OrdinalIgnoreCase)) return "rbr";
                if (string.Equals(text, "Rotas Brasil", StringComparison.OrdinalIgnoreCase)) return "rotas_brasil";
                if (string.Equals(text, "Outro mapa", StringComparison.OrdinalIgnoreCase)) return "other";
                return "base";
            }
        }

        private string CurrentMapModeLabel
        {
            get
            {
                string text = cmbMapMode == null ? string.Empty : Convert.ToString(cmbMapMode.SelectedItem);
                return string.IsNullOrWhiteSpace(text) ? "Mapa Base" : text;
            }
        }

        private void LoadMapMode()
        {
            if (cmbMapMode == null) return;
            string key = "base";
            try
            {
                if (File.Exists(MapModeFile)) key = (File.ReadAllText(MapModeFile) ?? string.Empty).Trim().ToLowerInvariant();
            }
            catch { }
            int index = key == "promods" ? 1 : key == "rbr" ? 2 : key == "rotas_brasil" ? 3 : key == "other" ? 4 : 0;
            cmbMapMode.SelectedIndex = index;
        }

        private void MapModeChanged(object sender, EventArgs e)
        {
            try
            {
                ClientStore.Ensure();
                File.WriteAllText(MapModeFile, CurrentMapModeKey);
                ClientStore.Log("mapa em uso: " + CurrentMapModeLabel);
            }
            catch { }
        }

'''
    s=s[:pos]+methods+s[pos:]

# Stamp map identity into independent Central GAT telemetry.
needle='''            tele["gat_account_user"] = _accountUser;\n            tele["gat_client_version"] = CurrentVersion;\n'''
repl='''            tele["gat_account_user"] = _accountUser;\n            tele["gat_client_version"] = CurrentVersion;\n            tele["gat_map"] = CurrentMapModeKey;\n            tele["gat_map_label"] = CurrentMapModeLabel;\n'''
if needle in s:
    s=s.replace(needle,repl,1)
elif 'tele["gat_map"]' not in s:
    raise SystemExit('Central telemetry stamp point not found')

main.write_text(s,encoding='utf-8')

s=proj.read_text(encoding='utf-8')
s=s.replace('<Version>1.0.9.0</Version>','<Version>1.0.10.0</Version>')
s=s.replace('<FileVersion>1.0.9.0</FileVersion>','<FileVersion>1.0.10.0</FileVersion>')
s=s.replace('<AssemblyVersion>1.0.9.0</AssemblyVersion>','<AssemblyVersion>1.0.10.0</AssemblyVersion>')
proj.write_text(s,encoding='utf-8')

s=installer.read_text(encoding='utf-8')
s=s.replace('Atualizar GAT Telemetria para 1.0.9?','Atualizar GAT Telemetria para 1.0.10?')
s=s.replace('GAT Telemetria C# 1.0.9 atualizado.','GAT Telemetria C# 1.0.10 atualizado.')
installer.write_text(s,encoding='utf-8')

s=installer_proj.read_text(encoding='utf-8')
s=s.replace('GAT_TELEMETRIA_DOTNET_UPDATE_1.0.9_TESTE','GAT_TELEMETRIA_DOTNET_UPDATE_1.0.10_MAPAS_TESTE')
installer_proj.write_text(s,encoding='utf-8')

print('GAT Telemetria 1.0.10 map mode applied')
