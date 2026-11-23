const queues=new WeakMap();
// Serialize local writes across requests so an awaited SQLite savepoint cannot
// accidentally include another driver's purchase or telemetry transaction.
export function serializeWrite(db,work){const prior=queues.get(db)||Promise.resolve();const result=prior.catch(()=>{}).then(work);const tail=result.catch(()=>{});queues.set(db,tail);return result.finally(()=>{if(queues.get(db)===tail)queues.delete(db)});}
export async function passengerAtomic(db,enabled,work){if(!enabled)return work();db.sql.exec('SAVEPOINT passenger_packet');try{const result=await work();db.sql.exec('RELEASE passenger_packet');return result;}catch(e){db.sql.exec('ROLLBACK TO passenger_packet; RELEASE passenger_packet');throw e;}}
