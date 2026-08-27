using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json;

namespace GatLogServer
{
    internal static class UpdateService
    {
        private const string ManifestUrl = "https://raw.githubusercontent.com/bidu620-alt/GAT-LOG-Updates/main/server_dotnet_version.json";

        public static async Task CheckAsync(string currentVersion, IWin32Window owner, bool silent)
        {
            try
            {
                using (var http = new HttpClient { Timeout = TimeSpan.FromSeconds(8) })
                {
                    var text = await http.GetStringAsync(ManifestUrl);
                    var remote = JsonConvert.DeserializeObject<RemoteVersion>(text);
                    if (remote == null || string.IsNullOrWhiteSpace(remote.Version))
                        throw new InvalidOperationException("Manifesto de atualização inválido.");

                    Version cur, rem;
                    if (!Version.TryParse(currentVersion, out cur)) cur = new Version(0, 0);
                    if (!Version.TryParse(remote.Version, out rem)) rem = new Version(0, 0);

                    if (rem <= cur)
                    {
                        if (!silent) MessageBox.Show(owner, "Você já está usando a versão mais recente.", "GAT-LOG", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }

                    var answer = MessageBox.Show(owner,
                        "Nova versão " + remote.Version + " disponível.\r\n\r\n" + (remote.Notes ?? "") + "\r\n\r\nDeseja atualizar agora?",
                        "GAT-LOG | Atualização", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                    if (answer != DialogResult.Yes) return;

                    var bytes = await http.GetByteArrayAsync(remote.SetupUrl);
                    if (!string.IsNullOrWhiteSpace(remote.Sha256))
                    {
                        using (var sha = SHA256.Create())
                        {
                            var got = BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
                            if (!string.Equals(got, remote.Sha256.Trim().ToLowerInvariant(), StringComparison.Ordinal))
                                throw new InvalidOperationException("SHA-256 da atualização não confere.");
                        }
                    }

                    var path = Path.Combine(Path.GetTempPath(), "GAT_LOG_SERVER_DOTNET_UPDATE_" + remote.Version + ".exe");
                    File.WriteAllBytes(path, bytes);
                    Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
                    Application.Exit();
                }
            }
            catch (Exception ex)
            {
                if (!silent)
                    MessageBox.Show(owner, "Não foi possível verificar a atualização.\r\n\r\n" + ex.Message, "GAT-LOG", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
}
