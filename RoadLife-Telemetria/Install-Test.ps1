param([string]$InstallDir, [switch]$NoPause, [ValidateSet('payload','v5-original')][string]$PayloadFolder = 'payload')
$ErrorActionPreference = 'Stop'
try {
 if (-not $InstallDir) {
  Add-Type -AssemblyName System.Windows.Forms
  $dialog = New-Object System.Windows.Forms.FolderBrowserDialog
  $dialog.Description = 'Selecione a pasta onde a RoadLife Telemetria esta instalada'
  $dialog.ShowNewFolderButton = $false
  if ($dialog.ShowDialog() -ne 'OK') { exit }
  $InstallDir = $dialog.SelectedPath
 }
 $target = (Resolve-Path -LiteralPath $InstallDir).Path
 if (-not (Test-Path -LiteralPath (Join-Path $target 'GAT_TELEMETRIA_APP.exe'))) {
  throw 'Esta pasta nao contem GAT_TELEMETRIA_APP.exe. Selecione a instalacao atual da RoadLife.'
 }
 if (Get-Process -Name GAT_TELEMETRIA,GAT_TELEMETRIA_APP -ErrorAction SilentlyContinue) {
  throw 'Feche a RoadLife e tente novamente.'
 }
 $payload = Join-Path $PSScriptRoot $PayloadFolder
 if (-not (Test-Path -LiteralPath (Join-Path $payload 'GAT_TELEMETRIA_APP.exe'))) { throw 'Pacote incompleto. Extraia o ZIP inteiro primeiro.' }
 $backup = Join-Path $target ('backup-teste-1.0.68.17-' + (Get-Date -Format yyyyMMdd-HHmmss-fff) + '-' + [Guid]::NewGuid().ToString('N').Substring(0,8))
 New-Item -ItemType Directory -Path $backup | Out-Null
 $manifest = @()
 foreach ($file in Get-ChildItem $payload -File -Recurse) {
  $relative = $file.FullName.Substring($payload.Length + 1)
  $dest = Join-Path $target $relative
  $saved = Join-Path $backup $relative
  $existed = Test-Path -LiteralPath $dest
  if ($existed) {
   New-Item -ItemType Directory -Path (Split-Path $saved) -Force | Out-Null
   Copy-Item -LiteralPath $dest -Destination $saved
  }
  $manifest += [pscustomobject]@{ Path = $relative; Existed = $existed }
 }
 $manifest | ConvertTo-Json | Set-Content (Join-Path $backup 'files.json') -Encoding UTF8
 foreach ($file in Get-ChildItem $payload -File -Recurse) {
  $relative = $file.FullName.Substring($payload.Length + 1)
  $dest = Join-Path $target $relative
  New-Item -ItemType Directory -Path (Split-Path $dest) -Force | Out-Null
  Copy-Item -LiteralPath $file.FullName -Destination $dest -Force
 }
 foreach ($name in @('limite_056.mp3','limite_56.mp3','limite_056.wav','limite_56.wav')) {
  $relative = Join-Path 'voz/limites' $name
  $old = Join-Path $target $relative
  if (Test-Path -LiteralPath $old) {
   $saved = Join-Path $backup $relative
   New-Item -ItemType Directory -Path (Split-Path $saved) -Force | Out-Null
   Move-Item -LiteralPath $old -Destination $saved
   $manifest += [pscustomobject]@{ Path = $relative; Existed = $true }
   $manifest | ConvertTo-Json | Set-Content (Join-Path $backup 'files.json') -Encoding UTF8
  }
 }
 Write-Host "Pacote $PayloadFolder aplicado. Backup: $backup"
 Write-Host 'Abra a RoadLife pelo atalho habitual. Dados e Dashboard2 existentes foram preservados.'
} catch { if ($NoPause) { throw }; Write-Host $_.Exception.Message -ForegroundColor Red }
if (-not $NoPause) { Read-Host 'Pressione Enter para fechar' }

