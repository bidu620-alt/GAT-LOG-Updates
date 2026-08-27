from pathlib import Path

root = Path('/tmp/gat-src')
core = root / 'internal/core/core.go'

s = core.read_text(encoding='utf-8')

# Identify this agent build as 1.0.1.
s = s.replace('InternalVersion = "0.1.7"', 'InternalVersion = "1.0.1"')

old = '''\tif validSteamID(c.ModeratorSteamID) {\n\t\tset("moderator_list", c.ModeratorSteamID)\n\t} else {\n\t\tset("moderator_list", "0")\n\t}\n'''
new = '''\t// SCS expects moderator_list to be an array count plus indexed entries.\n\t// Remove stale indexed moderator entries before writing the current value.\n\tmodEntries := regexp.MustCompile(`(?m)^\\s*moderator_list\\[\\d+\\]\\s*:\\s*.*(?:\\r?\\n|$)`)\n\ts = modEntries.ReplaceAllString(s, "")\n\tif validSteamID(c.ModeratorSteamID) {\n\t\tset("moderator_list", "1")\n\t\tset("moderator_list[0]", c.ModeratorSteamID)\n\t} else {\n\t\tset("moderator_list", "0")\n\t}\n'''
if old not in s:
    raise SystemExit('moderator PatchServerConfig block not found')
s = s.replace(old, new, 1)

# Allow recovery/import from an existing SCS server_config.sii.
old_import = '[]string{"moderator_steam_id", "moderator", "steam_id64", "steamid64"}'
new_import = '[]string{"moderator_steam_id", "moderator_list[0]", "moderator", "steam_id64", "steamid64"}'
if old_import not in s:
    raise SystemExit('moderator import keys not found')
s = s.replace(old_import, new_import, 1)

core.write_text(s, encoding='utf-8')
