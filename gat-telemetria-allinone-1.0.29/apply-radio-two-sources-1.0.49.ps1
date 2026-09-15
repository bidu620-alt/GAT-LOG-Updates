# GAT Telemetria 1.0.49 - simplifica Radio GAT para duas fontes:
# 1) CANAL GAT = fonte oficial para todos
# 2) MEU VIDEO = fonte individual/local
# Remove CANAL WEB da interface e do bridge.

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$main = Get-ChildItem $root -Recurse -Filter MainForm.cs | Select-Object -First 1
$hub41 = Get-ChildItem $root -Recurse -Filter '*Hub*41*.cs' | Select-Object -First 1
$bridge = Join-Path $root 'DashMediaBridge.cs'

if (-not $main) { throw 'MainForm.cs nao encontrado.' }
if (-not $hub41) { throw 'Arquivo Hub 1.0.41+ nao encontrado.' }
if (-not (Test-Path $bridge)) { throw 'DashMediaBridge.cs nao encontrado.' }

$mainText = Get-Content $main.FullName -Raw -Encoding UTF8
$hubText = Get-Content $hub41.FullName -Raw -Encoding UTF8
$bridgeText = Get-Content $bridge -Raw -Encoding UTF8

$mainText = $mainText.Replace('CurrentVersion = "1.0.48.0"', 'CurrentVersion = "1.0.49.0"')
$mainText = $mainText.Replace('Text = "Cliente 1.0.48"', 'Text = "Cliente 1.0.49"')
$mainText = $mainText.Replace('HUB 1.0.48:', 'HUB 1.0.49:')

# Interface: remove o botao CANAL WEB e deixa somente CANAL GAT + MEU VIDEO.
$hubText = $hubText.Replace('Text = "CANAL WEB"', 'Text = "CANAL WEB REMOVIDO"')
$hubText = $hubText.Replace('Text = "☻ CANAL WEB"', 'Text = "CANAL WEB REMOVIDO"')
$hubText = $hubText.Replace('Text = "● CANAL WEB"', 'Text = "CANAL WEB REMOVIDO"')

# Tenta remover blocos comuns de criacao do botao web, se existirem.
$patterns = @(
    '(?ms)^\s*var\s+btnWeb\s*=.*?;\s*btnWeb\..*?Controls\.Add\(btnWeb\);\s*',
    '(?ms)^\s*Button\s+btnWeb\s*=.*?;\s*btnWeb\..*?Controls\.Add\(btnWeb\);\s*',
    '(?ms)^\s*var\s+webButton\s*=.*?;\s*webButton\..*?Controls\.Add\(webButton\);\s*'
)
foreach ($p in $patterns) { $hubText = [regex]::Replace($hubText, $p, '') }

# Como fallback, esconde qualquer botao residual de Canal Web ao montar a pagina.
if ($hubText -notmatch 'GAT_RADIO_TWO_SOURCES_V149') {
    $marker = @'
// GAT_RADIO_TWO_SOURCES_V149
private void HideLegacyRadioWebButtons149(Control root)
{
    if (root == null) return;
    foreach (Control c in root.Controls)
    {
        if ((c.Text ?? "").IndexOf("CANAL WEB", StringComparison.OrdinalIgnoreCase) >= 0)
            c.Visible = false;
        if (c.HasChildren) HideLegacyRadioWebButtons149(c);
    }
}

'@
    $insertAt = $hubText.LastIndexOf('}')
    if ($insertAt -gt 0) { $hubText = $hubText.Substring(0,$insertAt) + $marker + $hubText.Substring($insertAt) }

    # chama depois de abrir a pagina radio
    $hubText = $hubText.Replace('EnsureRadio041();', 'EnsureRadio041(); HideLegacyRadioWebButtons149(_hubPages041["radio"]);')
}

# Bridge: deixa somente canalGat e myVideo no contrato de midia.
$bridgeText = $bridgeText.Replace(',\n                            ["web"] = new JObject { ["url"] = ReadLocal("radio-web-url.txt") }', '')
$bridgeText = $bridgeText.Replace(',`r`n                            ["web"] = new JObject { ["url"] = ReadLocal("radio-web-url.txt") }', '')
$bridgeText = $bridgeText.Replace('["web"] = new JObject { ["url"] = ReadLocal("radio-web-url.txt") },', '')

if ($mainText -notlike '*1.0.49.0*') { throw 'Versao 1.0.49 nao aplicada.' }
if ($hubText -notlike '*GAT_RADIO_TWO_SOURCES_V149*') { throw 'Patch de duas fontes nao aplicado.' }

Set-Content $main.FullName $mainText -Encoding UTF8
Set-Content $hub41.FullName $hubText -Encoding UTF8
Set-Content $bridge $bridgeText -Encoding UTF8

Write-Host 'GAT Telemetria 1.0.49: Radio GAT simplificada para Canal GAT (todos) + Meu Video (individual). Canal Web removido.'
