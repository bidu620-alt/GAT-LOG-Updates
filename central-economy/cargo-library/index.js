import {readFileSync} from 'node:fs';
const read=file=>JSON.parse(readFileSync(new URL(file,import.meta.url),'utf8'));
const freeze=value=>{if(value&&typeof value==='object'){for(const child of Object.values(value))freeze(child);Object.freeze(value);}return value;};
const manifest=freeze(read('./manifest.json'));
const passengerTransport=freeze(read('./coaches-2026.json'));
const categories=new Map(manifest.categories.map(item=>[item.id,freeze(read('./'+item.file))]));
const sourceGroups=new Map(manifest.catalog_groups.map(item=>[item.id,item.file]));
const cargoCategory=new Map();
for(const [id,category]of categories)for(const cargo of category.cargo_ids){if(cargoCategory.has(cargo))throw Error('Ambiguous cargo category: '+cargo);cargoCategory.set(cargo,id);}
const normalize=id=>String(id||'').trim().toLowerCase().replace(/^cargo\./,'');
export function getLibraryMetadata(){return manifest;}
export function listCategories(){return manifest.categories;}
export function getCategory(id){const category=categories.get(id);if(!category)throw new RangeError('Categoria interna desconhecida: '+id);return category;}
export function getCargoIds(id){return getCategory(id).cargo_ids;}
export function classifyCargoId(id){return cargoCategory.get(normalize(id))||null;}
export function matchesCategory(category,id){getCategory(category);return classifyCargoId(id)===category;}
export function getCatalogGroup(id){const file=sourceGroups.get(id);if(!file)throw new RangeError('Grupo de catálogo desconhecido: '+id);return freeze(read('./'+file));}
export function getUnselectedCargoIds(){return freeze(read('./review.json'));}

// Passenger services are not freight IDs; they do not affect cargo classification.
export function getPassengerTransportCatalog(){return passengerTransport;}
export function listPassengerServices(){return passengerTransport.services;}
