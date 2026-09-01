using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Windows.Forms;

internal class Updater : Form
{
    const string PackageUrl="https://raw.githubusercontent.com/bidu620-alt/GAT-LOG-Updates/main/releases/GAT_SERVER_LOCAL_1.0.39.zip";
    const string PackageHash="__PAYLOAD_SHA256__";
    readonly Label status=new Label{Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleCenter,Text="Preparando atualizacao do GAT Servidor..."};
    [STAThread] static void Main(){Application.EnableVisualStyles();Application.Run(new Updater());}
    Updater(){Text="GAT Servidor 1.0.39 - Central local";ClientSize=new Size(560,150);StartPosition=FormStartPosition.CenterScreen;Controls.Add(status);Shown+=async(s,e)=>await Install();}
    static string Hash(string path){using(var sha=SHA256.Create())using(var f=File.OpenRead(path))return BitConverter.ToString(sha.ComputeHash(f)).Replace("-","").ToLowerInvariant();}
    async Task Install(){
        string temp=Path.Combine(Path.GetTempPath(),"GAT-local-"+Guid.NewGuid().ToString("N"));
        string target=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),"GAT-LOG Server");
        string exe=Path.Combine(target,"GAT_LOG_SERVER.exe"),previous=null;
        bool replaced=false,centralInstalled=false;
        try{
            if(!File.Exists(exe))throw new InvalidOperationException("Nao encontrei o GAT Servidor em C:\\ProgramData\\GAT-LOG Server. Nenhum arquivo foi alterado.");
            var version=FileVersionInfo.GetVersionInfo(exe).FileVersion;
            if(version!="1.0.38.0")throw new InvalidOperationException("Esta atualizacao foi preparada para a versao 1.0.38. Versao encontrada: "+version);
            if(Directory.Exists(Path.Combine(target,"central")))throw new InvalidOperationException("Ja existe uma central nesta instalacao. A atualizacao nao substituira seus arquivos.");
            Directory.CreateDirectory(temp);status.Text="Baixando e verificando a atualizacao...\r\nPode levar alguns minutos.";
            ServicePointManager.SecurityProtocol=SecurityProtocolType.Tls12;
            string zip=Path.Combine(temp,"package.zip");
            using(var client=new HttpClient{Timeout=TimeSpan.FromMinutes(10)})using(var response=await client.GetAsync(PackageUrl,HttpCompletionOption.ResponseHeadersRead)){
                response.EnsureSuccessStatusCode();using(var f=File.Create(zip))await response.Content.CopyToAsync(f);
            }
            if(Hash(zip)!=PackageHash)throw new InvalidOperationException("A verificacao de integridade falhou. Nada foi instalado.");
            string stage=Path.Combine(temp,"files");Directory.CreateDirectory(stage);
            using(var archive=ZipFile.OpenRead(zip))foreach(var entry in archive.Entries){
                string name=entry.FullName.Replace('/',Path.DirectorySeparatorChar);
                string dest=Path.GetFullPath(Path.Combine(stage,name));
                if(!dest.StartsWith(stage+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Caminho invalido no pacote.");
                if(string.IsNullOrEmpty(entry.Name)){Directory.CreateDirectory(dest);continue;}
                Directory.CreateDirectory(Path.GetDirectoryName(dest));entry.ExtractToFile(dest);
            }
            if(!File.Exists(Path.Combine(stage,"central","node.exe"))||!File.Exists(Path.Combine(stage,"GAT_LOG_SERVER.exe")))throw new InvalidDataException("Pacote incompleto.");
            status.Text="Instalando e preservando a versao anterior...";
            foreach(var p in Process.GetProcessesByName("GAT_LOG_SERVER"))using(p){
                if(!string.Equals(p.MainModule.FileName,exe,StringComparison.OrdinalIgnoreCase))continue;
                p.CloseMainWindow();if(!await Task.Run(()=>p.WaitForExit(6000)))throw new InvalidOperationException("Feche o painel do GAT Servidor e execute novamente. O comboio pode continuar ligado.");
            }
            previous=Path.Combine(target,"update-backups",DateTime.Now.ToString("yyyyMMdd-HHmmss"));Directory.CreateDirectory(previous);
            File.Copy(exe,Path.Combine(previous,"GAT_LOG_SERVER.exe"));
            string incoming=Path.Combine(target,"central-installing-"+Guid.NewGuid().ToString("N"));if(Directory.Exists(incoming))throw new InvalidOperationException("Existe uma instalacao incompleta. Seus arquivos foram preservados.");
            CopyDirectory(Path.Combine(stage,"central"),incoming);
            Directory.Move(incoming,Path.Combine(target,"central"));centralInstalled=true;
            string replacement=Path.Combine(target,"GAT_LOG_SERVER.replacement");
            File.Copy(Path.Combine(stage,"GAT_LOG_SERVER.exe"),replacement,true);File.Replace(replacement,exe,null);replaced=true;
            MessageBox.Show(this,"GAT Servidor atualizado para 1.0.39.\r\nAbra CENTRAL DO SITE para continuar.\r\nO dominio ainda permanece na Cloudflare.","Atualizacao concluida",MessageBoxButtons.OK,MessageBoxIcon.Information);
            Process.Start(new ProcessStartInfo(exe){WorkingDirectory=target,UseShellExecute=true});Close();
        }catch(Exception ex){
            if(replaced&&previous!=null)File.Copy(Path.Combine(previous,"GAT_LOG_SERVER.exe"),exe,true);
            if(centralInstalled)Directory.Delete(Path.Combine(target,"central"),true);
            status.Text="Atualizacao nao concluida.";MessageBox.Show(this,ex.Message,"GAT Servidor",MessageBoxButtons.OK,MessageBoxIcon.Warning);
        }finally{try{Directory.Delete(temp,true);}catch{}}
    }
    static void CopyDirectory(string source,string target){Directory.CreateDirectory(target);foreach(var f in Directory.GetFiles(source))File.Copy(f,Path.Combine(target,Path.GetFileName(f)));foreach(var d in Directory.GetDirectories(source))CopyDirectory(d,Path.Combine(target,Path.GetFileName(d)));}
}
