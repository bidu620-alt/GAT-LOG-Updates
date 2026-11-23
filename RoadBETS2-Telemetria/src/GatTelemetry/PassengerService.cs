using System;using System.Linq;using Newtonsoft.Json.Linq;
namespace GatTelemetry;
internal static class PassengerService {
 private static bool Active(JObject b)=>b?["active"]?.Type==JTokenType.Boolean&&b.Value<bool>("active")||b?["onJob"]?.Type==JTokenType.Boolean&&b.Value<bool>("onJob");
 private static string Text(JToken token){string s=token?.Type==JTokenType.String?(string)token:"";try{if(s.StartsWith("\""))s=Newtonsoft.Json.JsonConvert.DeserializeObject<string>(s);}catch{return "";}return s.Trim();}
 internal static JObject Read(JObject raw,DateTime now){
  var b=raw["bus_job"] as JObject;
  if(!Active(b))b=((string)raw.SelectToken("job.source_kind")??(string)raw.SelectToken("job.sourceKind"))=="bus_job"?raw["job"] as JObject:null;
  if(Active(b)){
   string a=Text(b["sourceCity"]??b["source_city"]),z=Text(b["destinationCity"]??b["destination_city"]);if(a!=""&&z!="")return new JObject{["source"]="sdk",["origin"]=a,["destination"]=z,["service_id"]=Text(b["id"]??b["jobId"])};
  }
  var e=raw["gat_save_evidence"] as JObject;var job=e?.SelectToken("jobs.current_bus_job") as JObject;
  DateTime modified;
  if(e==null||job==null||(string)e["status"]!="snapshot_recent"||(string)e["profile_id"]!=(string)raw["gat_profile_id"]||(string)e["job_kind"]!="bus_job"||(string)e["vehicle_mode"]!="bus"||string.IsNullOrWhiteSpace((string)e.SelectToken("save.game_sha256"))||(e.Value<double?>("snapshot_age_seconds")??double.MaxValue)>120||e.Value<bool?>("transport_truncated")==true||!DateTime.TryParse((string)e.SelectToken("save.game_modified_at"),null,System.Globalization.DateTimeStyles.RoundtripKind,out modified))return null;
  double age=(now-modified.ToUniversalTime()).TotalSeconds;if(age< -30||age>120)return null;
  var f=job["fields"] as JObject;var refs=f?["stops"] as JArray;var units=e["related_units"] as JArray;
  var cities=(refs??new JArray()).Select(r=>units?.OfType<JObject>().FirstOrDefault(u=>(string)u["id"]==(string)r)).Where(u=>u!=null).Select(u=>Text(u.SelectToken("fields.city"))).Where(s=>s!="").ToArray();
  string origin=Text(f?["source_city"]),destination=Text(f?["target_city"]??f?["destination_city"]);if(origin=="")origin=cities.FirstOrDefault()??"";if(destination=="")destination=cities.LastOrDefault()??"";
  return origin==""||destination==""||origin==destination?null:new JObject{["source"]="save",["origin"]=origin,["destination"]=destination,["service_id"]=Text(job["id"])};
 }
}
