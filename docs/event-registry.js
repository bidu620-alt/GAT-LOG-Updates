(()=>{
  const GREEK_LATIN={α:'a',β:'b',γ:'g',δ:'d',ε:'e',ζ:'z',η:'i',θ:'th',ι:'i',κ:'k',λ:'l',μ:'m',ν:'n',ξ:'x',ο:'o',π:'p',ρ:'r',σ:'s',ς:'s',τ:'t',υ:'y',φ:'f',χ:'ch',ψ:'ps',ω:'o'};
  const norm=v=>String(v||'').normalize('NFD').replace(/[\u0300-\u036f]/g,'').toLowerCase().replace(/[α-ως]/g,c=>GREEK_LATIN[c]||c).replace(/[^a-z0-9]+/g,' ').trim();
  const compact=v=>norm(v).replace(/[^a-z0-9]/g,'');
  function idVariants(v){
    const raw=compact(v);if(!raw)return [];
    const out=new Set([raw]);
    for(const prefix of ['jobcargo','cargodef','scscargo','cargoid','cargo']){
      if(raw.startsWith(prefix)&&raw.length>prefix.length+2)out.add(raw.slice(prefix.length));
    }
    return [...out];
  }
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
      {official:'Wood Shavings',label:'Cavacos de madeira',ids:['wshavings'],aliases:['Casca de madeira','Aparas de madeira']},
      {official:'Potatoes',label:'Batatas',ids:['potatoes']},
      {official:'Sugar',label:'Açúcar',ids:['sugar']},
      {official:'Beef',label:'Carne bovina',ids:['beef_meat','beef'],aliases:['Carne']},
      {official:'Diesel',label:'Combustível diesel',ids:['diesel']},
      {official:'Petrol',label:'Gasolina',ids:['petrol','gasoline']},
      {official:'Kerosene',label:'Querosene',ids:['kerosene']},
      {official:'LPG',label:'GLP',ids:['lpg']},
      {official:'Fuel Tanker',label:'Tanque de combustível',ids:['fueltanker','fuel_tanker','tanker','fuel_tanks'],aliases:['Caminhão-tanque de combustível','Tanques de combustível']},
      {official:'Live Pigs',label:'Porcos vivos',ids:['live_pigs','pigs'],aliases:['Porcos Vivos']},
      {official:'Hot Chemicals',label:'Produtos químicos quentes',ids:['hchemicals','hot_chemicals','hot_chem'],aliases:['Produtos químicos aquecidos']},
      {official:'Acid',label:'Ácido',ids:['acid']},
      {official:'Arsenic',label:'Arsênico',ids:['arsenic'],aliases:['Arsênio']},
      {official:'Chlorine',label:'Cloro',ids:['chlorine']},
      {official:'Hydrochloric Acid',label:'Ácido clorídrico',ids:['hydrochlor','hydrochloric_acid']},
      {official:'Pesticides',label:'Pesticidas',ids:['pesticide','pesticides']},
      {official:'Sulphuric Acid',label:'Ácido sulfúrico',ids:['sulfuric','sulphuric_acid']},
      {official:'Hospital Waste',label:'Resíduos hospitalares',ids:['hwaste','hospital_waste']},
      {official:'Scrap Metals',label:'Sucata metálica',ids:['scrap_metals']},
      {official:'Used Car Batteries',label:'Baterias usadas',ids:['used_battery','used_car_batteries','batteries'],aliases:['Baterias automotivas usadas']},
      {official:'Cement',label:'Cimento',ids:['cement']},
      {official:'Iron Pipes (large)',label:'Tubos de ferro',ids:['iron_pipes']},
      {official:'Glass Panels',label:'Painéis de vidro',ids:['glass','glass_panels']},
      {official:'Coal',label:'Carvão',ids:['coal']},
      {official:'Ore',label:'Minério',ids:['ore']},
      {official:'Low Bed Semi-trailers',label:'Semirreboques prancha baixa',ids:['overweight','low_bed_semi_trailers']},
      {official:'Excavator',label:'Escavadeira',ids:['excavator']}
    ]
  },{
    id:'children_2026', name:'Dia das Crianças 2026', title:'Dia das Crianças 2026', medal:'🎈', achievement_enabled:true,
    start:'2026-10-05T00:00:00-03:00', end:'2026-10-13T00:00:00-03:00', goal:6, sequence:true,
    approved_steps:{gensey:[{index:3,reason:'Etapa 4 aprovada: entrega realizada enquanto a Central estava indisponível e não foi recebida pela Central.'}]},
    cargos:[
      {official:'Toys',label:'Brinquedos',origin:'Porto',destination:'Barcelona',ids:['toys'],aliases:['Brinquedos','Toy']},
      {official:'Chocolate',label:'Chocolate',origin:'Barcelona',destination:'Gênova',destination_aliases:['Genova'],ids:['chocolate'],aliases:['Chocolate']},
      {official:'Chewing Gum',label:'Chicletes',origin:'Gênova',origin_aliases:['Genova'],destination:'Viena',destination_aliases:['Wien'],ids:['chewing_gums','chewing_gum','chewinggum'],aliases:['Chicletes','Chewing Gum']},
      {official:'Milk',label:'Leite',origin:'Viena',origin_aliases:['Wien'],destination:'Varsóvia',destination_aliases:['Warszawa'],ids:['milk'],aliases:['Leite','Milk']},
      {official:'Clothes',label:'Roupas',origin:'Varsóvia',origin_aliases:['Warszawa'],destination:'Budapeste',destination_aliases:['Budapest'],ids:['clothes'],aliases:['Roupas','Clothes']},
      {official:'Yoghurt',label:'Iogurte',origin:'Budapeste',destination:'Tessalônica',destination_aliases:['Thessaloniki','Θεσσαλονίκη'],ids:['yoghurt','yogurt'],aliases:['Iogurte','Yogurt','Yoghurt']}
    ]
  }];

  function eventById(id){return EVENTS.find(e=>e.id===id)||null}
  function rowTime(row){const t=Date.parse(row?.delivered_at||row?.completed_at||row?.date||'');return Number.isFinite(t)?t:NaN}
  function cargoMatches(row,cargo){
    const rowNames=[
      row?.cargo,row?.cargo_name,row?.cargo_name_raw,row?.name,row?.title,
      row?.cargo_identity?.cargo_name_raw,row?.cargo_identity?.cargo_name
    ].map(norm).filter(Boolean);
    const acceptedNames=[cargo.official,cargo.label,...(cargo.aliases||[])].map(norm).filter(Boolean);
    if(acceptedNames.some(x=>rowNames.includes(x)))return true;

    const rowIds=[
      row?.cargo_id,row?.cargoId,row?.catalog_id,
      row?.cargo_identity?.cargo_id_raw,row?.cargo_identity?.cargo_id,
      row?.mission?.cargo_id
    ].flatMap(idVariants);
    const acceptedIds=(cargo.ids||[]).flatMap(idVariants);
    return acceptedIds.some(x=>rowIds.includes(x));
  }
  function routeMatches(row,cargo){
    const src=norm(row?.source||row?.source_city||row?.origin||row?.origin_city||'');
    const dst=norm(row?.destination||row?.destination_city||'');
    const srcAccepted=[cargo.origin,...(cargo.origin_aliases||[])].map(norm).filter(Boolean);
    const dstAccepted=[cargo.destination,...(cargo.destination_aliases||[])].map(norm).filter(Boolean);
    return srcAccepted.includes(src)&&dstAccepted.includes(dst);
  }
  function progress(profile,eventId){
    const event=eventById(eventId);if(!event)return {count:0,goal:0,completed:false,completedAt:null,matched:new Map()};
    const start=Date.parse(event.start),end=Date.parse(event.end);
    const rows=Array.isArray(profile?.deliveries)?profile.deliveries:(Array.isArray(profile?.cargo_history)?profile.cargo_history:[]);
    const matched=new Map();
    const steps=[];
    const ordered=[...rows].filter(row=>{const t=rowTime(row);return Number.isFinite(t)&&t>=start&&t<end}).sort((a,b)=>rowTime(a)-rowTime(b));
    if(event.sequence){
      let expected=0;
      const user=norm(profile?.user||profile?.driver||profile?.account_user||'');
      const approvals=new Map((Array.isArray(event.approved_steps?.[user])?event.approved_steps[user]:[]).map(item=>[Number(item.index),item]));
      const usedApprovals=[];
      const insertApprovedSteps=()=>{
        while(expected>0&&approvals.has(expected)&&event.cargos?.[expected]){
          const cargo=event.cargos[expected],approval=approvals.get(expected);
          const row={source:cargo.origin||'',destination:cargo.destination||'',manual:true};
          matched.set(cargo.official,{row,t:null,cargo,manual:true});
          steps.push({row,t:null,cargo,manual:true,reason:approval.reason||''});
          usedApprovals.push(approval);expected++;
        }
      };
      for(const row of ordered){
        const cargo=event.cargos?.[expected]; if(!cargo) break;
        if(cargoMatches(row,cargo)&&routeMatches(row,cargo)){
          matched.set(cargo.official,{row,t:rowTime(row),cargo}); steps.push({row,t:rowTime(row),cargo}); expected++;
          insertApprovedSteps();
        }
      }
      const goal=Number(event.goal)||event.cargos?.length||0;
      const count=steps.length,completed=count>=goal;
      const override=event.manual_completions?.[user]||null;
      if(override)return {count:Math.min(Number(override.count)||goal,goal),goal,completed:override.completed!==false,completedAt:override.completedAt?Date.parse(override.completedAt):null,matched,manual:true,reason:override.reason||'',steps,completedSteps:count};
      return {count,goal,completed,completedAt:completed?steps[steps.length-1]?.t:null,matched,manual:usedApprovals.length>0,reason:usedApprovals.map(x=>x.reason).filter(Boolean).join(' '),steps,completedSteps:count};
    }
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
  window.GATEventRegistry={events:EVENTS,eventById,progress,achievementList,norm,compact,cargoMatches};
})();