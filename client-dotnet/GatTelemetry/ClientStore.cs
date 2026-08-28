using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;
using Newtonsoft.Json;

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
                return JsonConvert.DeserializeObject<List<ServerEntry>>(File.ReadAllText(ServersFile, Encoding.UTF8)) ?? new List<ServerEntry>();
            }
            catch
            {
                return new List<ServerEntry>();
            }
        }

        public static void SaveServers(List<ServerEntry> servers)
        {
            Ensure();
            File.WriteAllText(ServersFile, JsonConvert.SerializeObject(servers ?? new List<ServerEntry>(), Formatting.Indented), Encoding.UTF8);
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
                return JsonConvert.DeserializeObject<List<CredentialEntry>>(File.ReadAllText(CredentialsFile, Encoding.UTF8)) ?? new List<CredentialEntry>();
            }
            catch
            {
                return new List<CredentialEntry>();
            }
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
