param([switch]$DebugMode)

Add-Type -AssemblyName PresentationFramework, PresentationCore, WindowsBase
$ErrorActionPreference = "Stop"
[System.Threading.Thread]::CurrentThread.CurrentCulture = [System.Globalization.CultureInfo]::GetCultureInfo("pt-BR")
[System.Threading.Thread]::CurrentThread.CurrentUICulture = [System.Globalization.CultureInfo]::GetCultureInfo("pt-BR")

$Base = Split-Path -Parent $MyInvocation.MyCommand.Path
$TelemetryPath = Join-Path $Base "data\telemetry.json"

$native = @"
using System;
using System.Runtime.InteropServices;
public static class RoadLifeNative {
    [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
    [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    [DllImport("user32.dll")] public static extern int GetWindowLong(IntPtr hWnd, int nIndex);
    [DllImport("user32.dll")] public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
}
"@
Add-Type $native

[xml]$xaml = @"
<Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="ROADLIFE Dashboard2"
        WindowStyle="None"
        ResizeMode="CanResizeWithGrip"
        AllowsTransparency="True"
        Background="Transparent"
        ShowInTaskbar="True"
        Topmost="True"
        Width="1200"
        Height="297"
        MinWidth="780"
        MinHeight="193"
        WindowStartupLocation="CenterScreen">
  <Grid Name="Root">
    <Viewbox Stretch="Fill">
      <Canvas Width="1332" Height="330">
        <Border Canvas.Left="16" Canvas.Top="35" Width="165" Height="260" Background="#80042012" CornerRadius="28"/>
        <Border Canvas.Left="190" Canvas.Top="35" Width="340" Height="110" Background="#80042012" CornerRadius="18"/>
        <Border Canvas.Left="190" Canvas.Top="147" Width="145" Height="74" Background="#80042012" CornerRadius="16"/>
        <Border Canvas.Left="337" Canvas.Top="147" Width="193" Height="74" Background="#80042012" CornerRadius="16"/>
        <Border Canvas.Left="532" Canvas.Top="62" Width="62" Height="84" Background="#80042012" CornerRadius="14"/>
        <Border Canvas.Left="532" Canvas.Top="149" Width="62" Height="72" Background="#80042012" CornerRadius="14"/>
        <Ellipse Canvas.Left="603" Canvas.Top="32" Width="235" Height="215" Fill="#80042012"/>
        <Border Canvas.Left="840" Canvas.Top="65" Width="74" Height="78" Background="#80042012" CornerRadius="18"/>
        <Border Canvas.Left="840" Canvas.Top="147" Width="74" Height="74" Background="#80042012" CornerRadius="18"/>
        <Border Canvas.Left="916" Canvas.Top="35" Width="410" Height="190" Background="#80042012" CornerRadius="22"/>
        <Border Canvas.Left="190" Canvas.Top="229" Width="355" Height="84" Background="#80042012" CornerRadius="18"/>
        <Border Canvas.Left="547" Canvas.Top="229" Width="360" Height="84" Background="#80042012" CornerRadius="18"/>
        <Border Canvas.Left="909" Canvas.Top="229" Width="417" Height="84" Background="#80042012" CornerRadius="18"/>

        <Image Name="StructureImage" Canvas.Left="0" Canvas.Top="0" Width="1332" Height="330" Stretch="Fill"/>

        <TextBlock Canvas.Left="46" Canvas.Top="66" Width="108" FontSize="20" FontWeight="Bold" Foreground="White" TextAlignment="Center" Text="ROADLIFE"/>
        <TextBlock Canvas.Left="68" Canvas.Top="91" Width="64" FontSize="11" FontWeight="Bold" Foreground="White" TextAlignment="Center" Text="ETS2"/>
        <TextBlock Name="OnlineText" Canvas.Left="44" Canvas.Top="130" Width="115" FontSize="15" FontWeight="Bold" Foreground="White" TextAlignment="Center" Text="● ONLINE"/>
        <TextBlock Name="DateText" Canvas.Left="43" Canvas.Top="207" Width="120" FontSize="15" FontWeight="Bold" Foreground="White" TextAlignment="Center" Text="--/--/--"/>
        <TextBlock Name="TimeText" Canvas.Left="43" Canvas.Top="239" Width="120" FontSize="15" FontWeight="Bold" Foreground="White" TextAlignment="Center" Text="--:--:--"/>

        <TextBlock Canvas.Left="214" Canvas.Top="45" Width="120" FontSize="13" FontWeight="Bold" Foreground="White" Text="ROTA ATUAL"/>
        <TextBlock Name="RouteText" Canvas.Left="207" Canvas.Top="69" Width="312" FontSize="23" FontWeight="Bold" Foreground="White" TextAlignment="Center" Text="— → —"/>
        <TextBlock Name="StartKmText" Canvas.Left="210" Canvas.Top="118" Width="64" FontSize="13" FontWeight="Bold" Foreground="White" Text="0 km"/>
        <TextBlock Name="RemainKmTop" Canvas.Left="456" Canvas.Top="118" Width="62" FontSize="13" FontWeight="Bold" Foreground="White" TextAlignment="Right" Text="0 km"/>

        <TextBlock Canvas.Left="207" Canvas.Top="157" Width="116" FontSize="13" FontWeight="Bold" Foreground="White" TextAlignment="Center" Text="PESO"/>
        <TextBlock Name="CargoWeight" Canvas.Left="207" Canvas.Top="183" Width="116" FontSize="23" FontWeight="Bold" Foreground="White" TextAlignment="Center" Text="0,0 t"/>

        <TextBlock Canvas.Left="355" Canvas.Top="157" Width="155" FontSize="13" FontWeight="Bold" Foreground="White" TextAlignment="Center" Text="COMBUSTÍVEL"/>
        <TextBlock Name="FuelText" Canvas.Left="347" Canvas.Top="181" Width="170" FontSize="17" FontWeight="Bold" Foreground="White" TextAlignment="Center" Text="0 / 0 L"/>
        <TextBlock Name="FuelPct" Canvas.Left="453" Canvas.Top="205" Width="52" FontSize="16" FontWeight="Bold" Foreground="White" TextAlignment="Center" Text="0%"/>
        <Border Canvas.Left="357" Canvas.Top="211" Width="125" Height="8" Background="#5530FF78" CornerRadius="4"/>
        <Border Name="FuelBar" Canvas.Left="357" Canvas.Top="211" Width="0" Height="8" Background="#FF4CFF8A" CornerRadius="4"/>

        <TextBlock Canvas.Left="536" Canvas.Top="71" Width="54" FontSize="11" FontWeight="Bold" Foreground="White" TextAlignment="Center" Text="MARCHA"/>
        <TextBlock Name="GearText" Canvas.Left="530" Canvas.Top="95" Width="66" FontSize="24" FontWeight="Bold" Foreground="White" TextAlignment="Center" Text="N"/>
        <TextBlock Canvas.Left="536" Canvas.Top="158" Width="54" FontSize="11" FontWeight="Bold" Foreground="White" TextAlignment="Center" Text="TEMP"/>
        <TextBlock Name="TempText" Canvas.Left="531" Canvas.Top="183" Width="64" FontSize="17" FontWeight="Bold" Foreground="White" TextAlignment="Center" Text="0°C"/>

        <TextBlock Name="SpeedText" Canvas.Left="653" Canvas.Top="91" Width="124" FontSize="68" FontWeight="Bold" Foreground="White" TextAlignment="Center" Text="0"/>
        <TextBlock Canvas.Left="672" Canvas.Top="160" Width="86" FontSize="18" FontWeight="Bold" Foreground="White" TextAlignment="Center" Text="km/h"/>

        <Ellipse Canvas.Left="845" Canvas.Top="74" Width="63" Height="63" Fill="#FFF5F5F5" Stroke="#FFFF3737" StrokeThickness="6"/>
        <TextBlock Name="LimitText" Canvas.Left="845" Canvas.Top="90" Width="63" FontSize="28" FontWeight="Bold" Foreground="#FF111111" TextAlignment="Center" Text="—"/>
        <TextBlock Canvas.Left="840" Canvas.Top="139" Width="74" FontSize="10" FontWeight="Bold" Foreground="White" TextAlignment="Center" Text="LIMITE"/>
        <TextBlock Canvas.Left="840" Canvas.Top="169" Width="74" FontSize="11" FontWeight="Bold" Foreground="White" TextAlignment="Center" Text="CRUISE"/>
        <TextBlock Name="CruiseText" Canvas.Left="840" Canvas.Top="194" Width="74" FontSize="17" FontWeight="Bold" Foreground="White" TextAlignment="Center" Text="OFF"/>

        <TextBlock Canvas.Left="936" Canvas.Top="48" Width="150" FontSize="14" FontWeight="Bold" Foreground="White" Text="CARGA DINÂMICA"/>
        <TextBlock Canvas.Left="1110" Canvas.Top="50" Width="175" FontSize="9" FontWeight="Bold" Foreground="White" TextAlignment="Right" Text="Reconhecimento automático"/>
        <TextBlock Name="CargoRightName" Canvas.Left="1100" Canvas.Top="83" Width="185" FontSize="30" FontWeight="Bold" Foreground="White" TextAlignment="Center" Text="SEM CARGA"/>
        <Image Name="CargoPreview" Canvas.Left="932" Canvas.Top="94" Width="320" Height="126" Stretch="Uniform"/>

        <TextBlock Canvas.Left="211" Canvas.Top="247" Width="155" FontSize="11" FontWeight="Bold" Foreground="White" TextAlignment="Center" Text="DISTÂNCIA RESTANTE"/>
        <TextBlock Name="RemainBottom" Canvas.Left="220" Canvas.Top="274" Width="138" FontSize="21" FontWeight="Bold" Foreground="White" TextAlignment="Center" Text="0 km"/>
        <TextBlock Canvas.Left="386" Canvas.Top="247" Width="175" FontSize="11" FontWeight="Bold" Foreground="White" TextAlignment="Center" Text="PERCURSO DA VIAGEM"/>
        <TextBlock Name="TravelBottom" Canvas.Left="407" Canvas.Top="274" Width="138" FontSize="21" FontWeight="Bold" Foreground="White" TextAlignment="Center" Text="0 km"/>
        <TextBlock Canvas.Left="576" Canvas.Top="247" Width="145" FontSize="11" FontWeight="Bold" Foreground="White" TextAlignment="Center" Text="CHEGADA ESTIMADA"/>
        <TextBlock Name="EtaText" Canvas.Left="599" Canvas.Top="274" Width="100" FontSize="21" FontWeight="Bold" Foreground="White" TextAlignment="Center" Text="—"/>
        <TextBlock Canvas.Left="716" Canvas.Top="247" Width="150" FontSize="11" FontWeight="Bold" Foreground="White" TextAlignment="Center" Text="CAMINHÃO"/>
        <TextBlock Name="TruckText" Canvas.Left="708" Canvas.Top="274" Width="166" FontSize="18" FontWeight="Bold" Foreground="White" TextAlignment="Center" Text="—"/>
        <TextBlock Canvas.Left="930" Canvas.Top="247" Width="88" FontSize="13" FontWeight="Bold" Foreground="White" TextAlignment="Center" Text="DANOS"/>
        <TextBlock Canvas.Left="1011" Canvas.Top="250" Width="82" FontSize="10" FontWeight="Bold" Foreground="White" TextAlignment="Center" Text="Caminhão"/>
        <TextBlock Name="TruckDamageText" Canvas.Left="1011" Canvas.Top="274" Width="82" FontSize="18" FontWeight="Bold" Foreground="White" TextAlignment="Center" Text="0,0%"/>
        <TextBlock Canvas.Left="1110" Canvas.Top="250" Width="82" FontSize="10" FontWeight="Bold" Foreground="White" TextAlignment="Center" Text="Reboque"/>
        <TextBlock Name="TrailerDamageText" Canvas.Left="1110" Canvas.Top="274" Width="82" FontSize="18" FontWeight="Bold" Foreground="White" TextAlignment="Center" Text="0,0%"/>
        <TextBlock Canvas.Left="1208" Canvas.Top="250" Width="82" FontSize="10" FontWeight="Bold" Foreground="White" TextAlignment="Center" Text="Carga"/>
        <TextBlock Name="CargoDamageText" Canvas.Left="1208" Canvas.Top="274" Width="82" FontSize="18" FontWeight="Bold" Foreground="White" TextAlignment="Center" Text="0,0%"/>
      </Canvas>
    </Viewbox>
  </Grid>
</Window>
"@

$reader = New-Object System.Xml.XmlNodeReader $xaml
$Window = [Windows.Markup.XamlReader]::Load($reader)

$names = "Root","StructureImage","OnlineText","DateText","TimeText","RouteText","StartKmText","RemainKmTop","CargoWeight","FuelText","FuelPct","FuelBar","GearText","TempText","SpeedText","LimitText","CruiseText","CargoRightName","CargoPreview","RemainBottom","TravelBottom","EtaText","TruckText","TruckDamageText","TrailerDamageText","CargoDamageText"
$ui=@{}
foreach($n in $names){$ui[$n]=$Window.FindName($n)}

$structure = Join-Path $Base "Assets\dashboard-structure-green.png"
if(Test-Path $structure){
  try{
    $b=New-Object Windows.Media.Imaging.BitmapImage
    $b.BeginInit()
    $b.UriSource=New-Object Uri($structure)
    $b.CacheOption=[Windows.Media.Imaging.BitmapCacheOption]::OnLoad
    $b.EndInit()
    $ui.StructureImage.Source=$b
  }catch{}
}

function Fmt-Dec([double]$v,[int]$d=1){$v.ToString("N$d",[Globalization.CultureInfo]::GetCultureInfo("pt-BR"))}
function Fmt-Int([double]$v){([math]::Round($v)).ToString("N0",[Globalization.CultureInfo]::GetCultureInfo("pt-BR"))}
function Safe($obj,[string]$name,$fallback){
  try{$p=$obj.PSObject.Properties[$name];if($null-ne$p -and $null-ne$p.Value){return $p.Value}}catch{}
  return $fallback
}
function Normalize-Cargo([string]$name){
  if([string]::IsNullOrWhiteSpace($name)){return ""}
  $s=$name.Normalize([Text.NormalizationForm]::FormD);$sb=New-Object Text.StringBuilder
  foreach($ch in $s.ToCharArray()){
    if([Globalization.CharUnicodeInfo]::GetUnicodeCategory($ch)-ne[Globalization.UnicodeCategory]::NonSpacingMark){[void]$sb.Append($ch)}
  }
  ([regex]::Replace($sb.ToString().ToLowerInvariant(),'[^a-z0-9]+','-')).Trim('-')
}
function Set-ImageFile($ctrl,[string]$path){
  if(-not(Test-Path $path)){return $false}
  try{
    $bmp=New-Object Windows.Media.Imaging.BitmapImage
    $bmp.BeginInit();$bmp.UriSource=New-Object Uri($path);$bmp.CacheOption=[Windows.Media.Imaging.BitmapCacheOption]::OnLoad;$bmp.EndInit()
    $ctrl.Source=$bmp
    return $true
  }catch{return $false}
}
function Set-CargoImage([string]$name){
  $folder=Join-Path $Base "Assets\cargo";$slug=Normalize-Cargo $name
  foreach($ext in @("png","jpg","jpeg","webp")){if($slug -and (Set-ImageFile $ui.CargoPreview (Join-Path $folder "$slug.$ext"))){return}}
  foreach($ext in @("png","jpg","jpeg","webp")){if(Set-ImageFile $ui.CargoPreview (Join-Path $folder "default.$ext")){return}}
}

function Apply-Telemetry($d){
  $route=$d.route;$cargo=$d.cargo;$fuel=$d.fuel;$truck=$d.truck;$damage=$d.damage
  $src=[string](Safe $route "source" "—");$dst=[string](Safe $route "destination" "—")
  $remain=[double](Safe $route "distance_remaining_km" 0);$travel=[double](Safe $route "distance_travelled_km" 0)
  $ui.RouteText.Text="$src → $dst";$ui.StartKmText.Text="0 km";$ui.RemainKmTop.Text="$(Fmt-Int $remain) km"
  $ui.RemainBottom.Text="$(Fmt-Int $remain) km";$ui.TravelBottom.Text="$(Fmt-Int $travel) km"

  $cname=[string](Safe $cargo "name" "Sem carga")
  $ui.CargoWeight.Text="$(Fmt-Dec ([double](Safe $cargo "weight_t" 0)) 1) t"
  $ui.CargoRightName.Text=$cname.ToUpperInvariant()
  Set-CargoImage $cname

  $cur=[double](Safe $fuel "current_l" 0);$cap=[double](Safe $fuel "capacity_l" 0);$pct=[double](Safe $fuel "percent" 0)
  if($pct-lt0){$pct=0};if($pct-gt100){$pct=100}
  $ui.FuelText.Text="$(Fmt-Int $cur) / $(Fmt-Int $cap) L";$ui.FuelPct.Text="$(Fmt-Int $pct)%";$ui.FuelBar.Width=125*($pct/100)

  $ui.GearText.Text=[string](Safe $truck "gear" "N")
  $ui.TempText.Text="$(Fmt-Int ([double](Safe $truck "temperature_c" 0)))°C"
  $ui.SpeedText.Text="$(Fmt-Int ([double](Safe $truck "speed_kmh" 0)))"
  $lim=[double](Safe $truck "speed_limit_kmh" 0);$ui.LimitText.Text=$(if($lim-gt0){"$(Fmt-Int $lim)"}else{"—"})
  $ui.CruiseText.Text=$(if([bool](Safe $truck "cruise" $false)){"ON"}else{"OFF"})
  $ui.EtaText.Text=[string](Safe $d "eta" "—");$ui.TruckText.Text=[string](Safe $truck "model" "—")

  $ui.TruckDamageText.Text="$(Fmt-Dec ([double](Safe $damage "truck_pct" 0)) 1)%"
  $ui.TrailerDamageText.Text="$(Fmt-Dec ([double](Safe $damage "trailer_pct" 0)) 1)%"
  $ui.CargoDamageText.Text="$(Fmt-Dec ([double](Safe $damage "cargo_pct" 0)) 1)%"
  $ui.OnlineText.Text=$(if([bool](Safe $d "online" $false)){"● ONLINE"}else{"● OFFLINE"})
}

$script:LastWrite=[datetime]::MinValue
$timer=New-Object Windows.Threading.DispatcherTimer
$timer.Interval=[TimeSpan]::FromMilliseconds(500)
$timer.Add_Tick({
  try{
    $now=Get-Date;$ui.DateText.Text=$now.ToString("dd/MM/yy");$ui.TimeText.Text=$now.ToString("HH:mm:ss")
    if(Test-Path $TelemetryPath){
      $fi=Get-Item $TelemetryPath
      if($fi.LastWriteTimeUtc-ne$script:LastWrite){
        $script:LastWrite=$fi.LastWriteTimeUtc
        Apply-Telemetry (Get-Content $TelemetryPath -Raw -Encoding UTF8 | ConvertFrom-Json)
      }
    }
  }catch{if($DebugMode){Write-Host $_.Exception.Message -ForegroundColor Red}}
})
$timer.Start()

$script:ClickThrough=$false;$script:Hwnd=[IntPtr]::Zero
$ui.Root.Add_MouseLeftButtonDown({if(-not$script:ClickThrough){try{$Window.DragMove()}catch{}}})
function Set-ClickThrough([bool]$enable){
  if($script:Hwnd-eq[IntPtr]::Zero){return}
  $style=[RoadLifeNative]::GetWindowLong($script:Hwnd,-20)
  if($enable){$style = $style -bor 0x20;$script:ClickThrough=$true}else{$style = $style -band (-bnot 0x20);$script:ClickThrough=$false}
  [RoadLifeNative]::SetWindowLong($script:Hwnd,-20,$style)|Out-Null
}
$Window.Add_SourceInitialized({
  $helper=New-Object Windows.Interop.WindowInteropHelper($Window);$script:Hwnd=$helper.Handle
  [RoadLifeNative]::RegisterHotKey($script:Hwnd,1001,0,0x77)|Out-Null
  [RoadLifeNative]::RegisterHotKey($script:Hwnd,1002,0,0x78)|Out-Null
  [RoadLifeNative]::RegisterHotKey($script:Hwnd,1003,0,0x79)|Out-Null
  $source=[Windows.Interop.HwndSource]::FromHwnd($script:Hwnd)
  $hook=[Windows.Interop.HwndSourceHook]{
    param($hwnd,$msg,$wParam,$lParam,[ref]$handled)
    if($msg-eq0x0312){
      switch([int]$wParam){
        1001{Set-ClickThrough(-not$script:ClickThrough);$handled.Value=$true}
        1002{$Window.Topmost=-not$Window.Topmost;$handled.Value=$true}
        1003{$Window.Close();$handled.Value=$true}
      }
    }
    [IntPtr]::Zero
  }
  $source.AddHook($hook)
})
$Window.Add_Closed({
  if($script:Hwnd-ne[IntPtr]::Zero){
    [RoadLifeNative]::UnregisterHotKey($script:Hwnd,1001)|Out-Null
    [RoadLifeNative]::UnregisterHotKey($script:Hwnd,1002)|Out-Null
    [RoadLifeNative]::UnregisterHotKey($script:Hwnd,1003)|Out-Null
  }
})

try{Apply-Telemetry (Get-Content $TelemetryPath -Raw -Encoding UTF8|ConvertFrom-Json)}catch{}
[void]$Window.ShowDialog()
