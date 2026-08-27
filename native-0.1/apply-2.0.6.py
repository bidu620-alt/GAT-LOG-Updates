from pathlib import Path
import re

p = Path('native-0.1/main.go')
s = p.read_text(encoding='utf-8')

s = s.replace('appVersion     = "2.0.5"', 'appVersion     = "2.0.6"')

new_normalize = r'''func normalizeTelemetry(m map[string]any) map[string]any {
	if n, ok := telemetryMass(m); ok {
		m["mass_kg"] = n
		m["cargoMass"] = n
		m["cargo_mass"] = n
	}

	// TruckSim GPS envia a distancia de navegacao em metros.
	// Mantemos os dois aliases para compatibilidade com servidor antigo e novo.
	if n, ok := num(pathValue(m, "navigation.estimatedDistance")); ok {
		m["distance_m"] = n
		m["remaining_km"] = n / 1000.0
	} else if n, ok := num(pathValue(m, "navigation.estimated_distance")); ok {
		m["distance_m"] = n
		m["remaining_km"] = n / 1000.0
	}

	// No TruckSim GPS o campo normal e truck.speed (m/s).
	// Converte para km/h e tambem aceita variantes ja normalizadas.
	if n, ok := num(pathValue(m, "truck.speed")); ok {
		m["speed_kmh"] = n * 3.6
	} else if n, ok := num(pathValue(m, "truck.speedKmh")); ok {
		m["speed_kmh"] = n
	} else if n, ok := num(pathValue(m, "truck.speed_kmh")); ok {
		m["speed_kmh"] = n
	}

	// Aliases simples para facilitar a leitura em qualquer versao do servidor.
	if v := pathValue(m, "job.cargo"); v != nil {
		m["cargo_name"] = fmt.Sprint(v)
	} else if v := pathValue(m, "job.cargoName"); v != nil {
		m["cargo_name"] = fmt.Sprint(v)
	}
	if v := pathValue(m, "job.sourceCity"); v != nil {
		m["source_city"] = fmt.Sprint(v)
	}
	if v := pathValue(m, "job.destinationCity"); v != nil {
		m["destination_city"] = fmt.Sprint(v)
	}
	if v := pathValue(m, "gameplay.onJob"); v != nil {
		m["on_job"] = v
	}
	return m
}'''

s, n = re.subn(
    r'func normalizeTelemetry\(m map\[string\]any\) map\[string\]any \{.*?\n\}\nfunc formatMass',
    new_normalize + '\nfunc formatMass',
    s,
    count=1,
    flags=re.S,
)
if n != 1:
    raise SystemExit('nao encontrei normalizeTelemetry para aplicar 2.0.6')

old = '''\t\t\t\tkm := "-"\n\t\t\t\tif x, ok := num(r.JSON["distance_m"]); ok {\n\t\t\t\t\tkm = fmt.Sprintf("%.1f km", x/1000)\n\t\t\t\t}\n\t\t\t\tvel := "-"\n\t\t\t\tif x, ok := num(r.JSON["speed_kmh"]); ok {\n\t\t\t\t\tvel = fmt.Sprintf("%.0f km/h", x)\n\t\t\t\t}\n'''
new = '''\t\t\t\tkm := "-"\n\t\t\t\tif x, ok := num(r.JSON["distance_m"]); ok {\n\t\t\t\t\tkm = fmt.Sprintf("%.1f km", x/1000)\n\t\t\t\t} else if x, ok := num(r.JSON["remaining_km"]); ok {\n\t\t\t\t\tkm = fmt.Sprintf("%.1f km", x)\n\t\t\t\t} else if x, ok := num(tele["distance_m"]); ok {\n\t\t\t\t\tkm = fmt.Sprintf("%.1f km", x/1000)\n\t\t\t\t} else if x, ok := num(tele["remaining_km"]); ok {\n\t\t\t\t\tkm = fmt.Sprintf("%.1f km", x)\n\t\t\t\t}\n\t\t\t\tvel := "-"\n\t\t\t\tif x, ok := num(r.JSON["speed_kmh"]); ok {\n\t\t\t\t\tvel = fmt.Sprintf("%.0f km/h", x)\n\t\t\t\t} else if x, ok := num(tele["speed_kmh"]); ok {\n\t\t\t\t\tvel = fmt.Sprintf("%.0f km/h", x)\n\t\t\t\t}\n'''
if old not in s:
    raise SystemExit('nao encontrei bloco de km/velocidade para aplicar 2.0.6')
s = s.replace(old, new, 1)

p.write_text(s, encoding='utf-8')
print('patch 2.0.6 aplicado')
