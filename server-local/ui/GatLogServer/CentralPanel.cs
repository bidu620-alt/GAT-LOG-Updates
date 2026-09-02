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
    private readonly Timer timer = new Timer { Interval = 3000 };
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
                    if(auto.Checked){if(!File.Exists(Path.Combine(Data,"central.sqlite")))throw new InvalidOperationException("O banco local da Central nao foi encontrado.");key.SetValue("GATCentralLocal",Quote(Application.ExecutablePath)+" --central-only");}
                    else key.DeleteValue("GATCentralLocal",false);
                }
            }catch(Exception ex){if(auto.Checked)auto.Checked=false;MessageBox.Show(this,ex.Message,"GAT Central");}
        };Controls.Add(auto);
        AddLabel("O dominio api.gatlogets2.com.br usa esta Central local pelo Cloudflare Tunnel.\r\nFechar o painel nao para a Central. Para interromper, use PARAR CENTRAL.\r\nBackups automaticos a cada 6 horas: ultimas 14 copias.",20,385,10);
        timer.Tick+=async(s,e)=>await RefreshAsync();timer.Start();
        Disposed+=(s,e)=>{timer.Dispose();http.Dispose();};
        _=RefreshAsync();
    }
    private void AddLabel(string text,int x,int y,float size){Controls.Add(new Label{Text=text,Location=new Point(x,y),AutoSize=true,Font=new Font("Segoe UI",size)});}
    private void ButtonAt(string text,int x,int y,Func<Task> action){
        var button=new Button {Text=text,Location=new Point(x,y),Size=new Size(220,44),BackColor=Color.FromArgb(31,111,211),FlatStyle=FlatStyle.Flat};
        button.Click+=async(s,e)=>{button.Enabled=false;try{await action();}catch(Exception ex){MessageBox.Show(this,ex.Message,"GAT Central",MessageBoxButtons.OK,MessageBoxIcon.Warning);}finally{button.Enabled=true;await RefreshAsync();}};Controls.Add(button);
    }
    private static bool IsLocalCentral(JObject json){
        var version=(string)json["agent_version"]??"";
        return (bool?)json["ok"]==true
            && string.Equals((string)json["service"],"GAT Central Local",StringComparison.OrdinalIgnoreCase)
            && version.EndsWith("-local",StringComparison.OrdinalIgnoreCase);
    }
    private async Task<JObject> ProbeAsync(){
        using(var response=await http.GetAsync("http://127.0.0.1:5056/health")){
            var content=await response.Content.ReadAsStringAsync();
            if(!response.IsSuccessStatusCode)throw new InvalidOperationException("HTTP "+(int)response.StatusCode);
            return JObject.Parse(content);
        }
    }
    private void ShowStopped(){
        var hasDb=File.Exists(Path.Combine(Data,"central.sqlite"));
        state.Text=hasDb?"CENTRAL LOCAL PARADA":"BANCO LOCAL NAO ENCONTRADO";state.ForeColor=Color.Gold;
        detail.Text=hasDb
            ?"A Central esta parada. O banco permanece salvo no PC.\r\nUse INICIAR CENTRAL para restaurar login, telemetria, ranking e o site.\r\nNao e necessario importar o banco novamente."
            :"O arquivo central.sqlite nao foi encontrado. Nao importe nem substitua um banco sem antes confirmar o backup correto.";
    }
    private async Task RefreshAsync(){
        if(checking||IsDisposed)return;checking=true;
        try{
            var json=await ProbeAsync();
            if(!IsLocalCentral(json)){
                state.Text="PORTA 5056 EM USO";state.ForeColor=Color.Orange;
                detail.Text="Existe outro servico respondendo na porta 5056, mas ele nao foi identificado como GAT Central Local.\r\nNenhuma nova Central sera iniciada ate essa porta ser liberada.";
                return;
            }
            var version=(string)json["agent_version"]??"local";
            state.Text="CENTRAL LOCAL ATIVA";state.ForeColor=Color.LightGreen;
            detail.Text="Banco, login, telemetria e ranking no seu PC. Versao: "+version+".\r\nEndereco local: http://127.0.0.1:5056\r\nO Cloudflare Tunnel apenas encaminha api.gatlogets2.com.br para esta Central.";
        }catch(HttpRequestException){ShowStopped();}
        catch(TaskCanceledException){ShowStopped();}
        catch{
            state.Text="STATUS DA CENTRAL INDETERMINADO";state.ForeColor=Color.Gold;
            detail.Text="Nao foi possivel validar a resposta da porta 5056. O banco nao foi alterado.\r\nTente atualizar o status ou reiniciar somente a Central.";
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
        if(File.Exists(Path.Combine(Data,"central.sqlite")))throw new InvalidOperationException("Ja existe um banco local. A importacao inicial fica bloqueada para proteger contas, ranking e historico atuais.");
        using(var dialog=new OpenFileDialog{Title="Exportacao completa do D1 (.sql)",Filter="Banco SQL|*.sql"}){
            if(dialog.ShowDialog(this)!=DialogResult.OK)return;
            state.Text="IMPORTANDO E CONFERINDO...";
            var result=await Command("import "+Quote(dialog.FileName));
            MessageBox.Show(this,"Banco importado e conferido.\r\n"+result,"GAT Central");
        }
    }
    internal static void StartBackground(){
        if(!File.Exists(Path.Combine(Data,"central.sqlite")))throw new InvalidOperationException("O banco local da Central nao foi encontrado.");
        Process.Start(new ProcessStartInfo(Node,Quote(Host)){WorkingDirectory=Runtime,UseShellExecute=false,CreateNoWindow=true});
    }
    private async Task StartAsync(){
        try{
            var json=await ProbeAsync();
            if(IsLocalCentral(json))return;
            throw new InvalidOperationException("A porta 5056 esta ocupada por outro servico.");
        }
        catch(HttpRequestException){}
        catch(TaskCanceledException){}
        StartBackground();
        for(var i=0;i<10;i++){
            await Task.Delay(300);
            try{if(IsLocalCentral(await ProbeAsync()))return;}catch(HttpRequestException){}catch(TaskCanceledException){}
        }
        throw new InvalidOperationException("A Central nao respondeu na porta 5056 apos a tentativa de inicio.");
    }
    private async Task StopAsync(){
        if(MessageBox.Show(this,"Parar o recebimento das viagens e o ranking no PC? O comboio ETS2 continuara funcionando.","GAT Central",MessageBoxButtons.YesNo)!=DialogResult.Yes)return;
        var path=Path.Combine(Data,"status.json");
        if(File.Exists(path)){
            var status=JObject.Parse(File.ReadAllText(path));
            try{using(var p=Process.GetProcessById((int)status["pid"])){
                if(!string.Equals(p.MainModule.FileName,Node,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Processo diferente da central; nenhuma acao realizada.");
                p.Kill();
                await Task.Run(()=>p.WaitForExit(3000));
            }}catch(ArgumentException){}
        }
        await Task.Delay(300);
    }
    private async Task BackupAsync(){var result=await Command("backup");MessageBox.Show(this,"Backup salvo em:\r\n"+result,"GAT Central");}
}
