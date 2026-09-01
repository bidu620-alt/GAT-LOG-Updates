using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json;

namespace GatLogServer;

internal static class UpdateService
{
	private const string ManifestUrl = "https://raw.githubusercontent.com/bidu620-alt/GAT-LOG-Updates/main/server_dotnet_version.json";

	public static async Task CheckAsync(string currentVersion, IWin32Window owner, bool silent)
	{
		_ = 1;
		try
		{
			using HttpClient http = new HttpClient
			{
				Timeout = TimeSpan.FromSeconds(12.0)
			};
			RemoteVersion remote = JsonConvert.DeserializeObject<RemoteVersion>(await http.GetStringAsync("https://raw.githubusercontent.com/bidu620-alt/GAT-LOG-Updates/main/server_dotnet_version.json"));
			if (remote == null || string.IsNullOrWhiteSpace(remote.Version))
			{
				throw new InvalidOperationException("Manifesto de atualização inválido.");
			}
			if (!Version.TryParse(currentVersion, out var result))
			{
				result = new Version(0, 0);
			}
			if (!Version.TryParse(remote.Version, out var result2))
			{
				result2 = new Version(0, 0);
			}
			if (result2 <= result)
			{
				if (!silent)
				{
					MessageBox.Show(owner, "Você já está usando a versão mais recente.", "GAT-LOG", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
				}
				return;
			}
			if (MessageBox.Show(owner, "Nova versão " + remote.Version + " disponível.\r\n\r\n" + remote.Notes + "\r\n\r\nConfigurações, contas, histórico, tokens e backups serão preservados.\r\n\r\nDeseja atualizar agora?", "GAT-LOG | Atualização", MessageBoxButtons.YesNo, MessageBoxIcon.Asterisk) != DialogResult.Yes)
			{
				return;
			}
			byte[] array = await http.GetByteArrayAsync(remote.SetupUrl);
			if (!string.IsNullOrWhiteSpace(remote.Sha256))
			{
				using SHA256 sHA = SHA256.Create();
				if (!string.Equals(BitConverter.ToString(sHA.ComputeHash(array)).Replace("-", "").ToLowerInvariant(), remote.Sha256.Trim().ToLowerInvariant(), StringComparison.Ordinal))
				{
					throw new InvalidOperationException("SHA-256 da atualização não confere.");
				}
			}
			string text = Path.Combine(Path.GetTempPath(), "GAT_LOG_SERVER_DOTNET_UPDATE_" + remote.Version + ".exe");
			File.WriteAllBytes(text, array);
			Process.Start(new ProcessStartInfo(text)
			{
				UseShellExecute = true
			});
			Application.Exit();
		}
		catch (Exception ex)
		{
			if (!silent)
			{
				MessageBox.Show(owner, "Não foi possível verificar a atualização.\r\n\r\n" + ex.Message, "GAT-LOG", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
			}
		}
	}
}
