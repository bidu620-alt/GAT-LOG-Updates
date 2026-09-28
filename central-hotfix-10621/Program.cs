// Build trigger after enabling PR workflow on main.
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

internal static class Program
{
    private const string ServerDir = @"C:\ProgramData\GAT-LOG Server";
    private static readonly string CentralDir = Path.Combine(ServerDir, "central");
    private static readonly string NodeExe = Path.Combine(CentralDir, "node.exe");
    private static readonly string HostMjs = Path.Combine(CentralDir, "host.mjs");
    private static readonly string StartScript = Path.Combine(ServerDir, "GAT_CENTRAL_START.ps1");
    private const string RunValueName = "GAT Central Local";

    [STAThread]
    private static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        try
        {
            RunAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "Nao foi possivel concluir o hotfix da Central.\r\n\r\n" + ex.Message,
                "GAT Central - Hotfix 1.0.62.1",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private static async Task RunAsync()
    {
        if (!File.Exists(NodeExe) || !File.Exists(HostMjs))
        {
            MessageBox.Show(
                "Nao encontrei a Central instalada neste computador.\r\n\r\n" +
                "Esperado:\r\n" + NodeExe + "\r\n" + HostMjs + "\r\n\r\n" +
                "Este hotfix nao altera nem cria o banco de dados.",
                "GAT Central - Hotfix 1.0.62.1",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string dataDir = Path.Combine(localAppData, "GAT-LOG", "Central");
        Directory.CreateDirectory(dataDir);

        string backupDir = Path.Combine(ServerDir, "update-backups", "hotfix-central-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"));
        Directory.CreateDirectory(backupDir);

        if (File.Exists(StartScript))
            File.Copy(StartScript, Path.Combine(backupDir, Path.GetFileName(StartScript)), true);

        string previousRun = "";
        using (var runKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true)
                           ?? Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run"))
        {
            previousRun = Convert.ToString(runKey?.GetValue(RunValueName)) ?? "";
            if (!string.IsNullOrWhiteSpace(previousRun))
                File.WriteAllText(Path.Combine(backupDir, "previous-run-entry.txt"), previousRun);

            string command = "powershell.exe -NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -File \"" + StartScript + "\"";
            runKey?.SetValue(RunValueName, command, RegistryValueKind.String);
        }

        string script = @"$ErrorActionPreference = 'SilentlyContinue'
$central = 'C:\ProgramData\GAT-LOG Server\central'
$node = Join-Path $central 'node.exe'
$host = Join-Path $central 'host.mjs'
$data = Join-Path $env:LOCALAPPDATA 'GAT-LOG\Central'
$log = Join-Path $data 'startup-hotfix.log'
New-Item -ItemType Directory -Force -Path $data | Out-Null
function Log([string]$m) {
  Add-Content -Path $log -Value ((Get-Date -Format 'yyyy-MM-dd HH:mm:ss') + ' ' + $m)
}
if (!(Test-Path $node) -or !(Test-Path $host)) {
  Log 'ERRO: node.exe ou host.mjs nao encontrado.'
  exit 2
}
$listener = Get-NetTCPConnection -State Listen -LocalPort 5056 -ErrorAction SilentlyContinue | Select-Object -First 1
if ($listener) {
  Log ('Central ja ativa na porta 5056. PID=' + $listener.OwningProcess)
  exit 0
}
Log 'Iniciando GAT Central Local...'
Start-Process -FilePath $node -ArgumentList @($host) -WorkingDirectory $central -WindowStyle Hidden
Start-Sleep -Seconds 3
$listener = Get-NetTCPConnection -State Listen -LocalPort 5056 -ErrorAction SilentlyContinue | Select-Object -First 1
if ($listener) {
  Log ('Central iniciada. PID=' + $listener.OwningProcess)
  exit 0
}
Log 'ERRO: a Central nao abriu a porta 5056.'
exit 3
";
        File.WriteAllText(StartScript, script);

        // Executa o reparo agora, sem tocar no banco ou no Cloudflare.
        var psi = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = "-NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -File \"" + StartScript + "\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = ServerDir
        };
        using (var p = Process.Start(psi))
        {
            if (p != null)
            {
                if (!p.WaitForExit(15000))
                {
                    try { p.Kill(); } catch { }
                }
            }
        }

        bool healthy = await WaitForHealthAsync();
        if (healthy)
        {
            File.WriteAllText(
                Path.Combine(dataDir, "hotfix-central-1.0.62.1-installed.txt"),
                "Hotfix instalado em " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") +
                Environment.NewLine +
                "Inicializacao automatica: HKCU Run -> " + StartScript);

            MessageBox.Show(
                "Hotfix 1.0.62.1 instalado com sucesso.\r\n\r\n" +
                "A Central esta respondendo em 127.0.0.1:5056.\r\n" +
                "Ela tambem foi configurada para iniciar automaticamente ao entrar no Windows.\r\n\r\n" +
                "Banco de dados, ranking, contas, historico e Cloudflare NAO foram alterados.",
                "GAT Central corrigida",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        else
        {
            MessageBox.Show(
                "O hotfix foi instalado, mas a Central ainda nao respondeu na porta 5056.\r\n\r\n" +
                "Confira o log:\r\n" + Path.Combine(dataDir, "startup-hotfix.log") + "\r\n\r\n" +
                "Nenhum dado do banco foi apagado ou modificado.",
                "GAT Central - verificar inicializacao",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }

    private static async Task<bool> WaitForHealthAsync()
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        for (int i = 0; i < 10; i++)
        {
            try
            {
                using var response = await client.GetAsync("http://127.0.0.1:5056/health");
                if (response.IsSuccessStatusCode)
                {
                    string body = await response.Content.ReadAsStringAsync();
                    if (body.Contains("GAT Central Local", StringComparison.OrdinalIgnoreCase) ||
                        body.Contains("\"ok\":true", StringComparison.OrdinalIgnoreCase) ||
                        body.Contains("\"ok\": true", StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
            catch { }

            await Task.Delay(1000);
        }
        return false;
    }
}
