# AFET UYANIŞI — Oyun Akışı ve Bölüm Tasarımı (bağlayıcı)

> **Bu belge oyunun baştan sona nasıl oynandığını tanımlar.** Sel'e ya da Deprem'e tıklandığı andan sonuç ekranına kadar her sahne, süre, replik, seçim ve geri bildirim buradadır.
> Kaynaklar: [`referans/tasarim_genel_akis.png`](referans/tasarim_genel_akis.png) ("Afet Uyanışı: Doğru Kararı Ver"), [`referans/tasarim_guvenlik_rehberi.png`](referans/tasarim_guvenlik_rehberi.png) ("Deprem ve Sel Güvenlik Rehberi") ve paket belgeleri (`docs/GAME_DESIGN_DOCUMENT.md`, `story/*`).
> Kaynaklar birbiriyle çeliştiğinde **bu belge geçerlidir**; verilen kararlar ve gerekçeleri [§10](#10-kaynaklar-arası-çelişkiler-ve-verilen-kararlar)'dadır.
> Metinlerin oyundaki güncel hali `Assets/Data/Story/*.json` (Arda) ve `Assets/Data/News/*.json` (Eren) dosyalarındadır; bu belgedeki replikler ilk tasarımdır.

---

## 1. Oyun özeti

| | |
|---|---|
| **Slogan** | *Küçük kararlar, büyük fark yaratır.* |
| **Tür** | **İzometrik 2D pixel art**, kısa, seçimli hikâye oyunu (Tilemap ile kurulan odalar), Türkçe |
| **Ana karakter** | Sarı yağmurluklu, sırt çantalı çocuk (sprite: `visuals/characters/arda_yuruyus`). Her iki bölümde de aynı çocuk. |
| **Yan karakterler** | Anne (sprite hazır), 3 arkadaş (Sel), öğretmen (Sel sonu), komşu + görevli (Deprem sonu) |
| **Hedef** | Afetlere karşı doğru davranışları öğretmek · yanlış bilginin risklerini göstermek · kısa ama etkili bir deneyim sunmak |
| **Süre** | Her bölüm yaklaşık 4–4,5 dakika (tasarım hedefi 3–5 dk) |
| **Bölümler** | **Sel** ve **Deprem** oynanabilir · Orman Yangını ve Heyelan menüde **kilitli** ("Yakında") |

**Temel döngü (iki bölümde de):** Uyarı → yorum → karar → sonuç. Yanlış karar cezalandırılmaz; anında açıklama gelir ve oyuncu tekrar seçer. Her bölüm güvenli sonla biter, yapılan yanlışlar sonuç ekranında listelenir.

---

## 2. Genel akış

```text
Ana Menü (≈10 sn)
  └─ Sel bulutuna / Deprem bulutuna tıkla
       ├─ Ortak açılış: Uyanış (20 sn) → Sabah Rutini (30 sn)      ← iki bölümde aynı sahneler
       ├─ Hikâye ve etkileşimler (2,5–3,5 dk)                     ← haber sahnesinde bölümler ayrışır
       └─ Sonuç ve bilgilendirme (≈20 sn) → Tekrar Dene / Menüye Dön
```

> **Güncel sıra (17 Eki):** Deprem (`EarthquakeHome`, §7.4) → rapor → **[Devam: Sel Bölümü]** → kararma → "SEL BÖLÜMÜ" kartı → Sel (`FloodEvening`, §6.0, akşam mahalle seli) → sonuç → Tekrar Dene (yalnız Sel) / Menü. Menüden Sel ve Deprem ayrı ayrı da başlatılabilir. Aşağıdaki "ortak açılış" (uyanış + sabah rutini) yalnız eski sabah sel akışı içindir ve artık oynanmaz.

- **Olay seçimi menüdedir:** oyuncu bulutu seçer, bölüm o anda başlar. Görsel 1'deki "Olay Seçimi" adımı budur.
- **Uyanış ve Sabah Rutini iki bölümde ortaktır.** Aynı oda, banyo ve mutfak sahneleri kullanılır, bu da çizim ve iş yükünü yarıya indirir. Hikâye mutfaktaki **TV haberi** ile ayrışır: Sel'de yağmur uyarısı, Deprem'de sarsıntı haberi.
- Kilitli bulutlar (Orman Yangını, Heyelan) hiçbir sahneye gitmez, sadece "Yakında" yazar.

---

## 3. Mekanikler ve tuşlar

| Mekanik | Tuş | Nerede | Not |
|---|---|---|---|
| Hareket | **W / A / S / D** veya **ok tuşları** | Ev içi, sokak | İzometrik: 8 yöne yürüme; duvar ve eşyalara çarpar |
| Etkileşim | **E** | Kalk, yüzünü yıka, eşya al, tabela incele | Ekranda ipucu: `[E] Kalk` |
| Diyaloğu ilerlet | **E**, **Space**, **Enter** veya **sol tık** | Tüm konuşmalar | |
| Seçim yap | **1–5** veya **fare** | Tüm kararlar | Seçenek numaraları ekranda yazar |
| Zamanlı seçim | 1–5 / fare | Deprem anı (E_D1) | **5 saniye**, sayaç çubuğu; seçenekler sayaç başlamadan görünür |
| Çök – Kapan – Tutun | **S** (Çök) → **A** (Kapan) → **D** basılı (Tutun) | Deprem (E05) | Hareket o sırada kapalı (S/A/D yürümez); D sarsıntı bitene kadar basılı tutulur |
| Önemli eşyalar | **E** | Yağmurluk, şemsiye (Sel), deprem çantası (Deprem) | Alınan eşya HUD'da ikon olarak görünür (P1) |
| Menü | **A / D**, **← / →** bulut seç · **Enter** başlat · **Esc** geri · **Space** introyu atla | Ana menü | Fareyle bulut tıklamak doğrudan başlatır |

> **Teknik not:** Proje yalnızca yeni **Input System**'i kullanır. `Input.GetKeyDown` gibi eski çağrılar bu projede **hata verir**. Tuşlar `AfetUyanisi.Core.GameInput` sınıfından okunur, bu tablo ile o sınıf aynı kalmalıdır.

---

## 4. Ana menü (Berk)

**Açılış (5 sn, Space/Enter ile atlanır):**

| Zaman | Ekranda |
|---|---|
| 0,0–1,0 sn | Karanlık oda, kırmızı **07:00** dijital saat parlar |
| 1,0–2,0 sn | Oda sabah ışığıyla aydınlanır, çocuk yatakta uyuyor |
| 2,0–4,0 sn | Dört rüya bulutu sırayla belirir: **Deprem, Sel, Orman Yangını, Heyelan** |
| 4,0–5,0 sn | **AFET UYANIŞI** başlığı, slogan *"Küçük kararlar, büyük fark yaratır."*, **Yeni Oyun / Ayarlar / Çıkış** |

**Döngüde:** çocuk nefes alır (1 px), saat hafifçe parlar, bulutlar farklı fazlarda 1–2 px yukarı-aşağı süzülür.

**Etkileşim:**
- Açık buluta (Sel, Deprem) **tıklamak** bölümü başlatır: fade out → `GameSceneRouter.LoadChapter(ChapterId.Flood / Earthquake)`.
- Klavyede A/D ile bulut seçilir, **Enter** veya **Yeni Oyun** seçili bölümü başlatır. Varsayılan seçim: **Sel**.
- Kilitli bulutlarda kilit ikonu ve "(Kilitli)" yazısı bulunur, tıklanınca **"Yakında"** ipucu çıkar.
- **Ayarlar:** ses seviyesi, tam ekran, geri. **Çıkış:** oyunu kapatır.
- Introyu atlamak için basılan tuş aynı karede oyunu başlatmamalıdır.

---

## 5. Ortak açılış — Uyanış + Sabah Rutini (Mahmut; replikler Arda)

| # | ID (Sel / Deprem) | Süre | Mekân | Ne olur | Oyuncu |
|---|---|---|---|---|---|
| 1 | `F01_WAKE` / `E01_WAKE` | 0:00–0:20 | Çocuk odası | Siyahtan aydınlığa geçiş, 07:00 alarmı çalar, çocuk gözlerini açar. *"Saat yedi olmuş... Yine okula geç kalmayayım."* | **[E] Kalk** → kapıya yürü (A/D) |
| 2 | `F02_WASH` / `E02_ROUTINE` | 0:20–0:50 | Banyo → mutfak | Lavaboda yüzünü yıkar (1 sn su efekti). *"Oh, su buz gibi! Şimdi uyandım işte."* · *"Bakalım kahvaltıda ne var?"* | **[E] Yüzünü yıka** |

Mutfakta **TV açıktır**. Bölümler buradan sonra ayrışır.

### 5.1 İlk sahnenin ayrıntılı kurgusu (uygulanan)

Oyuncunun ilk 50 saniyesi iki bölümde de aynıdır ve oyunun tonunu belirler: sakin, sıcak bir sabah. Afet henüz yok, oyuncu sadece hareketi ve etkileşimi öğrenir.

| Vuruş | Ekranda | Oyuncu | Uygulama notu |
|---|---|---|---|
| 1. Siyah | Ekran kapkaranlık (alarm sesi P2) | — | `SetBlack()` |
| 2. Açılış (1,5 sn) | İzometrik yatak odası (Tilemap), çocuk yatakta uyuyor | — | `World/Bedroom` + varsa `SleepingBoy` objesi |
| 3. Uyan | Ekranın altında `[E] Kalk` | **E** | Oyuncu henüz görünmez, E her yerden çalışır |
| 4. Kalkış (0,25 sn) | Kısa kararma; çocuk yatağın yanında, güneye bakarak belirir | — | `SleepingBoy` gizlenir, `Characters/Boy` doğma noktasına konur |
| 5. İç ses | Çocuğun başının üstünde balon: *"Saat yedi olmuş... Yine okula geç kalmayayım."* | E/Space ile geçer | Eren'in konuşma balonu (`DialogueSpeaker` "Çocuk") |
| 6. Serbest dolaşma | Oda; kapıya yaklaşınca `[E] Banyoya git` | **W/A/S/D** ile 8 yöne yürür, kapıda **E** | `Bedroom/Door` etkileşim noktası (`Interactable`); duvar ve eşyalara çarpar |
| 7. Geçiş | Kararma → banyo açılır, çocuk kapının önünde | — | `World/Bathroom`, `Bathroom/Spawn` |
| 8. Lavabo | Lavaboya yaklaşınca `[E] Yüzünü yıka` | Yürür, **E** | `Bathroom/Sink` |
| 9. Rutin | Balon: *"Oh, su buz gibi! Şimdi uyandım işte."* → *"Bakalım kahvaltıda ne var?"* | E/Space | (P1: 1 sn su efekti, flaş yok) |
| 10. Çıkış | Kapıda `[E] Mutfağa git` → kararma → mutfak (bölüm burada ayrışır) | Yürür, **E** | `Bathroom/Door` |

**Kurallar:** Oyuncu sadece bir etkileşim noktasını beklerken yürüyebilir (`UsePoint`). Diyalog, haber ve karar sırasında hareket kapalıdır. Odalar arası geçişte ekran kısa kararır (`EnterRoom`). Karo ölçüsü, piksel ölçeği ve kamera değerleri karo seti gelince `technical/ISIMLENDIRME_VE_KLASORLER.md`'ye yazılacak.

---

## 6. SEL bölümü — "Bilinçli ol, güvende kal"

> **Not (güncel, lider kararı 17 Eki):** Oynanış için geçerli Sel akışı artık **§6.0 Akşam mahalle seli**dir. Aşağıdaki §6.1–6.3 eski sabah akışıdır (Bedroom → Bathroom → FloodChapter: mutfak, sokak, otobüs, okul yolu); başvuru için korunmuştur, dosyaları silinmedi ama oyunda kullanılmaz (Build Settings'te kapalı).

### 6.0 Akşam mahalle seli (yeni, uygulanıyor)

Sahne: `Assets/Scenes/Chapters/Flood/FloodEvening.unity`, kod `Assets/Scripts/Chapters/FloodEvening/` (tek akış/süre/sonuç sorumlusu `FloodEveningController`). `GameSceneRouter.LoadChapter(ChapterId.Flood)` bu sahneyi açar (menüdeki Sel bulutu ve depremden sonra "Devam: Sel Bölümü"). Ana karakter **Cenk**, deprem modundaki görünümüyle (çantasız yelekli; lider kararı: yağmurluk/çanta yok, `Cenk.prefab` aynen). Toplam ≈ 3–4 dk.

**Bölüm sırası kesindir: DEPREM → SEL.** Deprem raporu (AFET FARKINDALIK RAPORU) okunduktan sonra birincil buton **[Devam: Sel Bölümü]** (1 / Enter / fare): menüye dönmeden kısa kararma → Sel sahnesi. **[Tekrar Dene]** depremi baştan açar, **[Menüye Dön]** menüye döner. Geçiş yalnız bir kez tetiklenir (`GameSceneRouter.ContinueToNextChapter`). Sel'in "Tekrar Dene"si yalnız Sel'i yeniden açar, depremi başlatmaz.

**Akış (durumlar: Intro → Warning → RouteChoice → SafeRoute → Gate → CodePanel → FinalApproach → Success / Timeout)**

| # | Durum | Ne olur | Oyuncu |
|---|---|---|---|
| A0 | Intro | Siyah ekranda **"SEL BÖLÜMÜ"** kartı + *"Başka bir gün — şiddetli yağışlı bir akşam"* (depremin seli tetiklediği ya da hasarlı eve dönüldüğü izlenimi yok). Kararma açılır: akşam, koyu mavi mahalle, sıcak sokak lambaları, hafif yağmur; kamera mahalleden Cenk'e kayar (`FE_INTRO`). | E/Space/Enter/tık ile kart geçilebilir |
| A | Warning | Cenk kuru ve güvenli noktada; telefon bildirimi (oyun içi kurgu, kurum logosu yok): *"Şiddetli yağış nedeniyle su baskını riski var. Suyla kaplı ve kapatılmış yollara girmeyin. Belirlenmiş güvenli yüksek bölgeye yönelin."* (`AlertNotification`, `FloodWarningPhone`) | **[Tamam]** fare/klavye; aynı girdi sonraki etkileşimi tetiklemez |
| B | (gizli süre) | Uyarı kapanıp kontrol açılınca **120 sn** aktif oynanış süresi başlar. Sayısal geri sayım YOK; sağda **"SU SEVİYESİ"** çevresel göstergesi (altında "Suya hiç girme!"), yağmur, çevredeki su ve aşamalı uyarılar (≈%33 *Yağış artıyor*, ≈%66 *Su yükseliyor*, ≈%85 *Dikkatli ol*). Süre diyalog, okuma paneli (söylenti, ipucu), geçiş ve sonuç ekranında durur; aktif mini oyunlarda (dal, kapı, şifre) akar. Süre, su göstergesi, yağmur ve çevresel su aynı ilerleme değerini kullanır (`FloodEveningHub.Progress01`). | Dolaşır |
| C | RouteChoice | Yol ayrımı: **alçak yol** (kısa, su basmış, kapatılmış) ve **yüksek yol** (uzun, kuru, işaretli güvenli rota). Alçak yola yönelen Cenk suya ulaşmadan bariyerde durdurulur ve geri itilir: *"Bu yol kapalı. Suyun derinliğini ve akıntısını bilemezsin. Güvenli rotaya dön."* Yanlış tercih sonuç için kaydedilir; kısa yol hiçbir avantaj vermez. | Yüksek yola yürür |
| D | SafeRoute | **Söylenti kontrolü:** *"Kestirme açıkmış, herkes oradan gidiyor."* — Kaynak: bilinmiyor. Duyuru kontrol edilince kapalı yol ve güvenli rota bilgisi gösterilir; doğrulanmamış söylenti doğru seçenek değildir. | [E] panoyu incele |
| E | SafeRoute | **Geçit:** kuru ve korunaklı geçitte devrilmiş ağaç/dal çocuğa taşıtılmaz; *engeli aşmaya çalışma, işaretli alternatif geçidi aç* — [E] + kısa ilerleme çubuğu, bir kez. | [E] |
| F | SafeRoute | **Rüzgâr:** açık ve kuru geçitte yön ikonu + kısa açıklama; hareketi hafifçe yavaşlatır, suya/tehlikeye itmez; diyalog/UI sırasında ve korunaklı alanda kapalı. "Rüzgârı takip et" mesajı verilmez; güvenli rota işaretleri belirleyicidir. | Yürür |
| G | Gate | **Bahçe kapısı** (güvenli yüksek bölgenin kuru girişi): [E] Kapıyı aç → kolu fareyle dairesel çevir ya da klavye eşdeğeri, net ilerleme göstergesi; tamamlanınca kalıcı açık; uzaklaşma/iptalde kontrol geri gelir. | Fare / klavye |
| H | CodePanel | **Şifre paneli** (korunaklı giriş): 4 hane (**4725**), ipuçları aynı güvenli alanda, okunur ve tekrar incelenebilir, tek anlamlı; fare + klavye rakamları, Sil / Onayla / Kapat; yanlış kod açıklama verir, kilitlenme yok; panel açıkken yürüme yok. Oyuncu ipucu için sel bölgesine gönderilmez. | Rakamlar |
| I | FinalApproach | Yağmur ve ses artar, su yalnız çevresel tehlike alanlarında yükselir. Hedef: **"Belirlenmiş güvenli yüksek bölgeye ulaş."** Herhangi bir ev/apartman otomatik güvenli sayılmaz; hedef oyun içinde işaretli, selden etkilenmeyen, deprem hasarı olmayan alandır. | Yürür |
| J1 | Success | Güvenli alana girince süre ve çevresel tehlike durur: **"Güvenli bölgeye ulaştın."**, doğru kararlar ve düzeltilen yanlışlar | — |
| J2 | Timeout | Karakter boğulurken/suya girerken gösterilmez; olduğu kuru noktada durur: *"Rota için ayrılan oyun süresi doldu. Güvenli yerde kal ve yardım yönlendirmelerini izle."* Aynı karede varış ve süre dolması → başarı (tek sonuç). | — |
| J | Sonuç | Öğrenilenler: Suyla kaplı yola girme · Kapatılmış yolları kullanma · Söylentinin kaynağını doğrula · Güvenli yüksek bölgelere ilişkin resmî yönlendirmeleri izle · Eve ulaşmak uğruna risk alma. Alt not: 120 sn yalnızca oyunun süresidir. | **[Sel Bölümünü Tekrar Dene]** [1] / **[Menüye Dön]** [2] (fare, ←/→ + Enter) |

**ID'ler:** diyalog `Assets/Data/Story/flood_evening.json` (`FE_*`); görevler `Assets/Resources/Objectives/objectives_flood_evening.json` (`FE_WARN`, `FE_HIGH`, `FE_RUMOR` bonus, `FE_PASSAGE`, `FE_GATE`, `FE_CODE`, `FE_SAFE`); modül adımları (Hub): `warning_read`, `high_road`, `rumor_checked`, `branch_cleared`, `gate_opened`, `code_opened`, `safe_zone`; yanlışlar: `low_road`, `rumor_trusted`, `wrong_code`.

### 6.1 Zaman çizelgesi (ESKİ sabah akışı — oyunda kullanılmıyor)

| # | ID | Süre | Mekân | Olay | Oyuncu ne yapar | Kim yapar |
|---|---|---|---|---|---|---|
| 1–2 | `F01_WAKE`, `F02_WASH` | 0:00–0:50 | Oda, banyo | Ortak açılış (§5) | E ile kalk, yüzünü yıka | Mahmut |
| 3a | `F03_MOTHER_CHAT` | 0:50–1:05 | Mutfak | Çocuk mutfağa girer, anne masanın yanında. Sıcak sabah sohbeti: Anne *"Günaydın canım! Kahvaltı hazır, gel otur."* → kahvaltı, okul. | Diyaloğu geçer | Mahmut (sahne, akış) · Arda (replik) |
| 3b | `F03_TV_JINGLE` | 1:05–1:10 | Mutfak (TV) | TV'den haber **jingle**'ı (ses kancası `UnityEvent`, dosya gerekmez). Anne: *"Aa, haberler başladı. Bir bakalım."* Çocuk TV'ye döner. | Diyaloğu geçer | Mahmut (jingle kancası) · Arda (replik) |
| 3c | `NEWS_F03_TV` → `F03_NEWS` | 1:10–1:35 | Mutfak (TV) | TV: **SON DAKİKA — BUGÜN ŞİDDETLİ YAĞMUR BEKLENİYOR**, renk lejantı, alt yazı. Çocuk: *"Şiddetli yağmur mu? Okul yolumuzda hep su birikiyor..."* | Haberi izler, diyaloğu geçer | Eren (TV) · Arda (replik) |
| 4a | `F04_MOTHER` → `F_D1` | 1:35–1:55 | Mutfak | **Anne tekrar sorar:** *"Duydun mu canım? Bugün şiddetli yağmur bekleniyormuş."* Çocuk: *"Sence okula gitmeli miyim?"* → **Karar F_D1** | Karar verir | Arda (diyalog+karar) · Mahmut |
| 4b | `F04_CHECK` → `NEWS_F04_OFFICIAL` → `F04_PREPARE` | 1:55–2:15 | Mutfak | Anne: *"Telefondan belediyeye bakalım. Resmî duyuru gelmeden karar vermeyelim."* → **resmî duyuru kartı** (okullar açık, su birikintilerinden uzak durun) → Anne: *"Şemsiye ve yağmurluk al. Yolda oyalanma, sudan uzak dur lütfen."* | Diyaloğu geçer | Eren (kart) · Arda (replik) · Mahmut |
| 4c | (Coat, Umbrella, ExitDoor) | 2:15–2:25 | Mutfak kapısı | Askıda sarı yağmurluk, şemsiyelikte şemsiye (yatak odasında alındıysa şemsiye adımı atlanır). | **[E] Yağmurluğu al**, **[E] Şemsiyeyi al**, **[E] Dışarı çık** | Mahmut |
| 5a | `F05_EXIT` / `F05_EXIT_GEAR` | 2:25–2:30 | Evin önü, kaldırım | Kararma → hafif yağmur. *"Yağmur başlamış bile..."* · yağmurluk/şemsiye alındıysa *"İyi ki yağmurluğumu giydim..."* | — | Mahmut · Arda (replik) |
| 5b | `F05_STREET` (+ `F05_PUDDLE_WARNING`, `F05_DRAIN_WARNING`) | 2:30–3:00 | Uzun izometrik sokak | Yağmur artar, yol kenarında su birikintileri, rögar. Çocuk: *"Sudan, rögarlardan ve elektrik direklerinden uzak yürümeliyim."* Birikintiye/yola basarsa durur, uyarı balonu ve geri itilir (ceza yok); rögar yakınında da uyarı. | Durağa yürür | Mahmut |
| 5c | `F05_POSTER` (isteğe bağlı) → `F05_DOWNPOUR` | 3:00–3:20 | Otobüs durağı (cam sundurma) | **[E] Afişi oku:** *"DİKKATLİ OLUN! Sağanak yağışta su birikintilerinden ve rögarlardan uzak durun."* Sundurma altına girince yağmur 5 sn içinde **sağanağa** döner (sahne kararır, su yükselir, şimşek/flaş yok). *"Yağmur iyice arttı... Sundurmanın altında beklemeliyim."* | Afişi okur, sundurmada bekler | Mahmut |
| 5d | `F05_BUS_ARRIVE` | 3:20–3:35 | Durak önü | Otobüs gelir, kapısı açılır. *"Otobüs geldi, hemen binelim!"* → **[E] Otobüse bin** → kapı kapanır, otobüs hareket eder, kararma. | **[E] Otobüse bin** | Mahmut |
| 6a | `F05_BOARD` → `F06_RUMORS` → `NEWS_F06_RUMORS` → `F_D2` | 3:35–4:15 | **Otobüs içi** (`World/BusInterior`, `BusInterior.prefab`): koltuklar, direkler, yağmurlu camlardan geçen şehir, şoför | Çocuk: *"Günaydın! Oh be, içerisi kupkuru."* Üç arkadaş (Arkadaş 1–3, balonlar) Okul Caddesi hakkında farklı şeyler söyler. Çocuk: *"Telefondan bir bakayım."* → **söylenti kartları** (telefon gibi, "Kaynak: bilinmiyor") → **Karar F_D2**. **Yanlışta (A/B):** kararma → okul durağındaki Okul Caddesi'nin kenarı: çocuk suya **girmeden** bariyerin önünde durur, suda mahsur araç görünür, `F07_RISK_WARNING` → otobüse dönülür, karar tekrar sorulur. **D'de** resmî yol duyurusu (`NEWS_F06_OFFICIAL_ROAD`). | Söylentileri okur, kartı seçer, karar verir | Mahmut (sahne, akış) · Arda (replik) · Eren (kartlar) |
| 6b | `F06_DECIDED` → `F06_ARRIVE` | 4:15–4:35 | Otobüs içi | *"Sudan geçmeyeceğim. Köprü üstündeki yaya yolundan giderim."* Otobüs yavaşlayıp durur, LED tabela "DURAK: OKUL", şoför: *"Okul durağı!"*, kapı açılır. | **[E] Otobüsten in** (kapıya yürür) | Mahmut |
| 7 | `F07_ARRIVE` → `F07_RISK` (+ `F07_WATER_WARNING`) | 4:35–5:05 | **Okul yolu** (`World/SchoolRoute`, `SchoolRoute.prefab`): okul durağı, su basmış Okul Caddesi | Kararma → çocuk otobüsten iner, otobüs gider, yağmur hafiflemiş. *"Okul Caddesi tamamen su altında!"* Bariyer, **"SU BİRİKİNTİSİ TEHLİKELİDİR"** tabelası, suda mahsur kalmış araç (dörtlü flaşör) **uzaktan, kaldırımdan** görülür. Suya doğru yürüyünce durur, `F07_WATER_WARNING` balonu ve geri itilir (ceza yok, sonuçta sayılır). | **[E] Tabelayı incele** (büyük tabela açılır) | Mahmut |
| 8 | `F08_BRIDGE` → `F08_SAFEROUTE` | 5:05–5:35 | Köprü üstü yaya yolu → okul kapısı | *"Köprü üstündeki yaya yolu güvenli."* Çocuk köprüden su basmış caddenin üstünden geçer, okul kapısında öğretmen: *"Günaydın! Güvenli yoldan gelmene çok sevindim."* | Yürür | Mahmut |
| 9 | `F09_RESULT` | ≈20 sn | Sonuç ekranı (sahnenin üstünde) | Başlık, istatistik şeridi (kararlar, yağmurluk/şemsiye, suya yaklaşma uyarısı sayısı), yeşil **Öğrenilenler**, kırmızı **Yanlış davranışların** (her biri kısa açıklamayla; yanlış/uyarı yoksa gizli) (§8). Geçici görünüm: `FloodResultView` (Mahmut) — Berk kendi `ResultViewBase` panelini bağlayınca yerini alır. | **Tekrar Dene** [1] / **Menüye Dön** [2] (fare veya ← → + Enter) | Berk (ekran) · Mahmut |

> **Otobüse binişten sonrası (uygulandı).** Söylentiler ve F_D2 **otobüste** geçer; arkadaşlar bir sonraki durakta iner, çocuk okul durağında yalnız iner. Riskli bölge (F07) ve güvenli rota (F08) okul durağının önündedir: Okul Caddesi su altında, köprü üstü yaya yolu caddenin üstünden okul kapısına çıkar. Bölüm toplamı ≈5,5–6 dk (oyuncuya göre).

### 6.2 Kararlar

**F_D1 — "Haberde şiddetli yağmur uyarısı var. Ne yapalım?"** (süresiz, 3 seçenek, 1 doğru)

| ID | Tuş | Seçenek | Doğru? | Geri bildirim | Sonuçta "yanlış davranış" |
|---|---|---|---|---|---|
| F_D1_A | 1 | Resmî duyuruları kontrol edelim. | ✅ | Doğru! Kararı resmî duyurulara göre vermek en güvenlisi. | — |
| F_D1_B | 2 | Bir şey olmaz, hemen çıkayım. | ❌ | Uyarıyı küçümsemek riskli. Önce resmî duyurulara bakmalısın. | Sel uyarısını ciddiye almamak |
| F_D1_C | 3 | Sosyal medyada ne diyorlar bakayım. | ❌ | Kaynağı belirsiz paylaşımlar yanıltıcı olabilir. Resmî kaynağa bak. | Sosyal medya söylentilerine inanmak |

**F_D2 — "Herkes farklı bir şey söylüyor. Ne yapmalısın?"** (süresiz, 4 seçenek, 2 doğru, **birini bulmak yeterli**)

| ID | Tuş | Seçenek | Doğru? | Geri bildirim | Sonuçta "yanlış davranış" |
|---|---|---|---|---|---|
| F_D2_A | 1 | Arkadaşlarınla geç. | ❌ | Suyla kaplı yolun derinliği ve akıntısı görünenden farklı olabilir. Asla girme. | Su birikintisinden geçmek |
| F_D2_B | 2 | Riskli yolu dene. | ❌ | Yollar bir sebeple kapatılır. Kapanan yolu kullanma. | Kapanan yolları kullanmak |
| F_D2_C | 3 | Farklı bir rota seç. | ✅ | Doğru! Su birikintilerinden uzak, güvenli bir rota seçtin. | — |
| F_D2_D | 4 | Resmî duyuruları kontrol et. | ✅ | Doğru! Söylentiye değil, resmî duyuruya güvendin. → ardından **yol kapalı duyurusu** kartı | — |

**Yanlış seçimde (F_D2_A, F_D2_B):** çocuk suya **girmeden** ekran durur. Riskli bölge görüntüsü gösterilir. Çocuk: *"Dur! Şu arabaya bak, suyun içinde mahsur kalmış... Suyun ne kadar derin olduğunu bilemem."* Ardından karar tekrar sorulur. Tehlikeli davranış hiçbir zaman başarıyla gösterilmez.

### 6.3 Haber ve kart içerikleri (Eren)

| ID | Tür | İçerik |
|---|---|---|
| `NEWS_F03_TV` | TV haberi | Bant: **SON DAKİKA** · Başlık: **BÖLGEDE ŞİDDETLİ YAĞMUR BEKLENİYOR** · Alt yazı: *Sel ve su baskını riskine karşı resmî uyarıları takip edin, suyla kaplı yollardan uzak durun.* · Harita + renk lejantı (kurgu): Kırmızı (Çok tehlikeli), Turuncu (Tehlikeli), Sarı (Dikkatli), Yeşil (Normal) |
| `NEWS_F04_OFFICIAL` | Resmî duyuru kartı (telefon ekranı) | *Okullar bugün açık. Vatandaşlarımız su birikintilerinden ve kapalı yollardan uzak durmalıdır.* — Kaynak: Belediye duyurusu (kurgu) |
| `NEWS_F06_RUMORS` | 3 söylenti kartı | "Yol açık, rahat geçilir!" · "Sosyal medya: Su azalmış!" · "Biraz su var sadece, abartıyorlar." — üçünde de **Kaynak: bilinmiyor** |
| `NEWS_F06_OFFICIAL_ROAD` | Resmî duyuru kartı | *Okul Caddesi su baskını nedeniyle kapalıdır. Yayalar köprü üstü yaya yolunu kullanmalıdır.* — Kaynak: Belediye duyurusu (kurgu) |

Hiçbir kartta gerçek kurum adı veya logosu (AFAD, MGM, valilik logosu vb.) kullanılmaz. Renk lejantı kurgudur, "her yerde bu anlama gelir" iddiası yoktur.

---

## 7. DEPREM bölümü — "Sarsıldığımızda doğru hareket et"

> **Not (güncel):** Oynanış için geçerli akış artık **§7.4 Deprem modu: 120 sn farkındalık simülasyonu**dur. Aşağıdaki §7.1–7.3 eski akıştır; başvuru için korunmuştur ve yeni sahnede (`EarthquakeHome.unity`) kullanılmaz.

### 7.1 Zaman çizelgesi

| # | ID | Süre | Mekân | Olay | Oyuncu ne yapar | Kim yapar |
|---|---|---|---|---|---|---|
| 1–2 | `E01_WAKE`, `E02_ROUTINE` | 0:00–0:50 | Oda, banyo | Ortak açılış (§5) | E ile kalk, yüzünü yıka | Mahmut |
| 3 | `E03_NEWS` | 0:50–1:30 | Mutfak (TV) | Kahvaltı masasına oturur (bu masa birazdan "sağlam masa" olacak). TV: **SON DAKİKA — BÖLGEDE HAFİF SARSINTILAR HİSSEDİLDİ**. Çocuk endişelenir. Anne: *"Depremin ne zaman olacağını kimse bilemez ama hazırlıklı olmak bizim elimizde. Deprem çantamız kapının yanında, toplanma alanımız parktaki alan, unutma."* Anne işe çıkar. | **[E] Kahvaltı yap**, haberi izler, diyaloğu geçer | Eren (TV) · Arda (replik) · Mahmut |
| 4 | `E04_QUAKE` | 1:30–1:45 | Mutfak | **Ani sarsıntı:** kamera sarsılır, lamba sallanır, gürleme sesi. *"Ne oluyor?! Her yer sallanıyor!"* → **Karar E_D1** (5 seçenek, **5 sn**) | 5 saniyede seçer | Arda (karar) · Mahmut (efekt) |
| 5 | `E05_DROPCOVER` | 1:45–2:05 | Masanın altı | Doğru seçimden sonra **ÇÖK (S) → KAPAN (A) → TUTUN (D basılı)**. Sarsıntı bitene kadar (≈4 sn) D basılı tutulur, erken bırakılırsa *"Sarsıntı bitene kadar tutun!"* uyarısı çıkar. | S, A, D | Mahmut |
| 6 | `E06_AFTER` | 2:05–2:45 | Hasarlı mutfak | Sarsıntı biter, ortalık dağınık. *"Sarsıntı durdu... Sakin olmalıyım."* → **Karar E_D2** (2 doğru davranışı bul) | Seçer; çanta alınınca HUD'da görünür | Arda (karar) · Mahmut |
| 7 | `E07_EXIT` | 2:45–3:15 | Bina koridoru, merdiven (**ACİL ÇIKIŞ** tabelası) ve asansör | *"Çantamı aldım, ocak kapalı. Şimdi binadan çıkmalıyım."* → **Karar E_D3** | Seçer, merdivenden iner | Arda (karar) · Mahmut |
| 8 | `E08_ASSEMBLY` | 3:15–3:45 | Toplanma alanı (park, **TOPLANMA ALANI** tabelası) | Anne gelir: *"Canım, iyi misin?"* Çocuk: *"İyiyim anne. Masanın altında çöktüm, kapandım, tutundum!"* Komşu: *"Herkes iyi mi? Resmî açıklamaları takip edelim."* Görevli: *"Binalar kontrol edilmeden kimse içeri girmesin."* | Diyaloğu geçer | Arda · Mahmut |
| 9 | `E09_RESULT` | 3:45–4:05 | Sonuç ekranı | Öğrenilenler + yapılan yanlışlar (§8) | Tekrar Dene / Menüye Dön | Berk (ekran) · Mahmut |

### 7.2 Kararlar

**E_D1 — "Sarsıntı başladı! Ne yaparsın?"** (**5 saniye**, 5 seçenek, 1 doğru)

| ID | Tuş | Seçenek | Doğru? | Geri bildirim | Sonuçta "yanlış davranış" |
|---|---|---|---|---|---|
| E_D1_1 | 1 | Sağlam masanın altına Çök–Kapan–Tutun | ✅ | Doğru! Baş ve boynunu koru, sarsıntı bitene kadar tutun. | — |
| E_D1_2 | 2 | Pencereye koş | ❌ | Camlar kırılabilir. Pencerelerden uzak dur. | Pencereye koşmak |
| E_D1_3 | 3 | Merdivene koş | ❌ | Sarsıntı sırasında koşmak düşmene ve yaralanmana yol açabilir. Merdiven, sarsıntı bittikten sonra binadan çıkmak içindir. | Sarsıntı sırasında merdivene koşmak |
| E_D1_4 | 4 | Asansöre git | ❌ | Deprem sırasında asansör kullanılmaz; elektrik kesilirse mahsur kalırsın. | Asansör kullanmak |
| E_D1_5 | 5 | Balkona çık | ❌ | Balkonlar sarsıntıda tehlikeli olabilir. İçeride güvenli bir noktada kal. | Balkona çıkmak |
| — | süre doldu | — | — | Sarsıntı başladı! Donup kalma: hemen güvenli bir yerde Çök–Kapan–Tutun. | — |

- Seçenekler sayaç başlamadan görünür, sayaç 5 → 0 akar. Hızlı ışık efekti (flaş) kullanılmaz.
- **Yanlış seçimde** karakter tehlikeli yere koşarken gösterilmez. Eylemden **önce** ekran durur, açıklama çıkar, karar sayaç sıfırlanarak tekrar sorulur. Süre dolduğunda da aynısı olur.
- (P1) Seçeneklerin sırası her oynanışta karıştırılabilir, böylece doğru cevap hep 1 olmaz.

**E_D2 — "Sarsıntı durdu. Ne yapmalısın?"** (süresiz, 4 seçenek, **2 doğru, ikisi de bulunmalı**)

| ID | Tuş | Seçenek | Doğru? | Geri bildirim | Sonuçta "yanlış davranış" |
|---|---|---|---|---|---|
| E_D2_A | 1 | Hemen dışarı koş. | ❌ | Panikle koşmak yaralanmana yol açabilir. Önce sakin ol, çantanı al ve tehlikeleri kontrol et. | Aceleyle dışarı koşmak |
| E_D2_B | 2 | Asansörü kullan. | ❌ | Deprem sonrası asansör kullanılmaz; elektrik kesilebilir, mahsur kalabilirsin. | Asansör kullanmak |
| E_D2_C | 3 | Acil durum çantasını al. | ✅ | Doğru! Çantanda su, el feneri, düdük ve ilk yardım malzemeleri var. | — |
| E_D2_D | 4 | Ocağı ve doğalgazı kontrol et. | ✅ | Doğru! Açık ocak yangına yol açabilir. Gaz kokusu varsa hiçbir düğmeye dokunma ve büyüklerine haber ver. | — |

Doğru seçilen seçenek işaretli kalır, iki doğru da bulununca karar biter.

**E_D3 — "Binadan nasıl çıkarsın?"** (süresiz, 2 seçenek)

| ID | Tuş | Seçenek | Doğru? | Geri bildirim | Sonuçta "yanlış davranış" |
|---|---|---|---|---|---|
| E_D3_A | 1 | Asansörle inerim. | ❌ | Deprem sonrası asansör kullanılmaz; elektrik kesilirse mahsur kalırsın. | Asansör kullanmak |
| E_D3_B | 2 | Merdivenden sakince inerim. | ✅ | Doğru! Sarsıntı bittikten sonra merdivenden, sakin şekilde çık ve toplanma alanına git. | — |

### 7.3 Haber içeriği (Eren)

| ID | Tür | İçerik |
|---|---|---|
| `NEWS_E03_TV` | TV haberi | Bant: **SON DAKİKA** · Başlık: **BÖLGEDE HAFİF SARSINTILAR HİSSEDİLDİ** · Alt yazı: *Uzmanlar hatırlatıyor: Deprem çantanızı ve toplanma alanınızı kontrol edin.* · Kırmızı tonlu bölge haritası |

Depremler önceden tahmin edilemez. Haber "deprem olacak" demez, sadece **hazırlık** hatırlatır. Telefona gelen "erken uyarı" bildirimi kullanılmaz, çünkü oyuncuya uyarının her zaman geleceği izlenimini verir.

### 7.4 Deprem modu: 120 sn farkındalık simülasyonu (yeni, uygulanıyor)

Bu bölüm, oynanışta **§7.1'deki eski akışın yerini alır** (eski metin başvuru için yukarıda duruyor). Sahne: `Assets/Scenes/Chapters/Earthquake/EarthquakeHome.unity`; `GameSceneRouter.LoadChapter(ChapterId.Earthquake)` bu sahneyi açar. Ana karakter Cenk'tir. Toplam süre ≈ 3–5 dk.

**Veri dosyaları**

| Dosya | İçerik |
|---|---|
| `Assets/Data/Earthquake/earthquake_mode.json` | Telefon bildirimi, 18 incelenebilir nesne (ad, açıklama, tehlike sınıfı), uyarılar, anne soruları Q1–Q4, korunma (Ç-K-T) metinleri, sarsıntı sonrası metinleri, rapor etiketleri ve ipuçları |
| `Assets/Data/Story/earthquake_home.json` | Konuşma balonları (`EQH_*`, konuşanlar "Çocuk" ve "Anne") |

**Akış**

| # | Evre | Ne olur | Oyuncu ne yapar |
|---|---|---|---|
| A | Uyanış + telefon | Saat 07:00, Cenk uyuyor. Komodindeki telefon titrer ve çalar (`EQH_WAKE`). Cenk uyanır, kamera telefona kayar. Ekrana bildirim gelir: **AFET FARKINDALIK SİMÜLASYONU** / **DEPREM SENARYOSU: 02:00** / *Çevreni incele, riskleri belirle ve güvenli davranışları seç!* Alt not: *Bu bir eğitim simülasyonudur. Depremler önceden kesin olarak tahmin edilemez.* | **[BAŞLA]** (E / Enter / Space / fare) |
| B | 120 sn hazırlık | Sağ üstte geri sayım başlar (`EQH_PHONE_AFTER`). Sayaç rengi: 120–61 beyaz, 60–31 amber, 30–11 kırmızı, 10–0 hafif nabız + tik sesi. Bilgi kartı açıkken, soru sırasında ve oda geçişlerinde sayaç **durur**. | Yatak odası ve salon-mutfakta gezer, E ile nesneleri inceler |
| B1 | İnceleme | Her nesnenin kısa öğretici kartı vardır. Sınıflar: **Tehlike** (pencereler, ayna, kitaplık, gardırop, vitrin, buzdolabı), **Korunma yeri** (çalışma masası, yemek masası), **Güvenlik** (deprem çantası, ocak, merdiven, toplanma tabelası), **Bilgi** (telefon, anne), **Çıkış** (kapılar, asansör). İncelemek hiçbir zaman cezalı değildir. | E |
| B2 | Ayna tehlike bölgesi | Aynanın önüne girince uyarı: **CAM TEHLİKESİ!** *Deprem sırasında cam ve kırılabilecek eşyalardan uzak dur.* Uyarıdan sonra bölgede 3 sn daha kalırsa **bir kerelik −10 sn**. Çıkıp tekrar girmek yeni ceza getirmez. | Bölgeden uzaklaşır |
| B3 | Annenin soruları | Anne her soruda portresiyle çıkar, **5 sn** soru sayacı akar (120 sn sayacı durur). Doğru: ceza yok, kısa olumlu geri bildirim. Yanlış: **−30 sn** + kısa açıklama. Süre dolarsa: **SÜRE DOLDU!**, **−30 sn**, doğru cevap açıklanır. Tetikleme: Q1 oyuncu anneye ilk kez E ile konuştuğunda ya da sayaç 95'e indiğinde (anne seslenir: `EQH_MOTHER_CALL`), Q2 sayaç 65'te, Q3 sayaç 35'te. | 1–4 tuşları veya fare |
| B4 | Hazırlık tamam | Q1–Q3 soruldu ve en az 4 nesne incelendiyse ipucu çıkar: *Hazırlığın tamam! Güvenli bir yerde bekle. [Space] Simülasyonu hızlandır* (`EQH_READY`). Space kalan süreyi 5 sn'ye indirir, ceza değildir. | İsterse Space |
| C | Sarsıntı | Sayaç 0 olunca **SARSINTI!** (`EQH_QUAKE`): kamera sarsılır, gürleme, eşyalar titrer, lamba sallanır, kitaplıktan kitaplar düşer, toz; flaş yok. Sarsıntı ≈ 8 sn. | Korunur (aşağıda) |
| C1 | Korunma | Cenk sağlam masaya yakınsa (≈1,2 birim) masanın altında: **ÇÖK** (E) → **KAPAN** (E, başını ve enseni koru) → **TUTUN** (E basılı ≈ 6 sn). Bırakırsa *"Sarsıntı bitene kadar tutun!"* uyarısı çıkar, ilerleme durur. Masadan uzaksa olduğu yerde **ÇÖK–KAPAN** (doğru davranış, puanı masadan az). Sarsıntı başladıktan sonra masaya koşmak ödüllendirilmez. Pencere/ayna/kitaplık bölgesindeyse ek geri bildirim ve "tehlikeye maruz kalma" kaydı (yaralanma animasyonu yok). 3 sn tepki yoksa ipucu; hiç korunmazsa raporda "Korunma yapılmadı". | E, E, E basılı |
| D | Sarsıntı sonrası | Toz çöker; yerde kitaplar, pencere ve ayna önünde cam kırıkları. Anne: *"Cenk! İyi misin?"* (`EQH_AFTER_MOTHER`) → **Q4** (5 sn). Cam kırıklarının üstünden geçince uyarı (ceza yok, kayıt). Deprem çantası **şimdi** alınır (`EQH_BAG`); sarsıntı sırasında asla. Daire kapısı açılır (sarsıntıdan önce kilitliydi: "Sarsıntı sırasında dışarı koşulmaz"). | Tehlikeleri kontrol eder, çantayı alır, çıkar |
| D1 | Koridor | **Asansör**: E → *"Asansör kullanılmaz!"* (yanlış karar kaydı, ilerletmez). **Merdiven** + ACİL ÇIKIŞ işareti: E → *"Merdivenden sakince inelim."* (`EQH_EXIT`) | Merdiveni seçer |
| D2 | Toplanma alanı | Park, TOPLANMA ALANI tabelası. Anne ile kısa konuşma (`EQH_ASSEMBLY`): bina kontrol edilmeden içeri girilmez, resmî duyurular takip edilir. | Diyaloğu geçer |
| E | Rapor | **AFET FARKINDALIK RAPORU** (aşağıda) | **Devam: Sel Bölümü** (birincil, menüsüz Sel'e geçer, §6.0) / Tekrar Dene (deprem baştan) / Menüye Dön |

**Anne soruları** (metinler birebir, ✅ doğru)

| ID | Soru | Şıklar |
|---|---|---|
| Q1 | Deprem sırasında ne yapmalıyız? | Pencereye koşmalıyız / **Sağlam bir masanın altında Çök–Kapan–Tutun yapmalıyız** ✅ / Asansöre binmeliyiz / Merdivene koşmalıyız |
| Q2 | Deprem sırasında asansörü kullanmak doğru mu? | Evet, daha hızlı ineriz / **Hayır, asansörden uzak durmalıyız** ✅ / Sadece elektrik varsa kullanabiliriz |
| Q3 | Deprem sırasında camlara yakın durmalı mıyız? | Evet / **Hayır, camlardan uzak durmalıyız** ✅ / Pencereden dışarı bakmalıyız |
| Q4 | Sarsıntı bittikten sonra ilk ne yapmalıyız? (sarsıntı sonrası) | **Çevremizdeki tehlikeleri kontrol etmeliyiz** ✅ / Hemen asansöre binmeliyiz / Hasarlı binaya geri dönmeliyiz |

Her yanlış şıkkın kısa açıklaması `earthquake_mode.json` içindedir.

**Süre cezaları** (Inspector'dan ayarlanır): yanlış cevap −30 sn, süre dolması −30 sn, aynada uzun kalma −10 sn (bir kez), doğru cevap ve inceleme 0. Sayaç asla negatife düşmez; aynı olay üst üste ceza vermez.

**Rapor ve etiketler**

Raporda: doğru/yanlış cevap ve süre dolması sayısı, bulunan tehlikeler (incelenen/toplam), gereksiz tehlike maruziyeti, korunma (*Masa altında Ç-K-T* / *Yerinde Ç-K* / *Yapılmadı*), çanta alındı mı, asansör denendi mi, depremden önce kalan süre (hızlandırma kullanıldıysa "Hazırlık tamam"). Her yanlış için kısa açıklama ve ipucu gösterilir. Puan çoğunlukla doğru kararlardan ve tehlike tanımadan gelir; kalan süre puanın küçük bir kısmıdır.

| Puan | Etiket |
|---|---|
| 85+ | AFET BİLİNCİ USTASI |
| 65–84 | HAZIRLIKLI |
| 40–64 | GELİŞİYOR |
| 0–39 | DAHA FAZLA ÖĞREN |

**Güvenlik ilkeleri (metinlerin uyduğu kurallar):** depremlerin önceden tahmin edilebileceği hiçbir yerde ima edilmez (120 sn yalnızca eğitim sayacıdır); deprem anında sağlam masanın altında Çök–Kapan–Tutun, masa yoksa olduğun yerde başı ve enseyi korumak; pencere, cam, ayna ve sabitlenmemiş uzun eşyalardan uzak durmak; uzun mobilyalar depremden **önce** duvara sabitlenir (çocuk bunu bir büyüğüne söyler), sarsıntı sırasında sabitlemeye çalışılmaz; sarsıntıda dışarı veya merdivene koşulmaz, asansör hiç kullanılmaz; sarsıntıdan sonra cam kırıkları ve gaz kokusu kontrol edilir (gaz kokusu varsa düğmelere dokunulmaz), deprem çantası alınır, merdivenden sakince inilir, toplanma alanına gidilir, resmî duyurular takip edilir ve hasarlı binaya geri dönülmez. Kapı eşiği, merdiven veya pencere hiçbir yerde "güvenli yer" olarak gösterilmez.

---

## 8. Sonuç ekranı (Berk ekranı yapar, içerik veriden gelir)

| | Sel | Deprem |
|---|---|---|
| **Başlık (hiç yanlış yoksa)** | Tebrikler! Bilinçli davrandın. | Tebrikler! Doğru davrandın. |
| **Başlık (yanlış varsa)** | Bölümü tamamladın! Yanlışlarından ders çıkardın. | Bölümü tamamladın! Yanlışlarından ders çıkardın. |
| **✅ Öğrenilenler** | Sel uyarılarını ciddiye al · Sosyal medyadaki bilgileri sorgula · Su birikintilerinden uzak dur · Kapanan yolları kullanma · Güvenli alternatif rota seç · Resmî kurumların duyurularını takip et | Sarsıntı anında ÇÖK – KAPAN – TUTUN · Sarsıntı bitince sakin kal · Asansör kullanma · Acil durum çantanı hazır tut · Resmî açıklamaları takip et · Toplanma alanına git |
| **❌ Yanlış davranışların** | Oyuncunun bu oynanışta seçtiği yanlış seçeneklerin "yanlış davranış" maddeleri (§6.2). Yanlış yoksa bu kutu gizlenir. | Aynı şekilde (§7.2) |
| **Alt not** | *Bu bir farkındalık oyunudur. Gerçek bir afette resmî kurumların talimatlarına uy.* | aynı |
| **Butonlar** | **Sel Bölümünü Tekrar Dene** (`GameSceneRouter.RestartCurrentChapter`, yalnız Sel) · **Menüye Dön** (`GameSceneRouter.ReturnToMenu`) | **Devam: Sel Bölümü** (`GameSceneRouter.ContinueToNextChapter`) · **Tekrar Dene** · **Menüye Dön** |

Yerleşim, Görsel 2'deki gibi iki kutudur: solda yeşil "Öğrenilenler", sağda kırmızı "Yanlış Davranışların".

---

## 9. ID sözleşmesi (kod bu ID'lere bakar, metinlere değil)

| Tür | Dosya (sahibi) | ID'ler |
|---|---|---|
| Diyalog sahneleri — Sel | `Assets/Data/Story/flood.json` (Arda) | `F01_WAKE`, `F02_WASH`, `F03_MOTHER_CHAT`, `F03_TV_JINGLE`, `F03_NEWS`, `F04_MOTHER`, `F04_CHECK`, `F04_PREPARE`, `F05_EXIT`, `F05_EXIT_GEAR`, `F05_STREET`, `F05_PUDDLE_WARNING`, `F05_DRAIN_WARNING`, `F05_POSTER`, `F05_DOWNPOUR`, `F05_BUS_ARRIVE`, `F05_BOARD`, `F06_RUMORS`, `F06_DECIDED`, `F06_ARRIVE`, `F07_ARRIVE`, `F07_RISK`, `F07_RISK_WARNING`, `F07_WATER_WARNING`, `F08_BRIDGE`, `F08_SAFEROUTE` |
| Kararlar — Sel | aynı dosya | `F_D1` (`F_D1_A..C`), `F_D2` (`F_D2_A..D`) |
| Diyalog sahneleri — Deprem | `Assets/Data/Story/earthquake.json` (Arda) | `E01_WAKE`, `E02_ROUTINE`, `E03_NEWS`, `E04_QUAKE`, `E06_AFTER`, `E07_EXIT`, `E08_ASSEMBLY` |
| Kararlar — Deprem | aynı dosya | `E_D1` (`E_D1_1..5`), `E_D2` (`E_D2_A..D`), `E_D3` (`E_D3_A..B`) |
| Haberler ve kartlar | `Assets/Data/News/*.json` (Eren, biçimi Eren belirler) | `NEWS_F03_TV`, `NEWS_F04_OFFICIAL`, `NEWS_F06_RUMORS`, `NEWS_F06_OFFICIAL_ROAD`, `NEWS_E03_TV` |
| Bölüm adımları (kod) | `FloodChapterController`, `EarthquakeChapterController` (Mahmut) | Sel: `F01_WAKE` … `F09_RESULT` · Deprem: `E01_WAKE` … `E09_RESULT` |

**Kodda kim kimi çağırır:**

```text
Menü (Berk) ──LoadChapter──▶ GameSceneRouter (Mahmut) ──▶ FloodChapter / EarthquakeChapter sahnesi
Bölüm akışı (Mahmut, ChapterControllerBase)
   ├─ Say("F04_MOTHER")       ──▶ DialogueRunner (Arda)  ──▶ DialoguePanel (Berk)
   ├─ Ask("F_D2", sonuç)      ──▶ DecisionRunner (Arda)  ──▶ DecisionPanel (Berk)
   ├─ ShowNews("NEWS_F03_TV") ──▶ NewsBroadcast (Eren)
   ├─ WaitForInteract("Kalk") ──▶ InteractionHint (Berk)
   └─ ShowResult()            ──▶ ResultPanel (Berk) ──Tekrar/Menü──▶ GameSceneRouter
```

Sözleşmeler `Assets/Scripts/Shared/Contracts/` altındaki soyut sınıflardır (`DialogueRunnerBase`, `DecisionRunnerBase`, `NewsPlayerBase`, `DialogueViewBase`, `ChoiceViewBase`, `ResultViewBase`, `InteractionHintBase`, `ScreenFaderBase`). Herkes kendi sınıfını bunlardan türetir. Bir parça henüz yoksa bölüm akışı onu atlar ve Console'a yazar, böylece herkes diğerlerini beklemeden test edebilir.

---

## 10. Kaynaklar arası çelişkiler ve verilen kararlar

| # | Konu | Kaynaklar ne diyor | Karar | Neden |
|---|---|---|---|---|
| 1 | Deprem nerede olur? | Görsel 2: **okulda**, ders sırasında · Görsel 1 ve paket: **evde** | **Evde, kahvaltıdan sonra mutfakta** | Oda, banyo ve mutfak/TV sahneleri Sel ile ortak kalır, 48 saatte büyük zaman kazancı sağlar. Çocuk ve anne sprite'ları hazır; sınıf, sınıf arkadaşları ve öğretmen çizimi gerekmiyor. Okul versiyonu jam sonrasına bırakıldı. |
| 2 | Deprem kahramanı | Paket: farklı bir yetişkin · Görseller: aynı çocuk | **Aynı çocuk** | Görsellere uygun ve hazır sprite kullanılıyor. |
| 3 | Anne nasıl biri? | Paket: uyarıyı küçümseyen anne · Görseller: sorumlu anne | **Sorumlu anne** | İki görsel de böyle. "Uyarıyı küçümseme" teması F_D1'in yanlış seçeneklerinde ve arkadaşların söylentilerinde işleniyor. |
| 4 | Deprem anı mekaniği | Paket: 5 seçenek / 5 sn · Görsel 2: S-A-D ile Çök-Kapan-Tutun · Görsel 1: E'li süre çubuğu | **Önce 5 sn'de 5 seçenek (P0), sonra S-A-D ile Çök-Kapan-Tutun (P1)** | İkisi birlikte hem karar vermeyi hem hareketin sırasını öğretiyor. Zaman yetmezse doğru seçim doğrudan güvenli pozu oynatır. |
| 5 | "Doğalgazı kontrol et" doğru mu? | Görsel 1'de vurgulu doğru seçenek "Acil durum çantası al"; "yanan ocakları kontrol etmemek" ise yanlış davranış listesinde | **İkisi de doğru, ikisi de bulunmalı (E_D2)** | Sarsıntı sonrası resmî rehberler hem çantayı almayı hem ocak ve gazı kontrol etmeyi önerir. |
| 6 | Deprem TV başlığı | Görsel 2: "UZMANLARDAN DEPREM UYARISI" | **"BÖLGEDE HAFİF SARSINTILAR HİSSEDİLDİ"** + hazırlık hatırlatması | Depremler tahmin edilemez, oyun bunu ima etmemeli. |
| 7 | Yanlış seçimden sonra ne olur? | Paket: hikâye devam eder | **Geri bildirim + tekrar seç** | Her bölüm güvenli sonla biter. Yanlışlar kaybolmaz, sonuç ekranında listelenir. Akış dallanmadığı için yapımı da kolay. |
| 8 | Sel'in sonu | Görsel 2 "Yanlış Bilgi"de (160–200 sn) bitiyor, paneli 6 yerine "8" numaralı · Görsel 1'de Riskli Bölge + Güvenli Rota var | **Görsel 1'in devamı eklendi** (F07, F08) | Görsel 2'deki numara yazım hatası olarak kabul edildi. |
| 9 | Olay seçimi ne zaman? | Görsel 2 akışında 4. adım (rutinden sonra) | **Menüdeki bulutla** | Kullanıcı isteği: "Sel'e/Deprem'e tıklayınca başlayan oyun". Açılış iki bölümde ortak, hikâye haber sahnesinde ayrışıyor. |
| 10 | Telefon "erken uyarı" bildirimi | Paket: kurgu içi uyarı (P0) | **Kullanılmıyor** | Görsellerde yok ve uyarının hep geleceği izlenimini verebilir. Eren'in NEWS-02 görevi Deprem TV haberine çevrildi. |
| 11 | Sel bölümünün içeriği ve sırası (17 Eki) | AGENTS P0 #3: uyanma + el-yüz yıkama + TV hava uyarısı + bilinçsiz anne diyaloğu + okul yolu + söylenti + güvenli karar · Lider master prompt'u: depremden sonra akşam mahalle seli | **Akşam mahalle seli (§6.0) eski sabah akışının yerine geçer; sıra DEPREM → SEL** | Lider kararı. Eski akışın zorunlu maddelerinden **uyanma, el-yüz yıkama, TV haberi, anne diyaloğu (F_D1), otobüs ve F_D2 kararı yeni akışta yok**; söylenti/kaynak kontrolü, kapalı yol, güvenli karar ve öğrenme ekranı korunuyor. AGENTS.md §1 P0 #3 ve §8'deki "MainMenu → Flood → Result → MainMenu → Earthquake → Result" test sırası bu karara göre güncellenmeli (sahibi: Mahmut). Sel telefon uyarısı kurgu içi sel uyarısıdır, deprem erken uyarısı değildir (#10 geçerli). |

---

## 11. Öncelik ve kesme listesi

| Öncelik | Ne | Zaman yetmezse |
|---|---|---|
| **P0** (asla kesilmez) | Menü → iki bölüm → sonuç → menü döngüsü · F_D1, F_D2, E_D1 (5 sn), E_D2, E_D3 kararları ve geri bildirimleri · sonuç ekranı | — |
| P1 | Çök-Kapan-Tutun (S-A-D) mini oyunu · önemli eşyaların HUD ikonları · F07 tabela inceleme · seçenek sırası karıştırma · yürüyüş animasyonları | Doğru seçim doğrudan güvenli pozu oynatır, eşyalar sadece replikle alınır, yürüme yerine fade geçişi kullanılır |
| P2 | Kamera sarsıntısı ayarı (kapatılabilir) · ses efektleri · yağmur overlay animasyonu · komşu ve görevli sprite'ları | Sessiz ya da statik |
| Kapsam dışı | Okulda deprem versiyonu · Orman Yangını ve Heyelan bölümleri · kayıt sistemi · puan tablosu | Jam sonrası |

---

## 12. Gereken görseller (bölüme göre)

| Görsel | Kullanıldığı yer | Durum |
|---|---|---|
| Çocuk odası (07:00 saatli) | Menü, F01/E01 | ✅ `visuals/backgrounds/BG_Bedroom_Sunrise.png` (yatak çakışmasına dikkat, ART_BIBLE) |
| Banyo | F02/E02 | 🟡 Sadece konsept (`visuals/previews/PREVIEW_Bathroom_Concept.png`) |
| Mutfak + TV + sağlam masa | F03/F04, E02–E04 | 🟡 Konsept var, oyun görseli yok |
| Hasarlı mutfak | E06 | ❌ Yok (mutfak üstüne dağınık eşya katmanı yeterli) |
| Yağmurlu sokak + durak | F05/F06 | 🟡 Konsept (`PREVIEW_RainyStreet_Concept.png`) |
| Riskli bölge (bariyer, tabela, araç) | F07 | ✅ `Assets/Art/Props/SchoolRoute/` (su basmış Okul Caddesi, bariyer, tabela, suda araç) |
| Köprü üstü yaya yolu + okul kapısı | F08 | ✅ `Assets/Art/Props/SchoolRoute/` (köprü, okul cephesi, kapı, bayrak) |
| Bina koridoru, merdiven, asansör, ACİL ÇIKIŞ | E07 | ❌ Yok |
| Toplanma alanı (park + tabela) | E08 | ❌ Yok |
| Çocuk (8 yön yürüme) | Her yer | ✅ `visuals/characters/spritesheets/arda_*` |
| Çocuk: Çök-Kapan-Tutun pozu | E05 | ❌ Yok |
| Anne (8 yön yürüme + portreler) | F04, E03, E08 | ✅ `spritesheets/anne_*`, `sprites/CHR_Mother_*` |
| 3 arkadaş, öğretmen, komşu, görevli | F06, F08, E08 | 🟡 3 arkadaş + öğretmen: `Assets/Art/Characters/Friends/` (renk değişimi, duruş karesi); komşu/görevli yok |
| TV haber ekranı, kartlar | F03, F04, F06, E03 | ❌ Eren üretecek |
| Paneller, sonuç ekranı, ikonlar, font | Her yer | ❌ Berk üretecek |

Sesler: alarm saati, yağmur döngüsü, bildirim/haber jingle'ı, deprem gürlemesi, UI tıklama. Sadece lisansı net (CC0 veya kendi üretimimiz) sesler kullanılır.
