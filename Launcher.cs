using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        try
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string truckDir = Path.Combine(baseDir, "TruckSimGPS");
            string truckExe = Path.Combine(truckDir, "TruckSimGPS_Server.exe");
            string gatExe = Path.Combine(baseDir, "GAT_TELEMETRIA_APP.exe");
            if (!File.Exists(gatExe))
            {
                MessageBox.Show("A instalação do GAT Telemetria está incompleta. Reinstale o aplicativo.", "GAT Telemetria", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            if (!Process.GetProcessesByName("TruckSimGPS_Server").Any() && File.Exists(truckExe))
            {
                try
                {
                    Process.Start(new ProcessStartInfo { FileName = truckExe, Arguments = "-minimized", WorkingDirectory = truckDir, UseShellExecute = true, WindowStyle = ProcessWindowStyle.Minimized });
                    Thread.Sleep(1200);
                }
                catch { }
            }
            Process.Start(new ProcessStartInfo { FileName = gatExe, WorkingDirectory = baseDir, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show("Não foi possível iniciar o GAT Telemetria.\n\n" + ex.Message, "GAT Telemetria", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
