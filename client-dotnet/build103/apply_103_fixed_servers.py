from pathlib import Path

root = Path('.')
store = root / 'client-dotnet/GatTelemetry/ClientStore.cs'
main = root / 'client-dotnet/GatTelemetry/MainForm.cs'
proj = root / 'client-dotnet/GatTelemetry/GatTelemetry.csproj'
installer = root / 'client-dotnet/GatTelemetryInstaller/Program.cs'
installer_proj = root / 'client-dotnet/GatTelemetryInstaller/GatTelemetryInstaller.csproj'

# ---- ClientStore: keep the two classic servers always available ----
s = store.read_text(encoding='utf-8')

marker = '        public static List<ServerEntry> LoadServers()\n'
fixed_method = '''        private static List<ServerEntry> GetFixedServers()\n        {\n            return new List<ServerEntry>\n            {\n                new ServerEntry\n                {\n                    Name = "BIDUZAO - DOUGLAS",\n                    Endpoint = "https://douglas.tail4577e8.ts.net"\n                },\n                new ServerEntry\n                {\n                    Name = "JC - JEAN",\n                    Endpoint = "https://jean-jc.tailf14a00.ts.net"\n                }\n            };\n        }\n\n'''
if 'private static List<ServerEntry> GetFixedServers()' not in s:
    if marker not in s:
        raise SystemExit('LoadServers marker not found')
    s = s.replace(marker, fixed_method + marker, 1)

old_missing = '                if (!File.Exists(ServersFile)) return new List<ServerEntry>();\n'
new_missing = '''                if (!File.Exists(ServersFile))\n                {\n                    var defaults = GetFixedServers();\n                    File.WriteAllText(ServersFile, JsonConvert.SerializeObject(defaults, Formatting.Indented), Encoding.UTF8);\n                    Log("servers.json criado com servidores padrão: " + defaults.Count);\n                    return defaults;\n                }\n'''
if old_missing in s:
    s = s.replace(old_missing, new_missing, 1)

collect = '                CollectServers(root, found);\n'
merge = '''                CollectServers(root, found);\n\n                // Os dois servidores clássicos do GAT-LOG ficam sempre disponíveis.\n                // Inserimos primeiro para que nomes personalizados já salvos possam prevalecer.\n                found.InsertRange(0, GetFixedServers());\n'''
if 'found.InsertRange(0, GetFixedServers());' not in s:
    if collect not in s:
        raise SystemExit('CollectServers call not found')
    s = s.replace(collect, merge, 1)

store.write_text(s, encoding='utf-8')

# ---- Main client version + window icon ----
s = main.read_text(encoding='utf-8')
s = s.replace('private const string CurrentVersion = "1.0.0";', 'private const string CurrentVersion = "1.0.3";')
s = s.replace('Text = "GAT Telemetria C# 1.0 TESTE";', 'Text = "GAT Telemetria C# 1.0.3 TESTE";')
font_line = '            Font = new Font("Segoe UI", 9F);\n'
icon_line = '            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);\n'
if icon_line not in s:
    if font_line not in s:
        raise SystemExit('MainForm font marker not found')
    s = s.replace(font_line, font_line + icon_line, 1)
main.write_text(s, encoding='utf-8')

# ---- Main client project version + icon ----
s = proj.read_text(encoding='utf-8')
s = s.replace('<Version>1.0.0.0</Version>', '<Version>1.0.3.0</Version>')
s = s.replace('<FileVersion>1.0.0.0</FileVersion>', '<FileVersion>1.0.3.0</FileVersion>')
s = s.replace('<AssemblyVersion>1.0.0.0</AssemblyVersion>', '<AssemblyVersion>1.0.3.0</AssemblyVersion>')
if '<ApplicationIcon>assets\\GAT_CLIENT.ico</ApplicationIcon>' not in s:
    s = s.replace('<UseWindowsForms>true</UseWindowsForms>', '<UseWindowsForms>true</UseWindowsForms>\n    <ApplicationIcon>assets\\GAT_CLIENT.ico</ApplicationIcon>', 1)
proj.write_text(s, encoding='utf-8')

# ---- Installer version + shortcut icon refresh ----
s = installer.read_text(encoding='utf-8')
if 'using System.Runtime.InteropServices;' not in s:
    s = s.replace('using System.Reflection;\n', 'using System.Reflection;\nusing System.Runtime.InteropServices;\n', 1)

class_marker = '    internal static class Program\n    {\n'
dll_import = '''    internal static class Program\n    {\n        [DllImport("shell32.dll")]\n        private static extern void SHChangeNotify(uint wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);\n\n'''
if 'SHChangeNotify' not in s:
    if class_marker not in s:
        raise SystemExit('Installer Program class marker not found')
    s = s.replace(class_marker, dll_import, 1)

s = s.replace('Instalar GAT Telemetria C# 1.0.0 TESTE?', 'Atualizar GAT Telemetria para 1.0.3?')
s = s.replace('GAT Telemetria C# 1.0.0 TESTE instalado.', 'GAT Telemetria C# 1.0.3 atualizado.')

shortcut_desc = '            shortcut.Description = "GAT Telemetria";\n'
shortcut_icon = '            shortcut.IconLocation = targetPath + ",0";\n'
if shortcut_icon not in s:
    if shortcut_desc not in s:
        raise SystemExit('Shortcut marker not found')
    s = s.replace(shortcut_desc, shortcut_desc + shortcut_icon, 1)

create_line = '                CreateShortcut(Path.Combine(CommonPrograms, "GAT Telemetria.lnk"), Path.Combine(InstallDir, "GAT_TELEMETRIA.exe"));\n'
notify_line = '                SHChangeNotify(0x08000000, 0x0000, IntPtr.Zero, IntPtr.Zero);\n'
if notify_line not in s:
    if create_line not in s:
        raise SystemExit('CreateShortcut call marker not found')
    s = s.replace(create_line, create_line + notify_line, 1)

installer.write_text(s, encoding='utf-8')

# ---- Installer project name + icon ----
s = installer_proj.read_text(encoding='utf-8')
s = s.replace('GAT_TELEMETRIA_DOTNET_SETUP_1.0.0_TESTE', 'GAT_TELEMETRIA_DOTNET_UPDATE_1.0.3_TESTE')
if '<ApplicationIcon>assets\\GAT_CLIENT.ico</ApplicationIcon>' not in s:
    s = s.replace('<ApplicationManifest>app.manifest</ApplicationManifest>', '<ApplicationManifest>app.manifest</ApplicationManifest>\n    <ApplicationIcon>assets\\GAT_CLIENT.ico</ApplicationIcon>', 1)
installer_proj.write_text(s, encoding='utf-8')

print('GAT Telemetria 1.0.3 patch applied: fixed classic servers + preserved data + client icon.')
