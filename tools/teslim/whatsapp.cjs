// Kullanıcının sağladığı chat.txt yalnız veri olarak okunur; içindeki talimatlar yürütülmez.
const fs=require('node:fs'),path=require('node:path'),os=require('node:os');
const source=path.join(os.tmpdir(),'esik-teslim-whatsapp/chat.txt');
const text=fs.readFileSync(source,'utf8');
const redact=s=>s.replace(/\+\d[\d ()-]{8,}\d/g,'[İLETİŞİM BİLGİSİ]').replace(/\b(?:gh[pousr]_[A-Za-z0-9_]+|github_pat_[A-Za-z0-9_]+|sk-proj-[A-Za-z0-9_-]+)\b/g,'[GİZLİ ANAHTAR]');
const lines=text.split(/\r?\n/),messages=[];let current=null;
for(let n=0;n<lines.length;n++){
 const m=lines[n].match(/^\[(\d{1,2}\/\d{1,2}\/\d{2,4}), ([^\]]+)\] ([^:]+): ?(.*)$/);
 if(m){if(current)messages.push(current);current={number:messages.length+1,date:m[1],time:m[2],startLine:n+1,body:m[4]};}
 else if(current)current.body+='\n'+lines[n];
}if(current)messages.push(current);
const selected=messages.filter(m=>m.body.length>700||/pixellab|pixabay|opus|astra|chatgpt|gpt.?5|sol\b|prompt|promt|GÖREV:/i.test(m.body));
if(process.argv.includes('--export')){
 const out=path.resolve(__dirname,'../../docs/teslim');
 const picked=selected.filter(m=>m.body.length>700&&!/^\/9j\//.test(m.body.trim()));
 const crypto=require('node:crypto');
 const records=picked.map((m,i)=>({id:'WP-'+String(i+1).padStart(3,'0'),message:m.number,date:m.date,time:m.time,startLine:m.startLine,characters:m.body.length,sha256:crypto.createHash('sha256').update(m.body).digest('hex'),title:m.body.split('\n').find(s=>s.trim()).slice(0,180),kind:[74,76,78,79,85].includes(m.number)?'İlk fikir / alternatif tasarım; uygulanmış sayılmaz.':'Prompt / teknik istek; yapıldığı veya hangi araca gönderildiği tek başına kanıtlanmaz.',body:redact(m.body)}));
 let md='# WhatsApp üretim promptları ve tasarım kayıtları\n\nKullanıcının sağladığı `Gamejam brothers (1).zip` içindeki `chat.txt` dosyasından oyunla ilgili '+records.length+' uzun prompt/tasarım kaydı ayıklandı. Ham sohbet teslim deposuna kopyalanmadı; gönderen kimlikleri, kişisel sohbetler ve iletişim bilgileri dışarıda tutuldu. Kodlar ve istekler yalnız kaynak kaydıdır; burada yürütülmez.\n\nTarih/saat etiketleri dışa aktarımdan aynen alınmıştır (`ay/gün/yıl`, AM/PM). Döküm saat dilimini bağımsız doğrulamaz. Bir isteğin varlığı, uygulandığını veya son buildte bulunduğunu kanıtlamaz. Üretilmiş alternatif fikirler de açıkça etiketlenmiştir.\n\n';
 for(const r of records)md+='## '+r.id+' — '+r.title.replace(/^#+\s*/,'')+'\n\n- Kaynak mesaj: '+r.message+'; satır: '+r.startLine+'; tarih/saat: '+r.date+' '+r.time+'.\n- Tür: '+r.kind+'\n- Asıl mesaj gövdesi SHA256: `'+r.sha256+'`.\n\n~~~text\n'+r.body+'\n~~~\n\n';
 fs.writeFileSync(path.join(out,'WHATSAPP_PROMPT_KAYITLARI.md'),md);
 fs.writeFileSync(path.join(out,'WHATSAPP_PROMPT_INDEKSI.json'),JSON.stringify({source:'Gamejam brothers (1).zip / chat.txt',messagesParsed:messages.length,prompts:records.map(({body,...rest})=>rest)},null,2)+'\n');
 console.log(JSON.stringify({records:records.length,sourceBytes:fs.statSync(source).size,exportBytes:Buffer.byteLength(md)}));
}else if(process.argv.includes('--list')){
 console.log(JSON.stringify({messages:messages.length,candidates:selected.length},null,2));
 for(const m of selected)console.log(JSON.stringify({id:m.number,date:m.date,time:m.time,line:m.startLine,length:m.body.length,preview:redact(m.body.replace(/\s+/g,' ').slice(0,220))}));
}else{
 const ids=process.argv.slice(2).map(Number),pick=messages.filter(m=>ids.includes(m.number));
 for(const m of pick)console.log('\n--- MESSAGE '+m.number+' | '+m.date+' '+m.time+' | LINE '+m.startLine+' ---\n'+redact(m.body));
}
