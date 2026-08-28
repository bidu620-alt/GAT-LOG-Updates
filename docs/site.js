const RAW='https://raw.githubusercontent.com/bidu620-alt/GAT-LOG-Updates/main/';

async function bindRelease(manifest, versionId, buttonId){
  const versionEl=document.getElementById(versionId);
  const button=document.getElementById(buttonId);
  try{
    const r=await fetch(RAW+manifest,{cache:'no-store'});
    if(!r.ok) throw new Error('manifest');
    const data=await r.json();
    versionEl.textContent='Versão '+(data.display_version||data.version||'atual');
    const url=data.setup_url||data.url||'';
    if(url){button.href=url;button.classList.remove('disabled');}
  }catch(_){
    versionEl.textContent='Versão disponível no GitHub';
  }
}

bindRelease('server_dotnet_version.json','serverVersion','serverDownload');
bindRelease('client_dotnet_version.json','clientVersion','clientDownload');

const examples=[
  ['Contrato externo • longa distância','500 km','Qualquer carga elegível','24 horas'],
  ['Carga pesada • contrato externo','350 km','Acima de 20 t','24 horas'],
  ['Rota internacional • contrato externo','700 km','Qualquer carga elegível','48 horas'],
  ['Entrega técnica • contrato externo','450 km','Máquinas / equipamentos','24 horas']
];

const jobButton=document.getElementById('exampleJob');
if(jobButton){
  jobButton.addEventListener('click',()=>{
    const job=examples[Math.floor(Math.random()*examples.length)];
    const card=jobButton.closest('.job-card');
    card.querySelector('.job-title h3').textContent=job[0];
    const values=card.querySelectorAll('.job-rule-grid b');
    values[0].textContent='External Contract';
    values[1].textContent=job[1];
    values[2].textContent=job[2];
    values[3].textContent=job[3];
    jobButton.textContent='Sortear outro exemplo';
  });
}
