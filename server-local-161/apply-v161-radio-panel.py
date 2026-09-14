from pathlib import Path
import sys

root = Path(sys.argv[1]) if len(sys.argv) > 1 else Path('server-local/ui/GatLogServer')
p = root / 'RadioPanel.cs'
s = p.read_text(encoding='utf-8')
MARKER = 'GAT_RADIO_VIDEO_UI_V161'

if MARKER not in s:
    s = s.replace('AddLabel(card, "Playlist do YouTube", 390, 270, 220);', 'AddLabel(card, "Link do YouTube (vídeo ou playlist)", 390, 270, 300); // GAT_RADIO_VIDEO_UI_V161', 1)
    s = s.replace('case "invalid_youtube_playlist": return "Link de playlist do YouTube inválido.";', 'case "invalid_youtube_playlist": return "Link de playlist do YouTube inválido.";\n            case "invalid_youtube_url": return "Informe um link válido de vídeo ou playlist do YouTube.";', 1)
    s = s.replace('case "playlist_required": return "Informe uma playlist antes de ligar a rádio.";', 'case "playlist_required": return "Informe uma playlist antes de ligar a rádio.";\n            case "source_required": return "Informe um vídeo ou playlist antes de ligar a rádio.";', 1)
    s = s.replace('_playlist.Text = (string)radio["playlist_url"] ?? "";', '_playlist.Text = (string)radio["source_url"] ?? (string)radio["playlist_url"] ?? "";', 1)
    s = s.replace('MessageBox.Show(this, "Cole o link de uma playlist do YouTube antes de ligar a rádio.", "Rádio GAT", MessageBoxButtons.OK, MessageBoxIcon.Information);', 'MessageBox.Show(this, "Cole o link de um vídeo ou playlist do YouTube antes de ligar a rádio.", "Rádio GAT", MessageBoxButtons.OK, MessageBoxIcon.Information);', 1)
    s = s.replace('playlist_url = playlist,', 'source_url = playlist,', 1)
    s = s.replace('_playlist.Text = (string)radio?["playlist_url"] ?? _playlist.Text;', '_playlist.Text = (string)radio?["source_url"] ?? (string)radio?["playlist_url"] ?? _playlist.Text;', 1)
    s = s.replace('Configuração salva. Os GAT Telemetria receberão a programação pela Central.', 'Configuração salva. Vídeo/playlist será enviado aos GAT Telemetria pela Central.', 1)

required = [MARKER, 'Link do YouTube (vídeo ou playlist)', 'invalid_youtube_url', 'source_required', 'source_url = playlist', 'radio["source_url"]']
for item in required:
    if item not in s:
        raise SystemExit('Patch de painel Radio 1.0.61 incompleto: ' + item)

p.write_text(s, encoding='utf-8')
print('GAT Server 1.0.61: painel Radio GAT atualizado para video individual ou playlist.')
