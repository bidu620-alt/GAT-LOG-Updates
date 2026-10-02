(()=>{
'use strict';
const first=(obj,...paths)=>{
  for(const path of paths){
    let v=obj;
    for(const p of path.split('.')){ if(v==null) break; v=v[p]; }
    if(v!==undefined && v!==null) return v;
  }
};
const num=v=>{v=Number(v);return Number.isFinite(v)?v:0};
const clamp=(v,a,b)=>Math.max(a,Math.min(b,v));
const damage=v=>{let x=num(v);if(x<=1.001)x*=100;return clamp(x,0,100)};
const gear=t=>{
  const g=num(first(t,'truck.displayedGear','Truck.DisplayedGear'));
  return g<0?'R'+Math.abs(g):g===0?'N':'D'+g;
};
window.RoadLifeTelemetry={
  normalize(t){
    const fuel=num(first(t,'truck.fuel','Truck.Fuel'));
    const fuelCapacity=num(first(t,'truck.fuelCapacity','Truck.FuelCapacity'));
    const wear=['Engine','Transmission','Cabin','Chassis','Wheels']
      .map(x=>damage(first(t,'truck.wear'+x,'Truck.Wear'+x)));
    return {
      connected:!!first(t,'game.connected','Game.Connected'),
      speed:Math.abs(num(first(t,'truck.speed','Truck.Speed'))),
      speedLimit:num(first(t,'navigation.speedLimit','Navigation.SpeedLimit')),
      gear:gear(t),
      temperature:num(first(t,'truck.waterTemperature','Truck.WaterTemperature')),
      cruiseOn:!!first(t,'truck.cruiseControlOn','Truck.CruiseControlOn'),
      cruiseSpeed:num(first(t,'truck.cruiseControlSpeed','Truck.CruiseControlSpeed')),
      source:first(t,'job.sourceCity','Job.SourceCity')||'—',
      destination:first(t,'job.destinationCity','Job.DestinationCity')||'—',
      cargo:first(t,'job.cargo','Job.Cargo')||'Sem carga',
      cargoId:first(t,'job.cargoId','Job.CargoId','job.cargo_id')||'',
      cargoMass:num(first(t,'job.cargoMass','Job.CargoMass')),
      remainingKm:num(first(t,'navigation.estimatedDistance','Navigation.EstimatedDistance'))/1000,
      tripKm:num(first(t,'job.plannedDistanceKm','Job.PlannedDistanceKm','job.distanceKm','Job.DistanceKm')),
      eta:first(t,'navigation.estimatedTime','Navigation.EstimatedTime'),
      fuel,
      fuelCapacity,
      fuelPct:fuelCapacity>0?clamp(fuel/fuelCapacity*100,0,100):0,
      truckMake:first(t,'truck.make','Truck.Make','truck.makeName','Truck.MakeName')||'',
      truckModel:first(t,'truck.model','Truck.Model','truck.modelName','Truck.ModelName')||'',
      truckDamage:Math.max(...wear),
      trailerDamage:damage(first(t,'trailer.wear','Trailer.Wear','trailer.damage','Trailer.Damage')),
      cargoDamage:damage(first(t,'job.cargoDamage','Job.CargoDamage','gameplay.jobDelivered.cargoDamage'))
    };
  }
};
})();