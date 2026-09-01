using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.ServiceProcess;
using System.Threading.Tasks;
using System.Windows.Forms;

internal sealed class Setup : Form
{
    const string Token="__TUNNEL_TOKEN__";
    const string Url="__CLOUDFLARED_URL__";
    const string Digest="__CLOUDFLARED_SHA256__";
    readonly Label status=new Label{Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleCenter,Text="Conectando o GAT Servidor..."};
    [STAThread] static void Main(){Application.EnableVisualStyles();Application.Run(new Setup());}
    Setup(){Text="Conexao da Central GAT";ClientSize=new Size(570,175);StartPosition=FormStartPosition.CenterScreen;Controls.Add(status);Shown+=async(s,e)=>await Install();}
    static string Hash(string path){using(var sha=SHA256.Create())using(var f=File.OpenRead(path))return BitConverter.ToString(sha.ComputeHash(f)).Replace("-","").ToLowerInvariant();}
    static async Task<int> Command(string exe,string args){
        using(var p=Process.Start(new ProcessStartInfo(exe,args){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true})){
            var output=p.StandardOutput.ReadToEndAsync();var errors=p.StandardError.ReadToEndAsync();
            bool exited=await Task.Run(()=>p.WaitForExit(60000));if(!exited){p.Kill();throw new InvalidOperationException("A configuracao demorou demais. Tente novamente.");}
            await output;await errors;return p.ExitCode;
        }
    }
    async Task Install(){
        string partial=null;
        try{
            ServicePointManager.SecurityProtocol=SecurityProtocolType.Tls12;
            using(var client=new HttpClient{Timeout=TimeSpan.FromSeconds(5)}){
                var health=await client.GetStringAsync("http://127.0.0.1:5056/health");
                if(!health.Contains("1.0.39-local"))throw new InvalidOperationException("Inicie a Central do Site no GAT Servidor antes de conectar.");
            }
            if(ServiceController.GetServices().Any(s=>s.ServiceName.Equals("Cloudflared",StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("Ja existe um servico Cloudflared neste PC. Nenhuma configuracao foi substituida; envie esta tela para conferir a conexao existente.");
            string dir=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),"GAT-LOG Server","tunnel");
            Directory.CreateDirectory(dir);string exe=Path.Combine(dir,"cloudflared.exe");partial=exe+".download";
            status.Text="Baixando e verificando o conector oficial...\r\nAguarde alguns minutos.";
            using(var client=new HttpClient{Timeout=TimeSpan.FromMinutes(10)})using(var response=await client.GetAsync(Url,HttpCompletionOption.ResponseHeadersRead)){
                response.EnsureSuccessStatusCode();using(var f=File.Create(partial))await response.Content.CopyToAsync(f);
            }
            if(Hash(partial)!=Digest)throw new InvalidOperationException("O download nao passou na verificacao. Nenhum servico foi instalado.");
            if(File.Exists(exe))File.Delete(exe);File.Move(partial,exe);partial=null;
            status.Text="Instalando a conexao em segundo plano...";
            if(await Command(exe,"service install "+Token)!=0)throw new InvalidOperationException("O Windows nao concluiu a instalacao do conector. Envie esta tela para conferir.");
            using(var service=new ServiceController("Cloudflared")){
                service.Refresh();if(service.Status==ServiceControllerStatus.Stopped)service.Start();
                await Task.Run(()=>service.WaitForStatus(ServiceControllerStatus.Running,TimeSpan.FromSeconds(30)));
            }
            status.Text="Conexao instalada. Aguardando o teste pela internet...";
            bool ready=false;
            using(var client=new HttpClient{Timeout=TimeSpan.FromSeconds(6)}){
                for(int i=0;i<18;i++){
                    await Task.Delay(3000);
                    try{var text=await client.GetStringAsync("https://central-teste.gatlogets2.com.br/health");if(text.Contains("1.0.39-local")){ready=true;break;}}catch(HttpRequestException){}catch(TaskCanceledException){}
                }
            }
            status.Text=ready?"CONEXAO INSTALADA E TESTADA":"CONEXAO INSTALADA - TESTE EXTERNO PENDENTE";
            MessageBox.Show(this,(ready?"A Central GAT respondeu pela internet.":"O conector foi instalado, mas a resposta externa ainda nao foi confirmada.")+"\r\nEnvie uma captura desta tela para concluir a ligacao do dominio.\r\nO dominio principal ainda nao foi alterado.","GAT Central",MessageBoxButtons.OK,ready?MessageBoxIcon.Information:MessageBoxIcon.Warning);
        }catch(Exception error){status.Text="Configuracao nao concluida.";MessageBox.Show(this,error.Message.Replace(Token,"[credencial protegida]"),"GAT Central",MessageBoxButtons.OK,MessageBoxIcon.Warning);}
        finally{if(partial!=null)try{File.Delete(partial);}catch{}}
    }
}
