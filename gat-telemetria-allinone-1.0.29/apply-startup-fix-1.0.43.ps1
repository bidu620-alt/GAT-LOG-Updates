param(
    [Parameter(Mandatory=$true)]
    [string]$Root
)

$ErrorActionPreference = 'Stop'
$rootPath = (Resolve-Path $Root).Path
$main = Get-ChildItem $rootPath -Filter 'MainForm.cs' -Recurse | Select-Object -First 1
$hub42 = Get-ChildItem $rootPath -Filter 'MainForm.Hub042.cs' -Recurse | Select-Object -First 1
if (-not $main -or -not $hub42) { throw 'Fonte 1.0.42 incompleto para aplicar hotfix 1.0.43.' }

$mainText = Get-Content $main.FullName -Raw
$hubText = Get-Content $hub42.FullName -Raw

$mainText = $mainText.Replace('CurrentVersion = "1.0.42.0"', 'CurrentVersion = "1.0.43.0"')
$mainText = $mainText.Replace('Text = "Cliente 1.0.42"', 'Text = "Cliente 1.0.43"')

# A camada visual 1.0.42 nao pode impedir o cliente principal de abrir.
$mainText = $mainText.Replace("`t`tApplyHub042();", @'
        try
        {
            ApplyHub042();
        }
        catch (Exception ex)
        {
            try
            {
                string logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GAT-LOG", "GAT-Telemetria");
                Directory.CreateDirectory(logDir);
                File.AppendAllText(Path.Combine(logDir, "startup-error.log"), DateTime.Now.ToString("s") + " HUB 1.0.43: " + ex + Environment.NewLine);
            }
            catch { }
        }
'@.TrimEnd())

# DriverAvatar usa BackColor transparente: habilita suporte antes de definir Color.Transparent.
$oldStyle = 'SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);'
$newStyle = 'SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);'
if (-not $hubText.Contains($oldStyle)) { throw 'DriverAvatar042 SetStyle nao encontrado.' }
$hubText = $hubText.Replace($oldStyle, $newStyle)
$hubText = $hubText.Replace('GAT Telemetria BETA 1.0.42', 'GAT Telemetria BETA 1.0.43')
$hubText = $hubText.Replace('Cliente 1.0.42 TESTE', 'Cliente 1.0.43 TESTE')

foreach ($m in @('CurrentVersion = "1.0.43.0"','ApplyHub042();','startup-error.log')) {
    if ($mainText -notlike "*$m*") { throw "MainForm 1.0.43 sem $m" }
}
foreach ($m in @('SupportsTransparentBackColor','GAT Telemetria BETA 1.0.43','Cliente 1.0.43 TESTE')) {
    if ($hubText -notlike "*$m*") { throw "Hub 1.0.43 sem $m" }
}

Set-Content $main.FullName $mainText -Encoding UTF8
Set-Content $hub42.FullName $hubText -Encoding UTF8
Write-Host 'GAT Telemetria 1.0.43: corrigido crash do avatar transparente e adicionada protecao de inicializacao.'
