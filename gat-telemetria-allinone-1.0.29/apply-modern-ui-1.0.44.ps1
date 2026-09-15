param(
    [Parameter(Mandatory=$true)]
    [string]$Root
)

$ErrorActionPreference = 'Stop'
$rootPath = (Resolve-Path $Root).Path
$main = Get-ChildItem $rootPath -Filter 'MainForm.cs' -Recurse | Select-Object -First 1
$hub42 = Get-ChildItem $rootPath -Filter 'MainForm.Hub042.cs' -Recurse | Select-Object -First 1
$truck = Get-ChildItem $rootPath -Filter 'TruckOverlay041.cs' -Recurse | Select-Object -First 1
$video = Get-ChildItem $rootPath -Filter 'VideoOverlay041.cs' -Recurse | Select-Object -First 1
$project = Get-ChildItem $rootPath -Filter 'GAT_TELEMETRIA.csproj' -Recurse | Select-Object -First 1
if (-not $main -or -not $hub42 -or -not $truck -or -not $video -or -not $project) {
    throw 'Fonte 1.0.43 incompleto para aplicar interface 1.0.44.'
}

$mainText = Get-Content $main.FullName -Raw
$hub42Text = Get-Content $hub42.FullName -Raw
$truckText = Get-Content $truck.FullName -Raw
$videoText = Get-Content $video.FullName -Raw
$projectText = Get-Content $project.FullName -Raw

$mainText = $mainText.Replace('CurrentVersion = "1.0.43.0"', 'CurrentVersion = "1.0.44.0"')
$mainText = $mainText.Replace('Text = "Cliente 1.0.43"', 'Text = "Cliente 1.0.44"')
$mainText = $mainText.Replace('HUB 1.0.43:', 'HUB 1.0.44:')

if ($mainText -notlike '*ApplyHub044();*') {
    $needle = 'ApplyHub042();'
    $idx = $mainText.IndexOf($needle)
    if ($idx -lt 0) { throw 'ApplyHub042() nao encontrado para encadear a 1.0.44.' }
    $mainText = $mainText.Insert($idx + $needle.Length, "`r`n            ApplyHub044();")
}

$hub42Text = $hub42Text.Replace('GAT Telemetria BETA 1.0.43', 'GAT Telemetria BETA 1.0.44')
$hub42Text = $hub42Text.Replace('Cliente 1.0.43 TESTE', 'Cliente 1.0.44 TESTE')

$hub044Source = Join-Path $PSScriptRoot 'MainForm.Hub044.cs'
if (-not (Test-Path $hub044Source)) { throw 'MainForm.Hub044.cs nao encontrado.' }
$hub044Target = Join-Path $main.Directory.FullName 'MainForm.Hub044.cs'
Copy-Item $hub044Source $hub044Target -Force

if ($projectText -notmatch '<Project\s+Sdk=' -and $projectText -notmatch 'MainForm\.Hub044\.cs') {
    $compileGroup = @"
  <ItemGroup>
    <Compile Include="MainForm.Hub044.cs" />
  </ItemGroup>
"@
    if ($projectText -notmatch '</Project>') { throw 'Fim do csproj nao encontrado.' }
    $projectText = $projectText -replace '</Project>', ($compileGroup + "`r`n</Project>")
}

# Overlay do caminhao: sem barra branca, redimensionamento nas bordas e fechar proprio.
$truckText = $truckText.Replace('MinimumSize = new Size(250, 220);', 'MinimumSize = new Size(230, 180);')
$truckText = $truckText.Replace('FormBorderStyle = FormBorderStyle.SizableToolWindow;', "FormBorderStyle = FormBorderStyle.None;`r`n        Padding = new Padding(1);")
$truckText = $truckText.Replace('Text = "GAT DASH • Informações do caminhão";', 'Text = "GAT DASH • Sobreposição do caminhão";')
$truckText = $truckText.Replace('Label sub = new Label { Text = "OVERLAY • CAMINHÃO"', 'Label sub = new Label { Text = "SOBREPOSIÇÃO • CAMINHÃO"')
$truckText = $truckText.Replace('Arraste a janela e use as bordas para redimensionar.', 'Arraste pelo topo • redimensione pelas bordas.')

$truckNeedle = 'Controls.Add(title); Controls.Add(sub);'
if ($truckText.Contains($truckNeedle) -and $truckText -notlike '*close044*') {
    $truckInsert = @'
Controls.Add(title); Controls.Add(sub);
        var close044 = new Button
        {
            Text = "×", Width = 34, Height = 30, Top = 8,
            Left = Math.Max(0, ClientSize.Width - 44),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(7, 29, 50),
            ForeColor = Color.FromArgb(210, 230, 248),
            Font = new Font("Segoe UI Semibold", 12f, FontStyle.Bold),
            Cursor = Cursors.Hand, TabStop = false
        };
        close044.FlatAppearance.BorderSize = 0;
        close044.FlatAppearance.MouseOverBackColor = Color.FromArgb(130, 33, 52);
        close044.Click += delegate { Close(); };
        Controls.Add(close044);
'@
    $truckText = $truckText.Replace($truckNeedle, $truckInsert.TrimEnd())
}

if ($truckText -notlike '*WM_NCHITTEST_044*') {
    $last = $truckText.LastIndexOf('}')
    if ($last -lt 0) { throw 'Fim TruckOverlay041.cs nao encontrado.' }
    $truckMethods = @'

    private const int WM_NCHITTEST_044 = 0x0084;
    private const int HTCLIENT_044 = 1;
    private const int HTCAPTION_044 = 2;
    private const int HTLEFT_044 = 10;
    private const int HTRIGHT_044 = 11;
    private const int HTTOP_044 = 12;
    private const int HTTOPLEFT_044 = 13;
    private const int HTTOPRIGHT_044 = 14;
    private const int HTBOTTOM_044 = 15;
    private const int HTBOTTOMLEFT_044 = 16;
    private const int HTBOTTOMRIGHT_044 = 17;

    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);
        if (m.Msg != WM_NCHITTEST_044 || (int)m.Result != HTCLIENT_044) return;
        long raw = m.LParam.ToInt64();
        int sx = unchecked((short)(raw & 0xffff));
        int sy = unchecked((short)((raw >> 16) & 0xffff));
        Point p = PointToClient(new Point(sx, sy));
        const int grip = 8;
        bool left = p.X <= grip, right = p.X >= ClientSize.Width - grip;
        bool top = p.Y <= grip, bottom = p.Y >= ClientSize.Height - grip;
        if (left && top) m.Result = (IntPtr)HTTOPLEFT_044;
        else if (right && top) m.Result = (IntPtr)HTTOPRIGHT_044;
        else if (left && bottom) m.Result = (IntPtr)HTBOTTOMLEFT_044;
        else if (right && bottom) m.Result = (IntPtr)HTBOTTOMRIGHT_044;
        else if (left) m.Result = (IntPtr)HTLEFT_044;
        else if (right) m.Result = (IntPtr)HTRIGHT_044;
        else if (top) m.Result = (IntPtr)HTTOP_044;
        else if (bottom) m.Result = (IntPtr)HTBOTTOM_044;
        else if (p.Y <= 54) m.Result = (IntPtr)HTCAPTION_044;
    }
'@
    $truckText = $truckText.Insert($last, $truckMethods)
}

# Overlay de video: sem barra branca e com topo escuro para arrastar.
$videoText = $videoText.Replace('MinimumSize = new Size(300, 190);', 'MinimumSize = new Size(260, 160);')
$videoText = $videoText.Replace('FormBorderStyle = FormBorderStyle.SizableToolWindow;', "FormBorderStyle = FormBorderStyle.None;`r`n        Padding = new Padding(6);")
$videoText = $videoText.Replace('Text = "GAT DASH • Vídeo flutuante";', 'Text = "GAT DASH • Sobreposição de vídeo";')
$videoText = $videoText.Replace('"VideoOverlay-1.0.41"', '"VideoOverlay-1.0.44"')
$videoText = $videoText.Replace('index.html?v=1041', 'index.html?v=1044')

if ($videoText -notlike '*_dragging044*') {
    $fieldNeedle = 'private bool _ready;'
    $videoText = $videoText.Replace($fieldNeedle, $fieldNeedle + "`r`n    private bool _dragging044;`r`n    private Point _dragOffset044;")
}

$oldHandler = 'if (type == "mediaRefresh") await PushMediaAsync();'
if ($videoText.Contains($oldHandler) -and $videoText -notlike '*dragStart044*') {
    $newHandler = @'
if (type == "mediaRefresh") await PushMediaAsync();
                    else if (type == "dragStart044")
                    {
                        _dragging044 = true;
                        _dragOffset044 = new Point(Cursor.Position.X - Left, Cursor.Position.Y - Top);
                    }
                    else if (type == "dragMove044" && _dragging044)
                    {
                        Location = new Point(Cursor.Position.X - _dragOffset044.X, Cursor.Position.Y - _dragOffset044.Y);
                    }
                    else if (type == "dragEnd044") _dragging044 = false;
                    else if (type == "closeWindow044") Close();
'@
    $videoText = $videoText.Replace($oldHandler, $newHandler.TrimEnd())
}

$videoText = $videoText.Replace(
    '.brand{font-weight:800;color:#55a5ff;margin-right:auto}',
    '.brand{font-weight:800;color:#55a5ff;margin-right:auto;cursor:move;user-select:none}.close044{border:0;background:transparent;color:#bcd7f7;font-size:19px;width:28px;height:28px;cursor:pointer}.close044:hover{color:white;background:#7b2636;border-radius:5px}'
)
$videoText = $videoText.Replace(
    "<span class='brand'>GAT • VÍDEO</span><button id='gat'",
    "<span id='drag044' class='brand'>GAT • VÍDEO</span><button id='gat'"
)
$videoText = $videoText.Replace(
    "<button id='web' class='tab'>Canal Web</button></div>",
    "<button id='web' class='tab'>Canal Web</button><button id='close044' class='close044'>×</button></div>"
)
$videoText = $videoText.Replace(
    "post({type:'mediaRefresh'});setInterval(()=>post({type:'mediaRefresh'}),3000);",
    "const d044=document.getElementById('drag044');d044.onmousedown=()=>post({type:'dragStart044'});window.addEventListener('mousemove',()=>post({type:'dragMove044'}));window.addEventListener('mouseup',()=>post({type:'dragEnd044'}));document.getElementById('close044').onclick=()=>post({type:'closeWindow044'});post({type:'mediaRefresh'});setInterval(()=>post({type:'mediaRefresh'}),3000);"
)

if ($videoText -notlike '*WM_NCHITTEST_044*') {
    $last = $videoText.LastIndexOf('}')
    if ($last -lt 0) { throw 'Fim VideoOverlay041.cs nao encontrado.' }
    $videoMethods = @'

    private const int WM_NCHITTEST_044 = 0x0084;
    private const int HTCLIENT_044 = 1;
    private const int HTLEFT_044 = 10;
    private const int HTRIGHT_044 = 11;
    private const int HTTOP_044 = 12;
    private const int HTTOPLEFT_044 = 13;
    private const int HTTOPRIGHT_044 = 14;
    private const int HTBOTTOM_044 = 15;
    private const int HTBOTTOMLEFT_044 = 16;
    private const int HTBOTTOMRIGHT_044 = 17;

    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);
        if (m.Msg != WM_NCHITTEST_044 || (int)m.Result != HTCLIENT_044) return;
        long raw = m.LParam.ToInt64();
        int sx = unchecked((short)(raw & 0xffff));
        int sy = unchecked((short)((raw >> 16) & 0xffff));
        Point p = PointToClient(new Point(sx, sy));
        const int grip = 7;
        bool left = p.X <= grip, right = p.X >= ClientSize.Width - grip;
        bool top = p.Y <= grip, bottom = p.Y >= ClientSize.Height - grip;
        if (left && top) m.Result = (IntPtr)HTTOPLEFT_044;
        else if (right && top) m.Result = (IntPtr)HTTOPRIGHT_044;
        else if (left && bottom) m.Result = (IntPtr)HTBOTTOMLEFT_044;
        else if (right && bottom) m.Result = (IntPtr)HTBOTTOMRIGHT_044;
        else if (left) m.Result = (IntPtr)HTLEFT_044;
        else if (right) m.Result = (IntPtr)HTRIGHT_044;
        else if (top) m.Result = (IntPtr)HTTOP_044;
        else if (bottom) m.Result = (IntPtr)HTBOTTOM_044;
    }
'@
    $videoText = $videoText.Insert($last, $videoMethods)
}

foreach ($m in @('CurrentVersion = "1.0.44.0"','ApplyHub044();')) {
    if ($mainText -notlike "*$m*") { throw "MainForm 1.0.44 sem $m" }
}
$hubSourceText = Get-Content $hub044Source -Raw
foreach ($m in @('PERFIL DO MOTORISTA','VIAGEM ATUAL','RÁDIO GAT • TOCANDO AGORA','SOBREPOSIÇÃO DE VÍDEO')) {
    if ($hubSourceText -notlike "*$m*") { throw "Hub 1.0.44 sem $m" }
}
foreach ($m in @('FormBorderStyle = FormBorderStyle.None','WM_NCHITTEST_044','close044')) {
    if ($truckText -notlike "*$m*") { throw "Truck overlay 1.0.44 sem $m" }
}
foreach ($m in @('FormBorderStyle = FormBorderStyle.None','dragStart044','closeWindow044','WM_NCHITTEST_044')) {
    if ($videoText -notlike "*$m*") { throw "Video overlay 1.0.44 sem $m" }
}

Set-Content $main.FullName $mainText -Encoding UTF8
Set-Content $hub42.FullName $hub42Text -Encoding UTF8
Set-Content $truck.FullName $truckText -Encoding UTF8
Set-Content $video.FullName $videoText -Encoding UTF8
Set-Content $project.FullName $projectText -Encoding UTF8
Write-Host 'GAT Telemetria 1.0.44: home moderna com caminhao GAT LOG, tipografia renovada e overlays sem barra branca.'
