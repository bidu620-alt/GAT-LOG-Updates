using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;

namespace GatLogServer;

internal static class AuthService
{
	public static string DataDir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GAT-LOG");

	public static string AuthPath => Path.Combine(DataDir, "native_auth.json");

	public static string AgentSecretPath => Path.Combine(DataDir, "agent_secret.txt");

	public static NativeAuth EnsureAuth()
	{
		Directory.CreateDirectory(DataDir);
		try
		{
			if (File.Exists(AuthPath))
			{
				NativeAuth nativeAuth = JsonConvert.DeserializeObject<NativeAuth>(File.ReadAllText(AuthPath, Encoding.UTF8));
				if (nativeAuth != null && !string.IsNullOrWhiteSpace(nativeAuth.User) && !string.IsNullOrWhiteSpace(nativeAuth.Hash))
				{
					return nativeAuth;
				}
			}
		}
		catch
		{
		}
		NativeAuth nativeAuth2 = NewAuth("gatlog", "gatlog");
		Save(nativeAuth2);
		return nativeAuth2;
	}

	public static bool Verify(string user, string password)
	{
		NativeAuth nativeAuth = EnsureAuth();
		if (string.Equals((user ?? "").Trim(), (nativeAuth.User ?? "").Trim(), StringComparison.OrdinalIgnoreCase))
		{
			return string.Equals(PasswordHash(password ?? "", nativeAuth.Salt ?? ""), nativeAuth.Hash ?? "", StringComparison.OrdinalIgnoreCase);
		}
		return false;
	}

	public static void Change(string user, string password)
	{
		user = (user ?? "").Trim();
		if (user.Length == 0)
		{
			throw new InvalidOperationException("Usuário vazio.");
		}
		if ((password ?? "").Length < 4)
		{
			throw new InvalidOperationException("A senha precisa ter pelo menos 4 caracteres.");
		}
		Save(NewAuth(user, password));
	}

	private static NativeAuth NewAuth(string user, string password)
	{
		byte[] array = new byte[16];
		using (RandomNumberGenerator randomNumberGenerator = RandomNumberGenerator.Create())
		{
			randomNumberGenerator.GetBytes(array);
		}
		string salt = string.Concat(array.Select((byte b) => b.ToString("x2")));
		return new NativeAuth
		{
			User = user,
			Salt = salt,
			Hash = PasswordHash(password, salt),
			UpdatedAt = DateTimeOffset.Now.ToString("o")
		};
	}

	private static void Save(NativeAuth auth)
	{
		Directory.CreateDirectory(DataDir);
		string text = AuthPath + ".tmp";
		File.WriteAllText(text, JsonConvert.SerializeObject((object)auth, (Formatting)1), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
		if (File.Exists(AuthPath))
		{
			File.Delete(AuthPath);
		}
		File.Move(text, AuthPath);
	}

	private static string PasswordHash(string password, string salt)
	{
		byte[] array = Encoding.UTF8.GetBytes(salt + "\0" + password);
		using (SHA256 sHA = SHA256.Create())
		{
			for (int i = 0; i < 120000; i++)
			{
				array = sHA.ComputeHash(array);
			}
		}
		return string.Concat(array.Select((byte b) => b.ToString("x2")));
	}
}
