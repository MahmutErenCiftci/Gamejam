# Pixel Art Görsel Stil Rehberi — AFET UYANIŞI

**Sahip:** K3 · **Destek:** K1 menü, K2 sel, K4 hikâye

## Görsel hedef

**2D side-view**, sıcak ve duygusal ev içi sahnelerden, yağmurlu mavi tonlara ve deprem için gerilimli fakat **travmatize etmeyen** görselliğe geçen, modern indie pixel art atmosferi.

### Palet ve atmosfer (yol gösterici)

| Tema | Temel tonlar | Hissiyat |
|---|---|---|
| Sabah odası | Lacivert `#1B2441`, gün doğumu `#F7B45B`, turuncu `#E27A44` | Güvenli, sakin |
| Sel | Koyu mavi `#23395B`, yağmur `#6487A8`, soğuk ışık `#A4BED0` | Dikkat ve belirsizlik |
| Deprem | Antrasit `#343645`, sıcak ev `#B88760`, kırmızı vurgu `#DC664C` | Zaman baskısı, paniksiz |
| Yangın (kilitli) | Turuncu `#E87D3A`, koyu kırmızı `#692E31` | Tek bulut illüstrasyonu |
| Heyelan (kilitli) | Kahve `#8A6750`, gri `#56606A` | Tek bulut illüstrasyonu |

**Önemli:** Ekli görseller tutarlı bir üretim kaynağından gelmiyor; karakter boyutları/yüz anatomileri farklı olabilir. K3 bir ana referans seçip Unity sahnelerinde aynı render ölçeğine ve renk uyumuna çeker. Kullanıcıya farklı üç çocuk gibi görünmemeli.

## Ana menü kompozisyonu (640×360 referans koordinatı)

```text
+--------------------------------------------------------------------------------+
|  AFET UYANIŞI     [ DEPREM ]   [ SEL ]    [ YANGIN ]     [ HEYELAN ]            |
|  Yeni Oyun                    (rüya bulutları)                                  |
|  Ayarlar                                                                      |
|  Çıkış                           pencere / oda detayları                       |
|                                                                               |
|              [07:00]   [Uyuyan çocuk ve yatağı]          [masa/raf]            |
+--------------------------------------------------------------------------------+
```

**Not:** Bu şemadaki etiketler yerleşim mantığıdır; gerçek UI, 640×360'da ekranın üstünde 4 büyük illüstrasyonu aynı ölçüde sıkıştırmak yerine bulutları 75–110 px genişliğinde küçültebilir. Ama **seçme alanı görünür ve ayrık** olmalı; karakter ve menü kapatılmamalı.

## Layer sırası (menü)

1. `BG_Bedroom` — tek büyük arka plan (statik).
2. `PROP` — duvar/masa/saat, gerekirse arka planda mevcut oldukları için ikinci kez ekleme.
3. `CHAR_SleepingBoy` — yatak ve çocuk ayrı görüntü ise, arka plan üzerindeki yatakla üst üste bindirmeyi önle.
4. `DreamBubble` — 4 ayrı şeffaf sprite veya UI image.
5. `Canvas` — başlık, butonlar, kilit işareti/tooltip.
6. `FadeOverlay` — fade-in/out.

**Mevcut arka plan görselinde zaten yatak ve saat vardır**; `SleepingBoy` isimli sprite **yatağıyla beraber** gelmektedir. Aynı odaya doğrudan üstüne koyarsanız iki yatak oluşabilir. K3 şu iki stratejiden birini seçmelidir:

- **Hızlı öneri:** Oda arka planını olduğu gibi kullan; yatak bölgesini gerekirse kapatan lokal dekor katmanıyla uyuyan çocuk/yatak sprite'ını yerleştir.
- **Alternatif:** Ayrı uyuyan karakter sprite'ı çıkar/temizle, arka plan yatağını aynen tut. Büyük sanat çalışmasına dönüşürse ilk seçeneğe dön.

## Sprite ve animasyon standartları

- Bütün PNG'lerde gerçek alfa: `A=0` şeffaf alan. Kontrol için görselleri renkli arka plan üstünde açın.
- `Point / No Filter`, mipmaps kapalı; büyütme 2x/3x gibi tam sayı ölçeklere yakın olsun.
- Bir animasyonun 10 görseli var diye bunlar aynı boy/hiza ile çizilmiş **çalışır frame sheet** değildir. `sprite sheet` gerekiyorsa K3 boy/hiza/aksını doğrular ve yeniden keser.
- Çocuk min hareketleri: nefes (scale/transform), blink 1–2 frame, E ile kalk, yürüme (2–4 frame). Deprem yetişkin: bir idle ve güvenli poz yeter.
- Menü rüya bulutları: 4 ayrı `SpriteRenderer` veya `Image`, `CanvasGroup` fade ve `RectTransform` floating.
- UI metinleri: Türkçe karakterli pixel-friendly font; lisans net olmalı.

## Hızlı ve kaliteli sahne planı

**Sel minimum görseller:** 1 ev odası, 1 TV/kahvaltı kartı, 1 yağmurlu sokak + 3 farklı iddia balonu, 1 sonuç UI.  
**Deprem minimum görseller:** 1 salon ve büyük sağlam masa, 1 yetişkin (sabit sprite kabul), 5 seçim kartı, 1 sarsıntı/camera shake, 1 sonuç UI.

**Sinematik:** Ana menü 5 sn; sahne içinde ekstra 20+ sn video/sinematik **yapma**. Kullanıcıyı karar mekaniklerine hızlı getir.

## Görsel paketindeki dosyaları nasıl okuyacaksınız?

- `visuals/concept` ve `visuals/previews`: **konsept ekranı**; Unity oynanışının hazır asset'i değildir.
- `visuals/backgrounds`: sahne görselleri; üstünde yatak/masa gibi objeler zaten bulunabilir.
- `visuals/sprites`: ayrı PNG karakter görselleri (statik poze uygun); hepsi gerçek oyun sprite sheet'i değildir.
- `visuals/ui`: logo, 07:00, afet bulutları. `UI_MenuButtons_REFERENCE.png` dosyası birden çok butonu aynı resimde içerir; butonları **ayrı Unity Button** olarak oluşturup gerekirse K3 ayrı crop/ikon üretmeli.

## Görsel kabul testi

- [ ] Bütün sprite'lar ayrı Unity asset (gerekenler), alfa doğru.
- [ ] Logo ve butonlar 640×360 içinde okunuyor.
- [ ] Uyuyan çocuk ile yatak arka planı çakışmıyor.
- [ ] 4 afet balonunun adı okunuyor, seçilen 2 bölüm vurgulu, kilitli 2 bölüm soluk.
- [ ] Sel ve Deprem UI erişilebilir ve anlaşılır, animasyonlar hikâyeyi yavaşlatmıyor.
