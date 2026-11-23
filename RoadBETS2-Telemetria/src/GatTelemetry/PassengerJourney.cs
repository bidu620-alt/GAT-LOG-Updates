using System;using System.IO;using System.Security.Cryptography;using System.Text;using Newtonsoft.Json.Linq;
namespace GatTelemetry;
// Commands and identity persist independently of the freight receipt journal.
// Only the Central measures and awards a monitored trip; no client reward math.
internal sealed class PassengerJourney {
 private readonly string file;private JObject state;private long generation;private readonly object sync=new object();
 internal string Message {get;private set;}="";
 internal PassengerJourney(string path){file=path;bool found=false;foreach(string candidate in new[]{file,file+".previous"}){if(!File.Exists(candidate))continue;found=true;try{var bytes=ProtectedData.Unprotect(File.ReadAllBytes(candidate),null,DataProtectionScope.CurrentUser);var record=JObject.Parse(Encoding.UTF8.GetString(bytes));long gen=record.Value<long?>("generation")??0;var value=record["state"] as JObject??record;if(state==null||gen>generation){state=value;generation=gen;}}catch{}}if(found&&state==null)throw new InvalidDataException("Estado protegido da viagem não pôde ser recuperado.");}
 // Two authenticated slots keep the previous complete state if power fails
 // halfway through a write. No rename/delete permission or save-game write.
 private void Save(){Directory.CreateDirectory(Path.GetDirectoryName(file));long next=generation+1;var record=new JObject{["generation"]=next,["state"]=state.DeepClone()};byte[] bytes=ProtectedData.Protect(Encoding.UTF8.GetBytes(record.ToString()),null,DataProtectionScope.CurrentUser);using(var stream=new FileStream(next%2==1?file:file+".previous",FileMode.Create,FileAccess.Write,FileShare.Read)){stream.Write(bytes,0,bytes.Length);stream.Flush(true);}generation=next;}
 private bool Active=>state!=null&&state.Value<bool?>("archived")!=true;
 internal JObject Current {get{lock(sync)return !Active?null:(JObject)state.DeepClone();}}
 internal bool CanAutoStart(JObject service){lock(sync)return !Active&&(state?.Value<bool?>("blocked_service")!=true||ServiceKey(state)!=ServiceKey(service));}
 private static string ServiceKey(JObject service)=>string.Join("|",(string)service?["source"],(string)service?["service_id"],(string)service?["origin"],(string)service?["destination"]);
 internal void ObserveInactiveService(JObject raw){lock(sync){if(Active||state?.Value<bool?>("blocked_service")!=true)return;bool inactive=(string)state["source"]=="sdk"&&raw["bus_job"] is JObject b&&b.Value<bool?>("active")==false&&b.Value<bool?>("onJob")!=true;
  var e=raw["gat_save_evidence"] as JObject;if((string)state["source"]=="save"&&e?.Value<bool?>("full_snapshot")==true&&(string)e["profile_id"]==(string)state["profile_id"]&&(string)e["status"]=="snapshot_recent"&&e.SelectToken("jobs.current_bus_job")?.Type==JTokenType.Null)inactive=true;
  if(inactive){state["blocked_service"]=false;Save();}
 }}
 internal void Start(JObject raw,string user,string origin,string destination,string source="manual",string serviceId=""){
  lock(sync){if(Active)throw new InvalidOperationException("Já existe uma viagem de passageiros pendente.");
   if((string)VehicleClassification.Read(raw)["vehicle_type"]!="bus")throw new InvalidOperationException("Ônibus não identificado.");
   if(raw.SelectToken("game.connected")?.Value<bool?>()!=true||raw.SelectToken("game.paused")?.Value<bool?>()==true||(source=="manual"&&Math.Abs(raw.SelectToken("truck.speed")?.Value<double?>()??0)>1))throw new InvalidOperationException("Pare o ônibus com o jogo conectado para iniciar.");
   if(string.IsNullOrWhiteSpace(user)||string.IsNullOrWhiteSpace((string)raw["gat_profile_id"])||string.IsNullOrWhiteSpace(origin)||string.IsNullOrWhiteSpace(destination))throw new InvalidOperationException("Conta, perfil, origem e destino são obrigatórios.");
   state=new JObject{["id"]=Guid.NewGuid().ToString("N"),["user"]=user,["profile_id"]=raw["gat_profile_id"],["vehicle"]=(string)raw.SelectToken("truck.id"),["plate"]=(string)raw.SelectToken("truck.licensePlate"),["origin"]=origin.Trim(),["destination"]=destination.Trim(),["source"]=source,["service_id"]=serviceId,["confirmed"]=true,["action"]="progress",["created_at"]=DateTime.UtcNow.ToString("o")};Save();
  }
 }
 internal void Command(string action){lock(sync){if(!Active)throw new InvalidOperationException("Nenhuma viagem pendente.");if(action!="finish"&&action!="cancel")throw new ArgumentException("Ação inválida.");state["action"]=action;Save();}}
 internal bool Matches(JObject raw,string user){lock(sync)return Active&&string.Equals((string)state["user"],user,StringComparison.OrdinalIgnoreCase)&&string.Equals((string)state["profile_id"],(string)raw?["gat_profile_id"],StringComparison.OrdinalIgnoreCase)&&(string)state["vehicle"]==(string)raw?.SelectToken("truck.id")&&(string)state["plate"]==(string)raw?.SelectToken("truck.licensePlate");}
 internal void Apply(JObject raw,string user){lock(sync){
  if(raw==null||raw["gat_journal_chain"]!=null)return;
  var vehicle=VehicleClassification.Read(raw);foreach(var p in vehicle.Properties())raw[p.Name]=p.Value.DeepClone();
  if(!Active)return;
  if(!string.Equals((string)state["user"],user,StringComparison.OrdinalIgnoreCase)||!string.Equals((string)state["profile_id"],(string)raw["gat_profile_id"],StringComparison.OrdinalIgnoreCase)||(string)state["vehicle"]!=(string)raw.SelectToken("truck.id")||(string)state["plate"]!=(string)raw.SelectToken("truck.licensePlate")){Message="Viagem preservada: volte à mesma conta, perfil e ônibus.";return;}
  raw["gat_passenger_session"]=state.DeepClone();raw["gat_trip_id"]=(string)state["id"];raw["job_latch_key"]=(string)state["id"];raw["gat_job_event_key"]=(string)state["id"];
 }}
 internal void Acknowledge(JObject response){lock(sync){var status=response?["passenger_trip"] as JObject;if(state==null||status==null||(string)status["id"]!=(string)state["id"])return;Message=(string)status["reason"]??("Passageiros: "+(string)status["state"]+" • "+((double?)status["distance_km"]??0).ToString("0.0")+" km validados");
  if((string)status["state"]=="completed"||(string)status["state"]=="cancelled"){state["archived"]=true;state["blocked_service"]=(string)state["source"]!="manual";Save();}else if((string)status["state"]=="active"){state["last_server_distance_km"]=status["distance_km"];Save();}
 }}
}
