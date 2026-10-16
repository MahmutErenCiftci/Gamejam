# AGENTS.md — AFET UYANIŞI / Bağlayıcı 48 Saatlik Çalışma Sözleşmesi

**Durum:** ZORUNLU · **Sürüm:** 2.0 · **Ekip:** 4 kişi · **Motor:** Unity 2D + C# · **Kapsam:** Sel **ve** Deprem oynanabilir.  
Bu belge, ekip üyeleri ve kullanılan bütün AI kodlama agent'ları için tek yetkili kurallar belgesidir. Başka doküman çelişirse **AGENTS.md geçerlidir**. Ürün/süre değişikliği yalnızca insan PM'in açık onayı ve belge güncellemesiyle mümkündür.

## 1. Değiştirilemez P0 hedefleri

1. **48 saat sonunda oynanabilir ve Windows'ta çalışır bir build.** Planlama zamanı `T+0` jam başlangıcıdır.
2. Menü: 5 saniyelik açılış; karanlık → kırmızı **07:00** → çocuk odası → uyuyan çocuk → **4 afet balonu** → “AFET UYANIŞI”, Yeni Oyun / Ayarlar / Çıkış.
3. **SEL oynanabilir:** uyanma + el-yüz yıkama + TV hava uyarısı + bilinçsiz anne diyaloğu + okul yolunda şiddetli yağmur/kapalı yol + kulaktan kulağa yanlış bilgi + güvenli karar + öğrenme ekranı.
4. **DEPREM oynanabilir:** evinde gündelik sabahını yaşayan farklı bir yetişkin → kurgu içi uyarı → beş seçeneğin 5 saniyede sunulması → sarsıntı başlar → karar sonucu, doğru davranış ve öğrenme ekranı. Oyuncuya erken uyarının her zaman/garanti olduğu öğretilmez.
5. **ORMAN YANGINI** ve **HEYELAN** ana menüde görünecek ancak 'Yakında' olarak **kilitli**; boş sahne yok.
6. Türkçe UI, okunur metin, en az bir klavye ve mouse kullanımı, menüye geri dönüş.
7. Her iki bölümün eğitsel mesajı doğruluk açısından gözden geçirilmiş; gerçek afette güvenli olmayan davranış ödüllendirilmiyor.

## 2. Öncelik ve saat kapıları

| Zaman | Mutlaka görülebilecek şey | Geçiş kararı |
|---|---|---|
| T+0–4 | Unity proje düzeni, roller, repoda AGENTS, ilk sahne | K1+PM onayı |
| T+4–10 | Menü prototipi ve **ilk Windows build**; iki bölüm için çalışan basit sahne | K1+PM onayı |
| T+10–18 | Sel: uyanış/haber/anne dikey kesiti; Deprem: 5 seçim sayacı prototipi | Her iki sahip demo gösterir |
| T+18–28 | Sel güvenli karar & kapanış; Deprem tam olay akışı | **Her iki bölüm baştan sona oynanabilir** |
| T+28–36 | Entegrasyon, minimum sanat/ses polish, davranış doğruluğu, iki bölümün hatalarını kapatma | PM P0 kabulü |
| **T+36** | **FEATURE FREEZE**; yeni oyun özelliği ve yeni sahne yok | Herkes uyar |
| T+36–44 | Hata düzeltme, regresyon, farklı PC'de build, yedek | K1+K4 çıkış kontrolü |
| T+44–48 | Final Windows build ve jüri sunum provası/teslim | PM son onayı |

**Karar kuralı:** T+18'de herhangi bir bölüm çalışmıyorsa animasyonu, serbest dolaşmayı ve yan sahneleri küçült. T+28'de iki bölüm de bitmediyse polish ve opsiyonel tüm özellikleri kes. **İki bölümün temel akışından vazgeçilmez**; gerektiğinde her biri 1–3 sahnede ilerler.

## 3. Tekil dosya ve görev sahipliği

> **GÜNCEL: Bu ekipte roller farklı dağıtıldı.** Kişi, dosya ve sahne sahipliği için [`planning/EKIP_ROLLERI.md`](planning/EKIP_ROLLERI.md) geçerlidir (Mahmut: lider/genel + bölüm sahneleri, Berk: giriş sayfası + UI, Arda: diyalog, Eren: haber). Aşağıdaki K1–K4 tablosu paketin orijinal önerisidir; çelişirse EKIP_ROLLERI.md kazanır. Bu belgedeki diğer kurallar (sahne kilidi mantığı, agent kuralları, güvenlik) aynen geçerlidir.

| Kişi | Ünvan | Sahip olduğu dosyalar | En önemli teslim |
|---|---|---|---|
| **K1** | Technical Lead / Menu / Integration | `Assets/Scenes/Menu/`, `Assets/Scripts/Core/`, `Assets/Scripts/Menu/`, `ProjectSettings/`, `Packages/`, build konfigürasyonu | Çalışır menü, sahne geçişi, ilk/final build |
| **K2** | Flood Gameplay Developer | `Assets/Scenes/Chapters/Flood/`, `Assets/Scripts/Chapters/Flood/`, paylaşılan gameplay için K1'in belirlediği kendi dosyaları | Baştan sona Sel bölümü |
| **K3** | Visual/Animation + Earthquake Developer | `Assets/Art/`, `Assets/Animations/`, `Assets/Prefabs/Visual/`, `Assets/Scenes/Chapters/Earthquake/`, `Assets/Scripts/Chapters/Earthquake/` | Görseller ve oynanabilir Deprem bölümü |
| **K4** | Product/Narrative/QA/Sound | `Assets/Data/`, `Assets/Audio/`, `qa/`, `story/`; organizasyon ve test görevleri | Diyalog/veri, doğruluk, QA ve jüri demosu |

**SAHNE KİLİDİ:** `MainMenu.unity` sadece K1; `Flood*.unity` sadece K2; `Earthquake*.unity` sadece K3 tarafından düzenlenir. Bir başkasının sahnesine değişiklik: görev açıklamasında açık devretme + ilgili sahip onayı gerekir.

**Paylaşılan kod sözleşmesi:** K1, `Assets/Scripts/Shared/` arayüzlerini tanımlar. K2/K3 önce kontratı okur, kendi dosyalarında uygular; K1 izni olmadan ortak interface, namespace, input ayarı veya oyun yöneticisini değiştirmez. K4, C# scripti ile değil veri dosyaları ile içerik teslim eder.

## 4. Agent davranış kuralları

- **Her agent önce AGENTS.md ve kendi işiyle ilgili belgenin tamamını okur.** Görev olmadan dosya değişikliği yapmaz.
- Başlamadan önce şu formatta 1–3 satır bildir: `Görev ID | değişecek dosyalar | bağımlılık/riski`.
- Bir agent yalnızca atanmış **owner path** içindeki dosyaları değiştirir. Başka path gerekiyorsa durur ve PM/K1 izni ister.
- Başka kişinin sahnesini, prefabını, `.meta` dosyasını veya ProjectSettings'i sessizce düzenleme/silme/taşıma.
- Repo genelinde otomatik refactor, mimari yeniden yazımı, paket yükseltmesi, asset indirme, C# namespace değişikliği **yasak** (PM+K1 izni olmadan).
- Mevcut Unity sürümünü, render pipeline'ını ve Input System kurulumunu koru. Varsayımdan “Unity kurulumunu düzeltiyorum” diye toplu değişiklik yapma.
- İnternetten lisansı belirsiz sprite/ses/fon çekme. PNG'lerin görsel referans ile gerçek animasyon sprite sheet'i arasındaki ayrımını koru.
- Doğrulamadığın hiçbir işlem için `test edildi`, `derlendi`, `çalışıyor` deme.
- Bitirirken rapor: `Değiştirilen dosyalar`, `Ne çalışır`, `Nasıl test edilir`, `Test sonucu`, `Açık blokaj`, `Sonraki sahibine teslim`.
- Doğrulanmış, küçük PR'lar; `main` dalına doğrudan push yok. `dev` her entegrasyondan sonra oynanabilir kalmalıdır.
- PM kararı olmadan otomatik birden fazla agent'ın aynı dosyayı eşzamanlı değiştirmesine izin verilmez.

## 5. Oyun / içerik güvenlik ve doğruluk

- **Sel:** Suyla kaplı yoldan yürüme veya araçla geçme, akıntıya girme, köprü/sel kenarına yaklaşma, kaynağı belirsiz söylentiyle risk alma ödüllendirilmez. Güvenli yerde kalma, resmî açıklamaları doğrulama ve yetkili yönlendirmesine uyma vurgulanır.
- **Deprem:** Sarsıntı başladıysa güvenli konumda **Çök–Kapan–Tutun** (mümkünse sağlam masa altı); camlardan, ağır devrilebilecek eşyalardan uzak durma. Sarsıntı sırasında binadan koşarak çıkmayı, balkona yönelmeyi, asansör/merdiven kullanmayı doğru seçenek göstermeyin. Yerel resmî tavsiyelerle son kontrol gerekir.
- Gerçek kurumlardan gelmiş gibi sahte bildirim/garanti üretme. Renk kodları ülke/kuruma göre değişebilir. Deprem erken uyarısı herkese her yerde mutlaka 5 saniye önceden gelir iddiasında bulunma.
- Çocuklara tehlikeli davranışları özendirecek oynanış/puanlama kurma. Yalnızca güvenli tercihi anlatan ve yanlış tercihi açıklayan feedback kullan.

## 6. Görev tanımı ve bitiş kriterleri

**Her issue** şu alanları içerir: `ID`, `owner`, `tahmini bitiş`, `bağımlılıklar`, `değiştirilecek dosyalar`, `acceptance criteria`, `test adımları`.

**Done ancak şu durumda:**
- [ ] İlgili sahne açılıyor ve senaryo sonuna ulaşılabiliyor.
- [ ] Unity Console'da o değişikliğin neden olduğu hata yok; eksik referans yok.
- [ ] En az bir klavye/mouse girdi testi yapıldı (uygunsa).
- [ ] Türkçe karakterler/UI ekran dışına taşmıyor.
- [ ] Owner değişiklikleri kontrol etti; PR incelemesi var.
- [ ] Test koşulamadıysa `UNTESTED` etiketi ve neden yazıldı; *Done* denmedi.
- [ ] `dev` dalına birleştirildikten sonra menü + Sel + Deprem smoke test geçti.

## 7. Toplantı, blokaj, iletişim

- Başlangıçta 15–20 dk kickoff; devamında yaklaşık her 6 saatte 10 dk durum toplantısı. Gerçek uyku/mola dönüşümlü planlanır.
- Durum formatı: `Bitti / Şu an / Engel / Tahmini bitiş / Yardım ihtiyacı`.
- **20 dakikadan fazla ilerleyemeyen** kişi blokajı takım kanalına bildirir. Kodunu gizlice başkasının dosyasında çözmeye çalışmaz.
- Acil çatışmayı **K1 teknik**, özellik/süre çatışmasını **PM ürün**, doğru bilgi sorularını **K4 içerik** karara bağlar.
- PR merge yetkisi K1'e (**MahmutErenCiftci**); GitHub kuralıyla zorunludur, `dev` ve `main`'e başka kimse birleştiremez. PM içerik/öncelik onayı K4'te olabilir ama ekip baştan belirler.
- Git dal adları ve korumalar için `CLAUDE.md` ve `technical/GIT_AND_MERGE.md` "Bu repoya özel" bölümü geçerlidir (`develop` yerine `dev`).

## 8. Test ve teslim

- T+10'a kadar birinci Windows build; T+28'de iki tamamlanmış demo; T+36 feature freeze.
- T+44'te final RC: `MainMenu → Flood → Result → MainMenu → Earthquake → Result → MainMenu` başarıyla test edilmiş olmalı.
- Paket: çalıştırılabilir Windows build klasörü, `README_PLAY.md`, tanıtım ekran görüntüleri, kısa demo konuşma planı.
- Eğer Unity kurulu değilse agent `build aldım` diyemez; yalnız statik inceleme raporlar.

## 9. Okunması gereken diğer belgeler

`START_HERE.md`, `docs/GAME_DESIGN_DOCUMENT.md`, `story/FLOOD_CHAPTER.md`, `story/EARTHQUAKE_CHAPTER.md`, `technical/UNITY_PLAN.md`, `technical/INTERFACE_CONTRACTS.md`, `planning/48H_TIMELINE.md`, `planning/OWNERS_AND_HANDOFFS.md`, `technical/GIT_AND_MERGE.md`, `qa/TEST_PLAN.md`.

> **SON KURAL:** Bu repo bir game jam reposudur. Mühendislik mükemmelliği ancak oyunu 48 saatte teslim etmeye hizmet ediyorsa değerlidir. Her zaman küçük, test edilebilir, başkasının alanına taşmayan bir değişikliği seç.
