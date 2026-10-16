# 48 Saatlik Gantt Tarzı Takvim ve Teslim Kapıları

Saatler **Game Jam T+0** anından başlar. Ekip 48 saat aralıksız çalışmaz; mola ve dönüşümlü uyku süreleri bu blokların içindedir. Kritik görevleri aynı saatlerde körlemesine sıkıştırmayın.

## 0–4 saat — Başlatma

- **K1:** Unity sürümü kontrolü; repo yapısı; `.gitignore`, Visible Meta Files, Force Text; `MainMenu` boş sahnesi; input yaklaşımı; 1. commit.
- **K2:** `FloodChapter` temel scene; karakter hareket/etkileşim prototipi; sahne başına state listesi.
- **K3:** assetlerin sayımı ve şeffaflık testi; `EarthquakeChapter` temel scene; görsel palet seçimi.
- **K4:** scope/rol onayı, kısa GDD, iki bölümün oyun metinleri, görev board hazırlığı.
- **Gate T+4:** projenin Unity'de açılması, dört iş alanının ayrı olması.

## 4–10 saat — İlk çalışır vertical slice & ilk build

- **K1:** ana menü (başlık, dört bulut, iki açık seçim, iki kilitli), scene router, Settings/Exit, ilk Windows build.
- **K2:** F01 uyanma + E, F03 TV/anne prototipi; tek basit metin kutusu.
- **K3:** temel pixel assets Unity'ye import; menü/bed PNG; Deprem oda layout; 5 seçenek butonları ve sayaç skeleton.
- **K4:** Sel TV/anne metni, Deprem 5 seçeneği; doğru/yanlış açıklamaları; QA check ilk sürüm.
- **Gate T+10:** Windows build açılır ve MainMenu'den iki bölüme geçişin placeholder hali vardır.

## 10–18 saat — Gerçek mekanikler

- **K1:** 5 saniyelik menü animasyonu, bulutların seçimi, menüye dönüş kontratı, genel bug.
- **K2:** F01–F03 çalışır; karar 1 / diyalog akışı; sahneler arası geçiş.
- **K3:** EQ01–EQ03 çalışır; 5 saniyelik timer, 1–5 input, doğru/yanlış feedback; yağmurlu sokak arka planı ilk hali.
- **K4:** F04–F07 içerikleri; haber kartları, sel karar 2 metni; QA test script; ses ihtiyaçları.
- **Gate T+18:** **Sel'in ilk yarısı** ve **Deprem'in karar mekaniği** ayrı ayrı oynanabilir.

## 18–28 saat — İki bölümün tam ilk sürümü

- **K1:** shared Result UI ve sahne geçişleri, build entegrasyonu, PR merge/soft lock kontrolü.
- **K2:** F04–F07 yağmur, trafik, söylentiler, karar 2, sel sonucu; tam loop.
- **K3:** deprem sonuç ekranı + tekrar oynama; gerekirse tek kısa sarsıntı efekti; K2 için yağmur/arkadaş sprite son sürümü.
- **K4:** içerik QA (sel/deprem güvenliği), ses ekleme, tümdengelim test senaryoları ve sonuç yazımı.
- **Gate T+28:** **her iki bölüm de menüden sonuç ekranına kadar oynanabilir.** Her bölüm owner'ı video/kısa canlı demo sunar.

## 28–36 saat — Hata kapama ve minimal polish

- **K1:** bütün PR'lar birleşir, menü seçimi/transition sağlamlaştırır; RC0 build.
- **K2:** flood yumuşak kilitler, metin taşmaları, kontrol ipuçları ve karar feedback.
- **K3:** eksik sprite bağları, animasyon hızları, ses/efekt minimum polish; deprem input/resets.
- **K4:** iki bölüm uçtan uca regresyon, eğitici final metinleri; jüri sunumu ilk prova.
- **Gate T+36:** Product Owner yazılı onayı; **FEATURE FREEZE** başlar.

## 36–44 saat — QA, bug fix, Windows build

- Yeni bölüm, oynanış, asset sistemi yok. Yalnızca P0 bug fix, okunabilirlik, erişilebilirlik.
- K1 build/merge; K2 Sel bug; K3 Deprem/görsel bug; K4 QA ve test raporu.
- T+40: RC1. T+44: RC2, mümkünse ayrı Windows makinede menü → sel → menü → deprem → menü test.

## 44–48 saat — Teslim

- Final Windows build ve yedek kopya T+46'ya kadar hazır.
- T+44–46: jüri demosu 2–3 dakika prova + oyun ekran görüntüleri.
- T+46–48: yalnızca kritik crash varsa düzelt; teslim kurallarını kontrol et; dosyaları yükle.

## Planı bozabilecek durumlar: neyi keseriz?

| Sorun | Kapsam kararı |
|---|---|
| Görsel üretimi gecikti | Geçici resimle/tek oda arka planı; sel ve deprem mekaniklerini kesme |
| Sel diyalogları çok uzun | Her konuşmayı 1–2 satırda özetle; 2 karar korunsun |
| Deprem animasyonu zor | Karakteri tek güvenli poz sprite'ına geçir; 5 saniye karar ve feedback korunsun |
| Unity merge çakışması | Sahne sahibi branch'i temel; diğer kişinin sahne dosyasına müdahalesi geri alınır; K1 karar verir |
| T+28'de bir bölüm bitmedi | Tüm ekstra animasyonları iptal et; sahipleri sadece o bölümün tam loop'una odaklanır |
| T+44 build açılmıyor | Son çalışan build'e dön; kırılgan polish kodunu çıkar |

## Takımın saatlik minimum kontrolü

- [ ] Repo `dev` birleşik olarak açılıyor mu?
- [ ] Her owner'ın bugünkü tek ana hedefi belli mi?
- [ ] Kapanmayan BLOCKED var mı?
- [ ] Son çalışan build path ve commit kaydı var mı?
- [ ] İki bölüm için P0 kritik görevleri ilerliyor mu?
