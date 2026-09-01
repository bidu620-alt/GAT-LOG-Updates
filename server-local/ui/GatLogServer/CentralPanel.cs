using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;
using Newtonsoft.Json.Linq;

namespace GatLogServer;

internal sealed class CentralPanel : UserControl
{
    private readonly Label state = new Label();
    private readonly Label detail = new Label();
    private readonly Timer timer = new Timer { Interval = 5000 };
    private readonly HttpClient http = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
    private bool checking;
    private static string Runtime => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "central");
    private static string Node => Path.Combine(Runtime, "node.exe");
    private static string Host => Path.Combine(Runtime, "host.mjs");
    private static string Data => Path.Combine(AuthService.DataDir, "Central");
    private static string Quote(string s) => "\"" + s.Replace("\"", "") + "\"";

    internal CentralPanel()
    {
        Dock=DockStyle.Fill; AutoScroll=true; BackColor=Color.FromArgb(3,29,44); ForeColor=Color.White;
        AddLabel("CENTRAL DO SITE NO SEU PC",20,18,22);
        state.SetBounds(20,65,820,35);state.Font=new Font("Segoe UI",12,FontStyle.Bold);Controls.Add(state);
        detail.SetBounds(20,105,830,95);Controls.Add(detail);
        ButtonAt("IMPORTAR BANCO .SQL",20,205,ImportAsync);
        ButtonAt("INICIAR CENTRAL",260,205,StartAsync);
        ButtonAt("PARAR CENTRAL",500,205,StopAsync);
        ButtonAt("CRIAR BACKUP",20,265,BackupAsync);
        ButtonAt("ABRIR BACKUPS",260,265,()=>{Directory.CreateDirectory(Path.Combine(Data,"backups"));Process.Start("explorer.exe",Quote(Path.Combine(Data,"backups")));return Task.CompletedTask;});
        ButtonAt("GUIA DE CONEXAO",500,265,()=>{Process.Start(new ProcessStartInfo(Path.Combine(Runtime,"LEIA-ME.txt")){UseShellExecute=true});return Task.CompletedTask;});
        var auto=new CheckBox {Text="Iniciar a central quando eu entrar no Windows",AutoSize=true,Location=new Point(20,335)};
        using(var key=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run")) auto.Checked=key?.GetValue("GATCentralLocal")!=null;
        auto.CheckedChanged+=(s,e)=>{
            try{
                using(var key=Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run")){
                    if(auto.Checked){if(!File.Exists(Path.Combine(Data,"central.sqlite")))throw new InvalidOperationException("Importe e teste o banco antes de ativar o inicio automatico.");key.SetValue("GATCentralLocal",Quote(Application.ExecutablePath)+" --central-only");}
                    else key.DeleteValue("GATCentralLocal",false);
                }
            }catch(Exception ex){if(auto.Checked)auto.Checked=false;MessageBox.Show(this,ex.Message,"GAT Central");}
        };Controls.Add(auto);
        AddLabel("O site sera conectado depois da importacao e do teste local.\r\nA central continua funcionando ao fechar o painel. Para interromper, use PARAR CENTRAL.\r\nBackups automaticos a cada 6 horas: ultimas 14 copias.",20,385,10);
        timer.Tick+=async(s,e)=>await RefreshAsync();timer.Start();
        Disposed+=(s,e)=>{timer.Dispose();http.Dispose();};
        _=RefreshAsync();
    }
    private void AddLabel(string text,int x,int y,float size){Controls.Add(new Label{Text=text,Location=new Point(x,y),AutoSize=true,Font=new Font("Segoe UI",size)});}
    private void ButtonAt(string text,int x,int y,Func<Task> action){
        var button=new Button {Text=text,Location=new Point(x,y),Size=new Size(220,44),BackColor=Color.FromArgb(31,111,211),FlatStyle=FlatStyle.Flat};
        button.Click+=async(s,e)=>{button.Enabled=false;try{await action();}catch(Exception ex){MessageBox.Show(this,ex.Message,"GAT Central",MessageBoxButtons.OK,MessageBoxIcon.Warning);}finally{button.Enabled=true;await RefreshAsync();}};Controls.Add(button);
    }
    private async Task RefreshAsync(){
        if(checking||IsDisposed)return;checking=true;
        try{
            string content=await http.GetStringAsync("http://127.0.0.1:5056/health");
            var json=JObject.Parse(content);
            if((string)json["agent_version"]!="1.0.39-local")throw new InvalidOperationException();
            state.Text="CENTRAL LOCAL ATIVA";state.ForeColor=Color.LightGreen;
            detail.Text="Banco e ranking no PC. As regras de versao e dos sete danos estao ativas.\r\nEndereco local: http://127.0.0.1:5056\r\nEste status nao confirma que o dominio publico ja foi conectado.";
        }catch{
            state.Text=File.Exists(Path.Combine(Data,"central.sqlite"))?"CENTRAL LOCAL PARADA":"AGUARDANDO IMPORTACAO DO BANCO";state.ForeColor=Color.Gold;
            detail.Text="Importe a exportacao completa e atual do D1 para preservar contas, senhas e historico.\r\nA instalacao nao muda o dominio nem substitui o banco da Cloudflare.";
        }finally{checking=false;}
    }
    private async Task<string> Command(string args){
        if(!File.Exists(Node))throw new FileNotFoundException("Atualizacao incompleta: runtime da central ausente.");
        var info=new ProcessStartInfo(Node,Quote(Host)+" "+args){WorkingDirectory=Runtime,UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
        using(var p=Process.Start(info)){
            var stdout=p.StandardOutput.ReadToEndAsync();var stderr=p.StandardError.ReadToEndAsync();
            await Task.Run(()=>p.WaitForExit());var output=await stdout;var error=await stderr;
            if(p.ExitCode!=0)throw new InvalidOperationException(error+output);return output;
        }
    }
    private async Task ImportAsync(){
        using(var dialog=new OpenFileDialog{Title="Exportacao completa do D1 (.sql)",Filter="Banco SQL|*.sql"}){
            if(dialog.ShowDialog(this)!=DialogResult.OK)return;
            state.Text="IMPORTANDO E CONFERINDO...";
            var result=await Command("import "+Quote(dialog.FileName));
            MessageBox.Show(this,"Banco importado e conferido.\r\n"+result,"GAT Central");
        }
    }
    internal static void StartBackground(){
        if(!File.Exists(Path.Combine(Data,"central.sqlite")))throw new InvalidOperationException("Importe o banco antes de iniciar.");
        Process.Start(new ProcessStartInfo(Node,Quote(Host)){WorkingDirectory=Runtime,UseShellExecute=false,CreateNoWindow=true});
    }
    private async Task StartAsync(){
        try{var json=JObject.Parse(await http.GetStringAsync("http://127.0.0.1:5056/health"));if((string)json["agent_version"]=="1.0.39-local")return;throw new InvalidOperationException("A porta 5056 esta ocupada por outro servico.");}
        catch(HttpRequestException){}catch(TaskCanceledException){}
        StartBackground();await Task.Delay(1500);
    }
    private Task StopAsync(){
        if(MessageBox.Show(this,"Parar o recebimento das viagens e o ranking no PC? O comboio ETS2 continuara funcionando.","GAT Central",MessageBoxButtons.YesNo)!=DialogResult.Yes)return Task.CompletedTask;
        var path=Path.Combine(Data,"status.json");
        if(File.Exists(path)){
            var status=JObject.Parse(File.ReadAllText(path));
            try{using(var p=Process.GetProcessById((int)status["pid"])){
                if(!string.Equals(p.MainModule.FileName,Node,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Processo diferente da central; nenhuma acao realizada.");
                p.Kill();
            }}catch(ArgumentException){}
        }
        return Task.CompletedTask;
    }
    private async Task BackupAsync(){var result=await Command("backup");MessageBox.Show(this,"Backup salvo em:\r\n"+result,"GAT Central");}
}
