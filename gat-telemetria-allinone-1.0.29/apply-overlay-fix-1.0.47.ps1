param(
    [Parameter(Mandatory=$true)]
    [string]$Root
)

$ErrorActionPreference = 'Stop'
$rootPath = (Resolve-Path $Root).Path
$main = Get-ChildItem $rootPath -Filter 'MainForm.cs' -Recurse | Select-Object -First 1
$hub44 = Get-ChildItem $rootPath -Filter 'MainForm.Hub044.cs' -Recurse | Select-Object -First 1
$hub45 = Get-ChildItem $rootPath -Filter 'MainForm.Hub045.cs' -Recurse | Select-Object -First 1
$truck = Get-ChildItem $rootPath -Filter 'TruckOverlay041.cs' -Recurse | Select-Object -First 1
if (-not $main -or -not $hub44 -or -not $hub45 -or -not $truck) {
    throw 'Fonte 1.0.46 incompleto para aplicar a atualizacao 1.0.47.'
}

$mainText = Get-Content $main.FullName -Raw
$hub44Text = Get-Content $hub44.FullName -Raw
$hub45Text = Get-Content $hub45.FullName -Raw
$truckText = Get-Content $truck.FullName -Raw

$mainText = $mainText.Replace('CurrentVersion = "1.0.46.0"', 'CurrentVersion = "1.0.47.0"')
$mainText = $mainText.Replace('Text = "Cliente 1.0.46"', 'Text = "Cliente 1.0.47"')
$mainText = $mainText.Replace('HUB 1.0.46:', 'HUB 1.0.47:')
$hub44Text = $hub44Text.Replace('GAT Telemetria BETA 1.0.46', 'GAT Telemetria BETA 1.0.47')
$hub44Text = $hub44Text.Replace('Cliente 1.0.46 TESTE', 'Cliente 1.0.47 TESTE')
$hub45Text = $hub45Text.Replace('GAT Telemetria BETA 1.0.46', 'GAT Telemetria BETA 1.0.47')
$hub45Text = $hub45Text.Replace('Cliente: 1.0.46 TESTE', 'Cliente: 1.0.47 TESTE')

# Corrige o bug de cultura pt-BR: valores como 81,10 estavam sendo lidos como 8110.
$dPattern = '(?s)    private static double D\(JObject j, params string\[\] paths\)\s*\{.*?\r?\n    \}\r?\n\r?\n    private static bool B'
$dReplacement = @'
    private static double D(JObject j, params string[] paths)
    {
        foreach (string p in paths)
        {
            try
            {
                JToken t = j.SelectToken(p);
                if (t == null) continue;

                if (t.Type == JTokenType.Integer || t.Type == JTokenType.Float)
                    return t.Value<double>();

                string raw = Convert.ToString(t, CultureInfo.InvariantCulture) ?? "";
                double v;
                if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out v)) return v;
                if (double.TryParse(raw, NumberStyles.Float, new CultureInfo("pt-BR"), out v)) return v;
            }
            catch { }
        }
        return 0;
    }

    private static bool IsValidRoadSpeed047(double value)
    {
        return !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0 && value <= 300;
    }

    private static bool B
'@
$newTruck = [regex]::Replace($truckText, $dPattern, $dReplacement, 1)
if ($newTruck -eq $truckText) { throw 'Metodo numerico D() nao encontrado no overlay.' }
$truckText = $newTruck

# Nao deixa um valor de telemetria invalido contaminar a media/ETA.
$oldSpeed = 'double speed = Math.Abs(D(j, "truck.speed", "Truck.Speed"));'
$newSpeed = 'double speed = Math.Abs(D(j, "truck.speed", "Truck.Speed")); if (!IsValidRoadSpeed047(speed)) speed = 0;'
if (-not $truckText.Contains($oldSpeed)) { throw 'Leitura de velocidade do overlay nao encontrada.' }
$truckText = $truckText.Replace($oldSpeed, $newSpeed)
$truckText = $truckText.Replace('if (speed > 5) _avgSpeed = _avgSpeed <= 0 ? speed : (_avgSpeed * 0.94 + speed * 0.06);', 'if (IsValidRoadSpeed047(speed) && speed > 5) _avgSpeed = _avgSpeed <= 0 ? speed : (_avgSpeed * 0.94 + speed * 0.06);')
$truckText = $truckText.Replace('_average.Text = "VELOCIDADE MÉDIA\r\n" + (_avgSpeed > 1 ? Math.Round(_avgSpeed) + " km/h" : "—");', '_average.Text = "VELOCIDADE MÉDIA\r\n" + (IsValidRoadSpeed047(_avgSpeed) && _avgSpeed > 1 ? Math.Round(_avgSpeed) + " km/h" : "—");')

# Mantem o titulo da distancia em uma linha para o valor nao ficar cortado no modo estreito.
$truckText = $truckText.Replace('_remaining.Text = "DISTÂNCIA RESTANTE\r\n—";', '_remaining.Text = "DIST. RESTANTE\r\n—";')
$truckText = $truckText.Replace('_remaining.Text = "DISTÂNCIA RESTANTE\r\n" + (remain > 0 ? Math.Round(remain) + " km" : "—");', '_remaining.Text = "DIST. RESTANTE\r\n" + (remain > 0 ? Math.Round(remain) + " km" : "—");')
$truckText = $truckText.Replace('_jobPanel.SetBounds(pad, top, usable, 112);', '_jobPanel.SetBounds(pad, top, usable, 124);')
$truckText = $truckText.Replace('_weight.SetBounds(0, 60, half, 52);', '_weight.SetBounds(0, 60, half, 60);')
$truckText = $truckText.Replace('_remaining.SetBounds(half + gap, 60, half, 52);', '_remaining.SetBounds(half + gap, 60, half, 60);')

# ETA somente com distancia e media validas.
$truckText = $truckText.Replace('if (remainKm <= 0 || avgSpeed < 10) return "—";', 'if (remainKm <= 0 || !IsValidRoadSpeed047(avgSpeed) || avgSpeed < 10) return "—";')

foreach ($m in @('CurrentVersion = "1.0.47.0"')) {
    if ($mainText -notlike "*$m*") { throw "MainForm 1.0.47 sem $m" }
}
foreach ($m in @('JTokenType.Integer','IsValidRoadSpeed047','DIST. RESTANTE','TEMPO ESTIMADO')) {
    if ($truckText -notlike "*$m*") { throw "Overlay 1.0.47 sem $m" }
}

Set-Content $main.FullName $mainText -Encoding UTF8
Set-Content $hub44.FullName $hub44Text -Encoding UTF8
Set-Content $hub45.FullName $hub45Text -Encoding UTF8
Set-Content $truck.FullName $truckText -Encoding UTF8

Write-Host 'GAT Telemetria 1.0.47: velocidade, media, distancia restante e ETA do overlay corrigidos.'