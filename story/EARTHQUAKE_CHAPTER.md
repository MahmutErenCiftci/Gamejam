# Deprem Bölümü — Detaylı Senaryo ve 5 Saniyelik Karar Mekaniği

> ⚠️ **GÜNCEL DEĞİL — paketin ilk senaryosu.** Bağlayıcı Deprem akışı: [`docs/OYUN_AKISI.md` §7](../docs/OYUN_AKISI.md) (kahraman aynı çocuk, deprem evde, 5 sn karar + Çök-Kapan-Tutun). Güncel metinler: `Assets/Data/Story/earthquake.json`.

**Sahip:** K3 (sahne/mekanik/görsel), K4 (senaryo/eğitim), K1 (geçiş) · **P0** · **Hedef:** 1–2 dk · **İlk demo:** T+18 · **Tamamı:** T+28

## Tasarım amacı

Oyuncu başka bir yetişkinin evinde sabahına girer. Kısa bildirim/olay sonrasında sarsıntı başlar. Oyuncu **güvenli davranışı 5 seçenek arasından 5 saniyede** seçer. Bu olay *farkındalık oyunu kurgusudur*; deprem uyarılarının her yerde kesin 5 saniye önceden geldiği anlatılmaz.

## EQ01 — Sabah (0:00–0:25)

**Mekân:** Yetişkinin salonu; sağlam masa görünür, pencereler ve çıkış kapısı kadrajdadır. Oyuncu eski bölümdeki çocuğu değil yeni bir karakteri görür.

Kısa diyalog:

- Yetişkin: “Günaydın. Kahveyi koydum.”
- Eşi: “Günaydın. Bugün biraz yoğun olacak.”
- Yetişkin telefona bakar.
- Bildirim sesinden hemen sonra bir mesaj: “Deprem ile ilgili uyarı bildirimi — Güvende kal.” (*oyundaki kurgu ekranı; gerçek otorite markası kullanma*).
- Baş karakter: “Ne oluyor?”

## EQ02 — Beş saniyelik karar (0:25–0:40)

Kamera sabit, basit sarsıntı hazırlığı ve **5 büyük seçenek**. Sayacı açmadan **seçeneklerin tamamı görünür** olmalı; oyuncu 5 saniye içinde karar vermelidir. Seçenekler:

| Tuş | Metin | Sonuç | Eğitim gerekçesi |
|---|---|---|---|
| **1** | **Sağlam masanın altına Çök–Kapan–Tutun** | **Doğru** | Baş-boynu koru ve sarsıntı geçene dek tutun. |
| 2 | Pencereye koş | Yanlış | Cam ve düşen parçalar tehlikeli olabilir. |
| 3 | Merdivene koş | Yanlış | Sarsıntı sırasında koşmak/dengesini yitirmek tehlikelidir. |
| 4 | Asansöre git | Yanlış | Deprem sırasında asansör kullanılmaz. |
| 5 | Balkona çık | Yanlış | Dışarı fırlamak/kapı-balkon alanına koşmak risklidir. |

- `5...4...3...2...1` görsel sayaç. Dikkat çekici ama erişilebilir; hızlı ışık flaşı yok.
- Seçim verildiğinde sayaç durur, sarsıntı başlar ve **uygun/kısa geri bildirim** görünür.
- Tuşlara ek olarak mouse butonları çalışmalı.
- Seçim yapılmazsa uyarı: “Sarsıntı başladı! Başını korumayı ve güvenli bir yerde Çök–Kapan–Tutun'u hatırla.” Sonra bir defa tekrar deneyebilir veya sonucu görebilir.
- Doğru seçimde karakter sağlam masanın yanında/altında **Çök–Kapan–Tutun** pozuna geçer (tek kısa sprite/anim yeterli).
- Yanlış seçimde karakterin tehlikeli yere koştuğunu gösteren uzun bir animasyon yapmayın; eylemden **önce** ekran durur ve risk bilgisi çıkar.

## EQ03 — Sarsıntı ve güvenli davranış (0:40–1:05)

- Sarsıntı: kamera azami 1–3 piksel yer değiştirsin, raf/kupa hafif titreşsin, düşük yoğunluklu ses.
- Karakter baş-boynunu kapatır; masanın sağlam ve doğru konumlandığı görünmeli.
- Sonrasında panik/koşma yerine sakin ses ve sonuç ekranına geçiş.

## EQ04 — Sonuç/öğrenme (1:05–1:35)

Ekran metni:

> **Sarsıntı sırasında kendini koru:** Uygun ve güvenli bir noktada Çök–Kapan–Tutun. Baş ve boynunu koru; camlardan ve devrilebilecek eşyalardan uzak dur. Sarsıntı sırasında asansöre binme veya merdivene koşma. Her durumda yerel resmî yönlendirmeleri izle.

Butonlar: `Tekrar Dene` / `Menüye Dön`.

**Not:** “Müteahhit” olması gerekmez; oynanışın amacı meslek değil ev içi deprem davranışıdır. İstersek karakter adı olmadan geçer.

## 48 saatlik yapım paketi

**Zorunlu komponentler:**

- `EarthquakeChapter.unity` (K3; kimse izinsiz değiştirmez).
- `EarthquakeChoiceController.cs`: 5 saniyelik sayaç, 5 seçenek, seçim olayları.
- `EarthquakeFeedback.cs`: kısa doğru/yanlış açıklaması ve sonuç ekranı tetikleyicisi.
- `EarthquakeCameraShake.cs`: tercihen Coroutine; abartısız, disable edilebilir.
- `EarthquakeRoom` görseli: tek arka plan, masa/pencere/kapı.
- Yetişkin/eş: statik piksel karakter dahi kabul; gerekirse geçici siluet, fakat okunur hareket.
- `ResultPanel` ortak component (K1 kontratına göre, K3 kendi sahnesine prefab yerleştirir).

**İkinci bölüm için asla eklenmeyecek şeyler:** Gerçek fizik tabanlı bina çökmesi, çok katlı kaçış yolu, yıkım simülasyonu, kişisel eşya toplama, karmaşık envanter, çoklu son sahne.

## Kabul testleri

- [ ] MainMenu → Deprem düğmesi → bu sahne (kilitsiz).
- [ ] `1` doğru; `2–5` yanlış ve gerekçeleri uygun.
- [ ] 5 sn içinde mouse/klavye kararı çalışıyor; süre dolarsa düzgün fallback.
- [ ] Deprem efekti sırasında UI yanlışlıkla ikinci kez tetiklenmiyor.
- [ ] Yanlış seçeneğin tehlikeli eylemi oyunda ödüllendirilmiyor.
- [ ] Bölüm bittiğinde menüye dönüş; ikinci girişte sayaç temiz başlayabiliyor.
- [ ] Düşük donanımda metin okunur; flaş yok.

## Kullanıcı deneyimi uyarısı

Eğitsel güvenlik mesajı yerel yetkili afet kurumlarının rehberleriyle gözden geçirilmeli. Ekranda kurguya ait kısa bir açıklama sunulabilir: “Bu bir farkındalık oyunudur. Güncel resmî afet talimatlarını takip edin.”
