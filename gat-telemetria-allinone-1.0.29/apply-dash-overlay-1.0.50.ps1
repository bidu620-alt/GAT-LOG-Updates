param(
    [Parameter(Mandatory=$true)]
    [string]$Root
)

$ErrorActionPreference = 'Stop'
$rootPath = (Resolve-Path $Root).Path
$main = Get-ChildItem $rootPath -Filter 'MainForm.cs' -Recurse | Select-Object -First 1
$hub41 = Get-ChildItem $rootPath -Filter 'MainForm.Hub041.cs' -Recurse | Select-Object -First 1
$hub44 = Get-ChildItem $rootPath -Filter 'MainForm.Hub044.cs' -Recurse | Select-Object -First 1
$hub45 = Get-ChildItem $rootPath -Filter 'MainForm.Hub045.cs' -Recurse | Select-Object -First 1
$radio = Get-ChildItem $rootPath -Filter 'RadioForm.cs' -Recurse | Select-Object -First 1
$bridge = Get-ChildItem $rootPath -Filter 'DashMediaBridge.cs' -Recurse | Select-Object -First 1
$truck = Get-ChildItem $rootPath -Filter 'TruckOverlay041.cs' -Recurse | Select-Object -First 1
$video = Get-ChildItem $rootPath -Filter 'VideoOverlay041.cs' -Recurse | Select-Object -First 1
if (-not $main -or -not $hub41 -or -not $hub44 -or -not $hub45 -or -not $radio -or -not $bridge -or -not $truck -or -not $video) {
    throw 'Fonte 1.0.49 incompleto para aplicar a atualizacao 1.0.50.'
}

$mainText = Get-Content $main.FullName -Raw
$hub41Text = Get-Content $hub41.FullName -Raw
$hub44Text = Get-Content $hub44.FullName -Raw
$hub45Text = Get-Content $hub45.FullName -Raw
$radioText = Get-Content $radio.FullName -Raw
$bridgeText = Get-Content $bridge.FullName -Raw
$truckText = Get-Content $truck.FullName -Raw
$videoText = Get-Content $video.FullName -Raw

# Versao 1.0.50 TESTE.
$mainText = $mainText.Replace('CurrentVersion = "1.0.49.0"', 'CurrentVersion = "1.0.50.0"')
$mainText = $mainText.Replace('Text = "Cliente 1.0.49"', 'Text = "Cliente 1.0.50"')
$mainText = $mainText.Replace('HUB 1.0.49:', 'HUB 1.0.50:')
foreach ($name in @('hub41Text','hub44Text','hub45Text','radioText')) {
    $v = Get-Variable $name -ValueOnly
    $v = $v.Replace('1.0.49', '1.0.50')
    Set-Variable $name $v
}

# GAT DASH / HUB: Canal Web deixa de existir. Mantem somente Canal GAT e Meu Video.
$hub41Text = $hub41Text.Replace('Canal GAT, Meu Vídeo e Canal Web', 'Canal GAT e Meu Vídeo')
$hub41Text = $hub41Text.Replace('if (mode == "gat" || mode == "mine" || mode == "web") File.WriteAllText(Path.Combine(Application.LocalUserAppDataPath, "radio-active-mode.txt"), mode);', 'if (mode == "gat" || mode == "mine") File.WriteAllText(Path.Combine(Application.LocalUserAppDataPath, "radio-active-mode.txt"), mode); else if (mode == "web") File.WriteAllText(Path.Combine(Application.LocalUserAppDataPath, "radio-active-mode.txt"), "gat");')

# Bridge do DASH: nao publica mais a fonte web e normaliza modo antigo para Canal GAT.
$bridgeText = [regex]::Replace($bridgeText, '(?m)^\s*\["web"\]\s*=\s*new JObject \{ \["url"\] = ReadLocal\("radio-web-url\.txt"\) \},\r?\n', '')
$bridgeText = $bridgeText.Replace('return mode == "mine" || mode == "web" ? mode : "gat";', 'return mode == "mine" ? "mine" : "gat";')

# VIDEO flutuante: remove qualquer botao/rota residual do Canal Web.
$videoText = $videoText.Replace('if (mode == "gat" || mode == "mine" || mode == "web")', 'if (mode == "gat" || mode == "mine")')
$videoText = $videoText.Replace("<button id='web' class='tab'>Canal Web</button>", '')
$videoText = $videoText.Replace("if(state.mode==='web')return(m.web||{}).url||'';", '')
$videoText = $videoText.Replace("['gat','mine','web'].forEach", "['gat','mine'].forEach")
$videoText = $videoText.Replace("const src=state.mode==='web'?url:(yt(url)||url);", "const src=(yt(url)||url);")
$videoText = $videoText.Replace("if(['gat','mine','web'].includes(String(m.activeMode||'')))state.mode=String(m.activeMode);", "if(['gat','mine'].includes(String(m.activeMode||'')))state.mode=String(m.activeMode);else if(String(m.activeMode||'')==='web')state.mode='gat';")

# SOBREPOSICAO DO CAMINHAO: movimento igual a uma janela normal/flutuante.
if ($truckText -notmatch 'using System\.Runtime\.InteropServices;') {
    $truckText = $truckText.Replace('using System.Net.Http;', "using System.Net.Http;`r`nusing System.Runtime.InteropServices;")
}

if ($truckText -notlike '*GAT_DRAG_050*') {
    $ctorNeedle = '        BuildUi();'
    if (-not $truckText.Contains($ctorNeedle)) { throw 'BuildUi do overlay do caminhao nao encontrado.' }
    $ctorPatch = @'
        BuildUi();
        // GAT_DRAG_050: permite arrastar pelo topo mesmo quando o clique cai nos Labels filhos.
        EnableDrag050(_header);
        EnableDrag050(_title);
        EnableDrag050(_sub);
        EnableDrag050(_connected);
'@
    $truckText = $truckText.Replace($ctorNeedle, $ctorPatch.TrimEnd())

    $methodNeedle = '    private void LayoutResponsive045()'
    $idx = $truckText.IndexOf($methodNeedle)
    if ($idx -lt 0) { throw 'LayoutResponsive045 nao encontrado no overlay.' }
    $dragCode = @'
    // GAT_DRAG_050
    private const int WM_NCLBUTTONDOWN_050 = 0x00A1;
    private const int HTCAPTION_050 = 2;

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    private void EnableDrag050(Control control)
    {
        if (control == null) return;
        control.Cursor = Cursors.SizeAll;
        control.MouseDown += delegate(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            ReleaseCapture();
            SendMessage(Handle, WM_NCLBUTTONDOWN_050, (IntPtr)HTCAPTION_050, IntPtr.Zero);
        };
    }

'@
    $truckText = $truckText.Insert($idx, $dragCode)
}

# Validacoes do hotfix.
if ($mainText -notlike '*CurrentVersion = "1.0.50.0"*') { throw 'Versao 1.0.50 nao aplicada.' }
foreach ($m in @('GAT_DRAG_050','EnableDrag050(_header)','ReleaseCapture()','SendMessage(Handle')) {
    if ($truckText -notlike "*$m*") { throw "Overlay 1.0.50 sem $m" }
}
if ($hub41Text -like '*Canal GAT, Meu Vídeo e Canal Web*') { throw 'Hub ainda exibe Canal Web.' }
if ($videoText.Contains("id='web' class='tab'") -or $videoText.Contains("['gat','mine','web']")) { throw 'Overlay de video ainda contem Canal Web.' }
if ($bridgeText -like '*radio-web-url.txt*') { throw 'Bridge do DASH ainda publica Canal Web.' }

Set-Content $main.FullName $mainText -Encoding UTF8
Set-Content $hub41.FullName $hub41Text -Encoding UTF8
Set-Content $hub44.FullName $hub44Text -Encoding UTF8
Set-Content $hub45.FullName $hub45Text -Encoding UTF8
Set-Content $radio.FullName $radioText -Encoding UTF8
Set-Content $bridge.FullName $bridgeText -Encoding UTF8
Set-Content $truck.FullName $truckText -Encoding UTF8
Set-Content $video.FullName $videoText -Encoding UTF8

Write-Host 'GAT Telemetria 1.0.50: Canal Web removido do GAT DASH e overlay do caminhao com arraste corrigido.'
