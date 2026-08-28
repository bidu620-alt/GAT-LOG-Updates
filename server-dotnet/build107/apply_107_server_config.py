from pathlib import Path

root = Path('/tmp/gat-src')
core = root / 'internal/core/core.go'
agent = root / 'cmd/agent/main.go'

s = core.read_text(encoding='utf-8')

# Make the agent version visible so we can distinguish this build at runtime.
s = s.replace('InternalVersion = "1.0.1"', 'InternalVersion = "1.0.7"')

# Session name can be empty. \s* was consuming the following newline and making
# "Session description:" appear as the session name. Only allow spaces/tabs.
old_session = 'if m := lastMatch(txt, `(?m)Session name:\\s*(.+)$`); m != "" {'
new_session = 'if m := lastMatch(txt, `(?m)Session name:[ \\t]*([^\\r\\n]*)$`); m != "" {'
if old_session not in s:
    raise SystemExit('session name parser block not found')
s = s.replace(old_session, new_session, 1)

# The generic set() helper inserts missing keys before the LAST closing brace in
# the SII file. server_config.sii has an inner server_config block and an outer
# SiiNunit block, so moderator_list[0] ended up outside server_config and made
# the file invalid. Insert the indexed moderator immediately after
# moderator_list instead.
old_mod = '''\tif validSteamID(c.ModeratorSteamID) {
\t\tset("moderator_list", "1")
\t\tset("moderator_list[0]", c.ModeratorSteamID)
\t} else {
\t\tset("moderator_list", "0")
\t}
'''
new_mod = '''\tif validSteamID(c.ModeratorSteamID) {
\t\tset("moderator_list", "1")
\t\tneedle := "    moderator_list: 1"
\t\tpos := strings.Index(s, needle)
\t\tif pos < 0 {
\t\t\treturn fmt.Errorf("nao foi possivel localizar moderator_list no server_config.sii")
\t\t}
\t\tnl := "\\n"
\t\tif strings.Contains(s, "\\r\\n") {
\t\t\tnl = "\\r\\n"
\t\t}
\t\tlineEnd := strings.IndexByte(s[pos:], '\\n')
\t\tif lineEnd >= 0 {
\t\t\tinsertAt := pos + lineEnd + 1
\t\t\ts = s[:insertAt] + "    moderator_list[0]: " + c.ModeratorSteamID + nl + s[insertAt:]
\t\t} else {
\t\t\ts += nl + "    moderator_list[0]: " + c.ModeratorSteamID + nl
\t\t}
\t} else {
\t\tset("moderator_list", "0")
\t}
'''
if old_mod not in s:
    raise SystemExit('moderator patch block not found')
s = s.replace(old_mod, new_mod, 1)
core.write_text(s, encoding='utf-8')

# Never start the dedicated server with default values if patching the SII
# failed. Surface the real configuration error to the C# application.
a = agent.read_text(encoding='utf-8')
old_start = '\t_ = core.PatchServerConfig(c)\n\tcmd := exec.Command(c.ServerExe)'
new_start = '''\tif e := core.PatchServerConfig(c); e != nil {
\t\treturn fmt.Errorf("nao foi possivel aplicar server_config.sii: %w", e)
\t}
\tcmd := exec.Command(c.ServerExe)'''
if old_start not in a:
    raise SystemExit('startServer PatchServerConfig block not found')
a = a.replace(old_start, new_start, 1)
agent.write_text(a, encoding='utf-8')
