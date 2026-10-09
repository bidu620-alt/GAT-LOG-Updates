param([Parameter(Mandatory=$true)][string]$BackupDir)
$ErrorActionPreference = 'Stop'
if (Get-Process -Name GAT_TELEMETRIA,GAT_TELEMETRIA_APP -ErrorAction SilentlyContinue) { throw 'Feche a RoadLife primeiro.' }
$backup = (Resolve-Path -LiteralPath $BackupDir).Path
$target = Split-Path $backup
$entries = Get-Content (Join-Path $backup 'files.json') -Raw | ConvertFrom-Json
foreach ($entry in $entries) {
 $dest = [IO.Path]::GetFullPath((Join-Path $target $entry.Path))
 if (-not $dest.StartsWith($target + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Caminho invalido no backup.' }
 if ($entry.Existed) { Copy-Item -LiteralPath (Join-Path $backup $entry.Path) -Destination $dest -Force }
 elseif (Test-Path -LiteralPath $dest) { Remove-Item -LiteralPath $dest }
}
Write-Host 'Arquivos anteriores restaurados.'
