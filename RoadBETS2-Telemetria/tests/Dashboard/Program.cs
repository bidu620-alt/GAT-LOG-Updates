using System;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Newtonsoft.Json.Linq;
class Program {
 static void Check(bool pass,string message){if(!pass)throw new Exception(message);Console.WriteLine("PASS "+message);}
 static string Text(Window w,string name){return ((TextBlock)w.FindName(name)).Text;}
 static void Save(Window w,string file,int width,int height){var root=(FrameworkElement)w.Content;root.Measure(new Size(width,height));root.Arrange(new Rect(0,0,width,height));root.UpdateLayout();var bmp=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);bmp.Render(root);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bmp));using(var stream=File.Create(file))encoder.Save(stream);}
 [STAThread] static int Main(string[] args){try{
  RenderOptions.ProcessRenderMode=System.Windows.Interop.RenderMode.SoftwareOnly;
  string root=args[0],outDir=args[1];Directory.CreateDirectory(outDir);
  var app=Assembly.LoadFrom(Path.Combine(root,"GAT_TELEMETRIA_APP.exe"));var type=app.GetType("GatTelemetry.MainForm",true);
  var form=FormatterServices.GetUninitializedObject(type);
  Window w;using(var stream=File.OpenRead(Path.Combine(root,"RoadLifeDash","RoadLifeDash.xaml")))w=(Window)System.Windows.Markup.XamlReader.Load(stream);
  type.GetField("_roadLifeOverlay",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(form,w);
  var version=(string)type.GetField("CurrentVersion",BindingFlags.NonPublic|BindingFlags.Static).GetRawConstantValue();Check(version=="1.0.68.42","versao oficial 1.0.68.42");
  var newer=type.GetMethod("IsNewer",BindingFlags.NonPublic|BindingFlags.Static);Check((bool)newer.Invoke(null,new object[]{version,"1.0.68.41"})&&!(bool)newer.Invoke(null,new object[]{version,version}),"botao atualizar reconhece 42 acima da 41 sem repetir versao");
  var manifest=JObject.Parse("{'version':'1.0.68.42','setup_url':'https://example.test/setup41.exe','sha256':'fixture'}").ToObject(app.GetType("GatTelemetry.RemoteVersion",true));Check((string)manifest.GetType().GetProperty("EffectiveUrl").GetValue(manifest,null)=="https://example.test/setup41.exe","manifesto compativel com o atualizador existente");
  type.GetMethod("LoadRoadLifeImage",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(form,new object[]{"RoadLifeLogo",Path.Combine(root,"RoadLifeDash","Assets","bidulog-logo.png")});
  var method=type.GetMethod("ApplyRoadLifePayload",BindingFlags.NonPublic|BindingFlags.Instance);
  Check(w.FindName("ThemeGreen")!=null&&w.FindName("ThemeBlue")!=null&&w.FindName("ThemeYellow")!=null&&w.FindName("ThemeRed")!=null,"seletores de tema preservados");
  type.GetMethod("RoadLifeThemeWire",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(form,null);
  var d=JObject.Parse(File.ReadAllText(args[2]));method.Invoke(form,new object[]{d});
  if(args.Length>3&&args[3]=="benchmark"){
   var visual=(FrameworkElement)w.Content;visual.Measure(new Size(1200,380));visual.Arrange(new Rect(0,0,1200,380));visual.UpdateLayout();
   var timer=System.Diagnostics.Stopwatch.StartNew();for(int i=0;i<200;i++)method.Invoke(form,new object[]{d});timer.Stop();double updates=timer.Elapsed.TotalMilliseconds/200;
   timer.Restart();for(int i=0;i<15;i++){d["truck"]["speed_kmh"]=70+i%10;method.Invoke(form,new object[]{d});visual.UpdateLayout();var frame=new RenderTargetBitmap(1200,380,96,96,PixelFormats.Pbgra32);frame.Render(visual);}timer.Stop();
   Console.WriteLine(new JObject{["update_ms"]=updates,["render_ms"]=timer.Elapsed.TotalMilliseconds/15}.ToString(Newtonsoft.Json.Formatting.None));w.Close();return 0;
  }
  ((TextBlock)w.FindName("DateText")).Text="22/11/2026";((TextBlock)w.FindName("TimeText")).Text="10:42:00";
  Check(Text(w,"SourceText")=="Hamburgo"&&Text(w,"DestinationText")=="Dresden","origem e destino separados");
  Check(Text(w,"SpeedText")=="78","velocidade real no mostrador");
  Check(Math.Abs(((RotateTransform)w.FindName("SpeedNeedleRotation")).Angle+3.375)<0.01,"ponteiro proporcional à escala de 0 a 160 km/h");
  Check(w.FindName("RpmText")==null&&w.FindName("TempText")==null,"temperatura e RPM removidos");
  Check(((Image)w.FindName("RoadLifeLogo")).Source!=null,"logo carregada");
  Check(((TextBlock)w.FindName("LimitText")).FontSize>=36&&((SolidColorBrush)((TextBlock)w.FindName("LimitText")).Foreground).Color==Colors.Black,"limite com numero preto grande");
  Check(((SolidColorBrush)((System.Windows.Shapes.Ellipse)w.FindName("LimitSign")).Fill).Color==Colors.White,"placa com fundo branco");
  Check(Text(w,"CruiseText")=="80 km/h","cruzeiro ligado mostra velocidade ajustada");
  Check(Math.Abs(((Border)w.FindName("FuelBar")).Width-24.64)<0.1,"barra de combustível proporcional");
  Check(Text(w,"RangeText").Contains("850"),"autonomia");
  Check(w.AllowsTransparency&&((SolidColorBrush)w.Background).Color.A==0,"janela transparente");
  var cargoImage=(Image)w.FindName("CargoPreview");var firstImage=cargoImage.Source;method.Invoke(form,new object[]{d});Check(firstImage!=null&&object.ReferenceEquals(firstImage,cargoImage.Source),"imagem da carga reutilizada entre atualizacoes");
  var sizeType=app.GetType("GatTelemetry.NeonOverlaySizing",true);var sizing=sizeType.GetMethod("Constrain",BindingFlags.NonPublic|BindingFlags.Static);
  foreach(double dpi in new[]{1.0,1.25,1.5,2.0})foreach(int edge in new[]{1,2,3,4,5,6,7,8}){
   var rect=(int[])sizing.Invoke(null,new object[]{new[]{100,100,900,400},edge,1200*dpi,380*dpi,480*dpi,152*dpi});
   double rw=rect[2]-rect[0],rh=rect[3]-rect[1];Check(Math.Abs(rw-rh*1200/380)<2&&rw>=480*dpi-1,"redimensionamento proporcional borda "+edge+" DPI "+dpi);
  }
  var ptr=System.Runtime.InteropServices.Marshal.AllocHGlobal(16);try{
   System.Runtime.InteropServices.Marshal.Copy(new[]{10,20,730,400},0,ptr,4);object[] argsNative={IntPtr.Zero,0x0214,new IntPtr(8),ptr,false};type.GetMethod("RoadLifeWndProc",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(form,argsNative);var rect=new int[4];System.Runtime.InteropServices.Marshal.Copy(ptr,rect,0,4);Check((bool)argsNative[4]&&Math.Abs((rect[2]-rect[0])-(rect[3]-rect[1])*1200.0/380)<2,"hook nativo preserva proporcao da janela");
  }finally{System.Runtime.InteropServices.Marshal.FreeHGlobal(ptr);}
  Check(w.MinWidth<=480&&w.FindName("NeonResizeGrip")!=null,"reducao ate 480 px e controle de tamanho acessivel");
  var panel=(Border)System.Windows.Media.VisualTreeHelper.GetChild((Canvas)((Viewbox)w.FindName("NeonScaleHost")).Child,0);Check(panel.BorderThickness.Left>=3&&((SolidColorBrush)panel.Background).Color.A>=170,"bordas grossas e fundo mais opaco");
  Check(((TextBlock)w.FindName("SpeedText")).Effect==null&&panel.Effect==null,"sombras pesadas removidas");
  Save(w,Path.Combine(outDir,"Painel-Neon-40-previa.png"),1200,380);Save(w,Path.Combine(outDir,"preview-compacto.png"),720,228);Save(w,Path.Combine(outDir,"preview-minimo.png"),480,152);
  
  d["truck"]["speed_kmh"]=120;method.Invoke(form,new object[]{d});Check(((SolidColorBrush)((TextBlock)w.FindName("SpeedText")).Foreground).Color.R==255,"alerta de velocidade preservado");
  d["display"]["on_job"]=false;d["cargo"]["name"]="Sem carga";method.Invoke(form,new object[]{d});Check(Text(w,"CargoStatusText")=="SEM CARGA ATIVA","estado sem viagem");Save(w,Path.Combine(outDir,"preview-sem-carga.png"),1200,380);
  d["display"]["connected"]=false;method.Invoke(form,new object[]{d});Check(Text(w,"SpeedText")=="—"&&Text(w,"CruiseText")=="—","sem dados não inventa leituras");Save(w,Path.Combine(outDir,"preview-desconectado.png"),1200,380);
  d["display"]["connected"]=true;d["truck"]["cruise"]=false;method.Invoke(form,new object[]{d});Check(Text(w,"CruiseText")=="DESLIGADO","cruzeiro desligado nao mostra velocidade memorizada como ativo");
  var distanceType=app.GetType("GatTelemetry.DashboardDistanceState",true);type.GetField("_dashboardDistance",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(form,Activator.CreateInstance(distanceType,true));
  var raw=JObject.Parse("{'game':{'connected':true},'truck':{'cruiseControlOn':false,'cruiseControlSpeed':80}}");var copy=raw.DeepClone();
  var payload=(JObject)type.GetMethod("RoadLifeOverlayBuildPayload",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(form,new object[]{raw});
  Check(payload["truck"]["cruise"].Value<bool>()==false,"estado real do GPS para cruzeiro desligado");Check(JToken.DeepEquals(raw,copy),"construcao visual nao altera pacote de telemetria");
  Check(w.ShowActivated==false&&w.ShowInTaskbar==false,"abertura do overlay sem ativacao ou foco adicional");
  var accountPush=type.GetMethod("RoadLifeOverlayPush",BindingFlags.NonPublic|BindingFlags.Instance);
  var livePush=type.GetMethod("ApplyNeonLiveFrame",BindingFlags.NonPublic|BindingFlags.Instance);
  var prepare=type.GetMethod("PrepareNeonOpeningFrame",BindingFlags.NonPublic|BindingFlags.Instance);
  var rawA=JObject.Parse("{'game':{'connected':true},'truck':{'speed':78,'model':'Scania','cruiseControlOn':false},'job':{'cargo':'A'},'gameplay':{'onJob':true}}");
  var rawB=(JObject)rawA.DeepClone();rawB["truck"]["speed"]=12;
  accountPush.Invoke(form,new object[]{rawA});prepare.Invoke(form,null);Check(Text(w,"SpeedText")=="78","primeiro quadro preparado antes da abertura");
  Check((bool)livePush.Invoke(form,new object[]{w,rawA}),"fluxo visual atual aceito");
  for(int i=0;i<25;i++)accountPush.Invoke(form,new object[]{rawB});Check(Text(w,"SpeedText")=="78","pacotes da conta nao competem com o quadro visual");
  Check((bool)livePush.Invoke(form,new object[]{w,rawB})&&Text(w,"SpeedText")=="12","nova amostra do fluxo unico atualiza painel");
  var stale=new Window();Check(!(bool)livePush.Invoke(form,new object[]{stale,rawA})&&Text(w,"SpeedText")=="12","resposta antiga de janela fechada nao altera novo painel");stale.Close();
  type.GetField("_neonInteractiveSizing",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(form,true);Check(!(bool)livePush.Invoke(form,new object[]{w,rawA})&&Text(w,"SpeedText")=="12","renderizacao pausada enquanto redimensiona");
  type.GetField("_neonInteractiveSizing",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(form,false);Check((bool)livePush.Invoke(form,new object[]{w,rawA}),"renderizacao retoma apos redimensionar");
  Check((bool)livePush.Invoke(form,new object[]{w,null})&&Text(w,"SpeedText")=="—"&&Text(w,"TripStatusText")=="GPS DESCONECTADO","perda de GPS nao deixa velocidade antiga congelada");
  w.Close();Console.WriteLine("ALL OVERLAY TESTS PASSED");return 0;
 }catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}

