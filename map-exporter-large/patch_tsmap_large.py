from pathlib import Path

root = Path('upstream')
canvas = root / 'TsMap.Canvas' / 'TsMapCanvas.cs'
canvas_proj = root / 'TsMap.Canvas' / 'TsMap.Canvas.csproj'
setup = root / 'TsMap.Canvas' / 'SetupForm.cs'
setup_designer = root / 'TsMap.Canvas' / 'SetupForm.Designer.cs'
program = root / 'TsMap.Canvas' / 'Program.cs'

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

            if (_currentGeneratedTile % GatProgressEvery == 0 || _currentGeneratedTile == _totalTileCount)
                RedrawMap(true);

            if (_currentGeneratedTile % GatGcEvery == 0)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            }
        }

'''
s = s[:start] + new_save + s[end:]

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

old_msg = '''                    MessageBox.Show("Tile map has been generated!", "TsMap - Tile Map Generation Finished",\n                        MessageBoxButtons.OK, MessageBoxIcon.Information);\n'''
new_msg = '''                    string text = _tileErrorCount == 0\n                        ? "GAT Map Exporter: mapa concluído. Se executar novamente na mesma pasta, os tiles existentes serão reaproveitados."\n                        : $"GAT Map Exporter terminou com {_tileErrorCount} tile(s) com erro. Execute novamente na mesma pasta para tentar somente os que faltaram. Veja GAT_MAP_EXPORT_ERRORS.log.";\n                    MessageBox.Show(text, "GAT Map Exporter - Exportação finalizada",\n                        MessageBoxButtons.OK, _tileErrorCount == 0 ? MessageBoxIcon.Information : MessageBoxIcon.Warning);\n'''
if old_msg not in s:
    raise SystemExit('mensagem final nao encontrada')
s = s.replace(old_msg, new_msg, 1)
canvas.write_text(s, encoding='utf-8-sig')

# Diagnostico do carregamento: o upstream construia o canvas e fazia _mapper.Parse()
# sem try/catch. Qualquer incompatibilidade fazia o aplicativo simplesmente desaparecer.
ss = setup.read_text(encoding='utf-8-sig')
old_next = '''        private void NextBtn_Click(object sender, EventArgs e)\n        {\n            Cursor = Cursors.WaitCursor;\n            new TsMapCanvas(this, GameFolderBrowserDialog.SelectedPath, _mods).Show();\n            Hide();\n        }\n'''
new_next = r'''        private void NextBtn_Click(object sender, EventArgs e)
        {
            Cursor = Cursors.WaitCursor;
            NextBtn.Enabled = false;

            try
            {
                string selectedGame = GameFolderBrowserDialog.SelectedPath;
                if (string.IsNullOrWhiteSpace(selectedGame) || !Directory.Exists(selectedGame))
                    throw new DirectoryNotFoundException("A pasta selecionada do ETS2 não existe.");

                string baseScs = Path.Combine(selectedGame, "base.scs");
                if (!File.Exists(baseScs))
                    throw new FileNotFoundException("Não encontrei base.scs. Selecione a pasta raiz do Euro Truck Simulator 2.", baseScs);

                WriteLoadLog("INICIO DO CARREGAMENTO", null);
                var map = new TsMapCanvas(this, selectedGame, _mods);
                map.Show();
                Hide();
                WriteLoadLog("MAPA CARREGADO COM SUCESSO", null);
            }
            catch (Exception ex)
            {
                Cursor = Cursors.Default;
                NextBtn.Enabled = true;
                WriteLoadLog("ERRO AO CARREGAR MAPA", ex);

                MessageBox.Show(
                    "O mapa não pôde ser carregado, mas o GAT Map Exporter não será fechado.\n\n" +
                    "Erro: " + ex.Message + "\n\n" +
                    "Foi criado GAT_MAP_LOAD_ERROR.log na pasta do programa. Envie esse arquivo para o ChatGPT para corrigirmos a compatibilidade.",
                    "GAT Map Exporter - erro ao carregar mapa",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);

                Show();
                Activate();
            }
        }

        private void WriteLoadLog(string stage, Exception ex)
        {
            try
            {
                string log = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "GAT_MAP_LOAD_ERROR.log");
                var lines = new List<string>();
                lines.Add("============================================================");
                lines.Add(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " | " + stage);
                lines.Add("Versão: GAT MAP EXPORTER LARGE TESTE 0.2 DIAGNÓSTICO");
                lines.Add("Jogo: " + (GameFolderBrowserDialog.SelectedPath ?? ""));
                lines.Add("Pasta mods: " + (modPath ?? ""));
                lines.Add("Load mods: " + loadMods.Checked);
                lines.Add("Mods na ordem do exportador:");
                for (int i = 0; i < _mods.Count; i++)
                    lines.Add("  " + (i + 1) + ". " + _mods[i] + " | ativo=" + _mods[i].Load);
                if (ex != null)
                {
                    lines.Add("Tipo: " + ex.GetType().FullName);
                    lines.Add("Mensagem: " + ex.Message);
                    lines.Add("Detalhes:");
                    lines.Add(ex.ToString());
                }
                File.AppendAllLines(log, lines);
            }
            catch { }
        }
'''
if old_next not in ss:
    raise SystemExit('NextBtn_Click original nao encontrado')
ss = ss.replace(old_next, new_next, 1)
setup.write_text(ss, encoding='utf-8-sig')

# Captura também exceções de UI que ocorram fora do botão Continue.
pp = program.read_text(encoding='utf-8-sig')
pp = pp.replace('using System.Windows.Forms;', 'using System.Windows.Forms;\nusing System.IO;', 1)
old_main = '''        [STAThread]\n        private static void Main()\n        {\n            Application.EnableVisualStyles();\n            Application.SetCompatibleTextRenderingDefault(false);\n            Application.Run(new SetupForm());\n        }\n'''
new_main = r'''        private static void WriteCrash(Exception ex)
        {
            try
            {
                File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "GAT_MAP_CRASH.log"),
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + Environment.NewLine + ex + Environment.NewLine + Environment.NewLine);
            }
            catch { }
        }

        [STAThread]
        private static void Main()
        {
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (sender, args) =>
            {
                WriteCrash(args.Exception);
                MessageBox.Show("O GAT Map Exporter encontrou um erro. O detalhe foi salvo em GAT_MAP_CRASH.log.\n\n" + args.Exception.Message,
                    "GAT Map Exporter - erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
            };
            AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
            {
                var ex = args.ExceptionObject as Exception;
                if (ex != null) WriteCrash(ex);
            };

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new SetupForm());
        }
'''
if old_main not in pp:
    raise SystemExit('Main original nao encontrado')
pp = pp.replace(old_main, new_main, 1)
program.write_text(pp, encoding='utf-8-sig')

# Identificacao visual da build de diagnostico.
dd = setup_designer.read_text(encoding='utf-8-sig')
dd = dd.replace('this.Text = "Setup - TsMap";', 'this.Text = "GAT Map Exporter Large 0.2 - Diagnóstico";', 1)
setup_designer.write_text(dd, encoding='utf-8-sig')

p = canvas_proj.read_text(encoding='utf-8-sig')
p = p.replace('<AssemblyName>TsMap.Canvas</AssemblyName>', '<AssemblyName>GAT_MAP_EXPORTER_LARGE</AssemblyName>', 1)
p = p.replace('<Prefer32Bit>true</Prefer32Bit>', '<Prefer32Bit>false</Prefer32Bit>')
canvas_proj.write_text(p, encoding='utf-8-sig')

checks = {
    canvas: ['GatProgressEvery = 25', 'GAT_MAP_EXPORT_ERRORS.log', 'GatAfterTile();'],
    setup: ['GAT_MAP_LOAD_ERROR.log', 'WriteLoadLog("ERRO AO CARREGAR MAPA"', 'File.Exists(baseScs)'],
    program: ['GAT_MAP_CRASH.log', 'Application.ThreadException'],
}
for f, tokens in checks.items():
    text = f.read_text(encoding='utf-8-sig')
    for token in tokens:
        if token not in text:
            raise SystemExit('patch incompleto em ' + str(f) + ': ' + token)

print('GAT Map Exporter Large 0.2: x64 + resume + diagnostico de carregamento e crash')
