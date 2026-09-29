param(
    [Parameter(Mandatory=$true)]
    [string]$Root
)

$ErrorActionPreference = 'Stop'
$rootPath = (Resolve-Path $Root).Path
$main = Get-ChildItem $rootPath -Filter 'MainForm.cs' -Recurse | Select-Object -First 1
$project = Get-ChildItem $rootPath -Filter 'GAT_TELEMETRIA.csproj' -Recurse | Select-Object -First 1
if (-not $main -or -not $project) { throw 'Fonte 1.0.68.8 incompleta para aplicar detector automatico de mapa.' }

$mainText = Get-Content $main.FullName -Raw
$projectText = Get-Content $project.FullName -Raw

$mainText = $mainText.Replace('CurrentVersion = "1.0.68.8"', 'CurrentVersion = "1.0.68.9"')
$mainText = $mainText.Replace('Text = "Cliente 1.0.68.8"', 'Text = "Cliente 1.0.68.9"')
$mainText = $mainText.Replace('HUB 1.0.68.8:', 'HUB 1.0.68.9:')

$mapAssignPattern = 'tele\["gat_map"\]\s*=\s*CurrentMapModeKey\s*;\s*tele\["gat_map_label"\]\s*=\s*CurrentMapModeLabel\s*;'
if ([regex]::IsMatch($mainText, $mapAssignPattern)) {
    $mapAssignReplacement = @'
		GatMapDetection gatMap = DetectMapFromGameLog();
		tele["gat_map"] = gatMap.Key;
		tele["gat_map_label"] = gatMap.Label;
		tele["gat_map_package"] = gatMap.PackageName;
		tele["gat_map_source"] = gatMap.Source;
'@
    $mainText = [regex]::Replace($mainText, $mapAssignPattern, $mapAssignReplacement.Trim(), 1)
} elseif ($mainText -notlike '*DetectMapFromGameLog()*') {
    throw 'Bloco gat_map nao encontrado no MainForm.'
}

$srcDir = Split-Path $main.FullName -Parent
$mapPath = Join-Path $srcDir 'MapAutoDetect.cs'
$mapSource = @'
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace GatTelemetry;

internal sealed partial class MainForm
{
    private sealed class GatMapDetection
    {
        public string Key = "base";
        public string Label = "Mapa Base";
        public string PackageName = string.Empty;
        public string Source = "game.log";
    }

    private sealed class GatMountedMod
    {
        public string Name = string.Empty;
        public string PackageName = string.Empty;
        public string Raw = string.Empty;
    }

    private DateTime _gatMapLastScanUtc = DateTime.MinValue;
    private string _gatMapLastLogPath = string.Empty;
    private long _gatMapLastLogLength = -1;
    private DateTime _gatMapLastLogWriteUtc = DateTime.MinValue;
    private GatMapDetection _gatMapCached = new GatMapDetection();

    private GatMapDetection DetectMapFromGameLog()
    {
        try
        {
            if ((DateTime.UtcNow - _gatMapLastScanUtc).TotalSeconds < 8.0 && _gatMapCached != null)
                return _gatMapCached;

            _gatMapLastScanUtc = DateTime.UtcNow;
            string path = GatFindGameLog();
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return GatRememberMap(new GatMapDetection { Key = "base", Label = "Mapa Base", Source = "game.log:not-found" }, path);

            FileInfo fi = new FileInfo(path);
            if (string.Equals(path, _gatMapLastLogPath, StringComparison.OrdinalIgnoreCase) &&
                fi.Length == _gatMapLastLogLength &&
                fi.LastWriteTimeUtc == _gatMapLastLogWriteUtc &&
                _gatMapCached != null)
                return _gatMapCached;

            _gatMapLastLogPath = path;
            _gatMapLastLogLength = fi.Length;
            _gatMapLastLogWriteUtc = fi.LastWriteTimeUtc;

            string[] all = File.ReadAllLines(path);
            int start = Math.Max(0, all.Length - 14000);
            var lines = new List<string>(all.Length - start);
            for (int i = start; i < all.Length; i++) lines.Add(all[i] ?? string.Empty);

            List<GatMountedMod> mods = GatParseMountedMods(lines);
            GatMountedMod mapMod = GatChooseMapMod(mods, lines);
            if (mapMod == null)
                return GatRememberMap(new GatMapDetection { Key = "base", Label = "Mapa Base", Source = "game.log:no-map-mod" }, path);

            string label = GatCleanMapLabel(mapMod.Name, mapMod.PackageName);
            string key = GatSlug(label);
            if (string.IsNullOrWhiteSpace(key)) key = GatSlug(mapMod.PackageName);
            if (string.IsNullOrWhiteSpace(key) || key == "map" || key == "mapa") key = "other";

            return GatRememberMap(new GatMapDetection
            {
                Key = key,
                Label = string.IsNullOrWhiteSpace(label) ? mapMod.PackageName : label,
                PackageName = mapMod.PackageName,
                Source = "game.log:auto"
            }, path);
        }
        catch (Exception ex)
        {
            try { ClientStore.Log("mapa auto: falha ao ler game.log: " + ex.Message); } catch { }
            return _gatMapCached ?? new GatMapDetection();
        }
    }

    private GatMapDetection GatRememberMap(GatMapDetection result, string path)
    {
        if (result == null) result = new GatMapDetection();
        bool changed = _gatMapCached == null ||
            !string.Equals(_gatMapCached.Key, result.Key, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(_gatMapCached.Label, result.Label, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(_gatMapCached.PackageName, result.PackageName, StringComparison.OrdinalIgnoreCase);

        _gatMapCached = result;
        if (changed)
        {
            try
            {
                ClientStore.Log("mapa auto detectado: key=" + result.Key +
                    " | label=" + result.Label +
                    " | package=" + result.PackageName +
                    " | source=" + result.Source +
                    (string.IsNullOrWhiteSpace(path) ? string.Empty : " | log=" + path));
            }
            catch { }
        }
        return result;
    }

    private static string GatFindGameLog()
    {
        var candidates = new List<string>();
        string personal = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
        if (!string.IsNullOrWhiteSpace(personal))
            candidates.Add(Path.Combine(personal, "Euro Truck Simulator 2", "game.log.txt"));

        string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(profile))
        {
            candidates.Add(Path.Combine(profile, "Documents", "Euro Truck Simulator 2", "game.log.txt"));
            candidates.Add(Path.Combine(profile, "OneDrive", "Documents", "Euro Truck Simulator 2", "game.log.txt"));
        }

        return candidates
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(File.Exists)
            .Select(p => new FileInfo(p))
            .OrderByDescending(f => f.LastWriteTimeUtc)
            .Select(f => f.FullName)
            .FirstOrDefault() ?? string.Empty;
    }

    private static List<GatMountedMod> GatParseMountedMods(List<string> lines)
    {
        var result = new List<GatMountedMod>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var mounted = new Regex(@"\[mod_package_manager\]\s+Mod\s+\""(?<name>[^\"]+)\"\s+has been mounted\.\s*\(package_name:\s*(?<pkg>[^,\)]+)", RegexOptions.IgnoreCase);
        var active = new Regex(@"\[mods\]\s+Active (?:local|workshop) mod\s+(?<pkg>[^\s]+)\s+\(name:\s*(?<name>[^,\)]+)", RegexOptions.IgnoreCase);

        foreach (string line in lines)
        {
            Match m = mounted.Match(line);
            if (!m.Success) m = active.Match(line);
            if (!m.Success) continue;

            string name = (m.Groups["name"].Value ?? string.Empty).Trim();
            string pkg = (m.Groups["pkg"].Value ?? string.Empty).Trim();
            string id = (pkg + "|" + name).ToLowerInvariant();
            if (!seen.Add(id)) continue;
            result.Add(new GatMountedMod { Name = name, PackageName = pkg, Raw = line });
        }
        return result;
    }

    private static GatMountedMod GatChooseMapMod(List<GatMountedMod> mods, List<string> lines)
    {
        GatMountedMod best = null;
        int bestScore = int.MinValue;

        foreach (GatMountedMod mod in mods)
        {
            string joined = ((mod.Name ?? string.Empty) + " " + (mod.PackageName ?? string.Empty)).ToLowerInvariant();
            int score = 0;

            if (Regex.IsMatch(joined, @"(^|[^a-z0-9])(map|maps|mapa|mapas)([^a-z0-9]|$)")) score += 90;
            if (joined.Contains("defmap") || joined.Contains("def-map") || joined.Contains("def_map")) score += 95;
            if (joined.Contains("-map-") || joined.Contains("_map_") || joined.EndsWith("-map") || joined.EndsWith("_map")) score += 80;
            if (joined.Contains("road") || joined.Contains("roads") || joined.Contains("route") || joined.Contains("rota")) score += 35;
            if (joined.Contains("region") || joined.Contains("territory") || joined.Contains("expansion") || joined.Contains("rebuild")) score += 30;
            if (joined.Contains("connector") || joined.Contains("connection")) score += 18;

            if (Regex.IsMatch(joined, @"\b(asset|assets|model|models|media|sound|sounds|engine|truck|trailer|skin|paint|wheel|wheels|interior|radio|weather|physics|traffic|cargo)\b"))
                score -= 55;

            string pkgToken = (mod.PackageName ?? string.Empty).Trim().ToLowerInvariant();
            if (pkgToken.Length > 2)
            {
                foreach (string line in lines)
                {
                    string low = line.ToLowerInvariant();
                    if (!low.Contains(pkgToken)) continue;
                    if (low.Contains("/map/") || low.Contains("\\map\\") || low.Contains(".mbd") || low.Contains("map_data.sii"))
                    {
                        score += 130;
                        break;
                    }
                }
            }

            if (score > bestScore)
            {
                bestScore = score;
                best = mod;
            }
        }

        return bestScore >= 60 ? best : null;
    }

    private static string GatCleanMapLabel(string name, string packageName)
    {
        string s = (name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(s)) s = (packageName ?? string.Empty).Trim();

        s = Regex.Replace(s, @"\s*\((?:map|mapa|def(?:inition)?(?:\s*&\s*map)?|package)[^\)]*\)\s*$", "", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"\s+(?:def(?:inition)?\s*&\s*map|def\s*&\s*map|map|mapa)\s+package\s*$", "", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"\s+map\s*$", "", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"\s+mapa\s*$", "", RegexOptions.IgnoreCase);
        return s.Trim();
    }

    private static string GatSlug(string value)
    {
        string s = (value ?? string.Empty).Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var b = new StringBuilder();
        foreach (char c in s)
        {
            UnicodeCategory cat = CharUnicodeInfo.GetUnicodeCategory(c);
            if (cat == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsLetterOrDigit(c)) b.Append(c);
            else if (b.Length > 0 && b[b.Length - 1] != '_') b.Append('_');
        }
        string key = b.ToString().Trim('_');
        key = Regex.Replace(key, @"_(?:map|mapa)_package$", "");
        key = Regex.Replace(key, @"_(?:def|definition)(?:_and)?_(?:map|mapa)_package$", "");
        key = Regex.Replace(key, @"_package$", "");
        key = Regex.Replace(key, @"_(?:map|mapa)$", "");
        return key.Trim('_');
    }
}
'@

Set-Content $mapPath $mapSource -Encoding UTF8

if ($projectText -notmatch '<Project\s+Sdk=' -and $projectText -notmatch 'MapAutoDetect\.cs') {
    $compileGroup = @"
  <ItemGroup>
    <Compile Include="MapAutoDetect.cs" />
  </ItemGroup>
"@
    if ($projectText -notmatch '</Project>') { throw 'Fim do csproj nao encontrado.' }
    $projectText = $projectText -replace '</Project>', ($compileGroup + [Environment]::NewLine + '</Project>')
}

if ($mainText -notlike '*CurrentVersion = "1.0.68.9"*') { throw 'Versao 1.0.68.9 nao aplicada.' }
foreach ($m in @('DetectMapFromGameLog()','gat_map_package','gat_map_source')) {
    if ($mainText -notlike "*$m*") { throw "MainForm sem detector automatico: $m" }
}
foreach ($m in @('GatParseMountedMods','GatChooseMapMod','game.log:auto','mapa auto detectado','map_data.sii')) {
    if ($mapSource -notlike "*$m*") { throw "MapAutoDetect sem marcador: $m" }
}

Set-Content $main.FullName $mainText -Encoding UTF8
Set-Content $project.FullName $projectText -Encoding UTF8
Write-Host 'GAT Telemetria 1.0.68.9: detector automatico de mapa pelo game.log aplicado.'
