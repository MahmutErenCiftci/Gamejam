/* TESLİM-01: salt okunur kaynak taraması. Yalnız docs/teslim altına rapor yazar.
 * Çalıştırma: node tools/teslim/envanter.cjs
 * Bu tarama build/oynanış veya lisans onayı değildir.
 */
const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const cp = require('node:child_process');
const root = path.resolve(__dirname, '../..');
const out = path.join(root, 'docs/teslim');
fs.mkdirSync(out, {recursive:true});
const read = f => fs.readFileSync(path.join(root,f), 'utf8');
const exists = f => fs.existsSync(path.join(root,f));
const git = (...args) => cp.execFileSync('git', args, {cwd:root, encoding:'utf8'}).trim();
function walk(rel) {
  if (!exists(rel)) return [];
  return fs.readdirSync(path.join(root,rel),{withFileTypes:true}).flatMap(e => {
    const p = rel + '/' + e.name;
    return e.isDirectory() ? walk(p) : [p];
  });
}
const assetFiles = walk('Assets');
const visualFiles = walk('visuals');
const toolFiles = walk('tools').filter(f=>!f.startsWith('tools/teslim/'));
const textExt = new Set(['.cs','.prefab','.unity','.asset','.mat','.controller','.anim','.json','.shader','.shadergraph','.js','.py','.md','.txt','.cginc']);
const docs = [...assetFiles,...toolFiles].filter(f=>textExt.has(path.extname(f).toLowerCase()));
const texts = docs.map(file=>({file,text:read(file)}));
const guidToFile = new Map(), inbound = new Map(), edges = new Map();
for (const meta of assetFiles.filter(f=>f.endsWith('.meta'))) {
  const guid = read(meta).match(/^guid:\s*([a-f0-9]{32})/m)?.[1];
  if (guid) guidToFile.set(guid,meta.slice(0,-5));
}
for (const {file,text} of texts) {
  const refs = new Set();
  for (const m of text.matchAll(/\bguid:\s*([a-f0-9]{32})/g)) {
    const target = guidToFile.get(m[1]);
    if (!target || target===file) continue;
    refs.add(target);
    if (!inbound.has(target)) inbound.set(target,new Set());
    inbound.get(target).add(file);
  }
  edges.set(file,[...refs]);
}
const buildSettings = read('ProjectSettings/EditorBuildSettings.asset');
const scenes = [...buildSettings.matchAll(/- enabled: (\d)\s+path: ([^\r\n]+)/g)].map(m=>({enabled:m[1]==='1',path:m[2].trim()}));
function traverse(roots) {
  const reached = new Set(), todo = [...roots];
  while(todo.length) {
    const f=todo.pop(); if(reached.has(f)) continue;
    reached.add(f); for(const n of edges.get(f)||[]) todo.push(n);
  }
  return reached;
}
const sceneReach = traverse(scenes.filter(s=>s.enabled).map(s=>s.path));
const resourceReach = traverse(assetFiles.filter(f=>f.includes('/Resources/')&&!f.endsWith('.meta')&&!f.includes('/Editor/')));
const tracked = new Set(git('ls-files').split(/\r?\n/));
const cc0 = JSON.parse(read('tools/audio/cc0_sources.json'));
const cc0Map = new Map();
for (const p of cc0.packs) for(const [original,target] of Object.entries(p.files)) cc0Map.set(target,{...p,original});
const mediaExt = new Set(['.png','.jpg','.jpeg','.gif','.bmp','.webp','.wav','.mp3','.ogg','.ttf','.otf','.mp4','.avi','.psd','.aseprite']);
const fontSources = {
  PixelOperator:{name:'Pixel Operator',creator:'Jayvee Enaguas (HarvettFox96)',license:'CC0-1.0',url:'https://fontlibrary.org/en/font/pixel-operator',proof:'Assets/Fonts/PixelOperator/README.txt; Assets/Fonts/PixelOperator/LICENSE-CC0.txt',changes:'TR Bold: Ğ/ğ/İ/Ş/ş ve simgeler eklendi; TR Heavy: ek kalın kesim türetildi.'},
  PixelifySans:{name:'Pixelify Sans',creator:'The Pixelify Sans Project Authors',license:'SIL OFL 1.1',url:'https://github.com/eifetx/Pixelify-Sans',proof:'Assets/Fonts/PixelifySans/OFL.txt; Assets/Art/UI/Fonts/PixelifySans/PixelifySans-OFL.txt',changes:'Dosya düzeyinde değişiklik doğrulanmadı; Unity importu/atlas üretimi ayrı.'},
  Nunito:{name:'Nunito Black',creator:'The Nunito Project Authors',license:'SIL OFL 1.1',url:'https://github.com/googlefonts/nunito',proof:'Assets/Art/Dialogue/Fonts/Nunito-OFL.txt; Assets/Resources/Objectives/Nunito-OFL.txt',changes:'İki klasörde kopya mevcut; kaynak font değişikliği doğrulanmadı.'},
  PressStart2P:{name:'Press Start 2P',creator:'The Press Start 2P Project Authors / cody@zone38.net',license:'SIL OFL 1.1',url:'https://fonts.google.com/specimen/Press+Start+2P',proof:'Assets/Fonts/PressStart2P/OFL.txt',changes:'Değişiklik doğrulanmadı.'},
  LiberationSans:{name:'Liberation Sans',creator:'Google Corporation; Red Hat, Inc. (yerel telif bildirimi)',license:'SIL OFL 1.1',url:'https://github.com/liberationfonts/liberation-fonts',proof:'Assets/TextMesh Pro/Fonts/LiberationSans - OFL.txt',changes:'TextMesh Pro ile gelen font; kullanım/atlas referansı envanterde.'}
};
const pixabay = new Map([
 ['SFX_FE_Tree_Fall','https://pixabay.com/sound-effects/search/falling%20tree/'],
 ['SFX_Ball_Kick','https://pixabay.com/sound-effects/search/kick%20ball/'],
 ['SFX_Cenk_Hurt','https://pixabay.com/sound-effects/search/young%20boy%20pain%20grunt/'],
 ['SFX_FE_Shock','https://pixabay.com/sound-effects/search/%20sound%20of%20electric%20shock/'],
 ['SFX_Footstep_Wood_Loop','https://pixabay.com/sound-effects/search/walking%20on%20wood/']
]);
const parseJson=f=>JSON.parse(read(f).replace(/^\uFEFF/,''));
const pixabayDetailed=exists('docs/teslim/PIXABAY_KAYNAKLARI.json')?parseJson('docs/teslim/PIXABAY_KAYNAKLARI.json'):null;
const originalMp3Matches=exists('docs/teslim/MP3_ORIJINAL_ESLESMELERI.json')?parseJson('docs/teslim/MP3_ORIJINAL_ESLESMELERI.json'):[];
// Kesin üretim kaydı yalnız gerçekten karşılaştırılan 34 dosyada verilir.
let synthesisCheck={status:'UNTESTED',command:'node tools/audio/generate_audio.js --check',output:''};
try {
  synthesisCheck.output=cp.execFileSync(process.execPath,['tools/audio/generate_audio.js','--check'],{cwd:root,encoding:'utf8'});
  synthesisCheck.status=synthesisCheck.output.includes('PASS: 34 files')?'PASS':'INCOMPLETE';
} catch(error) {synthesisCheck.status='FAIL';synthesisCheck.output=String(error.stdout||error.message);}
const verifiedSynth = new Set(synthesisCheck.status==='PASS'?[...synthesisCheck.output.matchAll(/^([A-Z][A-Za-z0-9_]+):/gm)].map(m=>m[1]):[]);
fs.writeFileSync(path.join(out,'SES_SENTEZ_DOGRULAMA.txt'),synthesisCheck.command+'\nDurum: '+synthesisCheck.status+'\n'+synthesisCheck.output);
function sourceFor(file) {
  const ext = path.extname(file).toLowerCase(), stem = path.basename(file,ext);
  if(ext==='.mp3'&&pixabayDetailed){
    const entry=pixabayDetailed.files.find(f=>f.id===stem);
    const match=originalMp3Matches.find(f=>f.file===file&&f.originals.includes(entry?.original));
    if(entry&&match&&crypto.createHash('sha256').update(fs.readFileSync(path.join(root,file))).digest('hex')===match.sha256)
      return {kind:'HAZIR_PIXABAY_SESI',provider:entry.provider+'; sanatçı: '+entry.artist,source:entry.title,url:entry.url,license:'Pixabay Content License; '+pixabayDetailed.licenseUrl,proof:'MP3_ORIJINAL_ESLESMELERI.json: orijinal indirmeyle SHA256 aynı; resmî tekil içerik sayfası kontrol edildi.',changes:entry.changes,aiGenerated:entry.aiGenerated,certainty:'DOSYA_KAYNAK_SAYFASI_ESLESTI'};
  }
  if(cc0Map.has(file)) {
    const p=cc0Map.get(file);
    return {kind:'HAZIR_KAYNAK',provider:'Kenney',source:p.name+' / '+p.original,url:p.page,license:'CC0-1.0',proof:'tools/audio/cc0_sources.json; '+p.licenseFile,changes:'OGG verisi değiştirilmeden çalışma zamanı adına dönüştürülmüş (manifest beyanı).',certainty:'BELGEYLE_ESLESTI'};
  }
  if(['.ttf','.otf'].includes(ext)) {
    const key=Object.keys(fontSources).find(k=>file.replaceAll(' ','').includes(k));
    if(key) {const f=fontSources[key];return {kind:'HAZIR_FONT',provider:f.creator,source:f.name,url:f.url,license:f.license,proof:f.proof,changes:f.changes,certainty:'YEREL_LISANS_MEVCUT'};}
  }
  if(ext==='.mp3') return {
    kind:'DIS_KAYNAK_TEYIT_BEKLIYOR',provider:pixabay.has(stem)?'Pixabay (kullanıcı beyanındaki ses türüne göre aday eşleşme)':'TEYIT_BEKLIYOR',source:stem,
    url:pixabay.get(stem)||'',license:'Tekil içerik sayfası/yazar/lisans kaydı bekleniyor; Pixabay ise Content License, CC0 değil.',proof:pixabay.has(stem)?'18 Ekim 2026 kullanıcı mesajı + dosya adı + kod kancası; dosyanın aynı indirme olduğu teyit edilmedi.':'Dosya mevcut; kaynağı repoda bulunamadı.',
    changes:'Kırpma, loop, hız ve dönüştürme bilgisi teyit bekliyor.',certainty:'EKIP_TEYIDI_GEREKLI'
  };
  if(ext==='.wav') {
    if(verifiedSynth.has(stem)) return {kind:'KODLA_SENTEZ_DOGRULANDI',provider:'Ekip / Codex (Astra kaydı AUDIO_LIST.md içinde)',source:'tools/audio/generate_audio.js',url:'',license:'Özgün prosedürel üretim; üretici kodunda harici örnek/kütüphane yok. Ekip hak beyanı ayrıca alınacak.',proof:'SES_SENTEZ_DOGRULAMA.txt: 34 dosya bit düzeyinde yeniden üretim karşılaştırması PASS.',changes:'Sinüs, gürültü, filtre ve katmanlama; WAV. Overrides varsa oyun Kenney OGG dosyasını öncelikli yükler.',certainty:'BIT_DUZEYINDE_URETIM_DOGRULANDI'};
    const gens=texts.filter(t=>t.file.endsWith('.js')&&/audio|sfx/i.test(t.file)&&t.text.includes(stem)).map(t=>t.file);
    if(/^SFX_FE_Thunder_[23]$/.test(stem)) gens.push('tools/audio/generate_flood_hazards.js');
    if(file.includes('/Audio/')&&gens.length) return {kind:'KODLA_SENTEZ_ADAYI',provider:'Ekip / AI destekli üretim kodu',source:gens.join('; '),url:'',license:'Üretim kodu özgün sentez beyan ediyor; üçüncü taraf örnek kullanımı görülmedi. Hak onayı ekipte.',proof:gens.join('; '),changes:'Sinüs, gürültü, filtre, zarf ve katmanlama ile WAV üretimi; bit düzeyinde doğrulama bu taramada yapılmadı.',certainty:'URETIM_KODUNDA_ADI_VAR'};
    return {kind:'KAYNAGI_BELIRSIZ_SES',provider:'TEYIT_BEKLIYOR',source:stem,url:'',license:'TEYIT_BEKLIYOR',proof:'Dosya mevcut; açık üretim veya indirme kaydı eşleştirilemedi.',changes:'TEYIT_BEKLIYOR',certainty:'EKIP_TEYIDI_GEREKLI'};
  }
  const artTools=texts.filter(t=>t.file.startsWith('tools/')&&t.file.endsWith('.js')&&t.text.includes(stem)).map(t=>t.file);
  if(file.startsWith('visuals/')||file.includes('/Source~/')) return {kind:'URETIM_REFERANSI_VEYA_HAM_GORSEL',provider:'Ekip / araç adı teyit bekliyor',source:'Kullanıcı: karakterler pixelart ile üretildi, promptlar ekipçe hazırlandı.',url:'',license:'Araç ve girdi görselinin kullanım hakkı teyit bekliyor.',proof:artTools.join('; ')||'visuals/ASSET_MANIFEST.md; kullanıcı beyanı',changes:'Referans/konsept veya ham üretim girdisi; oyunda kullanıldığı varsayılmadı.',certainty:'KAYNAK_TEYIDI_GEREKLI'};
  return {kind:artTools.length?'KODDA_GORSEL_ISLEME_KAYDI':'GORSEL_KAYNAGI_TEYIT_BEKLIYOR',provider:'Ekip / AI destekli üretim, araç teyidi bekleniyor',source:artTools.join('; ')||'Üretim kaydı tekil dosyayla eşleştirilemedi.',url:'',license:'Temel görsel ve kullanılan AI/asset sağlayıcısının koşulları teyit bekliyor.',proof:artTools.join('; ')||'Dosya + referanslar',changes:artTools.length?'Üretim/dönüşüm kodunda ad geçiyor; yeni çizim mi türetim mi içerik ayrıca incelenmeli.':'Kırpma, palette, sprite sheet veya yeniden çizim bilgisi ekipten alınmalı.',certainty:artTools.length?'KOD_ESLESMESI_KOKEN_KANITI_DEGIL':'EKIP_TEYIDI_GEREKLI'};
}
const assets=[...assetFiles,...visualFiles].filter(f=>mediaExt.has(path.extname(f).toLowerCase())).sort().map(file=>{
  const ext=path.extname(file).toLowerCase(),stem=path.basename(file,ext);
  const nameRefs=texts.filter(t=>t.file.endsWith('.cs')&&(t.text.includes('"'+stem+'"')||t.text.includes("'"+stem+"'")||t.text.includes('"'+stem+'/'))).map(t=>t.file);
  const refs=[...(inbound.get(file)||[])];
  const resources=file.includes('/Resources/');
  const excluded=file.split('/').some(p=>p.endsWith('~'));
  return {file,bytes:fs.statSync(path.join(root,file)).size,sha256:crypto.createHash('sha256').update(fs.readFileSync(path.join(root,file))).digest('hex'),gitTracked:tracked.has(file),extension:ext,
    serializedRefs:refs,codeNameRefs:nameRefs,sceneDependency:sceneReach.has(file),resourceDependency:resourceReach.has(file),
    usage:excluded?'Unity importundan hariç (~ klasörü); ham girdi, public repo lisansı yine incelenmeli.':file.startsWith('visuals/')?'Assets dışında referans/konsept; doğrudan build girdisi değil.':sceneReach.has(file)?'Etkin sahnenin GUID bağımlılık zincirinde; görünür/aktif olduğu ve oynanış testi doğrulanmadı.':resources?'Resources altında; build adayı, gerçekten yüklenmesi/çalması doğrulanmadı.':resourceReach.has(file)?'Resources varlığının GUID zincirinde; çalışma zamanı kullanımı doğrulanmadı.':nameRefs.length?'Kodda ad eşleşmesi; çağrının etkinliği ve sahne bağlantısı doğrulanmadı.':refs.length?'Serialize referans mevcut; etkin sahne/çalışma zamanı bağlantısı doğrulanmadı.':'Kesin kullanım kanıtı yok; kullanılmıyor sonucu çıkarılamaz.',
    ...sourceFor(file)};
});
const packages=JSON.parse(read('Packages/manifest.json')).dependencies;
const cache=exists('Library/PackageCache')?fs.readdirSync(path.join(root,'Library/PackageCache')):[];
const packageRows=Object.entries(packages).map(([name,version])=>{
  const cacheDir=cache.find(d=>d.startsWith(name+'@'));
  const base=cacheDir?'Library/PackageCache/'+cacheDir:null;
  let info={}; if(base&&exists(base+'/package.json')) info=JSON.parse(read(base+'/package.json'));
  const licenseFiles=base?fs.readdirSync(path.join(root,base),{withFileTypes:true}).filter(e=>e.isFile()&&!e.name.endsWith('.meta')&&/licen[sc]e|third.?party|notices/i.test(e.name)).map(e=>e.name):[];
  return {name,version,displayName:info.displayName||name,license:info.license||'package.json alanı yok; yerel LICENSE / Unity koşulları incelenmeli',licenseFiles:licenseFiles.map(n=>base+'/'+n),note:name.includes('modules.')?'Unity yerleşik modülü; kurulu olması oyunda kullanıldığı anlamına gelmez.':'Manifest bağımlılığı; Editor/runtime ayrımı ve buildte kullanım ayrıca doğrulanmalı.'};
});
const counts={totalMedia:assets.length,audio:assets.filter(a=>['.wav','.mp3','.ogg'].includes(a.extension)).length,mp3:assets.filter(a=>a.extension==='.mp3').length,fonts:assets.filter(a=>['.ttf','.otf'].includes(a.extension)).length,images:assets.filter(a=>!['.wav','.mp3','.ogg','.ttf','.otf','.mp4','.avi'].includes(a.extension)).length,packages:packageRows.length,csharpFiles:assetFiles.filter(f=>f.endsWith('.cs')).length};
const result={status:'TASLAK_EKIP_TEYIDI_BEKLENIYOR',generatedAt:new Date().toISOString(),timezone:'Europe/Istanbul',commit:git('rev-parse','HEAD'),branch:git('branch','--show-current'),counts,scenes,assets,packages:packageRows,synthesisCheck:{status:synthesisCheck.status,command:synthesisCheck.command,verifiedFiles:[...verifiedSynth]},method:'Etkin EditorBuildSettings sahnelerinin GUID grafiği; Resources kökleri; C# sabit ad eşleşmeleri. Aktif Build Profile veya build artefaktı doğrulanmadı. Kaynak tespiti belge/kod aday eşleştirmesidir; ses dinleme, lisans sertifikası ve oynanış testi değildir. İstisna: generate_audio.js içindeki 34 WAV için bit düzeyinde üretim doğrulaması ayrıca kaydedilmiştir.'};
fs.writeFileSync(path.join(out,'ENVANTER.json'),JSON.stringify(result,null,2)+'\n');
const esc=s=>String(s??'').replaceAll('|',' / ').replaceAll('\n',' ').replaceAll('\r','');
const table=(headers,rows)=>'| '+headers.join(' | ')+' |\n| '+headers.map(()=>'---').join(' | ')+' |\n'+rows.map(r=>'| '+r.map(esc).join(' | ')+' |').join('\n')+'\n';
const header='# Dosya düzeyinde asset envanteri — TESLİM-01\n\n**TASLAK / statik inceleme.** Üretim zamanı: '+result.generatedAt+' (UTC). İncelenen commit: `'+result.commit+'`; çalışma ağacındaki yerel değişiklikler de taranmıştır.\n\n'+result.method+'\n\nToplam '+counts.totalMedia+' medya dosyası; '+counts.audio+' ses, '+counts.fonts+' font dosyası, '+counts.images+' görsel. Tam SHA256, tüm referanslar ve kaynak alanları: [ENVANTER.json](ENVANTER.json).\n\n';
const groups=new Map();
for(const a of assets) {const group=path.posix.dirname(a.file);if(!groups.has(group))groups.set(group,[]);groups.get(group).push(a);}
let md=header;
for(const [group,list] of groups) md+='## '+group+'\n\n'+table(['Dosya','Kaynak / durum','Kullanım kanıtı','Referans örneği'],list.map(a=>['`'+path.basename(a.file)+'`',a.kind,a.usage,[...a.serializedRefs,...a.codeNameRefs].slice(0,2).join('; ')||'—']))+'\n';
fs.writeFileSync(path.join(out,'ASSET_ENVANTERI.md'),md);
const audio=assets.filter(a=>['.wav','.mp3','.ogg'].includes(a.extension));
let smd='# Sesler — dosya ve kaynak eşleştirmesi\n\n**TASLAK.** '+audio.length+' ses dosyası. Bir ID için Overrides klasöründeki OGG önceliklidir (`GameAudioRuntime.Load`). Aynı ID\'nin sentez WAV yedeği farklı kaynakmış gibi saklanmamıştır; ikisi ayrı listelenir. MP3 için uzantı, kaynağın veya AI kullanımının kanıtı sayılmaz.\n\n';
for(const a of audio) smd+='## '+path.basename(a.file)+'\n\n- **Proje dosyası:** `'+a.file+'`\n- **Sağlayıcı / kaynak:** '+a.provider+'; '+a.source+'\n- **Bağlantı:** '+(a.url||'TEYİT BEKLİYOR / proje içi üretim kodu')+'\n- **Lisans / izin:** '+a.license+'\n- **Kullanım:** '+a.usage+'\n- **Kanca / referans:** '+([...a.codeNameRefs,...a.serializedRefs].slice(0,4).join('; ')||'Tekil bağlantı bulunamadı; ekip teyidi gerekiyor.')+'\n- **Üretim/değişiklik:** '+a.changes+'\n- **Kanıt:** '+a.proof+'\n- **Git kaydı:** '+(a.gitTracked?'Takip ediliyor':'Takip edilmiyor')+'; **durum:** '+a.certainty+'\n\n';
fs.writeFileSync(path.join(out,'SES_KAYNAKLARI.md'),smd);
fs.writeFileSync(path.join(out,'PAKET_ENVANTERI.md'),'# Motor ve paket envanteri\n\nUnity sürümü: '+read('ProjectSettings/ProjectVersion.txt').split('\n')[0]+'. Bunlar hazır kod/diğer beyanının teknik ekidir. Manifestte bulunmak oyunda kullanımı veya AI ile üretimi kanıtlamaz. Lisans alanı boş paketler için aşağıdaki LICENSE/Third Party Notices dosyaları son dağıtıma göre incelenmelidir.\n\n'+table(['Paket','Sürüm','Lisans alanı','Yerel koşullar'],packageRows.map(p=>[p.name,p.version,p.license,p.licenseFiles.join('; ')||'Yerel kök lisans kaydı eşleştirilemedi / yerleşik modül'])));
console.log(JSON.stringify({commit:result.commit,counts,enabledScenes:scenes.filter(s=>s.enabled),unresolvedAudio:audio.filter(a=>a.certainty==='EKIP_TEYIDI_GEREKLI').map(a=>a.file),fontDetails:assets.filter(a=>['.ttf','.otf'].includes(a.extension)).map(a=>({file:a.file,sceneDependency:a.sceneDependency,resourceDependency:a.resourceDependency,refs:a.serializedRefs.slice(0,3),code:a.codeNameRefs.slice(0,3)}))},null,2));
