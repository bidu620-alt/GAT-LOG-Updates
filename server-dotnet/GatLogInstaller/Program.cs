using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

namespace GatLogInstaller
{
    internal static class Program
    {
        private static string ProgramData => Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        private static string InstallDir => Path.Combine(ProgramData, "GAT-LOG Server");
        private static string StartMenu => Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms);

        [STAThread]
        private static void Main()
        {
            try
            {
                var answer = MessageBox.Show(
                    "Instalar GAT-LOG Server C# 1.0.0 TESTE?\r\n\r\n" +
                    "O cliente dos motoristas não será alterado.\r\n" +
                    "Configurações e telemetria em %LOCALAPPDATA%\\GAT-LOG serão preservadas.\r\n\r\n" +
                    "A versão antiga da interface será fechada.",
                    "GAT-LOG Server C#", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                if (answer != DialogResult.Yes) return;

                Kill("GAT_LOG_SERVER");
                Kill("GAT_LOG_AGENT");
                Thread.Sleep(500);

                Directory.CreateDirectory(InstallDir);
                Directory.CreateDirectory(Path.Combine(InstallDir, "assets"));

                Extract("payload/GAT_LOG_SERVER.exe", Path.Combine(InstallDir, "GAT_LOG_SERVER.exe"));
                Extract("payload/GAT_LOG_SERVER.exe.config", Path.Combine(InstallDir, "GAT_LOG_SERVER.exe.config"));
                Extract("payload/Newtonsoft.Json.dll", Path.Combine(InstallDir, "Newtonsoft.Json.dll"));
                Extract("payload/GAT_LOG_AGENT.exe", Path.Combine(InstallDir, "GAT_LOG_AGENT.exe"));
                Extract("payload/assets/logo.png", Path.Combine(InstallDir, "assets", "logo.png"));
                Extract("payload/assets/banner.png", Path.Combine(InstallDir, "assets", "banner.png"));

                RemoveOldShortcuts();
                CreateShortcut(Path.Combine(StartMenu, "GAT-LOG Server.lnk"), Path.Combine(InstallDir, "GAT_LOG_SERVER.exe"));

                StartHidden(Path.Combine(InstallDir, "GAT_LOG_AGENT.exe"), "--background");
                Thread.Sleep(500);
                Process.Start(new ProcessStartInfo(Path.Combine(InstallDir, "GAT_LOG_SERVER.exe")) { UseShellExecute = true, WorkingDirectory = InstallDir });

                MessageBox.Show(
                    "GAT-LOG Server C# 1.0.0 TESTE instalado.\r\n\r\n" +
                    "Programa: " + InstallDir + "\r\n" +
                    "Atalho: " + Path.Combine(StartMenu, "GAT-LOG Server.lnk") + "\r\n\r\n" +
                    "O servidor dedicado do ETS2 não foi apagado nem modificado.",
                    "GAT-LOG", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Falha na instalação:\r\n\r\n" + ex, "GAT-LOG", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static void Extract(string resource, string destination)
        {
            using (var input = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource))
            {
                if (input == null) throw new FileNotFoundException("Recurso não encontrado no instalador: " + resource);
                var tmp = destination + ".new";
                using (var output = File.Create(tmp)) input.CopyTo(output);
                if (File.Exists(destination)) File.Delete(destination);
                File.Move(tmp, destination);
            }
        }

        private static void Kill(string processName)
        {
            try
            {
                foreach (var p in Process.GetProcessesByName(processName))
                {
                    try { p.Kill(); p.WaitForExit(1500); } catch { }
                    finally { p.Dispose(); }
                }
            }
            catch { }
        }

        private static void StartHidden(string exe, string args)
        {
            Process.Start(new ProcessStartInfo(exe, args)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                WorkingDirectory = Path.GetDirectoryName(exe)
            });
        }

        private static void RemoveOldShortcuts()
        {
            try
            {
                foreach (var file in Directory.GetFiles(StartMenu, "GAT*.lnk"))
                {
                    try { File.Delete(file); } catch { }
                }
                var desktop = Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);
                foreach (var file in Directory.GetFiles(desktop, "GAT*.lnk"))
                {
                    try { File.Delete(file); } catch { }
                }
            }
            catch { }
        }

        private static void CreateShortcut(string shortcutPath, string targetPath)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(shortcutPath));
            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null) return;
            dynamic shell = Activator.CreateInstance(shellType);
            dynamic shortcut = shell.CreateShortcut(shortcutPath);
            shortcut.TargetPath = targetPath;
            shortcut.WorkingDirectory = Path.GetDirectoryName(targetPath);
            shortcut.Description = "GAT-LOG Server";
            shortcut.Save();
        }
    }
}
