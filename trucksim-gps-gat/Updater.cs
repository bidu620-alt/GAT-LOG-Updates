using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Windows.Forms;

internal static class Program
{
    const string Url = "https://raw.githubusercontent.com/bidu620-alt/GAT-LOG-Updates/main/releases/TruckSimGPS_Server_GAT_DANOS_1.0.exe";
    const string ExpectedSha = "__SHA__";

    [STAThread]
    static void Main()
    {
        Application.EnableVisualStyles();
        if (!IsAdministrator())
        {
            try
            {
                ProcessStartInfo p = new ProcessStartInfo(Application.ExecutablePath);
                p.UseShellExecute = true;
                p.Verb = "runas";
                Process.Start(p);
            }
            catch
            {
                MessageBox.Show("A atualizacao precisa de permissao de Administrador.", "GAT - TruckSim GPS", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            return;
        }

        if (Process.GetProcessesByName("TruckSimGPS_Server").Length > 0 || Process.GetProcessesByName("eurotrucks2").Length > 0)
        {
            MessageBox.Show("Feche o ETS2 e o TruckSim GPS antes de atualizar.", "GAT - TruckSim GPS", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        string target = FindInstall();
        if (target == null)
        {
            MessageBox.Show("Nao encontrei o TruckSim GPS instalado. Instale a versao oficial primeiro.", "GAT - TruckSim GPS", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        string temp = Path.Combine(Path.GetTempPath(), "TruckSimGPS_Server_GAT_DANOS_1.0.exe");
        try
        {
            ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;
            using (WebClient wc = new WebClient()) wc.DownloadFile(Url, temp);
            if (!String.Equals(Sha256(temp), ExpectedSha, StringComparison.OrdinalIgnoreCase))
                throw new Exception("Falha na verificacao de integridade do arquivo baixado.");

            string backup = target + ".backup_GAT_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
            File.Copy(target, backup, true);
            File.Copy(temp, target, true);
            MessageBox.Show("TruckSim GPS atualizado com sucesso para suporte aos danos reais do GAT.\n\nBackup criado em:\n" + backup, "GAT - TruckSim GPS", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show("Nao foi possivel atualizar o TruckSim GPS.\n\n" + ex.Message, "GAT - TruckSim GPS", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            try { if (File.Exists(temp)) File.Delete(temp); } catch { }
        }
    }

    static bool IsAdministrator()
    {
        WindowsIdentity id = WindowsIdentity.GetCurrent();
        WindowsPrincipal p = new WindowsPrincipal(id);
        return p.IsInRole(WindowsBuiltInRole.Administrator);
    }

    static string FindInstall()
    {
        string[] roots = new string[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
        };
        foreach (string root in roots)
        {
            if (String.IsNullOrWhiteSpace(root)) continue;
            string p = Path.Combine(root, "TruckSim GPS Telemetry Server", "TruckSimGPS_Server.exe");
            if (File.Exists(p)) return p;
        }
        return null;
    }

    static string Sha256(string file)
    {
        using (FileStream fs = File.OpenRead(file))
        using (SHA256 sha = SHA256.Create())
        {
            byte[] hash = sha.ComputeHash(fs);
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }
    }
}
