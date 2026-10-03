param(
    [Parameter(Mandatory=$true)]
    [string]$Root
)

$ErrorActionPreference = 'Stop'
$rootPath = (Resolve-Path $Root).Path
$main = Get-ChildItem $rootPath -Filter 'MainForm.cs' -Recurse | Select-Object -First 1
$hub = Get-ChildItem $rootPath -Filter 'MainForm.Hub041.cs' -Recurse | Select-Object -First 1
$voice = Get-ChildItem $rootPath -Filter 'VoiceClean.cs' -Recurse | Select-Object -First 1
$project = Get-ChildItem $rootPath -Filter 'GAT_TELEMETRIA.csproj' -Recurse | Select-Object -First 1
if (-not $main -or -not $hub -or -not $voice -or -not $project) {
    throw 'Fonte 1.0.68.9 incompleta para integrar RoadLife Dashboard2.'
}

$mainText = Get-Content $main.FullName -Raw
$hubText = Get-Content $hub.FullName -Raw
$voiceText = Get-Content $voice.FullName -Raw
$projectText = Get-Content $project.FullName -Raw

# Versao de teste dedicada. A 1.0.68.9 publicada permanece intacta.
$mainText = $mainText.Replace('CurrentVersion = "1.0.68.9"', 'CurrentVersion = "1.0.68.10"')
$mainText = $mainText.Replace('Text = "Cliente 1.0.68.9"', 'Text = "Cliente 1.0.68.10"')
$mainText = $mainText.Replace('HUB 1.0.68.9:', 'HUB 1.0.68.10:')

# Atualiza os textos visiveis do Hub.
Get-ChildItem $rootPath -Filter '*.cs' -Recurse | ForEach-Object {
    $t = Get-Content $_.FullName -Raw
    $u = $t.Replace('GAT Telemetria BETA 1.0.68.9', 'GAT Telemetria BETA 1.0.68.10')
    $u = $u.Replace('Cliente 1.0.68.9 TESTE', 'Cliente 1.0.68.10 TESTE')
    $u = $u.Replace('Cliente: 1.0.68.9 TESTE', 'Cliente: 1.0.68.10 TESTE')
    $u = $u.Replace('Cliente 1.0.68.9', 'Cliente 1.0.68.10')
    if ($u -ne $t) { Set-Content $_.FullName $u -Encoding UTF8 }
}
$hubText = Get-Content $hub.FullName -Raw

# Botao RoadLife dentro da pagina GAT DASH.
$controlsOld = '        info.Controls.Add(title); info.Controls.Add(desc); info.Controls.Add(open);'
$controlsNew = @'
        var roadlife = HubButton041("ROADLIFE DASHBOARD2", 245);
        roadlife.Left = 330; roadlife.Top = 182; roadlife.Height = 42;
        roadlife.Click += delegate { ToggleRoadLifeDashboard2(); };
        info.Controls.Add(title); info.Controls.Add(desc); info.Controls.Add(open); info.Controls.Add(roadlife);
'@.TrimEnd()
if ($hubText.Contains($controlsOld)) {
    $hubText = $hubText.Replace($controlsOld, $controlsNew)
} elseif ($hubText -notlike '*ROADLIFE DASHBOARD2*') {
    throw 'Area de botoes da pagina GAT DASH nao encontrada.'
}

# Inicializa/encerra a ponte junto com o GAT.
$resume = '        ResumeLayout(true);'
if ($hubText.Contains($resume) -and $hubText -notlike '*RoadLifeDashboard2Initialize();*') {
    $hubText = $hubText.Replace($resume, '        RoadLifeDashboard2Initialize();' + [Environment]::NewLine + $resume)
}

# A telemetria que ja foi captada pelo GAT e enviada ao visualizador.
$voiceNeedle = '            JObject tele = JObject.Parse(raw);'
if ($voiceText.Contains($voiceNeedle) -and $voiceText -notlike '*RoadLifeDashboard2Push(tele);*') {
    $voiceText = $voiceText.Replace(
        $voiceNeedle,
        $voiceNeedle + [Environment]::NewLine + '            RoadLifeDashboard2Push(tele);'
    )
} elseif ($voiceText -notlike '*RoadLifeDashboard2Push(tele);*') {
    throw 'Ponto de captura do VoiceClean nao encontrado.'
}

# Copia a classe ponte versionada para a fonte reconstruida.
$template = Join-Path $PSScriptRoot 'RoadLifeDashboard2Bridge16810.cs'
if (-not (Test-Path $template)) { throw 'RoadLifeDashboard2Bridge16810.cs ausente.' }
$sourceDir = Split-Path $main.FullName -Parent
$bridge = Join-Path $sourceDir 'RoadLifeDashboard2Bridge.cs'
Copy-Item $template $bridge -Force

# Projetos .NET Framework antigos listam cada .cs explicitamente.
if ($projectText -notmatch '<Project\s+Sdk=' -and $projectText -notmatch 'RoadLifeDashboard2Bridge\.cs') {
    $compile = @"
  <ItemGroup>
    <Compile Include="RoadLifeDashboard2Bridge.cs" />
  </ItemGroup>
"@
    if ($projectText -notmatch '</Project>') { throw 'Fim do csproj nao encontrado.' }
    $projectText = $projectText -replace '</Project>', ($compile + [Environment]::NewLine + '</Project>')
}

if ($mainText -notlike '*CurrentVersion = "1.0.68.10"*') { throw 'Versao 1.0.68.10 nao aplicada.' }
foreach ($m in @('ROADLIFE DASHBOARD2','ToggleRoadLifeDashboard2','RoadLifeDashboard2Initialize')) {
    if ($hubText -notlike "*$m*") { throw "Hub sem RoadLife: $m" }
}
if ($voiceText -notlike '*RoadLifeDashboard2Push(tele);*') {
    throw 'GAT nao esta encaminhando telemetria ao RoadLife.'
}

$bridgeText = Get-Content $bridge -Raw
foreach ($m in @('RoadLifeDashboard2Push','RoadLifeDashboard2BuildPayload','RoadLifeDashboard2Script','telemetry.json','RoadLifeGear')) {
    if ($bridgeText -notlike "*$m*") { throw "Bridge incompleta: $m" }
}

Set-Content $main.FullName $mainText -Encoding UTF8
Set-Content $hub.FullName $hubText -Encoding UTF8
Set-Content $voice.FullName $voiceText -Encoding UTF8
Set-Content $project.FullName $projectText -Encoding UTF8

Write-Host 'GAT Telemetria 1.0.68.10 TESTE: RoadLife Dashboard2 PS1 editavel integrado; GAT capta e o PS1 apenas visualiza.'
