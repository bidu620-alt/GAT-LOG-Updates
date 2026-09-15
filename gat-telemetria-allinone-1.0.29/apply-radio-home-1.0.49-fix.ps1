param(
    [Parameter(Mandatory=$true)]
    [string]$Root
)

$ErrorActionPreference = 'Stop'
$rootPath = (Resolve-Path $Root).Path
$hub41 = Get-ChildItem $rootPath -Filter 'MainForm.Hub041.cs' -Recurse | Select-Object -First 1
$radio = Get-ChildItem $rootPath -Filter 'RadioForm.cs' -Recurse | Select-Object -First 1
if (-not $hub41 -or -not $radio) { throw 'Arquivos do hotfix 1.0.49 nao encontrados.' }

# Corrige a quebra de linha literal deixada pelo patch principal.
$s = Get-Content $hub41.FullName -Raw
$literal = 'return;`r`n        if (_hubRadio041 != null'
if ($s.Contains($literal)) {
    $s = $s.Replace($literal, "return;`r`n        if (_hubRadio041 != null")
}
if ($s -notlike '*ConfigureAccount049(_accountUser, _accountToken)*') { throw 'Hotfix 1.0.49 nao encontrou ConfigureAccount049.' }
Set-Content $hub41.FullName $s -Encoding UTF8

# O metodo SwitchWebModeAsync/RestorePlayerPageAsync pertencia ao Canal Web e foi
# removido pela 1.0.49. Elimina as duas referencias restantes para compilar apenas
# os modos Canal GAT e Meu Video.
$r = Get-Content $radio.FullName -Raw
$r = $r.Replace('_siteWeb.Click += async delegate { await SwitchWebModeAsync(); };', '_siteWeb.Click += delegate { };')
$legacyRestore = @'
        if (_webMode)
        {
            _webMode = false;
            await RestorePlayerPageAsync();
        }

'@
if ($r.Contains($legacyRestore.TrimEnd())) {
    $r = $r.Replace($legacyRestore.TrimEnd(), '        _webMode = false;')
}
$r = $r.Replace('        if (_webMode)`r`n        {`r`n            _webMode = false;`r`n            await RestorePlayerPageAsync();`r`n        }', '        _webMode = false;')
if ($r -like '*SwitchWebModeAsync()*' -or $r -like '*RestorePlayerPageAsync()*') {
    throw 'Hotfix 1.0.49 ainda encontrou referencia ativa ao Canal Web.'
}
Set-Content $radio.FullName $r -Encoding UTF8

Write-Host 'Hotfix 1.0.49: EnsureRadio e referencias legadas do Canal Web corrigidos.'
