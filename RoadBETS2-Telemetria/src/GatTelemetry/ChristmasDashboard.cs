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
    private int resizeStartWidth;
    private const int DefaultWidth=600, MinPanelWidth=360;
    private string SizePath => Path.Combine(ClientStore.DataDir,"dash-natal-width.txt");
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
        Location=new Point(16,40);ApplyWidth(LoadWidth(),false);
        using(var stream=typeof(ChristmasDashboard).Assembly.GetManifestResourceStream("BiduLog.DashNatal.Base.png")){
            if(stream==null)throw new InvalidOperationException("Arte da Dash Natal não encontrada.");
            using(var im=Image.FromStream(stream))landscape=new Bitmap(im);
        }
        if(landscape.Width!=1200||landscape.Height!=400)throw new InvalidOperationException("Dimensões inesperadas da arte.");
        alpha=new byte[1200*400];
        for(int y=0;y<400;y++)for(int x=0;x<1200;x++)alpha[y*1200+x]=landscape.GetPixel(x,y).A;
        menu.Items.Add("Selecionar foto do motorista...",null,(s,e)=>ChoosePhoto());
        menu.Items.Add("Remover foto",null,(s,e)=>{try{File.Delete(PhotoPath());photoKey=null;LoadPhoto();}catch(Exception ex){MessageBox.Show(this,ex.Message);}});
        menu.Items.Add("Diminuir tamanho (-)",null,(s,e)=>ApplyWidth(ClientSize.Width-60));
        menu.Items.Add("Aumentar tamanho (+)",null,(s,e)=>ApplyWidth(ClientSize.Width+60));
        var sizes=new ToolStripMenuItem("Tamanho proporcional");
        foreach(int size in new[]{360,420,480,540,600,720,900,1080,1200,1440}){int v=size;sizes.DropDownItems.Add(v+" x "+(v/3)+" px",null,(s,e)=>ApplyWidth(v));}
        menu.Items.Add(sizes);
        menu.Items.Add("Restaurar tamanho (600 x 200)",null,(s,e)=>ApplyWidth(DefaultWidth));
        menu.Items.Add("Sempre no topo",null,(s,e)=>TopMost=!TopMost);menu.Items.Add("Fechar Dash Natal",null,(s,e)=>Close());ContextMenuStrip=menu;
        clock.Tick+=async(s,e)=>await RefreshData();Shown+=(s,e)=>{LoadPhoto();clock.Start();};
        FormClosed+=(s,e)=>{closing=true;clock.Stop();clock.Dispose();portrait?.Dispose();landscape?.Dispose();menu.Dispose();};
        Resize+=(s,e)=>Shape();Shape();
    }
    protected override bool ShowWithoutActivation=>true;
    private int LoadWidth()
    {
        try{if(int.TryParse(File.ReadAllText(SizePath),out int n)&&n>0)return n;}catch{}
        return DefaultWidth;
    }
    private void SaveWidth()
    {
        try{ClientStore.Ensure();File.WriteAllText(SizePath,ClientSize.Width.ToString(CultureInfo.InvariantCulture));}catch{}
    }
    // Keep a precise 3:1 aspect ratio in physical pixels, including on small screens.
    private static Size ProportionalSize(int requested,int availableWidth,int availableHeight)
    {
        int upper=Math.Max(3,Math.Min(availableWidth,availableHeight*3));
        upper-=upper%3;
        int lower=Math.Min(MinPanelWidth,upper);
        int width=Math.Max(lower,Math.Min(upper,requested));
        width-=width%3;
        return new Size(width,width/3);
    }
    private void ApplyWidth(int requested,bool persist=true)
    {
        var area=Screen.FromControl(this).WorkingArea;
        var next=ProportionalSize(requested,Math.Max(3,area.Width-16),Math.Max(1,area.Height-16));
        if(ClientSize!=next)ClientSize=next;
        if(persist)SaveWidth();
    }
    private bool IsResizeGrip(Point p)
    {
        // The lower-right border is the grip; the image retains its original silhouette.
        return p.X>=ClientSize.Width*0.92 && p.Y>=ClientSize.Height*0.78;
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
        if(IsResizeGrip(e.Location)){resizing=true;resizeStart=Cursor.Position;resizeStartWidth=ClientSize.Width;}
        else{drag=e.Location;dragging=true;}
        Capture=true;
    }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if(resizing&&e.Button==MouseButtons.Left){
            var pos=Cursor.Position;int dx=pos.X-resizeStart.X,dy=pos.Y-resizeStart.Y;
            ApplyWidth(resizeStartWidth+(Math.Abs(dx)>=Math.Abs(dy*3)?dx:dy*3),false);
        }else if(dragging&&e.Button==MouseButtons.Left)Location=new Point(Left+e.X-drag.X,Top+e.Y-drag.Y);
        else Cursor=IsResizeGrip(e.Location)?Cursors.SizeNWSE:Cursors.Default;
    }
    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if(resizing)SaveWidth();
        resizing=false;dragging=false;Capture=false;
    }
    protected override void OnMouseCaptureChanged(EventArgs e)
    {
        base.OnMouseCaptureChanged(e);
        if(!Capture){if(resizing)SaveWidth();resizing=false;dragging=false;}
    }
    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        if((ModifierKeys&Keys.Control)==Keys.Control && e.Delta!=0)ApplyWidth(ClientSize.Width+(e.Delta>0?60:-60));
    }
    protected override void OnMouseDoubleClick(MouseEventArgs e){base.OnMouseDoubleClick(e);float x=e.X*W/ClientSize.Width,y=e.Y*H/ClientSize.Height;if(x>=45&&x<=174&&y>=112&&y<=249)ChoosePhoto();}
    protected override void OnMouseClick(MouseEventArgs e){base.OnMouseClick(e);float x=e.X*W/ClientSize.Width,y=e.Y*H/ClientSize.Height;if(e.Button==MouseButtons.Left&&x>=130&&x<=170&&y>=205&&y<=248)ChoosePhoto();}
}
