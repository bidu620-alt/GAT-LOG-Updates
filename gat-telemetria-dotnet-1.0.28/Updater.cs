using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

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
            if (String.IsNullOrWhiteSpace(target)) target = FindTargetExe();
            if (String.IsNullOrWhiteSpace(target) || !File.Exists(target))
            {
                MessageBox.Show("Nao consegui localizar a instalacao atual do GAT Telemetria.\n\nAbra a versao antiga e use o botao VERIFICAR ATUALIZACAO por dentro do proprio GAT Telemetria.", "GAT Telemetria " + Version, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            target = Path.GetFullPath(target);
            if (NeedsAdministrator(target) && !IsAdministrator()) { Elevate(target); return; }
            WaitForOldAppToClose(target);
            Install(target);
        }
        catch (Exception ex)
        {
            MessageBox.Show("Nao foi possivel concluir a atualizacao do GAT Telemetria.\n\n" + ex.Message, "GAT Telemetria " + Version, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static string ReadTargetArgument(string[] args)
    {
        if (args == null) return null;
        for (int i = 0; i < args.Length - 1; i++) if (String.Equals(args[i], "--target", StringComparison.OrdinalIgnoreCase)) return args[i + 1];
        return null;
    }

    private static string FindTargetExe()
    {
        foreach (Process p in Process.GetProcessesByName("GAT_TELEMETRIA"))
        {
            try { string path = p.MainModule.FileName; if (!String.IsNullOrWhiteSpace(path) && File.Exists(path)) return path; }
            catch { }
            finally { try { p.Dispose(); } catch { } }
        }
        string fromRegistry = FindInRegistry();
        if (!String.IsNullOrWhiteSpace(fromRegistry)) return fromRegistry;
        string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        string pfx86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        string[] known = new string[] {
            Path.Combine(local, "Programs", "GAT Telemetria", "GAT_TELEMETRIA.exe"),
            Path.Combine(local, "GAT Telemetria", "GAT_TELEMETRIA.exe"),
            Path.Combine(local, "GAT", "GAT Telemetria", "GAT_TELEMETRIA.exe"),
            Path.Combine(roaming, "GAT Telemetria", "GAT_TELEMETRIA.exe"),
            Path.Combine(pf, "GAT Telemetria", "GAT_TELEMETRIA.exe"),
            Path.Combine(pfx86, "GAT Telemetria", "GAT_TELEMETRIA.exe")
        };
        foreach (string p in known) if (!String.IsNullOrWhiteSpace(p) && File.Exists(p)) return p;
        string found = SearchRoot(Path.Combine(local, "Programs"), 4);
        if (!String.IsNullOrWhiteSpace(found)) return found;
        found = SearchRoot(local, 3);
        if (!String.IsNullOrWhiteSpace(found)) return found;
        return SearchRoot(desktop, 3);
    }

    private static string FindInRegistry()
    {
        try
        {
            string appPath = ReadAppPath(RegistryHive.CurrentUser, RegistryView.Default);
            if (!String.IsNullOrWhiteSpace(appPath)) return appPath;
            appPath = ReadAppPath(RegistryHive.LocalMachine, RegistryView.Registry64);
            if (!String.IsNullOrWhiteSpace(appPath)) return appPath;
            appPath = ReadAppPath(RegistryHive.LocalMachine, RegistryView.Registry32);
            if (!String.IsNullOrWhiteSpace(appPath)) return appPath;
        }
        catch { }
        RegistryHive[] hives = new RegistryHive[] { RegistryHive.CurrentUser, RegistryHive.LocalMachine };
        RegistryView[] views = new RegistryView[] { RegistryView.Default, RegistryView.Registry64, RegistryView.Registry32 };
        foreach (RegistryHive hive in hives)
        foreach (RegistryView view in views)
        {
            try
            {
                using (RegistryKey baseKey = RegistryKey.OpenBaseKey(hive, view))
                using (RegistryKey uninstall = baseKey.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall"))
                {
                    if (uninstall == null) continue;
                    foreach (string name in uninstall.GetSubKeyNames())
                    using (RegistryKey key = uninstall.OpenSubKey(name))
                    {
                        if (key == null) continue;
                        string display = Convert.ToString(key.GetValue("DisplayName"));
                        if (display.IndexOf("GAT Telemetria", StringComparison.OrdinalIgnoreCase) < 0) continue;
                        string loc = Convert.ToString(key.GetValue("InstallLocation"));
                        if (!String.IsNullOrWhiteSpace(loc)) { string p = Path.Combine(loc.Trim('"'), "GAT_TELEMETRIA.exe"); if (File.Exists(p)) return p; }
                        string iconPath = CleanExecutablePath(Convert.ToString(key.GetValue("DisplayIcon")));
                        if (!String.IsNullOrWhiteSpace(iconPath) && File.Exists(iconPath)) return iconPath;
                    }
                }
            }
            catch { }
        }
        return null;
    }

    private static string ReadAppPath(RegistryHive hive, RegistryView view)
    {
        try
        {
            using (RegistryKey baseKey = RegistryKey.OpenBaseKey(hive, view))
            using (RegistryKey key = baseKey.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\App Paths\GAT_TELEMETRIA.exe"))
            {
                if (key == null) return null;
                string p = CleanExecutablePath(Convert.ToString(key.GetValue(null)));
                return File.Exists(p) ? p : null;
            }
        }
        catch { return null; }
    }

    private static string CleanExecutablePath(string value)
    {
        if (String.IsNullOrWhiteSpace(value)) return null;
        string s = value.Trim(); int comma = s.IndexOf(','); if (comma > 0) s = s.Substring(0, comma); return s.Trim().Trim('"');
    }

    private static string SearchRoot(string root, int depth)
    {
        if (depth < 0 || String.IsNullOrWhiteSpace(root) || !Directory.Exists(root)) return null;
        try
        {
            string direct = Path.Combine(root, "GAT_TELEMETRIA.exe"); if (File.Exists(direct)) return direct;
            if (depth == 0) return null;
            foreach (string dir in Directory.GetDirectories(root))
            {
                string name = Path.GetFileName(dir);
                if (name.StartsWith("Temp", StringComparison.OrdinalIgnoreCase) || name.Equals("Packages", StringComparison.OrdinalIgnoreCase)) continue;
                string found = SearchRoot(dir, depth - 1); if (!String.IsNullOrWhiteSpace(found)) return found;
            }
        }
        catch { }
        return null;
    }

    private static bool NeedsAdministrator(string target)
    {
        string path = Path.GetFullPath(target), pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), pfx86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        return (!String.IsNullOrWhiteSpace(pf) && path.StartsWith(pf, StringComparison.OrdinalIgnoreCase)) || (!String.IsNullOrWhiteSpace(pfx86) && path.StartsWith(pfx86, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsAdministrator()
    {
        WindowsIdentity id = WindowsIdentity.GetCurrent(); WindowsPrincipal p = new WindowsPrincipal(id); return p.IsInRole(WindowsBuiltInRole.Administrator);
    }

    private static void Elevate(string target)
    {
        try
        {
            ProcessStartInfo psi = new ProcessStartInfo(); psi.FileName = Application.ExecutablePath; psi.Arguments = "--target \"" + target.Replace("\"", "") + "\""; psi.UseShellExecute = true; psi.Verb = "runas"; Process.Start(psi);
        }
        catch { MessageBox.Show("A atualizacao precisa de permissao de Administrador.", "GAT Telemetria " + Version, MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }

    private static void WaitForOldAppToClose(string target)
    {
        DateTime end = DateTime.UtcNow.AddSeconds(20);
        while (DateTime.UtcNow < end)
        {
            bool running = false;
            foreach (Process p in Process.GetProcessesByName("GAT_TELEMETRIA"))
            {
                try { string path = p.MainModule.FileName; if (String.Equals(Path.GetFullPath(path), target, StringComparison.OrdinalIgnoreCase)) running = true; }
                catch { }
                finally { try { p.Dispose(); } catch { } }
            }
            if (!running) return; Thread.Sleep(350);
        }
        throw new Exception("A versao antiga ainda esta aberta. Feche o GAT Telemetria e tente novamente.");
    }

    private static void Install(string targetExe)
    {
        string installDir = Path.GetDirectoryName(targetExe); if (String.IsNullOrWhiteSpace(installDir)) throw new Exception("Pasta de instalacao invalida.");
        string tempRoot = Path.Combine(Path.GetTempPath(), "GAT_TELEMETRIA_" + Version + "_" + Guid.NewGuid().ToString("N"));
        string zip = Path.Combine(tempRoot, "runtime.zip"), stage = Path.Combine(tempRoot, "stage"); Directory.CreateDirectory(tempRoot); Directory.CreateDirectory(stage);
        try
        {
            ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;
            using (WebClient wc = new WebClient()) wc.DownloadFile(RuntimeUrl, zip);
            if (!String.Equals(Sha256(zip), ExpectedRuntimeSha, StringComparison.OrdinalIgnoreCase)) throw new Exception("Falha na verificacao de integridade do pacote 1.0.28.");
            ZipFile.ExtractToDirectory(zip, stage);
            if (!File.Exists(Path.Combine(stage, "GAT_TELEMETRIA.exe"))) throw new Exception("O pacote baixado nao contem GAT_TELEMETRIA.exe.");
            string backupRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GAT", "Backups", "GAT_Telemetria_" + DateTime.Now.ToString("yyyyMMdd_HHmmss")); Directory.CreateDirectory(backupRoot);
            foreach (string source in Directory.GetFiles(stage, "*", SearchOption.AllDirectories))
            {
                string rel = source.Substring(stage.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), dest = Path.Combine(installDir, rel);
                if (File.Exists(dest)) { string back = Path.Combine(backupRoot, rel); Directory.CreateDirectory(Path.GetDirectoryName(back)); File.Copy(dest, back, true); }
            }
            foreach (string source in Directory.GetFiles(stage, "*", SearchOption.AllDirectories))
            {
                string rel = source.Substring(stage.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), dest = Path.Combine(installDir, rel); Directory.CreateDirectory(Path.GetDirectoryName(dest)); CopyWithRetry(source, dest);
            }
            MessageBox.Show("GAT Telemetria atualizado com sucesso para a versao 1.0.28.\n\nA nova interface e os danos reais ja estao instalados.", "GAT Telemetria 1.0.28", MessageBoxButtons.OK, MessageBoxIcon.Information);
            ProcessStartInfo psi = new ProcessStartInfo(targetExe); psi.WorkingDirectory = installDir; psi.UseShellExecute = true; Process.Start(psi);
        }
        finally { try { if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, true); } catch { } }
    }

    private static void CopyWithRetry(string source, string dest)
    {
        Exception last = null;
        for (int i = 0; i < 20; i++) { try { File.Copy(source, dest, true); return; } catch (Exception ex) { last = ex; Thread.Sleep(250); } }
        throw new Exception("Nao foi possivel substituir " + Path.GetFileName(dest) + ". " + (last == null ? "" : last.Message));
    }

    private static string Sha256(string file)
    {
        using (FileStream fs = File.OpenRead(file)) using (SHA256 sha = SHA256.Create()) { byte[] hash = sha.ComputeHash(fs); return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant(); }
    }
}
