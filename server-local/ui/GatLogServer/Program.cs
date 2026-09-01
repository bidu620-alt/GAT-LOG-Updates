using System;
using System.Windows.Forms;

namespace GatLogServer;

internal static class Program
{
	[STAThread]
	private static void Main(string[] args)
	{
		if (args.Length == 1 && args[0] == "--central-only") { try { CentralPanel.StartBackground(); } catch { } return; }
		Application.EnableVisualStyles();
		Application.SetCompatibleTextRenderingDefault(defaultValue: false);
		using (LoginForm loginForm = new LoginForm())
		{
			if (loginForm.ShowDialog() != DialogResult.OK)
			{
				return;
			}
		}
		Application.Run(new MainForm());
	}
}
