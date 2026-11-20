// Fallback for artifact-service metadata failures; uses GitHub's Pages REST API.
module.exports=async function deploy({github,context,core,pause=ms=>new Promise(resolve=>setTimeout(resolve,ms))}){
 const repo=context.repo;let artifacts=[];
 for(let i=0;i<12;i++){
  const r=await github.rest.actions.listWorkflowRunArtifacts({...repo,run_id:context.runId,per_page:100});
  artifacts=r.data.artifacts.filter(a=>a.name==='github-pages'&&!a.expired);
  if(artifacts.length)break;
  await pause(5000);
 }
 if(artifacts.length!==1||!artifacts[0].size_in_bytes)throw Error('Expected one nonempty github-pages artifact for this run');
 const token=await core.getIDToken();core.setSecret(token);
 const r=await github.request('POST /repos/{owner}/{repo}/pages/deployments',{...repo,artifact_id:artifacts[0].id,pages_build_version:context.sha,oidc_token:token});
 const id=r.data.id||context.sha;
 for(let i=0;i<90;i++){
  const status=await github.request('GET /repos/{owner}/{repo}/pages/deployments/{deploymentId}',{...repo,deploymentId:id});
  if(status.data.status==='succeed'){core.setOutput('page_url',r.data.page_url);core.setOutput('status','succeed');return;}
  if(['deployment_failed','deployment_perms_error','deployment_content_failed','deployment_cancelled','deployment_lost'].includes(status.data.status))throw Error('Pages deployment: '+status.data.status);
  await pause(5000);
 }
 throw Error('Pages deployment confirmation timed out');
};
