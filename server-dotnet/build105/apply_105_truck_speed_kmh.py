from pathlib import Path

core = Path('/tmp/gat-src/internal/core/core.go')
if not core.exists():
    raise SystemExit('core source not found')

s = core.read_text(encoding='utf-8')
old = '''\t// Values explicitly named kmh are already in km/h and must never be
\t// multiplied again. Only the raw SDK truck.speed value is in m/s.
\tspeed := FloatAny(t, "truck.speedKmh", "truck.speed_kmh", "speed_kmh")
\tif speed == 0 {
\t\tspeed = FloatAny(t, "truck.speed")
\t\tif speed != 0 {
\t\t\tspeed *= 3.6
\t\t}
\t}
'''
new = '''\t// TruckSim GPS REST v1 already exposes truck.speed in km/h.
\t// Never multiply truck.speed by 3.6 here.
\tspeed := FloatAny(t, "truck.speedKmh", "truck.speed_kmh", "speed_kmh", "truck.speed")
'''
if old not in s:
    raise SystemExit('1.0.4 speed block not found')
s = s.replace(old, new, 1)
core.write_text(s, encoding='utf-8')
