# EŞİK

> ## ⚠️ Önemli bilgilendirme
>
> **Bu oyun tamamen vibe coding yöntemiyle, yapay zekâ araçlarıyla yapılmıştır.** Oyunun kodu, görselleri, sesleri ve metinleri ile bu depoda belirtilen **tüm referans ve kaynakların araştırılması ve hazırlanması dahil her şey Claude (Anthropic, Claude Code) ve ChatGPT (OpenAI, Codex dahil) ile yaptırılmıştır.** İstisna olarak hazır kaynaklar (açık lisanslı fontlar, Kenney ve Pixabay sesleri) ve PixelLab ile üretilen karakter animasyonları da kullanıldı; bunlar kaynakçada ayrıca listelenmiştir. Ekip; fikirleri, görevleri ve promptları verdi, çıktıları seçti, test etti ve birleştirdi. Ayrıntılı AI ve kaynak beyanı: [docs/teslim/KAYNAKLAR.md](docs/teslim/KAYNAKLAR.md).

**Küçük kararlar, büyük fark yaratır.**

EŞİK, deprem ve sel sırasında verilen kararları konu alan Türkçe, izometrik 2D pixel art farkındalık oyunudur. Cenk'in ev içindeki hazırlıklarından yağmurlu mahalledeki rota seçimine uzanan iki bölüm; etkileşimler, kısa mini oyunlar ve açıklayıcı sonuç ekranlarıyla ilerler.

CIVIL GAME JAM 2026 için Unity 2D ve C# ile geliştirilmiştir. Eski sürümler ve bazı geliştirme belgelerinde **AFET UYANIŞI** adı kullanılır.

**[Windows v0.9.1 indir](https://github.com/MahmutErenCiftci/Gamejam/releases/download/v0.9.1/ESIK_v0.9.1_Windows.zip)** · **[Tüm sürümler](https://github.com/MahmutErenCiftci/Gamejam/releases)** · **[Kaynaklar ve AI beyanı](docs/teslim/KAYNAKLAR.md)**

## İndir ve çalıştır

1. [v0.9.1 release sayfasındaki](https://github.com/MahmutErenCiftci/Gamejam/releases/tag/v0.9.1) `ESIK_v0.9.1_Windows.zip` dosyasını indir.
2. ZIP'in **tamamını** bir klasöre çıkar. Oyunu ZIP'in içinden başlatma.
3. Çıkardığın klasörde **`ESIK.exe`** dosyasını çalıştır.
4. Menüden **Deprem** veya **Sel** bölümünü seçerek başla. İlk bölümün sonuç ekranından diğer bölüme devam edebilirsin.

Oyuncu için Unity Editor kurulumu gerekmez. Windows **x86-64** buildidir; EXE'nin PE mimarisi kontrol edilmiştir. `ESIK_Data`, `UnityPlayer.dll`, `MonoBleedingEdge` ve ZIP'teki diğer destek dosyalarını EXE ile birlikte tut. Çalıştırılacak dosya `UnityCrashHandler64.exe` değildir.

v0.8 indirmesi **59.352.099 bayt**; açılmış dosyaların toplamı yaklaşık **282,7 MiB**'dır. Ölçülmüş minimum CPU/RAM/GPU gereksinimi ve desteklenen Windows alt sürümleri için bu repoda bir test matrisi bulunmuyor.

İndirmeyi doğrulamak için SHA256:

```text
94305e2d229045365f30600c6e589fa00afbf8df7dd47ddf0126f325313142fd
```

## Nasıl oynanır?

**Deprem:** Evdeki hazırlık noktalarını incele; afet çantası, kitaplık ve diğer etkileşimlerle ilerle. Sarsıntı sırasında korunma adımlarını uygula. Sonrasında tehlikeleri değerlendir, merdiven/toplanma alanı akışını takip et ve farkındalık raporunu gör.

**Sel:** Okul ve köprü başlangıcından yağmurlu mahalleye geç. Kapalı yol ile alternatif rota arasındaki farkı gör; çevresel tehlikelerden uzak dur, ritim etkileşiminde ekrandaki işareti takip et ve apartman/PIN finaline ulaş.

Menüde seçtiğin afet önce oynanır. Sonuç ekranından diğer bölüme geçilir; iki bölümün ardından ortak oyun sonu ekranı açılır. Tekrar ve menü seçenekleri ilgili ekranlarda gösterilir. Orman Yangını ve Heyelan, jam sürümünde **kilitli / Yakında** içerikleridir.

Bu bir farkındalık oyunudur. Oyundaki sayaçlar ve mini oyunlar gerçek bir afetin zamanını veya güvenliğini ölçmez. Gerçek bir afette güncel resmî yönlendirmelere uyulmalıdır.

## Kontroller

| İşlem | Kontrol |
| --- | --- |
| Yürüme | **W A S D** veya **ok tuşları** |
| Yakındaki nesneyle etkileşim | **E** |
| Diyaloğu / giriş kartını ilerletme | **E**, **Space**, **Enter** veya **sol tık** |
| Seçenek seçme | Ekrandaki numara (**1–5**) veya **fare** |
| Menü seçimi / onay | **A/D**, **←/→**, **Enter/Space** veya **fare** |
| Açılışı atlama | **Space** veya **Enter** |
| Depremde çökme / kapanma adımları | Ekran istediğinde **E** |
| Depremde tutunma ve denge | **Space basılı tut**, **A/D** ile dengeyi düzelt |
| Sel ritim etkileşimi | Gösterge doğru aralıktayken **E**; ekrandaki İT/ÇEK talimatını izle |
| PIN paneli | **0–9**, **Backspace/Delete**, panelin onay/kapat kontrolleri veya **fare** |
| Duraklatma / geri | **Esc**; açık mini oyun veya panel varsa önce o ekranın geri işlemi uygulanır |
| Sel şimşek parlaması ayarı | **L** |
| Uygun sonuç ekranında tekrar | **R** veya ekrandaki buton |

Kontroller güncel `GameInput` ve ilgili mini oyun kodlarından çıkarılmıştır. Eski tasarım belgelerindeki **S–A–D** korunma sırası güncel denge alıştırmasının kontrolü olarak kullanılmamalıdır. Mini oyunlarda ekrandaki ipuçları takip edilmelidir. Klavye tuşlarını yeniden atama sistemi yoktur.

## Ayarlar ve duraklatma

Ana menüde **Ayarlar** ekranında müzik/efekt ses düzeyleri, tam ekran, çözünürlük, parlaklık ve kontrol bilgileri bulunur. Görüntü ayarları **Uygula** ile uygulanır; tercihler saklanır. **Varsayılanlara Dön** ile ayarlar sıfırlanabilir.

Oyun sırasında **Esc**, uygun durumda **Devam Et / Ana Menüye Dön** menüsünü açar. Ana menüye dönüşte onay istenir. Çanta, kitaplık, ritim, şifre gibi bir panel Esc'i kullanıyorsa önce panel kendi geri işlemini yapar. Ana menüye dönmek mevcut oyun akışını sıfırlar.

## Sürüm geçmişi

18 Ekim 2026 incelemesinde GitHub'da **5 yayımlanmış release** bulundu. Aşağıdaki saatler **UTC+3**'tür. Özellik özetleri release notlarına dayanır; her eski buildin bütün özellikleri yeni sürüm için garanti sayılmaz.

| Sürüm | Yayın | Dosya / kısa özet |
| --- | --- | --- |
| [v0.8](https://github.com/MahmutErenCiftci/Gamejam/releases/tag/v0.8) — kararlı | 18 Eki 2026, 16.26 | `ESIK_v0.8_Windows.zip` — “Jam sürümü.” Önceki sürüme göre ayrıntılı değişiklik notu yayımlanmamış. |
| [v0.7 önizleme](https://github.com/MahmutErenCiftci/Gamejam/releases/tag/v0.7-onizleme) | 18 Eki 2026, 14.40 | İki bölümün sırayla oynanması ve oyun sonu; deprem yönlendirmesi/denge; okul-köprü ve akşam seli; İT/ÇEK ritmi, tehlikeler, PIN finali; yeni UI, adım sesleri ve Esc menüsü. |
| [v0.4.1 önizleme](https://github.com/MahmutErenCiftci/Gamejam/releases/tag/v0.4.1-onizleme) | 17 Eki 2026, 20.58 | Deprem çantasının sırtta görünmesi, daha anlaşılır denge alıştırması ve sokak çevresi ayrıntıları. |
| [v0.4 önizleme](https://github.com/MahmutErenCiftci/Gamejam/releases/tag/v0.4-onizleme) | 17 Eki 2026, 19.34 | Sesler/müzik, çanta görünümü, telefon ekranları, etkileşim ipuçları, ocak ve denge işleri, ön nesnelerin saydamlaşması. |
| [v0.3.1 önizleme](https://github.com/MahmutErenCiftci/Gamejam/releases/tag/v0.3.1-onizleme) | 17 Eki 2026, 18.25 | Şehir manzaralı menü, Sel/Deprem önizlemesi, sel ara sahnesi ve deprem kitaplık sonucu. Notlarda sesin henüz bulunmadığı belirtilmiş. |

v0.7/v0.8 paketlerinde oyun dosyası kökte **`ESIK.exe`**; v0.3.1–v0.4.1 paketlerinde ilgili sürüm klasörünün içinde **`AfetUyanisi.exe`**'dir. GitHub'ın otomatik **Source code** ZIP'i, oynanabilir Windows ZIP'inden farklıdır.

Tüm 5 release ZIP'i indirilerek GitHub SHA256 değerleriyle karşılaştırıldı; **5/5 eşleşti**. Dosya yapıları ve Unity destek dosyaları incelendi. Bu incelemede EXE'ler çalıştırılmadı; tam oynanış testi yapıldığı iddia edilmez. Ayrıntılı commit, boyut, hash ve paket karşılaştırması: **[Release incelemesi](docs/teslim/RELEASE_INCELEMESI.md)**.

## AI kullanımı ve kaynak şeffaflığı

Proje yoğun AI desteği / vibe coding yöntemiyle geliştirilmiştir. Ekip yaratıcı yönlendirmeleri ve görevleri hazırlamış; üretim, kod, entegrasyon ve düzenleme süreçlerinde **ChatGPT, OpenAI Codex ve Claude/Claude Code** kullanmıştır. **Karakter GIF'leri PixelLab ile üretilmiştir.** Kesin model alt sürümleri ve her çıktının araç eşleştirmesi, kayıt bulunduğu ölçüde ayrıntılı beyanda belirtilir.

| AI alanı | Kullanım |
| --- | --- |
| Görsel — **Kullanıldı** | PixelLab karakter/yön GIF'leri; AI konsept ve referansları; AI destekli kodla sprite, UI, çevre ve efekt üretimi/dönüştürme. |
| Ses — **Kullanıldı** | AI destekli kodla prosedürel müzik/efekt sentezi. Ayrıca Pixabay'dan alınan ağaç düşme ve ahşap adım seslerinin kaynak sayfalarında **AI generated** etiketi var; üretici modelleri açıklanmıyor. |
| Kod — **Kullanıldı** | Unity C# sistemleri, oynanış/UI, entegrasyon, üretim araçları ve hata düzeltme. |
| Metin — **Kullanıldı** | Tasarım, görev ve prompt belgeleri; AI destekli yazım/düzenleme süreci ve teslim dokümantasyonu. Oyun içi metin gruplarının ayrıntılı araç dağılımı teyit bekliyor. |
| Diğer — **Kullanıldı** | Planlama, koordinasyon, kaynak envanteri ve rapor hazırlığı. Bu README de Codex desteğiyle hazırlandı. |

AI çıktıları ile üçüncü taraf hazır kaynaklar ayrı kaydedilir. Hazır seslerde **Kenney Interface Sounds / Impact Sounds (CC0)** ve **Pixabay Content License** kapsamındaki kayıtlar; fontlarda **Pixel Operator (CC0)**, **Pixelify Sans / Press Start 2P / Nunito / Liberation Sans (SIL OFL 1.1)** kullanılmıştır veya proje kaynaklarında bulunmaktadır. Kaynak motor/paketleri Unity koşullarına tabidir. Bu eserler takımın elle ve sıfırdan ürettiği içerik gibi sunulmaz.

- **[Kaynakça ve değişiklikler](docs/teslim/KAYNAKLAR.md):** tekil ses bağlantıları, üreticiler, fontlar ve görsel kaynak zinciri.
- **[AI / hazır kaynak form taslağı](docs/teslim/FORM_BEYANLARI_TASLAK.md):** beşer kategorinin ayrı açıklamaları; teyit gereken alanlar işaretli.
- **[AI görsel galerisi](docs/teslim/AI_GORSELLER_GALERI.html):** 39 görsel + 34 karakter GIF'inin tamamı; orijinal dosyalar ve SHA256 kayıtları.
- **[Berk'in görsel/kaynak eki](docs/teslim/BERK_KAYNAK_RAPORU.md):** menü, efekt ve sel kapağı kaynakları, türetilmiş halleri ve kodla üretim kayıtları.
- **[WhatsApp prompt kayıtları](docs/teslim/WHATSAPP_PROMPT_KAYITLARI.md):** 35 oyunla ilgili prompt/tasarım kaydı; kişisel sohbetten ayıklanmıştır. Tasarım talebi, tamamlanmış iş kanıtı sayılmaz.

**Açık kaynak kayıtları:** İki MP3 (`SFX_FE_Sweep_Scream`, `SFX_FE_Wade_Loop`), 10 afet çantası WAV'ı ve iki menü afet bulutunun ilk üretim kaynağı henüz tamamlanmamıştır. Kaynakların ham dosya olarak public dağıtım hakkı ayrıca kontrol edilmelidir. Repo görünürlüğü, üçüncü taraf içeriklere yeni bir lisans vermez. Bütün projeyi kapsayan MIT/CC0 gibi bir lisans bu README ile ilan edilmemektedir. Ayrıntılar: **[Uygunluk raporu](docs/teslim/RAPOR.md)**.

## Ekip

| Üye | Sorumluluk |
| --- | --- |
| [Mahmut](https://github.com/MahmutErenCiftci) | Liderlik, bölüm sahneleri, entegrasyon, görsel/ses çalışmaları ve build |
| [Berk](https://github.com/berkatac41) | Ana menü, ortak UI, ayarlar ve duraklatma menüsü |
| [Arda](https://github.com/ardabekduz123-beep) | Diyalog sistemi/metinleri ve ilgili etkileşim işleri |
| [Eren Ataç](https://github.com/erenatacc41) | Haber/bildirim içerikleri, ilgili görseller ve olay parçaları |

Ayrıntılı sahiplik ve devirler: [Ekip rolleri](planning/EKIP_ROLLERI.md). AI araçları kayıtlı insan ekip üyelerinin yerine yazar veya yarışmacı olarak gösterilmez.

## Unity'de açmak ve geliştirmek

- **Unity Editor: `6000.6.6f1`** — kesin sürüm `ProjectSettings/ProjectVersion.txt` içinde.
- Repo kökünü Unity Hub'dan proje olarak ekle; paketlerin çözülmesini bekle.
- Başlangıç sahnesi: `Assets/Scenes/Menu/MainMenu.unity`.
- v0.8 kaynak sürümünü incelemek için [v0.8 tag'ini](https://github.com/MahmutErenCiftci/Gamejam/tree/v0.8) kullan. Tag'in işaret ettiği commit: `0507fc3c480a11a7ad584267f085191cd639c6db`. `dev` veya daha yeni bir feature dalı aynı sürüm kabul edilmez.
- Build sahneleri: `MainMenu`, `EarthquakeHome`, `FloodSchool`, `FloodEvening`, `GameEnd`.
- Input System ve URP kurulumunu koru; bağımlılık sürümleri `Packages/manifest.json` içindedir.

Katkıdan önce [AGENTS.md](AGENTS.md), [ekip rolleri](planning/EKIP_ROLLERI.md) ve [Git kurallarını](technical/GIT_AND_MERGE.md) oku. Menü/bölüm sahneleri ve ortak dosyaların tek sahipliği vardır. Değişiklikler feature dalında hazırlanır; PR hedefi `dev`'dir.

## Bilinen sınırlamalar ve test kapsamı

- Jam sürümünde Sel ve Deprem bulunur; diğer afetler oynanabilir bölüm değildir.
- Bu README'nin hazırlanmasında release dosyaları statik olarak incelendi. Yeni Windows uçtan uca testi yapılmadı; özellikle çözünürlük/tam ekran davranışı gerçek Windows buildinde kontrol edilmelidir.
- Berk'in devir notunda duraklatma sırasında gerçek zaman kullanan birkaç görsel ayrıntının sürebileceği ve yeni bir AudioSource'un mikser ayarına bağlanmasında en fazla 0,5 saniyelik gecikme olabileceği belirtiliyor.
- Eski sürüm notlarındaki hata ve test eksikleri, v0.8'de giderildiği kanıtlanmadıkça kapalı sayılmaz. Geliştirme belgelerindeki eski akışlar da mevcut buildin özelliği olarak sunulmaz.

Test ve teslim kanıtları: [Doğrulama kaydı](docs/teslim/DOGRULAMA.md) · [Test planı](qa/TEST_PLAN.md) · [Açık bilgiler](docs/teslim/EKSIK_BILGILER.md).
