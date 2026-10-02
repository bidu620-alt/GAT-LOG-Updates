(()=>{
  const norm=v=>String(v||'').normalize('NFD').replace(/[\u0300-\u036f]/g,'').toLowerCase().replace(/[^a-z0-9]+/g,' ').trim();
  const EVENTS=[{
    id:'halloween_2026',
    name:'Halloween 2026',
    title:'Halloween 2026',
    medal:'🎃',
    achievement_enabled:true,
    start:'2026-09-30T00:00:00-03:00',
    end:'2026-11-01T00:00:00-03:00',
    goal:30,
    manual_completions:{gensey:{count:30,completed:true,reason:'Correção administrativa: evento confirmado como concluído; duas cargas com erro histórico ignoradas.'}},
    cargos:[
      {official:'Dynamite',label:'Dinamite',ids:['dynamite']},
      {official:'Lumber',label:'Madeira serrada',ids:['lumber']},
      {official:'Sawdust Panels',label:'Painéis de serragem',ids:['sawpanels']},
      {official:'Wood Shavings',label:'Cavacos de madeira',ids:['wshavings']},
      {official:'Potatoes',label:'Batatas',ids:['potatoes']},
      {official:'Sugar',label:'Açúcar',ids:['sugar']},
      {official:'Beef',label:'Carne bovina',ids:['beef_meat','beef']},
      {official:'Diesel',label:'Combustível diesel',ids:['diesel']},
      {official:'Petrol',label:'Gasolina',ids:['petrol','gasoline']},
      {official:'Kerosene',label:'Querosene',ids:['kerosene']},
      {official:'LPG',label:'GLP',ids:['lpg']},
      {official:'Fuel Tanker',label:'Tanque de combustível',ids:['fueltanker','fuel_tanker']},
      {official:'Chemicals',label:'Produtos químicos',ids:['chemicals']},
      {official:'Hot Chemicals',label:'Produtos químicos quentes',ids:['hchemicals','hot_chemicals']},
      {official:'Acid',label:'Ácido',ids:['acid']},
      {official:'Arsenic',label:'Arsênico',ids:['arsenic']},
      {official:'Chlorine',label:'Cloro',ids:['chlorine']},
      {official:'Hydrochloric Acid',label:'Ácido clorídrico',ids:['hydrochlor','hydrochloric_acid']},
      {official:'Pesticides',label:'Pesticidas',ids:['pesticide','pesticides']},
      {official:'Sulphuric Acid',label:'Ácido sulfúrico',ids:['sulfuric','sulphuric_acid']},
      {official:'Hospital Waste',label:'Resíduos hospitalares',ids:['hwaste','hospital_waste']},
      {official:'Scrap Metals',label:'Sucata metálica',ids:['scrap_metals']},
      {official:'Used Car Batteries',label:'Baterias usadas',ids:['used_battery','used_car_batteries']},
      {official:'Cement',label:'Cimento',ids:['cement']},
      {official:'Iron Pipes (large)',label:'Tubos de ferro',ids:['iron_pipes']},
      {official:'Glass Panels',label:'Painéis de vidro',ids:['glass','glass_panels']},
      {official:'Coal',label:'Carvão',ids:['coal']},
      {official:'Ore',label:'Minério',ids:['ore']},
      {official:'Low Bed Semi-trailers',label:'Semirreboques prancha baixa',ids:['overweight','low_bed_semi_trailers']},
      {official:'Excavator',label:'Escavadeira',ids:['excavator']}
    ]
  }];

  function eventById(id){return EVENTS.find(e=>e.id===id)||null}
  function rowTime(row){const t=Date.parse(row?.delivered_at||row?.completed_at||row?.date||'');return Number.isFinite(t)?t:NaN}
  function cargoTokens(row){
    return new Set([
      row?.cargo_id,row?.cargoId,row?.cargo_identity?.cargo_id_raw,
      row?.cargo,row?.cargo_name,row?.name,row?.title
    ].map(norm).filter(Boolean));
  }
  function cargoMatches(row,cargo){
    const tokens=cargoTokens(row);
    const accepted=[cargo.official,cargo.label,...(cargo.ids||[]),...(cargo.aliases||[])].map(norm).filter(Boolean);
    return accepted.some(x=>tokens.has(x));
  }
  function progress(profile,eventId){
    const event=eventById(eventId);if(!event)return {count:0,goal:0,completed:false,completedAt:null,matched:new Map()};
    const start=Date.parse(event.start),end=Date.parse(event.end);
    const rows=Array.isArray(profile?.deliveries)?profile.deliveries:(Array.isArray(profile?.cargo_history)?profile.cargo_history:[]);
    const matched=new Map();
    for(const row of rows){
      const t=rowTime(row);if(!Number.isFinite(t)||t<start||t>=end)continue;
      for(const cargo of event.cargos||[]){
        if(!matched.has(cargo.official)&&cargoMatches(row,cargo))matched.set(cargo.official,{row,t,cargo});
      }
    }
    const goal=Number(event.goal)||event.cargos?.length||0;
    const user=norm(profile?.user||profile?.driver||profile?.account_user||'');
    const override=event.manual_completions?.[user]||null;
    if(override){return {count:Math.min(Number(override.count)||goal,goal),goal,completed:override.completed!==false,completedAt:override.completedAt?Date.parse(override.completedAt):null,matched,manual:true,reason:override.reason||''};}
    const count=Math.min(matched.size,goal);
    const completed=goal>0&&count>=goal;
    const completedAt=completed?Math.max(...[...matched.values()].map(x=>x.t)):null;
    return {count,goal,completed,completedAt,matched,manual:false,reason:''};
  }
  function achievementList(profile){
    return EVENTS.filter(e=>e.achievement_enabled!==false).map(e=>{
      const p=progress(profile,e.id);
      return {
        id:'event_'+e.id,eventId:e.id,title:e.title||e.name||e.id,
        description:p.completed?'Evento concluído. Meta '+p.goal+'/'+p.goal+' atingida.':'Complete o evento. Progresso: '+p.count+'/'+p.goal+'.',
        unlocked:p.completed,medal:e.medal||'🏆',kind:'event',progress:p.count,goal:p.goal,completedAt:p.completedAt
      };
    });
  }
  window.GATEventRegistry={events:EVENTS,eventById,progress,achievementList,norm,cargoMatches};
})();