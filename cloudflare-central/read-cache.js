// Only cache read models. Credentials, account authorization and write operations
// always reach D1. Cache keys include the release and UTC month.
const local = new Map();
const pending = new Map();
export async function cachedRead(key, ttlSeconds, loader) {
  const fullKey = `gat-1.0.52:${new Date().toISOString().slice(0, 7)}:${key}`;
  const existing = local.get(fullKey);
  if (existing && existing.expires > Date.now()) return structuredClone(existing.value);
  if (pending.has(fullKey)) return structuredClone(await pending.get(fullKey));
  const work = (async () => {
    const cache = typeof caches !== 'undefined' ? caches.default : null;
    const cacheKey = new Request('https://api.gatlogets2.com.br/__read_cache/' + encodeURIComponent(fullKey));
    if (cache) {
      try {
        const hit = await cache.match(cacheKey);
        if (hit) {
          const entry = await hit.json();
          if (entry.expires > Date.now()) { remember(fullKey, entry); return entry.value; }
        }
      } catch (_) { /* Cache failure must not block reads. */ }
    }
    const value = await loader();
    const entry = {value, expires: Date.now() + ttlSeconds * 1000};
    remember(fullKey, entry);
    if (cache) {
      try { await cache.put(cacheKey, new Response(JSON.stringify(entry), {headers: {'Content-Type': 'application/json', 'Cache-Control': `public, max-age=${ttlSeconds}`}})); } catch (_) {}
    }
    return value;
  })();
  pending.set(fullKey, work);
  try { return structuredClone(await work); } finally { pending.delete(fullKey); }
}
function remember(key, entry) {
  if (local.size >= 256) local.delete(local.keys().next().value);
  local.set(key, entry);
}
