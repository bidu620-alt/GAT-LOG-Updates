param([Parameter(Mandatory=$true)][string]$Root)

$main = Get-ChildItem $Root -Filter 'MainForm.cs' -Recurse | Select-Object -First 1
if (-not $main) { throw 'MainForm.cs nao encontrado para aplicar sensor de viagem v2' }
$text = Get-Content $main.FullName -Raw
$startMarker = 'private JObject StabilizeJobTelemetry(JObject tele)'
$endMarker = 'private static void CopyValue'
$start = $text.IndexOf($startMarker, [StringComparison]::Ordinal)
if ($start -lt 0) { throw 'Metodo StabilizeJobTelemetry nao encontrado' }
$end = $text.IndexOf($endMarker, $start, [StringComparison]::Ordinal)
if ($end -lt 0) { throw 'Fim do metodo StabilizeJobTelemetry nao encontrado' }

$newMethod = @'
private JObject StabilizeJobTelemetry(JObject tele)
	{
		// job-v2: o GAT Telemetria apenas observa o ETS2.
		// A Central GAT e a unica autoridade que decide entregue x cancelado.
		if (tele == null)
		{
			return null;
		}

		string cargo = TextAny(tele, "cargo_name", "job.cargoName", "job.cargo");
		string cargoId = TextAny(tele, "cargo_id", "job.cargoId", "Job.CargoId");
		string source = TextAny(tele, "source_city", "job.sourceCity", "Job.SourceCity");
		string destination = TextAny(tele, "destination_city", "job.destinationCity", "Job.DestinationCity");
		double mass = NumberAny(tele, "mass_kg", "cargoMass", "cargo_mass", "job.cargoMass", "Job.CargoMass");
		double planned = NumberAny(tele, "planned_distance_km", "job.plannedDistanceKm", "Job.PlannedDistanceKm");
		double remaining = NumberAny(tele, "remaining_km");
		bool rawOnJob = BoolAny(tele, "gameplay.onJob", "onJob", "job.onJob", "job.active");
		bool rawHasJob = rawOnJob && (!string.IsNullOrWhiteSpace(cargo) || !string.IsNullOrWhiteSpace(cargoId)) && mass > 0.0;

		if (rawHasJob)
		{
			string oldCargoId = (_latchedJob == null) ? string.Empty : TextAny(_latchedJob, "cargo_id");
			string oldSource = (_latchedJob == null) ? string.Empty : TextAny(_latchedJob, "source_city");
			string oldDestination = (_latchedJob == null) ? string.Empty : TextAny(_latchedJob, "destination_city");
			bool sameRawJob = _latchedJob != null &&
				(string.IsNullOrWhiteSpace(cargoId) || string.IsNullOrWhiteSpace(oldCargoId) || string.Equals(cargoId, oldCargoId, StringComparison.OrdinalIgnoreCase)) &&
				(string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(oldSource) || string.Equals(source, oldSource, StringComparison.OrdinalIgnoreCase)) &&
				(string.IsNullOrWhiteSpace(destination) || string.IsNullOrWhiteSpace(oldDestination) || string.Equals(destination, oldDestination, StringComparison.OrdinalIgnoreCase));

			if (!sameRawJob)
			{
				_latchedJob = new JObject();
				CopyValue(tele, _latchedJob, "cargo_name", "cargo_name", "job.cargoName", "job.cargo");
				CopyValue(tele, _latchedJob, "cargo_id", "cargo_id", "job.cargoId", "Job.CargoId");
				CopyValue(tele, _latchedJob, "mass_kg", "mass_kg", "cargoMass", "cargo_mass", "job.cargoMass", "Job.CargoMass");
				CopyValue(tele, _latchedJob, "source_city", "source_city", "job.sourceCity", "Job.SourceCity");
				CopyValue(tele, _latchedJob, "source_city_id", "source_city_id", "job.sourceCityId", "Job.SourceCityId");
				CopyValue(tele, _latchedJob, "destination_city", "destination_city", "job.destinationCity", "Job.DestinationCity");
				CopyValue(tele, _latchedJob, "destination_city_id", "destination_city_id", "job.destinationCityId", "Job.DestinationCityId");
				_latchedJob["planned_distance_km"] = (planned > 0.0) ? planned : remaining;
				_latchedJobKey = Guid.NewGuid().ToString("N");
				ClientStore.Log("JOB OBSERVED START | trip=" + _latchedJobKey + " | " + JobSummary(_latchedJob));
			}

			tele["gat_schema"] = "job-v2";
			tele["gat_job_state"] = "active";
			tele["gat_job_event"] = string.Empty;
			tele["gat_trip_id"] = _latchedJobKey;
			tele["job_latched"] = true;
			tele["job_latch_key"] = _latchedJobKey;
			tele["on_job"] = true;
			return tele;
		}

		// O ETS2 nao possui mais trabalho carregado. Nao reaproveitamos carga/rota antigas
		// nos campos normais. Enviamos somente o trip_id anterior para a Central fechar
		// a viagem usando os sinais brutos e o recibo jobDeliveredDetails.
		if (_latchedJob != null)
		{
			string endedTrip = _latchedJobKey;
			tele["gat_schema"] = "job-v2";
			tele["gat_job_state"] = "idle";
			tele["gat_job_event"] = string.Empty;
			tele["gat_trip_id"] = endedTrip;
			tele["gat_previous_cargo_name"] = TextAny(_latchedJob, "cargo_name");
			tele["gat_previous_cargo_id"] = TextAny(_latchedJob, "cargo_id");
			tele["job_latched"] = false;
			tele["job_latch_key"] = endedTrip;
			tele["on_job"] = false;
			ClientStore.Log("JOB OBSERVED END | trip=" + endedTrip + " | Central decidira o resultado");
			_latchedJob = null;
			_latchedJobKey = string.Empty;
			return tele;
		}

		tele["gat_schema"] = "job-v2";
		tele["gat_job_state"] = "idle";
		tele["gat_job_event"] = string.Empty;
		tele["job_latched"] = false;
		tele["on_job"] = false;
		return tele;
	}

	'@

$text = $text.Substring(0, $start) + $newMethod + $text.Substring($end)
Set-Content $main.FullName $text -Encoding UTF8

$check = Get-Content $main.FullName -Raw
if ($check -notlike '*gat_schema"] = "job-v2"*') { throw 'Patch job-v2 nao foi aplicado' }
if ($check -like '*string text3 = (flag2 ? "delivered" : "cancelled")*') { throw 'Logica antiga de decisao entregue/cancelado ainda presente' }
if ($check -notlike '*Central GAT e a unica autoridade*') { throw 'Contrato sensor -> Central ausente' }
Write-Host 'GAT Telemetria configurado como sensor job-v2.'
