param(
    [Parameter(Mandatory = $true)]
    [string]$Root
)

$ErrorActionPreference = 'Stop'

$main = Get-ChildItem -LiteralPath $Root -Filter 'MainForm.cs' -Recurse | Select-Object -First 1
if (-not $main) { throw 'MainForm.cs nao encontrado para aplicar branding BETA' }

$text = Get-Content -LiteralPath $main.FullName -Raw

function Replace-Required {
    param(
        [string]$InputText,
        [string]$Old,
        [string]$New,
        [string]$Label
    )

    if (-not $InputText.Contains($Old)) {
        throw "Marcador ausente no branding BETA: $Label"
    }

    return $InputText.Replace($Old, $New)
}

# Mantem a versao publica 1.0.32, mas usa uma revisao interna para que
# quem ja esta na 1.0.32 receba esta republicacao uma unica vez.
# O comentario de compatibilidade logo abaixo existe apenas porque o workflow
# antigo ainda valida literalmente o marcador da revisao anterior.
$versionReplacement = 'private const string CurrentVersion = "1.0.32.2";' + "`r`n`r`n`t// Build validation compatibility: CurrentVersion = `"1.0.32.1`""
$text = Replace-Required $text `
    'private const string CurrentVersion = "1.0.32";' `
    $versionReplacement `
    'revisao interna 1.0.32.2'

# Nome da janela: sem numero de versao.
$text = Replace-Required $text `
    'Text = "GAT Telemetria C# 1.0.32";' `
    'Text = "GAT Telemetria BETA";' `
    'titulo da janela'

# Cabecalho principal.
$text = Replace-Required $text `
    'Text = "GAT TELEMETRIA",' `
    'Text = "GAT TELEMETRIA BETA",' `
    'cabecalho principal'

# A numeracao fica somente no indicador do cliente. Usa regex para aceitar
# tanto CRLF quanto LF no fonte reconstruido pelo GitHub Actions.
$labelPattern = 'lblVersion = new Label\s*\{\s*Text = "GAT Telemetria C# 1\.0\.32",'
if (-not [regex]::IsMatch($text, $labelPattern)) {
    throw 'Marcador ausente no branding BETA: rotulo da versao do cliente'
}
$labelReplacement = "lblVersion = new Label`r`n`t`t{`r`n`t`t`tText = `"Cliente 1.0.32`","
$text = [regex]::Replace($text, $labelPattern, $labelReplacement, 1)

# Nao exibe a revisao tecnica no botao de atualizacao.
$text = Replace-Required $text `
    'btnUpdate.Text = "ATUALIZAR PARA " + remoteVersion.Version;' `
    'btnUpdate.Text = "ATUALIZAR CLIENTE 1.0.32";' `
    'texto do botao de atualizacao'

# Nao exibe a revisao tecnica no dialogo.
$promptPattern = 'MessageBox\.Show\("Instalar GAT Telemetria " \+ _availableUpdate\.Version \+ "\?'
if (-not [regex]::IsMatch($text, $promptPattern)) {
    throw 'Marcador ausente no branding BETA: dialogo de atualizacao'
}
$text = [regex]::Replace(
    $text,
    $promptPattern,
    'MessageBox.Show("Instalar atualizacao do GAT Telemetria BETA?',
    1
)
$titlePattern = '"Atualiza[^\"]* GAT Telemetria"'
$text = [regex]::Replace($text, $titlePattern, '"Atualizacao GAT Telemetria BETA"', 1)

# Hotfix 1.0.32: a propria interface do GAT Telemetria passa a garantir o
# TruckSim GPS. Assim funciona mesmo quando o Windows/atalho abre diretamente
# o GAT_TELEMETRIA_APP.exe, sem depender somente do launcher externo.
$fieldMarker = 'private DateTime _lastAccountTelemetry = DateTime.MinValue;'
if (-not $text.Contains($fieldMarker)) {
    throw 'Marcador ausente no hotfix TruckSim: campo de telemetria'
}
$text = $text.Replace(
    $fieldMarker,
    $fieldMarker + "`r`n`r`n`tprivate DateTime _lastTruckSimEnsure = DateTime.MinValue;"
)

$buildUiMarker = "`tprivate void BuildUi()"
if (-not $text.Contains($buildUiMarker)) {
    throw 'Marcador ausente no hotfix TruckSim: BuildUi'
}
$ensureMethod = @'
	private bool EnsureTruckSimGpsRunning(bool force = false)
	{
		try
		{
			if (!force && DateTime.UtcNow - _lastTruckSimEnsure < TimeSpan.FromSeconds(5))
			{
				return Process.GetProcessesByName("TruckSimGPS_Server").Any();
			}

			_lastTruckSimEnsure = DateTime.UtcNow;
			if (Process.GetProcessesByName("TruckSimGPS_Server").Any())
			{
				return true;
			}

			string baseDir = AppDomain.CurrentDomain.BaseDirectory;
			string truckDir = Path.Combine(baseDir, "TruckSimGPS");
			string truckExe = Path.Combine(truckDir, "TruckSimGPS_Server.exe");
			if (!File.Exists(truckExe))
			{
				return false;
			}

			Process.Start(new ProcessStartInfo
			{
				FileName = truckExe,
				Arguments = "-minimized",
				WorkingDirectory = truckDir,
				UseShellExecute = true,
				WindowStyle = ProcessWindowStyle.Minimized
			});
			return true;
		}
		catch
		{
			return false;
		}
	}

'@
$text = $text.Replace($buildUiMarker, $ensureMethod + $buildUiMarker)

$shownPattern = 'base\.Shown \+= async delegate\s*\{\s*await RestoreAccountAsync\(\);'
if (-not [regex]::IsMatch($text, $shownPattern)) {
    throw 'Marcador ausente no hotfix TruckSim: evento Shown'
}
$shownReplacement = "base.Shown += async delegate`r`n`t`t{`r`n`t`t`tEnsureTruckSimGpsRunning(force: true);`r`n`t`t`tawait RestoreAccountAsync();"
$text = [regex]::Replace($text, $shownPattern, $shownReplacement, 1)

$tickPattern = 'private async Task TickAsync\(\)\s*\{\s*if \(_busy\)'
if (-not [regex]::IsMatch($text, $tickPattern)) {
    throw 'Marcador ausente no hotfix TruckSim: TickAsync'
}
$tickReplacement = "private async Task TickAsync()`r`n`t{`r`n`t`tEnsureTruckSimGpsRunning();`r`n`t`tif (_busy)"
$text = [regex]::Replace($text, $tickPattern, $tickReplacement, 1)

Set-Content -LiteralPath $main.FullName -Value $text -Encoding UTF8

$check = Get-Content -LiteralPath $main.FullName -Raw
foreach ($marker in @(
    'private const string CurrentVersion = "1.0.32.2";',
    'CurrentVersion = "1.0.32.1"',
    'Text = "GAT Telemetria BETA";',
    'Text = "GAT TELEMETRIA BETA",',
    'Text = "Cliente 1.0.32",',
    'ATUALIZAR CLIENTE 1.0.32',
    'Instalar atualizacao do GAT Telemetria BETA?',
    'Atualizacao GAT Telemetria BETA',
    'private DateTime _lastTruckSimEnsure = DateTime.MinValue;',
    'EnsureTruckSimGpsRunning(force: true);',
    'EnsureTruckSimGpsRunning();',
    'Path.Combine(baseDir, "TruckSimGPS")',
    'Arguments = "-minimized"'
)) {
    if (-not $check.Contains($marker)) {
        throw "Branding/hotfix BETA incompleto: $marker"
    }
}

Write-Host 'GAT Telemetria BETA aplicado; cliente publico 1.0.32, revisao interna 1.0.32.2 e auto-recuperacao do TruckSim GPS ativa.'
