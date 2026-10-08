param([Parameter(Mandatory=$true)][string]$Root)
$ErrorActionPreference = 'Stop'

$journal = Get-ChildItem -LiteralPath $Root -Filter 'TripJournal.cs' -Recurse | Select-Object -First 1
if (-not $journal) { throw 'TripJournal.cs nao encontrado na fonte reconstruida.' }
$text = Get-Content -LiteralPath $journal.FullName -Raw

$oldRead = 'double speedKmh = Math.Abs(DoubleAny(telemetry, "speed_kmh", "truck.speedKmh", "truck.speed_kmh", "truck.speed"));'
$newRead = @'
double speedKmh = Math.Abs(DoubleAny(telemetry, "speed_kmh", "truck.speedKmh", "truck.speed_kmh", "truck.speed"));
		double roadLimitKmh = DoubleAny(telemetry, "speed_limit_kmh");
		if (!double.IsNaN(roadLimitKmh) && roadLimitKmh > 0.0 && roadLimitKmh < 3.0)
		{
			roadLimitKmh *= 3.6;
		}
		if ((double.IsNaN(roadLimitKmh) || double.IsInfinity(roadLimitKmh) || roadLimitKmh <= 0.0 || roadLimitKmh > 250.0)
			&& BoolAny(telemetry, "game.connected", "gameConnected", "connected"))
		{
			roadLimitKmh = 60.0;
		}
'@.TrimEnd()
if ($text.Contains($oldRead) -and $text -notlike '*roadLimitKmh = 60.0*') { $text = $text.Replace($oldRead, $newRead) }
elseif ($text -notlike '*double roadLimitKmh = DoubleAny(telemetry*') { throw 'Leitura da velocidade no TripJournal nao reconhecida.' }

$text = $text.Replace('UpdateSpeedFine(_state.ActiveTrip, speedKmh);', 'UpdateSpeedFine(_state.ActiveTrip, speedKmh, roadLimitKmh);')
$text = $text.Replace('UpdateSpeedFine(activeTrip, speedKmh);', 'UpdateSpeedFine(activeTrip, speedKmh, roadLimitKmh);')
$oldSignature = 'private void UpdateSpeedFine(TripReceipt trip, double speedKmh)'
$newSignature = 'private void UpdateSpeedFine(TripReceipt trip, double speedKmh, double roadLimitKmh)'
if ($text.Contains($oldSignature)) { $text = $text.Replace($oldSignature, $newSignature) }
elseif ($text -notlike '*private void UpdateSpeedFine(TripReceipt trip, double speedKmh, double roadLimitKmh)*') { throw 'Assinatura de UpdateSpeedFine nao reconhecida.' }

$oldThreshold = 'else if (speedKmh <= 91.0)'
$newThreshold = 'else if (double.IsNaN(roadLimitKmh) || double.IsInfinity(roadLimitKmh) || roadLimitKmh <= 0.0 || speedKmh <= roadLimitKmh + 1.0)'
if ($text.Contains($oldThreshold)) { $text = $text.Replace($oldThreshold, $newThreshold) }
elseif ($text -notlike '*speedKmh <= roadLimitKmh + 1.0*') { throw 'Regra fixa de 91 km/h nao encontrada no TripJournal.' }

if ($text -notlike '*roadLimitKmh + 1.0*' -or $text -like '*UpdateSpeedFine(_state.ActiveTrip, speedKmh);*' -or $text -notlike '*roadLimitKmh = 60.0*') {
    throw 'A regra de tolerancia por limite da rodovia nao foi aplicada corretamente.'
}
Set-Content -LiteralPath $journal.FullName -Value $text -Encoding UTF8

$engine = Get-ChildItem -LiteralPath $Root -Filter 'TelemetryEngine.cs' -Recurse | Select-Object -First 1
if (-not $engine) { throw 'TelemetryEngine.cs nao encontrado na fonte reconstruida.' }
$engineText = Get-Content -LiteralPath $engine.FullName -Raw
$engineAnchor = @'
			if (TryAny(m, out var value3, "truck.speedKmh", "truck.speed_kmh", "speed_kmh", "truck.speed"))
			{
				m["speed_kmh"] = Math.Abs(value3);
			}
'@
$engineInsert = @'
			if (TryAny(m, out var value3, "truck.speedKmh", "truck.speed_kmh", "speed_kmh", "truck.speed"))
			{
				m["speed_kmh"] = Math.Abs(value3);
			}
			bool gameConnected = TryBoolean(m, "game.connected", "gameConnected", "connected");
			bool hasRoadLimit = TryAny(m, out var roadLimitKmh, "navigation.speedLimit", "Navigation.SpeedLimit", "speed_limit_kmh", "speedLimit");
			if (hasRoadLimit && roadLimitKmh > 0.0 && roadLimitKmh < 3.0) roadLimitKmh *= 3.6;
			bool validRoadLimit = hasRoadLimit && !double.IsNaN(roadLimitKmh) && !double.IsInfinity(roadLimitKmh) && roadLimitKmh > 0.0 && roadLimitKmh <= 250.0;
			if (validRoadLimit)
			{
				m["speed_limit_kmh"] = roadLimitKmh;
				m["speed_limit_source"] = "game";
				m["speed_limit_is_fallback"] = false;
			}
			else if (gameConnected)
			{
				m["speed_limit_kmh"] = 60.0;
				m["speed_limit_source"] = "fallback_no_sign";
				m["speed_limit_is_fallback"] = true;
			}
'@
if ($engineText.Contains($engineAnchor) -and $engineText -notlike '*speed_limit_source*') { $engineText = $engineText.Replace($engineAnchor, $engineInsert) }
elseif ($engineText -notlike '*speed_limit_source*') { throw 'Normalize de velocidade no TelemetryEngine nao reconhecido.' }
$booleanMethod = @'
	private static bool TryBoolean(JObject m, params string[] paths)
	{
		foreach (string path in paths)
		{
			JToken token = m.SelectToken(path, errorWhenNoMatch: false);
			if (token == null || token.Type == JTokenType.Null) continue;
			if (token.Type == JTokenType.Boolean) return token.Value<bool>();
			if (bool.TryParse(token.ToString(), out var result)) return result;
			if (token.Type == JTokenType.Integer || token.Type == JTokenType.Float) return token.Value<double>() != 0.0;
		}
		return false;
	}

'@
if ($engineText -notlike '*private static bool TryBoolean(JObject m*') {
    $engineText = $engineText.Replace("`tpublic void Dispose()", $booleanMethod + "`tpublic void Dispose()")
}
if ($engineText -notlike '*speed_limit_is_fallback*' -or $engineText -notlike '*TryBoolean(JObject m*') { throw 'Fallback nao foi aplicado ao TelemetryEngine.' }
Set-Content -LiteralPath $engine.FullName -Value $engineText -Encoding UTF8
Write-Host 'TripJournal: limite real ou padrao de 60 km/h quando conectado, com tolerancia +1; sem alterar dados historicos.'
