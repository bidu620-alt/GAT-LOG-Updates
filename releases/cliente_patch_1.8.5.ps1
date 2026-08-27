$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Windows.Forms -ErrorAction SilentlyContinue

function Msg([string]$Text,[string]$Title='GAT Telemetria Cliente | Atualizacao 1.8.5'){
    [System.Windows.Forms.MessageBox]::Show($Text,$Title,[System.Windows.Forms.MessageBoxButtons]::OK,[System.Windows.Forms.MessageBoxIcon]::Information)|Out-Null
}

$dir=Join-Path $env:LOCALAPPDATA 'GAT Telemetria Cliente'
$target=Join-Path $dir 'GAT_Telemetria_Cliente.ps1'
$hashPath=Join-Path $dir 'client_integrity.sha256'
$launcher=Join-Path $dir 'GAT Telemetria Cliente.exe'
$launcherBackup=Join-Path $dir ('GAT Telemetria Cliente.backup_'+(Get-Date -Format 'yyyyMMdd_HHmmss')+'.exe')
$scriptBackup=Join-Path $dir ('GAT_Telemetria_Cliente.backup_'+(Get-Date -Format 'yyyyMMdd_HHmmss')+'.ps1')
$newLauncher=Join-Path $dir ('GAT_Telemetria_Cliente_Silent_'+[Guid]::NewGuid().ToString('N')+'.exe')

try{
    if(!(Test-Path $target)){throw 'Arquivo principal do cliente nao encontrado.'}
    Copy-Item $target $scriptBackup -Force
    if(Test-Path $launcher){Copy-Item $launcher $launcherBackup -Force}

    $raw=Get-Content $target -Raw -Encoding UTF8
    $versionPattern='(?m)^\s*\$AppVersion\s*=\s*[''\"]1\.8(?:\.\d+)?[''\"]\s*$'
    if($raw -notmatch $versionPattern){throw 'A versao instalada nao pertence a base 1.8.x.'}
    $raw=[regex]::Replace($raw,$versionPattern,'$AppVersion=''1.8.5''',1)

    # Garante que chamadas auxiliares de PowerShell continuem ocultas.
    $oldCall="Start-Process -FilePath 'powershell.exe' -ArgumentList @('-NoProfile','-ExecutionPolicy','Bypass','-STA','-File',('\"'+`$tmp+'\"')) | Out-Null"
    $newCall="Start-Process -FilePath 'powershell.exe' -ArgumentList @('-NoProfile','-WindowStyle','Hidden','-ExecutionPolicy','Bypass','-STA','-File',('\"'+`$tmp+'\"')) -WindowStyle Hidden | Out-Null"
    if($raw.Contains($oldCall)){$raw=$raw.Replace($oldCall,$newCall)}

    $utf8=New-Object System.Text.UTF8Encoding($true)
    [System.IO.File]::WriteAllText($target,$raw,$utf8)
    $hash=(Get-FileHash -Algorithm SHA256 -Path $target).Hash.ToLowerInvariant()
    Set-Content -Path $hashPath -Value $hash -Encoding ASCII -Force

    $cs=@'
using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;

internal static class GatTelemetryLauncher
{
    [STAThread]
    private static void Main()
    {
        try
        {
            string dir = AppDomain.CurrentDomain.BaseDirectory;
            string script = Path.Combine(dir, "GAT_Telemetria_Cliente.ps1");
            string hashFile = Path.Combine(dir, "client_integrity.sha256");

            if (!File.Exists(script))
            {
                MessageBox.Show("Arquivo GAT_Telemetria_Cliente.ps1 nao encontrado.", "GAT Telemetria Cliente", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            if (!File.Exists(hashFile))
            {
                MessageBox.Show("Arquivo de integridade do cliente nao encontrado.", "GAT Telemetria Cliente", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            string expected = File.ReadAllText(hashFile).Trim().ToLowerInvariant();
            string actual;
            using (SHA256 sha = SHA256.Create())
            using (FileStream fs = File.OpenRead(script))
            {
                byte[] digest = sha.ComputeHash(fs);
                StringBuilder sb = new StringBuilder(digest.Length * 2);
                foreach (byte b in digest) sb.Append(b.ToString("x2"));
                actual = sb.ToString();
            }

            if (expected.Length == 0 || !String.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show("O arquivo do cliente foi alterado ou esta incompleto. Use VERIFICAR ATUALIZACAO/reparo.", "GAT Telemetria Cliente", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            ProcessStartInfo psi = new ProcessStartInfo();
            psi.FileName = "powershell.exe";
            psi.Arguments = "-NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -STA -File \"" + script + "\"";
            psi.WorkingDirectory = dir;
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.WindowStyle = ProcessWindowStyle.Hidden;
            Process.Start(psi);
        }
        catch (Exception ex)
        {
            MessageBox.Show("Nao foi possivel abrir o GAT Telemetria Cliente.\r\n\r\n" + ex.Message, "GAT Telemetria Cliente", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
'@

    # Compila como WindowsApplication: o launcher nao possui console proprio.
    Add-Type -TypeDefinition $cs -Language CSharp -OutputAssembly $newLauncher -OutputType WindowsApplication -ReferencedAssemblies @('System.dll','System.Core.dll','System.Windows.Forms.dll')
    if(!(Test-Path $newLauncher)){throw 'Nao foi possivel gerar o launcher silencioso.'}

    $replaced=$false
    try{
        if(Test-Path $launcher){Remove-Item $launcher -Force}
        Move-Item $newLauncher $launcher -Force
        $replaced=$true
    }catch{
        $silent=Join-Path $dir 'GAT Telemetria Cliente Silent.exe'
        Move-Item $newLauncher $silent -Force
        $launcher=$silent
    }

    # Atualiza atalhos existentes para o launcher silencioso, inclusive fixados na barra.
    try{
        $ws=New-Object -ComObject WScript.Shell
        $shortcutRoots=@(
            [Environment]::GetFolderPath('Desktop'),
            [Environment]::GetFolderPath('Programs'),
            (Join-Path $env:APPDATA 'Microsoft\Internet Explorer\Quick Launch\User Pinned\TaskBar')
        )|Where-Object {$_ -and (Test-Path $_)}|Select-Object -Unique
        foreach($root in $shortcutRoots){
            foreach($lnk in @(Get-ChildItem -Path $root -Filter '*.lnk' -File -Recurse -ErrorAction SilentlyContinue)){
                try{
                    $sc=$ws.CreateShortcut($lnk.FullName)
                    $name=[IO.Path]::GetFileNameWithoutExtension($lnk.Name)
                    $oldTarget=[string]$sc.TargetPath
                    if($name -match '(?i)GAT.*Telemetria' -or $oldTarget -match '(?i)GAT Telemetria Cliente\.exe$'){
                        $sc.TargetPath=$launcher
                        $sc.Arguments=''
                        $sc.WorkingDirectory=$dir
                        $icon=Join-Path $dir 'GAT_LOG.ico'
                        if(Test-Path $icon){$sc.IconLocation=$icon}
                        $sc.Save()
                    }
                }catch{}
            }
        }
    }catch{}

    $check=Get-Content $target -Raw -Encoding UTF8
    if(!$check.Contains('$AppVersion=''1.8.5''')){throw 'Falha ao gravar a versao 1.8.5.'}

    Start-Process -FilePath $launcher -WorkingDirectory $dir|Out-Null
    Msg 'Atualizacao 1.8.5 instalada. O launcher agora abre o GAT Telemetria com o PowerShell totalmente oculto.'
}catch{
    try{
        if(Test-Path $scriptBackup){
            Copy-Item $scriptBackup $target -Force
            $h=(Get-FileHash -Algorithm SHA256 -Path $target).Hash.ToLowerInvariant()
            Set-Content -Path $hashPath -Value $h -Encoding ASCII -Force
        }
        if(Test-Path $launcherBackup){Copy-Item $launcherBackup (Join-Path $dir 'GAT Telemetria Cliente.exe') -Force}
        if(Test-Path $newLauncher){Remove-Item $newLauncher -Force -ErrorAction SilentlyContinue}
    }catch{}
    Msg ("Nao foi possivel instalar a atualizacao 1.8.5.`r`n`r`n"+$_.Exception.Message) 'GAT Telemetria Cliente | Falha'
    exit 1
}
