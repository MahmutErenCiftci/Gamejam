// Public GitHub API ve ZIP'leri okur. Program çalıştırmaz, release değiştirmez.
const fs=require('node:fs'),path=require('node:path'),os=require('node:os'),crypto=require('node:crypto');
const root=path.resolve(__dirname,'../..'),out=path.join(root,'docs/teslim'),temp=path.join(os.tmpdir(),'esik-teslim-releases');
fs.mkdirSync(temp,{recursive:true});fs.mkdirSync(out,{recursive:true});
const base='https://api.github.com/repos/MahmutErenCiftci/Gamejam';
async function api(url){const r=await fetch(url,{headers:{'User-Agent':'ESIK-Release-Review','Accept':'application/vnd.github+json'}});if(!r.ok)throw Error(r.status+' '+url);return r.json();}
;(async()=>{
let releases=[];for(let page=1;page<20;page++){const batch=await api(base+'/releases?per_page=100&page='+page);releases.push(...batch);if(batch.length<100)break;}
const [tags,latest]=await Promise.all([api(base+'/tags?per_page=100'),api(base+'/releases/latest')]);
releases.sort((a,b)=>new Date(b.published_at)-new Date(a.published_at));
const summary=[];
for(const release of releases){
 const sha=tags.find(t=>t.name===release.tag_name)?.commit.sha||null;
 const row={id:release.id,tag:release.tag_name,name:release.name,prerelease:release.prerelease,draft:release.draft,publishedAtUtc:release.published_at,url:release.html_url,sourceCommit:sha,targetCommitish:release.target_commitish,notes:release.body,assets:[]};
 for(const a of release.assets){
  const dest=path.join(temp,path.basename(a.name));
  if(!fs.existsSync(dest)||fs.statSync(dest).size!==a.size){const r=await fetch(a.browser_download_url);if(!r.ok)throw Error('Download '+r.status);fs.writeFileSync(dest,Buffer.from(await r.arrayBuffer()));}
  const hash=crypto.createHash('sha256').update(fs.readFileSync(dest)).digest('hex');
  row.assets.push({name:a.name,url:a.browser_download_url,size:a.size,actualBytes:fs.statSync(dest).size,reportedDigest:a.digest,sha256:hash,digestMatches:a.digest===('sha256:'+hash),tempPath:dest});
 }
 summary.push(row);console.log(JSON.stringify({tag:row.tag,sha:row.sourceCommit,assets:row.assets.map(a=>({name:a.name,size:a.size,digestMatches:a.digestMatches}))}));
}
const sourcePaths=['ProjectSettings/ProjectVersion.txt','ProjectSettings/EditorBuildSettings.asset','Assets/Scripts/Core/GameInput.cs','Assets/Scripts/Core/GameFlow.cs','Assets/Scripts/Core/GameSceneRouter.cs','Assets/Scripts/Chapters/Earthquake/Mode/ProtectionMinigameController.cs','Assets/Data/Earthquake/earthquake_mode.json','Assets/Scripts/Chapters/FloodEvening/Flow/FloodEveningController.cs','Assets/Scripts/Chapters/FloodEvening/Events/BranchObstacle.cs','Assets/Scripts/Chapters/FloodEvening/Final/ApartmentFinale.cs','Assets/Scripts/UI/Pause/PauseMenu.cs'];
const snapshot=[];
for(const p of sourcePaths){const url='https://raw.githubusercontent.com/MahmutErenCiftci/Gamejam/'+encodeURIComponent(latest.tag_name)+'/'+p;const r=await fetch(url);if(!r.ok){snapshot.push({path:p,status:r.status});continue;}const body=await r.text(),dest=path.join(temp,'latest-source',p);fs.mkdirSync(path.dirname(dest),{recursive:true});fs.writeFileSync(dest,body);snapshot.push({path:p,status:200,sha256:crypto.createHash('sha256').update(body).digest('hex'),localFileMatches:fs.existsSync(path.join(root,p))&&fs.readFileSync(path.join(root,p),'utf8')===body});}
const record={reviewedAtUtc:new Date().toISOString(),repository:'https://github.com/MahmutErenCiftci/Gamejam',latestStableTag:latest.tag_name,releases:summary,latestSourceSnapshot:snapshot,verification:'Public release metadatası, tag commitleri, tüm yayımlanmış release assetlerinin gerçek SHA256 karşılaştırması. ZIP yapısı ayrıca incelenecek; EXE çalıştırılmadı.'};
fs.writeFileSync(path.join(out,'RELEASE_KAYITLARI.json'),JSON.stringify(record,null,2)+'\n');
console.log(JSON.stringify({count:summary.length,latest:latest.tag_name,temp,snapshot},null,2));
})().catch(error=>{console.error(error.message);process.exitCode=1;});
