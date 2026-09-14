param(
    [Parameter(Mandatory=$true)]
    [string]$Root
)

$ErrorActionPreference = 'Stop'
$rootPath = (Resolve-Path $Root).Path
$main = Get-ChildItem $rootPath -Filter 'MainForm.cs' -Recurse | Select-Object -First 1
$radio = Get-ChildItem $rootPath -Filter 'RadioForm.cs' -Recurse | Select-Object -First 1
if (-not $main) { throw 'MainForm.cs nao encontrado para aplicar Radio Overlay 1.0.35.' }
if (-not $radio) { throw 'RadioForm.cs 1.0.34 nao encontrado para aplicar Radio Overlay 1.0.35.' }

$mainText = Get-Content $main.FullName -Raw
$radioText = Get-Content $radio.FullName -Raw

$mainText = $mainText.Replace('CurrentVersion = "1.0.34.0"', 'CurrentVersion = "1.0.35.0"')
$mainText = $mainText.Replace('Text = "Cliente 1.0.34"', 'Text = "Cliente 1.0.35"')
if ($mainText -notmatch 'CurrentVersion = "1\.0\.35\.0"') { throw 'Nao consegui atualizar CurrentVersion para 1.0.35.0.' }

# A Radio/TV deixa de ser filha da janela principal para continuar aberta quando o GAT for minimizado.
if ($mainText -notlike '*radioForm.Show(this);*') { throw 'Nao encontrei radioForm.Show(this) do cliente 1.0.34.' }
$mainText = $mainText.Replace('radioForm.Show(this);', 'radioForm.Show();')

$oldField = 'private readonly Button _fullScreen = new Button();'
$newField = $oldField + "`r`n    private readonly Button _overlay = new Button();"
if ($radioText -notlike "*$oldField*") { throw 'Campo _fullScreen nao encontrado no RadioForm 1.0.34.' }
$radioText = $radioText.Replace($oldField, $newField)

$oldMode = 'private bool _fullScreenMode;'
$newMode = $oldMode + "`r`n    private bool _overlayMode;`r`n    private Rectangle _normalBounds;"
if ($radioText -notlike "*$oldMode*") { throw 'Estado de fullscreen nao encontrado no RadioForm 1.0.34.' }
$radioText = $radioText.Replace($oldMode, $newMode)

$radioText = $radioText.Replace('StartPosition = FormStartPosition.CenterParent;', 'StartPosition = FormStartPosition.CenterScreen;')
$oldKeyPreview = 'KeyPreview = true;'
$newKeyPreview = @"
KeyPreview = true;
        TopMost = true;
        ShowInTaskbar = true;
"@
if ($radioText -notlike "*$oldKeyPreview*") { throw 'KeyPreview nao encontrado no RadioForm 1.0.34.' }
$radioText = $radioText.Replace($oldKeyPreview, $newKeyPreview.TrimEnd())

$webMarker = '        _web.Left = 24; _web.Top = 100; _web.Width = ClientSize.Width - 48; _web.Height = 350;'
$overlayUi = @"
        SetupButton(_overlay, "MODO JOGO • SOBREPOSTO", ClientSize.Width - 224, 18, 200);
        _overlay.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _overlay.Click += delegate { ToggleOverlayMode(); };
        Controls.Add(_overlay);

$webMarker
"@
if ($radioText -notlike "*$webMarker*") { throw 'Area do player nao encontrada para inserir MODO JOGO.' }
$radioText = $radioText.Replace($webMarker, $overlayUi.TrimEnd())

$radioText = $radioText.Replace('if (_fullScreenMode) return;', 'if (_fullScreenMode || _overlayMode) return;')
$radioText = $radioText.Replace('Radio-1.0.34', 'Radio-1.0.35')
$radioText = $radioText.Replace('/index.html?v=134', '/index.html?v=135')

$loadVolumeMarker = '    private static int LoadVolume()'
$overlayMethod = @"
    private void ToggleOverlayMode()
    {
        if (!_overlayMode)
        {
            if (_fullScreenMode) ToggleFullScreen();
            _overlayMode = true;
            _normalBounds = Bounds;
            TopMost = true;
            ShowInTaskbar = true;
            FormBorderStyle = FormBorderStyle.SizableToolWindow;
            MinimumSize = new Size(360, 240);
            Size = new Size(520, 340);

            foreach (Control control in Controls)
            {
                if (!ReferenceEquals(control, _web) && !ReferenceEquals(control, _overlay)) control.Visible = false;
            }
            _web.Visible = true;
            _overlay.Visible = true;
            _web.Left = 8; _web.Top = 8;
            _web.Width = Math.Max(320, ClientSize.Width - 16);
            _web.Height = Math.Max(160, ClientSize.Height - 60);
            _web.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;

            _overlay.Text = "VOLTAR AO GAT";
            _overlay.Width = 160; _overlay.Height = 36;
            _overlay.Left = Math.Max(8, ClientSize.Width - _overlay.Width - 8);
            _overlay.Top = Math.Max(8, ClientSize.Height - _overlay.Height - 8);
            _overlay.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            _overlay.BringToFront();

            Screen screen = Screen.FromControl(this);
            Rectangle work = screen.WorkingArea;
            Location = new Point(Math.Max(work.Left, work.Right - Width - 16), Math.Max(work.Top, work.Bottom - Height - 16));

            foreach (Form form in Application.OpenForms)
            {
                if (!ReferenceEquals(form, this) && string.Equals(form.GetType().Name, "MainForm", StringComparison.Ordinal))
                {
                    form.WindowState = FormWindowState.Minimized;
                    break;
                }
            }
            Activate();
            BringToFront();
        }
        else
        {
            _overlayMode = false;
            FormBorderStyle = FormBorderStyle.Sizable;
            MinimumSize = new Size(700, 620);
            foreach (Control control in Controls) control.Visible = true;

            if (_normalBounds.Width >= 700 && _normalBounds.Height >= 620) Bounds = _normalBounds;
            else Size = new Size(760, 680);

            _web.Left = 24; _web.Top = 100; _web.Width = ClientSize.Width - 48; _web.Height = 350;
            _web.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            _state.Top = 466; _track.Top = 493; _source.Top = 529;
            _toggle.Left = 24; _toggle.Top = 566;
            _openYoutube.Left = 188; _openYoutube.Top = 566;
            _fullScreen.Left = 362; _fullScreen.Top = 566;

            _overlay.Text = "MODO JOGO • SOBREPOSTO";
            _overlay.Width = 200; _overlay.Height = 38;
            _overlay.Left = ClientSize.Width - 224; _overlay.Top = 18;
            _overlay.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _overlay.BringToFront();
        }
    }

$loadVolumeMarker
"@
if ($radioText -notlike "*$loadVolumeMarker*") { throw 'Ponto de insercao do modo sobreposto nao encontrado.' }
$radioText = $radioText.Replace($loadVolumeMarker, $overlayMethod.TrimEnd())

foreach ($marker in @(
    'CurrentVersion = "1.0.35.0"',
    'radioForm.Show();',
    'MODO JOGO • SOBREPOSTO',
    'TopMost = true;',
    'FormBorderStyle = FormBorderStyle.SizableToolWindow;',
    'form.WindowState = FormWindowState.Minimized;',
    'Radio-1.0.35',
    '/index.html?v=135'
)) {
    $haystack = if ($marker -like 'CurrentVersion*' -or $marker -eq 'radioForm.Show();') { $mainText } else { $radioText }
    if ($haystack -notlike "*$marker*") { throw "Patch Radio Overlay 1.0.35 incompleto: $marker" }
}

Set-Content $main.FullName $mainText -Encoding UTF8
Set-Content $radio.FullName $radioText -Encoding UTF8
Write-Host 'GAT Telemetria 1.0.35: Radio/TV independente, TopMost e modo jogo compacto sobreposto.'
