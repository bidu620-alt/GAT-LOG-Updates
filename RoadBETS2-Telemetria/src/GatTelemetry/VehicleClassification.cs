using System;using System.Text.RegularExpressions;using Newtonsoft.Json.Linq;
namespace GatTelemetry;
internal static class VehicleClassification {
 internal static JObject Read(JObject raw){
  string make=(string)raw.SelectToken("truck.make")??(string)raw.SelectToken("truck.manufacturer")??"",model=(string)raw.SelectToken("truck.model")??"",id=(string)raw.SelectToken("truck.id")??"";
  string identity=Regex.Replace((make+" "+model+" "+id).ToLowerInvariant().Normalize(System.Text.NormalizationForm.FormD),@"\p{Mn}","");
  bool bus=Regex.IsMatch(identity,@"\b(bus|coach|onibus|autocarro|marcopolo|busscar|comil|neobus|caio|irizar|paradiso|viaggio|torino|urbanuss|tourismo|travego|intouro|integro|citaro|touring|interlink|vissta buss|apache vip|g7|g8|9700|9800|9900)\b|\bo\s?(371|400|500)[a-z0-9]*\b|\bk\s?(230|250|270|310|320|340|360|380|400|410|420|440|450|460|480|500)[a-z0-9]*\b|\bb\s?(7|9|10|11|12|13|250|270|290|340|360|380|420|450)(r|m|s|rh)[a-z0-9]*\b");
  bool truck=Regex.IsMatch(identity,@"\b(fh\s?\d*|fm\s?\d*|fmx|actros|arocs|atego|axor|r\s?\d{3}|s\s?\d{3}|streamline|xf\s?\d*|xg|tgx|tgs|stralis|s way|t high|magnum|premium)\b");
  string chassis=(string)raw.SelectToken("truck.chassisMake")??(string)raw.SelectToken("truck.chassisManufacturer")??"";
  string brand=TruckBrandResolver.Resolve(make),ck=TruckBrandResolver.Resolve(chassis);
  if(bus&&Regex.IsMatch(ck,@"^(volvo|scania|mercedes|daf|man|iveco|renault)$"))brand=ck;
  return new JObject{["vehicle_brand"]=brand,["vehicle_model"]=model,["vehicle_type"]=bus&&!truck?"bus":truck&&!bus?"truck":"unknown",["license_plate"]=(string)raw.SelectToken("truck.licensePlate")??"",["body_brand"]=Regex.IsMatch(make.ToLowerInvariant(),@"^(marcopolo|busscar|comil|neobus|caio|irizar)$")?make.ToLowerInvariant():null,["chassis_brand"]=string.IsNullOrWhiteSpace(chassis)?null:ck};
 }
}
