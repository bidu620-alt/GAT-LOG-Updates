param(
    [Parameter(Mandatory=$true)]
    [string]$Root
)

$ErrorActionPreference = 'Stop'
$rootPath = (Resolve-Path $Root).Path
$main = Get-ChildItem $rootPath -Filter 'MainForm.cs' -Recurse | Select-Object -First 1
$hub = Get-ChildItem $rootPath -Filter 'MainForm.Hub041.cs' -Recurse | Select-Object -First 1
$hub45 = Get-ChildItem $rootPath -Filter 'MainForm.Hub045.cs' -Recurse | Select-Object -First 1
$voice = Get-ChildItem $rootPath -Filter 'Voice062.cs' -Recurse | Select-Object -First 1
if (-not $main -or -not $hub -or -not $voice) { throw 'Fonte 1.0.66 incompleto para aplicar 1.0.67.' }

$mainText = Get-Content $main.FullName -Raw
$hubText = Get-Content $hub.FullName -Raw
$voiceText = Get-Content $voice.FullName -Raw
$hub45Text = if ($hub45) { Get-Content $hub45.FullName -Raw } else { '' }

function Replace-Between([string]$text, [string]$startMarker, [string]$endMarker, [string]$replacement) {
    $s = $text.IndexOf($startMarker, [StringComparison]::Ordinal)
    if ($s -lt 0) { throw "Inicio nao encontrado: $startMarker" }
    $e = $text.IndexOf($endMarker, $s + $startMarker.Length, [StringComparison]::Ordinal)
    if ($e -lt 0) { throw "Fim nao encontrado: $endMarker" }
    return $text.Substring(0, $s) + $replacement.TrimEnd() + "`r`n`r`n" + $text.Substring($e)
}

# Versao 1.0.67 TESTE.
$mainText = $mainText.Replace('CurrentVersion = "1.0.66.0"', 'CurrentVersion = "1.0.67.0"')
$mainText = $mainText.Replace('Text = "Cliente 1.0.66"', 'Text = "Cliente 1.0.67"')
$mainText = $mainText.Replace('HUB 1.0.66:', 'HUB 1.0.67:')
$hubText = [regex]::Replace($hubText, 'Text\s*=\s*"GAT Telemetria BETA 1\.0\.66"', 'Text = "GAT Telemetria BETA 1.0.67"', 1)
$hubText = $hubText.Replace('Cliente 1.0.66 TESTE', 'Cliente 1.0.67 TESTE')
if ($hub45Text) {
    $hub45Text = $hub45Text.Replace('GAT Telemetria BETA 1.0.66', 'GAT Telemetria BETA 1.0.67')
    $hub45Text = $hub45Text.Replace('Cliente: 1.0.66 TESTE', 'Cliente: 1.0.67 TESTE')
}

$settings = @'
    private void BuildVoiceSettings062(Panel p)
    {
        VoiceLoadSettings062();
        var box = new Panel
        {
            Left = 0, Top = 252, Width = Math.Max(520, p.Width), Height = 205,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            BackColor = Color.FromArgb(5, 20, 36)
        };
        box.Controls.Add(new Label
        {
            Text = "VOZ E ALERTAS • PACOTES DE VOZ",
            Left = 14, Top = 10, Width = 330, Height = 23,
            ForeColor = Color.FromArgb(100, 180, 255),
            Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold)
        });
        _voiceVolumeLabel062 = new Label
        {
            Text = "Volume da voz: " + _voiceVolume062 + "%",
            Left = 14, Top = 39, Width = 150, Height = 24,
            ForeColor = Color.Gainsboro
        };
        box.Controls.Add(_voiceVolumeLabel062);
        _voiceVolumeBar062 = new TrackBar
        {
            Left = 160, Top = 30, Width = 310, Minimum = 0, Maximum = 100,
            Value = _voiceVolume062, TickFrequency = 10, SmallChange = 5, LargeChange = 10
        };
        _voiceVolumeBar062.Scroll += delegate { VoiceSetVolume062(_voiceVolumeBar062.Value); };
        box.Controls.Add(_voiceVolumeBar062);

        var test = HubButton041("TESTAR VOZ", 110);
        test.Left = 14; test.Top = 72;
        test.Click += delegate { if (!VoicePlayGroup062("startup")) MessageBox.Show("Nenhuma voz pronta para teste.", "Voz GAT", MessageBoxButtons.OK, MessageBoxIcon.Information); };
        box.Controls.Add(test);

        var replace = HubButton041("SUBSTITUIR VOZ", 135);
        replace.Left = 134; replace.Top = 72;
        replace.Click += delegate { VoiceReplacePackage167(); };
        box.Controls.Add(replace);

        var add = HubButton041("ADICIONAR FALAS", 145);
        add.Left = 279; add.Top = 72;
        add.Click += delegate { VoiceAddExtras167(); };
        box.Controls.Add(add);

        var model = HubButton041("BAIXAR MODELO TXT", 145);
        model.Left = 14; model.Top = 111;
        model.Click += delegate { VoiceOpenTemplate167(); };
        box.Controls.Add(model);

        var restore = HubButton041("RESTAURAR BACKUP", 145);
        restore.Left = 169; restore.Top = 111;
        restore.Click += delegate { VoiceRestoreBackup167(); };
        box.Controls.Add(restore);

        _voiceStatusLabel062 = new Label
        {
            Left = 14, Top = 151, Width = Math.Max(480, box.Width - 28), Height = 46,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            ForeColor = Color.FromArgb(145, 175, 205)
        };
        box.Controls.Add(_voiceStatusLabel062);
        box.Resize += delegate
        {
            int w = box.ClientSize.Width;
            _voiceVolumeBar062.Width = Math.Max(160, Math.Min(360, w - 190));
            _voiceStatusLabel062.Width = Math.Max(480, w - 28);
        };
        p.Controls.Add(box);
        VoiceRefreshUi062();
    }
'@
$voiceText = Replace-Between $voiceText '    private void BuildVoiceSettings062(Panel p)' '    private void VoiceRefreshUi062()' $settings

$refresh = @'
    private void VoiceRefreshUi062()
    {
        try
        {
            if (_voiceVolumeLabel062 != null) _voiceVolumeLabel062.Text = "Volume da voz: " + _voiceVolume062 + "%";
            if (_voiceVolumeBar062 != null && _voiceVolumeBar062.Value != _voiceVolume062) _voiceVolumeBar062.Value = _voiceVolume062;
            if (_voiceStatusLabel062 != null)
            {
                int general = 0, limits = 0, extras = 0;
                try { if (Directory.Exists(VoiceIndividualDir166())) general = Directory.GetFiles(VoiceIndividualDir166(), "*.mp3").Length; } catch { }
                try { if (Directory.Exists(VoiceSpeedDir166())) limits = Directory.GetFiles(VoiceSpeedDir166(), "*.mp3").Length; } catch { }
                try { string er = VoiceExtrasRoot167(); if (Directory.Exists(er)) extras = Directory.GetFiles(er, "*.mp3", SearchOption.AllDirectories).Length; } catch { }
                _voiceStatusLabel062.Text = "Pacote ativo • " + general + " falas gerais • " + limits + " limites • " + extras + " extras. Substitua ou acrescente vozes sem atualizar o GAT.";
            }
        }
        catch { }
    }
'@
$voiceText = Replace-Between $voiceText '    private void VoiceRefreshUi062()' '    private void VoiceSetVolume062(int value)' $refresh

$manager = @'
    private static readonly string[] VoiceGroups167 = new string[]
    {
        "startup","cargo_start","trip","speed","radar","fine","crash","damage","fuel_low","refuel",
        "fatigue","arriving","delivery","perfect","rain","night","lights","toll","service","random"
    };

    private static string VoiceExtrasRoot167()
    {
        string dir = Path.Combine(VoiceIndividualRoot166(), "extras");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static string VoiceBackupRoot167() { return Path.Combine(VoiceDataDir062(), "voicepack167-backup-latest"); }

    private static bool VoiceGroupAllowed167(string group)
    {
        return VoiceGroups167.Any(x => string.Equals(x, group ?? "", StringComparison.OrdinalIgnoreCase));
    }

    private static string[] VoiceExtraFiles167(string group)
    {
        try
        {
            if (!VoiceGroupAllowed167(group)) return new string[0];
            string dir = Path.Combine(VoiceExtrasRoot167(), group.ToLowerInvariant());
            if (!Directory.Exists(dir)) return new string[0];
            return Directory.GetFiles(dir, "*.mp3").OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();
        }
        catch { return new string[0]; }
    }

    private static void VoiceCopyDir167(string source, string dest)
    {
        if (!Directory.Exists(source)) return;
        Directory.CreateDirectory(dest);
        foreach (string file in Directory.GetFiles(source)) File.Copy(file, Path.Combine(dest, Path.GetFileName(file)), true);
        foreach (string sub in Directory.GetDirectories(source)) VoiceCopyDir167(sub, Path.Combine(dest, Path.GetFileName(sub)));
    }

    private void VoiceBackupCurrent167()
    {
        string backup = VoiceBackupRoot167();
        try
        {
            if (Directory.Exists(backup)) Directory.Delete(backup, true);
            Directory.CreateDirectory(backup);
            VoiceCopyDir167(VoiceIndividualRoot166(), backup);
        }
        catch { }
    }

    private static int VoiceGeneralId167(string file)
    {
        try
        {
            string n = Path.GetFileName(file);
            if (string.IsNullOrWhiteSpace(n) || n.Length < 4 || n[3] != '_') return 0;
            int id;
            if (!int.TryParse(n.Substring(0, 3), NumberStyles.Integer, CultureInfo.InvariantCulture, out id)) return 0;
            return id >= 1 && id <= 122 ? id : 0;
        }
        catch { return 0; }
    }

    private static int VoiceLimitId167(string file)
    {
        try
        {
            string n = Path.GetFileNameWithoutExtension(file) ?? "";
            if (!n.StartsWith("limite_", StringComparison.OrdinalIgnoreCase)) return 0;
            int v;
            if (!int.TryParse(n.Substring(7), NumberStyles.Integer, CultureInfo.InvariantCulture, out v)) return 0;
            return new int[] {20,30,40,50,60,70,80,90,100,110,120,130}.Contains(v) ? v : 0;
        }
        catch { return 0; }
    }

    private void VoiceReplacePackage167()
    {
        using (var dlg = new FolderBrowserDialog())
        {
            dlg.Description = "Selecione a pasta do pacote de voz. Ela pode conter general e limits.";
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            string root = dlg.SelectedPath;
            string generalSrc = Directory.Exists(Path.Combine(root, "general")) ? Path.Combine(root, "general") : root;
            string limitsSrc = Directory.Exists(Path.Combine(root, "limits")) ? Path.Combine(root, "limits") : root;
            List<string> generalFiles = new List<string>();
            List<string> limitFiles = new List<string>();
            try { generalFiles = Directory.GetFiles(generalSrc, "*.mp3").Where(x => VoiceGeneralId167(x) > 0).ToList(); } catch { }
            try { limitFiles = Directory.GetFiles(limitsSrc, "*.mp3").Where(x => VoiceLimitId167(x) > 0).ToList(); } catch { }
            if (generalFiles.Count == 0 && limitFiles.Count == 0)
            {
                MessageBox.Show("Nenhum MP3 reconhecido. Use 001_... até 122_... e/ou limite_020.mp3 até limite_130.mp3.", "Voz GAT", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            try
            {
                VoiceStop062();
                VoiceBackupCurrent167();
                string gd = VoiceIndividualDir166();
                string ld = VoiceSpeedDir166();
                foreach (string src in generalFiles)
                {
                    int id = VoiceGeneralId167(src);
                    string prefix = id.ToString("D3", CultureInfo.InvariantCulture) + "_";
                    foreach (string old in Directory.GetFiles(gd, prefix + "*.mp3")) try { File.Delete(old); } catch { }
                    File.Copy(src, Path.Combine(gd, Path.GetFileName(src)), true);
                }
                foreach (string src in limitFiles)
                {
                    int limit = VoiceLimitId167(src);
                    File.Copy(src, Path.Combine(ld, "limite_" + limit.ToString("D3", CultureInfo.InvariantCulture) + ".mp3"), true);
                }
                VoiceRefreshUi062();
                ClientStore.Log("pacote de voz 1.0.67 substituido: " + generalFiles.Count + " gerais / " + limitFiles.Count + " limites");
                MessageBox.Show("Voz atualizada: " + generalFiles.Count + " falas gerais e " + limitFiles.Count + " limites substituídos.\n\nUm backup do pacote anterior foi salvo.", "Voz GAT", MessageBoxButtons.OK, MessageBoxIcon.Information);
                VoicePlayGroup062("startup");
            }
            catch (Exception ex) { MessageBox.Show("Não foi possível substituir a voz: " + ex.Message, "Voz GAT", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }
    }

    private static string VoiceUniqueExtra167(string dir, string fileName)
    {
        Directory.CreateDirectory(dir);
        string safe = Path.GetFileName(fileName);
        string dest = Path.Combine(dir, safe);
        if (!File.Exists(dest)) return dest;
        string stem = Path.GetFileNameWithoutExtension(safe);
        for (int i = 2; i < 1000; i++)
        {
            dest = Path.Combine(dir, stem + "_" + i.ToString(CultureInfo.InvariantCulture) + ".mp3");
            if (!File.Exists(dest)) return dest;
        }
        return Path.Combine(dir, stem + "_" + DateTime.Now.ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture) + ".mp3");
    }

    private void VoiceAddExtras167()
    {
        using (var dlg = new FolderBrowserDialog())
        {
            dlg.Description = "Selecione extras/<grupo>/ ou uma pasta de grupo como crash, delivery ou random.";
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            string root = dlg.SelectedPath;
            string extrasSource = Directory.Exists(Path.Combine(root, "extras")) ? Path.Combine(root, "extras") : root;
            int added = 0;
            try
            {
                string selectedName = (new DirectoryInfo(extrasSource)).Name.ToLowerInvariant();
                if (VoiceGroupAllowed167(selectedName))
                {
                    string destDir = Path.Combine(VoiceExtrasRoot167(), selectedName);
                    foreach (string src in Directory.GetFiles(extrasSource, "*.mp3")) { File.Copy(src, VoiceUniqueExtra167(destDir, Path.GetFileName(src)), false); added++; }
                }
                else
                {
                    foreach (string group in VoiceGroups167)
                    {
                        string srcDir = Path.Combine(extrasSource, group);
                        if (!Directory.Exists(srcDir)) continue;
                        string destDir = Path.Combine(VoiceExtrasRoot167(), group);
                        foreach (string src in Directory.GetFiles(srcDir, "*.mp3")) { File.Copy(src, VoiceUniqueExtra167(destDir, Path.GetFileName(src)), false); added++; }
                    }
                }
                VoiceRefreshUi062();
                if (added > 0)
                {
                    ClientStore.Log("falas extras 1.0.67 adicionadas: " + added);
                    MessageBox.Show(added + " fala(s) extra(s) adicionada(s). Elas entram no sorteio do grupo correspondente.", "Voz GAT", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else MessageBox.Show("Nenhuma fala extra encontrada. Use pastas como extras/crash, extras/delivery ou extras/random.", "Voz GAT", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (Exception ex) { MessageBox.Show("Não foi possível adicionar as falas: " + ex.Message, "Voz GAT", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }
    }

    private void VoiceRestoreBackup167()
    {
        string backup = VoiceBackupRoot167();
        if (!Directory.Exists(backup))
        {
            MessageBox.Show("Ainda não existe backup de voz. Ele é criado automaticamente antes de uma substituição.", "Voz GAT", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (MessageBox.Show("Restaurar o pacote salvo antes da última substituição?", "Voz GAT", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        try
        {
            VoiceStop062();
            string active = VoiceIndividualRoot166();
            if (Directory.Exists(active)) Directory.Delete(active, true);
            Directory.CreateDirectory(active);
            VoiceCopyDir167(backup, active);
            VoiceRefreshUi062();
            ClientStore.Log("backup de voz 1.0.67 restaurado");
            MessageBox.Show("Backup de voz restaurado.", "Voz GAT", MessageBoxButtons.OK, MessageBoxIcon.Information);
            VoicePlayGroup062("startup");
        }
        catch (Exception ex) { MessageBox.Show("Não foi possível restaurar o backup: " + ex.Message, "Voz GAT", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private void VoiceOpenTemplate167()
    {
        const string url = "https://gatlogets2.com.br/GAT_MODELO_PACOTE_VOZ_v1.txt";
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = url, UseShellExecute = true }); }
        catch
        {
            try { Clipboard.SetText(url); } catch { }
            MessageBox.Show("Abra este endereço para baixar o modelo:\n" + url + "\n\nO link também foi copiado.", "Voz GAT", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
'@
$syncMarker = '    private void VoiceSyncDashSetting062(JObject message)'
$syncPos = $voiceText.IndexOf($syncMarker, [StringComparison]::Ordinal)
if ($syncPos -lt 0) { throw 'VoiceSyncDashSetting062 nao encontrado.' }
$voiceText = $voiceText.Substring(0, $syncPos) + $manager.TrimEnd() + "`r`n`r`n" + $voiceText.Substring($syncPos)

$groupMethod = @'
    private bool VoicePlayGroup062(string group, bool interrupt = true)
    {
        int first = 0, last = 0;
        string normalized = (group ?? "").ToLowerInvariant();
        switch (normalized)
        {
            case "startup": first = 1; last = 5; break;
            case "cargo_start": first = 6; last = 10; break;
            case "trip": first = 11; last = 15; break;
            case "speed": first = 16; last = 22; break;
            case "radar": first = 23; last = 27; break;
            case "fine": first = 28; last = 33; break;
            case "crash": first = 34; last = 43; break;
            case "damage": first = 44; last = 48; break;
            case "fuel_low": first = 49; last = 54; break;
            case "refuel": first = 55; last = 59; break;
            case "fatigue": first = 60; last = 64; break;
            case "arriving": first = 65; last = 69; break;
            case "delivery": first = 70; last = 77; break;
            case "perfect": first = 78; last = 82; break;
            case "rain": first = 83; last = 86; break;
            case "night": first = 87; last = 90; break;
            case "lights": first = 91; last = 94; break;
            case "toll": first = 95; last = 98; break;
            case "service": first = 99; last = 102; break;
            case "random": first = 103; last = 122; break;
            default: return false;
        }
        string[] extras167 = VoiceExtraFiles167(normalized);
        int baseCount = last - first + 1;
        int pick = _voiceRandom062.Next(0, Math.Max(1, baseCount + extras167.Length));
        if (pick >= baseCount && extras167.Length > 0)
        {
            string extra = extras167[pick - baseCount];
            bool ok = VoicePlayFile166(extra, interrupt);
            if (ok) ClientStore.Log("voz extra 1.0.67 [" + normalized + "]: " + Path.GetFileName(extra));
            return ok;
        }
        return VoicePlayId062(first + pick, interrupt);
    }
'@
$voiceText = Replace-Between $voiceText '    private bool VoicePlayGroup062(string group, bool interrupt = true)' '    private bool VoicePlayId062(int id, bool interrupt = true)' $groupMethod

foreach ($m in @('CurrentVersion = "1.0.67.0"','ObserveDamageDisplay064(d.CargoDamage')) { if ($mainText -notlike "*$m*") { throw "MainForm 1.0.67 sem $m" } }
foreach ($m in @('Cliente 1.0.67 TESTE','BuildVoiceSettings062(p);')) { if ($hubText -notlike "*$m*") { throw "Hub 1.0.67 sem $m" } }
foreach ($m in @('SUBSTITUIR VOZ','ADICIONAR FALAS','BAIXAR MODELO TXT','RESTAURAR BACKUP','VoiceReplacePackage167','VoiceAddExtras167','VoiceExtraFiles167','voz extra 1.0.67','VoiceOpenTemplate167')) { if ($voiceText -notlike "*$m*") { throw "Voice 1.0.67 sem $m" } }

Set-Content $main.FullName $mainText -Encoding UTF8
Set-Content $hub.FullName $hubText -Encoding UTF8
Set-Content $voice.FullName $voiceText -Encoding UTF8
if ($hub45) { Set-Content $hub45.FullName $hub45Text -Encoding UTF8 }
Write-Host 'GAT Telemetria 1.0.67 TESTE v2: gerenciador de pacotes, falas extras, modelo TXT e backup.'
