import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
import {getLibraryMetadata,listCategories,getCategory,getCargoIds,classifyCargoId,matchesCategory,getCatalogGroup,getUnselectedCargoIds} from './index.js';
const manifest=getLibraryMetadata();
assert.equal(listCategories().length,8);
const byId=new Map();let entries=0;
for(const group of manifest.catalog_groups){const items=getCatalogGroup(group.id).entries;entries+=items.length;for(const item of items)for(const id of item.cargo_ids||[]){assert(!byId.has(id));byId.set(id,item);}}
assert.equal(entries,380);assert.equal(byId.size,413);
const selected=new Set();for(const category of listCategories()){
 const data=getCategory(category.id);assert.equal(data.entries.length,data.cargo_ids.length);
 assert.equal(data.cargo_ids.length,category.cargo_id_count);
 for(const entry of data.entries){assert(byId.has(entry.cargo_id));assert(!selected.has(entry.cargo_id));selected.add(entry.cargo_id);assert.equal(classifyCargoId(entry.cargo_id),category.id);assert.equal(entry.name_pt,byId.get(entry.cargo_id).name_pt);}
 assert(Object.isFrozen(getCargoIds(category.id)));
}
const unselected=getUnselectedCargoIds().entries;assert.equal(selected.size+unselected.length,byId.size);
for(const entry of unselected){assert(byId.has(entry.cargo_id));assert(!selected.has(entry.cargo_id));assert.equal(classifyCargoId(entry.cargo_id),null);}
for(const [id,category]of Object.entries({logs:'madeireiro',beverages:'carga_seca',beverages_c:'conteineres',truck_batt:'carga_seca',condensator:'maquinario',locomotive:'maquinario',czl_es1000:'maquinario',carrots:'frigorifico',volvo_cars:'cegonheiro',moto_tires:'carga_seca',fertilizer:'agronegocio',gravel:'granel'}))assert.equal(classifyCargoId(id),category,id);
assert(matchesCategory('conteineres',' cargo.BEVERAGES_C '));
assert.equal(classifyCargoId('Bebidas'),null);assert.equal(classifyCargoId('unknown_c'),null);assert.equal(classifyCargoId('diesel'),null);
assert.throws(()=>getCategory('categoria_inexistente'),RangeError);
assert.throws(()=>getCargoIds('madeireiro').push('unknown'),TypeError);
// Snapshot groups remain byte-equivalent at the entry level: no catalog reclassification.
const fixture=JSON.parse(readFileSync(new URL('./manifest.json',import.meta.url),'utf8'));
assert.equal(fixture.counts.catalog_entries,entries);
console.log(JSON.stringify({status:'passed',catalogEntries:entries,cargoIds:byId.size,selected:selected.size,availableForOtherEvents:unselected.length,categories:listCategories().map(c=>({category:c.title,cargoIds:c.cargo_id_count}))}));
