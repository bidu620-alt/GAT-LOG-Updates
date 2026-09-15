(() => {
  'use strict';
  const $ = id => document.getElementById(id);
  const MODE_KEY='gat_dash_media_mode_v1', HIDDEN_KEY='gat_dash_media_hidden_v1';
  const native = window.chrome && window.chrome.webview ? 'windows' : (window.GatAndroid ? 'android' : 'web');
  const state={mode:localStorage.getItem(MODE_KEY)||'gat',hidden:localStorage.getItem(HIDDEN_KEY)==='1',media:null,error:'',embed:'',active:true};
  function post(payload){try{if(native==='windows')window.chrome.webview.postMessage(payload);else if(native==='android')window.GatAndroid.postMessage(JSON.stringify(payload));}catch{}}
  function youtubeEmbed(url){
    try{
      const u=new URL(url), h=u.hostname.toLowerCase();
      if(!['youtube.com','www.youtube.com','m.youtube.com','music.youtube.com','youtu.be'].includes(h))return '';
      const list=u.searchParams.get('list');
      const suffix='&autoplay=1&rel=0&playsinline=1&origin=https%3A%2F%2Fgatdash.local&widget_referrer=https%3A%2F%2Fgatlogets2.com.br%2F';
      if(list&&/^[A-Za-z0-9_-]{12,100}$/.test(list))return 'https://www.youtube.com/embed/videoseries?list='+encodeURIComponent(list)+suffix;
      let id=''; if(h==='youtu.be')id=u.pathname.replace(/^\//,'').split('/')[0]; else id=u.searchParams.get('v')||'';
      if(!id){const p=u.pathname.split('/').filter(Boolean);if(p.length>1&&['shorts','embed','live'].includes(p[0]))id=p[1]}
      return /^[A-Za-z0-9_-]{11}$/.test(id)?'https://www.youtube.com/embed/'+id+'?autoplay=1&rel=0&playsinline=1&origin=https%3A%2F%2Fgatdash.local&widget_referrer=https%3A%2F%2Fgatlogets2.com.br%2F':'';
    }catch{return ''}
  }
  function choice(){
    const m=state.media||{};
    if(state.mode==='mine')return {name:'Meu Vídeo',url:String((m.myVideo&&m.myVideo.url)||''),enabled:true};
    if(state.mode==='web')return {name:'Canal Web',url:String((m.web&&m.web.url)||''),enabled:true};
    return {name:'Canal GAT',url:String((m.channelGat&&m.channelGat.url)||''),enabled:!!(m.channelGat&&m.channelGat.enabled)};
  }
  function render(){
    if(!$('mediaGat'))return;
    $('mediaGat').classList.toggle('active',state.mode==='gat'); $('mediaMine').classList.toggle('active',state.mode==='mine'); $('mediaWeb').classList.toggle('active',state.mode==='web');
    const c=choice(), frame=$('mediaFrame'), empty=$('mediaEmpty'); $('mediaSourceName').textContent=c.name; $('toggleVideoBtn').textContent=state.hidden?'MOSTRAR VÍDEO':'OCULTAR VÍDEO';
    if(!state.active){frame.removeAttribute('src');frame.classList.add('hidden');empty.classList.remove('hidden');$('mediaEmptyTitle').textContent='Player pausado';$('mediaEmptyText').textContent='O áudio está ativo em outro módulo do GAT Telemetria.';$('mediaStatus').textContent='Mídia pausada para evitar áudio duplicado';state.embed='';return}
    if(state.hidden){frame.removeAttribute('src');frame.classList.add('hidden');empty.classList.remove('hidden');$('mediaEmptyTitle').textContent='Vídeo oculto';$('mediaEmptyText').textContent='Toque em MOSTRAR VÍDEO para voltar ao player.';$('mediaStatus').textContent='Player oculto pelo motorista';state.embed='';return}
    const url=c.url.trim();
    if(!c.enabled||!url){frame.removeAttribute('src');frame.classList.add('hidden');empty.classList.remove('hidden');$('mediaEmptyTitle').textContent=state.mode==='gat'?'Canal GAT sem programação':'Nenhuma fonte configurada';$('mediaEmptyText').textContent=state.error||'Configure esta fonte na Rádio/TV GAT do Telemetria no PC.';$('mediaStatus').textContent=state.error||'Aguardando GAT Telemetria';state.embed='';return}
    const embed=state.mode==='web'?url:(youtubeEmbed(url)||url);
    if(embed!==state.embed){state.embed=embed;frame.src=embed}
    frame.classList.remove('hidden');empty.classList.add('hidden');$('mediaStatus').textContent=state.mode==='web'?'Canal Web • a página pode bloquear incorporação':'Fonte sincronizada pelo GAT Telemetria';
  }
  window.gatDashPushMedia=payload=>{let m=payload;if(typeof m==='string'){try{m=JSON.parse(m)}catch{return}}if(!m||typeof m!=='object')return;if(['gat','mine','web'].includes(String(m.activeMode||''))){state.mode=String(m.activeMode);localStorage.setItem(MODE_KEY,state.mode)}state.media=m;state.error='';render()};
  window.gatDashMediaError=message=>{state.error='GAT Telemetria: '+(message||'ponte de mídia indisponível');render()};
  window.gatDashSetMediaActive042=enabled=>{state.active=!!enabled;state.embed='';render()};
  function setMode(mode){state.mode=mode;localStorage.setItem(MODE_KEY,mode);state.embed='';post({type:'mediaMode',mode});render()}
  $('mediaGat').onclick=()=>setMode('gat'); $('mediaMine').onclick=()=>setMode('mine'); $('mediaWeb').onclick=()=>setMode('web');
  $('toggleVideoBtn').onclick=()=>{state.hidden=!state.hidden;localStorage.setItem(HIDDEN_KEY,state.hidden?'1':'0');render()};
  function poll(){if(native!=='web')post({type:'mediaRefresh'})}
  render(); poll(); setInterval(poll,3000);
})();
