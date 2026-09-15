param(
    [Parameter(Mandatory=$true)]
    [string]$Root
)

$ErrorActionPreference = 'Stop'
$rootPath = (Resolve-Path $Root).Path
$main = Get-ChildItem $rootPath -Filter 'MainForm.cs' -Recurse | Select-Object -First 1
$hub041 = Get-ChildItem $rootPath -Filter 'MainForm.Hub041.cs' -Recurse | Select-Object -First 1
$radio = Get-ChildItem $rootPath -Filter 'RadioForm.cs' -Recurse | Select-Object -First 1
$project = Get-ChildItem $rootPath -Filter 'GAT_TELEMETRIA.csproj' -Recurse | Select-Object -First 1
if (-not $main -or -not $hub041 -or -not $radio -or -not $project) { throw 'Fonte Hub 1.0.41 incompleto para aplicar correcoes 1.0.42.' }

$mainText = Get-Content $main.FullName -Raw
$hubText = Get-Content $hub041.FullName -Raw
$radioText = Get-Content $radio.FullName -Raw
$projectText = Get-Content $project.FullName -Raw

# Versao do cliente.
$mainText = $mainText.Replace('CurrentVersion = "1.0.41.0"', 'CurrentVersion = "1.0.42.0"')
$mainText = $mainText.Replace('Text = "Cliente 1.0.41"', 'Text = "Cliente 1.0.42"')
if ($mainText -notmatch 'CurrentVersion = "1\.0\.42\.0"') { throw 'Nao consegui atualizar CurrentVersion para 1.0.42.0.' }

# Mantem o Hub 1.0.41 como base e aplica a camada visual/funcional 1.0.42 logo depois.
if ($mainText -notlike '*ApplyHub042();*') {
    $needle = 'ApplyHub041();'
    $idx = $mainText.IndexOf($needle)
    if ($idx -lt 0) { throw 'ApplyHub041() nao encontrado.' }
    $mainText = $mainText.Insert($idx + $needle.Length, "`r`n`t`tApplyHub042();")
}

$hub042Source = Join-Path $PSScriptRoot 'MainForm.Hub042.cs'
if (-not (Test-Path $hub042Source)) { throw 'MainForm.Hub042.cs nao encontrado.' }
$hub042Target = Join-Path $main.Directory.FullName 'MainForm.Hub042.cs'
Copy-Item $hub042Source $hub042Target -Force

if ($projectText -notmatch '<Project\s+Sdk=' -and $projectText -notmatch 'MainForm\.Hub042\.cs') {
    $compileGroup = @"
  <ItemGroup>
    <Compile Include="MainForm.Hub042.cs" />
  </ItemGroup>
"@
    if ($projectText -notmatch '</Project>') { throw 'Fim do csproj nao encontrado.' }
    $projectText = $projectText -replace '</Project>', ($compileGroup + "`r`n</Project>")
}

# Corrige textos remanescentes do shell anterior e sincroniza a selecao do DASH imediatamente com a Radio.
$hubText = $hubText.Replace('GAT Telemetria BETA 1.0.41', 'GAT Telemetria BETA 1.0.42')
$hubText = $hubText.Replace('Cliente 1.0.41 TESTE', 'Cliente 1.0.42 TESTE')
$oldMode = 'else if (type == "mediaMode") { SaveMediaMode041(Convert.ToString(m["mode"])); await DashMedia041(); }'
$newMode = 'else if (type == "mediaMode") { SaveMediaMode041(Convert.ToString(m["mode"])); try { _hubRadio041?.HubSyncMode042(); } catch { } await DashMedia041(); }'
if ($hubText.Contains($oldMode)) { $hubText = $hubText.Replace($oldMode, $newMode) }
elseif ($hubText -notlike '*HubSyncMode042*') { throw 'Handler mediaMode do DASH nao encontrado.' }

# Radio/TV: expoe controle para o Hub pausar o player quando outra pagina/overlay assumir a midia.
$radioText = $radioText.Replace('Radio-1.0.41', 'Radio-1.0.42').Replace('/index.html?v=141', '/index.html?v=142')
if ($radioText -notlike '*HubPause042*') {
    $marker = '    private async Task RefreshRadioAsync(bool force)'
    $idx = $radioText.IndexOf($marker)
    if ($idx -lt 0) { throw 'RefreshRadioAsync nao encontrado para inserir integracao 1.0.42.' }
    $methods = @'
    internal string HubNowPlaying042
    {
        get
        {
            try { return _track == null ? string.Empty : (_track.Text ?? string.Empty); }
            catch { return string.Empty; }
        }
    }

    internal async void HubPause042()
    {
        try
        {
            _listening = false;
            _toggle.Text = "OUVIR RÁDIO";
            if (_webMode && _web.CoreWebView2 != null)
            {
                try { await _web.CoreWebView2.ExecuteScriptAsync("document.querySelectorAll('video,audio').forEach(function(x){try{x.pause()}catch(e){}})"); } catch { }
            }
            else
            {
                try { await ExecutePlayerAsync("gatPause()"); } catch { }
            }
            UpdateActiveSourceUi();
        }
        catch { }
    }

    internal async void HubSyncMode042()
    {
        try { await SyncSharedMediaMode041(); }
        catch { }
    }

'@
    $radioText = $radioText.Insert($idx, $methods)
}

foreach ($m in @('CurrentVersion = "1.0.42.0"','ApplyHub041();','ApplyHub042();')) {
    if ($mainText -notlike "*$m*") { throw "MainForm 1.0.42 sem $m" }
}
foreach ($m in @('HubPause042','HubSyncMode042','HubNowPlaying042','Radio-1.0.42')) {
    if ($radioText -notlike "*$m*") { throw "Radio 1.0.42 sem $m" }
}
foreach ($m in @('Cliente 1.0.42 TESTE','HubSyncMode042')) {
    if ($hubText -notlike "*$m*") { throw "Hub base 1.0.42 sem $m" }
}

Set-Content $main.FullName $mainText -Encoding UTF8
Set-Content $hub041.FullName $hubText -Encoding UTF8
Set-Content $radio.FullName $radioText -Encoding UTF8
Set-Content $project.FullName $projectText -Encoding UTF8
Write-Host 'GAT Telemetria 1.0.42: Home ampliada, DASH ajustado e player unico entre Radio/DASH/overlay aplicado.'
