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
    private bool busy, closing, dragging;
    private Point drag;
    private Image portrait, landscape;
    private string photoKey;
    private const float W=1200,H=400;
    private static readonly Color Gold=Color.FromArgb(255,215,135), Ice=Color.FromArgb(172,221,244);
    private static readonly CultureInfo Pt=CultureInfo.GetCultureInfo("pt-BR");
    private const string Winter="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";
    internal ChristmasDashboard(Func<Task<JObject>> feed,Func<string> identity)
    {
        read=feed;driver=identity;Text="BiduLog Dash Natal";FormBorderStyle=FormBorderStyle.None;
        DoubleBuffered=true;BackColor=Color.FromArgb(5,18,30);TopMost=true;ShowInTaskbar=true;StartPosition=FormStartPosition.Manual;
        int width=Math.Min(1200,Screen.PrimaryScreen.WorkingArea.Width-32);Size=new Size(width,width/3);Location=new Point(16,40);
        try{using(var stream=new MemoryStream(Convert.FromBase64String(Winter)))using(var im=Image.FromStream(stream))landscape=new Bitmap(im);}catch{}
        menu.Items.Add("Selecionar foto do motorista...",null,(s,e)=>ChoosePhoto());
        menu.Items.Add("Remover foto",null,(s,e)=>{try{File.Delete(PhotoPath());photoKey=null;LoadPhoto();}catch(Exception ex){MessageBox.Show(this,ex.Message);}});
        foreach(int size in new[]{720,900,1080,1200,1440}){int v=size;menu.Items.Add("Tamanho "+v+" px",null,(s,e)=>{int n=Math.Min(v,Screen.FromControl(this).WorkingArea.Width-16);Size=new Size(n,n/3);});}
        menu.Items.Add("Sempre no topo",null,(s,e)=>TopMost=!TopMost);menu.Items.Add("Fechar Dash Natal",null,(s,e)=>Close());ContextMenuStrip=menu;
        clock.Tick+=async(s,e)=>await RefreshData();Shown+=(s,e)=>{LoadPhoto();clock.Start();};
        FormClosed+=(s,e)=>{closing=true;clock.Stop();clock.Dispose();portrait?.Dispose();landscape?.Dispose();menu.Dispose();};
        Resize+=(s,e)=>Shape();Shape();
    }
    protected override bool ShowWithoutActivation=>true;
    private void Shape(){if(ClientSize.Width<1||ClientSize.Height<1)return;using(var p=Round(new RectangleF(0,0,ClientSize.Width,ClientSize.Height),ClientSize.Width*.035f)){var old=Region;Region=new Region(p);old?.Dispose();}Invalidate();}
    private static GraphicsPath Round(RectangleF r,float radius){var p=new GraphicsPath();float d=Math.Min(Math.Min(r.Width,r.Height),radius*2);p.AddArc(r.X,r.Y,d,d,180,90);p.AddArc(r.Right-d,r.Y,d,d,270,90);p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);p.AddArc(r.X,r.Bottom-d,d,d,90,90);p.CloseFigure();return p;}
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
    private static void Txt(Graphics g,string t,RectangleF r,float size,Color c,bool center=false){
        using(var b=new SolidBrush(c))using(var f=new Font("Segoe UI",size,FontStyle.Bold,GraphicsUnit.Pixel))using(var sf=new StringFormat{Alignment=center?StringAlignment.Center:StringAlignment.Near,LineAlignment=StringAlignment.Center,Trimming=StringTrimming.EllipsisCharacter,FormatFlags=StringFormatFlags.NoWrap})g.DrawString(t??"—",f,b,r,sf);}
    private static void Card(Graphics g,RectangleF r){using(var p=Round(r,18))using(var b=new SolidBrush(Color.FromArgb(215,4,22,34)))using(var pen=new Pen(Color.FromArgb(220,84,166,194),1.8f)){g.FillPath(b,p);g.DrawPath(pen,p);}}
    private static void Field(Graphics g,RectangleF r,string label,string value,float sz=26){Card(g,r);Txt(g,label,new RectangleF(r.X+14,r.Y+4,r.Width-28,18),14,Gold);Txt(g,value,new RectangleF(r.X+14,r.Y+21,r.Width-28,r.Height-23),sz,Color.White);}
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.TextRenderingHint=System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;g.ScaleTransform(ClientSize.Width/W,ClientSize.Height/H);
        var outer=new RectangleF(3,3,W-6,H-6);using(var p=Round(outer,42)){var state=g.Save();g.SetClip(p);using(var b=new LinearGradientBrush(outer,Color.FromArgb(5,18,34),Color.FromArgb(25,60,76),90f))g.FillRectangle(b,outer);if(landscape!=null)Cover(g,landscape,outer);using(var b=new SolidBrush(Color.FromArgb(65,0,12,24)))g.FillRectangle(b,outer);g.Restore(state);using(var pen=new Pen(Color.FromArgb(160,95,42),8))g.DrawPath(pen,p);using(var pen=new Pen(Gold,3))g.DrawPath(pen,p);}
        for(int i=0;i<29;i++){float x=40+i*39.5f;using(var b=new SolidBrush(i%3==0?Gold:i%3==1?Color.White:Color.LightGreen))g.FillEllipse(b,x,7,4,4);}
        Card(g,new RectangleF(421,8,358,53));Txt(g,"BIDULOG TELEMETRIA",new RectangleF(450,8,316,46),25,Gold,true);
        using(var b=new SolidBrush(Color.Firebrick))g.FillPolygon(b,new[]{new PointF(432,32),new PointF(443,9),new PointF(461,33)});using(var b=new SolidBrush(Color.White)){g.FillEllipse(b,439,5,9,9);g.FillRectangle(b,430,30,34,6);}
        bool online=Flag(data,"display.connected"),job=online&&Flag(data,"display.on_job");double speed=Num(data,"truck.speed_kmh"),limit=Num(data,"truck.speed_limit_kmh"),fuel=Math.Max(0,Math.Min(100,Num(data,"fuel.percent")));
        string name=Str(data,"driver",Identity());if(string.IsNullOrWhiteSpace(name))name="—";
        Card(g,new RectangleF(25,71,365,145));var avatar=new RectangleF(39,85,116,116);using(var pp=new GraphicsPath()){pp.AddEllipse(avatar);var st=g.Save();g.SetClip(pp);using(var b=new SolidBrush(Color.FromArgb(17,53,71)))g.FillEllipse(b,avatar);if(portrait!=null)Cover(g,portrait,avatar);else using(var b=new SolidBrush(Ice)){g.FillEllipse(b,77,107,40,40);g.FillEllipse(b,58,151,78,73);}g.Restore(st);}using(var pen=new Pen(Gold,3))g.DrawEllipse(pen,avatar);
        Txt(g,"MOTORISTA",new RectangleF(167,81,211,20),14,Gold);Txt(g,name,new RectangleF(167,102,211,38),27,Color.White);Txt(g,"CAMINHÃO",new RectangleF(167,145,211,20),14,Gold);Txt(g,online?Str(data,"truck.model"):"—",new RectangleF(167,164,211,36),24,Color.White);
        Field(g,new RectangleF(25,224,365,68),"ORIGEM",job?Str(data,"route.source"):"—",25);Field(g,new RectangleF(25,300,365,68),"DESTINO",job?Str(data,"route.destination"):"—",25);
        Card(g,new RectangleF(800,71,375,88));using(var b=new SolidBrush(Color.White))g.FillEllipse(b,817,87,55,55);using(var pen=new Pen(Color.Firebrick,6))g.DrawEllipse(pen,817,87,55,55);Txt(g,online&&limit>0?N(limit):"—",new RectangleF(817,87,55,55),24,Color.Black,true);Txt(g,"LIMITE DA VIA",new RectangleF(882,80,270,20),14,Gold);Txt(g,job?N(Num(data,"route.distance_remaining_km"))+" km":"—",new RectangleF(882,105,270,37),31,Color.White);Txt(g,"DISTÂNCIA RESTANTE",new RectangleF(882,139,270,16),12,Ice);
        Card(g,new RectangleF(800,167,375,82));Txt(g,"COMBUSTÍVEL",new RectangleF(814,173,347,20),14,Gold);Txt(g,online?N(Num(data,"fuel.current_l"))+" L":"—",new RectangleF(814,195,135,40),29,Color.White);using(var b=new SolidBrush(Color.FromArgb(50,80,94)))g.FillRectangle(b,953,207,155,13);using(var b=new SolidBrush(Color.FromArgb(75,220,100)))if(online)g.FillRectangle(b,953,207,(float)(155*fuel/100),13);Txt(g,online?N(fuel)+"%":"—",new RectangleF(1112,193,52,40),19,Color.White,true);
        Field(g,new RectangleF(800,257,375,58),"CARGA",job?Str(data,"cargo.name"):"Sem carga",20);Field(g,new RectangleF(800,323,375,54),"PESO",job?Num(data,"cargo.weight_t").ToString("0.#",Pt)+" t":"—",22);
        var dial=new RectangleF(445,65,310,310);using(var b=new SolidBrush(Color.FromArgb(240,5,19,30)))g.FillEllipse(b,dial);using(var pen=new Pen(Gold,5))g.DrawEllipse(pen,dial);
        var arc=new RectangleF(460,80,280,280);using(var pen=new Pen(Color.FromArgb(55,104,120),8))g.DrawArc(pen,arc,145,250);using(var pen=new Pen(Color.FromArgb(65,210,120),8))g.DrawArc(pen,arc,145,Math.Max(.01f,(float)(Math.Min(160,Math.Max(0,online?speed:0))*250/160)));
        for(int n=0;n<=160;n+=10){double a=(145+n*250.0/160)*Math.PI/180;float cx=600,cy=220;var a1=new PointF(cx+(float)Math.Cos(a)*135,cy+(float)Math.Sin(a)*135);var a2=new PointF(cx+(float)Math.Cos(a)*(n%20==0?120:128),cy+(float)Math.Sin(a)*(n%20==0?120:128));using(var pen=new Pen(Ice,n%20==0?2.3f:1))g.DrawLine(pen,a1,a2);if(n%20==0)Txt(g,n.ToString(),new RectangleF(cx+(float)Math.Cos(a)*106-21,cy+(float)Math.Sin(a)*102-10,42,20),17,Ice,true);}
        if(online){double ang=(145+Math.Min(160,Math.Max(0,speed))*250/160)*Math.PI/180;using(var pen=new Pen(Color.OrangeRed,5))g.DrawLine(pen,600,220,600+(float)Math.Cos(ang)*117,220+(float)Math.Sin(ang)*109);}
        Color sc=online&&limit>0&&speed>limit+1?Color.FromArgb(255,85,80):Color.White;Txt(g,online?N(speed):"—",new RectangleF(493,190,214,87),76,sc,true);Txt(g,"km/h",new RectangleF(535,270,130,24),20,Ice,true);Txt(g,"CRUZEIRO "+(online&&Flag(data,"truck.cruise")?N(Num(data,"truck.cruise_speed_kmh"))+" km/h":online?"OFF":"—"),new RectangleF(443,307,314,31),21,Gold,true);Txt(g,online?"":"GPS DESCONECTADO",new RectangleF(442,342,316,20),14,Color.LightSalmon,true);
    }
    protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);if(e.Button==MouseButtons.Left){drag=e.Location;dragging=true;Capture=true;}}
    protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);if(dragging&&e.Button==MouseButtons.Left)Location=new Point(Left+e.X-drag.X,Top+e.Y-drag.Y);}
    protected override void OnMouseUp(MouseEventArgs e){base.OnMouseUp(e);dragging=false;Capture=false;}
    protected override void OnMouseDoubleClick(MouseEventArgs e){base.OnMouseDoubleClick(e);if(e.X<ClientSize.Width*.13&&e.Y>ClientSize.Height*.18&&e.Y<ClientSize.Height*.53)ChoosePhoto();}
}
