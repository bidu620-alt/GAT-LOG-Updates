using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Threading;
using System.Windows.Forms;

internal static class Program
{
    private const string Version = "1.0.28";
    private const string RuntimeUrl = "https://raw.githubusercontent.com/bidu620-alt/GAT-LOG-Updates/main/releases/GAT_TELEMETRIA_DOTNET_1.0.28_RUNTIME.zip";
    private const string ExpectedRuntimeSha = "__RUNTIME_SHA__";

    [STAThread]
    private static void Main(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        try
        {
            string target = ReadTargetArgument(args);
            if (String.IsNullOrWhiteSpace(target)) target = FindKnownTarget();

            if (String.IsNullOrWhiteSpace(target) || !File.Exists(target))
            {
                MessageBox.Show(
                    "Nao consegui localizar a instalacao atual do GAT Telemetria.\n\n" +
                    "Abra o GAT Telemetria instalado e use VERIFICAR ATUALIZACAO.\n\n" +
                    "Instalacao recomendada: C:\\TruckSimGPS_GAT\\GAT_TELEMETRIA.exe",
                    "GAT Telemetria " + Version,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            target = Path.GetFullPath(target);
            Install(target);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "Nao foi possivel concluir a atualizacao do GAT Telemetria.\n\n" + ex.Message,
                "GAT Telemetria " + Version,
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private static string ReadTargetArgument(string[] args)
    {
        if (args == null) return null;
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (String.Equals(args[i], "--target", StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        }
        return null;
    }

    private static string FindKnownTarget()
    {
        string[] known = new string[]
        {
            @"C:\TruckSimGPS_GAT\GAT_TELEMETRIA.exe",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "GAT Telemetria", "GAT_TELEMETRIA.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GAT Telemetria", "GAT_TELEMETRIA.exe")
        };

        foreach (string p in known)
        {
            if (!String.IsNullOrWhiteSpace(p) && File.Exists(p)) return p;
        }
        return null;
    }

    private static void Install(string targetExe)
    {
        string installDir = Path.GetDirectoryName(targetExe);
        if (String.IsNullOrWhiteSpace(installDir))
            throw new Exception("Pasta de instalacao invalida.");

        string tempRoot = Path.Combine(Path.GetTempPath(), "GAT_TELEMETRIA_UPDATE_" + Guid.NewGuid().ToString("N"));
        string zip = Path.Combine(tempRoot, "runtime.zip");
        string stage = Path.Combine(tempRoot, "stage");

        Directory.CreateDirectory(tempRoot);
        Directory.CreateDirectory(stage);

        try
        {
            ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;

            using (WebClient wc = new WebClient())
                wc.DownloadFile(RuntimeUrl, zip);

            string actualSha = Sha256(zip);
            if (!String.Equals(actualSha, ExpectedRuntimeSha, StringComparison.OrdinalIgnoreCase))
                throw new Exception("Falha na verificacao de integridade do pacote.");

            ZipFile.ExtractToDirectory(zip, stage);

            string stagedExe = Path.Combine(stage, "GAT_TELEMETRIA.exe");
            if (!File.Exists(stagedExe))
                throw new Exception("O pacote baixado nao contem GAT_TELEMETRIA.exe.");

            string backupRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "GAT",
                "Backups",
                "GAT_Telemetria_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));

            Directory.CreateDirectory(backupRoot);

            foreach (string source in Directory.GetFiles(stage, "*", SearchOption.AllDirectories))
            {
                string rel = source.Substring(stage.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string dest = Path.Combine(installDir, rel);

                if (File.Exists(dest))
                {
                    string back = Path.Combine(backupRoot, rel);
                    string backDir = Path.GetDirectoryName(back);
                    if (!String.IsNullOrWhiteSpace(backDir)) Directory.CreateDirectory(backDir);
                    File.Copy(dest, back, true);
                }
            }

            foreach (string source in Directory.GetFiles(stage, "*", SearchOption.AllDirectories))
            {
                string rel = source.Substring(stage.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string dest = Path.Combine(installDir, rel);
                string destDir = Path.GetDirectoryName(dest);
                if (!String.IsNullOrWhiteSpace(destDir)) Directory.CreateDirectory(destDir);
                CopyWithRetry(source, dest);
            }

            MessageBox.Show(
                "GAT Telemetria atualizado com sucesso para a versao 1.0.28.",
                "GAT Telemetria 1.0.28",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);

            ProcessStartInfo psi = new ProcessStartInfo(targetExe);
            psi.WorkingDirectory = installDir;
            psi.UseShellExecute = true;
            Process.Start(psi);
        }
        finally
        {
            try
            {
                if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, true);
            }
            catch { }
        }
    }

    private static void CopyWithRetry(string source, string dest)
    {
        Exception last = null;

        for (int i = 0; i < 40; i++)
        {
            try
            {
                File.Copy(source, dest, true);
                return;
            }
            catch (IOException ex)
            {
                last = ex;
                Thread.Sleep(500);
            }
            catch (UnauthorizedAccessException ex)
            {
                last = ex;
                Thread.Sleep(500);
            }
        }

        throw new Exception(
            "Nao foi possivel substituir " + Path.GetFileName(dest) +
            ". Feche o GAT Telemetria e tente novamente. " +
            (last == null ? "" : last.Message));
    }

    private static string Sha256(string file)
    {
        using (FileStream fs = File.OpenRead(file))
        using (SHA256 sha = SHA256.Create())
        {
            byte[] hash = sha.ComputeHash(fs);
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }
    }
}
