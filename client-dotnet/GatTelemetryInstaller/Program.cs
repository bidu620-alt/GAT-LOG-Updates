using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

namespace GatTelemetryInstaller
{
    internal static class Program
    {
        private static string ProgramData => Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        private static string InstallDir => Path.Combine(ProgramData, "GAT Telemetria");
        private static string CommonPrograms => Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms);
        private static string UserPrograms => Environment.GetFolderPath(Environment.SpecialFolder.Programs);

        [STAThread]
        private static void Main()
        {
            try
            {
                var answer = MessageBox.Show(
                    "Instalar GAT Telemetria C# 1.0.0 TESTE?\r\n\r\n" +
                    "A nova versão substitui o motor Go/Win32 pelo cliente C# e mantém os dados já salvos em:\r\n" +
                    "%LOCALAPPDATA%\\GAT Telemetria Cliente\r\n\r\n" +
                    "Servidores, motorista, token e conexão automática serão preservados.",
                    "GAT Telemetria C#", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                if (answer != DialogResult.Yes) return;

                Kill("GAT_TELEMETRIA");
                Kill("GAT_TELEMETRIA_0.1");
                Thread.Sleep(700);

                Directory.CreateDirectory(InstallDir);
                Extract("payload/GAT_TELEMETRIA.exe", Path.Combine(InstallDir, "GAT_TELEMETRIA.exe"));
                Extract("payload/GAT_TELEMETRIA.exe.config", Path.Combine(InstallDir, "GAT_TELEMETRIA.exe.config"));
                Extract("payload/Newtonsoft.Json.dll", Path.Combine(InstallDir, "Newtonsoft.Json.dll"));

                RemoveOldClientShortcuts();
                CreateShortcut(Path.Combine(CommonPrograms, "GAT Telemetria.lnk"), Path.Combine(InstallDir, "GAT_TELEMETRIA.exe"));

                Process.Start(new ProcessStartInfo(Path.Combine(InstallDir, "GAT_TELEMETRIA.exe"))
                {
                    UseShellExecute = true,
                    WorkingDirectory = InstallDir
                });

                MessageBox.Show(
                    "GAT Telemetria C# 1.0.0 TESTE instalado.\r\n\r\n" +
                    "Programa: " + InstallDir + "\r\n\r\n" +
                    "A pasta de dados antiga foi preservada.",
                    "GAT Telemetria", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Falha na instalação:\r\n\r\n" + ex, "GAT Telemetria", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static void Extract(string resource, string destination)
        {
            using (var input = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource))
            {
                if (input == null) throw new FileNotFoundException("Recurso não encontrado: " + resource);
                string tmp = destination + ".new";
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
                    try { p.Kill(); p.WaitForExit(1800); } catch { }
                    finally { p.Dispose(); }
                }
            }
            catch { }
        }

        private static void RemoveOldClientShortcuts()
        {
            RemoveMatching(CommonPrograms);
            RemoveMatching(UserPrograms);
            RemoveMatching(Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory));
            RemoveMatching(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory));
        }

        private static void RemoveMatching(string directory)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory)) return;
                foreach (var pattern in new[] { "GAT Telemetria*.lnk", "GAT_TELEMETRIA*.lnk" })
                    foreach (var file in Directory.GetFiles(directory, pattern))
                        try { File.Delete(file); } catch { }
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
            shortcut.Description = "GAT Telemetria";
            shortcut.Save();
        }
    }
}
