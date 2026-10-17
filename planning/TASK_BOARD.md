# Görev Panosu — 48 Saat / Tek Sorumlu

**Toplam 42 görev.** Sahipler `planning/EKIP_ROLLERI.md` dağılımına göredir (Mahmut / Berk / Arda / Eren). Görev başına tek `Owner`. `Status`: TODO, IN_PROGRESS, BLOCKED, IN_REVIEW, DONE. Durumlar (TODO/IN_REVIEW/DONE) `TASK_BOARD.csv` içinde. Akış ve ID'ler: `docs/OYUN_AKISI.md`.

| ID | Owner | Pri | Hedef | İş | Teslim testi |
|---|---|---|---|---|---|
| SETUP-01 | Mahmut | P0 | T+04 | Unity proje ve Git ayarları | Unity açılıyor; sahne ve meta ayarları doğru |
| SETUP-02 | Mahmut | P0 | T+04 | Kickoff, rol isimleri ve task board | 4 kişi owner atanmış, kanal ve teslim saati kayıtlı |
| MENU-01 | Berk | P0 | T+08 | MainMenu sahnesi + gerçek UI | Yeni Oyun/Ayarlar/Çıkış çalışıyor |
| MENU-02 | Berk | P0 | T+08 | Menu temel görselleri import | 8 temel PNG ayrı ve import ayarı doğru |
| MENU-03 | Berk | P0 | T+10 | 4 afet bulutu: 2 açık, 2 kilitli | Sel ve Deprem yüklenir; diğerleri Yakında |
| MENU-04 | Berk | P0 | T+14 | 5 sn menü intro ve skip | 07:00→oda→bulutlar→başlık; Space skip |
| CORE-01 | Mahmut | P0 | T+06 | GameSceneRouter kontratı | Berk (menü) ve bölüm sahneleri scene isimlerini bilir, menüye döner |
| BUILD-01 | Mahmut | P0 | T+10 | İlk Windows build | EXE açılıyor; hata açıkça raporlu |
| FLOOD-01 | Mahmut | P0 | T+10 | Sel sahnesi ve state skeleton | Sel sahnesi tek başına açılıyor |
| FLOOD-02 | Mahmut | P0 | T+12 | Çocuk hareket ve E etkileşimi | E ile uyanış/kapı aksiyonu işliyor |
| STORY-01 | Arda | P0 | T+12 | Sel TV/anne diyalogları | Türkçe ve eğitsel doğruluk kontrolü |
| STORY-02 | Arda | P0 | T+12 | Deprem 5 seçenek + feedback | 1 doğru, 2-5 yanlış, timeout açıklaması |
| FLOOD-03 | Mahmut | P0 | T+18 | TV + anne + karar 1 | Karar 1 tüm seçenekleri feedback veriyor |
| QUAKE-01 | Mahmut | P0 | T+12 | Deprem salon + masa görseli | Masa/pencere ve adult silhouette görünüyor |
| QUAKE-02 | Mahmut | P0 | T+18 | 5 saniye sayaç, 5 düğme | 1–5 ve mouse, timeout çalışıyor |
| ART-01 | Mahmut | P0 | T+18 | Sel için yağmurlu sokak minimum art | Tek arka plan/durak yağmur okunuşlu |
| STORY-03 | Arda | P0 | T+18 | Sel söylentiler + karar 2 | 3 arkadaş iddiası ve güvenli feedback hazır |
| FLOOD-04 | Mahmut | P0 | T+23 | Yağmur, kapalı yol, söylenti | Oyuncu tehlikeli sudan uzak bekliyor |
| FLOOD-05 | Mahmut | P0 | T+28 | Sel karar 2 + sonuç + reset | Sel tam loop ve menüye dönüş |
| QUAKE-03 | Mahmut | P0 | T+23 | Sarsıntı efekti / güvenli poz | Doğru güvenli davranış görünür, flaş yok |
| QUAKE-04 | Mahmut | P0 | T+28 | Deprem sonucu + retry + menü | Deprem tam loop, sayaç reset |
| CONTENT-01 | Arda | P0 | T+24 | Afet güvenlik doğruluk incelemesi | Sel/deprem yanlış güvenli mesaj yok |
| INTEG-01 | Mahmut | P0 | T+28 | Sel ve Deprem entegrasyonu | Menü iki bölüme doğru gider ve döner |
| MENU-05 | Berk | P0 | T+30 | Ayarlar ses/fullscreen ve kilit tooltip | İki buton işlevsel, kilit çalışıyor |
| ART-02 | Mahmut | P1 | T+34 | Menü/yağmur/sarsıntı minimum polish | Yeni sisteme gerek kalmadan görünüm düzeldi |
| AUDIO-01 | Mahmut | P1 | T+32 | Lisans uygun 3-4 ses / ses seviyesi | Menü/rain/quake kullanılabilir veya sessiz fallback |
| QA-01 | Mahmut | P0 | T+32 | İki bölümün tam test turu | E01-E06 ve F01-F08 kaydı |
| FIX-01 | Mahmut | P0 | T+36 | Sel bug fixes | Sel kritik bug 0 |
| FIX-02 | Mahmut | P0 | T+36 | Deprem bug fixes | Deprem kritik bug 0 |
| FREEZE-01 | Mahmut | P0 | T+36 | Feature freeze ve RC0 build | Yeni feature yok; çalışan build işaretli |
| QA-02 | Mahmut | P0 | T+44 | Regresyon ve test raporu | Menü→Sel→Deprem smoke test sonucu kayıtlı |
| BUILD-02 | Mahmut | P0 | T+44 | Release candidate Windows build | Ayrı Windows PC tercih, hash kaydı |
| DEMO-01 | Mahmut | P0 | T+46 | 3 dakikalık jüri provası | İki bölüm ve güvenlik mesajı 3 dk içinde |
| RELEASE-01 | Mahmut | P0 | T+48 | Final build ZIP ve teslim | Geri alınabilir başarılı build teslim edilmiş |
| UI-01 | Berk | P0 | T+12 | Ortak UI prefabları (DialoguePanel, DecisionPanel 3/5, ResultPanel, InteractionHint, FadePanel) | Prefablar görünüm olarak hazır; Arda/Eren/Mahmut kullanabiliyor |
| UI-02 | Berk | P0 | T+08 | Türkçe karakterli pixel font | Ç Ğ İ ı Ö Ş Ü doğru görünüyor; lisans net |
| DIALOG-01 | Arda | P0 | T+12 | Diyalog sistemi + JSON yükleyici | JSON satırları sırayla, konuşan adıyla; E/Space/mouse ile ilerliyor; bitince event |
| DIALOG-02 | Arda | P0 | T+16 | Seçenekli karar + feedback | 3 ve 5 seçenek; seçim feedback gösterip correct bilgisiyle event atıyor |
| NEWS-01 | Eren | P0 | T+14 | Sel TV haberi (`NEWS_F03_TV`) | Haber bandı + başlık + alt yazı + harita/renk lejantı (kurgu); bitince döner; sahte kurum logosu yok |
| NEWS-02 | Eren | P0 | T+16 | Deprem TV haberi (`NEWS_E03_TV`) | "Hafif sarsıntılar hissedildi" + hazırlık hatırlatması; deprem tahmini/erken uyarı iddiası yok |
| NEWS-03 | Eren | P0 | T+20 | Duyuru ve söylenti kartları (`NEWS_F04_OFFICIAL`, `NEWS_F06_RUMORS`, `NEWS_F06_OFFICIAL_ROAD`) | 3 söylenti kartı "Kaynak: bilinmiyor"; 2 resmî duyuru kartı (kurgu kaynak) |
| NEWS-04 | Eren | P1 | T+24 | Haber metinleri doğruluk kontrolü | Renk kodu evrensel diye anlatılmıyor; gerçek kurum adı/logosu yok |

**CSV:** `planning/TASK_BOARD.csv` doğrudan import edilebilir. Import esnasında sahibi yalnız rol kodu olarak görünür; GitHub kişisine eşleyin.
