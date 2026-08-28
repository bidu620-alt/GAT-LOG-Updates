from pathlib import Path

root = Path('.')
main = root / 'client-dotnet/GatTelemetry/MainForm.cs'
proj = root / 'client-dotnet/GatTelemetry/GatTelemetry.csproj'
installer = root / 'client-dotnet/GatTelemetryInstaller/Program.cs'
installer_proj = root / 'client-dotnet/GatTelemetryInstaller/GatTelemetryInstaller.csproj'

# Esta correção é deliberadamente pequena: preserva toda a estrutura da 1.0.3
# e só adiciona identificação automática do motorista pela conta Steam local.
s = main.read_text(encoding='utf-8')

old_choose = '''        private string ChooseDriver(List<string> players)\n        {\n            if (players == null || players.Count == 0) return string.Empty;\n\n            if (!string.IsNullOrWhiteSpace(_driver))\n            {\n                var m = players.FirstOrDefault(x => string.Equals(x, _driver, StringComparison.OrdinalIgnoreCase));\n                if (!string.IsNullOrWhiteSpace(m)) return m;\n            }\n\n            var saved = ClientStore.FindCredential(_endpoint, _settings.LastDriver);\n            if (saved != null)\n            {\n                var m = players.FirstOrDefault(x => string.Equals(x, saved.Driver, StringComparison.OrdinalIgnoreCase));\n                if (!string.IsNullOrWhiteSpace(m)) return m;\n            }\n\n            if (!string.IsNullOrWhiteSpace(_settings.LastDriver))\n            {\n                var m = players.FirstOrDefault(x => string.Equals(x, _settings.LastDriver, StringComparison.OrdinalIgnoreCase));\n                if (!string.IsNullOrWhiteSpace(m)) return m;\n            }\n\n            return players.Count == 1 ? players[0] : string.Empty;\n        }\n'''

new_choose = r'''        private string ChooseDriver(List<string> players)
        {
            if (players == null || players.Count == 0) return string.Empty;

            if (!string.IsNullOrWhiteSpace(_driver))
            {
                var m = players.FirstOrDefault(x => string.Equals(x, _driver, StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrWhiteSpace(m)) return m;
            }

            var saved = ClientStore.FindCredential(_endpoint, _settings.LastDriver);
            if (saved != null)
            {
                var m = players.FirstOrDefault(x => string.Equals(x, saved.Driver, StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrWhiteSpace(m)) return m;
            }

            if (!string.IsNullOrWhiteSpace(_settings.LastDriver))
            {
                var m = players.FirstOrDefault(x => string.Equals(x, _settings.LastDriver, StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrWhiteSpace(m)) return m;
            }

            // Instalação nova: identifica a conta Steam ativa deste PC e usa
            // o PersonaName dela para encontrar este motorista na lista da sessão.
            // Isso não depende da quantidade de jogadores online.
            var steam = GetLocalSteamIdentity();
            if (steam != null && !string.IsNullOrWhiteSpace(steam.PersonaName))
            {
                var steamName = steam.PersonaName.Trim();
                var m = players.FirstOrDefault(x =>
                    !string.IsNullOrWhiteSpace(x) &&
                    string.Equals(x.Trim(), steamName, StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrWhiteSpace(m))
                {
                    ClientStore.Log("motorista identificado pela Steam: " + steam.SteamId + " -> " + m);
                    return m;
                }
            }

            // Mantém o comportamento legado apenas como último fallback.
            return players.Count == 1 ? players[0] : string.Empty;
        }

        private sealed class SteamIdentity
        {
            public string SteamId { get; set; } = string.Empty;
            public string PersonaName { get; set; } = string.Empty;
        }

        private static SteamIdentity GetLocalSteamIdentity()
        {
            try
            {
                string steamPath = string.Empty;
                uint activeUser = 0;

                try
                {
                    using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam"))
                    {
                        steamPath = Convert.ToString(key?.GetValue("SteamPath")) ?? string.Empty;
                    }
                    using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam\ActiveProcess"))
                    {
                        var value = key?.GetValue("ActiveUser");
                        if (value != null) activeUser = Convert.ToUInt32(value);
                    }
                }
                catch { }

                if (string.IsNullOrWhiteSpace(steamPath))
                {
                    try
                    {
                        foreach (var p in Process.GetProcessesByName("steam"))
                        {
                            try
                            {
                                var exe = p.MainModule?.FileName;
                                if (!string.IsNullOrWhiteSpace(exe))
                                {
                                    steamPath = Path.GetDirectoryName(exe) ?? string.Empty;
                                    break;
                                }
                            }
                            catch { }
                            finally { p.Dispose(); }
                        }
                    }
                    catch { }
                }

                if (string.IsNullOrWhiteSpace(steamPath)) return null;
                steamPath = steamPath.Replace('/', Path.DirectorySeparatorChar);
                var loginUsers = Path.Combine(steamPath, "config", "loginusers.vdf");
                if (!File.Exists(loginUsers)) return null;

                var text = File.ReadAllText(loginUsers);
                var matches = System.Text.RegularExpressions.Regex.Matches(
                    text,
                    "\\\"(?<id>7656119[0-9]+)\\\"\\s*\\{(?<body>.*?)\\n\\s*\\}",
                    System.Text.RegularExpressions.RegexOptions.Singleline);

                SteamIdentity recent = null;
                foreach (System.Text.RegularExpressions.Match match in matches)
                {
                    if (!ulong.TryParse(match.Groups["id"].Value, out var steamId64)) continue;
                    var body = match.Groups["body"].Value;
                    var personaMatch = System.Text.RegularExpressions.Regex.Match(
                        body,
                        "\\\"PersonaName\\\"\\s+\\\"(?<v>(?:\\\\.|[^\\\"])*)\\\"");
                    if (!personaMatch.Success) continue;

                    var persona = personaMatch.Groups["v"].Value
                        .Replace("\\\\\\\"", "\\\"")
                        .Replace("\\\\\\\\", "\\\\")
                        .Trim();
                    if (string.IsNullOrWhiteSpace(persona)) continue;

                    var identity = new SteamIdentity
                    {
                        SteamId = steamId64.ToString(),
                        PersonaName = persona
                    };

                    // SteamID64 = base + AccountID. O ActiveUser do registro é o AccountID.
                    const ulong SteamIdBase = 76561197960265728UL;
                    if (activeUser != 0 && steamId64 >= SteamIdBase &&
                        steamId64 - SteamIdBase == activeUser)
                        return identity;

                    var recentMatch = System.Text.RegularExpressions.Regex.Match(
                        body,
                        "\\\"MostRecent\\\"\\s+\\\"1\\\"");
                    if (recentMatch.Success) recent = identity;
                }

                return recent;
            }
            catch
            {
                return null;
            }
        }
'''

if 'private static SteamIdentity GetLocalSteamIdentity()' not in s:
    if old_choose not in s:
        raise SystemExit('ChooseDriver block 1.0.3 not found')
    s = s.replace(old_choose, new_choose, 1)

s = s.replace('private const string CurrentVersion = "1.0.3";', 'private const string CurrentVersion = "1.0.4";')
s = s.replace('Text = "GAT Telemetria C# 1.0.3 TESTE";', 'Text = "GAT Telemetria C# 1.0.4 TESTE";')
main.write_text(s, encoding='utf-8')

s = proj.read_text(encoding='utf-8')
s = s.replace('<Version>1.0.3.0</Version>', '<Version>1.0.4.0</Version>')
s = s.replace('<FileVersion>1.0.3.0</FileVersion>', '<FileVersion>1.0.4.0</FileVersion>')
s = s.replace('<AssemblyVersion>1.0.3.0</AssemblyVersion>', '<AssemblyVersion>1.0.4.0</AssemblyVersion>')
proj.write_text(s, encoding='utf-8')

s = installer.read_text(encoding='utf-8')
s = s.replace('Atualizar GAT Telemetria para 1.0.3?', 'Atualizar GAT Telemetria para 1.0.4?')
s = s.replace('GAT Telemetria C# 1.0.3 atualizado.', 'GAT Telemetria C# 1.0.4 atualizado.')
installer.write_text(s, encoding='utf-8')

s = installer_proj.read_text(encoding='utf-8')
s = s.replace('GAT_TELEMETRIA_DOTNET_UPDATE_1.0.3_TESTE', 'GAT_TELEMETRIA_DOTNET_UPDATE_1.0.4_TESTE')
installer_proj.write_text(s, encoding='utf-8')

print('GAT Telemetria 1.0.4 patch applied: automatic local Steam identity, structure preserved.')
