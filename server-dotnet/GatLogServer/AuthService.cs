using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;

namespace GatLogServer
{
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
                    var a = JsonConvert.DeserializeObject<NativeAuth>(File.ReadAllText(AuthPath, Encoding.UTF8));
                    if (a != null && !string.IsNullOrWhiteSpace(a.User) && !string.IsNullOrWhiteSpace(a.Hash))
                        return a;
                }
            }
            catch { }

            var auth = NewAuth("gatlog", "gatlog");
            Save(auth);
            return auth;
        }

        public static bool Verify(string user, string password)
        {
            var a = EnsureAuth();
            return string.Equals((user ?? "").Trim(), (a.User ?? "").Trim(), StringComparison.OrdinalIgnoreCase)
                && string.Equals(PasswordHash(password ?? "", a.Salt ?? ""), a.Hash ?? "", StringComparison.OrdinalIgnoreCase);
        }

        public static void Change(string user, string password)
        {
            user = (user ?? "").Trim();
            if (user.Length == 0) throw new InvalidOperationException("Usuário vazio.");
            if ((password ?? "").Length < 4) throw new InvalidOperationException("A senha precisa ter pelo menos 4 caracteres.");
            Save(NewAuth(user, password));
        }

        private static NativeAuth NewAuth(string user, string password)
        {
            var saltBytes = new byte[16];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(saltBytes);
            var salt = string.Concat(saltBytes.Select(b => b.ToString("x2")));
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
            var tmp = AuthPath + ".tmp";
            File.WriteAllText(tmp, JsonConvert.SerializeObject(auth, Formatting.Indented), new UTF8Encoding(false));
            if (File.Exists(AuthPath)) File.Delete(AuthPath);
            File.Move(tmp, AuthPath);
        }

        private static string PasswordHash(string password, string salt)
        {
            byte[] x = Encoding.UTF8.GetBytes(salt + "\0" + password);
            using (var sha = SHA256.Create())
            {
                for (int i = 0; i < 120000; i++)
                    x = sha.ComputeHash(x);
            }
            return string.Concat(x.Select(b => b.ToString("x2")));
        }
    }
}
