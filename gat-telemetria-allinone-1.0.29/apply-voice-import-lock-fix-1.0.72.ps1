param([string]$Root = "")

$ErrorActionPreference = 'Stop'

function Find-File([string]$Name) {
    $hit = Get-ChildItem -Path $Root -Recurse -Filter $Name -File | Select-Object -First 1
    if (-not $hit) { throw "$Name nao encontrado em $Root" }
    return $hit.FullName
}

$mainPath = Find-File 'MainForm.cs'
$voicePath = Find-File 'Voice062.cs'

$mainText = Get-Content $mainPath -Raw
$voiceText = Get-Content $voicePath -Raw

# 1.0.72: hotfix do importador de vozes.
# O Windows/MCI pode manter o MP3 aberto por alguns milissegundos depois de VoiceStop062().
# Durante a importacao, silenciamos temporariamente a voz, aguardamos o handle fechar
# e repetimos a substituicao caso o arquivo ainda esteja bloqueado.

$mainText = $mainText.Replace('CurrentVersion = "1.0.71.0"', 'CurrentVersion = "1.0.72.0"')
$mainText = $mainText.Replace('Cliente 1.0.71', 'Cliente 1.0.72')

$old = @'
            int imported = 0;
            int ignored = 0;
            int firstLimit = 0;
            try
            {
                VoiceStop062();
                VoiceBackupCurrent167();
                string dest = VoiceSpeedDir166();

                foreach (string src in dlg.FileNames ?? new string[0])
                {
                    int limit = VoiceLimitId167(src);
                    if (limit <= 0)
                    {
                        ignored++;
                        continue;
                    }

                    string name = "limite_" + limit.ToString("D3", CultureInfo.InvariantCulture) + ".mp3";
                    File.Copy(src, Path.Combine(dest, name), true);
                    if (firstLimit == 0) firstLimit = limit;
                    imported++;
                }

                VoiceRefreshUi062();
'@

$new = @'
            int imported = 0;
            int ignored = 0;
            int firstLimit = 0;
            bool muteBefore172 = _voiceMuted062;
            try
            {
                // Evita que um novo alerta reabra o MP3 enquanto ele esta sendo substituido.
                _voiceMuted062 = true;
                VoiceStop062();
                System.Threading.Thread.Sleep(250);
                VoiceStop062();

                VoiceBackupCurrent167();
                string dest = VoiceSpeedDir166();

                foreach (string src in dlg.FileNames ?? new string[0])
                {
                    int limit = VoiceLimitId167(src);
                    if (limit <= 0)
                    {
                        ignored++;
                        continue;
                    }

                    string name = "limite_" + limit.ToString("D3", CultureInfo.InvariantCulture) + ".mp3";
                    string target = Path.Combine(dest, name);
                    VoiceReplaceLimitFile172(src, target);

                    if (firstLimit == 0) firstLimit = limit;
                    imported++;
                }

                VoiceRefreshUi062();
'@

if ($voiceText -notlike '*VoiceReplaceLimitFile172*') {
    if ($voiceText -notlike "*$old*") { throw 'Trecho do importador 1.0.71 nao encontrado' }
    $voiceText = $voiceText.Replace($old, $new)

    $catchNeedle = @'
            catch (Exception ex)
            {
                MessageBox.Show("Nao foi possivel importar as falas: " + ex.Message, "Voz GAT", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private static double VoicePathNumber171
'@

    $catchNew = @'
            catch (Exception ex)
            {
                MessageBox.Show("Nao foi possivel importar as falas: " + ex.Message, "Voz GAT", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                VoiceStop062();
                _voiceMuted062 = muteBefore172;
                _voiceOverspeed171 = false;
            }
        }
    }

    private void VoiceReplaceLimitFile172(string source, string target)
    {
        string temp = target + ".import172";
        try
        {
            if (File.Exists(temp)) File.Delete(temp);
            File.Copy(source, temp, true);

            Exception last = null;
            for (int attempt = 0; attempt < 12; attempt++)
            {
                try
                {
                    VoiceStop062();
                    if (attempt > 0) System.Threading.Thread.Sleep(150);
                    File.Copy(temp, target, true);
                    File.Delete(temp);
                    return;
                }
                catch (IOException ex)
                {
                    last = ex;
                    System.Threading.Thread.Sleep(150);
                }
                catch (UnauthorizedAccessException ex)
                {
                    last = ex;
                    System.Threading.Thread.Sleep(150);
                }
            }

            throw new IOException(
                "O arquivo " + Path.GetFileName(target) +
                " continua em uso. Feche qualquer player que esteja tocando essa fala e tente novamente.",
                last);
        }
        finally
        {
            try { if (File.Exists(temp)) File.Delete(temp); } catch { }
        }
    }

    private static double VoicePathNumber171
'@

    if ($voiceText -notlike "*$catchNeedle*") { throw 'Final do importador 1.0.71 nao encontrado' }
    $voiceText = $voiceText.Replace($catchNeedle, $catchNew)
}

Set-Content $mainPath $mainText -Encoding UTF8
Set-Content $voicePath $voiceText -Encoding UTF8

foreach ($m in @('CurrentVersion = "1.0.72.0"','Cliente 1.0.72')) {
    if ($mainText -notlike "*$m*") { throw "MainForm 1.0.72 sem $m" }
}
foreach ($m in @('VoiceReplaceLimitFile172','System.Threading.Thread.Sleep(250)','_voiceMuted062 = true','continua em uso')) {
    if ($voiceText -notlike "*$m*") { throw "Voice 1.0.72 sem $m" }
}

Write-Host 'GAT Telemetria 1.0.72: hotfix de importacao de voz com arquivo em uso aplicado.'
