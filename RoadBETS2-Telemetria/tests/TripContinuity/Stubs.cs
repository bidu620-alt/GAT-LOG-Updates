using System;
using System.IO;

namespace GatTelemetry {
 internal static class ClientStore {
   public static string DataDir { get; set; } = Path.Combine(Path.GetTempPath(),"bidutruck-memory-fixture");
   public static void Ensure() => Directory.CreateDirectory(DataDir);
   public static void Log(string message) { }
 }
 internal sealed class ModIntegrityResult {
   public string Status { get; set; } = "verified";
   public string Reason { get; set; }
   public string[] Matches { get; set; } = Array.Empty<string>();
   public string EvidenceHash { get; set; }
   public string CheckedAt { get; set; }
 }
 internal static class ModIntegrityScanner {
   public static ModIntegrityResult Check() => new ModIntegrityResult();
 }
}
