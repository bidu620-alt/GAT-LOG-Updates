from pathlib import Path

root = Path('.')
main = root / 'client-dotnet/GatTelemetry/MainForm.cs'
proj = root / 'client-dotnet/GatTelemetry/GatTelemetry.csproj'
installer = root / 'client-dotnet/GatTelemetryInstaller/Program.cs'
installer_proj = root / 'client-dotnet/GatTelemetryInstaller/GatTelemetryInstaller.csproj'

s = main.read_text(encoding='utf-8')

# Usings necessários apenas para normalização de nomes.
if 'using System.Globalization;' not in s:
    s = s.replace('using System.IO;\n', 'using System.IO;\nusing System.Globalization;\n', 1)
if 'using System.Text;' not in s:
    s = s.replace('using System.Security.Cryptography;\n', 'using System.Security.Cryptography;\nusing System.Text;\n', 1)

# Durante a identificação inicial, consulta a telemetria local antes de escolher o motorista.
old_call = '                    string matched = ChooseDriver(players.Players);\n'
new_call = '''                    JObject identityTelemetry = null;\n                    try { identityTelemetry = await _telemetry.ReadAsync(); } catch { }\n                    string matched = ChooseDriver(players.Players, identityTelemetry);\n'''
if old_call in s:
    s = s.replace(old_call, new_call, 1)
elif 'ChooseDriver(players.Players, identityTelemetry)' not in s:
    raise SystemExit('Chamada ChooseDriver não encontrada')

start = s.find('        private string ChooseDriver(')
end = s.find('        private async Task<bool> LoginAsync', start)
if start < 0 or end < 0:
    raise SystemExit('Bloco ChooseDriver/LoginAsync não encontrado')

robust = r'''        private string ChooseDriver(List<string> players, JObject localTelemetry)
        {
            if (players == null || players.Count == 0) return string.Empty;

            // 1) Motorista já conhecido nesta execução.
            if (!string.IsNullOrWhiteSpace(_driver))
            {
                var m = MatchPlayer(_driver, players);
                if (!string.IsNullOrWhiteSpace(m)) return m;
            }

            // 2) Motorista/token salvo anteriormente para este servidor.
            var saved = ClientStore.FindCredential(_endpoint, _settings.LastDriver);
            if (saved != null)
            {
                var m = MatchPlayer(saved.Driver, players);
                if (!string.IsNullOrWhiteSpace(m)) return m;
            }

            if (!string.IsNullOrWhiteSpace(_settings.LastDriver))
            {
                var m = MatchPlayer(_settings.LastDriver, players);
                if (!string.IsNullOrWhiteSpace(m)) return m;
            }

            // 3) Nome encontrado na telemetria local do próprio ETS2/TruckSim GPS.
            var fromTelemetry = FindTelemetryDriverHint(localTelemetry, players, 0);
            if (!string.IsNullOrWhiteSpace(fromTelemetry))
            {
                ClientStore.Log("motorista identificado pela telemetria local: " + fromTelemetry);
                return fromTelemetry;
            }

            // 4) Conta Steam ativa deste PC.
            var steam = GetLocalSteamIdentity();
            if (steam != null && !string.IsNullOrWhiteSpace(steam.PersonaName))
            {
                var m = MatchPlayer(steam.PersonaName, players);
                if (!string.IsNullOrWhiteSpace(m))
                {
                    ClientStore.Log("motorista identificado pela Steam: " + steam.SteamId + " -> " + m);
                    return m;
                }
            }

            // 5) game.log do ETS2 como último identificador local confiável.
            var fromLog = FindDriverInGameLog(players);
            if (!string.IsNullOrWhiteSpace(fromLog))
            {
                ClientStore.Log("motorista identificado pelo game.log: " + fromLog);
                return fromLog;
            }

            // Mantém o fallback legado para sessão com um único jogador.
            return players.Count == 1 ? players[0] : string.Empty;
        }

        private static string MatchPlayer(string hint, List<string> players)
        {
            if (string.IsNullOrWhiteSpace(hint) || players == null) return string.Empty;

            var normalized = NormalizeDriverName(hint);
            if (normalized.Length == 0) return string.Empty;

            var exact = players.FirstOrDefault(x =>
                !string.IsNullOrWhiteSpace(x) &&
                NormalizeDriverName(x) == normalized);
            if (!string.IsNullOrWhiteSpace(exact)) return exact;

            var compact = CompactDriverName(hint);
            if (compact.Length >= 3)
            {
                var compactMatches = players
                    .Where(x => !string.IsNullOrWhiteSpace(x) && CompactDriverName(x) == compact)
                    .ToList();
                if (compactMatches.Count == 1) return compactMatches[0];
            }

            // Aceita pequenas tags/símbolos extras somente quando o resultado é único.
            if (compact.Length >= 5)
            {
                var near = players.Where(x =>
                {
                    var p = CompactDriverName(x);
                    if (p.Length < 5) return false;
                    if (Math.Abs(p.Length - compact.Length) > 8) return false;
                    return p.Contains(compact) || compact.Contains(p);
                }).ToList();
                if (near.Count == 1) return near[0];
            }

            return string.Empty;
        }

        private static string NormalizeDriverName(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            var formD = value.Trim().Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();
            bool lastSpace = false;
            foreach (var ch in formD)
            {
                var category = CharUnicodeInfo.GetUnicodeCategory(ch);
                if (category == UnicodeCategory.NonSpacingMark) continue;
                if (char.IsLetterOrDigit(ch))
                {
                    sb.Append(char.ToLowerInvariant(ch));
                    lastSpace = false;
                }
                else if (char.IsWhiteSpace(ch) && !lastSpace && sb.Length > 0)
                {
                    sb.Append(' ');
                    lastSpace = true;
                }
            }
            return sb.ToString().Trim();
        }

        private static string CompactDriverName(string value)
        {
            return NormalizeDriverName(value).Replace(" ", string.Empty);
        }

        private static string FindTelemetryDriverHint(JToken token, List<string> players, int depth)
        {
            if (token == null || depth > 6) return string.Empty;

            var obj = token as JObject;
            if (obj != null)
            {
                foreach (var prop in obj.Properties())
                {
                    var key = prop.Name.Replace("_", string.Empty).Replace("-", string.Empty).ToLowerInvariant();
                    if (key == "playername" || key == "profilename" || key == "steamname" ||
                        key == "username" || key == "multiplayername" || key == "drivername")
                    {
                        var text = prop.Value.Type == JTokenType.String ? prop.Value.ToString() : string.Empty;
                        var m = MatchPlayer(text, players);
                        if (!string.IsNullOrWhiteSpace(m)) return m;
                    }

                    var nested = FindTelemetryDriverHint(prop.Value, players, depth + 1);
                    if (!string.IsNullOrWhiteSpace(nested)) return nested;
                }
                return string.Empty;
            }

            var array = token as JArray;
            if (array != null)
            {
                foreach (var child in array)
                {
                    var nested = FindTelemetryDriverHint(child, players, depth + 1);
                    if (!string.IsNullOrWhiteSpace(nested)) return nested;
                }
            }

            return string.Empty;
        }

        private static string FindDriverInGameLog(List<string> players)
        {
            try
            {
                var candidates = new List<string>();
                var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                if (!string.IsNullOrWhiteSpace(docs))
                    candidates.Add(Path.Combine(docs, "Euro Truck Simulator 2", "game.log.txt"));

                var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                if (!string.IsNullOrWhiteSpace(profile))
                {
                    candidates.Add(Path.Combine(profile, "Documents", "Euro Truck Simulator 2", "game.log.txt"));
                    candidates.Add(Path.Combine(profile, "OneDrive", "Documents", "Euro Truck Simulator 2", "game.log.txt"));
                }

                foreach (var path in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    if (!File.Exists(path)) continue;
                    string[] lines;
                    try { lines = File.ReadAllLines(path); }
                    catch { continue; }

                    int min = Math.Max(0, lines.Length - 3500);
                    for (int i = lines.Length - 1; i >= min; i--)
                    {
                        var line = lines[i] ?? string.Empty;
                        var lower = line.ToLowerInvariant();
                        if (!(lower.Contains("player") || lower.Contains("profile") || lower.Contains("steam") ||
                              lower.Contains("multiplayer") || lower.Contains("convoy") || lower.Contains("connected")))
                            continue;

                        var found = FindUniquePlayerMention(line, players);
                        if (!string.IsNullOrWhiteSpace(found)) return found;
                    }
                }
            }
            catch { }
            return string.Empty;
        }

        private static string FindUniquePlayerMention(string text, List<string> players)
        {
            if (string.IsNullOrWhiteSpace(text) || players == null) return string.Empty;
            var normalizedText = NormalizeDriverName(text);
            if (normalizedText.Length == 0) return string.Empty;

            var matches = new List<string>();
            foreach (var p in players)
            {
                var n = NormalizeDriverName(p);
                if (n.Length >= 3 && normalizedText.Contains(n)) matches.Add(p);
            }
            return matches.Distinct(StringComparer.OrdinalIgnoreCase).Count() == 1
                ? matches.First()
                : string.Empty;
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

                    const ulong SteamIdBase = 76561197960265728UL;
                    if (activeUser != 0 && steamId64 >= SteamIdBase && steamId64 - SteamIdBase == activeUser)
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

s = s[:start] + robust + s[end:]
s = s.replace('private const string CurrentVersion = "1.0.4";', 'private const string CurrentVersion = "1.0.5";')
s = s.replace('Text = "GAT Telemetria C# 1.0.4 TESTE";', 'Text = "GAT Telemetria C# 1.0.5 TESTE";')
main.write_text(s, encoding='utf-8')

s = proj.read_text(encoding='utf-8')
s = s.replace('<Version>1.0.4.0</Version>', '<Version>1.0.5.0</Version>')
s = s.replace('<FileVersion>1.0.4.0</FileVersion>', '<FileVersion>1.0.5.0</FileVersion>')
s = s.replace('<AssemblyVersion>1.0.4.0</AssemblyVersion>', '<AssemblyVersion>1.0.5.0</AssemblyVersion>')
proj.write_text(s, encoding='utf-8')

s = installer.read_text(encoding='utf-8')
s = s.replace('Atualizar GAT Telemetria para 1.0.4?', 'Atualizar GAT Telemetria para 1.0.5?')
s = s.replace('GAT Telemetria C# 1.0.4 atualizado.', 'GAT Telemetria C# 1.0.5 atualizado.')
installer.write_text(s, encoding='utf-8')

s = installer_proj.read_text(encoding='utf-8')
s = s.replace('GAT_TELEMETRIA_DOTNET_UPDATE_1.0.4_TESTE', 'GAT_TELEMETRIA_DOTNET_UPDATE_1.0.5_TESTE')
installer_proj.write_text(s, encoding='utf-8')

print('GAT Telemetria 1.0.5 patch applied: saved -> local telemetry -> Steam -> game.log, normalized matching.')
