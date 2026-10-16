export function telemetryCanonical(value) {
  if (value === null) return 'n';
  if (typeof value === 'boolean') return value ? 't' : 'f';
  if (typeof value === 'string') { const bytes=Buffer.from(value,'utf8'); return 's'+bytes.length+':'+bytes.toString('base64'); }
  if (typeof value === 'number') {
    if (!Number.isFinite(value)) throw Error('Non-finite telemetry number');
    const bytes=Buffer.alloc(8);bytes.writeDoubleBE(value===0?0:value);return 'd'+bytes.toString('hex');
  }
  if (Array.isArray(value)) return 'a'+value.length+':'+value.map(telemetryCanonical).join('');
  if (typeof value === 'object') {const keys=Object.keys(value).sort();return 'o'+keys.length+':'+keys.map(k=>telemetryCanonical(k)+telemetryCanonical(value[k])).join('');}
  throw Error('Unsupported telemetry token');
}
