param(
    [Parameter(Mandatory=$true)]
    [string]$Root
)

$ErrorActionPreference = 'Stop'
$rootPath = (Resolve-Path $Root).Path
$main = Get-ChildItem $rootPath -Filter 'MainForm.cs' -Recurse | Select-Object -First 1
$radio = Get-ChildItem $rootPath -Filter 'RadioForm.cs' -Recurse | Select-Object -First 1
$bridge = Get-ChildItem $rootPath -Filter 'DashMediaBridge.cs' -Recurse | Select-Object -First 1
$project = Get-ChildItem $rootPath -Filter 'GAT_TELEMETRIA.csproj' -Recurse | Select-Object -First 1
if (-not $main -or -not $radio -or -not $bridge -or -not $project) { throw 'Fonte 1.0.40 incompleto para aplicar Hub 1.0.41.' }

$mainText = Get-Content $main.FullName -Raw
$radioText = Get-Content $radio.FullName -Raw
$bridgeText = Get-Content $bridge.FullName -Raw
$projectText = Get-Content $project.FullName -Raw

# Versao 1.0.41.
$mainText = $mainText.Replace('CurrentVersion = "1.0.40.0"', 'CurrentVersion = "1.0.41.0"')
$mainText = $mainText.Replace('Text = "Cliente 1.0.40"', 'Text = "Cliente 1.0.41"')
if ($mainText -notmatch 'CurrentVersion = "1\.0\.41\.0"') { throw 'Nao consegui atualizar CurrentVersion para 1.0.41.0.' }

# MainForm passa a ser parcial para receber o shell modular sem mexer no motor existente.
if ($mainText -notmatch 'internal sealed partial class MainForm') {
    $mainText = [regex]::Replace($mainText, 'internal\s+sealed\s+class\s+MainForm\s*:\s*Form', 'internal sealed partial class MainForm : Form', 1)
}
if ($mainText -notmatch 'internal sealed partial class MainForm') { throw 'Nao consegui converter MainForm para partial.' }

# O BuildUi legado continua criando todos os controles usados pela logica; em seguida
# o Hub reorganiza a experiencia em paginas, preservando login, telemetria e servidor.
if ($mainText -notlike '*ApplyHub041();*') {
    $m = [regex]::Match($mainText, '(?m)^(\s*)BuildUi\(\);\s*$')
    if (-not $m.Success) { throw 'Chamada BuildUi() nao encontrada no construtor.' }
    $indent = $m.Groups[1].Value
    $replacement = $m.Value.TrimEnd() + "`r`n" + $indent + 'ApplyHub041();'
    $mainText = $mainText.Remove($m.Index, $m.Length).Insert($m.Index, $replacement)
}

$hubSource = Join-Path $PSScriptRoot 'MainForm.Hub041.cs'
if (-not (Test-Path $hubSource)) { throw 'MainForm.Hub041.cs nao encontrado ao lado do patch.' }
$hubTarget = Join-Path $main.Directory.FullName 'MainForm.Hub041.cs'
Copy-Item $hubSource $hubTarget -Force

# Projetos antigos precisam declarar o novo arquivo. SDK-style inclui *.cs automaticamente.
if ($projectText -notmatch '<Project\s+Sdk=' -and $projectText -notmatch 'MainForm\.Hub041\.cs') {
    $compileGroup = @"
  <ItemGroup>
    <Compile Include="MainForm.Hub041.cs" />
  </ItemGroup>
"@
    if ($projectText -notmatch '</Project>') { throw 'Fim do csproj nao encontrado.' }
    $projectText = $projectText -replace '</Project>', ($compileGroup + "`r`n</Project>")
}

# Radio/TV GAT: uma unica selecao de fonte para Radio, DASH e overlay.
$radioText = $radioText.Replace('Radio-1.0.40', 'Radio-1.0.41').Replace('/index.html?v=140', '/index.html?v=141')

if ($radioText -notlike '*SharedMediaModeFile041*') {
    $refreshMarker = '    private async Task RefreshRadioAsync(bool force)'
    $idx = $radioText.IndexOf($refreshMarker)
    if ($idx -lt 0) { throw 'RefreshRadioAsync nao encontrado para sincronizar Radio GAT.' }
    $sharedMethods = @'
    private static string SharedMediaModeFile041()
    {
        return Path.Combine(Application.LocalUserAppDataPath, "radio-active-mode.txt");
    }

    private static void SaveSharedMediaMode041(string mode)
    {
        try
        {
            mode = (mode ?? string.Empty).Trim().ToLowerInvariant();
            if (mode != "gat" && mode != "mine" && mode != "web") return;
            File.WriteAllText(SharedMediaModeFile041(), mode);
        }
        catch { }
    }

    private static string ReadSharedMediaMode041()
    {
        try
        {
            string file = SharedMediaModeFile041();
            if (!File.Exists(file)) return "gat";
            string mode = (File.ReadAllText(file) ?? string.Empty).Trim().ToLowerInvariant();
            return mode == "mine" || mode == "web" ? mode : "gat";
        }
        catch { return "gat"; }
    }

    private async Task SyncSharedMediaMode041()
    {
        string mode = ReadSharedMediaMode041();
        if (mode == "web")
        {
            if (!_webMode) await SwitchWebModeAsync();
            return;
        }
        if (mode == "mine")
        {
            if (!_personalMode || _webMode) await SwitchModeAsync(true);
            return;
        }
        if (_personalMode || _webMode) await SwitchModeAsync(false);
    }

'@
    $radioText = $radioText.Insert($idx, $sharedMethods)

    # Inicializa a pagina da Radio no mesmo modo compartilhado.
    $loadWeb = '        LoadSavedWebUrl();'
    if ($radioText.Contains($loadWeb)) {
        $modeInit = @'
        LoadSavedWebUrl();
        string sharedMode041 = ReadSharedMediaMode041();
        _personalMode = sharedMode041 == "mine";
        _webMode = sharedMode041 == "web";
        if (_personalMode) _personalInput.Text = _personalSourceUrl;
        else if (_webMode) _personalInput.Text = _webUrl;
'@
        $radioText = $radioText.Replace($loadWeb, $modeInit.TrimEnd())
    }

    # Canal GAT / Meu Video.
    $switchStart = $radioText.IndexOf('    private async Task SwitchModeAsync(bool personal)')
    if ($switchStart -lt 0) { throw 'SwitchModeAsync nao encontrado.' }
    $switchEnd = $radioText.IndexOf('    private async Task LoadPersonalFromInputAsync()', $switchStart)
    if ($switchEnd -lt 0) { $switchEnd = [Math]::Min($radioText.Length, $switchStart + 6000) }
    $switchBlock = $radioText.Substring($switchStart, $switchEnd - $switchStart)
    $switchBlock = $switchBlock.Replace('_personalMode = personal;', '_personalMode = personal;' + "`r`n        SaveSharedMediaMode041(personal ? `"mine`" : `"gat`");")
    $radioText = $radioText.Substring(0, $switchStart) + $switchBlock + $radioText.Substring($switchEnd)

    # Canal Web. Tambem cobre LoadWebFromInputAsync quando define _webMode=true.
    $radioText = $radioText.Replace('_webMode = true;', '_webMode = true;' + "`r`n        SaveSharedMediaMode041(`"web`");")

    # A cada polling da Radio, aceita mudancas feitas pelo DASH/overlay.
    $refreshStart = $radioText.IndexOf('    private async Task RefreshRadioAsync(bool force)')
    $brace = $radioText.IndexOf('{', $refreshStart)
    if ($brace -lt 0) { throw 'Corpo RefreshRadioAsync nao encontrado.' }
    $radioText = $radioText.Insert($brace + 1, "`r`n        await SyncSharedMediaMode041();")
}

# Ponte de midia informa tambem qual fonte esta ativa.
if ($bridgeText -notlike '*activeMode*') {
    $payloadMarker = '["ok"] = true,'
    $payloadIndex = $bridgeText.IndexOf($payloadMarker)
    if ($payloadIndex -lt 0) { throw 'Payload da ponte DASH nao encontrado.' }
    $lineEnd = $bridgeText.IndexOf("`n", $payloadIndex)
    $bridgeText = $bridgeText.Insert($lineEnd + 1, '                            ["activeMode"] = ReadMode041(),' + "`r`n")

    $readMarker = '        private static string ReadLocal(string name)'
    $readIndex = $bridgeText.IndexOf($readMarker)
    if ($readIndex -lt 0) { throw 'ReadLocal da ponte DASH nao encontrado.' }
    $modeMethod = @'
        private static string ReadMode041()
        {
            try
            {
                string path = Path.Combine(Application.LocalUserAppDataPath, "radio-active-mode.txt");
                if (!File.Exists(path)) return "gat";
                string mode = (File.ReadAllText(path) ?? string.Empty).Trim().ToLowerInvariant();
                return mode == "mine" || mode == "web" ? mode : "gat";
            }
            catch { return "gat"; }
        }

'@
    $bridgeText = $bridgeText.Insert($readIndex, $modeMethod)
}

foreach ($marker in @(
    'CurrentVersion = "1.0.41.0"',
    'internal sealed partial class MainForm',
    'ApplyHub041();'
)) {
    if ($mainText -notlike "*$marker*") { throw "MainForm 1.0.41 sem marcador: $marker" }
}
foreach ($marker in @('SharedMediaModeFile041','SyncSharedMediaMode041','radio-active-mode.txt','Radio-1.0.41')) {
    if ($radioText -notlike "*$marker*") { throw "Radio 1.0.41 sem marcador: $marker" }
}
foreach ($marker in @('activeMode','ReadMode041','radio-active-mode.txt')) {
    if ($bridgeText -notlike "*$marker*") { throw "Ponte 1.0.41 sem marcador: $marker" }
}

Set-Content $main.FullName $mainText -Encoding UTF8
Set-Content $radio.FullName $radioText -Encoding UTF8
Set-Content $bridge.FullName $bridgeText -Encoding UTF8
Set-Content $project.FullName $projectText -Encoding UTF8
Write-Host 'GAT Telemetria 1.0.41: Hub modular + DASH interno + Radio compartilhada + overlays aplicado.'
