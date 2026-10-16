# Görev Panosu — 48 Saat / Tek Sorumlu

**Toplam 34 görev.** Görev başına tek `Owner`. `Status`: TODO, IN_PROGRESS, BLOCKED, IN_REVIEW, DONE. Ayrıntı ve kontratlar AGENTS.md ile planning/OWNERS_AND_HANDOFFS.md içinde.

| ID | Owner | Pri | Hedef | İş | Teslim testi |
|---|---|---|---|---|---|
| SETUP-01 | K1 | P0 | T+04 | Unity proje ve Git ayarları | Unity açılıyor; sahne ve meta ayarları doğru |
| SETUP-02 | K4 | P0 | T+04 | Kickoff, rol isimleri ve task board | 4 kişi owner atanmış, kanal ve teslim saati kayıtlı |
| MENU-01 | K1 | P0 | T+08 | MainMenu sahnesi + gerçek UI | Yeni Oyun/Ayarlar/Çıkış çalışıyor |
| MENU-02 | K3 | P0 | T+08 | Menu temel görselleri import | 8 temel PNG ayrı ve import ayarı doğru |
| MENU-03 | K1 | P0 | T+10 | 4 afet bulutu: 2 açık, 2 kilitli | Sel ve Deprem yüklenir; diğerleri Yakında |
| MENU-04 | K1 | P0 | T+14 | 5 sn menü intro ve skip | 07:00→oda→bulutlar→başlık; Space skip |
| CORE-01 | K1 | P0 | T+06 | GameSceneRouter kontratı | K2/K3 scene isimlerini bilir, menüye döner |
| BUILD-01 | K1 | P0 | T+10 | İlk Windows build | EXE açılıyor; hata açıkça raporlu |
| FLOOD-01 | K2 | P0 | T+10 | Sel sahnesi ve state skeleton | Sel sahnesi tek başına açılıyor |
| FLOOD-02 | K2 | P0 | T+12 | Çocuk hareket ve E etkileşimi | E ile uyanış/kapı aksiyonu işliyor |
| STORY-01 | K4 | P0 | T+12 | Sel TV/anne diyalogları | Türkçe ve eğitsel doğruluk kontrolü |
| STORY-02 | K4 | P0 | T+12 | Deprem 5 seçenek + feedback | 1 doğru, 2-5 yanlış, timeout açıklaması |
| FLOOD-03 | K2 | P0 | T+18 | TV + anne + karar 1 | Karar 1 tüm seçenekleri feedback veriyor |
| QUAKE-01 | K3 | P0 | T+12 | Deprem salon + masa görseli | Masa/pencere ve adult silhouette görünüyor |
| QUAKE-02 | K3 | P0 | T+18 | 5 saniye sayaç, 5 düğme | 1–5 ve mouse, timeout çalışıyor |
| ART-01 | K3 | P0 | T+18 | Sel için yağmurlu sokak minimum art | Tek arka plan/durak yağmur okunuşlu |
| STORY-03 | K4 | P0 | T+18 | Sel söylentiler + karar 2 | 3 arkadaş iddiası ve güvenli feedback hazır |
| FLOOD-04 | K2 | P0 | T+23 | Yağmur, kapalı yol, söylenti | Oyuncu tehlikeli sudan uzak bekliyor |
| FLOOD-05 | K2 | P0 | T+28 | Sel karar 2 + sonuç + reset | Sel tam loop ve menüye dönüş |
| QUAKE-03 | K3 | P0 | T+23 | Sarsıntı efekti / güvenli poz | Doğru güvenli davranış görünür, flaş yok |
| QUAKE-04 | K3 | P0 | T+28 | Deprem sonucu + retry + menü | Deprem tam loop, sayaç reset |
| CONTENT-01 | K4 | P0 | T+24 | Afet güvenlik doğruluk incelemesi | Sel/deprem yanlış güvenli mesaj yok |
| INTEG-01 | K1 | P0 | T+28 | Sel ve Deprem entegrasyonu | Menü iki bölüme doğru gider ve döner |
| MENU-05 | K1 | P0 | T+30 | Ayarlar ses/fullscreen ve kilit tooltip | İki buton işlevsel, kilit çalışıyor |
| ART-02 | K3 | P1 | T+34 | Menü/yağmur/sarsıntı minimum polish | Yeni sisteme gerek kalmadan görünüm düzeldi |
| AUDIO-01 | K4 | P1 | T+32 | Lisans uygun 3-4 ses / ses seviyesi | Menü/rain/quake kullanılabilir veya sessiz fallback |
| QA-01 | K4 | P0 | T+32 | İki bölümün tam test turu | E01-E06 ve F01-F08 kaydı |
| FIX-01 | K2 | P0 | T+36 | Sel bug fixes | Sel kritik bug 0 |
| FIX-02 | K3 | P0 | T+36 | Deprem bug fixes | Deprem kritik bug 0 |
| FREEZE-01 | K1 | P0 | T+36 | Feature freeze ve RC0 build | Yeni feature yok; çalışan build işaretli |
| QA-02 | K4 | P0 | T+44 | Regresyon ve test raporu | Menü→Sel→Deprem smoke test sonucu kayıtlı |
| BUILD-02 | K1 | P0 | T+44 | Release candidate Windows build | Ayrı Windows PC tercih, hash kaydı |
| DEMO-01 | K4 | P0 | T+46 | 3 dakikalık jüri provası | İki bölüm ve güvenlik mesajı 3 dk içinde |
| RELEASE-01 | K1 | P0 | T+48 | Final build ZIP ve teslim | Geri alınabilir başarılı build teslim edilmiş |

**CSV:** `planning/TASK_BOARD.csv` doğrudan import edilebilir. Import esnasında sahibi yalnız rol kodu olarak görünür; GitHub kişisine eşleyin.
