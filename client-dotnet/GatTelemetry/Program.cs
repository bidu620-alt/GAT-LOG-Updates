using System;
using System.Threading;
using System.Windows.Forms;

namespace GatTelemetry
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            bool created;
            using (var mutex = new Mutex(true, "GAT_TELEMETRIA_CSHARP_SINGLE_INSTANCE", out created))
            {
                if (!created)
                {
                    MessageBox.Show("O GAT Telemetria já está aberto.", "GAT Telemetria", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new MainForm());
            }
        }
    }
}
