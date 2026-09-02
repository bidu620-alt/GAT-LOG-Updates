(()=>{
  'use strict';
  const PREFIX='gat_driver_profile_state_v2:';
  const clean=v=>String(v||'').replace(/^@/,'').trim().toLowerCase();
  const clone=v=>{try{return JSON.parse(JSON.stringify(v))}catch{return v}};
  const valid=p=>!!p&&typeof p==='object'&&!Array.isArray(p)&&!!clean(p.user);
  const keyFor=user=>PREFIX+clean(user);

  function load(user){
    const u=clean(user);if(!u)return null;
    try{
      const saved=JSON.parse(localStorage.getItem(keyFor(u))||'null');
      if(!saved||!valid(saved.profile)||clean(saved.profile.user)!==u)return null;
      return clone(saved.profile);
    }catch{return null}
  }

  function save(profile){
    if(!valid(profile))return false;
    const user=clean(profile.user);
    try{
      localStorage.setItem(keyFor(user),JSON.stringify({saved_at:Date.now(),profile:clone(profile)}));
      return true;
    }catch{return false}
  }

  function savedAt(user){
    const u=clean(user);if(!u)return 0;
    try{return Number(JSON.parse(localStorage.getItem(keyFor(u))||'null')?.saved_at)||0}catch{return 0}
  }

  function remove(user){
    const u=clean(user);if(!u)return;
    try{localStorage.removeItem(keyFor(u))}catch{}
  }

  window.GATDriverStore={load,save,savedAt,remove,clean};
})();