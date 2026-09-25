param(
    [Parameter(Mandatory=$true)]
    [string]$Root
)

$ErrorActionPreference = 'Stop'
$rootPath = (Resolve-Path $Root).Path
$main = Get-ChildItem $rootPath -Filter 'MainForm.cs' -Recurse | Select-Object -First 1
$hub = Get-ChildItem $rootPath -Filter 'MainForm.Hub041.cs' -Recurse | Select-Object -First 1
$radio = Get-ChildItem $rootPath -Filter 'RadioForm.cs' -Recurse | Select-Object -First 1
$project = Get-ChildItem $rootPath -Filter 'GAT_TELEMETRIA.csproj' -Recurse | Select-Object -First 1
if (-not $main -or -not $hub -or -not $radio -or -not $project) { throw 'Fonte 1.0.68.8 incompleta para aplicar 1.0.68.9 LIVE.' }

$mainText = Get-Content $main.FullName -Raw
$hubText = Get-Content $hub.FullName -Raw
$radioText = Get-Content $radio.FullName -Raw
$projectText = Get-Content $project.FullName -Raw
$nl = [Environment]::NewLine

$mainText = $mainText.Replace('CurrentVersion = "1.0.68.8"', 'CurrentVersion = "1.0.68.9"')
$mainText = $mainText.Replace('Text = "Cliente 1.0.68.8"', 'Text = "Cliente 1.0.68.9"')
$mainText = $mainText.Replace('HUB 1.0.68.8:', 'HUB 1.0.68.9:')
$hubText = $hubText.Replace('GAT Telemetria BETA 1.0.68.8', 'GAT Telemetria BETA 1.0.68.9')
$hubText = $hubText.Replace('Cliente 1.0.68.8 VOZ NOVA LIMPA', 'Cliente 1.0.68.9 RADIO + LIVE TESTE')

$liveSource = Join-Path $PSScriptRoot 'LiveMedia1689.cs'
if (-not (Test-Path $liveSource)) { throw 'LiveMedia1689.cs nao encontrado.' }
$liveTarget = Join-Path $main.Directory.FullName 'LiveMedia1689.cs'
Copy-Item $liveSource $liveTarget -Force

if ($projectText -notmatch '<Project\s+Sdk=' -and $projectText -notmatch 'LiveMedia1689\.cs') {
    $group = @'
  <ItemGroup>
    <Compile Include="LiveMedia1689.cs" />
  </ItemGroup>
'@
    $projectText = $projectText -replace '</Project>', ($group + $nl + '</Project>')
}

$homeStart = $hubText.IndexOf('    private Panel Home041()')
$homeEnd = $hubText.IndexOf('    private Label StatusCard041(', $homeStart)
if ($homeStart -lt 0 -or $homeEnd -lt 0) { throw 'Home041 nao encontrado.' }
$home = $hubText.Substring($homeStart, $homeEnd - $homeStart)
$home = [regex]::Replace($home, '(?s)\s*p\.Controls\.Add\(new Label \{ Left = 0, Top = 445,.*?\}\);', '')
if ($home -notlike '*BuildLiveHome1689(p);*') {
    $returnIndex = $home.LastIndexOf('        return p;')
    if ($returnIndex -lt 0) { throw 'return p da Home041 nao encontrado.' }
    $home = $home.Insert($returnIndex, '        BuildLiveHome1689(p);' + $nl)
}
$hubText = $hubText.Substring(0, $homeStart) + $home + $hubText.Substring($homeEnd)

if ($hubText -notlike '*await LiveMediaShown1689();*') {
    $shownNeedle = 'Shown += async delegate { SyncHub041(); await InitDash041(); };'
    if ($hubText.Contains($shownNeedle)) {
        $hubText = $hubText.Replace($shownNeedle, 'Shown += async delegate { SyncHub041(); await InitDash041(); await LiveMediaShown1689(); };')
    } else {
        $hubText = $hubText.Replace('await InitDash041();', 'await InitDash041(); await LiveMediaShown1689();')
    }
}
if ($hubText -notlike '*DisposeLiveMedia1689();*') {
    $disposeNeedle = 'try { _hubRadio041?.Dispose(); _hubDash041?.Dispose(); _hubHttp041.Dispose(); } catch { }'
    if (-not $hubText.Contains($disposeNeedle)) { throw 'Dispose do Hub nao encontrado.' }
    $hubText = $hubText.Replace($disposeNeedle, 'try { DisposeLiveMedia1689(); } catch { }' + $nl + '            ' + $disposeNeedle)
}

$loadVolumeMarker = '    private static int LoadVolume()'
if (-not $radioText.Contains($loadVolumeMarker)) { throw 'LoadVolume da Radio nao encontrado.' }
if ($radioText -notlike '*StartLiveAuto1689*') {
$radioMethods = @'
    internal int ExternalVolume1689 => _volume.Value;
    internal string LiveState1689 => _state.Text ?? string.Empty;

    internal async Task StartLiveAuto1689()
    {
        _personalMode = false;
        _webMode = false;
        _listening = true;
        _toggle.Text = "PARAR RÁDIO";
        SaveSharedMediaMode041("gat");
        ApplyModeUi();
        await RefreshRadioAsync(true);
        if (_playerReady && ActiveAvailable()) await LoadActiveSourceAsync();
    }

    internal async Task SetExternalVolume1689(int value)
    {
        value = Math.Max(0, Math.Min(100, value));
        _volume.Value = value;
        SaveVolume(value);
        if (_playerReady) await ExecutePlayerAsync("gatVolume(" + value + ")");
    }

    internal void OpenOverlay1689()
    {
        if (!_overlayMode) ToggleOverlayMode();
        try { Show(); Activate(); BringToFront(); } catch { }
    }

'@
    $radioText = $radioText.Replace($loadVolumeMarker, $radioMethods + $loadVolumeMarker)
}

$radioText = $radioText.Replace('_toggle.Visible = true;', '_toggle.Visible = _personalMode;')
$radioText = $radioText.Replace('Canal GAT: AO VIVO • clique em OUVIR RÁDIO', 'Canal GAT: AO VIVO')
$radioText = $radioText.Replace('Canal GAT: AO VIVO • pausada neste PC', 'Canal GAT: AO VIVO')
$radioText = $radioText.Replace(
    'CANAL GAT toca para todos os motoristas. Admin/Moderador define a programação aqui no Telemetria.',
    'CANAL GAT • transmissão contínua para todos. Somente ADM/Moderador altera a programação; motorista controla apenas o volume.'
)

if ($mainText -notlike '*CurrentVersion = "1.0.68.9"*') { throw 'MainForm nao ficou em 1.0.68.9.' }
foreach ($m in @('BuildLiveHome1689(p);','LiveMediaShown1689','DisposeLiveMedia1689')) {
    if ($hubText -notlike "*$m*") { throw "Hub 1.0.68.9 sem $m" }
}
foreach ($m in @('StartLiveAuto1689','SetExternalVolume1689','OpenOverlay1689','_toggle.Visible = _personalMode')) {
    if ($radioText -notlike "*$m*") { throw "Radio 1.0.68.9 sem $m" }
}
if ($projectText -notmatch '<Project\s+Sdk=' -and $projectText -notlike '*LiveMedia1689.cs*') {
    throw 'Projeto nao incluiu LiveMedia1689.cs.'
}

Set-Content $main.FullName $mainText -Encoding UTF8
Set-Content $hub.FullName $hubText -Encoding UTF8
Set-Content $radio.FullName $radioText -Encoding UTF8
Set-Content $project.FullName $projectText -Encoding UTF8

Write-Host 'GAT Telemetria 1.0.68.9 TESTE: Radio automatica, locucao WebRTC e transmissao opcional de rota adicionadas sobre a VOZ NOVA LIMPA.'
