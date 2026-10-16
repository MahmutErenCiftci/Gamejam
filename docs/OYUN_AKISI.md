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
| **Tür** | 2D yandan görünüşlü (side view), kısa, seçimli hikâye oyunu — pixel art, Türkçe |
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

- **Olay seçimi menüdedir:** oyuncu bulutu seçer, bölüm o anda başlar. Görsel 1'deki "Olay Seçimi" adımı budur.
- **Uyanış ve Sabah Rutini iki bölümde ortaktır.** Aynı oda, banyo ve mutfak sahneleri kullanılır, bu da çizim ve iş yükünü yarıya indirir. Hikâye mutfaktaki **TV haberi** ile ayrışır: Sel'de yağmur uyarısı, Deprem'de sarsıntı haberi.
- Kilitli bulutlar (Orman Yangını, Heyelan) hiçbir sahneye gitmez, sadece "Yakında" yazar.

---

## 3. Mekanikler ve tuşlar

| Mekanik | Tuş | Nerede | Not |
|---|---|---|---|
| Hareket | **A / D** veya **← / →** | Ev içi, sokak | Yandan görünüş: sadece sağ-sol |
| Etkileşim | **E** | Kalk, yüzünü yıka, eşya al, tabela incele | Ekranda ipucu: `[E] Kalk` |
| Diyaloğu ilerlet | **E**, **Space**, **Enter** veya **sol tık** | Tüm konuşmalar | |
| Seçim yap | **1–5** veya **fare** | Tüm kararlar | Seçenek numaraları ekranda yazar |
| Zamanlı seçim | 1–5 / fare | Deprem anı (E_D1) | **5 saniye**, sayaç çubuğu; seçenekler sayaç başlamadan görünür |
| Çök – Kapan – Tutun | **S** (Çök) → **A** (Kapan) → **D** basılı (Tutun) | Deprem (E05) | Hareket o sırada kapalı; D sarsıntı bitene kadar basılı tutulur |
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

---

## 6. SEL bölümü — "Bilinçli ol, güvende kal"

### 6.1 Zaman çizelgesi

| # | ID | Süre | Mekân | Olay | Oyuncu ne yapar | Kim yapar |
|---|---|---|---|---|---|---|
| 1–2 | `F01_WAKE`, `F02_WASH` | 0:00–0:50 | Oda, banyo | Ortak açılış (§5) | E ile kalk, yüzünü yıka | Mahmut |
| 3 | `F03_NEWS` | 0:50–1:20 | Mutfak (TV) | TV: **SON DAKİKA — BÖLGEDE ŞİDDETLİ YAĞMUR BEKLENİYOR**, renk lejantı, alt yazı. Çocuk: *"Şiddetli yağmur mu? Okul yolumuzda hep su birikiyor..."* | Haberi izler, diyaloğu geçer | Eren (TV) · Arda (replik) |
| 4 | `F04_MOTHER` | 1:20–1:50 | Mutfak | Çocuk anneye uyarıyı söyler → **Karar F_D1**. Anne: *"Belediyeyi kontrol edelim. Resmî duyuru gelmeden karar verme."* → **resmî duyuru kartı** (okullar açık, su birikintilerinden uzak durun) → Anne: *"Şemsiye ve yağmurluk al. Gereksiz yere dışarı çıkma lütfen."* | Karar verir, **[E] yağmurluğu al**, **[E] şemsiyeyi al**, kapıdan çıkar | Arda (diyalog+karar) · Eren (kart) · Mahmut |
| 5 | `F05_STREET` | 1:50–2:30 | Yağmurlu sokak, durak | Hafif yağmur 5 sn içinde şiddetlenir, sokakta su birikintileri, durakta "DİKKATLİ OLUN" afişi. *"Yağmur iyice arttı..."* | Sağa yürür | Mahmut |
| 6 | `F06_RUMORS` | 2:30–3:10 | Yol kenarı | Üç arkadaş farklı şeyler söyler (söylentiler). Ekranda **söylenti kartları** ("Kaynak: bilinmiyor") → **Karar F_D2** | Söylentileri okur, karar verir | Arda (diyalog+karar) · Eren (kartlar) |
| 7 | `F07_RISK` | 3:10–3:40 | Riskli bölge | Bariyer, **"SU BİRİKİNTİSİ TEHLİKELİDİR"** tabelası, suda mahsur kalmış bir araç. Doğru seçimden sonra **uzaktan, yolun kenarından** görülür. | **[E] Tabelayı incele** | Mahmut |
| 8 | `F08_SAFEROUTE` | 3:40–4:10 | Köprü üstü yaya yolu → okul kapısı | Alternatif güvenli rotadan okula varış. Öğretmen: *"Güvenli yoldan gelmene çok sevindim."* | Yürür | Mahmut |
| 9 | `F09_RESULT` | 4:10–4:30 | Sonuç ekranı | Öğrenilenler + yapılan yanlışlar (§8) | Tekrar Dene / Menüye Dön | Berk (ekran) · Mahmut |

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

---

## 8. Sonuç ekranı (Berk ekranı yapar, içerik veriden gelir)

| | Sel | Deprem |
|---|---|---|
| **Başlık (hiç yanlış yoksa)** | Tebrikler! Bilinçli davrandın. | Tebrikler! Doğru davrandın. |
| **Başlık (yanlış varsa)** | Bölümü tamamladın! Yanlışlarından ders çıkardın. | Bölümü tamamladın! Yanlışlarından ders çıkardın. |
| **✅ Öğrenilenler** | Sel uyarılarını ciddiye al · Sosyal medyadaki bilgileri sorgula · Su birikintilerinden uzak dur · Kapanan yolları kullanma · Güvenli alternatif rota seç · Resmî kurumların duyurularını takip et | Sarsıntı anında ÇÖK – KAPAN – TUTUN · Sarsıntı bitince sakin kal · Asansör kullanma · Acil durum çantanı hazır tut · Resmî açıklamaları takip et · Toplanma alanına git |
| **❌ Yanlış davranışların** | Oyuncunun bu oynanışta seçtiği yanlış seçeneklerin "yanlış davranış" maddeleri (§6.2). Yanlış yoksa bu kutu gizlenir. | Aynı şekilde (§7.2) |
| **Alt not** | *Bu bir farkındalık oyunudur. Gerçek bir afette resmî kurumların talimatlarına uy.* | aynı |
| **Butonlar** | **Tekrar Dene** (`GameSceneRouter.RestartCurrentChapter`) · **Menüye Dön** (`GameSceneRouter.ReturnToMenu`) | aynı |

Yerleşim, Görsel 2'deki gibi iki kutudur: solda yeşil "Öğrenilenler", sağda kırmızı "Yanlış Davranışların".

---

## 9. ID sözleşmesi (kod bu ID'lere bakar, metinlere değil)

| Tür | Dosya (sahibi) | ID'ler |
|---|---|---|
| Diyalog sahneleri — Sel | `Assets/Data/Story/flood.json` (Arda) | `F01_WAKE`, `F02_WASH`, `F03_NEWS`, `F04_MOTHER`, `F04_CHECK`, `F04_PREPARE`, `F05_STREET`, `F06_RUMORS`, `F07_RISK`, `F07_RISK_WARNING`, `F08_SAFEROUTE` |
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
| Riskli bölge (bariyer, tabela, araç) | F07 | 🟡 Konsept (Görsel 1, panel 5) |
| Köprü üstü yaya yolu + okul kapısı | F08 | ❌ Yok |
| Bina koridoru, merdiven, asansör, ACİL ÇIKIŞ | E07 | ❌ Yok |
| Toplanma alanı (park + tabela) | E08 | ❌ Yok |
| Çocuk (8 yön yürüme) | Her yer | ✅ `visuals/characters/spritesheets/arda_*` |
| Çocuk: Çök-Kapan-Tutun pozu | E05 | ❌ Yok |
| Anne (8 yön yürüme + portreler) | F04, E03, E08 | ✅ `spritesheets/anne_*`, `sprites/CHR_Mother_*` |
| 3 arkadaş, öğretmen, komşu, görevli | F06, F08, E08 | ❌ Yok (P2: siluet ya da renk değişimi) |
| TV haber ekranı, kartlar | F03, F04, F06, E03 | ❌ Eren üretecek |
| Paneller, sonuç ekranı, ikonlar, font | Her yer | ❌ Berk üretecek |

Sesler: alarm saati, yağmur döngüsü, bildirim/haber jingle'ı, deprem gürlemesi, UI tıklama. Sadece lisansı net (CC0 veya kendi üretimimiz) sesler kullanılır.
