// Server derives identity from provider fields. Never trusts vehicle_type/brand
// reported by the client as proof of entitlement or of a passenger service.
import {truckBrandKey} from './vehicle-economy-key.js';
const norm=v=>String(v||'').normalize('NFD').replace(/[\u0300-\u036f]/g,'').toLowerCase().replace(/[^a-z0-9]+/g,' ').trim();
const makers=new Set(['volvo','scania','mercedes','daf','man','iveco','renault']);
const bodymakers=new Set(['marcopolo','busscar','comil','neobus','caio','irizar']);
const busPatterns=/\b(bus|coach|onibus|autocarro|paradiso|viaggio|torino|urbanuss|tourismo|travego|intouro|integro|citaro|touring|interlink|irizar|vissta buss|apache vip|g7|g8|9700|9800|9900)\b|\bo\s?(371|400|500)[a-z0-9]*\b|\bk\s?(230|250|270|310|320|340|360|380|400|410|420|440|450|460|480|500)[a-z0-9]*\b|\bb\s?(7|9|10|11|12|13|250|270|290|340|360|380|420|450)(r|m|s|rh)[a-z0-9]*\b/;
const truckPatterns=/\b(fh\s?\d*|fm\s?\d*|fmx|actros|arocs|atego|axor|r\s?\d{3}|s\s?\d{3}|streamline|xf\s?\d*|xg|tgx|tgs|stralis|s way|t high|magnum|premium)\b/;
export function classifyVehicle(raw){
 const t=raw?.truck||{},make=String(t.make||t.manufacturer||t.brand||'').trim(),model=String(t.model||t.modelName||t.model_name||'').trim(),id=String(t.id||t.unitId||t.vehicleId||'').trim();
 const key=truckBrandKey(make),identity=norm([make,model,id].join(' ')),body=bodymakers.has(norm(make))?norm(make):null;
 const bus=!!body||busPatterns.test(identity),truck=truckPatterns.test(identity);
 const chassis=truckBrandKey(String(t.chassisMake||t.chassisManufacturer||''));
 const chassisBrand=bus&&makers.has(chassis)?chassis:null;
 const brand=chassisBrand||key;
 return {make,model,id,vehicle_brand:brand,vehicle_make:make,vehicle_model:model,vehicle_type:bus&&!truck?'bus':truck&&!bus?'truck':'unknown',vehicle_id:id,license_plate:String(t.licensePlate||t.license_plate||t.plate||'').trim(),body_brand:body,chassis_brand:chassisBrand,identity_source:'server_provider_catalog_v42'};
}
export function passengerBrandAccess(sql,user,vehicle,at){
 const brand=vehicle.vehicle_brand;
 const owned=sql.prepare('SELECT brand_key,acquired_at FROM eco_brands WHERE user=? AND brand_key=?').get(user,brand);
 const legacy=vehicle.body_brand&&!vehicle.chassis_brand?sql.prepare("SELECT brand_key,acquired_at FROM eco_brands WHERE user=? AND brand_key='onibus'").get(user):null;
 const entitlement=owned||legacy;
 if(!entitlement||Date.parse(entitlement.acquired_at)>Date.parse(at))return {ok:false,reason:'brand_not_owned',brand};
 return {ok:true,brand,entitlement_key:entitlement.brand_key,legacy:!owned};
}
