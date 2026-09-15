param(
    [Parameter(Mandatory=$true)]
    [string]$Root
)

$ErrorActionPreference = 'Stop'
$rootPath = (Resolve-Path $Root).Path
$hub41 = Get-ChildItem $rootPath -Filter 'MainForm.Hub041.cs' -Recurse | Select-Object -First 1
if (-not $hub41) { throw 'MainForm.Hub041.cs nao encontrado no hotfix 1.0.49.' }
$s = Get-Content $hub41.FullName -Raw
$literal = 'return;`r`n        if (_hubRadio041 != null'
if ($s.Contains($literal)) {
    $s = $s.Replace($literal, "return;`r`n        if (_hubRadio041 != null")
}
if ($s -notlike '*ConfigureAccount049(_accountUser, _accountToken)*') { throw 'Hotfix 1.0.49 nao encontrou ConfigureAccount049.' }
Set-Content $hub41.FullName $s -Encoding UTF8
Write-Host 'Hotfix 1.0.49: quebra de linha do EnsureRadio041 corrigida.'
