param([string]$InstallDir='C:\Program Files\RoadLife Telemetria',[ValidateSet('Install','Restore')][string]$Mode='Install',[switch]$NoElevate)
$ErrorActionPreference='Stop'
$admin=([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if(-not $admin -and -not $NoElevate){
  $argsText='-NoProfile -ExecutionPolicy Bypass -File "'+$PSCommandPath+'" -InstallDir "'+$InstallDir+'" -Mode '+$Mode
  Start-Process powershell.exe -ArgumentList $argsText -Verb RunAs -WindowStyle Hidden -Wait
  exit
}
function Within($Root,$Relative){
  $rootPath=[IO.Path]::GetFullPath($Root).TrimEnd('\')
  $filePath=[IO.Path]::GetFullPath((Join-Path $rootPath $Relative))
  if(-not $filePath.StartsWith($rootPath+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Caminho invalido no pacote.'}
  return $filePath
}
try {
  if(Get-Process -Name GAT_TELEMETRIA,GAT_TELEMETRIA_APP -ErrorAction SilentlyContinue){throw 'Feche a Telemetria antes de aplicar o teste.'}
  $target=(Resolve-Path -LiteralPath $InstallDir).Path
  if(-not(Test-Path -LiteralPath (Join-Path $target 'GAT_TELEMETRIA_APP.exe'))){throw 'Esta pasta nao contem a Telemetria instalada.'}
  $payload=Join-Path $PSScriptRoot 'payload'
  $backup=Join-Path $PSScriptRoot 'backup'
  $backupManifest=Join-Path $backup 'files.json'
  if($Mode -eq 'Restore'){
    $saved=Get-Content -LiteralPath $backupManifest -Raw | ConvertFrom-Json
    foreach($entry in $saved){
      $dest=Within $target $entry.Path
      if($entry.Existed){Copy-Item -LiteralPath (Within $backup $entry.Path) -Destination $dest -Force}
      elseif(Test-Path -LiteralPath $dest){Remove-Item -LiteralPath $dest}
    }
  } else {
    $files=Get-Content (Join-Path $PSScriptRoot 'sha256.json') -Raw | ConvertFrom-Json
    foreach($entry in $files){
      if((Get-FileHash -LiteralPath (Within $payload $entry.Path)).Hash -ne $entry.Hash){throw 'SHA256 incorreto: '+$entry.Path}
    }
    if(-not(Test-Path -LiteralPath $backupManifest)){
      New-Item -ItemType Directory -Force $backup | Out-Null
      $saved=@()
      foreach($entry in $files){
        $dest=Within $target $entry.Path;$existed=Test-Path -LiteralPath $dest
        if($existed){
          $savedFile=Within $backup $entry.Path
          New-Item -ItemType Directory -Force (Split-Path $savedFile) | Out-Null
          Copy-Item -LiteralPath $dest -Destination $savedFile
          if((Get-FileHash $dest).Hash -ne (Get-FileHash $savedFile).Hash){throw 'Backup nao confere: '+$entry.Path}
        }
        $saved += [pscustomobject]@{Path=$entry.Path;Existed=$existed}
      }
      $saved | ConvertTo-Json | Set-Content $backupManifest -Encoding UTF8
    }
    try {
      foreach($entry in $files){
        $dest=Within $target $entry.Path
        New-Item -ItemType Directory -Force (Split-Path $dest) | Out-Null
        Copy-Item -LiteralPath (Within $payload $entry.Path) -Destination $dest -Force
        if((Get-FileHash $dest).Hash -ne $entry.Hash){throw 'Arquivo instalado nao confere: '+$entry.Path}
      }
    } catch {
      foreach($entry in (Get-Content $backupManifest -Raw | ConvertFrom-Json)){
        $dest=Within $target $entry.Path
        if($entry.Existed){Copy-Item -LiteralPath (Within $backup $entry.Path) -Destination $dest -Force}
        elseif(Test-Path -LiteralPath $dest){Remove-Item -LiteralPath $dest}
      }
      throw
    }
  }
  @{ok=$true;mode=$Mode;version=(Get-Item (Join-Path $target 'GAT_TELEMETRIA_APP.exe')).VersionInfo.FileVersion} | ConvertTo-Json | Set-Content (Join-Path $PSScriptRoot 'resultado.json') -Encoding UTF8
} catch {
  @{ok=$false;error=$_.Exception.Message} | ConvertTo-Json | Set-Content (Join-Path $PSScriptRoot 'resultado.json') -Encoding UTF8
  throw
}
