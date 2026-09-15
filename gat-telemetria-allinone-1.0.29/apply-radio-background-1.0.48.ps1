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
if (-not $main -or -not $hub41 -or -not $hub44 -or -not $hub45) {
    throw 'Fonte 1.0.47 incompleto para aplicar a atualizacao 1.0.48.'
}

$mainText = Get-Content $main.FullName -Raw
$hub41Text = Get-Content $hub41.FullName -Raw
$hub44Text = Get-Content $hub44.FullName -Raw
$hub45Text = Get-Content $hub45.FullName -Raw

$mainText = $mainText.Replace('CurrentVersion = "1.0.47.0"', 'CurrentVersion = "1.0.48.0"')
$mainText = $mainText.Replace('Text = "Cliente 1.0.47"', 'Text = "Cliente 1.0.48"')
$mainText = $mainText.Replace('HUB 1.0.47:', 'HUB 1.0.48:')
$hub44Text = $hub44Text.Replace('GAT Telemetria BETA 1.0.47', 'GAT Telemetria BETA 1.0.48')
$hub44Text = $hub44Text.Replace('Cliente 1.0.47 TESTE', 'Cliente 1.0.48 TESTE')
$hub45Text = $hub45Text.Replace('GAT Telemetria BETA 1.0.47', 'GAT Telemetria BETA 1.0.48')
$hub45Text = $hub45Text.Replace('Cliente: 1.0.47 TESTE', 'Cliente: 1.0.48 TESTE')

# O RadioForm ficava dentro da pagina "radio". Ao trocar de aba, o Hub marcava
# essa pagina como Visible=false. O WebView2/YouTube podia suspender a midia e,
# ao voltar, a playlist acabava recarregada desde a primeira faixa.
#
# Na 1.0.48, depois que a Radio for aberta uma vez, a pagina continua visivel
# no fundo (SendToBack). Assim o mesmo WebView2 permanece vivo, tocando e com a
# mesma posicao da playlist. Trocar de aba nao envia pause nem recarrega a fonte.
$showStart = $hub41Text.IndexOf('    private void ShowHubPage041(string key)')
$showEnd = $hub41Text.IndexOf('    private Panel Home041()', $showStart)
if ($showStart -lt 0 -or $showEnd -lt 0) { throw 'ShowHubPage041 nao encontrado.' }

$newShow = @'
    private void ShowHubPage041(string key)
    {
        bool radioAlive = _hubRadio041 != null && !_hubRadio041.IsDisposed;
        foreach (var x in _hubPages041)
        {
            if (x.Key == "radio" && radioAlive)
            {
                // Mantem o player vivo por tras das outras paginas para a musica continuar.
                x.Value.Visible = true;
                if (key != "radio") x.Value.SendToBack();
            }
            else
            {
                x.Value.Visible = x.Key == key;
            }
        }

        foreach (var x in _hubNav041)
        {
            bool on = x.Key == key;
            x.Value.BackColor = on ? Color.FromArgb(14, 74, 133) : Color.FromArgb(8, 29, 50);
            x.Value.ForeColor = on ? Color.White : Color.FromArgb(210, 225, 242);
        }

        if (key == "radio")
        {
            EnsureRadio041();
            Panel radioPage;
            if (_hubPages041.TryGetValue("radio", out radioPage)) radioPage.Visible = true;
        }

        Panel activePage;
        if (_hubPages041.TryGetValue(key, out activePage)) activePage.BringToFront();
    }

'@
$hub41Text = $hub41Text.Substring(0, $showStart) + $newShow + $hub41Text.Substring($showEnd)

foreach ($m in @('CurrentVersion = "1.0.48.0"')) {
    if ($mainText -notlike "*$m*") { throw "MainForm 1.0.48 sem $m" }
}
foreach ($m in @('bool radioAlive','SendToBack()','TryGetValue("radio"','EnsureRadio041()')) {
    if ($hub41Text -notlike "*$m*") { throw "Radio em segundo plano 1.0.48 sem $m" }
}

Set-Content $main.FullName $mainText -Encoding UTF8
Set-Content $hub41.FullName $hub41Text -Encoding UTF8
Set-Content $hub44.FullName $hub44Text -Encoding UTF8
Set-Content $hub45.FullName $hub45Text -Encoding UTF8

Write-Host 'GAT Telemetria 1.0.48: Radio GAT continua tocando ao mudar de aba, sem reiniciar playlist.'
