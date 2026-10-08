(()=>{
  const GREEK_LATIN={α:'a',β:'b',γ:'g',δ:'d',ε:'e',ζ:'z',η:'i',θ:'th',ι:'i',κ:'k',λ:'l',μ:'m',ν:'n',ξ:'x',ο:'o',π:'p',ρ:'r',σ:'s',ς:'s',τ:'t',υ:'y',φ:'f',χ:'ch',ψ:'ps',ω:'o'};
  const norm=v=>String(v||'').normalize('NFD').replace(/[\u0300-\u036f]/g,'').toLowerCase().replace(/[α-ως]/g,c=>GREEK_LATIN[c]||c).replace(/[^a-z0-9]+/g,' ').trim();
  const compact=v=>norm(v).replace(/[^a-z0-9]/g,'');
  // Aliases reutilizáveis do catálogo oficial de cargas (normalizados por nome/ID).
  const CATALOG_ALIASES={hospitalwaste:['Lixo hospitalar'],hwaste:['Lixo hospitalar'],scrapmetals:['Sucata metálica','Sucata de Metal','Sucata de metal'],arsenic:['Arsênico'],beef:['Carne'],metalcentring:['Centração de metal'],crawlertractor:['Esteira de trator'],potatoes:['BATATA'],windturbinenacelle:['Nacele de Turbina Aeólica'],lpg:['Gas de Cozinha','Gás de cozinha'],asphaltmilleram0880:['Moedor de asfalto'],petfood:['Ração'],industrialcablereel:['Rolo de cabo industrial'],wagonkronegx520:['Vagão Krone GX 520'],squarebalerkronebigpack1290hdpvc:['Enfardadeira quadradas Krone BiG Pack 1290 HDC VC'],fertilizerspreader:['Adubadora'],metalcoil:['Bobina de metal'],metalbeams:['Vigas de metal'],fertilizer:['Adubo'],woodchipper:['Picador de madeira'],windturbinetower:['Torre de Turbina Aeólica'],tractors:['TRATOR']};
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
      {official:'Scrap Metals',label:'Metais de sucata',ids:['scrap_metals'],aliases:['Sucata metálica']},
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
      {official:'Yoghurt',label:'Iogurte',origin:'Budapeste',origin_aliases:['Budapest'],destination:'Tessalônica',destination_aliases:['Thessaloniki','Θεσσαλονίκη'],ids:['yoghurt','yogurt'],aliases:['Iogurte','Yogurt','Yoghurt']}
    ]
  },{
  "id": "special_cargo_2026",
  "name": "Carga Especial — Rei das Escoltas",
  "title": "Carga Especial — Rei das Escoltas",
  "medal": "👑",
  "achievement_enabled": true,
      "goal": 10,
  "min_km": 1000,
  "xp_per_cargo": 1000,
  "completion_xp": 5000,
  "completion_title": "Rei das Escoltas",
  "completion_description": "Você não apenas dirige caminhões, você comanda o trânsito e as rodovias.",
  "cargos": [
    {
      "official": "Escadaria para Construção",
      "label": "Escadaria para Construção",
      "ids": [
        "lattice"
      ],
      "min_km": 1000,
      "xp": 1000
    },
    {
      "official": "Peça de Caldeira",
      "label": "Peça de Caldeira",
      "ids": [
        "boiler_parts"
      ],
      "min_km": 1000,
      "xp": 1000
    },
    {
      "official": "Balde para Escavadeira",
      "label": "Balde para Escavadeira",
      "ids": [
        "ex_bucket"
      ],
      "min_km": 1000,
      "xp": 1000
    },
    {
      "official": "Silo Gigante",
      "label": "Silo Gigante",
      "ids": [
        "silo"
      ],
      "min_km": 1000,
      "xp": 1000
    },
    {
      "official": "Trocador de Calor",
      "label": "Trocador de Calor",
      "ids": [
        "heat_exch"
      ],
      "min_km": 1000,
      "xp": 1000
    },
    {
      "official": "Peça gigante secreta",
      "label": "Peça gigante secreta",
      "ids": [
        "mystery_box"
      ],
      "min_km": 1000,
      "xp": 1000
    },
    {
      "official": "Condensador Industrial",
      "label": "Condensador Industrial",
      "ids": [
        "condensator"
      ],
      "min_km": 1000,
      "xp": 1000
    },
    {
      "official": "Dispositivo high-tech",
      "label": "Dispositivo de Alta Tecnologia",
      "ids": [
        "mystery_cyl"
      ],
      "min_km": 1000,
      "xp": 1000
    },
    {
      "official": "Pneus Gigantes",
      "label": "Pneus Gigantes",
      "ids": [
        "dumper_tire"
      ],
      "min_km": 1000,
      "xp": 1000
    },
    {
      "official": "Chassis de Caminhão de Mineração",
      "label": "Chassi de Caminhão",
      "ids": [
        "dumper"
      ],
      "min_km": 1000,
      "xp": 1000
    }
  ]
},
{
  "id": "energia_total_2026",
  "name": "Operação Energia Total",
  "title": "Operação Energia Total — Refinaria e Combustíveis",
  "medal": "⚡",
  "achievement_enabled": true,
      "goal": 20,
  "min_km": 1000,
  "xp_per_cargo": 500,
  "completion_xp": 5000,
  "completion_title": "Operação Energia Total",
  "cargos": [
    {
      "official": "Diesel",
      "label": "Diesel",
      "ids": [
        "diesel"
      ],
      "min_km": 1000,
      "xp": 500
    },
    {
      "official": "Petrol",
      "label": "Gasolina",
      "ids": [
        "petrol"
      ],
      "min_km": 1000,
      "xp": 500
    },
    {
      "official": "Kerosene",
      "label": "Querosene",
      "ids": [
        "kerosene"
      ],
      "min_km": 1000,
      "xp": 500
    },
    {
      "official": "Fuel Oil",
      "label": "Óleo combustível",
      "ids": [
        "fuel_oil"
      ],
      "min_km": 1000,
      "xp": 500
    },
    {
      "official": "Fuel Tanker",
      "label": "Caminhão-tanque",
      "ids": [
        "fueltanker"
      ],
      "min_km": 1000,
      "xp": 500
    },
    {
      "official": "LPG",
      "label": "GLP",
      "ids": [
        "lpg",
        "lpg_t"
      ],
      "min_km": 1000,
      "xp": 500
    },
    {
      "official": "Propane",
      "label": "Propano",
      "ids": [
        "propane"
      ],
      "min_km": 1000,
      "xp": 500
    },
    {
      "official": "Acetylene",
      "label": "Acetileno",
      "ids": [
        "acetylene"
      ],
      "min_km": 1000,
      "xp": 500
    },
    {
      "official": "Chemical Sorbent",
      "label": "Solvente químico",
      "ids": [
        "chem_sorb_c",
        "chem_sorbent"
      ],
      "min_km": 1000,
      "xp": 500
    },
    {
      "official": "Chemicals",
      "label": "Químicos",
      "ids": [
        "chemicals"
      ],
      "min_km": 1000,
      "xp": 500
    },
    {
      "official": "Sulphuric Acid",
      "label": "Ácido sulfúrico",
      "ids": [
        "sulfuric",
        "sulfuric_t"
      ],
      "min_km": 1000,
      "xp": 500
    },
    {
      "official": "Hydrochloric Acid",
      "label": "Ácido clorídrico",
      "ids": [
        "hydrochlor"
      ],
      "min_km": 1000,
      "xp": 500
    },
    {
      "official": "Sodium Hydroxide",
      "label": "Hidróxido de sódio",
      "ids": [
        "sodhydro"
      ],
      "min_km": 1000,
      "xp": 500
    },
    {
      "official": "Pesticides",
      "label": "Pesticidas",
      "ids": [
        "pesticide"
      ],
      "min_km": 1000,
      "xp": 500
    },
    {
      "official": "Boric Acid",
      "label": "Ácido bórico",
      "ids": [
        "boric_acid"
      ],
      "min_km": 1000,
      "xp": 500
    },
    {
      "official": "Brake Fluid",
      "label": "Fluído de freio",
      "ids": [
        "brake_fluid"
      ],
      "min_km": 1000,
      "xp": 500
    },
    {
      "official": "Chlorine",
      "label": "Cloro",
      "ids": [
        "chlorine",
        "chlorine_t"
      ],
      "min_km": 1000,
      "xp": 500
    },
    {
      "official": "Anhydrous Ammonia",
      "label": "Amônia anidra",
      "ids": [
        "ammonia"
      ],
      "min_km": 1000,
      "xp": 500
    },
    {
      "official": "Arsenic",
      "label": "Arsênico",
      "ids": [
        "arsenic"
      ],
      "min_km": 1000,
      "xp": 500
    },
    {
      "official": "Hot Chemicals",
      "label": "Produtos químicos quentes",
      "ids": [
        "hchemicals"
      ],
      "min_km": 1000,
      "xp": 500
    }
  ]
}];

  function eventById(id){return EVENTS.find(e=>e.id===id)||null}
  function rowTime(row){const t=Date.parse(row?.delivered_at||row?.completed_at||row?.date||'');return Number.isFinite(t)?t:NaN}
  function catalogAliases(cargo){return [cargo?.official,...(cargo?.ids||[])].flatMap(value=>CATALOG_ALIASES[compact(value)]||[])}
  function cargoMatches(row,cargo){
    const rowNames=[
      row?.cargo,row?.cargo_name,row?.cargo_name_raw,row?.name,row?.title,
      row?.cargo_identity?.cargo_name_raw,row?.cargo_identity?.cargo_name
    ].map(norm).filter(Boolean);
    const acceptedNames=[cargo.official,cargo.label,...(cargo.aliases||[]),...catalogAliases(cargo)].map(norm).filter(Boolean);
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
    const start=event.start?Date.parse(event.start):-Infinity,end=event.end?Date.parse(event.end):Infinity;
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
        if(!matched.has(cargo.official)&&cargoMatches(row,cargo)&&(!cargo.min_km||Number(row?.distance_km??row?.distance??row?.km??0)>=cargo.min_km))matched.set(cargo.official,{row,t,cargo});
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
    return EVENTS.filter(e=>e.achievement_enabled!==false).flatMap(e=>{
      const p=progress(profile,e.id);
      const individual=e.xp_per_cargo?(e.cargos||[]).map(c=>({id:'event_'+e.id+'_'+compact(c.official),eventId:e.id,title:c.label,description:'Conclua uma entrega de pelo menos '+(c.min_km||e.min_km||0)+' km.',unlocked:p.matched.has(c.official),medal:'🏅',kind:'event_cargo',progress:p.matched.has(c.official)?1:0,goal:1,xp:c.xp||e.xp_per_cargo})):[];
      return [...individual,{
        id:'event_'+e.id,eventId:e.id,title:e.title||e.name||e.id,
        description:p.completed?'Evento concluído. Meta '+p.goal+'/'+p.goal+' atingida.':'Complete o evento. Progresso: '+p.count+'/'+p.goal+'.',
        unlocked:p.completed,medal:e.medal||'🏆',kind:'event',progress:p.count,goal:p.goal,completedAt:p.completedAt,xp:e.completion_xp||0,rewardTitle:e.completion_title||'',description:e.completion_description||('Complete o evento. Progresso: '+p.count+'/'+p.goal+'.')
      }];
    });
  }
  window.GATEventRegistry={events:EVENTS,eventById,progress,achievementList,norm,compact,cargoMatches};
})();