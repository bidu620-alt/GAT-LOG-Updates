$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Windows.Forms -ErrorAction SilentlyContinue
function Msg([string]$Text,[string]$Title='GAT LOG BETA 1.9.4'){
 [System.Windows.Forms.MessageBox]::Show($Text,$Title,[System.Windows.Forms.MessageBoxButtons]::OK,[System.Windows.Forms.MessageBoxIcon]::Information)|Out-Null
}
function Find-AppDir{
 $c=New-Object System.Collections.Generic.List[string]
 foreach($x in @($env:GATLOG_APPDIR,$env:GATLOG_RUNTIME_DIR)){if($x){[void]$c.Add($x)}}
 foreach($r in @('HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\GAT LOG BETA','HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\GAT LOG BETA','HKLM:\Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\GAT LOG BETA')){try{$v=(Get-ItemProperty $r -ErrorAction Stop).InstallLocation;if($v){[void]$c.Add([string]$v)}}catch{}}
 if($env:LOCALAPPDATA){[void]$c.Add((Join-Path $env:LOCALAPPDATA 'GAT LOG BETA'));[void]$c.Add((Join-Path $env:LOCALAPPDATA 'Programs\GAT LOG BETA'))}
 foreach($d in $c){try{if($d -and (Test-Path (Join-Path $d 'GAT_Server_Manager.ps1'))){return [IO.Path]::GetFullPath($d)}}catch{}}
 foreach($root in @($env:LOCALAPPDATA,(Join-Path $env:USERPROFILE 'Desktop'))){if(!$root -or !(Test-Path $root)){continue};try{$f=Get-ChildItem $root -Filter 'GAT_Server_Manager.ps1' -File -Recurse -ErrorAction SilentlyContinue|Select-Object -First 1;if($f){return $f.DirectoryName}}catch{}}
 return $null
}
$target='';$backup=''
try{
 $dir=Find-AppDir;if(!$dir){throw 'Nao encontrei a instalacao do GAT LOG BETA.'}
 $target=Join-Path $dir 'GAT_Server_Manager.ps1';if(!(Test-Path $target)){throw 'Arquivo principal do servidor nao encontrado.'}
 $backupRoot=Join-Path $env:LOCALAPPDATA 'GAT-LOG\BACKUP_ATUALIZACAO';New-Item -ItemType Directory -Path $backupRoot -Force|Out-Null
 $backup=Join-Path $backupRoot ('GAT_Server_Manager_antes_1.9.4_'+(Get-Date -Format 'yyyyMMdd_HHmmss')+'.ps1');Copy-Item $target $backup -Force
 $t=Get-Content $target -Raw
 $t=$t.Replace('$AppVersion = "1.9.3"','$AppVersion = "1.9.4"')
 if($t -notmatch '\$btnUpdateQuick\s*='){
  $old='$btnAccount = New-GatButton "CONTA / SENHA" 15 548 180 44 ([System.Drawing.Color]::FromArgb(10,42,68)) $sidebar' + "`r`n" + '$btnAccount.Add_Click({ Show-ChangeAdminPassword })'
  if(!$t.Contains($old)){$old=$old.Replace("`r`n","`n")}
  $new=$old + "`r`n" + '$btnUpdateQuick = New-GatButton "ATUALIZAR APP" 15 602 180 38 ([System.Drawing.Color]::FromArgb(35,105,175)) $sidebar' + "`r`n" + '$btnUpdateQuick.Add_Click({ Show-GatPage $pageSystem $navSystem; if($null -ne $script:AvailableUpdate){Start-GatSelfUpdate}else{Refresh-GatUpdateStatus $true} })'
  if(!$t.Contains($old)){throw 'Nao encontrei o ponto seguro para adicionar o botao de atualizacao.'}
  $t=$t.Replace($old,$new)
 }
 if($t -notmatch '\$startupUpdateTimer\s*='){
  $needle='[void]$form.ShowDialog()';$pos=$t.LastIndexOf($needle);if($pos -lt 0){throw 'Nao encontrei o ponto seguro para ativar a verificacao automatica.'}
  $auto=@'
# Verificacao automatica de atualizacao apos login e com a janela ja aberta.
# Falha de internet/GitHub nao impede o uso do GAT-LOG.
$startupUpdateTimer = New-Object System.Windows.Forms.Timer
$startupUpdateTimer.Interval = 1600
$startupUpdateTimer.Add_Tick({
    $startupUpdateTimer.Stop()
    Refresh-GatUpdateStatus $false
    if ($null -ne $script:AvailableUpdate) { Start-GatSelfUpdate }
})
$startupUpdateTimer.Start()

'@
  $t=$t.Insert($pos,$auto)
 }
 [IO.File]::WriteAllText($target,$t,(New-Object Text.UTF8Encoding($true)))
 try{$vf=Join-Path $dir 'GATLOG_VERSION.ini';$v=if(Test-Path $vf){Get-Content $vf -Raw}else{''};if($v -match '(?im)^servidor\s*='){$v=[regex]::Replace($v,'(?im)^servidor\s*=.*$','servidor=1.9.4')}else{$v+="`r`nservidor=1.9.4`r`n"};[IO.File]::WriteAllText($vf,$v,(New-Object Text.UTF8Encoding($false)))}catch{}
 try{$r='HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\GAT LOG BETA';if(Test-Path $r){Set-ItemProperty $r DisplayVersion '1.9.4' -ErrorAction SilentlyContinue}}catch{}
 Start-Sleep -Milliseconds 700
 $launcher=Join-Path $dir 'GAT LOG BETA.exe';if(Test-Path $launcher){Start-Process $launcher -WorkingDirectory $dir|Out-Null}else{Start-Process powershell.exe -ArgumentList @('-NoProfile','-ExecutionPolicy','Bypass','-STA','-File',('"'+$target+'"')) -WorkingDirectory $dir|Out-Null}
}catch{
 try{if($backup -and $target -and (Test-Path $backup)){Copy-Item $backup $target -Force}}catch{}
 Msg ("Falha ao aplicar a atualizacao 1.9.4.`r`n`r`n"+$_.Exception.Message) 'GAT LOG BETA | Atualizacao';exit 1
}
