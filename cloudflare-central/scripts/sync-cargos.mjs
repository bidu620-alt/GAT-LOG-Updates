import { readFile } from 'node:fs/promises';

const token=process.env.CLOUDFLARE_API_TOKEN;
const account=process.env.CLOUDFLARE_ACCOUNT_ID;
const database='609412d2-d3cd-478a-a1d5-f8ba728ed304';
if(!token||!account)throw new Error('Cloudflare deployment credentials are required');

const source=JSON.parse(await readFile(new URL('../../docs/ets2-official-cargos.json',import.meta.url),'utf8'));
const manualSuggestions={
  construction:['Cement','Cimento']
};
const batch=Object.entries(source.categories||{}).map(([id,rows])=>({
  sql:'UPDATE work_catalog SET compatible_cargos_json=? WHERE id=?',
  params:[JSON.stringify([...new Set([...rows.map(row=>row.name).filter(Boolean),...(manualSuggestions[id]||[])])]),id]
}));
const response=await fetch(`https://api.cloudflare.com/client/v4/accounts/${account}/d1/database/${database}/query`,{
  method:'POST',
  headers:{Authorization:`Bearer ${token}`,'Content-Type':'application/json'},
  body:JSON.stringify({batch})
});
const result=await response.json();
if(!response.ok||!result.success||result.result?.some(item=>!item.success))throw new Error(`Cargo sync failed: ${JSON.stringify(result.errors||result)}`);
console.log(`Synchronized ${batch.length} catalog cargo groups.`);
