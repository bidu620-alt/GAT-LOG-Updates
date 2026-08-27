$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Windows.Forms -ErrorAction SilentlyContinue

function Msg([string]$Text,[string]$Title='GAT Telemetria Cliente | Atualizacao 1.8.5'){
    [System.Windows.Forms.MessageBox]::Show($Text,$Title,[System.Windows.Forms.MessageBoxButtons]::OK,[System.Windows.Forms.MessageBoxIcon]::Information)|Out-Null
}

$dir=Join-Path $env:LOCALAPPDATA 'GAT Telemetria Cliente'
$target=Join-Path $dir 'GAT_Telemetria_Cliente.ps1'
$hashPath=Join-Path $dir 'client_integrity.sha256'
$backup=Join-Path $dir ('GAT_Telemetria_Cliente.backup_'+(Get-Date -Format 'yyyyMMdd_HHmmss')+'.ps1')

try{
    if(!(Test-Path $target)){throw 'Arquivo principal do cliente nao encontrado.'}
    Copy-Item $target $backup -Force
    $raw=Get-Content $target -Raw -Encoding UTF8

    $versionPattern='(?m)^\s*\$AppVersion\s*=\s*[''"]1\.8(?:\.\d+)?[''"]\s*$'
    if($raw -notmatch $versionPattern){throw 'A versao instalada nao pertence a base 1.8.x.'}
    $raw=[regex]::Replace($raw,$versionPattern,'$AppVersion=''1.8.5''',1)

    # Esconde a janela de console do PowerShell que hospeda o cliente.
    if($raw -notmatch 'GAT_HIDE_OWN_CONSOLE'){
        $hide=@'
# GAT_HIDE_OWN_CONSOLE
try{
    Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
public static class GatConsoleWindow {
    [DllImport("kernel32.dll")] public static extern IntPtr GetConsoleWindow();
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
}
"@ -ErrorAction SilentlyContinue
    $h=[GatConsoleWindow]::GetConsoleWindow()
    if($h-ne[IntPtr]::Zero){[void][GatConsoleWindow]::ShowWindow($h,0)}
}catch{}
'@
        $anchor="$ErrorActionPreference='SilentlyContinue'"
        if($raw.Contains($anchor)){
            $raw=$raw.Replace($anchor,$anchor+"`r`n"+$hide.TrimEnd())
        } else {
            $raw=$hide.TrimEnd()+"`r`n"+$raw
        }
    }

    # Garante que o atualizador seja iniciado oculto nas proximas versoes.
    $raw=$raw.Replace("Start-Process -FilePath 'powershell.exe' -ArgumentList @('-NoProfile','-ExecutionPolicy','Bypass','-STA','-File',('\"'+$tmp+'\"')) | Out-Null","Start-Process -FilePath 'powershell.exe' -ArgumentList @('-NoProfile','-WindowStyle','Hidden','-ExecutionPolicy','Bypass','-STA','-File',('\"'+$tmp+'\"')) -WindowStyle Hidden | Out-Null")
    $raw=$raw.Replace("Start-Process -FilePath 'powershell.exe' -ArgumentList @('-NoProfile','-ExecutionPolicy','Bypass','-STA','-File',('\"'+$tmp+'\"'))|Out-Null","Start-Process -FilePath 'powershell.exe' -ArgumentList @('-NoProfile','-WindowStyle','Hidden','-ExecutionPolicy','Bypass','-STA','-File',('\"'+$tmp+'\"')) -WindowStyle Hidden|Out-Null")

    $utf8=New-Object System.Text.UTF8Encoding($true)
    [System.IO.File]::WriteAllText($target,$raw,$utf8)
    $hash=(Get-FileHash -Algorithm SHA256 -Path $target).Hash.ToLowerInvariant()
    Set-Content -Path $hashPath -Value $hash -Encoding ASCII -Force

    $check=Get-Content $target -Raw -Encoding UTF8
    if(!$check.Contains('$AppVersion=''1.8.5''')){throw 'Falha ao gravar a versao 1.8.5.'}
    if($check -notmatch 'GAT_HIDE_OWN_CONSOLE'){throw 'Falha ao instalar ocultacao do console.'}

    # Atualiza atalhos do GAT Telemetria para abrir diretamente o script em modo oculto.
    try{
        $ws=New-Object -ComObject WScript.Shell
        $roots=@(
            [Environment]::GetFolderPath('Desktop'),
            [Environment]::GetFolderPath('Programs')
        )|Where-Object {$_ -and (Test-Path $_)}|Select-Object -Unique
        foreach($root in $roots){
            foreach($lnk in @(Get-ChildItem -Path $root -Filter '*.lnk' -File -Recurse -ErrorAction SilentlyContinue)){
                try{
                    $name=[IO.Path]::GetFileNameWithoutExtension($lnk.Name)
                    $sc=$ws.CreateShortcut($lnk.FullName)
                    $oldTarget=[string]$sc.TargetPath
                    if($name -match '(?i)GAT.*Telemetria' -or $oldTarget -match '(?i)GAT Telemetria Cliente\.exe$'){
                        $sc.TargetPath=(Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe')
                        $sc.Arguments='-NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -STA -File "'+$target+'"'
                        $sc.WorkingDirectory=$dir
                        $icon=Join-Path $dir 'GAT_LOG.ico'
                        if(Test-Path $icon){$sc.IconLocation=$icon}
                        $sc.Save()
                    }
                }catch{}
            }
        }
    }catch{}

    # Reabre diretamente em PowerShell oculto; nao depende do launcher antigo.
    Start-Process -FilePath (Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe') -ArgumentList @('-NoProfile','-WindowStyle','Hidden','-ExecutionPolicy','Bypass','-STA','-File',('"'+$target+'"')) -WorkingDirectory $dir -WindowStyle Hidden|Out-Null
    Msg 'Atualizacao 1.8.5 instalada. A janela preta do PowerShell agora fica oculta sem substituir o executavel do cliente.'
}catch{
    try{
        if(Test-Path $backup){
            Copy-Item $backup $target -Force
            $h=(Get-FileHash -Algorithm SHA256 -Path $target).Hash.ToLowerInvariant()
            Set-Content -Path $hashPath -Value $h -Encoding ASCII -Force
        }
    }catch{}
    Msg ("Nao foi possivel instalar a atualizacao 1.8.5.`r`n`r`n"+$_.Exception.Message) 'GAT Telemetria Cliente | Falha'
    exit 1
}
