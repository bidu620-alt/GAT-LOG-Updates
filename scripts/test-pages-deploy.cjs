const assert=require('node:assert/strict'),deploy=require('./deploy-pages-rest.cjs');
async function fixture({missing=0,duplicate=false,status='succeed'}={}){
 let lists=0,posts=0;const outputs={},artifact={id:123,name:'github-pages',size_in_bytes:500,expired:false};
 const github={rest:{actions:{listWorkflowRunArtifacts:async()=>({data:{artifacts:++lists<=missing?[]:duplicate?[artifact,artifact]:[artifact]}})}},request:async(route,body)=>{if(route.startsWith('POST')){posts++;assert.equal(body.artifact_id,123);assert.equal(body.pages_build_version,'commit');assert.equal(body.oidc_token,'fixture');return {data:{id:456,page_url:'https://example.test'}}}assert.equal(body.deploymentId,456);return {data:{status}}}};
 const core={getIDToken:async()=>'fixture',setSecret:v=>assert.equal(v,'fixture'),setOutput:(k,v)=>outputs[k]=v};
 await deploy({github,core,context:{repo:{owner:'owner',repo:'repo'},runId:42,sha:'commit'},pause:async()=>{}});return {lists,posts,outputs};
}
(async()=>{const r=await fixture({missing:2});assert.equal(r.lists,3);assert.equal(r.posts,1);assert.equal(r.outputs.status,'succeed');await assert.rejects(()=>fixture({duplicate:true}),/Expected one/);await assert.rejects(()=>fixture({missing:20}),/Expected one/);await assert.rejects(()=>fixture({status:'deployment_failed'}),/deployment_failed/);console.log('PASS: Pages artifact delay, strict selection, authenticated deployment and failure handling');})().catch(e=>{console.error(e);process.exitCode=1});
