#!/usr/bin/env python3
"""Apply authoritative game names/IDs after the Wiki updater. No guessed translations."""
import json, re, unicodedata
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
CATALOG=ROOT/'docs/ets2-official-cargos.json'
REGISTRY=ROOT/'scripts/ets2-game-cargo-names.json'
def norm(s):
    return re.sub(r'[^a-z0-9]+',' ',''.join(c for c in unicodedata.normalize('NFD',str(s or '')) if unicodedata.category(c)!='Mn').lower()).strip()
def apply(data,registry):
    cargos=registry['cargos'];matched=set()
    for category,rows in data['categories'].items():
        for row in rows:
            candidates=[c for c in cargos if (norm(row['name']) in {norm(n) for n in c['catalog_names']} or norm(row['name'])==norm(c['name_en'])) and (not c.get('catalog_dlc') or c['catalog_dlc']==row.get('dlc'))]
            if not candidates:
                # A Wiki update must not silently discard a previously verified mapping.
                candidates=[c for c in cargos if c['id'] in row.get('cargo_ids',[])]
            if not candidates:raise ValueError('No verified game ID for catalog row: '+row['name'])
            pts={c['name_pt'] for c in candidates}
            if len(pts)!=1:raise ValueError('Conflicting game translations: '+row['name'])
            old=row.get('name_pt');ids=sorted({c['id'] for c in candidates})
            aliases=set(row.get('aliases',[]))|{row['name']}|{c['name_en'] for c in candidates}
            for c in candidates:aliases.update(c.get('aliases',[]))
            if old:aliases.add(old)
            row.update(name_pt=next(iter(pts)),cargo_ids=ids,aliases=sorted(aliases-{next(iter(pts))}),name_source='ets2_locale_pt_br')
            matched.update(ids)
            if row['name']=='Mower Conditioner Krone BiG M 45':row['name']='Mower Conditioner Krone BiG M 450'
    # Installed official types absent from the Wiki-based catalog, not event variants.
    missing={'art_dumper':'heavy_machine','tank':'industrial','wagon':'industrial'}
    for c in cargos:
        if c['id'] not in missing or c['id'] in matched:continue
        data['categories'][missing[c['id']]].append({'name':c['name_en'],'name_pt':c['name_pt'],'cargo_ids':[c['id']],'aliases':[c['name_en']],'dlc':'Special Transport','weight':str(c['mass_definition_kg']/1000).rstrip('0').rstrip('.'),'name_source':'ets2_locale_pt_br'})
        matched.add(c['id'])
    # Identical game IDs are a single cargo, even when Wiki lists geography twice.
    for category,rows in data['categories'].items():
        unique={}
        for row in rows:
            key=tuple(row['cargo_ids'])
            if key not in unique:unique[key]=row;continue
            prior=unique[key]
            variants=prior.setdefault('catalog_variants',[{'dlc':prior['dlc'],'weight':prior['weight']}])
            variant={'dlc':row['dlc'],'weight':row['weight']}
            if variant not in variants:variants.append(variant)
            prior['aliases']=sorted(set(prior['aliases'])|set(row['aliases']))
        data['categories'][category]=list(unique.values())
    uncovered={c['id'] for c in cargos}-matched
    if uncovered:raise ValueError('Uncovered installed IDs: '+', '.join(sorted(uncovered)))
    data.update(name_locale=registry['locale'],name_game_version=registry['game_version'],name_source=registry['source'],cargo_id_count=len(matched),total_entries=sum(map(len,data['categories'].values())))
    data['category_counts']={k:len(v) for k,v in data['categories'].items()}
    data['source_note']='Catálogo com nomes e IDs conferidos nos arquivos oficiais instalados do ETS2; pesos e DLCs de referência preservados da Wiki quando disponíveis.'
    return data
def main():
    data=apply(json.loads(CATALOG.read_text(encoding='utf-8')),json.loads(REGISTRY.read_text(encoding='utf-8')))
    CATALOG.write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print(f"{data['total_entries']} catalog rows; {data['cargo_id_count']} verified game IDs")
if __name__=='__main__':main()
