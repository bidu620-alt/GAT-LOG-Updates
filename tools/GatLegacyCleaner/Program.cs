using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace GatLegacyCleaner
{
    internal static class Program
    {
        private static readonly string LocalAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        private static readonly string ProgramData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        private static readonly string ProgramFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        private static readonly string ProgramFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        private static readonly string NewServerDir = Path.Combine(ProgramData, "GAT-LOG Server");
        private static readonly string NewClientDir = Path.Combine(ProgramData, "GAT Telemetria");
        private static readonly string SharedServerData = Path.Combine(LocalAppData, "GAT-LOG");
        private static readonly string SharedClientData = Path.Combine(LocalAppData, "GAT Telemetria Cliente");

        private static readonly List<string> LegacyDirs = new List<string>
        {
            Path.Combine(LocalAppData, "Programs", "GAT-LOG Server"),
            Path.Combine(LocalAppData, "Programs", "GAT-LOG SERVER NATIVE"),
            Path.Combine(LocalAppData, "Programs", "GAT-LOG"),
            Path.Combine(LocalAppData, "Programs", "GAT Telemetria"),
            Path.Combine(LocalAppData, "Programs", "GAT Telemetria Cliente"),
            Path.Combine(ProgramFiles, "GAT-LOG Server"),
            Path.Combine(ProgramFilesX86, "GAT-LOG Server"),
            Path.Combine(ProgramFiles, "GAT Telemetria"),
            Path.Combine(ProgramFilesX86, "GAT Telemetria")
        };

        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            var confirm = MessageBox.Show(
                "Este utilitário remove somente instalações ANTIGAS do GAT-LOG Server e GAT Telemetria.\r\n\r\n" +
                "SERÃO PRESERVADOS:\r\n" +
                "• Dados do servidor em %LOCALAPPDATA%\\GAT-LOG\r\n" +
                "• Dados da telemetria em %LOCALAPPDATA%\\GAT Telemetria Cliente\r\n" +
                "• GAT-LOG Server C# em C:\\ProgramData\\GAT-LOG Server\r\n" +
                "• GAT Telemetria C# em C:\\ProgramData\\GAT Telemetria\r\n" +
                "• ETS2 Dedicated Server, TruckSim GPS e Tailscale\r\n\r\n" +
                "Deseja continuar?",
                "GAT-LOG | Limpeza de versões antigas",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (confirm != DialogResult.Yes) return;

            var log = new StringBuilder();
            int removed = 0;
            int failures = 0;

            Log(log, "Início da limpeza.");
            Log(log, "Preservando: " + SharedServerData);
            Log(log, "Preservando: " + SharedClientData);
            Log(log, "Preservando: " + NewServerDir);
            Log(log, "Preservando: " + NewClientDir);

            // Cliente antigo tem nome exclusivo, então pode ser encerrado diretamente.
            removed += KillAllByName("GAT_TELEMETRIA_0.1", log);
            removed += KillAllByName("GAT_TELEMETRIA_NATIVE", log);
            removed += KillAllByName("GAT_LOG_SERVER_NATIVE", log);

            // Server/Agent novos usam nomes iguais aos antigos. Só encerramos se o caminho for legado.
            removed += KillLegacyByPath("GAT_LOG_SERVER", log);
            removed += KillLegacyByPath("GAT_LOG_AGENT", log);

            Thread.Sleep(500);

            foreach (var dir in LegacyDirs.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (IsProtectedPath(dir)) continue;
                try
                {
                    if (!Directory.Exists(dir)) continue;
                    ClearReadOnly(dir);
                    Directory.Delete(dir, true);
                    removed++;
                    Log(log, "Pasta removida: " + dir);
                }
                catch (Exception ex)
                {
                    failures++;
                    Log(log, "FALHA ao remover pasta: " + dir + " | " + ex.Message);
                }
            }

            RemoveLegacyShortcuts(log, ref removed, ref failures);

            string logPath = Path.Combine(Path.GetTempPath(), "GAT_LOG_LIMPEZA_ANTIGOS.txt");
            try { File.WriteAllText(logPath, log.ToString(), Encoding.UTF8); } catch { }

            string result =
                "Limpeza concluída.\r\n\r\n" +
                "Itens/processos antigos removidos: " + removed + "\r\n" +
                "Falhas: " + failures + "\r\n\r\n" +
                "As pastas de dados e as instalações C# em ProgramData foram preservadas.\r\n\r\n" +
                "Agora você pode instalar o GAT-LOG Server C# e o GAT Telemetria C#.";

            if (failures > 0)
                result += "\r\n\r\nLog: " + logPath;

            MessageBox.Show(result, "GAT-LOG | Limpeza concluída", MessageBoxButtons.OK,
                failures == 0 ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        }

        private static int KillAllByName(string processName, StringBuilder log)
        {
            int count = 0;
            try
            {
                foreach (var p in Process.GetProcessesByName(processName))
                {
                    try
                    {
                        Log(log, "Encerrando processo antigo: " + p.ProcessName + " PID " + p.Id);
                        p.Kill();
                        p.WaitForExit(1800);
                        count++;
                    }
                    catch (Exception ex)
                    {
                        Log(log, "FALHA ao encerrar " + processName + ": " + ex.Message);
                    }
                    finally { p.Dispose(); }
                }
            }
            catch (Exception ex) { Log(log, "Falha ao consultar processo " + processName + ": " + ex.Message); }
            return count;
        }

        private static int KillLegacyByPath(string processName, StringBuilder log)
        {
            int count = 0;
            try
            {
                foreach (var p in Process.GetProcessesByName(processName))
                {
                    try
                    {
                        string exe = string.Empty;
                        try { exe = p.MainModule?.FileName ?? string.Empty; } catch { }
                        if (string.IsNullOrWhiteSpace(exe) || !IsLegacyPath(exe)) continue;
                        if (IsProtectedPath(exe)) continue;

                        Log(log, "Encerrando processo legado: " + p.ProcessName + " PID " + p.Id + " | " + exe);
                        p.Kill();
                        p.WaitForExit(1800);
                        count++;
                    }
                    catch (Exception ex)
                    {
                        Log(log, "FALHA ao encerrar processo legado " + processName + ": " + ex.Message);
                    }
                    finally { p.Dispose(); }
                }
            }
            catch (Exception ex) { Log(log, "Falha ao consultar " + processName + ": " + ex.Message); }
            return count;
        }

        private static bool IsLegacyPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            string full = SafeFullPath(path);
            if (IsProtectedPath(full)) return false;

            foreach (var legacy in LegacyDirs)
            {
                if (IsUnder(full, legacy)) return true;
            }

            string file = Path.GetFileName(full) ?? string.Empty;
            return file.Equals("GAT_TELEMETRIA_0.1.exe", StringComparison.OrdinalIgnoreCase) ||
                   file.IndexOf("SERVER_NATIVE", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsProtectedPath(string path)
        {
            string full = SafeFullPath(path);
            return IsUnder(full, NewServerDir) ||
                   IsUnder(full, NewClientDir) ||
                   IsUnder(full, SharedServerData) ||
                   IsUnder(full, SharedClientData);
        }

        private static bool IsUnder(string path, string root)
        {
            if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(root)) return false;
            string p = SafeFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string r = SafeFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            return p.StartsWith(r, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(p.TrimEnd(Path.DirectorySeparatorChar), r.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase);
        }

        private static string SafeFullPath(string path)
        {
            try { return Path.GetFullPath(Environment.ExpandEnvironmentVariables(path)); }
            catch { return path ?? string.Empty; }
        }

        private static void ClearReadOnly(string dir)
        {
            try
            {
                foreach (var file in Directory.GetFiles(dir, "*", SearchOption.AllDirectories))
                {
                    try
                    {
                        var attr = File.GetAttributes(file);
                        if ((attr & FileAttributes.ReadOnly) != 0)
                            File.SetAttributes(file, attr & ~FileAttributes.ReadOnly);
                    }
                    catch { }
                }
            }
            catch { }
        }

        private static void RemoveLegacyShortcuts(StringBuilder log, ref int removed, ref int failures)
        {
            var roots = new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory),
                Environment.GetFolderPath(Environment.SpecialFolder.Programs),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms),
                Environment.GetFolderPath(Environment.SpecialFolder.Startup),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup)
            };

            foreach (var root in roots.Where(x => !string.IsNullOrWhiteSpace(x) && Directory.Exists(x)).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                IEnumerable<string> links;
                try { links = Directory.GetFiles(root, "*.lnk", SearchOption.AllDirectories); }
                catch { continue; }

                foreach (var link in links)
                {
                    try
                    {
                        string name = Path.GetFileNameWithoutExtension(link) ?? string.Empty;
                        string target = GetShortcutTarget(link);
                        bool oldName = name.IndexOf("NATIVE", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                       name.IndexOf("TELEMETRIA_0.1", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                       name.IndexOf("TELEMETRIA 0.1", StringComparison.OrdinalIgnoreCase) >= 0;
                        bool oldTarget = !string.IsNullOrWhiteSpace(target) && IsLegacyPath(target);

                        if (!oldName && !oldTarget) continue;
                        File.Delete(link);
                        removed++;
                        Log(log, "Atalho antigo removido: " + link);
                    }
                    catch (Exception ex)
                    {
                        failures++;
                        Log(log, "FALHA ao remover atalho " + link + ": " + ex.Message);
                    }
                }
            }
        }

        private static string GetShortcutTarget(string shortcutPath)
        {
            try
            {
                var shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType == null) return string.Empty;
                dynamic shell = Activator.CreateInstance(shellType);
                dynamic shortcut = shell.CreateShortcut(shortcutPath);
                return Convert.ToString(shortcut.TargetPath) ?? string.Empty;
            }
            catch { return string.Empty; }
        }

        private static void Log(StringBuilder log, string text)
        {
            log.AppendLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + text);
        }
    }
}
