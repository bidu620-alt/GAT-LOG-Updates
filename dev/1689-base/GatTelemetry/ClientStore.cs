using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GatTelemetry;

internal static class ClientStore
{
	public static string DataDir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GAT Telemetria Cliente");

	public static string ServersFile => Path.Combine(DataDir, "servers.json");

	public static string CredentialsFile => Path.Combine(DataDir, "credentials.json");

	public static string SettingsFile => Path.Combine(DataDir, "client_settings.json");

	public static string AccountFile => Path.Combine(DataDir, "gat_account.json");

	public static string LogFile => Path.Combine(DataDir, "gat_dotnet.log");

	public static void Ensure()
	{
		Directory.CreateDirectory(DataDir);
	}

	private static List<ServerEntry> GetFixedServers()
	{
		return new List<ServerEntry>
		{
			new ServerEntry
			{
				Name = "BIDUZAO - DOUGLAS",
				Endpoint = "https://douglas.tail4577e8.ts.net"
			},
			new ServerEntry
			{
				Name = "JC - JEAN",
				Endpoint = "https://jean-jc.tailf14a00.ts.net"
			}
		};
	}

	public static List<ServerEntry> LoadServers()
	{
		Ensure();
		try
		{
			if (!File.Exists(ServersFile))
			{
				List<ServerEntry> fixedServers = GetFixedServers();
				File.WriteAllText(ServersFile, JsonConvert.SerializeObject(fixedServers, Formatting.Indented), Encoding.UTF8);
				Log("servers.json criado com servidores padrão: " + fixedServers.Count);
				return fixedServers;
			}
			string text = File.ReadAllText(ServersFile, Encoding.UTF8);
			JToken token = JToken.Parse(text);
			List<ServerEntry> list = new List<ServerEntry>();
			CollectServers(token, list);
			list.InsertRange(0, GetFixedServers());
			Dictionary<string, ServerEntry> dictionary = new Dictionary<string, ServerEntry>(StringComparer.OrdinalIgnoreCase);
			foreach (ServerEntry item in list)
			{
				if (item != null && !string.IsNullOrWhiteSpace(item.Endpoint))
				{
					string text2 = NormalizeEndpoint(item.Endpoint);
					if (!string.IsNullOrWhiteSpace(text2))
					{
						dictionary[text2] = new ServerEntry
						{
							Name = (string.IsNullOrWhiteSpace(item.Name) ? "Servidor GAT" : item.Name.Trim()),
							Endpoint = text2
						};
					}
				}
			}
			List<ServerEntry> list2 = dictionary.Values.OrderBy((ServerEntry x) => x.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
			string text3 = JsonConvert.SerializeObject(list2, Formatting.Indented);
			if (!JsonEquivalent(text, text3))
			{
				BackupOnce(ServersFile, "servers.before-dotnet-migration.json");
				File.WriteAllText(ServersFile, text3, Encoding.UTF8);
				Log("servers.json legado normalizado: " + list2.Count + " servidor(es)");
			}
			return list2;
		}
		catch (Exception ex)
		{
			Log("LoadServers falhou: " + ex.Message);
			return new List<ServerEntry>();
		}
	}

	private static void CollectServers(JToken token, List<ServerEntry> output)
	{
		if (token == null || output == null)
		{
			return;
		}
		if (token is JArray jArray)
		{
			{
				foreach (JToken item in jArray)
				{
					CollectServers(item, output);
				}
				return;
			}
		}
		if (token is JObject jObject)
		{
			string text = jObject.Value<string>("endpoint");
			if (!string.IsNullOrWhiteSpace(text))
			{
				output.Add(new ServerEntry
				{
					Name = jObject.Value<string>("name"),
					Endpoint = text
				});
			}
			JToken jToken = jObject["value"] ?? jObject["Value"];
			if (jToken != null)
			{
				CollectServers(jToken, output);
			}
		}
	}

	public static void SaveServers(List<ServerEntry> servers)
	{
		Ensure();
		List<ServerEntry> value = (from g in (from x in servers ?? new List<ServerEntry>()
				where x != null && !string.IsNullOrWhiteSpace(x.Endpoint)
				select new ServerEntry
				{
					Name = (string.IsNullOrWhiteSpace(x.Name) ? "Servidor GAT" : x.Name.Trim()),
					Endpoint = NormalizeEndpoint(x.Endpoint)
				}).GroupBy((ServerEntry x) => x.Endpoint, StringComparer.OrdinalIgnoreCase)
			select g.Last()).ToList();
		File.WriteAllText(ServersFile, JsonConvert.SerializeObject(value, Formatting.Indented), Encoding.UTF8);
	}

	public static ClientSettings LoadSettings()
	{
		Ensure();
		try
		{
			if (!File.Exists(SettingsFile))
			{
				return new ClientSettings();
			}
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
			if (!File.Exists(CredentialsFile))
			{
				return new List<CredentialEntry>();
			}
			JToken token = JToken.Parse(File.ReadAllText(CredentialsFile, Encoding.UTF8));
			List<CredentialEntry> list = new List<CredentialEntry>();
			CollectCredentials(token, list);
			return (from g in list.Where((CredentialEntry x) => x != null && !string.IsNullOrWhiteSpace(x.Endpoint) && !string.IsNullOrWhiteSpace(x.Driver)).GroupBy((CredentialEntry x) => NormalizeEndpoint(x.Endpoint) + "\n" + x.Driver, StringComparer.OrdinalIgnoreCase)
				select g.OrderByDescending((CredentialEntry x) => x.SavedAt).First()).ToList();
		}
		catch (Exception ex)
		{
			Log("LoadCredentials falhou: " + ex.Message);
			return new List<CredentialEntry>();
		}
	}

	private static void CollectCredentials(JToken token, List<CredentialEntry> output)
	{
		if (token == null || output == null)
		{
			return;
		}
		if (token is JArray jArray)
		{
			{
				foreach (JToken item in jArray)
				{
					CollectCredentials(item, output);
				}
				return;
			}
		}
		if (token is JObject jObject)
		{
			string text = jObject.Value<string>("endpoint");
			string text2 = jObject.Value<string>("driver");
			if (!string.IsNullOrWhiteSpace(text) && !string.IsNullOrWhiteSpace(text2))
			{
				output.Add(new CredentialEntry
				{
					Endpoint = NormalizeEndpoint(text),
					Driver = text2,
					Token = jObject.Value<string>("token"),
					SavedAt = jObject.Value<string>("saved_at")
				});
			}
			JToken jToken = jObject["value"] ?? jObject["Value"];
			if (jToken != null)
			{
				CollectCredentials(jToken, output);
			}
		}
	}

	public static CredentialEntry FindCredential(string endpoint, string preferredDriver = null)
	{
		string ep = NormalizeEndpoint(endpoint);
		List<CredentialEntry> source = (from x in LoadCredentials()
			where string.Equals(NormalizeEndpoint(x.Endpoint), ep, StringComparison.OrdinalIgnoreCase)
			select x).ToList();
		if (!string.IsNullOrWhiteSpace(preferredDriver))
		{
			CredentialEntry credentialEntry = source.FirstOrDefault((CredentialEntry x) => string.Equals(x.Driver, preferredDriver, StringComparison.OrdinalIgnoreCase));
			if (credentialEntry != null)
			{
				return credentialEntry;
			}
		}
		return source.OrderByDescending((CredentialEntry x) => x.SavedAt).FirstOrDefault();
	}

	public static string GetPlainToken(CredentialEntry credential)
	{
		if (credential == null || string.IsNullOrWhiteSpace(credential.Token))
		{
			return string.Empty;
		}
		try
		{
			byte[] bytes = ProtectedData.Unprotect(Convert.FromBase64String(credential.Token), null, DataProtectionScope.CurrentUser);
			return Encoding.UTF8.GetString(bytes);
		}
		catch
		{
			return string.Empty;
		}
	}

	public static void SaveCredential(string endpoint, string driver, string token)
	{
		if (!string.IsNullOrWhiteSpace(endpoint) && !string.IsNullOrWhiteSpace(driver) && !string.IsNullOrWhiteSpace(token))
		{
			string ep = NormalizeEndpoint(endpoint);
			List<CredentialEntry> list = LoadCredentials();
			list.RemoveAll((CredentialEntry x) => string.Equals(NormalizeEndpoint(x.Endpoint), ep, StringComparison.OrdinalIgnoreCase) && string.Equals(x.Driver, driver, StringComparison.OrdinalIgnoreCase));
			byte[] inArray = ProtectedData.Protect(Encoding.UTF8.GetBytes(token), null, DataProtectionScope.CurrentUser);
			list.Add(new CredentialEntry
			{
				Endpoint = ep,
				Driver = driver,
				Token = Convert.ToBase64String(inArray),
				SavedAt = DateTime.UtcNow.ToString("o")
			});
			File.WriteAllText(CredentialsFile, JsonConvert.SerializeObject(list, Formatting.Indented), Encoding.UTF8);
		}
	}

	public static GatAccountCredential LoadAccountCredential()
	{
		Ensure();
		try
		{
			if (!File.Exists(AccountFile))
			{
				return null;
			}
			GatAccountCredential gatAccountCredential = JsonConvert.DeserializeObject<GatAccountCredential>(File.ReadAllText(AccountFile, Encoding.UTF8));
			if (gatAccountCredential == null || string.IsNullOrWhiteSpace(gatAccountCredential.User) || string.IsNullOrWhiteSpace(gatAccountCredential.Token))
			{
				return null;
			}
			byte[] bytes = ProtectedData.Unprotect(Convert.FromBase64String(gatAccountCredential.Token), null, DataProtectionScope.CurrentUser);
			gatAccountCredential.Token = Encoding.UTF8.GetString(bytes);
			return gatAccountCredential;
		}
		catch (Exception ex)
		{
			Log("LoadAccountCredential falhou: " + ex.Message);
			return null;
		}
	}

	public static void SaveAccountCredential(string user, string token)
	{
		Ensure();
		if (!string.IsNullOrWhiteSpace(user) && !string.IsNullOrWhiteSpace(token))
		{
			byte[] inArray = ProtectedData.Protect(Encoding.UTF8.GetBytes(token), null, DataProtectionScope.CurrentUser);
			GatAccountCredential value = new GatAccountCredential
			{
				User = user.Trim(),
				Token = Convert.ToBase64String(inArray),
				SavedAt = DateTime.UtcNow.ToString("o")
			};
			File.WriteAllText(AccountFile, JsonConvert.SerializeObject(value, Formatting.Indented), Encoding.UTF8);
		}
	}

	public static void ClearAccountCredential()
	{
		try
		{
			if (File.Exists(AccountFile))
			{
				File.Delete(AccountFile);
			}
		}
		catch
		{
		}
	}

	public static string GetDeviceId()
	{
		string text = ReadMachineGuid(RegistryView.Registry64) ?? ReadMachineGuid(RegistryView.Registry32);
		if (string.IsNullOrWhiteSpace(text))
		{
			text = Environment.MachineName + "|" + Environment.UserName;
		}
		using SHA256 sHA = SHA256.Create();
		return BitConverter.ToString(sHA.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-", "").ToLowerInvariant();
	}

	private static string ReadMachineGuid(RegistryView view)
	{
		try
		{
			using RegistryKey registryKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
			using RegistryKey registryKey2 = registryKey.OpenSubKey("SOFTWARE\\Microsoft\\Cryptography");
			return registryKey2?.GetValue("MachineGuid")?.ToString();
		}
		catch
		{
			return null;
		}
	}

	public static string NormalizeEndpoint(string endpoint)
	{
		if (string.IsNullOrWhiteSpace(endpoint))
		{
			return string.Empty;
		}
		endpoint = endpoint.Trim().TrimEnd('/');
		if (!endpoint.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !endpoint.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
		{
			endpoint = "https://" + endpoint;
		}
		return endpoint;
	}

	private static bool JsonEquivalent(string a, string b)
	{
		try
		{
			return JToken.DeepEquals(JToken.Parse(a), JToken.Parse(b));
		}
		catch
		{
			return false;
		}
	}

	private static void BackupOnce(string sourcePath, string backupName)
	{
		try
		{
			string text = Path.Combine(DataDir, backupName);
			if (File.Exists(sourcePath) && !File.Exists(text))
			{
				File.Copy(sourcePath, text, overwrite: false);
			}
		}
		catch
		{
		}
	}

	public static void Log(string text)
	{
		try
		{
			Ensure();
			File.AppendAllText(LogFile, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " " + text + Environment.NewLine, Encoding.UTF8);
		}
		catch
		{
		}
	}
}
