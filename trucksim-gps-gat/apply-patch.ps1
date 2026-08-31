param([Parameter(Mandatory=$true)][string]$Root)
$ErrorActionPreference = 'Stop'

$model = Join-Path $Root 'source/Funbit.Ets.Telemetry.Server/Data/TelemetryV1.cs'
$reader = Join-Path $Root 'source/Funbit.Ets.Telemetry.Server/Data/ScsTelemetryDataReader.cs'

$s = Get-Content $model -Raw
$anchor = '        public float FuelRange { get; set; }'
if ($s -notmatch 'WearEngine') {
    if (-not $s.Contains($anchor)) { throw 'Ponto FuelRange nao encontrado em TelemetryV1.cs' }
    $insert = @"
        public float FuelRange { get; set; }

        // GAT Telemetria: desgaste real do caminhao (0..1)
        public float WearEngine { get; set; }
        public float WearTransmission { get; set; }
        public float WearCabin { get; set; }
        public float WearChassis { get; set; }
        public float WearWheels { get; set; }
"@
    $s = $s.Replace($anchor, $insert.TrimEnd())
    Set-Content $model $s -Encoding UTF8
}

$s = Get-Content $reader -Raw
$anchor2 = '                FuelRange = dash?.FuelValue?.Range ?? 0f,'
if ($s -notmatch 'WearEngine = curr') {
    if (-not $s.Contains($anchor2)) { throw 'Ponto FuelRange nao encontrado em ScsTelemetryDataReader.cs' }
    $insert2 = @"
                FuelRange = dash?.FuelValue?.Range ?? 0f,

                // GAT Telemetria: exposicao dos desgastes reais no REST v1
                WearEngine = curr?.DamageValues?.Engine ?? 0f,
                WearTransmission = curr?.DamageValues?.Transmission ?? 0f,
                WearCabin = curr?.DamageValues?.Cabin ?? 0f,
                WearChassis = curr?.DamageValues?.Chassis ?? 0f,
                WearWheels = curr?.DamageValues?.WheelsAvg ?? 0f,
"@
    $s = $s.Replace($anchor2, $insert2.TrimEnd())
    Set-Content $reader $s -Encoding UTF8
}

$verifyModel = Get-Content $model -Raw
$verifyReader = Get-Content $reader -Raw
foreach ($name in @('WearEngine','WearTransmission','WearCabin','WearChassis','WearWheels')) {
    if (-not $verifyModel.Contains("public float $name")) { throw "Campo $name nao foi criado" }
}
foreach ($name in @('WearEngine','WearTransmission','WearCabin','WearChassis','WearWheels')) {
    if (-not $verifyReader.Contains("$name = curr")) { throw "Mapeamento $name nao foi criado" }
}
Write-Host 'Patch GAT aplicado ao TruckSim GPS com sucesso.'
