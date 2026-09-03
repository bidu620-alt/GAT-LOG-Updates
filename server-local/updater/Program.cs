using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Windows.Forms;

internal class Updater : Form
{
    const string PackageUrl="https://raw.githubusercontent.com/bidu620-alt/GAT-LOG-Updates/main/releases/GAT_SERVER_LOCAL_1.0.52.zip";
    const string PackageHash="__PAYLOAD_SHA256__";
    readonly Label status=new Label{Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleCenter,Text="Preparando atualizacao do GAT Servidor..."};
    [STAThread] static void Main(){Application.EnableVisualStyles();Application.Run(new Updater());}
    Updater(){Text="GAT Servidor 1.0.52 - revisao de Pontos GAT";ClientSize=new Size(590,160);StartPosition=FormStartPosition.CenterScreen;Controls.Add(status);Shown+=async(s,e)=>await Install();}
    static string Hash(string path){using(var sha=SHA256.Create())using(var f=File.OpenRead(path))return BitConverter.ToString(sha.ComputeHash(f)).Replace("-","").ToLowerInvariant();}
    async Task Install(){
        string temp=Path.Combine(Path.GetTempPath(),"GAT-local-"+Guid.NewGuid().ToString("N"));
        string target=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),"GAT-LOG Server");
        string exe=Path.Combine(target,"GAT_LOG_SERVER.exe"),central=Path.Combine(target,"central"),previous=null;
        string data=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"GAT-LOG","Central");
        try{
            if(!File.Exists(exe)||!Directory.Exists(central))throw new InvalidOperationException("Nao encontrei a instalacao atual do GAT Servidor/Central.");
            var version=FileVersionInfo.GetVersionInfo(exe).FileVersion;
            if(version!="1.0.39.0"&&version!="1.0.40.0"&&version!="1.0.41.0"&&version!="1.0.42.0"&&version!="1.0.43.0"&&version!="1.0.44.0"&&version!="1.0.45.0"&&version!="1.0.46.0"&&version!="1.0.47.0"&&version!="1.0.48.0"&&version!="1.0.49.0"&&version!="1.0.50.0"&&version!="1.0.51.0"&&version!="1.0.52.0")throw new InvalidOperationException("Esta atualizacao foi preparada para as versoes 1.0.39 a 1.0.52. Versao encontrada: "+version);
            Directory.CreateDirectory(temp);status.Text="Baixando e verificando a atualizacao...";
            ServicePointManager.SecurityProtocol=SecurityProtocolType.Tls12;
            string zip=Path.Combine(temp,"package.zip");
            using(var client=new HttpClient{Timeout=TimeSpan.FromMinutes(10)})using(var response=await client.GetAsync(PackageUrl,HttpCompletionOption.ResponseHeadersRead)){
                response.EnsureSuccessStatusCode();using(var f=File.Create(zip))await response.Content.CopyToAsync(f);
            }
            if(Hash(zip)!=PackageHash)throw new InvalidOperationException("A verificacao de integridade falhou. Nada foi instalado.");
            string stage=Path.Combine(temp,"files");Directory.CreateDirectory(stage);
            using(var archive=ZipFile.OpenRead(zip))foreach(var entry in archive.Entries){
                string name=entry.FullName.Replace('/',Path.DirectorySeparatorChar),dest=Path.GetFullPath(Path.Combine(stage,name));
                if(!dest.StartsWith(stage+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Caminho invalido no pacote.");
                if(string.IsNullOrEmpty(entry.Name)){Directory.CreateDirectory(dest);continue;}
                Directory.CreateDirectory(Path.GetDirectoryName(dest));entry.ExtractToFile(dest);
            }
            if(!File.Exists(Path.Combine(stage,"central","node.exe"))||!File.Exists(Path.Combine(stage,"central","worker.js"))||!File.Exists(Path.Combine(stage,"GAT_LOG_SERVER.exe")))throw new InvalidDataException("Pacote incompleto.");
            status.Text="Parando a Central por alguns segundos...\r\nO banco, contas, historico e viagens abertas serao preservados.";
            foreach(var p in Process.GetProcessesByName("GAT_LOG_SERVER"))using(p){try{if(string.Equals(p.MainModule.FileName,exe,StringComparison.OrdinalIgnoreCase)){p.CloseMainWindow();await Task.Run(()=>p.WaitForExit(5000));}}catch{}}
            StopCentral(data,Path.Combine(central,"node.exe"));await Task.Delay(800);
            previous=Path.Combine(target,"update-backups",DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-"+(version??"desconhecida"));Directory.CreateDirectory(previous);
            File.Copy(exe,Path.Combine(previous,"GAT_LOG_SERVER.exe"),true);CopyDirectory(central,Path.Combine(previous,"central"));
            string db=Path.Combine(data,"central.sqlite");if(File.Exists(db)){Directory.CreateDirectory(Path.Combine(previous,"data"));File.Copy(db,Path.Combine(previous,"data","central.sqlite"),true);}
            status.Text="Instalando Central 1.0.52 e ativando revisao de Pontos GAT...";
            string oldCentral=central+"-old-"+Guid.NewGuid().ToString("N"),incoming=central+"-new-"+Guid.NewGuid().ToString("N");
            CopyDirectory(Path.Combine(stage,"central"),incoming);Directory.Move(central,oldCentral);Directory.Move(incoming,central);
            string replacement=Path.Combine(target,"GAT_LOG_SERVER.replacement");File.Copy(Path.Combine(stage,"GAT_LOG_SERVER.exe"),replacement,true);File.Replace(replacement,exe,null);
            try{Directory.Delete(oldCentral,true);}catch{}
            Process.Start(new ProcessStartInfo(exe,"--central-only"){WorkingDirectory=target,UseShellExecute=true});
            MessageBox.Show(this,"GAT Servidor 1.0.52 instalado.\r\n\r\nProprietario, Admin e Moderador agora podem revisar no site entregas que ficaram com 0/100 Pontos GAT por erro de validacao. A Central calcula uma PONTUACAO SUGERIDA usando somente as penalidades salvas da propria viagem.\r\n\r\nO painel permite CONFIRMAR a pontuacao sugerida ou MANTER 0. Cada decisao fica registrada na auditoria com motorista, entrega, usuario que revisou, data, motivo automatico, pontuacao anterior, sugerida e aprovada. A mesma entrega nao pode ser revisada duas vezes.\r\n\r\nA revisao de Pontos GAT nao altera XP, quilometragem nem o historico da entrega. As viagens persistentes e a retomada entre jogo/app/PC da 1.0.51 continuam ativas.\r\n\r\nBanco, historico, pontos, entregas, contas, senhas e PCs vinculados foram preservados.","Atualizacao concluida",MessageBoxButtons.OK,MessageBoxIcon.Information);Close();
        }catch(Exception ex){
            try{if(previous!=null){StopCentral(data,Path.Combine(central,"node.exe"));if(Directory.Exists(Path.Combine(previous,"central"))){if(Directory.Exists(central))Directory.Delete(central,true);CopyDirectory(Path.Combine(previous,"central"),central);}if(File.Exists(Path.Combine(previous,"GAT_LOG_SERVER.exe")))File.Copy(Path.Combine(previous,"GAT_LOG_SERVER.exe"),exe,true);}}catch{}
            status.Text="Atualizacao nao concluida.";MessageBox.Show(this,ex.Message,"GAT Servidor",MessageBoxButtons.OK,MessageBoxIcon.Warning);
        }finally{try{Directory.Delete(temp,true);}catch{}}
    }
    static void StopCentral(string data,string node){try{string status=Path.Combine(data,"status.json");if(!File.Exists(status))return;string txt=File.ReadAllText(status);var m=System.Text.RegularExpressions.Regex.Match(txt,"\\\"pid\\\"\\s*:\\s*(\\d+)");if(!m.Success)return;using(var p=Process.GetProcessById(int.Parse(m.Groups[1].Value))){if(string.Equals(p.MainModule.FileName,node,StringComparison.OrdinalIgnoreCase)){p.Kill();p.WaitForExit(5000);}}}catch{}}
    static void CopyDirectory(string source,string target){Directory.CreateDirectory(target);foreach(var f in Directory.GetFiles(source))File.Copy(f,Path.Combine(target,Path.GetFileName(f)),true);foreach(var d in Directory.GetDirectories(source))CopyDirectory(d,Path.Combine(target,Path.GetFileName(d)));}
}
