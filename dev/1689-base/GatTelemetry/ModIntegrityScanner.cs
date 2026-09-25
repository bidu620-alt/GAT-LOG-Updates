using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace GatTelemetry;

internal static class ModIntegrityScanner
{
	private static readonly object Sync = new object();

	private static DateTime _lastCheck = DateTime.MinValue;

	private static ModIntegrityResult _last = new ModIntegrityResult();

	private static readonly string[] SuspiciousPhrases = new string[21]
	{
		"no damage", "zero damage", "0 damage", "damage 0", "damage disabled", "disable damage", "no cargo damage", "cargo no damage", "zero cargo damage", "no trailer damage",
		"trailer no damage", "no truck damage", "truck no damage", "zero truck damage", "no wear", "zero wear", "sem dano", "dano zero", "sem danos", "indestructible",
		"invincible truck"
	};

	public static ModIntegrityResult Check()
	{
		lock (Sync)
		{
			if ((DateTime.UtcNow - _lastCheck).TotalSeconds < 12.0 && _last != null)
			{
				return Clone(_last);
			}
			_lastCheck = DateTime.UtcNow;
			_last = ScanNow();
			return Clone(_last);
		}
	}

	private static ModIntegrityResult ScanNow()
	{
		ModIntegrityResult modIntegrityResult = new ModIntegrityResult
		{
			CheckedAt = DateTime.UtcNow.ToString("o")
		};
		try
		{
			string folderPath = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
			string path = Path.Combine(Path.Combine(folderPath, "Euro Truck Simulator 2"), "game.log.txt");
			if (!File.Exists(path))
			{
				modIntegrityResult.Status = "unknown";
				modIntegrityResult.Reason = "game_log_missing";
				return modIntegrityResult;
			}
			List<string> list = new List<string>();
			List<string> list2 = new List<string>();
			foreach (string item in File.ReadLines(path))
			{
				if (string.IsNullOrWhiteSpace(item))
				{
					continue;
				}
				string text = item.Trim();
				string text2 = text.ToLowerInvariant();
				if (!text2.Contains("[mod") && !text2.Contains("mod_package") && !text2.Contains("mod package") && !text2.Contains("workshop") && !text2.Contains(".scs") && !text2.Contains("active mod") && !text2.Contains("loaded mod") && !text2.Contains("mounting mod") && !text2.Contains("mod manager"))
				{
					continue;
				}
				if (list2.Count < 250)
				{
					list2.Add(Sanitize(text, folderPath));
				}
				string[] suspiciousPhrases = SuspiciousPhrases;
				foreach (string value in suspiciousPhrases)
				{
					if (text2.Contains(value))
					{
						string safe = Sanitize(text, folderPath);
						if (!list.Any((string x) => string.Equals(x, safe, StringComparison.OrdinalIgnoreCase)))
						{
							list.Add(safe);
						}
						break;
					}
				}
				if (list.Count >= 10)
				{
					break;
				}
			}
			modIntegrityResult.EvidenceHash = HashLines(list2);
			if (list.Count > 0)
			{
				modIntegrityResult.Status = "blocked";
				modIntegrityResult.Reason = "damage_mod_detected";
				modIntegrityResult.Matches = list.ToArray();
			}
			else
			{
				modIntegrityResult.Status = "ok";
				modIntegrityResult.Reason = "active_mod_log_scanned";
				modIntegrityResult.Matches = new string[0];
			}
		}
		catch (Exception ex)
		{
			modIntegrityResult.Status = "unknown";
			modIntegrityResult.Reason = "scan_failed:" + ex.GetType().Name;
			modIntegrityResult.Matches = new string[0];
		}
		return modIntegrityResult;
	}

	private static string Sanitize(string line, string docs)
	{
		string text = line ?? string.Empty;
		if (!string.IsNullOrWhiteSpace(docs))
		{
			text = text.Replace(docs, "%DOCUMENTS%");
		}
		if (text.Length > 180)
		{
			text = text.Substring(0, 180);
		}
		return text;
	}

	private static string HashLines(List<string> lines)
	{
		try
		{
			string s = string.Join("\n", lines ?? new List<string>());
			using SHA256 sHA = SHA256.Create();
			return BitConverter.ToString(sHA.ComputeHash(Encoding.UTF8.GetBytes(s))).Replace("-", string.Empty).ToLowerInvariant();
		}
		catch
		{
			return string.Empty;
		}
	}

	private static ModIntegrityResult Clone(ModIntegrityResult x)
	{
		if (x == null)
		{
			return new ModIntegrityResult();
		}
		return new ModIntegrityResult
		{
			Status = x.Status,
			Reason = x.Reason,
			Matches = ((x.Matches == null) ? new string[0] : x.Matches.ToArray()),
			EvidenceHash = x.EvidenceHash,
			CheckedAt = x.CheckedAt
		};
	}
}
