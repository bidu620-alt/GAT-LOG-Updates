export const EMOJI_PACK={id:'emoji:dog-estrada-1',kind:'emoji',title:'Dog da Estrada — Pacote 01',description:'8 emojis e figurinhas do mascote BiduLog para publicações, comentários e reações.',cash_centavos:300,gold_units:600000,bcoin_units:0};
export const PREMIUM_EMOJIS=['positivo','risada','bora-rodar','entrega-feita','deu-ruim','cafe','ets2','ats'];
export const STANDARD_EMOJIS=['👍','❤️','😂','😮','😢','🔥','🚛','🎉'];
export function isBiduOwner(db,user){return user==='biduzao'&&!!db.sql.prepare("SELECT 1 FROM accounts WHERE user=? AND disabled=0 AND role IN ('owner','admin')").get(user)}
export function hasEmojiPack(db,user){return !!user&&(isBiduOwner(db,user)||!!db.sql.prepare('SELECT 1 FROM eco_entitlements WHERE user=? AND product_id=?').get(user,EMOJI_PACK.id))}
export function checkEmojiText(db,user,text){const codes=[...String(text||'').matchAll(/:bidu_([a-z0-9-]+):/g)].map(m=>m[1]);if(codes.some(x=>!PREMIUM_EMOJIS.includes(x)))throw Object.assign(new Error('invalid_emoji'),{status:400});if(codes.length&&!hasEmojiPack(db,user))throw Object.assign(new Error('emoji_pack_required'),{status:403})}
