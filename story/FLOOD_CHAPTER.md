# Sel Bölümü — Detaylı Senaryo ve Sahne Planı

**Sahip:** K2 (oynanış), K4 (metin), K3 (görsel) · **P0** · **Hedef oynanış:** 2–4 dakika · **Başarı kapısı:** T+28'de baştan sona demo.

## Anlatının ana fikri

Çocuk erken uyarıyı anlamaz; yetişkin de ciddiyetini küçümser. Okul yolunda yağış artar ve sel nedeniyle yollar kapanır. Arkadaşları farklı iddialar yayar. Oyuncu **güvenilir bilgi** ve **kendini tehlikeye atmama** ilkesine göre karar vererek bölümü bitirir.

## Sahne F01 — Uyanış (0:00–0:25)

**Mekân:** Çocuk odası; saat `07:00`. Menüdeki odanın yeniden kullanımı. Kamera 2D side view.

- Fade from black, saat alarm sesi, çocuğun gözlerini açması.
- `E - Kalk` UI görünür, basılınca karakter yerinden kalkar.
- Diyalog (iç ses): “Yine okula geç kalmayayım...”
- Etkileşim: kapıya yaklaş, `E - Koridora çık`.
- Kabul: oyun input alıyor, introya geri dönmüyor, takılmadan F02.

## Sahne F02 — Sabah rutini (0:25–0:40)

**Mekân:** Çok küçük banyo ekranı. İş yükünü azaltmak için tek sprite + 1 etkileşim kullanılabilir.

- Lavabonun yanında `E - Yüzünü yıka`.
- 1 sn su efekti + elleri yüze götürme veya tek kare geçiş.
- Karakter: “Bakalım kahvaltıda ne var?”
- Kabul: tek etkileşimle F03'e gider; birden çok tıklama/soft lock yok.

## Sahne F03 — TV haberi ve anne (0:40–1:25)

**Mekân:** Mutfak/salon. TV’de yağış uyarısı grafiği.

- Haber: “Bugün yer yer çok kuvvetli yağış bekleniyor. Sel ve su baskını riskine karşı resmî uyarıları takip edin, suyla kaplı yollardan uzak durun.”
- Renk kodları gösterilebilir fakat **kuruma göre değişen renklerin evrensel anlamı** iddia edilmez. Güvenli basit altyazı: “Uyarı seviyesi: Yüksek. Açıklamayı okuyun.”
- Çocuk: “Anne, bu uyarı neden kırmızı? Dışarı çıkmak tehlikeli mi?”
- Anne: “Yağmur işte canım, hep yağıyor. Bir şey olmaz.”
- Çocuk: “Ama haberde yolların su basabileceğini söylediler.”

**Karar 1: Uyarıyı nasıl değerlendirirsin?**

| ID | Oyuncu seçeneği | Geri bildirim | Güvenli mi? |
|---|---|---|---|
| F-D1-A | “Resmî uyarının ayrıntılarını kontrol edelim.” | “Doğru. Başlığı değil, ayrıntıyı ve yetkili yönlendirmeyi kontrol etmek önemlidir.” | ✅ |
| F-D1-B | “Annem haklı, haber abartılı olabilir.” | “Uyarıyı küçümsemek riskli. Resmî kaynakları kontrol etmek daha güvenlidir.” | ❌ |
| F-D1-C | “Sosyal medyada yazılanlara göre karar verelim.” | “Kaynağı doğrulanmamış paylaşımlar güvenilir değildir.” | ❌ |

**Akış notu:** Her seçimde hikâye devam eder; geri bildirim kısa (2–3 saniye). Gerçek dünyada ciddi uyarı varken çocuk riskli okul yoluna yönlendirilmemeli. Geçiş, yetişkinlerin yol durumu bilgisini beklediği **güvenli bir servis durağı / kapalı alan** çevresinde anlatılabilir.

## Sahne F04 — Şiddetli yağmur (1:25–1:55)

**Mekân:** Yağmurlu sokak ve durak, oyuncunun güvende olduğu kaldırım/kapalı bekleme noktası.

- Başlangıçta hafif yağış; 5 saniye içinde şiddetlenir.
- Araçların ilerlemesi durur; görevli bariyer koymuştur.
- Çocuk: “Yağmur iyice arttı. Neden trafik durdu?”
- Ses: yağmur loop + tek uyarı korna. Kamera 2–3 px atmosfer kayması.

## Sahne F05 — Söylenti zinciri (1:55–2:35)

**Mekân:** Bariyerin gerisinde güvenli bekleme alanı; çocuk ve üç arkadaş.

Diyalog:

- Arkadaş A: “Telefonda bir mesaj gördüm. İleride yol sel yüzünden kapanmış!”
- Arkadaş B: “Ben köprünün tamamen yıkıldığını duydum.”
- Arkadaş C: “Birileri baraj taşmış diyor. Ama kim söyledi bilmiyorum.”
- Çocuk: “Hangisi gerçekten doğru? Bu mesajlar aynı şeyi bile söylemiyor.”

**Oyun mekaniği:** 3 haber kartı görünür. Kartların altında `Kaynak: bilinmiyor` / `Resmî yol duyurusu: tarih/saat mevcut` gibi ayrım. Oyuncu resmî duyuruyu kontrol etme seçeneğiyle ilerler. Uydurma kurumsal logo kullanma; kurgu uyarısı göster.

## Sahne F06 — İkinci karar (2:35–3:00)

**Karar 2: Yol kapalı, ne yaparsın?**

| ID | Seçenek | Geri bildirim | Güvenli mi? |
|---|---|---|---|
| F-D2-A | “Güvenli alanda kalıp yetkililerin açıklamasını beklerim.” | “Doğru. Suyla kaplı yola girmeyin. Resmî uyarıları ve görevli yönlendirmesini izleyin.” | ✅ |
| F-D2-B | “Yol su dolu olsa da karşıya geçerim.” | “Bu tehlikelidir. Suyun derinliği ve akıntı gücü görünenden farklı olabilir.” | ❌ |
| F-D2-C | “Bir arkadaşın paylaştığı kestirmeyi denerim.” | “Doğrulanmamış yönlendirmeler sizi riskli bir alana götürebilir.” | ❌ |

**Güvenlik:** Yanlış seçeneği seçen karakter gerçekten tehlikeli suya girmez. Seçim yalnızca bilgi/uyarı animasyonuyla düzeltilir; başarıyı güvenli davranışa bağla.

## Sahne F07 — Ekipler ve kapanış (3:00–3:30)

- Ekipler yolu emniyete alır ve durumla ilgili açıklama yayınlar. Suyun çekilmesini/kontrolü beklediklerini söylerler; “hemen okul yolu açıldı ve güvendeyiz” şeklinde gerçek dışı acele geçiş yok.
- Çocuk: “Sadece çok yağmur yağacak sanmıştım. Bilginin kaynağı da önemliymiş.”
- Ekranda:
  - **Resmî uyarının içeriğini oku.**
  - **Suyla kaplı yola girme.**
  - **Söylentiyi paylaşmadan kaynağını doğrula.**
- `Tekrar Dene` ve `Menüye Dön`.

## Mekanik teknik kabul

- F01–F07 tek bir bölüm state machine olarak veya 3 Unity sahnesiyle oluşturulabilir.
- Metin/karar verisi K4'ün dosyalarında; K2 kod/Unity'ye bağlar.
- Diyalog kutusu: “devam” `E`, `Space` veya mouse; seçenek için `1–3` ya da mouse.
- Yanlış seçim, yanlış bilgi kalıcı puanı gerektirmez. Öğretici feedback zorunludur.
- Sonuç ekranına ulaşan oyuncu tek tuşla menüye döner.

## Minimum görsel ihtiyaçları

1. Gündoğumu odası ve uyuyan/uyanmış çocuk (mevcut `visuals` klasöründe).
2. Basit lavabolu banyo çizimi (yoksa sahne arası metin kartı).
3. TV / anne konuşma alanı (tek salon arka planı veya görsel kart).
4. Yağmurlu sokak, durak, kapalı yol, 3 arkadaş (konseptte referans var; hazır gameplay asset olmayabilir).
5. Haber UI, diyalog UI, sonuç UI.

**Zaman yetmezse:** F02 ayrı sahne olmaz, F03 tek ekran; F04–F06 yağmur arka planında üst üste UI. **Kritik kararlar ve eğitici kapanış kesilemez.**
