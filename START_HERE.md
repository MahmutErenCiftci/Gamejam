# AFET UYANIŞI — 48 Saatlik Game Jam Çalışma Paketi

> **SÜRÜM 2.0 · 4 kişi · Unity 2D/C# · ZORUNLU İKİ OYNANABİLİR BÖLÜM: SEL + DEPREM**  
> Bu ZIP **oyunun hazır Unity projesi değildir**. Uygulama planı, özgün görsel referanslar/PNG'ler, görev panosu, içerik örnekleri ve ekip/AI-agent kurallarıdır. Klasörleri mevcut Unity projenizle kontrollü birleştirin.

## 60 saniyede ne yapacağız?

**Amaç:** 48 saat sonunda 2–5 dakikalık Sel hikâyesi + 1–2 dakikalık Deprem karar sahnesi; çalışan ana menü, 4 afet rüya balonu, kapanış/eğitim ekranları ve doğrulanmış Windows build.

**Ana menü:** Çocuk uyuyor; dijital saat 07:00; dört rüya balonu: Deprem, Sel, Orman Yangını, Heyelan. **Sel ve Deprem oynanabilir.** Orman Yangını ile Heyelan **kilitli / “Yakında”**.

**Tek doğruluk kaynağı:** [`AGENTS.md`](AGENTS.md). İnsan ekip üyeleri ve tüm kod agent'ları bu dosyayı okumadan değişiklik yapamaz. Claude Code için [`CLAUDE.md`](CLAUDE.md) bunu açıkça işaret eder.

## İlk 15 dakikada yapılacaklar

1. Bir kişi PM seçilsin; dört kişi kendine **K1, K2, K3, K4** rolü atasın. İsimleri [`planning/OWNERS_AND_HANDOFFS.md`](planning/OWNERS_AND_HANDOFFS.md) dosyasındaki tabloya yazın.
2. ~~Belgeleri Unity GitHub reposunun köküne taşıyın~~ — **yapıldı**: paket repo kökünde. `visuals/` yalnız referanstır; oyunda kullanılacak PNG'leri K3 `Assets/Art/` altına import eder (MENU-02).
3. Repo Unity sürümünü ve render/input ayarlarını sabitleyin. `Assets/`, `Packages/`, `ProjectSettings/` klasörlerini sıfırdan silip değiştirmeyin.
4. `planning/TASK_BOARD.csv` dosyasını GitHub Issues/Projects'e aktarın; görevlerin **tek bir sahibi** olsun.
5. Sadece **K1** ilk sahne ve genel ayarlardan sorumlu. K2, Sel sahnesinde; K3, Deprem sahnesi ve görsel dosyalarda; K4, metin/veri/testte çalışır.
6. T+4 saat sonunda Unity'de açılan bir başlangıç projesi; T+10'da ilk Windows build hedefleyin.

## Hangi belgeyi kim okumalı?

| Belge | İçerik | Zorunlu okuyan |
|---|---|---|
| [`AGENTS.md`](AGENTS.md) | Bağlayıcı iş bölümü, izinler, kapsam, teslim koşulları | **Herkes ve tüm agent'lar** |
| [`docs/GAME_DESIGN_DOCUMENT.md`](docs/GAME_DESIGN_DOCUMENT.md) | Oyun vizyonu, ekranlar, döngü ve mekanikler | Herkes |
| [`planning/48H_TIMELINE.md`](planning/48H_TIMELINE.md) | 48 saatlik takvim, kapılar, kesme planı | Herkes |
| [`planning/OWNERS_AND_HANDOFFS.md`](planning/OWNERS_AND_HANDOFFS.md) | K1–K4, dosya sahipliği ve teslim protokolü | Herkes |
| [`story/FLOOD_CHAPTER.md`](story/FLOOD_CHAPTER.md) | Sel için sahne/diyalog/karar akışı | K2, K4, K3 |
| [`story/EARTHQUAKE_CHAPTER.md`](story/EARTHQUAKE_CHAPTER.md) | Deprem için beş seçenekli refleks sahnesi | K3, K4, K1 |
| [`technical/UNITY_PLAN.md`](technical/UNITY_PLAN.md) | Unity sahneleri, prefab, input, çözünürlük, kod sorumluluğu | K1, K2, K3 |
| [`technical/INTERFACE_CONTRACTS.md`](technical/INTERFACE_CONTRACTS.md) | Bileşenler birbirine nasıl bağlanacak? | K1, K2, K3, K4 |
| [`technical/GIT_AND_MERGE.md`](technical/GIT_AND_MERGE.md) | Git, conflict önleme, PR sırası | Herkes |
| [`docs/ART_BIBLE.md`](docs/ART_BIBLE.md) | Görsel stil, karakter, mekan, mevcut PNG'nin durumu | K3, K1, K2 |
| [`visuals/ASSET_MANIFEST.md`](visuals/ASSET_MANIFEST.md) | Gerçek görsellerin konumları ve üretime hazır olma notları | K3, K1 |
| [`qa/TEST_PLAN.md`](qa/TEST_PLAN.md) | Sel/Deprem test ve DoD, güvenlik, Windows build | K4, K1 |
| [`planning/RISK_AND_CUT_LIST.md`](planning/RISK_AND_CUT_LIST.md) | Zaman kaybında neyi kesiyoruz? | Herkes |
| [`agents/AGENT_PLAYBOOK.md`](agents/AGENT_PLAYBOOK.md) | Agent görev verme ve raporlama sözleşmesi | Tüm agent kullananlar |
| [`planning/TASK_BOARD.csv`](planning/TASK_BOARD.csv) | İş listesi: ID, sahip, saat ve kabul şartları | Herkes |

## Klasörler

- `visuals/concept/`: ekran kartları ve eskizler (**referans**, oynanış görüntüsü değildir).
- `visuals/backgrounds/`: oda gibi arka plan referansları.
- `visuals/sprites/`: çocuk, anne, uyku pozu vb. **ayrı PNG'ler**.
- `visuals/ui/`: deprem/sel/yıldırım/heyelan bulutları, 07:00, logo, buton görselleri.
- `visuals/previews/`: konsept panodan kırpılmış örnek ekranlar.
- `data/`: diyalog/karar JSON **örnekleri**. Unity C# okuyucusu yazılmadıkça otomatik çalışmazlar.
- `templates/`: PR, bug ve günlük durum şablonları.

## Ölçülebilir 'Bitti' tanımı

- [ ] Windows'ta menü açılıyor; giriş animasyonu ve geçişler çalışıyor.
- [ ] **Sel**: uyanış → haber → anne → yağmur → söylenti → güvenli karar → kapanış.
- [ ] **Deprem**: ev sahnesi → kısa uyarı → 5 seçenek / 5 saniye → deprem → uygun davranış geri bildirimi → kapanış.
- [ ] Her bölümden menüye dönülebiliyor, kayıp asset/script yok.
- [ ] Orman Yangını ve Heyelan menüde kilitli, boş sahneye gitmiyor.
- [ ] T+36'da özellik geliştirme duruyor, T+48'den önce gerçek build teslim ediliyor.

> **En kritik söz:** Bir ekip üyesi ya da AI agent, başka bir kişinin sahnesini, proje ayarlarını veya kapsamı **sessizce değiştiremez**.
