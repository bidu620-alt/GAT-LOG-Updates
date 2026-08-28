using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GatTelemetry
{
    internal static class ClientStore
    {
        public static string DataDir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GAT Telemetria Cliente");
        public static string ServersFile => Path.Combine(DataDir, "servers.json");
        public static string CredentialsFile => Path.Combine(DataDir, "credentials.json");
        public static string SettingsFile => Path.Combine(DataDir, "client_settings.json");
        public static string LogFile => Path.Combine(DataDir, "gat_dotnet.log");

        public static void Ensure()
        {
            Directory.CreateDirectory(DataDir);
        }

        public static List<ServerEntry> LoadServers()
        {
            Ensure();
            try
            {
                if (!File.Exists(ServersFile)) return new List<ServerEntry>();

                string text = File.ReadAllText(ServersFile, Encoding.UTF8);
                var root = JToken.Parse(text);
                var found = new List<ServerEntry>();
                CollectServers(root, found);

                // Algumas versões antigas salvaram um objeto PowerShell contendo
                // { value: [ ... ], Count: N } junto com as entradas normais.
                // Normalizamos tudo e removemos duplicados pelo endpoint.
                var map = new Dictionary<string, ServerEntry>(StringComparer.OrdinalIgnoreCase);
                foreach (var item in found)
                {
                    if (item == null || string.IsNullOrWhiteSpace(item.Endpoint)) continue;
                    string ep = NormalizeEndpoint(item.Endpoint);
                    if (string.IsNullOrWhiteSpace(ep)) continue;
                    map[ep] = new ServerEntry
                    {
                        Name = string.IsNullOrWhiteSpace(item.Name) ? "Servidor GAT" : item.Name.Trim(),
                        Endpoint = ep
                    };
                }

                var result = map.Values.OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase).ToList();

                // Ao detectar formato legado/misturado, guarda backup e regrava no
                // formato canônico para as próximas inicializações.
                string canonical = JsonConvert.SerializeObject(result, Formatting.Indented);
                if (!JsonEquivalent(text, canonical))
                {
                    BackupOnce(ServersFile, "servers.before-dotnet-migration.json");
                    File.WriteAllText(ServersFile, canonical, Encoding.UTF8);
                    Log("servers.json legado normalizado: " + result.Count + " servidor(es)");
                }

                return result;
            }
            catch (Exception ex)
            {
                Log("LoadServers falhou: " + ex.Message);
                return new List<ServerEntry>();
            }
        }

        private static void CollectServers(JToken token, List<ServerEntry> output)
        {
            if (token == null || output == null) return;

            var array = token as JArray;
            if (array != null)
            {
                foreach (var child in array) CollectServers(child, output);
                return;
            }

            var obj = token as JObject;
            if (obj == null) return;

            string endpoint = obj.Value<string>("endpoint");
            if (!string.IsNullOrWhiteSpace(endpoint))
            {
                output.Add(new ServerEntry
                {
                    Name = obj.Value<string>("name"),
                    Endpoint = endpoint
                });
            }

            // Suporta o wrapper criado por versões antigas/PowerShell.
            var value = obj["value"] ?? obj["Value"];
            if (value != null) CollectServers(value, output);
        }

        public static void SaveServers(List<ServerEntry> servers)
        {
            Ensure();
            var clean = (servers ?? new List<ServerEntry>())
                .Where(x => x != null && !string.IsNullOrWhiteSpace(x.Endpoint))
                .Select(x => new ServerEntry
                {
                    Name = string.IsNullOrWhiteSpace(x.Name) ? "Servidor GAT" : x.Name.Trim(),
                    Endpoint = NormalizeEndpoint(x.Endpoint)
                })
                .GroupBy(x => x.Endpoint, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.Last())
                .ToList();
            File.WriteAllText(ServersFile, JsonConvert.SerializeObject(clean, Formatting.Indented), Encoding.UTF8);
        }

        public static ClientSettings LoadSettings()
        {
            Ensure();
            try
            {
                if (!File.Exists(SettingsFile)) return new ClientSettings();
                return JsonConvert.DeserializeObject<ClientSettings>(File.ReadAllText(SettingsFile, Encoding.UTF8)) ?? new ClientSettings();
            }
            catch
            {
                return new ClientSettings();
            }
        }

        public static void SaveSettings(ClientSettings settings)
        {
            Ensure();
            settings.UpdatedAt = DateTime.UtcNow.ToString("o");
            File.WriteAllText(SettingsFile, JsonConvert.SerializeObject(settings, Formatting.Indented), Encoding.UTF8);
        }

        public static List<CredentialEntry> LoadCredentials()
        {
            Ensure();
            try
            {
                if (!File.Exists(CredentialsFile)) return new List<CredentialEntry>();

                string text = File.ReadAllText(CredentialsFile, Encoding.UTF8);
                var root = JToken.Parse(text);
                var found = new List<CredentialEntry>();
                CollectCredentials(root, found);

                return found
                    .Where(x => x != null && !string.IsNullOrWhiteSpace(x.Endpoint) && !string.IsNullOrWhiteSpace(x.Driver))
                    .GroupBy(x => NormalizeEndpoint(x.Endpoint) + "\n" + x.Driver, StringComparer.OrdinalIgnoreCase)
                    .Select(g => g.OrderByDescending(x => x.SavedAt).First())
                    .ToList();
            }
            catch (Exception ex)
            {
                Log("LoadCredentials falhou: " + ex.Message);
                return new List<CredentialEntry>();
            }
        }

        private static void CollectCredentials(JToken token, List<CredentialEntry> output)
        {
            if (token == null || output == null) return;

            var array = token as JArray;
            if (array != null)
            {
                foreach (var child in array) CollectCredentials(child, output);
                return;
            }

            var obj = token as JObject;
            if (obj == null) return;

            string endpoint = obj.Value<string>("endpoint");
            string driver = obj.Value<string>("driver");
            if (!string.IsNullOrWhiteSpace(endpoint) && !string.IsNullOrWhiteSpace(driver))
            {
                output.Add(new CredentialEntry
                {
                    Endpoint = NormalizeEndpoint(endpoint),
                    Driver = driver,
                    Token = obj.Value<string>("token"),
                    SavedAt = obj.Value<string>("saved_at")
                });
            }

            var value = obj["value"] ?? obj["Value"];
            if (value != null) CollectCredentials(value, output);
        }

        public static CredentialEntry FindCredential(string endpoint, string preferredDriver = null)
        {
            string ep = NormalizeEndpoint(endpoint);
            var all = LoadCredentials()
                .Where(x => string.Equals(NormalizeEndpoint(x.Endpoint), ep, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (!string.IsNullOrWhiteSpace(preferredDriver))
            {
                var exact = all.FirstOrDefault(x => string.Equals(x.Driver, preferredDriver, StringComparison.OrdinalIgnoreCase));
                if (exact != null) return exact;
            }
            return all.OrderByDescending(x => x.SavedAt).FirstOrDefault();
        }

        public static string GetPlainToken(CredentialEntry credential)
        {
            if (credential == null || string.IsNullOrWhiteSpace(credential.Token)) return string.Empty;
            try
            {
                byte[] protectedBytes = Convert.FromBase64String(credential.Token);
                byte[] clear = ProtectedData.Unprotect(protectedBytes, null, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(clear);
            }
            catch
            {
                return string.Empty;
            }
        }

        public static void SaveCredential(string endpoint, string driver, string token)
        {
            if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(driver) || string.IsNullOrWhiteSpace(token)) return;
            string ep = NormalizeEndpoint(endpoint);
            var all = LoadCredentials();
            all.RemoveAll(x => string.Equals(NormalizeEndpoint(x.Endpoint), ep, StringComparison.OrdinalIgnoreCase) &&
                               string.Equals(x.Driver, driver, StringComparison.OrdinalIgnoreCase));
            byte[] clear = Encoding.UTF8.GetBytes(token);
            byte[] protectedBytes = ProtectedData.Protect(clear, null, DataProtectionScope.CurrentUser);
            all.Add(new CredentialEntry
            {
                Endpoint = ep,
                Driver = driver,
                Token = Convert.ToBase64String(protectedBytes),
                SavedAt = DateTime.UtcNow.ToString("o")
            });
            File.WriteAllText(CredentialsFile, JsonConvert.SerializeObject(all, Formatting.Indented), Encoding.UTF8);
        }

        public static string GetDeviceId()
        {
            string guid = ReadMachineGuid(RegistryView.Registry64) ?? ReadMachineGuid(RegistryView.Registry32);
            if (string.IsNullOrWhiteSpace(guid)) guid = Environment.MachineName + "|" + Environment.UserName;
            using (var sha = SHA256.Create())
            {
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(guid))).Replace("-", "").ToLowerInvariant();
            }
        }

        private static string ReadMachineGuid(RegistryView view)
        {
            try
            {
                using (var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view))
                using (var key = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography"))
                {
                    return key?.GetValue("MachineGuid")?.ToString();
                }
            }
            catch { return null; }
        }

        public static string NormalizeEndpoint(string endpoint)
        {
            if (string.IsNullOrWhiteSpace(endpoint)) return string.Empty;
            endpoint = endpoint.Trim().TrimEnd('/');
            if (!endpoint.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !endpoint.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                endpoint = "https://" + endpoint;
            return endpoint;
        }

        private static bool JsonEquivalent(string a, string b)
        {
            try
            {
                return JToken.DeepEquals(JToken.Parse(a), JToken.Parse(b));
            }
            catch { return false; }
        }

        private static void BackupOnce(string sourcePath, string backupName)
        {
            try
            {
                string backup = Path.Combine(DataDir, backupName);
                if (File.Exists(sourcePath) && !File.Exists(backup)) File.Copy(sourcePath, backup, false);
            }
            catch { }
        }

        public static void Log(string text)
        {
            try
            {
                Ensure();
                File.AppendAllText(LogFile, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " " + text + Environment.NewLine, Encoding.UTF8);
            }
            catch { }
        }
    }
}
