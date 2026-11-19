using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace GatTelemetry;

internal sealed class SaveGameReader
{
    private readonly SemaphoreSlim gate = new SemaphoreSlim(1, 1);
    private DateTime checkedAt = DateTime.MinValue, fullSentAt = DateTime.MinValue;
    private string profile = "", stamp = "", sentHash = "";
    private JObject cached;

    public async Task<JObject> ReadAsync(JObject telemetry)
    {
        await gate.WaitAsync().ConfigureAwait(false);
        try {
            string id = (string)telemetry?["gat_profile_id"] ?? "";
            if (telemetry?.SelectToken("game.connected")?.Value<bool?>() != true ||
                !Regex.IsMatch(id, @"\A(?:[a-fA-F0-9]{2}){1,128}\z"))
                return Unavailable("active_profile_unavailable");
            if (!string.Equals(profile,id,StringComparison.OrdinalIgnoreCase)) {
                profile=id; stamp=""; cached=null; checkedAt=DateTime.MinValue; sentHash="";
            }
            if ((DateTime.UtcNow-checkedAt).TotalSeconds>=3) {
                checkedAt=DateTime.UtcNow;
                string docs=Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                string game=Path.Combine(docs,"Euro Truck Simulator 2");
                var candidates=new[]{"profiles","steam_profiles"}.SelectMany(root => {
                    string saves=Path.Combine(game,root,id,"save");
                    return Directory.Exists(saves)?Directory.GetDirectories(saves).Select(d=>Path.Combine(d,"game.sii")).Where(File.Exists):Enumerable.Empty<string>();
                }).Select(f=>new FileInfo(f)).OrderByDescending(f=>f.LastWriteTimeUtc).ToArray();
                if(candidates.Length==0) { cached=null; return Unavailable("save_not_found"); }
                var file=candidates[0];
                string next=file.FullName+"|"+file.LastWriteTimeUtc.Ticks+"|"+file.Length;
                if(next!=stamp || cached==null) {
                    cached=await DecodeAsync(file.DirectoryName,id).ConfigureAwait(false);
                    if((string)cached["status"]!="unavailable") { stamp=next; try { Directory.CreateDirectory(ClientStore.DataDir); File.WriteAllText(Path.Combine(ClientStore.DataDir,"save-reader-last.json"),cached.ToString()); } catch {} }
                }
            }
            if(cached==null)return Unavailable("save_not_read");
            var evidence=(JObject)cached.DeepClone();
            if((string)evidence["status"]=="unavailable")return evidence;
            DateTime date;
            if(DateTime.TryParse((string)evidence.SelectToken("save.game_modified_at"),null,System.Globalization.DateTimeStyles.RoundtripKind,out date)) {
                double age=(DateTime.UtcNow-date.ToUniversalTime()).TotalSeconds;
                evidence["snapshot_age_seconds"]=Math.Max(0,Math.Round(age));
                evidence["status"]=age>=-30&&age<=120?"snapshot_recent":"snapshot_stale";
            }
            string hash=(string)evidence.SelectToken("save.game_sha256")??"";
            if(hash!=sentHash||(DateTime.UtcNow-fullSentAt).TotalSeconds>=30) {
                sentHash=hash;fullSentAt=DateTime.UtcNow;evidence["full_snapshot"]=true;return evidence;
            }
            return new JObject { ["schema"]=1,["source"]="ets2_save_readonly",["status"]=evidence["status"],
                ["profile_id"]=id,["full_snapshot"]=false,["game_sha256"]=hash,
                ["snapshot_age_seconds"]=evidence["snapshot_age_seconds"],
                ["game_modified_at"]=evidence.SelectToken("save.game_modified_at"),["job_kind"]=evidence["job_kind"] };
        }
        catch(Exception ex) { cached=null;return Unavailable("reader_error:"+ex.GetType().Name); }
        finally {gate.Release();}
    }
    private static JObject Unavailable(string reason) => new JObject {
        ["schema"]=1,["source"]="ets2_save_readonly",["status"]="unavailable",["reason"]=reason
    };
    private static async Task<JObject> DecodeAsync(string folder,string id)
    {
        string root=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"SaveReader");
        string node=Path.Combine(root,"node.exe"),script=Path.Combine(root,"reader.mts");
        if(!File.Exists(node)||!File.Exists(script))return Unavailable("save_reader_components_missing");
        using(var process=new Process()) {
            process.StartInfo=new ProcessStartInfo(node,"--max-old-space-size=384 --disable-warning=ExperimentalWarning --disable-warning=MODULE_TYPELESS_PACKAGE_JSON --experimental-transform-types \""+script+"\" \""+folder+"\" "+id) {
                UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,
                WorkingDirectory=root
            };
            process.Start();
            var stdout=process.StandardOutput.ReadToEndAsync();var stderr=process.StandardError.ReadToEndAsync();
            bool exited=await Task.Run(()=>process.WaitForExit(8000)).ConfigureAwait(false);
            if(!exited){try{process.Kill();}catch{}return Unavailable("save_decode_timeout");}
            string json=await stdout.ConfigureAwait(false);await stderr.ConfigureAwait(false);
            if(System.Text.Encoding.UTF8.GetByteCount(json)>60000)return Unavailable("evidence_too_large");
            return JObject.Parse(json);
        }
    }
}
