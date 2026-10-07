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

# A pagina Dashboard oferece somente os dois overlays ROADLIFE pedidos.
# O coletor e as demais paginas do GAT permanecem intactos.
$dashPattern='(?s)    private Panel Dash041\(\)\s*\{.*?\r?\n    \}\s*\r?\n    private Panel Radio041\(\)'
$dashReplacement=@'
    private Panel Dash041()
    {
        var p = Page041();
        p.Controls.Add(Head041("DASHBOARD", "ROADLIFE TELEMETRIA • escolha um dos overlays."));

        var tools = new Panel
        {
            Left = 0, Top = 62, Width = p.Width, Height = 58,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            BackColor = Color.FromArgb(5, 24, 17)
        };
        var dash = HubButton041("ROADLIFE DASH", 190);
        dash.Left = 10; dash.Top = 9; dash.Height = 40;
        dash.Click += delegate { ToggleRoadLifeOverlay(); };
        tools.Controls.Add(dash);

        var video = HubButton041("SOBREPOR VÍDEO", 175);
        video.Left = 212; video.Top = 9; video.Height = 40;
        video.Click += delegate { OpenVideoOverlay041(); };
        tools.Controls.Add(video);
        p.Controls.Add(tools);

        var info = new Label
        {
            Left = 12, Top = 145, Width = 760, Height = 62,
            Text = "RoadLife Dash mostra os dados recebidos pelo GAT Telemetria.\r\n" +
                   "A sobreposição de vídeo abre em uma janela flutuante.",
            ForeColor = Color.FromArgb(176, 205, 188),
            Font = new Font("Segoe UI", 10.5f)
        };
        p.Controls.Add(info);
        return p;
    }

    private Panel Radio041()
'@
if(-not [regex]::IsMatch($hubText,$dashPattern)){throw 'Pagina Dashboard original nao encontrada.'}
$hubText=[regex]::Replace($hubText,$dashPattern,$dashReplacement,1)

# Atualiza nomes exibidos, sem renomear protocolos, endpoints, classes ou campos internos.
$brandReplacements=@(
  @('GAT Telemetria BETA 1.0.68.12','ROADLIFE TELEMETRIA 1.0.68.12'),
  @('GAT LOG ETS2','ROADLIFE ETS2'),
  @('GAT TELEMETRIA BETA','ROADLIFE TELEMETRIA'),
  @('GAT DASH','ROADLIFE DASH'),
  @('RÁDIO GAT','RÁDIO ROADLIFE'),
  @('Rádio/TV GAT','Rádio/TV RoadLife'),
  @('Conta GAT','Conta RoadLife'),
  @('CONTA GAT','CONTA ROADLIFE'),
  @('Central GAT','Central RoadLife'),
  @('CENTRAL GAT','CENTRAL ROADLIFE'),
  @('GAT Telemetria','RoadLife Telemetria'),
  @('Text = "GAT"','Text = "ROADLIFE"'),
  @('Central principal do ecossistema GAT LOG ETS2','Central principal do ecossistema ROADLIFE ETS2'),
  @('A mesma Rádio/TV RoadLife usada pelo DASH','Rádio RoadLife integrada ao aplicativo'),
  @('Canal GAT','Canal RoadLife')
)
foreach($pair in $brandReplacements){$hubText=$hubText.Replace($pair[0],$pair[1])}
$hubText=$hubText.Replace('Text = "ROADLIFE", Left = 22, Top = 10, AutoSize = true, ForeColor = Color.White','Text = "ROADLIFE", Left = 22, Top = 10, AutoSize = true, ForeColor = Color.FromArgb(54, 242, 122)')
$hubText=$hubText.Replace('Color.FromArgb(8, 29, 50)','Color.FromArgb(7, 38, 26)')
$hubText=$hubText.Replace('Color.FromArgb(38, 88, 135)','Color.FromArgb(35, 119, 75)')
$hubText=$hubText.Replace('Color.FromArgb(14, 74, 133)','Color.FromArgb(24, 111, 65)')
$hubText=$hubText.Replace('Nav041(p, "dash", "ROADLIFE DASH", 120)','Nav041(p, "dash", "DASHBOARD", 120)')
$hubText=$hubText.Replace('Module041(mods, "ROADLIFE DASH", "Dashboard e overlays", "dash")','Module041(mods, "DASHBOARD", "RoadLife Dash e sobreposição de vídeo", "dash")')

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

# Adiciona o modulo de temas clicaveis sem alterar a telemetria.
$themeTemplate=Join-Path $PSScriptRoot 'RoadLifeTheme16812.cs'
if(-not(Test-Path $themeTemplate)){throw 'RoadLifeTheme16812.cs ausente.'}
$themeFile=Join-Path $sourceDir 'RoadLifeTheme16812.cs'
Copy-Item $themeTemplate $themeFile -Force

$hostText=Get-Content $overlayHostFile -Raw
$oldLoad=@'
            object loaded;
            using (FileStream fs = File.OpenRead(xamlPath))
                loaded = System.Windows.Markup.XamlReader.Load(fs);
'@
$newLoad=@'
            object loaded;
            string roadLifeXamlText = File.ReadAllText(xamlPath);
            roadLifeXamlText = RoadLifeThemePrepareXaml(roadLifeXamlText);
            using (var sr = new StringReader(roadLifeXamlText))
            using (var xr = System.Xml.XmlReader.Create(sr))
                loaded = System.Windows.Markup.XamlReader.Load(xr);
'@
if(-not $hostText.Contains($oldLoad)){throw 'Bloco XAML do RoadLife nao encontrado para tema.'}
$hostText=$hostText.Replace($oldLoad,$newLoad)
$hostText=$hostText.Replace('            LoadRoadLifeImage("StructureImage", Path.Combine(RoadLifeOverlayRoot(), "Assets", "dashboard-structure-green.png"));','            RoadLifeThemeWire();'+[Environment]::NewLine+'            LoadRoadLifeImage("StructureImage", Path.Combine(RoadLifeOverlayRoot(), "Assets", "dashboard-structure-green.png"));')
Set-Content $overlayHostFile $hostText -Encoding UTF8

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
    <Compile Include="RoadLifeTheme16812.cs" />
  </ItemGroup>
'@
  $projectText=$projectText -replace '</Project>',($compile+[Environment]::NewLine+'</Project>')
}elseif($projectText -notmatch '<Project\s+Sdk=' -and $projectText -notmatch 'RoadLifeTheme16812\.cs'){
  $projectText=$projectText.Replace('<Compile Include="RoadLifeOverlayHost.cs" />','<Compile Include="RoadLifeOverlayHost.cs" />'+[Environment]::NewLine+'    <Compile Include="RoadLifeTheme16812.cs" />')
}

Set-Content $main.FullName $mainText -Encoding UTF8
Set-Content $hub.FullName $hubText -Encoding UTF8
Set-Content $project.FullName $projectText -Encoding UTF8

# Rebranding visual do overlay de video; os modos e URLs da API GAT ficam iguais.
$videoFile=Get-ChildItem $rootPath -Filter 'VideoOverlay041.cs' -Recurse | Select-Object -First 1
if(-not $videoFile){throw 'VideoOverlay041.cs ausente.'}
$videoText=Get-Content $videoFile.FullName -Raw
foreach($pair in @(
  @('GAT DASH • Vídeo flutuante','ROADLIFE • Sobreposição de vídeo'),
  @('GAT • VÍDEO','ROADLIFE • VÍDEO'),
  @('Canal GAT','Canal RoadLife'),
  @('Rádio/TV GAT','Rádio/TV RoadLife'),
  @('Abra o GAT Telemetria','Abra o ROADLIFE TELEMETRIA'),
  @('#174b78','#17683e'),
  @('#55a5ff','#36f27a'),
  @('#245b8d','#28784a'),
  @('#09223a','#0a2b1b'),
  @('#124b7f','#17683e'),
  @('#bcd7f7','#c5ead2'),
  @('#78b8ff','#75e79b')
)){$videoText=$videoText.Replace($pair[0],$pair[1])}
Set-Content $videoFile.FullName $videoText -Encoding UTF8

# Troca apenas strings visiveis do nome do produto nos demais formularios.
Get-ChildItem $rootPath -Filter '*.cs' -Recurse | ForEach-Object {
  $t=Get-Content $_.FullName -Raw
  $u=$t
  foreach($pair in @(
    @('GAT Telemetria BETA 1.0.68.12','ROADLIFE TELEMETRIA 1.0.68.12'),
    @('GAT Telemetria','ROADLIFE TELEMETRIA'),
    @('GAT LOG ETS2','ROADLIFE ETS2'),
    @('GAT LOG','ROADLIFE'),
    @('GAT DASH','ROADLIFE DASH'),
    @('RÁDIO / TV GAT','RÁDIO / TV ROADLIFE'),
    @('Rádio / TV GAT','Rádio / TV RoadLife'),
    @('RÁDIO GAT','RÁDIO ROADLIFE'),
    @('Rádio GAT','Rádio RoadLife'),
    @('CANAL GAT','CANAL ROADLIFE'),
    @('Canal GAT','Canal RoadLife'),
    @('Conta GAT','Conta RoadLife'),
    @('CONTA GAT','CONTA ROADLIFE'),
    @('Central GAT','Central RoadLife'),
    @('CENTRAL GAT','CENTRAL ROADLIFE'),
    @('site GAT','site RoadLife'),
    @('Conectando ao GAT','Conectando à telemetria'),
    @('Conectado ao GAT','Conectado à telemetria'),
    @('VOLTAR AO GAT','VOLTAR')
  )){$u=$u.Replace($pair[0],$pair[1])}
  if($_.Name -eq 'MainForm.Hub042.cs'){$u=$u.Replace('Initials = "GAT"','Initials = "RDL"').Replace('? "GAT" :','? "RDL" :')}
  if($u -ne $t){Set-Content $_.FullName $u -Encoding UTF8}
}

$checkMain=Get-Content $main.FullName -Raw
$checkHub=Get-Content $hub.FullName -Raw
$checkHost=Get-Content $overlayHostFile -Raw

if($checkMain -notlike '*CurrentVersion = "1.0.68.12"*'){throw 'Versao 1.0.68.12 nao aplicada.'}
foreach($m in @('ROADLIFE DASH','ToggleRoadLifeOverlay','OpenVideoOverlay041','RoadLifeOverlayInitialize')){
  if($checkHub -notlike "*$m*"){throw "Hub sem RoadLife: $m"}
}
foreach($m in @('OpenRoadLifeOverlay','RoadLifeDash.xaml','RoadLifeOverlayPush','XamlReader.Load')){
  if($checkHost -notlike "*$m*"){throw "Host RoadLife incompleto: $m"}
}

Write-Host 'RoadLife Dashboard2 adicionado como overlay interno do GAT, com imagens automaticas de carga.'

