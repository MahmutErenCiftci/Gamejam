# AFET UYANIŞI — Görev Dağılımı (son 24 saat)

**Tarih:** 17 Ekim 2026, 23:15 · **Hazırlayan:** PM (Claude) · **Onay:** Mahmut
**İlke:** Agent'lar temeli atar, ekip kendi alanında devam ettirir. Herkes yalnızca kendi klasöründe çalışır; sahneye Mahmut yerleştirir.

> **Saat varsayımı:** T+0 = 16 Eki 23:30 (ilk commit) → **şu an T+24**. **Feature freeze T+36 = 18 Eki 11:30**, **final teslim T+48 = 18 Eki 23:30**. Gerçek teslim saati farklıysa bütün tarihleri aynı farkla kaydırın (Mahmut karar #1).
> Satırların kopyası `planning/TASK_BOARD.csv` dosyasında (eski satırlarda biten işler DONE yapıldı; eski sabah seli satırları FLOOD-03/04/05 ve STORY-02 **CUT** = yerine yeni akış geldi). Öncelik: **P0** teslim için şart · **P1** önemli · **P2** vakit kalırsa (freeze'de bitmemişse kesilir).

---

## 1. Kapılar ve bağımlılıklar

| Kapı | Ne | Kim | Ne zaman | Açtığı işler |
|---|---|---|---|---|
| **K0 = REL-01** | **PR #12 `dev`'e birleşir** (Berk menüsü, ses paketi, Arda'nın etkileşim/denge/ocak işi, deprem düzeltmeleri, 19 NPC). Mahmut gruba "dev'i çekin" yazar. | Mahmut | **Hemen** (T+24,5) | **PR #12 birleşmeden kimse yeni dal açmaz.** Sonrasında: EQ-10, EQ-11, UI-11, NEWS-06 |
| **K1 = SEL2-01** | **Akşam sel temeli `dev`'de**: `FloodEvening.unity` + `Assets/Scripts/Chapters/FloodEvening/` + `docs/SEL_AKSAM_DEVIR.md` (LEAD agent'ı yazıyor) ayrı PR ile birleşir. | Mahmut + agent'lar | T+26 (18 Eki 01:30); en geç sabah 08:00 | Sel işleri: SEL2-10, SEL2-20, SEL2-21, UI-10, MENU-10, CONTENT-02 ve agent işleri EQ-01, EQ-02, AUD-01 |
| **K2 = Ekip PR'ları** | Ekibin bütün P0/P1 PR'ları `dev`'e açılmış | Berk, Arda, Eren | **T+35 (18 Eki 10:30)** | Mahmut sahneye yerleştirir (SEL2-02) |
| **K3 = REL-03** | **FEATURE FREEZE + RC0 build** (v0.5-onizleme) | Mahmut | **T+36 (11:30)** | QA-03, QA-04, REL-05, REL-06. Bundan sonra yeni mekanik yok, sadece hata düzeltme |
| **K4 = REL-04** | RC1 + başka PC'de test + ZIP | Mahmut + Berk | T+44 (19:30) | REL-08 prova ve teslim |

Önemli notlar:
- **Sel temeli lokal dalda (`feature/mahmut-telefon-ses`) commit edilmemiş duruyor.** LEAD agent'ı Berk'in `MenuChapterLoader.cs` dosyasına ve Mahmut'un `ResultReportView.cs` / `EarthquakeResultManager.cs` dosyalarına dokundu. Bu yüzden **MENU-10, EQ-02 ve EQ-01 ancak K1'den sonra** başlar (yoksa çakışma çıkar).
- Eski sabah sel akışı (Bedroom → Bathroom → FloodChapter) ve Eren'in sele kapılma ara sahnesi **artık oyunda yok**. Dosyalar silinmez; kimse bunlara iş harcamaz.
- Arda'nın `Assets/Scripts/UI/InteractionPromptUI.cs` dosyası Berk'in klasöründe ama **sahibi Arda** (lider kararı). Berk bu dosyaya dokunmaz.

---

## 2. Kişi kişi görevler

### Berk — ana menü + ortak UI

| ID | Ö | İş | Dosyalar | Kabul kriteri | Süre | Bağımlılık | Bitiş |
|---|---|---|---|---|---|---|---|
| **MENU-10** | P0 | Menüyü yeni bölüm sırasına uydur | `Assets/Scenes/Menu/MainMenu.unity`, `Assets/Scripts/Menu/**`, `Assets/Art/Menu/**` | Varsayılan seçim **Deprem** (Deprem → Sel zinciri). Sel balonu tek başına `FloodEvening`'i açar. Sel önizlemesinde ve metinlerinde "sabah / okul yolu / otobüs" ifadesi yok, akşam mahalle seline uygun. Yangın ve Heyelan kilitli ("Yakında"). Menü → Deprem → rapor → Sel → Menü döngüsü takılmıyor. | 1,5 sa | K1 (`MenuChapterLoader.cs` LEAD'in değişikliğiyle gelir) | T+31 |
| **UI-10** | P0 | Sel bölümü arayüz cilası: "SEL BÖLÜMÜ" kartı, SU SEVİYESİ göstergesi, sonuç ekranı, kapı ve şifre panelleri | `Assets/Scripts/Chapters/FloodEvening/UI/**` (**Mahmut devrederse**, bkz. karar #4), `Assets/Art/UI/FloodEvening/**` | Görünüm deprem arayüzüyle tutarlı (koyu panel, turkuaz vurgu, Nunito). 1280x720 ve 1920x1080'de Türkçe metin taşmıyor. Tehlike mesajlarında renk + ikon + açıklama birlikte var. `FloodEvening.unity`'ye dokunulmadı. Devir yoksa: öneri listesi + mock PNG `Assets/Art/UI/FloodEvening/`'e, Mahmut'un agent'ı uygular. | 3 sa | K1 | T+35 |
| **UI-11** | P1 | Menüde "Kontroller" ekranı (Ayarlar içinde ya da ayrı satır) | `Assets/Scripts/Menu/**`, `MainMenu.unity` | WASD/oklar, E, Space/Enter, 1–5, fare, Esc listesi. Klavye ve fareyle açılıp kapanıyor. Metin `OYUN_AKISI §3` ile aynı. | 1,5 sa | K0 | T+33 |
| **UI-12** | P2 | Duraklatma paneli (Esc: Devam / Bölümü Yeniden Başlat / Menüye Dön) | `Assets/Prefabs/UI/PausePanel.prefab`, `Assets/Scripts/UI/PausePanel.cs` | `GameInput.BackPressed` + `GameSceneRouter` kullanır. Açık panel veya mini oyun varken açılmaz (Esc onları kapatıyor). Kapanınca `Time.timeScale` 1'e döner. Prefab + test sahnesi teslim edilir, sahneye Mahmut koyar. **Freeze'de bitmediyse kesilir.** | 2 sa | K0 | T+35 |
| **REL-05** | P1 | Teslim görselleri: itch.io kapak (630x500) + banner, 4–5 ekran görüntüsü, kontroller görseli | `teslim/gorseller/` (yeni klasör, Assets dışında) | RC0 build'den alınmış, logo ve Türkçe başlık okunuyor, gerçek kurum logosu yok. | 2 sa | REL-03 | T+42 |
| **QA-05** | P0 | RC build'i kendi bilgisayarında dene (ikinci PC testi) | — (sonuç Mahmut'a mesaj ya da `qa/` PR) | ZIP açılıyor; Menü → Deprem → Sel → Menü ve Ayarlar/Çıkış çalışıyor. Sorunlar ekran görüntüsüyle bildirildi. | 0,5 sa | REL-04 adayı | T+44 |

### Arda — diyalog sistemi + metinler + etkileşim ipucu, deprem dengesi, ocak

| ID | Ö | İş | Dosyalar | Kabul kriteri | Süre | Bağımlılık | Bitiş |
|---|---|---|---|---|---|---|---|
| **EQ-10** | P0 | Etkileşim ipucu başları kapatmasın (QA #4) | `Assets/Scripts/UI/InteractionPromptUI.cs` (+ `Assets/Scripts/UI/Editor/InteractionPrompt*`) | "Anneyle konuş" ipucu anne ve Cenk'in başını kapatmıyor (başların üstünde ya da yanında). Ekran kenarında taşmıyor. Diğer ipuçları (ocak, kitaplık, kapı) bozulmadı. Nokta başına ofset gerekiyorsa `Interactable` (Mahmut) için istek yazıldı. | 1,5 sa | K0 | T+29 |
| **EQ-11** | P1 | Toplanma alanı metinlerini JSON'a taşı (QA #6) | `Assets/Data/Earthquake/earthquake_mode.json` (sadece metin anahtarları) | `assemblyMeetTitle` ("ANNENİ BUL") ve `assemblyMeetHint` anahtarları eklendi. Kodda veya Inspector'da kalan diğer oyuncu metinlerinin listesi PR açıklamasında. Kodu Mahmut'un agent'ı okutur (EQ-04). JSON geçerli. | 0,5 sa | K0 | T+30 |
| **SEL2-10** | P0 | `flood_evening.json` metin cilası | `Assets/Data/Story/flood_evening.json` | Bütün `FE_*` ve aşama satırları doğru Türkçe yazımla, balonda en fazla 2 satır. Güvenlik dili net (suya girme, kapalı yol, resmî yönlendirme). "Sarı yağmurluk" geçmiyor. Depremin seli tetiklediği izlenimi yok. **ID'ler değişmedi.** | 1,5 sa | K1 | T+33 |
| **CONTENT-02** | P0 | Güvenlik ve doğruluk incelemesi (Deprem + yeni Sel) | `qa/ICERIK_KONTROLU.md` (yeni, PR ile; qa/ Mahmut'un) | Deprem: Çök-Kapan-Tutun, sarsıntıda koşma yok, merdiven/asansör, ocak, kitaplık, erken uyarı garantisi iması yok. Sel: suya girilmiyor, söylenti doğru seçenek değil, dal/rüzgâr güvenli, "120 sn oyun süresidir" notu var. Her bulgu dosya + ID + sahibiyle yazıldı. | 1,5 sa | K1 | T+34 |
| **EQ-12** | P1 | Anne seslenmesi bileşeni (lider fikri) | `Assets/Scripts/Dialogue/EscalatingReminder.cs`, `Assets/Data/Story/earthquake_home.json` (`EQ_MOM_CALL_1..3`), test sahnesi `Assets/Scenes/Test/Test_Arda_AnneSeslenme.unity` | Oyuncu hedefi ~10–15 sn yapmazsa anne repliği tekrar gelir, her seferinde daha vurgulu (büyük/kalın balon). 3. seferde hedef eşya parlar ve ok çıkar. Görev yapılınca durur. Diyalog veya panel açıkken saymaz. Ceza yok; sonuç için `IgnoredCount` olayı. Prefab + kullanım notu teslim edilir, sahneye Mahmut bağlar. **Freeze'de bitmediyse kesilir.** | 3 sa | K0 | T+35 |
| **QA-03** | P0 | Deprem test turu (RC0 build) | `qa/BUG_LOG.md` (PR ile) ya da Mahmut'a liste | En az 3 tur: hepsi doğru / yarısı / hiçbiri + kitaplık yaralanması + ocak. Her bulgu PASS/FAIL + adım + ekran görüntüsü. | 1,5 sa | REL-03 | T+40 |

### Eren — haber: TV, söylenti kartları, uyarı bildirimi

| ID | Ö | İş | Dosyalar | Kabul kriteri | Süre | Bağımlılık | Bitiş |
|---|---|---|---|---|---|---|---|
| **SEL2-20** | P0 | Sel uyarı bildirimi (`AlertNotification`) yeni bölümde | `Assets/Scripts/News/AlertNotification*.cs`, `Assets/Prefabs/News/AlertNotification.prefab`, `Assets/Data/FloodEvening/flood_evening_alert.json` (karar #5) | Metin kutuya sığıyor. "Kurgusal bildirim" etiketi görünüyor, kurum logosu yok. [Tamam] fareyle ve Enter/E/Space ile kapanıyor, aynı tuş sonraki etkileşimi tetiklemiyor. `Eren_AlertTest` ve diğer kullanımlar bozulmadı. | 1,5 sa | K1 | T+31 |
| **SEL2-21** | P0 | Söylenti panosu + resmî duyuru kartı (`RumorCards`) | `Assets/Scripts/News/RumorCard*.cs`, `Assets/Prefabs/News/RumorCards.prefab`, `Assets/Data/FloodEvening/flood_evening_rumor.json` | "Kestirme açıkmış, herkes oradan gidiyor." — **Kaynak: bilinmiyor**. Duyuru kartı alçak yolun kapalı olduğunu ve işaretli yüksek güvenli rotayı söylüyor. Söylentiye güvenmek doğru seçenek değil. 480x270 oyun kamerasında ve 1280x720/1920x1080'de okunuyor. Eski `flood_rumors.json` bozulmadı. | 2 sa | K1 | T+33 |
| **SEL2-22** | P1 | Duyuru kartına mini rota haritası | `Assets/Art/News/FloodEvening/**`, `RumorCards` (isteğe bağlı görsel alanı) | Piksel çizim: kapalı yol (X + bariyer ikonu), yüksek güvenli rota (ok + toplanma tabelası). Renk + ikon + yazı birlikte. Görsel yoksa kart eskisi gibi çalışıyor. | 2 sa | SEL2-21 | T+35 |
| **NEWS-06** | P1 | Haber ve uyarı metinleri doğruluk kontrolü (yeni dosyalar dahil) | `Assets/Data/News/**`, `Assets/Data/FloodEvening/*.json`; deprem telefon metni için Mahmut'a not | Gerçek kurum adı/logosu yok. Renk kodu evrensel diye anlatılmıyor. Deprem bildiriminde "her zaman önceden uyarı gelir" iması yok. | 0,5 sa | K0 | T+33 |
| **REL-06** | P1 | 10 saniyelik fragman (MP4 + GIF, 1280x720) | `teslim/fragman/` | Akış: deprem sarsıntısı → masa altı → rapor → "SEL BÖLÜMÜ" → yağmurlu mahalle → güvenli bölge → logo. Ses olmadan da anlaşılıyor, 10 MB altında. | 2 sa | REL-03 | T+42 |
| **QA-04** | P0 | Sel test turu (RC0 build) | `qa/BUG_LOG.md` (PR ile) ya da Mahmut'a liste | Başarı, süre dolması, alçak yol bariyeri, söylenti, yanlış şifre, "Tekrar Dene" depremi başlatmıyor, "Menüye Dön" sonrası tuşlar kilitli kalmıyor. Her bulgu PASS/FAIL ile. | 1,5 sa | REL-03 | T+40 |

### Mahmut — lider, sahneler, görsel, ses, test, build ((A) = Claude agent'a verilebilir, Mahmut onaylar)

| ID | Ö | İş | Dosyalar | Kabul kriteri | Süre | Bağımlılık | Bitiş |
|---|---|---|---|---|---|---|---|
| **REL-01** | P0 | PR #12'yi `dev`'e birleştir, ekibe "dev'i çekin" yaz | — | `dev`'de PR #12 içeriği var. Ekip `dev`'i çekip Unity'de hatasız açtı. | 0,25 sa | — | T+24,5 |
| **SEL2-01** | P0 | Akşam sel temelini toparla → ayrı PR → `dev` (A) | `Assets/Scenes/Chapters/Flood/FloodEvening.unity`, `Assets/Scripts/Chapters/FloodEvening/**`, `Assets/Prefabs/FloodEvening/**`, `Assets/Art/FloodEvening/**`, `Core/` eklemeleri, `docs/SEL_AKSAM_DEVIR.md` | Deprem raporu → [Devam: Sel] → FloodEvening baştan sona oynanıyor (başarı ve süre dolması). Console temiz. İlgisiz dosyalar commit dışı (`EarthquakeChapter.unity`, `TimeManager.asset`, `TilemapBedroom`, URP ayarları). | 2 sa | K0 | T+26 |
| **EQ-01** | P0 | Deprem sahne düzeltmeleri (QA #1, #2) (A) | `EarthquakeHome.unity`, `Assets/Scripts/Chapters/Earthquake/**` | Ocağın önünde "Pencereyi incele" çıkmıyor (`LivingWindow` noktası uzaklaştı). Masa altı pozunda Cenk masanın önünde çizilmiyor. | 1,5 sa | K1 | T+30 |
| **EQ-02** | P0 | Rapor tutarlılığı (QA #3) (A) | `ResultReportView.cs`, `EarthquakeResultManager.cs`, `EarthquakeModeData.cs` | Düşük sonuçta yeşil tik yok. Zorunlu görev eksikken "USTA" rozeti çıkmıyor (rozet hem puan hem görev şartına bağlı). 3 senaryonun ekran görüntüsü alındı. | 1 sa | K1 | T+30 |
| **EQ-03** | P1 | Telefon "BAŞLA" ekranında ilk E yutulmasın (QA #5) (A) | `PhoneAlertView.cs`, `PhoneAlertController.cs`, `Core/Audio/AudioPhoneAlertView.cs` | İlk E her seferinde başlatıyor, ikinci E sonraki etkileşimi tetiklemiyor. | 0,5 sa | K1 | T+32 |
| **EQ-04** | P1 | Toplanma metinlerini JSON'dan oku (A) | `EarthquakeModeController.cs`, `EarthquakeModeData.cs` | EQ-11 anahtarları okunuyor. Anahtar yoksa eski metin kullanılıyor. | 0,5 sa | EQ-11 | T+33 |
| **SEL2-02** | P0 | Ekip teslimlerini sahnelere yerleştir (UI-10, SEL2-20..22, EQ-12, UI-12) | `FloodEvening.unity`, `EarthquakeHome.unity` | Her PR birleşince sahne güncellendi, uçtan uca bir tur oynandı. | 2 sa (parça parça) | K2 | T+36 |
| **AUD-02** | P1 | Ses seviyelerini kulakla ayarla (Deprem + Sel) | `Assets/Audio/**`, `Core/Audio/**` | Diyalog sırasında ortam sesi kısılıyor. Hiçbir ses patlamıyor. Ayarlar'daki ses kaydırıcısı her şeyi etkiliyor. | 1 sa | AUD-01 | T+40 |
| **REL-03** | P0 | FEATURE FREEZE + RC0 build (v0.5-onizleme) | `Builds/Windows/`, GitHub Releases | Build açılıyor, Player.log temiz, commit hash'i kayıtlı. Ekibe "test edin" mesajı gitti. | 1 sa | SEL2-02, EQ-01, EQ-02 | T+36 |
| **FIX-03** | P0 | QA-03/04 bulgularından P0 olanları düzelt (A) | Sahibinin klasörü | Kritik hata sayısı 0. Her düzeltme küçük bir PR. | 3 sa | QA-03, QA-04 | T+43 |
| **REL-07** | P0 | Teslim paketi metinleri (A) | `README_PLAY.md`, `docs/JURY_DEMO.md`, `teslim/ITCH_SAYFASI.md`, `teslim/EKIP_VE_LISANS.md` | Kontroller, sistem gereksinimi, bilinen sorunlar yazıldı. Ekip listesi ve lisanslar (Nunito OFL, `Assets/Audio/Licenses`) var. Jüri demosu yeni sırayı anlatıyor (Deprem → Sel). | 1,5 sa | REL-03 | T+42 |
| **REL-04** | P0 | RC1 build + ikinci PC testi + ZIP + hash | `Builds/Windows/`, Releases | Berk'in PC'sinde tam döngü geçti (QA-05). ZIP ve yedek kopya hazır. | 1 sa | FIX-03, QA-05 | T+44 |
| **REL-08** | P0 | 3 dk jüri provası + final teslim | `docs/JURY_DEMO.md` | Prova 3 dakikayı geçmiyor, iki bölüm ve güvenlik mesajı gösterildi. Teslim yüklendi. | 1,5 sa | REL-04, REL-07 | T+46 → T+48 |
| **REL-02** | P2 | Build boyutunu küçült (DirectML/inference paketleri ~16 MB) | `Packages/manifest.json` | Sadece ayrı dalda ve tam build testinden sonra. Unity MCP bozulmuyor. **Freeze'den sonra yapılmaz.** | 1 sa | karar #10 | T+35 |
| **EQ-20** | P2 | Oda karartması görünsün diye deprem evinde yatak odası + salon bitişik | `EarthquakeHome.unity` | Önerim: **kes** (sahne yeniden kurulumu, freeze öncesi büyük risk). | 2–3 sa | karar #9 | — |

### Agent / Codex işleri (Mahmut başlatır, inceler, birleştirir)

| ID | Kim | İş | Durum / bağımlılık |
|---|---|---|---|
| SEL2-01 | 5 Claude agent'ı (LEAD, ENV, OUTFIT, PUZZLE, EVENTS) | Akşam sel temeli. OUTFIT teslim etti, diğerleri sürüyor. LEAD agent'ı `docs/SEL_AKSAM_DEVIR.md`'yi yazacak. | Sürüyor → K1 |
| **AUD-01** | **Codex** | Sel ses paketi: yağmur katmanları (`FloodEveningHub.Progress01`'e bağlı), rüzgâr, telefon uyarısı, bariyer, kapı kolu, şifre tuşları, başarı / süre doldu. Olaylar `FloodEveningHub` üzerinden. Dosyalar `Assets/Audio/**`, `Assets/Scripts/Core/Audio/**`. | Verildi; K1 ile birleşir. P1, T+34 |
| AUD-03 | Codex | Ayak sesleri (ev içi / taş yol / ıslak zemin; adım zamanlayıcısı ya da animasyon olayı). Sessizken oyun bozulmamalı. | P2, AUD-01'den sonra, T+35 |
| EQ-01..04, FIX-03, REL-07 | Claude agent'ları | Yukarıdaki Mahmut tablosu | Mahmut onaylar |

**Yük dengesi (P0+P1):** Berk ~8,5 sa · Arda ~9,5 sa · Eren ~9,5 sa · Mahmut ~10 sa insan zamanı (+ ~8 sa agent işi).

---

## 3. Ekip üyeleri için hazır promptlar

Kendi Claude Code'unuza **olduğu gibi** yapıştırın. Her görev için ayrı küçük dal + ayrı PR açın.

### Berk

```text
Ben Berk (GitHub: berkatac41), AFET UYANIŞI ekibinde ana menü + ortak UI sahibiyim. Unity 6000.6.6f1 kullanıyorum.

0) Kim olduğumu doğrula: `git config user.name` (berkatac41 / Berk görünmeli). Şu anki dalımı göster: `git branch --show-current`.
1) Kapı kontrolü: `gh pr view 12 --json state -q .state` sonucu MERGED değilse DUR ve bana söyle. MENU-10 ve UI-10 için ayrıca `git fetch origin && git ls-tree -r --name-only origin/dev | grep -E "FloodEvening.unity|SEL_AKSAM_DEVIR.md"` iki dosyayı da göstermeli; göstermiyorsa bu iki göreve başlama, önce UI-11'i yap.
2) Git: `git fetch origin && git checkout dev && git pull`, sonra görev başına `git checkout -b feature/berk-<iş>` (ör. feature/berk-menu-sira, feature/berk-sel-arayuz, feature/berk-kontroller). main ve dev'e asla commit/push yapma; PR sadece `gh pr create --base dev`; asla merge etme.
3) Baştan sona oku: CLAUDE.md, AGENTS.md, planning/EKIP_ROLLERI.md, docs/OYUN_AKISI.md (§3, §4, §6.0), docs/SEL_AKSAM_DEVIR.md, docs/GOREV_DAGILIMI.md (benim bölümüm ve kabul kriterleri).
4) Görevlerim (sırayla; ayrıntı ve kabul kriteri GOREV_DAGILIMI.md'de):
   - UI-11 (P1): Menüde "Kontroller" ekranı. Assets/Scripts/Menu/**, MainMenu.unity.
   - MENU-10 (P0): Menüyü Deprem → Sel sırasına uydur: varsayılan Deprem; Sel balonu FloodEvening'i açar; önizleme/metinlerde sabah-okul-otobüs ifadesi kalmasın; Yangın/Heyelan kilitli. MenuChapterLoader.cs'deki LEAD değişikliğini koru (Sel → FloodEvening, GameSceneRouter.LoadChapter).
   - UI-10 (P0): Sel arayüz cilası (giriş kartı, SU SEVİYESİ, sonuç ekranı, kapı/şifre panelleri). Assets/Scripts/Chapters/FloodEvening/UI/** klasörüne SADECE GOREV_DAGILIMI.md karar listesinde "UI-10 devri: ONAYLANDI" yazıyorsa dokun. Yazmıyorsa yalnız Assets/Art/UI/FloodEvening/ altına mock PNG + öneri listesi hazırla ve PR açıklamasına yaz.
   - UI-12 (P2): Duraklatma paneli prefab'ı (Assets/Prefabs/UI/PausePanel.prefab + Assets/Scripts/UI/PausePanel.cs). Saat 10:30'da bitmediyse bırak.
   - REL-05 (P1, RC0 build çıkınca): teslim/gorseller/ altına itch.io kapak 630x500, banner, 4–5 ekran görüntüsü, kontroller görseli.
   - QA-05 (P0): RC build'i kendi bilgisayarımda dene, sonucu raporla.
5) Kurallar: Tuşları sadece AfetUyanisi.Core.GameInput ile oku (eski Input.GetKeyDown yok). Sahne geçişi sadece GameSceneRouter. Assets/Scripts/Shared/** ve Contracts değişmez. FloodEvening.unity, EarthquakeHome.unity ve başkasının test sahnesine dokunma. Assets/Scripts/UI/InteractionPromptUI.cs Arda'nın, dokunma. ProjectSettings/ ve Packages/ değişmez. Yeni paket yok. Her asset .meta dosyasıyla commit edilir. Namespace AfetUyanisi.<Alan>, [SerializeField] private. Deneme için Assets/Scenes/Test/Test_Berk_<Konu>.unity kullan. Unity MCP ile sahne değiştirmeden önce bana sor.
6) Başlamadan önce yaz: "Görev ID | değişecek dosyalar | risk". Commit'ten önce `git status` çıktısını göster, ilgisiz dosyaları ekleme. Commit mesajı kısa ve Türkçe. PR açmadan önce `git pull origin dev`; çakışma varsa (özellikle .unity/.prefab) otomatik çözme, bana söyle.
7) Test: Unity'de Play ile gerçekten dene (klavye + fare, 1280x720 ve 1920x1080 Game görünümü), Console'da yeni hata olmasın. Denemediğin şeye "test edildi" deme; UNTESTED yaz.
8) PR açıklaması: Ne değişti / Dosyalar / Nasıl test edilir / Test sonucu (ekran görüntüsü) / Mahmut'un sahneye yapması gereken bağlama. PR'ı açınca bana "Mahmut'un incelemesini bekle" de.
```

### Arda

```text
Ben Arda (GitHub: ardabekduz123-beep), AFET UYANIŞI ekibinde diyalog sistemi + diyalog metinleri sahibiyim. Etkileşim ipucu (InteractionPromptUI), deprem dengesi ve ocak işleri de bende. Unity 6000.6.6f1 kullanıyorum.

0) Kim olduğumu doğrula: `git config user.name`. Şu anki dalımı göster: `git branch --show-current`.
1) Kapı kontrolü: `gh pr view 12 --json state -q .state` sonucu MERGED değilse DUR ve bana söyle. SEL2-10 ve CONTENT-02 için ayrıca `git fetch origin && git ls-tree -r --name-only origin/dev | grep -E "flood_evening.json|SEL_AKSAM_DEVIR.md"` iki dosyayı da göstermeli; göstermiyorsa önce EQ-10, EQ-11 ve EQ-12'yi yap.
2) Git: `git fetch origin && git checkout dev && git pull`, sonra görev başına `git checkout -b feature/arda-<iş>` (ör. feature/arda-ipucu-konum, feature/arda-sel-metin, feature/arda-anne-seslenme). main ve dev'e asla commit/push yapma; PR sadece `gh pr create --base dev`; asla merge etme.
3) Baştan sona oku: CLAUDE.md, AGENTS.md (özellikle §5 güvenlik), planning/EKIP_ROLLERI.md, docs/OYUN_AKISI.md (§6.0, §7.4, §9 ID sözleşmesi), docs/SEL_AKSAM_DEVIR.md, docs/GOREV_DAGILIMI.md (benim bölümüm).
4) Görevlerim (ayrıntı ve kabul kriteri GOREV_DAGILIMI.md'de):
   - EQ-10 (P0): Assets/Scripts/UI/InteractionPromptUI.cs — "Anneyle konuş" ipucu anne ve Cenk'in başını kapatmasın, ekrandan taşmasın, diğer ipuçları bozulmasın. Nokta başına ayar gerekiyorsa Interactable (Mahmut'un kodu) için PR açıklamasına istek yaz, o dosyayı değiştirme.
   - EQ-11 (P1): Assets/Data/Earthquake/earthquake_mode.json'a `assemblyMeetTitle` ("ANNENİ BUL") ve `assemblyMeetHint` metin anahtarlarını ekle. Kodda ya da Inspector'da kalan oyuncu metinlerini listele (kodu Mahmut değiştirecek).
   - EQ-12 (P1): Assets/Scripts/Dialogue/EscalatingReminder.cs — oyuncu hedefi 10–15 sn yapmazsa anne repliği giderek vurgulu tekrarlanır, 3. seferde hedef eşya parlar ve ok çıkar; görev yapılınca durur; diyalog/panel açıkken saymaz; ceza yok; IgnoredCount olayı. Replikler earthquake_home.json'da EQ_MOM_CALL_1..3. Test sahnesi Assets/Scenes/Test/Test_Arda_AnneSeslenme.unity. EarthquakeHome.unity'ye bağlamayı Mahmut yapar. Saat 10:30'da bitmediyse bırak.
   - SEL2-10 (P0): Assets/Data/Story/flood_evening.json metin cilası (yazım, balonda en fazla 2 satır, güvenlik dili, "sarı yağmurluk" yok, deprem ile sel arasında bağ kurma yok). ID'leri DEĞİŞTİRME.
   - CONTENT-02 (P0): qa/ICERIK_KONTROLU.md — Deprem + Sel güvenlik/doğruluk incelemesi; her bulgu dosya + ID + sahibiyle.
   - QA-03 (P0, RC0 build çıkınca): Deprem test turu, bulgular PASS/FAIL ile.
5) Kurallar: Tuşları sadece AfetUyanisi.Core.GameInput ile oku (eski Input API yok). Sahne geçişi sadece GameSceneRouter. Assets/Scripts/Shared/** ve Contracts değişmez. Hiçbir .unity bölüm sahnesini (EarthquakeHome, FloodEvening, MainMenu) düzenleme; prefab + script teslim et. Assets/Scripts/Chapters/** Mahmut'un, değiştirme. Yeni paket yok. .meta dosyaları asset'le birlikte commit edilir. Namespace AfetUyanisi.Dialogue, [SerializeField] private. Tehlikeli davranışı ödüllendiren ya da deprem tahmini ima eden metin yazma. Unity MCP ile sahne değiştirmeden önce bana sor.
6) Başlamadan önce yaz: "Görev ID | değişecek dosyalar | risk". Commit'ten önce `git status` göster, ilgisiz dosyaları ekleme. Commit mesajı kısa ve Türkçe. PR açmadan önce `git pull origin dev`; çakışma varsa bana söyle.
7) Test: JSON'ları geçerlilik için kontrol et; Unity'de kendi test sahnemde Play ile dene (klavye + fare). Console'da yeni hata olmasın. Denemediğin şeye "test edildi" deme; UNTESTED yaz.
8) PR açıklaması: Ne değişti / Dosyalar / Nasıl test edilir / Test sonucu (ekran görüntüsü) / Mahmut'un bağlaması gerekenler (Inspector alanları, sahne). PR'ı açınca bana "Mahmut'un incelemesini bekle" de.
```

### Eren

```text
Ben Eren (GitHub: erenatacc41), AFET UYANIŞI ekibinde haber parçalarının sahibiyim: TV haberi, söylenti/duyuru kartları (RumorCards), uyarı bildirimi (AlertNotification). Unity 6000.6.6f1 kullanıyorum.

0) Kim olduğumu doğrula: `git config user.name` (Eren / erenatacc41 görünmeli; "Mahmut Eren Ciftci" görünüyorsa `git config user.name "Eren"` ile düzelt ve bana söyle). Şu anki dalımı göster: `git branch --show-current`.
1) Kapı kontrolü: `gh pr view 12 --json state -q .state` sonucu MERGED değilse DUR ve bana söyle. SEL2-20/21/22 için ayrıca `git fetch origin && git ls-tree -r --name-only origin/dev | grep -E "flood_evening_alert.json|flood_evening_rumor.json|SEL_AKSAM_DEVIR.md"` üç dosyayı da göstermeli; göstermiyorsa önce NEWS-06'yı yap.
2) Git: `git fetch origin && git checkout dev && git pull`, sonra görev başına `git checkout -b feature/eren-<iş>` (ör. feature/eren-sel-uyari, feature/eren-sel-soylenti, feature/eren-fragman). main ve dev'e asla commit/push yapma; PR sadece `gh pr create --base dev`; asla merge etme.
3) Baştan sona oku: CLAUDE.md, AGENTS.md (özellikle §5), planning/EKIP_ROLLERI.md, docs/OYUN_AKISI.md (§6.0 A ve D satırları), docs/SEL_AKSAM_DEVIR.md, docs/GOREV_DAGILIMI.md (benim bölümüm). Ayrıca kendi bileşenlerimin yeni bölümde nasıl çağrıldığını oku (değiştirme): Assets/Scripts/Chapters/FloodEvening/Events/FloodWarningPhone.cs ve RumorCheckPoint.cs.
4) Görevlerim (ayrıntı ve kabul kriteri GOREV_DAGILIMI.md'de):
   - NEWS-06 (P1): Haber ve uyarı metinleri doğruluk kontrolü (Assets/Data/News/**, Assets/Data/FloodEvening/*.json): gerçek kurum adı/logosu yok, renk kodu evrensel değil, deprem uyarısı garanti gibi anlatılmıyor.
   - SEL2-20 (P0): AlertNotification yeni sel uyarısında: metin sığıyor, "Kurgusal bildirim" etiketi görünür, logo yok, [Tamam] fare + Enter/E/Space ile kapanıyor, aynı tuş sonraki etkileşimi tetiklemiyor. Eren_AlertTest ve diğer kullanımlar bozulmasın. Public API'yi (Play, Hide, Finished, IsPlaying) değiştirme, sadece ekle.
   - SEL2-21 (P0): RumorCards + flood_evening_rumor.json: "Kestirme açıkmış, herkes oradan gidiyor." Kaynak: bilinmiyor; resmî duyuru kartı kapalı alçak yolu ve işaretli yüksek güvenli rotayı söylüyor; 480x270 oyun kamerasında okunuyor. Eski flood_rumors.json bozulmasın.
   - SEL2-22 (P1): Duyuru kartına mini rota haritası (Assets/Art/News/FloodEvening/), RumorCards'a isteğe bağlı görsel alanı; görsel yoksa kart eskisi gibi çalışsın. Saat 10:30'da bitmediyse bırak.
   - REL-06 (P1, RC0 build çıkınca): teslim/fragman/ altına 10 sn fragman (MP4 + GIF, 1280x720, 10 MB altı).
   - QA-04 (P0, RC0 build çıkınca): Sel test turu (başarı, süre dolması, bariyer, söylenti, yanlış şifre, Tekrar Dene depremi başlatmıyor, Menüye Dön sonrası kilit yok).
5) Kurallar: Tuşları sadece AfetUyanisi.Core.GameInput ile oku (NewsInput de onu kullanmalı; eski Input API yok). Sahne geçişi sadece GameSceneRouter. Assets/Scripts/Shared/** ve Contracts değişmez. Hiçbir .unity bölüm sahnesini düzenleme; FloodEvening.unity'yi Mahmut düzenler. Assets/Scripts/Chapters/** Mahmut'un, değiştirme; gerekirse PR açıklamasına istek yaz. Yeni paket yok, internetten lisansı belirsiz görsel yok. .meta dosyaları asset'le birlikte commit edilir. Namespace AfetUyanisi.News, [SerializeField] private. Deneme için Assets/Scenes/Test/Test_Eren_<Konu>.unity. Unity MCP ile sahne değiştirmeden önce bana sor.
6) Başlamadan önce yaz: "Görev ID | değişecek dosyalar | risk". Commit'ten önce `git status` göster, ilgisiz dosyaları ekleme. Commit mesajı kısa ve Türkçe. PR açmadan önce `git pull origin dev`; .prefab çakışması varsa otomatik çözme, bana söyle.
7) Test: Unity'de kendi test sahnemde Play ile dene (klavye + fare, 1280x720 ve 1920x1080). Console'da yeni hata olmasın. Denemediğin şeye "test edildi" deme; UNTESTED yaz.
8) PR açıklaması: Ne değişti / Dosyalar / Nasıl test edilir / Test sonucu (ekran görüntüsü) / Mahmut'un sahnede bağlaması gerekenler. PR'ı açınca bana "Mahmut'un incelemesini bekle" de.
```

---

## 4. Zaman çizelgesi (öneri)

| Saat | T+ | Mahmut (+ agent'lar) | Berk | Arda | Eren |
|---|---|---|---|---|---|
| 17 Eki 23:15–00:00 | 24 | **REL-01 PR #12 merge**, gruba mesaj | dev'i çek, UI-11 | dev'i çek, EQ-10 | dev'i çek, `git user.name` düzelt, NEWS-06 |
| 00:00–02:00 | 24,5–26,5 | Agent'ları bitir, **SEL2-01** PR + merge (K1) | UI-11 PR | EQ-10 PR, EQ-11 | NEWS-06 |
| 02:00–07:30 | 26,5–32 | Dönüşümlü uyku. Agent'lar: EQ-01, EQ-02, EQ-03; Codex: AUD-01 | Uyku | Uyku | Uyku |
| 07:30–10:30 | 32–35 | EQ-04, Sel uçtan uca tur, PR'ları inceleyip birleştir | MENU-10, UI-10, (UI-12) | SEL2-10, CONTENT-02, (EQ-12) | SEL2-20, SEL2-21, (SEL2-22) |
| **10:30** | **35** | **K2: ekip PR'ları açık** | | | |
| 10:30–11:30 | 35–36 | SEL2-02 sahneye yerleştir → **REL-03 FREEZE + RC0** | PR düzeltmeleri | PR düzeltmeleri | PR düzeltmeleri |
| 11:30–16:00 | 36–40,5 | FIX-03 (agent), AUD-02 | REL-05 | QA-03 | QA-04, REL-06 |
| 16:00–19:30 | 40,5–44 | REL-07, **REL-04 RC1** | QA-05 (ikinci PC) | Prova metni | REL-06 bitir |
| 19:30–22:00 | 44–46,5 | **REL-08 prova**, son düzeltme yok (sadece engelleyici hata) | Prova | Prova (konuşmacı) | Prova (oyuncu) |
| 22:00–23:30 | 46,5–48 | Final ZIP + yükleme + yedek | — | — | — |

Kural: Her PR birleştikten sonra `dev` oynanabilir kalır (Menü → Deprem → Sel → Menü). 20 dakikadan fazla takılan kişi gruba yazar. Freeze'den sonra yeni özellik girmez.

---

## 5. Mahmut'un kararı gerekenler

1. **Gerçek teslim saati.** Bu plan T+48 = 18 Eki 23:30 ve freeze 11:30 varsayıyor; doğru saati `planning/OWNERS_AND_HANDOFFS.md`'ye yaz.
2. **PR #12'yi şimdi birleştir.** Auto-mode `gh pr merge`'ü engelliyor; GitHub'da Merge'e bas ya da kendin `gh pr merge 12 --merge --admin` çalıştır.
3. **Sel temeli ayrı PR mı?** Önerim: ayrı dal (`feature/mahmut-sel-aksam`) + ayrı PR. PR #12'ye eklenirse inceleme ve geri alma zorlaşır.
4. **UI-10 devri:** `Assets/Scripts/Chapters/FloodEvening/UI/**` klasörünü freeze'e kadar Berk'e devrediyor musun? Önerim: evet; agent'lar o klasöre dokunmayı bırakır. Onaylarsan bu satıra **"UI-10 devri: ONAYLANDI"** yaz (Berk'in promptu buna bakıyor).
5. **Sel haber verisi sahipliği:** `Assets/Data/FloodEvening/flood_evening_alert.json` ve `flood_evening_rumor.json` Eren'e geçsin mi? Önerim: evet, dosyalar yerinde kalsın (taşımak referansları kırabilir). `EKIP_ROLLERI.md`'ye ekle.
6. **`InteractionPromptUI.cs` + deprem dengesi + ocak dosyaları Arda'da.** `EKIP_ROLLERI.md`'ye yaz; Berk'in `Scripts/UI` sahipliğinden bu dosyayı ayır.
7. **Deprem çantası rengi:** lacivert-turuncu (EmergencyBag "Ready" görseliyle aynı) mı, tamamen turuncu mu? Önerim: lacivert-turuncu (ek iş yok).
8. **Yağmurluk görünümü (OUTFIT teslimi) kullanılmıyor.** `Cenk.prefab` ve `CenkOutfit.cs` değişiklikleri (60 çift + 20 PNG) commit'e girsin mi? Önerim: girsin ama kapalı kalsın; zararsız ve ileride kullanılabilir. İstemezsen SEL2-01 commit'inden çıkar.
9. **EQ-20 (bitişik yatak odası + salon):** Önerim: **kes**. Freeze öncesi sahne yeniden kurulumu çok riskli.
10. **REL-02 build boyutu:** Teslim platformunda boyut sınırı yoksa **dokunma**. Paket kaldırmak Unity MCP'yi bozabilir.
11. **EQ-12 anne seslenmesi:** P1 olarak girsin mi, yoksa yalnız 1 noktada mı (ilk anne konuşması)? Önerim: tek nokta, 10:30'da bitmezse kes.
12. **Eren'in sele kapılma ara sahnesi** yeni Sel'in "karakteri suya sokma" kuralıyla çelişiyor, oyunda kullanılmayacak. Fragmanda ya da jüri sunumunda ("eski tasarım" olarak) anılsın mı? Eren'e sen haber ver.
13. **Ana menüde Sel tek başına açılabilsin mi?** OYUN_AKISI "evet" diyor; MENU-10 buna göre yazıldı. Hikâye sırası yalnız Deprem → Sel olsun istiyorsan Sel balonunu kilitle.
