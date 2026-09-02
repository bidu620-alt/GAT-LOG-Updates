"""Torna a classificação automática conservadora e prioriza regras semânticas fortes."""
from pathlib import Path
import sys

path=Path(sys.argv[1])
worker=path.read_text(encoding='utf-8')
start=worker.find('async function autoClassifyCargo(env,cargo){')
end=worker.find('\nasync function learnCargoAlias',start)
if start<0 or end<0:
    raise RuntimeError('Nao encontrei autoClassifyCargo para endurecer classificacao.')
new=r'''async function autoClassifyCargo(env,cargo){
 const key=norm(cargo);if(!key)return{work:null,confidence:0,suggested_work_id:null};
 const rows=await env.DB.prepare('SELECT id,position,title,category,icon,compatible_cargos_json FROM work_catalog WHERE active=1 ORDER BY position').all(),items=rows.results||[];
 const semanticRule=AUTO_CARGO_RULES.find(rule=>rule.cargo.test(key));
 if(semanticRule){
  const semanticMatches=items.filter(item=>semanticRule.target.test(norm((item.title||'')+' '+(item.category||''))));
  if(semanticMatches.length===1)return{work:semanticMatches[0],confidence:Number(semanticRule.score||0),suggested_work_id:semanticMatches[0].id,source:'automatic'};
 }
 const alias=await env.DB.prepare('SELECT ca.work_id,ca.confidence,ca.source,wc.id,wc.position,wc.title,wc.category,wc.icon,wc.compatible_cargos_json FROM cargo_aliases ca JOIN work_catalog wc ON wc.id=ca.work_id WHERE ca.cargo_key=? AND wc.active=1').bind(key).first();
 if(alias&&(String(alias.source||'')==='manual'||Number(alias.confidence||0)>=.85))return{work:alias,confidence:Math.max(.99,Number(alias.confidence)||0),suggested_work_id:alias.id,source:'learned'};
 const ranked=items.map(item=>({item,score:catalogCargoScore(item,cargo)})).sort((a,b)=>b.score-a.score||Number(a.item.position)-Number(b.item.position));
 const first=ranked[0]||{item:null,score:0};
 const safe=first.item&&first.score>=.85;
 return{work:safe?first.item:null,confidence:Number(first.score||0),suggested_work_id:first.item?.id||null,source:safe?'automatic':'pending'};
}'''
worker=worker[:start]+new+worker[end:]
worker=worker.replace("{cargo:/\\b(trator|tractor|escavadeira|excavator|bulldozer|dozer|locomotiva|locomotive|guindaste|crane|carregadeira|loader|colheitadeira|harvester)\\b/,target:/(pesad|maquin|equip)/,score:.88},","{cargo:/\\b(trator|tractor|escavadeira|excavator|bulldozer|dozer|locomotiva|locomotive|guindaste|crane|carregadeira|loader|colheitadeira|harvester|empilhadeira|forklift)\\b/,target:/(pesad|maquin|equip)/,score:.88},")
worker=worker.replace("{cargo:/\\b(carro|carros|automovel|automoveis|cars?|motocicleta|motorcycle|veiculo|vehicle)\\b/,target:/(veicul|automov|carro)/,score:.86},","{cargo:/\\b(carro|carros|automovel|automoveis|cars?|motocicleta|motorcycle|veiculo|vehicle|suvs?|sedans?|hatchbacks?)\\b/,target:/(veicul|automov|carro)/,score:.90},")
for required in ["first.score>=.85","ca.source","suvs?","empilhadeira|forklift","semanticMatches"]:
    if required not in worker: raise RuntimeError('Patch de seguranca incompleto: '+required)
path.write_text(worker,encoding='utf-8')
print('Auto classification safety applied:',path)
