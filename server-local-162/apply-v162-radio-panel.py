from pathlib import Path
import sys

root = Path(sys.argv[1]) if len(sys.argv) > 1 else Path('server-local/ui/GatLogServer')
p = root / 'RadioPanel.cs'
s = p.read_text(encoding='utf-8')
MARKER = 'GAT_RADIO_STREAM_UI_V162'

if MARKER not in s:
    s = s.replace('Text = "Controle a playlist que toca somente no GAT Telemetria dos motoristas.",', 'Text = "Controle o Canal GAT: YouTube ou Rádio Online por stream direto MP3/AAC.", // GAT_RADIO_STREAM_UI_V162', 1)
    s = s.replace('AddLabel(card, "Link do YouTube (vídeo ou playlist)", 390, 270, 300);', 'AddLabel(card, "YouTube ou Rádio Online (MP3/AAC)", 390, 270, 330);', 1)
    s = s.replace('case "invalid_youtube_url": return "Informe um link válido de vídeo ou playlist do YouTube.";', 'case "invalid_youtube_url": return "Informe um link válido de vídeo ou playlist do YouTube.";\n            case "invalid_media_url": return "Informe um link do YouTube ou URL direta de rádio online (MP3/AAC).";', 1)
    s = s.replace('case "source_required": return "Informe um vídeo ou playlist antes de ligar a rádio.";', 'case "source_required": return "Informe YouTube ou uma rádio online antes de ligar o Canal GAT.";', 1)
    s = s.replace('MessageBox.Show(this, "Cole o link de um vídeo ou playlist do YouTube antes de ligar a rádio.", "Rádio GAT", MessageBoxButtons.OK, MessageBoxIcon.Information);', 'MessageBox.Show(this, "Cole um link do YouTube ou uma URL direta de rádio online (MP3/AAC) antes de ligar.", "Rádio GAT", MessageBoxButtons.OK, MessageBoxIcon.Information);', 1)
    s = s.replace('Configuração salva. Vídeo/playlist será enviado aos GAT Telemetria pela Central.', 'Configuração salva. YouTube/rádio online será enviado aos GAT Telemetria pela Central.', 1)
    s = s.replace('O servidor grava apenas o estado da rádio, nome e ID da playlist.', 'O servidor grava apenas o estado da rádio, nome e link da fonte. Para rádio online, prefira URL direta do stream; HTTPS é recomendado.', 1)

required = [MARKER, 'YouTube ou Rádio Online (MP3/AAC)', 'invalid_media_url', 'URL direta de rádio online', 'link da fonte']
for item in required:
    if item not in s:
        raise SystemExit('Patch de painel Radio 1.0.62 incompleto: ' + item)

p.write_text(s, encoding='utf-8')
print('GAT Server 1.0.62: painel Radio GAT atualizado para YouTube + stream MP3/AAC.')
