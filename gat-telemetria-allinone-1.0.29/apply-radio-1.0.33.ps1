param(
    [Parameter(Mandatory=$true)]
    [string]$Root
)

$ErrorActionPreference = 'Stop'
$rootPath = (Resolve-Path $Root).Path
$main = Get-ChildItem $rootPath -Filter 'MainForm.cs' -Recurse | Select-Object -First 1
$project = Get-ChildItem $rootPath -Filter 'GAT_TELEMETRIA.csproj' -Recurse | Select-Object -First 1
if (-not $main) { throw 'MainForm.cs nao encontrado para aplicar Radio GAT.' }
if (-not $project) { throw 'GAT_TELEMETRIA.csproj nao encontrado para aplicar Radio GAT.' }

$mainText = Get-Content $main.FullName -Raw
$projectText = Get-Content $project.FullName -Raw

# Dependencia oficial para incorporar o player web do YouTube dentro do app WinForms.
if ($projectText -notmatch 'Microsoft\.Web\.WebView2') {
    $package = @"
  <ItemGroup>
    <PackageReference Include="Microsoft.Web.WebView2" Version="1.0.2792.45" />
  </ItemGroup>
"@
    if ($projectText -notmatch '</Project>') { throw 'Fim do csproj nao encontrado.' }
    $projectText = $projectText -replace '</Project>', ($package + "`r`n</Project>")
    Set-Content $project.FullName $projectText -Encoding UTF8
}

# O formulario da radio e mantido fora do fonte compactado para ser facil de evoluir.
$radioSource = Join-Path $PSScriptRoot 'RadioForm.cs'
if (-not (Test-Path $radioSource)) { throw 'RadioForm.cs nao encontrado ao lado do patch.' }
Copy-Item $radioSource (Join-Path $main.Directory.FullName 'RadioForm.cs') -Force

# Atualiza a versao interna sem depender do hotfix exato 1.0.32.x usado como base.
$mainText = [regex]::Replace(
    $mainText,
    'private const string CurrentVersion = "1\.0\.32(?:\.\d+)?";',
    'private const string CurrentVersion = "1.0.33.0";',
    1
)
if ($mainText -notmatch 'CurrentVersion = "1\.0\.33\.0"') { throw 'Nao consegui atualizar CurrentVersion para 1.0.33.0.' }

$mainText = $mainText.Replace('Text = "Cliente 1.0.32",', 'Text = "Cliente 1.0.33",')
if ($mainText -notmatch 'Text = "Cliente 1\.0\.33"') { throw 'Nao consegui atualizar o rotulo de versao do cliente.' }

if ($mainText -notmatch 'RÁDIO GAT') {
    $anchor = 'btnUpdate = MakeButton("↻  VERIFICAR ATUALIZAÇÃO", 24, 654, 240, 36, async delegate'
    if (-not $mainText.Contains($anchor)) { throw 'Botao de atualizacao nao encontrado para inserir Radio GAT.' }

    $radioButton = @"
		RadioForm radioForm = null;
		Button btnRadio = MakeButton("RÁDIO GAT", 278, 654, 190, 36, delegate
		{
			if (radioForm == null || radioForm.IsDisposed)
			{
				radioForm = new RadioForm();
			}
			if (!radioForm.Visible)
			{
				radioForm.Show(this);
			}
			radioForm.WindowState = FormWindowState.Normal;
			radioForm.BringToFront();
		});
		btnRadio.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
		Controls.Add(btnRadio);

		
"@
    $mainText = $mainText.Replace($anchor, $radioButton + $anchor)
}

foreach ($marker in @('CurrentVersion = "1.0.33.0"','Text = "Cliente 1.0.33"','RÁDIO GAT','new RadioForm()')) {
    if ($mainText -notlike "*$marker*") { throw "Patch Radio GAT incompleto no MainForm: $marker" }
}

Set-Content $main.FullName $mainText -Encoding UTF8
Write-Host 'GAT Telemetria 1.0.33: Radio GAT aplicada com player YouTube, opt-in do motorista e volume local.'
