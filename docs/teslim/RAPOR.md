# EŞİK — CIVIL GAME JAM uygunluk, AI ve kaynak raporu

**Durum: TASLAK — ekip teyidi ve final teslim kontrolleri bekleniyor.**  
**İnceleme:** 18 Ekim 2026 · saat dilimi UTC+3 / Europe–Istanbul.  
**Proje:** Eski belgelerde AFET UYANIŞI; güncel oyun adı EŞİK.  
**Owner:** Mahmut / MahmutErenCiftci · **Görev:** TESLİM-01 (REL-07 kaynak ve beyan hazırlığı).  
**İncelenen HEAD:** `6a852698e34975a8e54edc42ee614996ce4e1321` · `feature/mahmut-son-ayar`. Çalışma ağacında bu commit dışında yerel değişiklikler vardır; bu hash final buildin kaynağı olarak onaylanmamıştır.

Bu rapor, doğrulanmış kayıtları ve eksikleri birlikte gösterir. Organizasyonun uygunluk kararı, final build testi veya tamamlanmış teslim makbuzu yerine geçmez. Eksikler kapatılmadan “bütün kurallara eksiksiz uyulmuştur” beyanı verilmemelidir.

## 1. Belgeler ve ekler

| Dosya | İçerik |
| --- | --- |
| [FORM_BEYANLARI_TASLAK.md](FORM_BEYANLARI_TASLAK.md) | Web formunun beş AI ve beş hazır kaynak kategorisi için metin taslakları |
| [AI_GORSELLER_GALERI.html](AI_GORSELLER_GALERI.html) | Gönderilen bütün görsel/GIF'lerin önizlemeli galerisi |
| [AI_GORSELLER.md](AI_GORSELLER.md) | 73 dosyanın numaralı listesi, tekrarları ve repo eşleşmeleri |
| [AI_ARSIV_KAYITLARI.json](AI_ARSIV_KAYITLARI.json) | İki ZIP'in ve 73 dosyanın adları, SHA256 değerleri, beyan durumu |
| [SES_KAYNAKLARI.md](SES_KAYNAKLARI.md) | 110 ses dosyası için sağlayıcı, lisans durumu, kod/sahne referansı |
| [ASSET_ENVANTERI.md](ASSET_ENVANTERI.md) | Assets ve visuals altındaki 1.083 medya dosyasının kullanım kanıtları |
| [ENVANTER.json](ENVANTER.json) | Dosya hashleri, tüm referanslar, motor/paket/sahne kayıtları |
| [PAKET_ENVANTERI.md](PAKET_ENVANTERI.md) | Manifestteki 56 Unity paket/modül kaydı ve yerel lisans dosyaları |
| [SES_SENTEZ_DOGRULAMA.txt](SES_SENTEZ_DOGRULAMA.txt) | 34 WAV'ın üretim koduyla bit düzeyinde karşılaştırmasının gerçek çıktısı |
| [EKSIK_BILGILER.md](EKSIK_BILGILER.md) | Ekipten alınacak cevaplar ve final kapatma listesi |

## 2. Yarışma kuralları ve uygunluk durumu

Kaynak: [CIVIL GAME JAM — Kurallar](https://game.ktimo.com.tr/kurallar). Sayfa 18 Ekim 2026'da tarayıcı üzerinden okundu; sayfadaki PDF bağlantısının adı `CIVIL_GAME_JAM_Kural_Kitapcigi_v09.pdf`. Teslim anındaki güncel sürüm ve duyurular yeniden kontrol edilmelidir. Aşağıdakiler kuralların özetidir.

| Kural | Gereklilik | Bulgu / kapatma işlemi | Durum |
| --- | --- | --- | --- |
| 1.1–1.2; 2.1–2.6 | Onaylanmış katılımcılar, yüz yüze etkinlik, 2–4 kişilik kesin kadro, temsilci | Repoda Mahmut/Berk/Arda/Eren olmak üzere dört kişilik rol kaydı var. Organizasyon kaydı, tam adlar, çekilme/başka takım durumu ve temsilcilik yetkisi ekipten doğrulanmalı. | EKİP TEYİDİ |
| 1.3; 3.1 | Oyuna özgü geliştirme 16 Ekim 18.00–18 Ekim 18.00 UTC+3 içinde | İlk yerel Git geçmişi 16 Ekim 23.31'de başlıyor. Bu, daha önce çalışma yapılmadığının kanıtı değildir. Ön hazırlık ile oyuna özgü üretim ekipçe açıklanmalı. Arşiv dosya adları bağımsız zaman kanıtı değildir. | EKİP TEYİDİ |
| 3.2; 6.6 | Windows veya masaüstü tarayıcıda test edilmiş oyun, çalıştırma talimatı | Unity sürümü `6000.6.6f1`, Windows hedefi planlarda var. Bu incelemede final EXE/ZIP yolu verilmedi, oyun çalıştırılmadı. | UNTESTED |
| 3.3–3.5 | Her hazır kaynağın adı, sağlayıcısı, bağlantısı, lisansı, kullanım/değişikliği | Kenney/font kayıtları ve sekiz Pixabay MP3 kaynağı doğrulandı. İki MP3, çanta sesleri ve iki afet bulutu kaynağı eksik. Kaynak belirtmek kullanım iznini tek başına kanıtlamaz. | EKSİK |
| 3.6 | Dağıtımı kısıtlı üçüncü taraf ham içerik public repoda paylaşılmamalı | 10 MP3 Git'e kayıtlı. Hangilerinin hangi hakla public paylaşıldığı doğrulanmalı. Pixabay kökenli olanlar için ham dağıtım koşulları ayrıca incelenmeli. | AÇIK KONTROL |
| 3.7 | Build ile kaynak sürümü eşleşmeli; erişim bilgileri paylaşılmamalı | Final build kaynak hash'i bekleniyor. Çalışma ağacı kirli. Sınırlı anahtar/desen taramasında eşleşme çıkmadı; bu kapsamlı güvenlik incelemesi değildir. | EKSİK |
| 4.1–4.4 | AI serbest; beş kategoride doğru/eksiksiz ve hazır kaynaktan ayrı beyan | Ekip yoğun AI/vibe coding kullanıldığını bildirdi. Beş alan ayrı ele alındı; PixelLab, ChatGPT, Codex, Claude kullanımı ekipçe bildirildi. Kesin alt sürüm/çıktı eşleştirmeleri ve kalan kaynak kayıtları açık. | TASLAK |
| 5.1–5.3 | Resmî teslim ekranı; oyun bilgisi; ZIP ≤10 GB; ayrı MP4/AVI ≤1 dk ve ≤5 GB | Bu oturumda teslim yapılmadı. Beş GitHub release ZIP’i boyut/hash/yapı bakımından incelendi; teslim videosu ve gerçek oynanış testi henüz doğrulanmadı. | BEKLİYOR |
| 5.2; 6.5 | Public HTTPS Git repo | GitHub bağlayıcısı `MahmutErenCiftci/Gamejam` için `visibility=public` döndürdü; varsayılan dal `dev`. Katılımcı hesabı olmadan erişim ve final kaynak sürümü son kontrolde ayrıca doğrulanmalı. | PUBLIC DOĞRULANDI; SÜRÜM BEKLİYOR |
| 5.4–5.6 | Son başarılı sürüm, ZIP+video doğrulaması ve makbuz | Makbuz numarası/sürümü yok; yükleme başlatmak teslimin tamamlandığı anlamına gelmiyor. | BEKLİYOR |
| 6.1–6.4 | Yeni sürüm kaydı 18 Ekim 18.00 UTC+3'ten önce sunucuda oluşmalı | Zamanında açılmış aynı dosya aktarımı sonrasında tamamlanabilir; bu ek geliştirme süresi değildir. Genel süre uzatımı duyurusu ayrıca kontrol edilmeli. | TESLİMDE KONTROL |
| 7.1–7.3 | 100 puanlık ölçütler; video dahil toplam 3 dakika, soru-cevap yok | Eski `docs/JURY_DEMO.md` artık güncel akışı tam yansıtmıyor ve soru-cevap önerileri içeriyor. Gerçek build sırasına göre prova yapılmalı. | GÜNCELLEME / PROVA |
| 8 | Ödül temsilci ve takım düzeni | Uygulama dosyalarından doğrulanamaz; ekip ve temsilci bilgileri geçerli olmalı. | EKİP SORUMLULUĞU |
| 9.1–9.8 | Hak sahipliği ekipte; organizasyona verilen kullanım izni için ekip onayı | Üçüncü taraf lisansları genişletilemez. İlgili ekip hak sahiplerinin onayı ve temsilci yetkisi insan tarafından alınmalı. Bu rapor onay vermiyor. | EKİP ONAYI BEKLİYOR |
| 10 | Doğru beyan, inceleme kayıtları, davranış ve itiraz usulü | Bu rapor kaynak/üretim kayıtlarını bir araya getiriyor. Gizlenmiş kullanım veya doğrulanmamış kesinlik içeren metin gönderilmemeli. | EKSİKLER KAPATILMALI |
| 11; EK B–C | Cihaz/alan sorumluluğu, cihazın çıkarılması, tütün/alkol ve katılım düzeni | Repo üzerinden kontrol edilemez. Etkinlikteki fiilî uygunluğu ekip teyit etmeli. | EKİP TEYİDİ |

## 3. AI kullanımının açık beyanı

Ekip beyanı: Proje yoğun biçimde AI destekli/vibe coding yöntemiyle geliştirildi. Promptlar ve yaratıcı yönlendirmeler ekip tarafından hazırlandı. Bu durum, AI ile üretilmiş içeriğin elle yapılmış gibi gösterilmesi anlamına gelmez. Tasarım kararları, seçme/düzeltme, entegrasyon ve teslim sorumluluğu kayıtlı ekip üyelerindedir; hangi işlemlerin gerçekten elle yapıldığı ayrıca teyit edilmelidir.

| Kategori | Taslak seçim | Araç ve kayıt | Amaç / oyundaki yer |
| --- | --- | --- | --- |
| Görsel | KULLANILDI | Kullanıcının gönderdiği 39 JPEG + 34 karakter GIF’i; GIF aracı **PixelLab** olarak doğrulandı. Genel araç listesi ChatGPT, Codex ve Claude. JPEG bazında araç/model eşleştirmeleri tam değil. Berk’in 127 PNG kaynak eki de eklendi. | Konseptler, logo, karakter/yön animasyonları; arka plan, prop, UI ve tilemap üretimi/işlenmesi. Her dosya için kesin final kullanım ayrımı envanterde ve ekip teyidinde. |
| Ses | KULLANILDI | `Assets/Audio/AUDIO_LIST.md` Codex/Astra kaydı; `tools/audio/*.js`, final ses üreticileri ve `MenuAudioSynth.cs`. | AI destekli kodla sinüs/gürültü/filtre tabanlı müzik, ortam ve efekt üretimi; menü, deprem, sel, final, UI. Ayrıca iki hazır Pixabay sesinin AI üretimi olduğu doğrulandı; dış üretici modelinin adı açıklanmıyor. Takımın doğrudan ses modeli kullanımı ayrıca teyit edilmeli. Kenney/Pixabay sesleri hazır kaynakta ayrıca beyan edilecek. |
| Kod | KULLANILDI | ChatGPT, Claude/Claude Code (Opus), OpenAI Codex (ekip arayüz adları: Sol/Astra); görev, prompt ve devir kayıtları var. Kesin alt sürümler bağımsız doğrulanmadı. | Unity C# oynanış, input, sahne akışı, UI/diyalog/haber entegrasyonu, araç scriptleri, hata düzeltme. Bir aracın dosyada anılması her dosyanın o araçla üretildiğini kanıtlamaz. |
| Metin | KULLANILDI (araç dağılımı ekipten teyit) | Claude ile hazırlanmış PM/görev belgeleri; ekip yoğun AI kullanım beyanı. | Tasarım/planlama, görev ve prompt metinleri; oyun içi diyalog, haber, soru, feedback ve sonuç metinlerinin AI ile üretilen/düzenlenen kısmı ekipten ayrıntılandırılacak. |
| Diğer | KULLANILDI | Claude/Codex görev ve koordinasyon kayıtları; bu rapor Codex ile hazırlandı. | Planlama, teknik görev ayrıştırma, üretim araçları, asset dönüştürme, dokümantasyon, kaynak envanteri ve teslim beyanı taslağı. |

Ses dalga biçiminin doğrudan bir üretken ses modelinden gelmemesi, AI destekli ses üretim sürecini gizlemek için gerekçe yapılmamıştır. Form açıklamasında kullanılan yöntem açıkça yazılacaktır. Unity AI Assistant/Inference paketlerinin kurulu olması tek başına bu hizmetlerle içerik üretildiğini kanıtlamaz; `CLAUDE.md` abonelik bulunmadığına ilişkin not içeriyor. Gerçek kullanım ekipten doğrulanmalı.

### Üretim süreci ve gerçek prompt kayıtları

1. **Yönlendirme:** Ekip fikir, afet akışı, karakter, görsel üslup ve etkileşim ihtiyaçlarını tarif ettiğini bildirdi.
2. **Görsel üretim:** Gönderilen arşivlerdeki AI görselleri/GIF'leri üretildi. İlk iki görsel arşivinde model/prompt metni yok; sonradan sağlanan WhatsApp ZIP’inden 35 uzun prompt/tasarım kaydı ayıklandı. Bunların tekil görsellerle eşleştirmesi ayrıca gerekir.
3. **Dönüştürme/türetim:** `tools/gif2sheet.js` GIF → hizalı PNG sprite sheet dönüşümünü yapıyor. Menü `MenuArtBuilder` dosyaları kaynak panolardan parçalar hazırlıyor. Anne koruma pozu ve Cenk final pozları mevcut sprite'lardan türetilen kod içeriyor.
4. **Kodlama ve entegrasyon:** Claude/Codex görev kayıtları ve C# sistemleri mevcut. Hangi sürümün final buildte olduğu ayrıca sabitlenmeli.
5. **Ses üretimi:** AI destekli üretim kodu örnek ses indirmeden dalga biçimleri oluşturuyor. Hazır ses override'ları ayrıca yükleniyor.
6. **İnsan kontrolü:** Ekip kullanım alanlarını, promptları ve test sonuçlarını onaylayacak; bu inceleme insan testini yapılmış varsaymıyor.

| Gerçek kayıt | Niteliği | Beyanda kullanım |
| --- | --- | --- |
| `docs/SEL_ZIGZAG_PROMPT.md` | “Liderin promptu (birebir)” başlığı altında gerçek kod/uygulama isteği | Akşam sel yolu, kavşak, ağaç olayı ve etkileşim yönlendirmesinin ekipçe tarif edilmesi |
| `docs/SEL_ZIGZAG_GOREVLER.md` | Ekip üyeleri için hazır Claude görev promptları | Akış, UI, olay ve entegrasyon talimatları |
| `docs/GOREV_DAGILIMI.md` | Claude PM kaydı; Claude/Codex görev ve kabul kriterleri | AI kullanım sürecinin dokümante edilmesi |
| `agents/AGENT_PLAYBOOK.md` | Agent görev şablonları | Planlama/çalışma yöntemi; tamamının uygulanmış olduğunun kanıtı değil |
| `Assets/Art/Backgrounds/TilemapBedroom/source_manifest.json` | Stil ve üretim manifesti | Teknik referans; araç koşulları veya kesin lisans belgesi değil |
| İki kullanıcı ZIP'i | Üretim çıktıları | Kullanıcı AI kökenini beyan etti; özgün prompt/model kanıtı yerine geçmez |

Eksik gerçek promptlar uydurulmayacak. Sonradan yazılmış bir süreç özeti varsa “sonradan hazırlanmış özet” diye işaretlenecek.

## 4. AI görsel ve karakter eki

**İki arşivdeki tüm 73 dosya** `ai_gorseller/` altına eklendi: 39 JPEG ve 34 GIF. 69 benzersiz SHA256 var; 4 tekrar silinmedi. 20 dosyanın repoda birebir byte eşleşmesi bulundu. Bu sayı, 20'sinin final ekranda göründüğü anlamına gelmez.

- Logo görseli **AI-001**, `Assets/Art/Menu/Source~/logo_esik.jpg` ile birebir eşleşti. Menü logo üretim kodu bu kaynak dosyasını tarif ediyor.
- Zikzak sel konsepti **AI-015**, `visuals/concept/sel_aksam_zigzag_ref.jpeg` ile eşleşti; ilgili gerçek prompt bu dosyayı referans alıyor.
- Anne ve önceki çocuk/Arda GIF'lerinin 18'i `visuals/characters/` kaynaklarıyla eşleşti.
- Cenk ve Zeynep GIF'leri de tam olarak eklendi. Dönüştürülmüş veya türetilmiş PNG ilişkisi yalnız isim/benzerlikten kesinleştirilmedi.

Konsept panoları final oyundan alınmış ekran görüntüsü diye sunulmayacak. Eski konseptlerde görünen metinler, davranışlar ve sayaçlar finalde uygulanmış veya eğitim doğruluğu onaylanmış kabul edilmeyecek. Bu arşivlere/repo dosyalarına girmeyen başka sohbet çıktılarının da eklendiği iddia edilmez.

## 5. Ses efektleri ve müzik kaynakları

### 5.1 Orijinal MP3 dosyalarından geri çıkarılan sekiz kaynak

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

SFX_FE_Sweep_Scream.mp3 ve SFX_FE_Wade_Loop.mp3 için orijinal indirme eşleşmesi bulunmadı. Assets/Audio/SFX/EmergencyBag/ altındaki 10 WAV için de açık üretim/indirme kaydı henüz yok. Bu dosyaların üreticisi, yöntemi, lisansı ve public dağıtım hakkı tamamlanmalı.

### 5.3 Kenney — doğrulanmış hazır kaynak kayıtları

Sağlayıcı: **Kenney**. İki resmî ürün sayfası CC0 gösteriyor; yerel paket lisansları ve dosya eşleştirme manifesti mevcut. Değişiklik: manifestte OGG içeriğinin değişmediği, çalışma zamanı ID'sine göre adlandırıldığı yazıyor; indirme ZIP hashleri de kayıtlı. Bu oturumda paket ZIP'leri yeniden indirilip karşılaştırılmadı.

| Paket / orijinal dosya | Projedeki dosya | Kullanım |
| --- | --- | --- |
| [Interface Sounds](https://kenney.nl/assets/interface-sounds) / `click_002.ogg` | `Overrides/SFX_UI_Click.ogg` | UI tıklama |
| Aynı / `select_001.ogg` | `Overrides/SFX_UI_Hover.ogg` | UI üzerine gelme/seçme |
| Aynı / `back_001.ogg` | `Overrides/SFX_UI_Back.ogg` | Geri işlemi |
| Aynı / `confirmation_001.ogg` | `Overrides/SFX_Correct.ogg` | Doğru seçim |
| Aynı / `error_001.ogg` | `Overrides/SFX_Wrong.ogg` | Yanlış seçim |
| [Impact Sounds](https://kenney.nl/assets/impact-sounds) / `impactWood_light_000.ogg` | `Overrides/SFX_Books_Fall.ogg` | Ahşap/kitap düşmesi |
| Aynı / `impactGlass_light_000.ogg` | `Overrides/SFX_Glass_Crack.ogg` | Cam çatlaması |

Dosyalar `Assets/Resources/Audio/` altındadır. Lisanslar: `Assets/Audio/Licenses/Kenney_Interface_Sounds.txt`, `Kenney_Impact_Sounds.txt`; manifest: `tools/audio/cc0_sources.json`. `GameAudioRuntime.Load` önce Overrides klasörüne baktığı için bu 7 hazır ses, aynı ID'deki sentez WAV yedeğinin önüne geçer.

### 5.4 Prosedürel sesler ve müzik

`tools/audio/generate_audio.js --check` bu incelemede **PASS** verdi: 34 mevcut WAV, üretim kodunun çıktısıyla bit düzeyinde eşleşiyor. Üç müzik döngüsü, yağmur/oda ortamları, alarm/telefon, UI, deprem gürleme/takırtı ve adım örnekleri bu kümede. Doğrudan ses modeli yerine AI destekli kodla dalga biçimi üretimi olarak beyan edilecek.

Ek üretim kayıtları: `generate_flood.js`, `generate_flood_hazards.js`, `generate_polish_sfx.js`, `generate_final_sfx.js`, `generate_tape.js`; `tools/sel_aksam_final/{world,ui}/generate_final_sfx.js`; menüde `MenuAudioSynth.cs`. Bu ek kümelerin tamamı için bit düzeyinde karşılaştırma yapılmadı. Eski WAV üreteci bulunması, sonradan onun yerine konmuş aynı adlı MP3'ün de özgün sentez olduğunu kanıtlamaz.

## 6. Fontlar ve değişiklik beyanı

| Kaynak / sağlayıcı | Lisans | Oyundaki dosya/kullanım kanıtı | Değişiklik |
| --- | --- | --- | --- |
| [Pixel Operator](https://fontlibrary.org/en/font/pixel-operator) — Jayvee Enaguas / HarvettFox96 | CC0 1.0 | `PixelOperatorTR-Bold.ttf`, `PixelOperatorTR-Heavy.ttf`; UI/DialogueRunner ve font kaynak varlığı referansları | TR karakter ve simgeleri eklendi; isim Pixel Operator TR yapıldı; Heavy ek kalın kesim türetildi. İşlem `README.txt`, `Tools~/make_tr.py`, `make_heavy.py` ile kayıtlı. |
| [Pixelify Sans](https://github.com/eifetx/Pixelify-Sans) — proje yazarları | SIL OFL 1.1 | Regular/Bold; görev HUD'u, sel PIN ekranı, oyun sonu ve ortak font varlığı | Kaynak font değişikliği doğrulanmadı; import/atlas kullanımından ayrı tutulmalı. |
| [Press Start 2P](https://fonts.google.com/specimen/Press+Start+2P) — proje yazarları, yerel telif kaydı Cody adresini içeriyor | SIL OFL 1.1 | Menü, pause ve ortak font varlığı | Değişiklik kaydı bulunmadı. |
| [Nunito](https://github.com/googlefonts/nunito) — Nunito Project Authors | SIL OFL 1.1 | Black; Dialogue ve Resources/Objectives kopyaları. Resources varlığı build adayı; fiilen gösterim doğrulanmadı. | İki kopya aynı kaynak ailesi; ayrı özgün font gibi sunulmaz. |
| [Liberation Sans](https://github.com/liberationfonts/liberation-fonts) — yerel bildirimde Google/Red Hat | SIL OFL 1.1 | TextMesh Pro font ve SDF/fallback varlıkları | Unity/TMP dağıtımıyla gelen hazır font; son build kullanımı teyit edilmeli. |

Tam yerel telif/lisans dosyaları korunmalı ve dağıtılan fontlara ilişkin bildirimler final pakete uygun biçimde eklenmelidir. Bu rapor mevcut üçüncü taraf eserleri ekibe ait veya CC0 ilan etmez.

## 7. Hazır kod, araçlar ve metin referansları

- **Unity 6000.6.6f1 / Unity Technologies:** C#, URP, Input System, Tilemap, UI, animasyon ve ilgili motor bileşenleri. Manifestte 56 paket/modül girdisi var; bunların hepsi final oyunda çalışan kod değildir. Paket adı/sürümü ve yerel LICENSE/Third Party Notices kayıtları ekte. Bazı incelenen Unity paketlerinde Unity Companion License yazıyor; tüm paketler tek lisansa genellenmemeli.
- **TextMesh Pro:** `Assets/TextMesh Pro/` içinde hazır shader, font, materyal ve kaynak dosyaları mevcut. Motor bileşenleri ekibin özgün kodu gibi beyan edilmemeli. İlgili Unity paket bildirimleri final dağıtıma göre kontrol edilmeli.
- **Üretim araçları:** Node.js scriptleri ve fontTools kullanan Python font dönüştürme kodu var. Bunlar oyun motorundan ayrı geliştirme araçlarıdır. Harici script/kod/Unity Asset Store paketi veya eğitimden kopya kullanıldıysa ekip eklemeli; bulunamayan kaynağa otomatik “yok” denmemeli.
- **Metin/eğitim referansları:** `qa/STOVE_HANDOFF.md` ve `qa/QUAKE_BALANCE_HANDOFF.md`, [AFAD deprem önlemleri](https://www.afad.gov.tr/deprem-oncesi-ani-ve-sonrasi-onlemini-al-guvende-kal) ve [ilk 72 saat PDF'ine](https://www.afad.gov.tr/kurumlar/afad.gov.tr/3473/xfiles/ilk72saat1.pdf) referans veriyor. Bu oturum bütün eğitsel içeriğin resmî doğrulaması değildir. Bilgiden yararlanma ile birebir metin/görsel kopyalama ayrılmalı; kopyalandıysa izin/atıf koşulları ayrıca yazılmalı.

## 8. Teknik incelemenin sınırları ve final teslim

Tarama 964 görsel, 110 ses ve 9 font dosyasını kapsıyor; 9 dosya 5 font ailesine ait. Bunlar kullanılan benzersiz asset sayıları değildir. Kaynak kopyaları, alternatifler, referanslar, eski sürümler ve sentez yedekleri sayıya dahildir. Kullanıcı arşivindeki 73 ek dosya bu 1.083 repo medyasından ayrı sayılır; hash eşleşmeleri tekrar içerir.

`EditorBuildSettings.asset` içinde etkin sahneler: `MainMenu`, `EarthquakeHome`, `FloodSchool`, `FloodEvening`, `GameEnd`. Etkin Build Profile'ın bunları değiştirmediği ve final EXE'nin bu ayarlarla alındığı doğrulanmadı. Prefab/sahne GUID grafiği ve Resources taraması bir assetin görüntülendiğini, sesin çaldığını veya script yolunun çalıştığını ispatlamaz.

Bu incelemede Unity Play Mode / EXE / final ZIP smoke testi yapılmadı. Test edilecek yol: **Menü → seçilen ilk afet → sonuç → diğer afet → sonuç → GameEnd → Menü**; ters bölüm sırası ve tekrar/menü çıkışları da final sürümde denenmeli. Eski `AGENTS.md` ile yeni akış belgeleri arasında kapsam/akış çelişkileri var; bu, yarışma ihlali sonucunu doğurmaz fakat eski planın tamamlandığı iddia edilmemelidir. Bağlayıcı iç belge güncellemesi PM kararı gerektirir.

Final için kaydedilecekler: takım/resmî tema; final EXE ve ZIP adı/boyutu/SHA256; kaynak commit/branch; sonradan değişiklik içerip içermediği; testçi/tarih/sonuç; ayrı video dosyası/süresi/boyutu; nihai beyan; hak sahipleri onayı; teslim makbuzu/sürüm/zaman. Süre sonunda yeni geliştirme yapılmadan teslim edilen kaynak sürümü korunmalı.

## 9. Görev teslim kaydı

**Değiştirilen dosyalar:** Bu görev yalnız `docs/teslim/**` ve `tools/teslim/**` altında belge/ek/rapor araçları oluşturdu. Başlangıçta var olan Unity değişiklikleri bu rapora ait değildir.  
**Ne çalışır:** Dosya envanteri, ses kaynağı kaydı, 73 görsel/GIF eki, galeri ve form taslağı oluşturuldu.  
**Nasıl test edilir:** `node tools/teslim/envanter.cjs`; sonra `node tools/teslim/galeri.cjs`; galeri filtreleri ve dosya bağlantıları kontrol edilir. Bunlar yalnız rapor çıktıları üretir; oyun buildi üretmez.  
**Test sonucu:** 34 WAV üretim karşılaştırması PASS; 73 kopyanın hash/boyut kontrolü PASS. Oyun/build UNTESTED.  
**Açık blokaj:** Kesin model/çıktı eşleştirmeleri; iki MP3, 10 çanta WAV’ı ve iki afet bulutu kaynağı; ham dağıtım izinleri; Eren Drive arşivi; final oynanış/video/makbuz.  
**Sonraki sahibine teslim:** Mahmut kaynak/araç cevaplarını toplar; ilgili ekip üyeleri kendi alanlarını teyit eder; temsilci final beyanını gerçek build ve kaynak kayıtlarına göre tamamlar.

## 10. Ek kaynaklar ve release güncellemesi

- [Kaynakça](KAYNAKLAR.md): PixelLab ve sekiz tekil Pixabay kaydı.
- [Release incelemesi](RELEASE_INCELEMESI.md): beş Windows ZIP’i indirildi, SHA256 ve dosya yapıları doğrulandı; EXE oynanış testi yapılmadı. v0.8 tag commit’i `0507fc3c480a11a7ad584267f085191cd639c6db`.
- [WhatsApp promptları](WHATSAPP_PROMPT_KAYITLARI.md): 35 teknik prompt/tasarım kaydı, kişisel sohbet ayıklanmış. Promptların varlığı yapılmış iş kanıtı değildir.
- [Berk kaynak eki](BERK_KAYNAK_RAPORU.md): 127 PNG + 5 metin/lisans dosyası, 132/132 kaynak/kopya hash eşleşmesi. İki menü afet bulutunun ilk kaynağı bilinmiyor.
- [Berk devir notu](BERK_MENU_TESLIM.md): PR #18 ayarlar/duraklatma katkısı; bildirilen testler ekip beyanı olarak ayrı.
- Eren’in Drive RAR kaynağı bağlantı olarak kaydedildi; bağlı hesap sorgusunda bulunmadı, tarayıcı giriş istedi. İçeriği okunmadığından asset listesine katıldığı iddia edilmedi.
