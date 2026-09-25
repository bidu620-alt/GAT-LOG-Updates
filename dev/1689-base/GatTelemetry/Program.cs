using System;
using System.Threading;
using System.Windows.Forms;

namespace GatTelemetry;

internal static class Program
{
	[STAThread]
	private static void Main()
	{
		bool createdNew;
		using (new Mutex(initiallyOwned: true, "GAT_TELEMETRIA_CSHARP_SINGLE_INSTANCE", out createdNew))
		{
			if (!createdNew)
			{
				MessageBox.Show("O GAT Telemetria já está aberto.", "GAT Telemetria", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
				return;
			}
			Application.EnableVisualStyles();
			Application.SetCompatibleTextRenderingDefault(defaultValue: false);
			Application.Run(new MainForm());
		}
	}
}
