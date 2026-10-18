const fs=require('node:fs'),path=require('node:path');
const out=path.resolve(__dirname,'../../docs/teslim');
const get=f=>fs.readFileSync(path.join(out,f),'utf8');
const put=(f,s)=>fs.writeFileSync(path.join(out,f),s);
function block(s,start,end,replacement){const a=s.indexOf(start),b=s.indexOf(end,a+start.length);if(a<0||b<0)throw Error('Missing section '+start);return s.slice(0,a)+replacement+'\n\n'+s.slice(b);}
let r=get('RAPOR.md');
r=r.replace('Kenney ve font kayıtları bulundu. MP3\'lerin tekil kaynakları ve çanta sesleri eksik.','Kenney/font kayıtları ve sekiz Pixabay MP3 kaynağı doğrulandı. İki MP3, çanta sesleri ve iki afet bulutu kaynağı eksik.');
r=r.replace('Bu oturumda teslim yapılmadı. ZIP/video dosyaları ve gerçek boyut/süre bilgileri henüz incelenmedi.','Bu oturumda teslim yapılmadı. Beş GitHub release ZIP’i boyut/hash/yapı bakımından incelendi; teslim videosu ve gerçek oynanış testi henüz doğrulanmadı.');
r=r.replace('Kod, görsel, ses, metin, diğer alanları taslakta ayrı ele alındı; kesin araç/model listesi tamamlanmalı.','Beş alan ayrı ele alındı; PixelLab, ChatGPT, Codex, Claude kullanımı ekipçe bildirildi. Kesin alt sürüm/çıktı eşleştirmeleri ve kalan kaynak kayıtları açık.');
r=r.replace('kullanıcının gönderdiği','kullanıcının gönderdiği');
r=r.replace('Kullanıcının gönderdiği 39 JPEG + 34 karakter GIF\'i; karakter aracı kullanıcı tarafından “pixelart” diye adlandırıldı, kesin hizmet adı/URL\'si henüz bilinmiyor. Diğer görsel modelleri bekleniyor. Claude/Codex destekli görsel üretim/dönüşüm scriptleri repoda var.','Kullanıcının gönderdiği 39 JPEG + 34 karakter GIF’i; GIF aracı **PixelLab** olarak doğrulandı. Genel araç listesi ChatGPT, Codex ve Claude. JPEG bazında araç/model eşleştirmeleri tam değil. Berk’in 127 PNG kaynak eki de eklendi.');
r=r.replace('Claude Code ve OpenAI Codex için görev, prompt ve devir kayıtları; tüm modeller/sürümler bekleniyor.','ChatGPT, Claude/Claude Code (Opus), OpenAI Codex (ekip arayüz adları: Sol/Astra); görev, prompt ve devir kayıtları var. Kesin alt sürümler bağımsız doğrulanmadı.');
r=r.replace('Bu arşivlerde model adı veya prompt metni yok.','İlk iki görsel arşivinde model/prompt metni yok; sonradan sağlanan WhatsApp ZIP’inden 35 uzun prompt/tasarım kaydı ayıklandı. Bunların tekil görsellerle eşleştirmesi ayrıca gerekir.');
r=r.replace('Bu iki ZIP yalnız görsel/GIF içeriyor.','İlk iki ZIP yalnız görsel/GIF içeriyor.');
r=block(r,'### 5.1 Kullanıcının verdiği beş Pixabay kaydı','### 5.3 Kenney — doğrulanmış hazır kaynak kayıtları',`### 5.1 Orijinal MP3 dosyalarından geri çıkarılan sekiz kaynak

Kullanıcı indirme bağlantılarının korunmadığını bildirdi. İndirilenler klasöründeki orijinal MP3’ler projedeki seslerle SHA256 üzerinden karşılaştırıldı: **10 MP3’nin 8’i birebir eşleşti**. Sonra tekil Pixabay sayfalarında başlık, sanatçı/sağlayıcı, lisans ve varsa AI etiketi kontrol edildi. Artık bu sekiz kayıtta arama URL’si yerine gerçek içerik URL’si var.

| Proje ID | Eser / kaynak | Üretici |
| --- | --- | --- |
| SFX_FE_Tree_Fall | [Falling Tree... AI Generated.](https://pixabay.com/sound-effects/nature-falling-tree-ai-generated-431321/) | Mollyroselee |
| SFX_Ball_Kick | [Soccer Ball Kick](https://pixabay.com/sound-effects/film-special-effects-soccer-ball-kick-37625/) | bittermelonheart (Freesound); Pixabay/freesound_community |
| SFX_Cenk_Hurt | [male_hurt7](https://pixabay.com/sound-effects/people-male-hurt7-48124/) | micahlg (Freesound); Pixabay/freesound_community |
| SFX_FE_Shock | [Electric shock](https://pixabay.com/sound-effects/film-special-effects-electric-shock-97989/) | thepodcastdoctor (Freesound); Pixabay/freesound_community |
| SFX_Footstep_Wood_Loop | [Walking on wood](https://pixabay.com/sound-effects/film-special-effects-walking-on-wood-363349/) | Soumages |
| SFX_Footstep_Outdoor_Loop | [walking on sidewalk](https://pixabay.com/sound-effects/film-special-effects-walking-on-sidewalk-432109/) | flutie8211 |
| AMB_EQ_Assembly_Outdoor_Loop | [crowd of people talking](https://pixabay.com/sound-effects/people-crowd-of-people-talking-29054/) | SomeoneCool15 (Freesound); Pixabay/freesound_community |
| SFX_FE_Sweep_Dream | [Dream Sound Effect (Downscale)](https://pixabay.com/sound-effects/film-special-effects-dream-sound-effect-downscale-7134/) | Unicornaphobist (Freesound); Pixabay/freesound_community |

Lisans: [Pixabay Content License](https://pixabay.com/service/license-summary/), **CC0 değil**. Sekiz MP3’nin byte içeriği indirme dosyasıyla aynı; yeniden adlandırılmış. Çalma ses seviyesi ve loop ayarları dosya değişikliğinden ayrıdır. Kaynak/hash eşleşmesi, ham MP3’nin public dağıtımına tek başına izin vermez.

**Ağaç düşme ve ahşap adım** içerik sayfaları AI generated etiketi taşıyor. Bunlar dış sağlayıcının AI ses üretimi olarak da beyan edilmeli; hangi ses modelinin kullanıldığı açıklanmıyor. male_hurt7 eserinin adı/icracısı “çocuk sesi” olarak değiştirilmemeli; Cenk için oyunda kullanımı ayrıca açıklanmalı.

[Tam tekil kaynaklar](PIXABAY_KAYNAKLARI.json) · [Gerçek hash eşleştirmeleri](MP3_ORIJINAL_ESLESMELERI.json).

### 5.2 Açık ses kaynakları

SFX_FE_Sweep_Scream.mp3 ve SFX_FE_Wade_Loop.mp3 için orijinal indirme eşleşmesi bulunmadı. Assets/Audio/SFX/EmergencyBag/ altındaki 10 WAV için de açık üretim/indirme kaydı henüz yok. Bu dosyaların üreticisi, yöntemi, lisansı ve public dağıtım hakkı tamamlanmalı.`);
r=r.replace('Kesin AI araç/model/prompt listesi, tekil ses kaynakları ve izinler, final build/video/test/makbuz.','Kesin model/çıktı eşleştirmeleri; iki MP3, 10 çanta WAV’ı ve iki afet bulutu kaynağı; ham dağıtım izinleri; Eren Drive arşivi; final oynanış/video/makbuz.');
r=r.replace('Doğrudan ses üreten model kullanımı ayrıca sorulacak.','Ayrıca iki hazır Pixabay sesinin AI üretimi olduğu doğrulandı; dış üretici modelinin adı açıklanmıyor. Takımın doğrudan ses modeli kullanımı ayrıca teyit edilmeli.');
r=r.replace('her modelin kaydı','her modelin kaydı');
if(!r.includes('## 10. Ek kaynaklar ve release güncellemesi'))r+='\n## 10. Ek kaynaklar ve release güncellemesi\n\n- [Kaynakça](KAYNAKLAR.md): PixelLab ve sekiz tekil Pixabay kaydı.\n- [Release incelemesi](RELEASE_INCELEMESI.md): beş Windows ZIP’i indirildi, SHA256 ve dosya yapıları doğrulandı; EXE oynanış testi yapılmadı. v0.8 tag commit’i `0507fc3c480a11a7ad584267f085191cd639c6db`.\n- [WhatsApp promptları](WHATSAPP_PROMPT_KAYITLARI.md): 35 teknik prompt/tasarım kaydı, kişisel sohbet ayıklanmış. Promptların varlığı yapılmış iş kanıtı değildir.\n- [Berk kaynak eki](BERK_KAYNAK_RAPORU.md): 127 PNG + 5 metin/lisans dosyası, 132/132 kaynak/kopya hash eşleşmesi. İki menü afet bulutunun ilk kaynağı bilinmiyor.\n- [Berk devir notu](BERK_MENU_TESLIM.md): PR #18 ayarlar/duraklatma katkısı; bildirilen testler ekip beyanı olarak ayrı.\n- Eren’in Drive RAR kaynağı bağlantı olarak kaydedildi; bağlı hesap sorgusunda bulunmadı, tarayıcı giriş istedi. İçeriği okunmadığından asset listesine katıldığı iddia edilmedi.\n';
put('RAPOR.md',r);

let f=get('FORM_BEYANLARI_TASLAK.md');
f=f.replace('Karakter üretim hizmeti: **[KESİN ARAÇ ADI VE URL]**; diğer görsel araçları/modelleri: **[TAM LİSTE]**.','Karakter GIF üretim hizmeti: **PixelLab — https://www.pixellab.ai/**. Ekip genel üretim araçlarını ChatGPT, OpenAI Codex ve Claude/Claude Code olarak bildirdi; her JPEG’in tekil araç/model eşleştirmesi henüz tam değil.');
f=f.replace('Claude Code ve OpenAI Codex **[TAM ARAÇ/MODEL LİSTESİ]**','ChatGPT, Claude/Claude Code (Opus ailesi) ve OpenAI Codex (ekibin kullandığı arayüz adları Sol/Astra)');
f=f.replace('Repoda görev/prompt/devir kayıtları bulunur.','Repoda görev/devir kayıtları ve WhatsApp’tan ayıklanmış 35 uzun prompt/tasarım kaydı bulunur.');
f=f.replace('**[SUNO / ELEVENLABS / BAŞKA SES VEYA MÜZİK AI ARACI KULLANILDIYSA ADI, AMACI, DOSYALARI EKLENECEK; YOKSA AÇIKÇA “DOĞRUDAN SES MODELİ KULLANILMADI” YAZILACAK]**.','Hazır edinilen Mollyroselee ağaç düşme ve Soumages ahşap adım seslerinin Pixabay sayfaları AI generated etiketi taşıyor; dış üretici modelleri açıklanmıyor. **[TAKIM AYRICA DOĞRUDAN SES/MÜZİK MODELİ KULLANDIYSA ARAÇ VE DOSYA EKLENECEK; YOKLUĞU TAHMİN EDİLMEYECEK]**.');
f=block(f,'Pixabay\'dan kullanılan sesler:','### Kod — Kullanıldı',`Pixabay’dan kullanılan sekiz sesin tekil kaynakları, sanatçıları ve kullanım yerleri [KAYNAKLAR.md](KAYNAKLAR.md) ile [PIXABAY_KAYNAKLARI.json](PIXABAY_KAYNAKLARI.json) içinde listelenmiştir. Ağaç: Mollyroselee; top: bittermelonheart; Cenk acı tepkisi: micahlg; elektrik: thepodcastdoctor; ahşap adım: Soumages; dış adım: flutie8211; kalabalık: SomeoneCool15; rüya geçişi: Unicornaphobist. Freesound topluluğu kayıtlarının sağlayıcısı Pixabay/freesound_community olarak ayrıca belirtilir. Lisans **Pixabay Content License**; orijinal indirilen MP3’lerle byte içeriği aynı, yalnız dosya adları runtime ID’lerine dönüştürülmüş. Ağaç ve ahşap adım kayıtları AI etiketi taşıdığı için AI ses alanında da açıklanmıştır.

**[KALAN İKİ MP3: SFX_FE_Sweep_Scream / SFX_FE_Wade_Loop; 10 EmergencyBag WAV: KAYNAK, ÜRETİCİ, LİSANS TAMAMLANACAK]**. Ham MP3’lerin public repo dağıtım hakkı ayrıca kontrol edilecek.`);
f=f.replace('Proje içinde bulunan Unity/TextMesh Pro','Proje içinde bulunan Unity/TextMesh Pro');
if(!f.includes('Berk’in ek kaydı'))f+='\n## Berk’in ek kaydı\n\nBerk’in kaynakça paketine göre ui_kit.png Codex ile üretilen kaynak sayfası; BubbleFlood/Quake görselleri kırpma/kenar temizliğiyle türetilmiş, glow ve düşünce yolları kodla hazırlanmış. Menü şehir/efekt ve sel kapağı görselleri kodla üretilmiş. **bubble_flood_128.png ve bubble_quake_128.png ilk kaynakları henüz bilinmiyor**; bu nedenle hazır görsel alanı kesinleştirilmeden tamamlanmalı. [Berk kaynak raporu](BERK_KAYNAK_RAPORU.md).\n';
put('FORM_BEYANLARI_TASLAK.md',f);

let e=get('EKSIK_BILGILER.md');
const status='## Alınan cevaplar / kalanlar\n\n- **PixelLab doğrulandı:** 34 karakter GIF’i. Genel araçlar ChatGPT, Codex, Claude; kesin model alt sürümleri doğrulanmadı.\n- **Promptlar bulundu:** WhatsApp’tan 35 uzun oyun prompt/tasarım kaydı ayıklandı. Tekil görsel üretim promptu eşleştirmeleri henüz tam değil.\n- **Seslerin 8 kaynağı geri çıkarıldı:** orijinal MP3/hash eşleşmesi ve tekil Pixabay sayfası doğrulaması var. İki MP3 + 10 çanta WAV kaynağı açık. Ağaç ve ahşap adım sesleri AI etiketli.\n- **Berk:** 127 PNG ve 5 metin/lisans kaydı eklendi; iki afet bulutunun ilk kaynağı açık.\n- **Release:** Beş yayımlanmış ZIP/hash/yapı incelendi; v0.8 son kararlı. EXE oynanışı ve teslim videosu/makbuzu doğrulanmadı.\n- **Eren Drive arşivi:** Erişim sağlanamadı; dosya bu oturuma eklenirse incelenebilir.\n\nAşağıdaki ilk soru listesi tarihsel kontrol listesidir; yukarıda kapanmış maddeler yeniden sorulmayacak.\n\n';
if(!e.includes('## Alınan cevaplar / kalanlar'))e=e.replace('## Birinci tur',status+'## Birinci tur');
put('EKSIK_BILGILER.md',e);

let g=fs.readFileSync(path.resolve(__dirname,'galeri.cjs'),'utf8');
g=g.replace('Araç / model / orijinal prompt: teyit bekliyor.','Araç: ${h(a.provider)}. Model / tekil orijinal prompt: kaydı bulunduğu ölçüde eşleştirilecek.');
g=g.replace('Araç/model/prompt ve lisans teyidi bekleniyor.','Karakter GIF’leri için PixelLab doğrulandı. Genel araçlar ChatGPT/Codex/Claude; tekil model, prompt ve lisans eşleştirmeleri kısmen açık.');
g=g.replace('Araç/model ve orijinal promptlar teyit bekliyor.','Karakter GIF aracı PixelLab olarak doğrulandı. Tekil model/prompt eşleştirmeleri kısmen açık.');
g=g.replace('Bu iki ZIP yalnız görsel/GIF içeriyor. Tam orijinal görsel promptu, model adı veya hizmet kullanım şartı içermez.','İlk iki ZIP yalnız görsel/GIF içeriyor. Ayrıca sağlanan WhatsApp dökümünden 35 uzun teknik prompt/tasarım kaydı ayıklandı (WHATSAPP_PROMPT_KAYITLARI.md). Bu kayıtların her GIF/JPEG’in birebir üretim promptu olduğu varsayılmadı.');
fs.writeFileSync(path.resolve(__dirname,'galeri.cjs'),g);
console.log('Updated report, declarations, remaining questions and gallery generation.');
