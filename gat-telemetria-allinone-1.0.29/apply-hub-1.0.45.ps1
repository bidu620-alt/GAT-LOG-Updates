param(
    [Parameter(Mandatory=$true)]
    [string]$Root
)

$ErrorActionPreference = 'Stop'
$rootPath = (Resolve-Path $Root).Path
$main = Get-ChildItem $rootPath -Filter 'MainForm.cs' -Recurse | Select-Object -First 1
$hub41 = Get-ChildItem $rootPath -Filter 'MainForm.Hub041.cs' -Recurse | Select-Object -First 1
$hub44 = Get-ChildItem $rootPath -Filter 'MainForm.Hub044.cs' -Recurse | Select-Object -First 1
$truck = Get-ChildItem $rootPath -Filter 'TruckOverlay041.cs' -Recurse | Select-Object -First 1
$project = Get-ChildItem $rootPath -Filter 'GAT_TELEMETRIA.csproj' -Recurse | Select-Object -First 1
if (-not $main -or -not $hub41 -or -not $hub44 -or -not $truck -or -not $project) {
    throw 'Fonte 1.0.44 incompleto para aplicar a atualizacao 1.0.45.'
}

$mainText = Get-Content $main.FullName -Raw
$hub41Text = Get-Content $hub41.FullName -Raw
$hub44Text = Get-Content $hub44.FullName -Raw
$projectText = Get-Content $project.FullName -Raw

$mainText = $mainText.Replace('CurrentVersion = "1.0.44.0"', 'CurrentVersion = "1.0.45.0"')
$mainText = $mainText.Replace('Text = "Cliente 1.0.44"', 'Text = "Cliente 1.0.45"')
$mainText = $mainText.Replace('HUB 1.0.44:', 'HUB 1.0.45:')

if ($mainText -notlike '*ApplyHub045();*') {
    $needle = 'ApplyHub044();'
    $idx = $mainText.IndexOf($needle)
    if ($idx -lt 0) { throw 'ApplyHub044() nao encontrado para encadear a 1.0.45.' }
    $mainText = $mainText.Insert($idx + $needle.Length, "`r`n            ApplyHub045();")
}

# O DASH Windows 1.0.44 devolvia sempre tolerancia=3 e descartava o que o usuario salvava.
# Na 1.0.45 o valor fica persistido em %LOCALAPPDATA%\GAT-LOG\GAT-Telemetria\gat-dash-settings.json.
$readyOld = 'if (type == "ready") { await DashJs041("window.gatDashNativeReady(''windows'',{host:'''',voice:true,tolerance:3})"); await DashSession041(); }'
$readyNew = 'if (type == "ready") { await DashJs041("window.gatDashNativeReady(''windows''," + DashSettingsJson045() + ")"); await DashSession041(); }'
if ($hub41Text.Contains($readyOld)) {
    $hub41Text = $hub41Text.Replace($readyOld, $readyNew)
} elseif ($hub41Text -notlike '*DashSettingsJson045()*') {
    throw 'Handler ready do GAT DASH nao encontrado para corrigir tolerancia.'
}

$settingsOld = 'else if (type == "setSettings") await DashJs041("window.gatDashSettings({host:'''',voice:true,tolerance:3})");'
$settingsNew = 'else if (type == "setSettings") { SaveDashSettings045(m); await DashJs041("window.gatDashSettings(" + DashSettingsJson045() + ")"); }'
if ($hub41Text.Contains($settingsOld)) {
    $hub41Text = $hub41Text.Replace($settingsOld, $settingsNew)
} elseif ($hub41Text -notlike '*SaveDashSettings045(m)*') {
    throw 'Handler setSettings do GAT DASH nao encontrado para corrigir tolerancia.'
}

# Atualiza referencias visuais ainda herdadas da 1.0.44.
$hub44Text = $hub44Text.Replace('GAT Telemetria BETA 1.0.44', 'GAT Telemetria BETA 1.0.45')
$hub44Text = $hub44Text.Replace('Cliente 1.0.44 TESTE', 'Cliente 1.0.45 TESTE')

$hub45Source = Join-Path $PSScriptRoot 'MainForm.Hub045.cs'
$truck45Source = Join-Path $PSScriptRoot 'TruckOverlay045.cs'
if (-not (Test-Path $hub45Source)) { throw 'MainForm.Hub045.cs nao encontrado.' }
if (-not (Test-Path $truck45Source)) { throw 'TruckOverlay045.cs nao encontrado.' }

$hub45Target = Join-Path $main.Directory.FullName 'MainForm.Hub045.cs'
Copy-Item $hub45Source $hub45Target -Force

# Usa var para manter compatibilidade caso PlayersResult esteja aninhado/renomeado no fonte-base.
$hub45Text = Get-Content $hub45Target -Raw
$hub45Text = $hub45Text.Replace('PlayersResult r = await _api.GetPlayersAsync(_endpoint);', 'var r = await _api.GetPlayersAsync(_endpoint);')
Set-Content $hub45Target $hub45Text -Encoding UTF8

# Substitui somente a classe de overlay pelo modelo completo 1.0.45, preservando o nome esperado pelo app.
Copy-Item $truck45Source $truck.FullName -Force

if ($projectText -notmatch '<Project\s+Sdk=' -and $projectText -notmatch 'MainForm\.Hub045\.cs') {
    $compileGroup = @"
  <ItemGroup>
    <Compile Include="MainForm.Hub045.cs" />
  </ItemGroup>
"@
    if ($projectText -notmatch '</Project>') { throw 'Fim do csproj nao encontrado.' }
    $projectText = $projectText -replace '</Project>', ($compileGroup + "`r`n</Project>")
}

foreach ($m in @('CurrentVersion = "1.0.45.0"','ApplyHub045();')) {
    if ($mainText -notlike "*$m*") { throw "MainForm 1.0.45 sem $m" }
}
foreach ($m in @('DashSettingsJson045()','SaveDashSettings045(m)')) {
    if ($hub41Text -notlike "*$m*") { throw "Persistencia da tolerancia 1.0.45 sem $m" }
}
foreach ($m in @('MOTORISTAS ONLINE EM ROTA','SISTEMA GAT','DataGridView','gat-dash-settings.json')) {
    if ($hub45Text -notlike "*$m*") { throw "Home 1.0.45 sem $m" }
}
$truckText = Get-Content $truck.FullName -Raw
foreach ($m in @('TOLERÂNCIA','TEMPO ESTIMADO','DANOS DO CAMINHÃO','DANOS DO REBOQUE','DANOS DA CARGA','WM_NCHITTEST_045')) {
    if ($truckText -notlike "*$m*") { throw "Overlay 1.0.45 sem $m" }
}

Set-Content $main.FullName $mainText -Encoding UTF8
Set-Content $hub41.FullName $hub41Text -Encoding UTF8
Set-Content $hub44.FullName $hub44Text -Encoding UTF8
Set-Content $project.FullName $projectText -Encoding UTF8

Write-Host 'GAT Telemetria 1.0.45: home com motoristas online, overlay expandido e tolerancia persistente aplicados.'