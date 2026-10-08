param([Parameter(Mandatory=$true)][string]$Root)
$ErrorActionPreference='Stop'
$file=Get-ChildItem (Resolve-Path $Root).Path -Filter 'TelemetryEngine.cs' -Recurse | Select-Object -First 1
if(-not $file){ throw 'TelemetryEngine.cs ausente' }
$text=Get-Content $file.FullName -Raw
if($text -like '*NormalizeRoadLimit(JObject m)*'){ Write-Host 'Memoria de limite ja aplicada'; exit 0 }
$text=$text.Replace('private DateTime _lastStartAttempt = DateTime.MinValue;','private DateTime _lastStartAttempt = DateTime.MinValue;')
$text=$text.Replace('private DateTime _lastStartAttempt = DateTime.MinValue;','private DateTime _lastStartAttempt = DateTime.MinValue;'+[Environment]::NewLine+'\tprivate static readonly object _roadLimitSync = new object();'+[Environment]::NewLine+'\tprivate static double _lastGameRoadLimitKmh = double.NaN;')
$old=@'
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
				m["speed_limit_kmh"] = 56.0;
				m["speed_limit_source"] = "fallback_no_sign";
				m["speed_limit_is_fallback"] = true;
			}
'@
$new='			NormalizeRoadLimit(m);'
if(-not $text.Contains($old)){ throw 'Bloco original de limite nao encontrado' }
$text=$text.Replace($old,$new)
$marker=[Environment]::NewLine+'\tprivate static void AddDamageAliases(JObject m)'
$method=@'

	private static void NormalizeRoadLimit(JObject m)
	{
		bool connected = TryBoolean(m, "game.connected", "gameConnected", "connected");
		lock (_roadLimitSync)
		{
			if (!connected)
			{
				_lastGameRoadLimitKmh = double.NaN;
				return;
			}
			bool hasRaw = TryAny(m, out var rawLimit, "navigation.speedLimit", "Navigation.SpeedLimit", "speedLimit", "navigation.speed_limit");
			if (hasRaw && rawLimit > 0.0 && rawLimit < 3.0) rawLimit *= 3.6;
			bool validRaw = hasRaw && !double.IsNaN(rawLimit) && !double.IsInfinity(rawLimit) && rawLimit > 0.0 && rawLimit <= 250.0;
			if (validRaw) _lastGameRoadLimitKmh = rawLimit;
			double effective = validRaw ? rawLimit : _lastGameRoadLimitKmh;
			bool unavailable = double.IsNaN(effective) || double.IsInfinity(effective) || effective <= 0.0;
			if (unavailable) effective = 0.0;
			m["speed_limit_kmh"] = effective;
			m["speed_limit_source"] = unavailable ? "unavailable_no_previous_limit" : "game";
			m["speed_limit_is_fallback"] = false;
			m["speed_limit_preserved"] = !validRaw && !unavailable;
		}
	}
'@
if(-not $text.Contains($marker)){ throw 'Ponto de insercao nao encontrado' }
$text=$text.Replace($marker,$method+$marker)
Set-Content $file.FullName $text -Encoding UTF8
Write-Host 'Limite valido preservado sem temporizador; desconexao limpa a memoria.'
