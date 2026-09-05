param(
    [Parameter(Mandatory = $true)]
    [string]$Root
)

$ErrorActionPreference = 'Stop'

$main = Get-ChildItem -LiteralPath $Root -Filter 'MainForm.cs' -Recurse | Select-Object -First 1
if (-not $main) { throw 'MainForm.cs nao encontrado para aplicar branding BETA' }

$text = Get-Content -LiteralPath $main.FullName -Raw

function Replace-Required {
    param(
        [string]$InputText,
        [string]$Old,
        [string]$New,
        [string]$Label
    )

    if (-not $InputText.Contains($Old)) {
        throw "Marcador ausente no branding BETA: $Label"
    }

    return $InputText.Replace($Old, $New)
}

# Mantem a versao publica 1.0.32, mas usa uma revisao interna para que
# quem ja esta na 1.0.32 receba esta republicacao uma unica vez.
$text = Replace-Required $text `
    'private const string CurrentVersion = "1.0.32";' `
    'private const string CurrentVersion = "1.0.32.1";' `
    'revisao interna 1.0.32.1'

# Nome da janela: sem numero de versao.
$text = Replace-Required $text `
    'Text = "GAT Telemetria C# 1.0.32";' `
    'Text = "GAT Telemetria BETA";' `
    'titulo da janela'

# Cabecalho principal.
$text = Replace-Required $text `
    'Text = "GAT TELEMETRIA",' `
    'Text = "GAT TELEMETRIA BETA",' `
    'cabecalho principal'

# A numeracao fica somente no indicador do cliente. Usa regex para aceitar
# tanto CRLF quanto LF no fonte reconstruido pelo GitHub Actions.
$labelPattern = 'lblVersion = new Label\s*\{\s*Text = "GAT Telemetria C# 1\.0\.32",'
if (-not [regex]::IsMatch($text, $labelPattern)) {
    throw 'Marcador ausente no branding BETA: rotulo da versao do cliente'
}
$labelReplacement = "lblVersion = new Label`r`n`t`t{`r`n`t`t`tText = `"Cliente 1.0.32`","
$text = [regex]::Replace($text, $labelPattern, $labelReplacement, 1)

# Nao exibe a revisao tecnica 1.0.32.1 no botao de atualizacao.
$text = Replace-Required $text `
    'btnUpdate.Text = "ATUALIZAR PARA " + remoteVersion.Version;' `
    'btnUpdate.Text = "ATUALIZAR CLIENTE 1.0.32";' `
    'texto do botao de atualizacao'

# Nao exibe a revisao tecnica no dialogo. O fonte reconstruido pode variar
# nos escapes de quebra de linha; por isso trocamos somente o prefixo estavel.
$promptPattern = 'MessageBox\.Show\("Instalar GAT Telemetria " \+ _availableUpdate\.Version \+ "\?'
if (-not [regex]::IsMatch($text, $promptPattern)) {
    throw 'Marcador ausente no branding BETA: dialogo de atualizacao'
}
$text = [regex]::Replace(
    $text,
    $promptPattern,
    'MessageBox.Show("Instalar atualização do GAT Telemetria BETA?',
    1
)
$text = $text.Replace('"Atualização GAT Telemetria"', '"Atualização GAT Telemetria BETA"')

Set-Content -LiteralPath $main.FullName -Value $text -Encoding UTF8

$check = Get-Content -LiteralPath $main.FullName -Raw
foreach ($marker in @(
    'private const string CurrentVersion = "1.0.32.1";',
    'Text = "GAT Telemetria BETA";',
    'Text = "GAT TELEMETRIA BETA",',
    'Text = "Cliente 1.0.32",',
    'ATUALIZAR CLIENTE 1.0.32',
    'Instalar atualização do GAT Telemetria BETA?',
    'Atualização GAT Telemetria BETA'
)) {
    if (-not $check.Contains($marker)) {
        throw "Branding BETA incompleto: $marker"
    }
}

Write-Host 'Branding GAT Telemetria BETA aplicado; cliente publico 1.0.32, revisao interna 1.0.32.1.'
