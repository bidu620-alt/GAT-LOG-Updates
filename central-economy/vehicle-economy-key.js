const norm=v=>String(v||'').normalize('NFD').replace(/[\u0300-\u036f]/g,'').toLowerCase().replace(/[^a-z0-9]+/g,' ').trim();

// Maintain the same make-first normalization as the BiduTruck client.
// The garage unlocks a manufacturer, not a specific truck model.
export function truckBrandKey(make){
 const n=norm(make);
 if(!n)return '';
 let brand='';
 if(/^(?:scania)(?: |$)/.test(n))brand='scania';
 else if(/^(?:volvo)(?: |$)/.test(n))brand='volvo';
 else if(/^(?:daf)(?: |$)/.test(n))brand='daf';
 else if(/^(?:iveco)(?: |$)/.test(n))brand='iveco';
 else if(/^(?:man)(?: |$)/.test(n))brand='man';
 else if(/^(?:mercedes(?: benz)?|mercedesbenz|mb)(?: |$)/.test(n))brand='mercedes';
 else if(/^(?:renault)(?: |$)/.test(n))brand='renault';
 if(!brand)return n;
 // Reject ambiguous manufacturers instead of granting another brand's license.
 const identities={
  scania:/(?:^| )scania(?: |$)/,
  volvo:/(?:^| )volvo(?: |$)/,
  daf:/(?:^| )daf(?: |$)/,
  iveco:/(?:^| )iveco(?: |$)/,
  man:/(?:^| )man(?: |$)/,
  mercedes:/(?:^| )(?:mercedes|mercedesbenz|mb)(?: |$)/,
  renault:/(?:^| )renault(?: |$)/
 };
 for(const [other,regex] of Object.entries(identities))
  if(other!==brand&&regex.test(n))return n;
 return brand;
}
export function vehicleEconomyKey(raw){
 const t=raw?.truck||{};
 // Vehicle maker can be supplied under different TruckSimGPS keys; do not infer
 // a paid garage license from an untrusted model, mod name or old selected truck.
 const make=norm([t.make,t.manufacturer,t.brand,t.makeName,t.manufacturerName]
  .find(v=>typeof v==='string'&&v.trim())||'');
 const type=norm(t.vehicle_type||t.vehicleType||t.type||raw?.vehicle_type||raw?.vehicle?.type);
 const identity=norm([make,t.model,t.modelName,t.model_name,t.id,t.unitId,t.unit_id,t.vehicleId].filter(Boolean).join(' '));
 const maker=truckBrandKey(make);
 if(/^(daf|iveco|man|mercedes|renault|scania|volvo)$/.test(maker))return maker;
 const bus=/^(bus|coach|onibus|autocarro)$/.test(type)
  ||/\b(bus|coach|onibus|autocarro|marcopolo|busscar|comil|neobus|caio|irizar|paradiso|viaggio|torino|urbanuss)\b/.test(identity)
  ||/\b(vissta\s*buss|apache\s*vip|g[78])\b/.test(identity)
  ||/\bo\s?(371|400|500)[a-z0-9]*\b/.test(identity)
  ||/\bk\s?(230|250|270|310|320|340|360|380|400|410|420|440|450|460|480|500)[a-z0-9]*\b/.test(identity)
  ||/\bb\s?(7|9|10|11|12|13|250|270|290|340|360|380|420|450)(r|m|s|rh)[a-z0-9]*\b/.test(identity);
 return bus?'onibus':maker;
}
