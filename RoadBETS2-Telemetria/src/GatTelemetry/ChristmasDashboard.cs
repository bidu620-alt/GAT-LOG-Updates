using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json.Linq;

namespace GatTelemetry;

internal sealed class ChristmasDashboard : Form
{
    private readonly Func<Task<JObject>> read;
    private readonly Func<string> driver;
    private readonly Timer clock = new Timer { Interval = 250 };
    private readonly ContextMenuStrip menu = new ContextMenuStrip();
    private JObject data;
    private bool busy, closing, dragging, resizing;
    private Point drag, resizeStart;
    private Rectangle resizeStartBounds;
    private int resizeEdge;
    private const int EdgeLeft=1, EdgeRight=2, EdgeTop=4, EdgeBottom=8;
    private const int DefaultWidth=600, DefaultHeight=200, MinPanelWidth=180, MinPanelHeight=60;
    private string SizePath => Path.Combine(ClientStore.DataDir,"dash-natal-size.txt");
    private Image portrait;
    private Bitmap landscape;
    private byte[] alpha;
    private string photoKey;
    private const float W=1200,H=400;
    private static readonly Color Gold=Color.FromArgb(255,215,135), Ice=Color.FromArgb(172,221,244);
    private static readonly CultureInfo Pt=CultureInfo.GetCultureInfo("pt-BR");
    internal ChristmasDashboard(Func<Task<JObject>> feed,Func<string> identity)
    {
        read=feed;driver=identity;Text="BiduLog Dash Natal";FormBorderStyle=FormBorderStyle.None;
        AutoScaleMode=AutoScaleMode.None;DoubleBuffered=true;BackColor=Color.FromArgb(5,18,30);TopMost=true;ShowInTaskbar=true;StartPosition=FormStartPosition.Manual;
        Location=new Point(16,40);ApplySize(LoadSize(),false);
        using(var stream=typeof(ChristmasDashboard).Assembly.GetManifestResourceStream("BiduLog.DashNatal.Base.png")){
            if(stream==null)throw new InvalidOperationException("Arte da Dash Natal não encontrada.");
            using(var im=Image.FromStream(stream))landscape=new Bitmap(im);
        }
        if(landscape.Width!=1200||landscape.Height!=400)throw new InvalidOperationException("Dimensões inesperadas da arte.");
        alpha=new byte[1200*400];
        for(int y=0;y<400;y++)for(int x=0;x<1200;x++)alpha[y*1200+x]=landscape.GetPixel(x,y).A;
        menu.Items.Add("Selecionar foto do motorista...",null,(s,e)=>ChoosePhoto());
        menu.Items.Add("Remover foto",null,(s,e)=>{try{File.Delete(PhotoPath());photoKey=null;LoadPhoto();}catch(Exception ex){MessageBox.Show(this,ex.Message);}});
        menu.Items.Add("Restaurar tamanho original",null,(s,e)=>ApplySize(new Size(DefaultWidth,DefaultHeight)));
        menu.Items.Add("Sempre no topo",null,(s,e)=>TopMost=!TopMost);menu.Items.Add("Fechar Dash Natal",null,(s,e)=>Close());ContextMenuStrip=menu;
        clock.Tick+=async(s,e)=>await RefreshData();Shown+=(s,e)=>{LoadPhoto();clock.Start();};
        FormClosed+=(s,e)=>{closing=true;clock.Stop();clock.Dispose();portrait?.Dispose();landscape?.Dispose();menu.Dispose();};
        Resize+=(s,e)=>Shape();Shape();
    }
    protected override bool ShowWithoutActivation=>true;
    private Size LoadSize()
    {
        try{
            var parts=File.ReadAllText(SizePath).Trim().Split(',');
            if(parts.Length==2&&int.TryParse(parts[0],out int w)&&int.TryParse(parts[1],out int h)&&w>0&&h>0)return new Size(w,h);
        }catch{}
        try{
            string legacy=Path.Combine(ClientStore.DataDir,"dash-natal-width.txt");
            if(int.TryParse(File.ReadAllText(legacy),out int w)&&w>0)return new Size(w,Math.Max(1,w/3));
        }catch{}
        return new Size(DefaultWidth,DefaultHeight);
    }
    private void SaveSize()
    {
        try{ClientStore.Ensure();File.WriteAllText(SizePath,ClientSize.Width.ToString(CultureInfo.InvariantCulture)+","+ClientSize.Height.ToString(CultureInfo.InvariantCulture));}catch{}
    }
    private static Size FreeSize(Size requested,int availableWidth,int availableHeight)
    {
        int maxW=Math.Max(1,availableWidth),maxH=Math.Max(1,availableHeight);
        return new Size(Math.Max(Math.Min(MinPanelWidth,maxW),Math.Min(maxW,requested.Width)),
                        Math.Max(Math.Min(MinPanelHeight,maxH),Math.Min(maxH,requested.Height)));
    }
    private void ApplySize(Size requested,bool persist=true)
    {
        var area=Screen.FromControl(this).WorkingArea;
        var next=FreeSize(requested,area.Width,area.Height);
        if(ClientSize!=next)ClientSize=next;
        if(persist)SaveSize();
    }
    // Corners and sides are independent: no fixed ratio or size steps.
    private int ResizeEdge(Point p)
    {
        float x=p.X/(float)Math.Max(1,ClientSize.Width), y=p.Y/(float)Math.Max(1,ClientSize.Height);
        if(x<=.09f&&y<=.29f)return EdgeLeft|EdgeTop;
        if(x>=.91f&&y<=.29f)return EdgeRight|EdgeTop;
        if(x<=.09f&&y>=.74f)return EdgeLeft|EdgeBottom;
        if(x>=.91f&&y>=.74f)return EdgeRight|EdgeBottom;
        if(x<=.035f)return EdgeLeft;
        if(x>=.965f)return EdgeRight;
        if(y<=.18f)return EdgeTop;
        if(y>=.90f)return EdgeBottom;
        return 0;
    }
    private static Rectangle ResizeBounds(Rectangle start,int edge,int dx,int dy,int maxW,int maxH)
    {
        int w=start.Width,h=start.Height;
        if((edge&EdgeLeft)!=0)w-=dx;else if((edge&EdgeRight)!=0)w+=dx;
        if((edge&EdgeTop)!=0)h-=dy;else if((edge&EdgeBottom)!=0)h+=dy;
        var size=FreeSize(new Size(w,h),maxW,maxH);
        // Dragging a top/left edge anchors the opposite edge.
        int x=(edge&EdgeLeft)!=0?start.Right-size.Width:start.Left;
        int y=(edge&EdgeTop)!=0?start.Bottom-size.Height:start.Top;
        return new Rectangle(x,y,size.Width,size.Height);
    }
    private static Cursor EdgeCursor(int edge)
    {
        if(edge==(EdgeLeft|EdgeTop)||edge==(EdgeRight|EdgeBottom))return Cursors.SizeNWSE;
        if(edge==(EdgeRight|EdgeTop)||edge==(EdgeLeft|EdgeBottom))return Cursors.SizeNESW;
        if((edge&(EdgeLeft|EdgeRight))!=0)return Cursors.SizeWE;
        if((edge&(EdgeTop|EdgeBottom))!=0)return Cursors.SizeNS;
        return Cursors.Default;
    }
    private void Shape()
    {
        if(alpha==null||ClientSize.Width<1||ClientSize.Height<1)return;
        var next=new Region();next.MakeEmpty();
        int w=ClientSize.Width,h=ClientSize.Height;
        using(var shape=new GraphicsPath(FillMode.Winding)){
            for(int y=0;y<h;y++){
                int sy=Math.Min(399,y*400/h),start=-1;
                for(int x=0;x<=w;x++){
                    bool visible=x<w&&alpha[sy*1200+Math.Min(1199,x*1200/w)]>=32;
                    if(visible&&start<0)start=x;
                    if(!visible&&start>=0){shape.AddRectangle(new Rectangle(start,y,x-start,1));start=-1;}
                }
            }
            next.Union(shape);
        }
        var old=Region;Region=next;old?.Dispose();Invalidate();
    }
    private string Identity()=> (driver()??"").Trim();
    private string PhotoPath(){string name=Identity();if(name.Length==0)name="local";using(var sha=SHA256.Create())return Path.Combine(ClientStore.DataDir,"dash-natal-"+BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(name.ToLowerInvariant()))).Replace("-","")+".jpg");}
    private void LoadPhoto(){string path=PhotoPath();if(path==photoKey)return;photoKey=path;portrait?.Dispose();portrait=null;try{if(File.Exists(path))using(var im=Image.FromFile(path))portrait=new Bitmap(im);}catch{}Invalidate();}
    private void ChoosePhoto(){using(var dlg=new OpenFileDialog{Title="Foto do motorista - Dash Natal",Filter="Imagens|*.jpg;*.jpeg;*.png;*.bmp",CheckFileExists=true}){
        if(dlg.ShowDialog(this)!=DialogResult.OK)return;try{if(new FileInfo(dlg.FileName).Length>15*1024*1024)throw new InvalidOperationException("Imagem maior que 15 MB.");using(var im=Image.FromFile(dlg.FileName)){if(im.Width>12000||im.Height>12000)throw new InvalidOperationException("Imagem muito grande.");using(var b=new Bitmap(384,384))using(var g=Graphics.FromImage(b)){g.Clear(Color.Black);g.InterpolationMode=InterpolationMode.HighQualityBicubic;Cover(g,im,new RectangleF(0,0,384,384));ClientStore.Ensure();b.Save(PhotoPath(),System.Drawing.Imaging.ImageFormat.Jpeg);}}photoKey=null;LoadPhoto();}catch(Exception ex){MessageBox.Show(this,ex.Message,"Foto do motorista");}}}
    private async Task RefreshData(){if(busy||closing||!Visible)return;busy=true;try{var next=await read();if(!closing){data=next;LoadPhoto();Invalidate();}}catch{if(!closing){data=null;Invalidate();}}finally{busy=false;}}
    private static double Num(JObject d,string p){double v=d?.SelectToken(p,false)?.Value<double?>()??0;return double.IsNaN(v)||double.IsInfinity(v)?0:v;}
    private static bool Flag(JObject d,string p)=>d?.SelectToken(p,false)?.Value<bool?>()==true;
    private static string Str(JObject d,string p,string fallback="—"){var t=(string)d?.SelectToken(p,false);return string.IsNullOrWhiteSpace(t)?fallback:t;}
    private static string N(double x)=>Math.Round(Math.Max(0,x)).ToString("0",Pt);
    private static void Cover(Graphics g,Image im,RectangleF r){float s=Math.Max(r.Width/im.Width,r.Height/im.Height),w=im.Width*s,h=im.Height*s;g.DrawImage(im,new RectangleF(r.X+(r.Width-w)/2,r.Y+(r.Height-h)/2,w,h));}
    private static void Value(Graphics g,string text,float x,float baseline,float maxWidth,float size,Color color,bool center=false,bool right=false,bool italic=true)
    {
        if(string.IsNullOrWhiteSpace(text))text="—";
        var style=FontStyle.Bold|(italic?FontStyle.Italic:FontStyle.Regular);
        using(var fmt=(StringFormat)StringFormat.GenericTypographic.Clone())
        using(var family=new FontFamily("Segoe UI")){
            fmt.FormatFlags|=StringFormatFlags.NoWrap|StringFormatFlags.MeasureTrailingSpaces;
            float actual=size;
            using(var probe=new Font(family,size,style,GraphicsUnit.Pixel)){
                float measured=g.MeasureString(text,probe,PointF.Empty,fmt).Width;
                if(measured>maxWidth)actual=size*maxWidth/measured;
            }
            using(var path=new GraphicsPath()){
                float ascent=actual*family.GetCellAscent(style)/family.GetEmHeight(style);
                path.AddString(text,family,(int)style,actual,new PointF(0,0),fmt);
                var bounds=path.GetBounds();
                float left=center?x-bounds.Width/2:right?x-bounds.Width:x;
                using(var matrix=new Matrix()){matrix.Translate(left-bounds.X,baseline-ascent);path.Transform(matrix);}
                if(color!=Color.Black){using(var pen=new Pen(Color.FromArgb(160,2,15,22),size>=60?2f:1f){LineJoin=LineJoin.Round})g.DrawPath(pen,path);}
                using(var brush=new SolidBrush(color))g.FillPath(brush,path);
            }
        }
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;
        g.InterpolationMode=InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode=PixelOffsetMode.HighQuality;
        g.ScaleTransform(ClientSize.Width/W,ClientSize.Height/H);
        if(landscape==null)return;
        g.DrawImage(landscape,new RectangleF(0,0,W,H));
        if(portrait!=null){
            var avatar=new RectangleF(48,119,122,122);
            using(var path=new GraphicsPath()){
                path.AddEllipse(avatar);var state=g.Save();g.SetClip(path);
                Cover(g,portrait,avatar);g.Restore(state);
            }
            var icon=new Rectangle(126,205,47,43);
            using(var camera=new GraphicsPath()){
                camera.AddEllipse(icon);var state=g.Save();g.SetClip(camera);
                g.DrawImage(landscape,icon,icon,GraphicsUnit.Pixel);g.Restore(state);
            }
        }
        bool online=Flag(data,"display.connected"),job=online&&Flag(data,"display.on_job");
        double speed=Num(data,"truck.speed_kmh"),limit=Num(data,"truck.speed_limit_kmh");
        double fuel=Math.Max(0,Math.Min(100,Num(data,"fuel.percent")));
        string name=Str(data,"driver",Identity());
        Value(g,name,239,168,115,27,Color.White);
        Value(g,online?Str(data,"truck.model"):"—",246,233,144,23,Color.White);
        Value(g,job?Str(data,"route.source"):"—",89,326,101,24,Color.White);
        Value(g,job?Str(data,"route.destination"):"—",331,326,106,24,Color.White);
        Value(g,online&&limit>0?N(limit):"—",785,169,52,31,Color.Black,true,false,false);
        Value(g,job?N(Num(data,"route.distance_remaining_km"))+" km":"—",1139,188,158,45,Color.White,false,true);
        Value(g,online?N(Num(data,"fuel.current_l"))+" L":"—",835,253,93,24,Color.White);
        var track=new RectangleF(934,235,157,14);
        using(var b=new SolidBrush(Color.FromArgb(8,23,33)))g.FillRectangle(b,track);
        if(online&&fuel>0){
            var bar=new RectangleF(934,235,(float)(157*fuel/100),14);
            using(var b=new LinearGradientBrush(bar,Color.FromArgb(169,255,48),Color.FromArgb(11,158,0),90f))g.FillRectangle(b,bar);
        }
        Value(g,online?N(fuel)+"%":"—",1150,248,55,23,Color.White,false,true);
        Value(g,job?Num(data,"cargo.weight_t").ToString("0.#",Pt)+" t":"—",816,328,90,24,Color.White);
        Value(g,job?Str(data,"cargo.name"):"Sem carga",997,326,111,21,Color.White);
        if(online){
            var state=g.Save();g.TranslateTransform(595,219);
            g.RotateTransform((float)(-135+Math.Min(160,Math.Max(0,speed))*270/160));
            var needle=new[]{new PointF(-5,1),new PointF(0,-97),new PointF(5,1)};
            using(var brush=new LinearGradientBrush(new RectangleF(-5,-97,10,98),Color.FromArgb(255,179,164),Color.FromArgb(137,0,0),0f))g.FillPolygon(brush,needle);
            using(var pen=new Pen(Color.FromArgb(250,90,56),1))g.DrawPolygon(pen,needle);
            g.Restore(state);
            using(var brush=new SolidBrush(Color.FromArgb(12,38,54)))g.FillEllipse(brush,587,211,16,16);
            using(var pen=new Pen(Color.FromArgb(88,123,148),2))g.DrawEllipse(pen,587,211,16,16);
        }
        Color sc=online&&limit>0&&speed>limit+1?Color.FromArgb(255,86,86):Color.White;
        Value(g,online?N(speed):"—",595,256,138,77,sc,true);
        Value(g,!online?"—":Flag(data,"truck.cruise")?N(Num(data,"truck.cruise_speed_kmh"))+" km/h":"OFF",615,334,108,22,Color.FromArgb(81,250,53),true);
        if(!online)Value(g,"GPS DESCONECTADO",595,284,150,11,Color.LightSalmon,true);
        // Small, unobtrusive diagonal grip on the lower-right trim.
        using(var pen=new Pen(Color.FromArgb(215,255,222,155),1.8f)){
            g.DrawLine(pen,1141,365,1151,355);g.DrawLine(pen,1147,367,1154,360);g.DrawLine(pen,1153,368,1157,364);
        }
    }
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if(e.Button!=MouseButtons.Left)return;
        resizeEdge=ResizeEdge(e.Location);
        if(resizeEdge!=0){resizing=true;resizeStart=Cursor.Position;resizeStartBounds=Bounds;Cursor=EdgeCursor(resizeEdge);}
        else{drag=e.Location;dragging=true;}
        Capture=true;
    }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if(resizing&&e.Button==MouseButtons.Left){
            var pos=Cursor.Position;
            var area=Screen.FromPoint(resizeStart).WorkingArea;
            var next=ResizeBounds(resizeStartBounds,resizeEdge,pos.X-resizeStart.X,pos.Y-resizeStart.Y,area.Width,area.Height);
            if(Bounds!=next)Bounds=next;
        }else if(dragging&&e.Button==MouseButtons.Left)Location=new Point(Left+e.X-drag.X,Top+e.Y-drag.Y);
        else Cursor=EdgeCursor(ResizeEdge(e.Location));
    }
    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if(resizing)SaveSize();
        resizing=false;dragging=false;resizeEdge=0;Capture=false;
    }
    protected override void OnMouseCaptureChanged(EventArgs e)
    {
        base.OnMouseCaptureChanged(e);
        if(!Capture){if(resizing)SaveSize();resizing=false;dragging=false;resizeEdge=0;}
    }
    protected override void OnMouseDoubleClick(MouseEventArgs e){base.OnMouseDoubleClick(e);float x=e.X*W/ClientSize.Width,y=e.Y*H/ClientSize.Height;if(x>=45&&x<=174&&y>=112&&y<=249)ChoosePhoto();}
    protected override void OnMouseClick(MouseEventArgs e){base.OnMouseClick(e);float x=e.X*W/ClientSize.Width,y=e.Y*H/ClientSize.Height;if(e.Button==MouseButtons.Left&&x>=130&&x<=170&&y>=205&&y<=248)ChoosePhoto();}
}
