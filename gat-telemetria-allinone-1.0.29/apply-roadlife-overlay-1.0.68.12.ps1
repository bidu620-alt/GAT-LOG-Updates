param(
  [Parameter(Mandatory=$true)]
  [string]$Root
)

$ErrorActionPreference='Stop'
$rootPath=(Resolve-Path $Root).Path
$main=Get-ChildItem $rootPath -Filter 'MainForm.cs' -Recurse | Select-Object -First 1
$hub=Get-ChildItem $rootPath -Filter 'MainForm.Hub041.cs' -Recurse | Select-Object -First 1
$project=Get-ChildItem $rootPath -Filter 'GAT_TELEMETRIA.csproj' -Recurse | Select-Object -First 1

if(-not $main -or -not $hub -or -not $project){
  throw 'Fonte 1.0.68.9 incompleta para adicionar RoadLife Overlay com carga dinamica.'
}

$mainText=Get-Content $main.FullName -Raw
$hubText=Get-Content $hub.FullName -Raw
$projectText=Get-Content $project.FullName -Raw

# Apenas identifica a build de teste. Nenhuma funcao existente do GAT e removida.
$mainText=$mainText.Replace('CurrentVersion = "1.0.68.9"','CurrentVersion = "1.0.68.12"')
$mainText=$mainText.Replace('Text = "Cliente 1.0.68.9"','Text = "Cliente 1.0.68.12"')
$mainText=$mainText.Replace('HUB 1.0.68.9:','HUB 1.0.68.12:')

Get-ChildItem $rootPath -Filter '*.cs' -Recurse | ForEach-Object {
  $t=Get-Content $_.FullName -Raw
  $u=$t.Replace('GAT Telemetria BETA 1.0.68.9','GAT Telemetria BETA 1.0.68.12')
  $u=$u.Replace('Cliente 1.0.68.9 TESTE','Cliente 1.0.68.12 TESTE')
  $u=$u.Replace('Cliente: 1.0.68.9 TESTE','Cliente: 1.0.68.12 TESTE')
  $u=$u.Replace('Cliente 1.0.68.9','Cliente 1.0.68.12')
  if($u -ne $t){Set-Content $_.FullName $u -Encoding UTF8}
}
$hubText=Get-Content $hub.FullName -Raw

# Acrescenta somente o botao do RoadLife no GAT DASH.
$controlsOld='        info.Controls.Add(title); info.Controls.Add(desc); info.Controls.Add(open);'
$controlsNew=@'
        var roadlife = HubButton041("ROADLIFE DASHBOARD2", 245);
        roadlife.Left = 330; roadlife.Top = 182; roadlife.Height = 42;
        roadlife.Click += delegate { ToggleRoadLifeOverlay(); };
        info.Controls.Add(title); info.Controls.Add(desc); info.Controls.Add(open); info.Controls.Add(roadlife);
'@.TrimEnd()

if($hubText.Contains($controlsOld)){
  $hubText=$hubText.Replace($controlsOld,$controlsNew)
}elseif($hubText -notlike '*ROADLIFE DASHBOARD2*'){
  throw 'Area do botao GAT DASH nao encontrada.'
}

# Inicializa somente o modulo isolado do overlay.
$resume='        ResumeLayout(true);'
if($hubText.Contains($resume) -and $hubText -notlike '*RoadLifeOverlayInitialize();*'){
  $hubText=$hubText.Replace($resume,'        RoadLifeOverlayInitialize();'+[Environment]::NewLine+$resume)
}

# O RoadLife recebe o MESMO JObject que o GAT ja capturou.
$needle1='UpdateTelemetryDisplay(TelemetryEngine.BuildDisplay(identityTelemetry));'
if($mainText.Contains($needle1) -and $mainText -notlike '*RoadLifeOverlayPush(identityTelemetry);*'){
  $mainText=$mainText.Replace($needle1,$needle1+[Environment]::NewLine+'			RoadLifeOverlayPush(identityTelemetry);')
}

$needle2='UpdateTelemetryDisplay(TelemetryEngine.BuildDisplay(tele));'
if($mainText.Contains($needle2) -and $mainText -notlike '*RoadLifeOverlayPush(tele);*'){
  $mainText=$mainText.Replace($needle2,$needle2+[Environment]::NewLine+'		RoadLifeOverlayPush(tele);')
}

if($mainText -notlike '*RoadLifeOverlayPush(identityTelemetry);*' -and $mainText -notlike '*RoadLifeOverlayPush(tele);*'){
  throw 'Ponto de telemetria do GAT nao encontrado.'
}

# Copia a classe isolada que hospeda o XAML dentro do MESMO processo do GAT.
$template=Join-Path $PSScriptRoot 'RoadLifeOverlayHost16812.cs'
if(-not(Test-Path $template)){throw 'RoadLifeOverlayHost16812.cs ausente.'}
$sourceDir=Split-Path $main.FullName -Parent
$overlayHostFile=Join-Path $sourceDir 'RoadLifeOverlayHost.cs'
Copy-Item $template $overlayHostFile -Force

# Referencias WPF necessarias apenas para a janela do overlay.
if($projectText -notmatch 'PresentationFramework'){
  $refs=@'
  <ItemGroup>
    <Reference Include="PresentationFramework" />
    <Reference Include="PresentationCore" />
    <Reference Include="WindowsBase" />
    <Reference Include="System.Xaml" />
  </ItemGroup>
'@
  $projectText=$projectText -replace '</Project>',($refs+[Environment]::NewLine+'</Project>')
}

# Projetos antigos podem exigir Compile explicito.
if($projectText -notmatch '<Project\s+Sdk=' -and $projectText -notmatch 'RoadLifeOverlayHost\.cs'){
  $compile=@'
  <ItemGroup>
    <Compile Include="RoadLifeOverlayHost.cs" />
  </ItemGroup>
'@
  $projectText=$projectText -replace '</Project>',($compile+[Environment]::NewLine+'</Project>')
}

Set-Content $main.FullName $mainText -Encoding UTF8
Set-Content $hub.FullName $hubText -Encoding UTF8
Set-Content $project.FullName $projectText -Encoding UTF8

$checkMain=Get-Content $main.FullName -Raw
$checkHub=Get-Content $hub.FullName -Raw
$checkHost=Get-Content $overlayHostFile -Raw

if($checkMain -notlike '*CurrentVersion = "1.0.68.12"*'){throw 'Versao 1.0.68.12 nao aplicada.'}
foreach($m in @('ROADLIFE DASHBOARD2','ToggleRoadLifeOverlay','RoadLifeOverlayInitialize')){
  if($checkHub -notlike "*$m*"){throw "Hub sem RoadLife: $m"}
}
foreach($m in @('OpenRoadLifeOverlay','Dashboard2.xaml','RoadLifeOverlayPush','XamlReader.Load')){
  if($checkHost -notlike "*$m*"){throw "Host RoadLife incompleto: $m"}
}

Write-Host 'RoadLife Dashboard2 adicionado como overlay interno do GAT, com imagens automaticas de carga.'
