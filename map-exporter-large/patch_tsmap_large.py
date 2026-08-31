from pathlib import Path

root = Path('upstream')
canvas = root / 'TsMap.Canvas' / 'TsMapCanvas.cs'
canvas_proj = root / 'TsMap.Canvas' / 'TsMap.Canvas.csproj'

s = canvas.read_text(encoding='utf-8-sig')

# Estado extra para exportacao longa/resumivel.
needle = '''        private uint _totalTileCount;\n        private uint _currentGeneratedTile;\n'''
repl = '''        private uint _totalTileCount;\n        private uint _currentGeneratedTile;\n        private uint _tileErrorCount;\n        private const int GatProgressEvery = 25;\n        private const int GatGcEvery = 100;\n'''
if needle not in s:
    raise SystemExit('campos de progresso nao encontrados')
s = s.replace(needle, repl, 1)

# Substitui o gravador de tile por versao resumivel/atomica.
start = s.find('        private void SaveTileImage(')
end = s.find('        private void ZoomOutAndCenterMap(', start)
if start < 0 or end < 0:
    raise SystemExit('SaveTileImage nao encontrado')
new_save = r'''        private void LogTileError(string exportPath, string message)
        {
            try
            {
                Directory.CreateDirectory(exportPath);
                File.AppendAllText(Path.Combine(exportPath, "GAT_MAP_EXPORT_ERRORS.log"),
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " | " + message + Environment.NewLine);
            }
            catch { }
        }

        private bool SaveTileImage(int z, int x, int y, PointF pos, float zoom, string exportPath, RenderFlags renderFlags)
        {
            string dir = Path.Combine(exportPath, "Tiles", z.ToString(), x.ToString());
            string file = Path.Combine(dir, y + ".png");

            // MODO RESUMIR: se o tile ja existe e nao esta vazio, nao renderiza de novo.
            try
            {
                if (File.Exists(file) && new FileInfo(file).Length > 128)
                    return false;
            }
            catch { }

            Directory.CreateDirectory(dir);
            string temp = file + ".tmp";

            try
            {
                using (var bitmap = new Bitmap(tileSize, tileSize))
                using (var g = Graphics.FromImage(bitmap))
                {
                    pos.X = (x == 0) ? pos.X : pos.X + (bitmap.Width / zoom) * x;
                    pos.Y = (y == 0) ? pos.Y : pos.Y + (bitmap.Height / zoom) * y;

                    _renderer.Render(g, new Rectangle(0, 0, bitmap.Width, bitmap.Height), zoom, pos, _palette,
                        renderFlags & ~RenderFlags.TextOverlay);

                    if (File.Exists(temp)) File.Delete(temp);
                    bitmap.Save(temp, ImageFormat.Png);
                }

                if (File.Exists(file)) File.Delete(file);
                File.Move(temp, file);
                return true;
            }
            catch (Exception ex)
            {
                _tileErrorCount++;
                try { if (File.Exists(temp)) File.Delete(temp); } catch { }
                LogTileError(exportPath, $"z={z} x={x} y={y} | {ex}");
                return false;
            }
        }

        private void GatAfterTile()
        {
            _currentGeneratedTile++;

            // Nao invalida a interface a cada PNG; isso criava milhares de repaints pendentes.
            if (_currentGeneratedTile % GatProgressEvery == 0 || _currentGeneratedTile == _totalTileCount)
                RedrawMap(true);

            // O renderizador usa muitos objetos System.Drawing/streams. Em mapas enormes,
            // uma coleta periodica reduz o crescimento de memoria entre dezenas de milhares de tiles.
            if (_currentGeneratedTile % GatGcEvery == 0)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            }
        }

'''
s = s[:start] + new_save + s[end:]

# Reinicia contadores e transforma o zoom 0 no mesmo fluxo de progresso.
s = s.replace('''                _currentGeneratedTile = 0;\n                _totalTileCount = 0;\n''', '''                _currentGeneratedTile = 0;\n                _totalTileCount = 0;\n                _tileErrorCount = 0;\n''', 1)

old0 = '''                        SaveTileImage(0, 0, 0, pos, zoom, exportPath, renderFlags);\n                        _currentGeneratedTile = 1;\n                        startZoomLevel++;\n'''
new0 = '''                        SaveTileImage(0, 0, 0, pos, zoom, exportPath, renderFlags);\n                        GatAfterTile();\n                        startZoomLevel++;\n'''
if old0 not in s:
    raise SystemExit('bloco zoom 0 nao encontrado')
s = s.replace(old0, new0, 1)

old_loop = '''                            SaveTileImage(z, x, y, pos, zoom, exportPath, renderFlags);\n                            _currentGeneratedTile++;\n                            RedrawMap(true);\n'''
new_loop = '''                            SaveTileImage(z, x, y, pos, zoom, exportPath, renderFlags);\n                            GatAfterTile();\n'''
if old_loop not in s:
    raise SystemExit('loop de tiles nao encontrado')
s = s.replace(old_loop, new_loop, 1)

# Mensagem final diferencia exportacao limpa de exportacao com tiles problemáticos.
old_msg = '''                    MessageBox.Show("Tile map has been generated!", "TsMap - Tile Map Generation Finished",\n                        MessageBoxButtons.OK, MessageBoxIcon.Information);\n'''
new_msg = '''                    string text = _tileErrorCount == 0\n                        ? "GAT Map Exporter: mapa concluído. Se executar novamente na mesma pasta, os tiles existentes serão reaproveitados."\n                        : $"GAT Map Exporter terminou com {_tileErrorCount} tile(s) com erro. Execute novamente na mesma pasta para tentar somente os que faltaram. Veja GAT_MAP_EXPORT_ERRORS.log.";\n                    MessageBox.Show(text, "GAT Map Exporter - Exportação finalizada",\n                        MessageBoxButtons.OK, _tileErrorCount == 0 ? MessageBoxIcon.Information : MessageBoxIcon.Warning);\n'''
if old_msg not in s:
    raise SystemExit('mensagem final nao encontrada')
s = s.replace(old_msg, new_msg, 1)

canvas.write_text(s, encoding='utf-8-sig')

p = canvas_proj.read_text(encoding='utf-8-sig')
p = p.replace('<AssemblyName>TsMap.Canvas</AssemblyName>', '<AssemblyName>GAT_MAP_EXPORTER_LARGE</AssemblyName>', 1)
# Garante processo realmente 64 bits no build x64.
p = p.replace('<Prefer32Bit>true</Prefer32Bit>', '<Prefer32Bit>false</Prefer32Bit>')
canvas_proj.write_text(p, encoding='utf-8-sig')

# Validacoes para evitar publicar build sem as protecoes.
check = canvas.read_text(encoding='utf-8-sig')
for token in [
    'GatProgressEvery = 25',
    'GatGcEvery = 100',
    'GAT_MAP_EXPORT_ERRORS.log',
    'new FileInfo(file).Length > 128',
    'GatAfterTile();',
    'GC.WaitForPendingFinalizers()',
]:
    if token not in check:
        raise SystemExit('patch incompleto: ' + token)

print('GAT Map Exporter Large patch aplicado: x64 + resume + GC + repaint reduzido + log de erros')
