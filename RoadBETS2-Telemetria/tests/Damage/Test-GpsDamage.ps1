param([Parameter(Mandatory=$true)][string]$Root)
$ErrorActionPreference = 'Stop'
[void][Reflection.Assembly]::LoadFrom("$root/dependencies/Newtonsoft.Json.dll")
$asm = [Reflection.Assembly]::LoadFrom("$root/TruckSimGPS_Server.exe")
$scsType = $asm.GetType('SCSSdkClient.Object.SCSTelemetry', $true)
$readerType = $asm.GetType('Funbit.Ets.Telemetry.Server.Data.ScsTelemetryDataReader', $true)
$map = $readerType.GetMethod('MapTruck', [Reflection.BindingFlags]'NonPublic,Static')
function Set-Value($obj, $name, $value) {
  $obj.GetType().GetProperty($name).GetSetMethod($true).Invoke($obj, @($value)) | Out-Null
}
$sample = [Activator]::CreateInstance($scsType)
Set-Value $sample 'SdkActive' $true
$damage = $sample.TruckValues.CurrentValues.DamageValues
$names = @('Engine','Transmission','Cabin','Chassis','WheelsAvg')
$outputs = @('WearEngine','WearTransmission','WearCabin','WearChassis','WearWheels')
for ($i=0; $i -lt 5; $i++) { Set-Value $damage $names[$i] ([single](($i+1)/100.0)) }
$truck = $map.Invoke($null,@($sample))
for ($i=0; $i -lt 5; $i++) {
  $actual = $truck.($outputs[$i])
  if ($null -eq $actual -or [Math]::Abs([double]$actual-(($i+1)/100.0)) -gt 0.000001) { throw "Incorrect mapping: $($outputs[$i])" }
}
Set-Value $damage 'Engine' ([single]0)
$zero = $map.Invoke($null,@($sample))
if ($null -eq $zero.WearEngine -or $zero.WearEngine -ne 0) { throw 'Real zero was lost' }
Set-Value $sample 'SdkActive' $false
$inactive = $map.Invoke($null,@($sample))
foreach ($name in $outputs) { if ($null -ne $inactive.$name) { throw "Inactive SDK falsely reported damage: $name" } }
$missing = $map.Invoke($null,[object[]]@($null))
foreach ($name in $outputs) { if ($null -ne $missing.$name) { throw "Missing sample falsely reported damage: $name" } }
[PSCustomObject]@{Result='PASS';Version=$asm.GetName().Version.ToString();Mappings=5;Checks=@('five nonzero components','real zero preserved','inactive SDK returns null','missing sample returns null')} | ConvertTo-Json -Depth 4