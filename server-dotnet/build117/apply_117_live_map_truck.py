from pathlib import Path

agent = Path('/tmp/gat-src/cmd/agent/main.go')
core = Path('/tmp/gat-src/internal/core/core.go')
if not agent.exists() or not core.exists():
    raise SystemExit('fontes do agente nao encontradas')

c = core.read_text(encoding='utf-8')
if 'InternalVersion = "1.0.16"' in c:
    c = c.replace('InternalVersion = "1.0.16"', 'InternalVersion = "1.0.17"', 1)
elif 'InternalVersion = "1.0.17"' not in c:
    raise SystemExit('versao 1.0.16 do agente nao encontrada')

if 'MapX        float64' not in c:
    needle = '\tOnJob       bool           `json:"on_job"`\n\tRaw         map[string]any `json:"telemetry,omitempty"`\n'
    replacement = ('\tOnJob       bool           `json:"on_job"`\n'
                   '\tMapX        float64        `json:"map_x,omitempty"`\n'
                   '\tMapZ        float64        `json:"map_z,omitempty"`\n'
                   '\tMapHeading  float64        `json:"map_heading,omitempty"`\n'
                   '\tTruckMake   string         `json:"truck_make,omitempty"`\n'
                   '\tTruckModel  string         `json:"truck_model,omitempty"`\n'
                   '\tRaw         map[string]any `json:"telemetry,omitempty"`\n')
    if needle not in c:
        raise SystemExit('TelemetryRecord nao encontrado')
    c = c.replace(needle, replacement, 1)
core.write_text(c, encoding='utf-8')

s = agent.read_text(encoding='utf-8')

# Enrich the normalized server record from the raw TruckSim/GAT client payload.
needle = '\trec := core.NormalizeTelemetry(b.Driver, b.DeviceID, q.Telemetry)\n\trec.Status = "ONLINE"\n'
replacement = ('\trec := core.NormalizeTelemetry(b.Driver, b.DeviceID, q.Telemetry)\n'
               '\trec.MapX = gatTelemetryFloat(q.Telemetry, "map_x", "truck.placement.x")\n'
               '\trec.MapZ = gatTelemetryFloat(q.Telemetry, "map_z", "truck.placement.z")\n'
               '\trec.MapHeading = gatTelemetryFloat(q.Telemetry, "map_heading", "truck.placement.heading")\n'
               '\trec.TruckMake = gatTelemetryString(q.Telemetry, "truck_make", "truck.make")\n'
               '\trec.TruckModel = gatTelemetryString(q.Telemetry, "truck_model", "truck.model")\n'
               '\trec.Status = "ONLINE"\n')
if 'rec.MapX = gatTelemetryFloat' not in s:
    if needle not in s:
        raise SystemExit('ponto clientTelemetry nao encontrado')
    s = s.replace(needle, replacement, 1)

# Expose the fields on the read-only public live endpoint used by the site map.
needle_pub = '\t\t\t"on_job":        v.OnJob,\n'
replacement_pub = ('\t\t\t"on_job":        v.OnJob,\n'
                   '\t\t\t"map_x":         v.MapX,\n'
                   '\t\t\t"map_z":         v.MapZ,\n'
                   '\t\t\t"map_heading":   v.MapHeading,\n'
                   '\t\t\t"truck_make":    v.TruckMake,\n'
                   '\t\t\t"truck_model":   v.TruckModel,\n')
if '"map_heading":   v.MapHeading' not in s:
    if needle_pub not in s:
        raise SystemExit('public live telemetry map nao encontrado')
    s = s.replace(needle_pub, replacement_pub, 1)

agent.write_text(s, encoding='utf-8')
print('GAT-LOG Server 1.0.17 live map and truck fields applied')
