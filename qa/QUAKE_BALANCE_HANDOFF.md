# QUAKE-BALANCE-01 — Çök–Kapan–Tutun denge alıştırması

- Owner: Arda'nın kullanıcı tarafından istenen entegrasyon görevi; son inceleme/merge Mahmut.
- Dal: `feature/arda-interaction-ui` (önceki etkileşim UI değişikliğiyle birlikte).
- Hedef: mevcut Deprem akışında E → E → A/D + Space; yaklaşık 16 sn denge aşaması.
- Bağımlılıklar: mevcut `EarthquakeHome`, `ProtectionMinigameController`, `ProtectionView`, Input System, TMP / Pixelify Sans.
- Sahne, prefab, paket sürümü ve ProjectSettings dosyaları bu görev için elle değiştirilmedi.

## Oyunda kullanım

1. Menüden Deprem'i başlat; mevcut 120 sn hazırlık akışını tamamla.
2. Sarsıntı başlayınca **E** ile çök, tekrar **E** ile başını/enseni koru.
3. **Space basılı** tutarken **A / D** (veya sol/sağ ok) ile denge göstergesini düzelt.
4. Space bırakılırsa karakter korunma pozunda kalır; tekrar basarak devam edilebilir.
5. Sarsıntı bitince ayrı denge alıştırması puanı ve eğitici geri bildirim çıkar; mevcut çanta/çıkış/rapor akışı sürer.

Masa yalnızca sarsıntı başlangıcında hemen erişilebilir konumdaysa kullanılır. Uzakta olan oyuncu masaya koşturulmaz. Güvenli yerinde korunma ile masa altındaki korunmanın bölüm puanı eşittir. Denge puanı ayrı bir oyun alıştırmasıdır; yaralanma/ölüm veya hayatta kalma olasılığı üretmez.

### Anlaşılırlık güncellemesi (Mahmut, 17 Ekim)

- TUTUN başında kısa talimat: gösterge durur, "İşareti YEŞİLDE tut: A sola, D sağa çeker" + alt şerit "Başlamak için SPACE'e bas ve basılı tut". Denge SPACE'e basılınca başlar (en az 1.2 sn, en geç 4 sn; `introMin/MaxSeconds`).
- Gösterge: [A] ◄ kırmızı(!) | amber | yeşil | amber | kırmızı(!) ► [D]; yeşilden çıkınca gereken tuş parlar ve "A/D ile ortaya dön", kırmızıda "Dengeni koru!" yazar. Kalan süre "12 sn" + azalan çubuk. Canlı puan gösterilmez.
- Bitişte 1.5 sn "Sarsıntı bitti — iyi tutundun!" / "Biraz zorlandın…" (`resultSeconds`); kartta "Denge: N/100".
- Model varsayılanları yumuşatıldı (yeşil 0.35, itme 4, frekans 0.4, düzeltme 6.5, sönüm 8): eskiden girdisiz kalmak yeşilde tutuyor, A/D ise aşırı savuruyordu. Eski değerleri taşıyan bileşenler `balanceTuning` sürümüyle bir kez yeni varsayılanlara yükseltilir.
- Testler: model menüsüne "girdisiz kalınca yeşilden çıkar", "0.45 / 0.6 sn gecikmeli oyuncu başarır"; entegre teste "talimat SPACE beklenir" ve "bitiş sonucu görünür" eklendi.

## Bağlantı ve ayarlar

`EarthquakeHome` içindeki mevcut korunma kontrolcüsü/view referansları kullanılır. `ProtectionView` gerektiğinde tek `EarthquakeBalanceHUD` oluşturur. İkinci hazırlık sayacı, hareket sistemi veya ayrı mini oyun sahnesi eklenmedi.

- `ProtectionMinigameController > Balance Settings`: 4 / 6 / 6 sn evreler, kuvvet/frekans, A/D düzeltme, sönümleme, yeşil/kırmızı eşikler, puan kaybı ve tuş bırakma toleransı.
- `ProtectionMinigameController > Camera Zoom`: en fazla %15; varsayılan %6.
- `EarthquakeCameraShake > Reduced Motion`: kamera sarsıntısı ve korunma zoom'unu kapatır; denge girdisi/UI sürer.
- `EarthquakeEventController > Generated Rumble Volume`: atanmış ses kaynağı yoksa kodun ürettiği özgün gürlemenin seviyesi.
- `Assets/Data/Earthquake/earthquake_mode.json > protection.balance`: Türkçe aşama, kontrol ve sonuç metinleri.

Kitaplık sabitleme sonucu mevcut `BookshelfAnchorMinigame.Secured` üzerinden işlemeye devam eder. Çanta sarsıntıda etkileşime kapalıdır; hazırlığı/sonraki alımı mevcut sistemden gelir.

## Değiştirilen dosyalar

Bu görevde eklenenler:
- `Assets/Scripts/Chapters/Earthquake/Mode/EarthquakeBalanceModel.cs` — deterministik dalga, denge ve alıştırma puanı.
- `Assets/Scripts/Chapters/Earthquake/UI/EarthquakeBalanceHUD.cs` — canlı gösterge, evre, kontrol ve uyarılar.
- `Assets/Scripts/Chapters/Earthquake/Editor/EarthquakeBalanceChecks.cs` — model/entegre test menüleri.

Güncellenenler:
- `Assets/Scripts/Core/GameInput.cs` — ortak Space basılı okuması.
- `Assets/Scripts/Chapters/Earthquake/Mode/ProtectionMinigameController.cs` — mevcut E/E akışından dengeye geçiş, poz/zoom ve iptal temizliği.
- `Assets/Scripts/Chapters/Earthquake/Mode/EarthquakeModeController.cs`, `EarthquakeStats.cs`, `EarthquakeResultManager.cs` — tek tetikleme, sonuç ve ayrı denge puanı.
- `Assets/Scripts/Chapters/Earthquake/Mode/EarthquakeUIContracts.cs`, `Assets/Scripts/Chapters/Earthquake/UI/ProtectionView.cs` — mevcut görünümle uyumlu ek kancalar.
- `Assets/Scripts/Chapters/Earthquake/Mode/EarthquakeEventController.cs`, `ShakeableProp.cs`, `Assets/Scripts/Chapters/Earthquake/EarthquakeCameraShake.cs` — şiddet, özgün gürleme, odak ve azaltılmış hareket.
- `Assets/Scripts/Chapters/Earthquake/Mode/EarthquakeModeData.cs`, `Assets/Data/Earthquake/earthquake_mode.json` — metinler.

Yeni mini oyun prefabı gerekmez; mevcut korunma bileşenleri kendileri bağlanır. Önceki etkileşim UI görevinin TMP kaynakları ve ortak ipucu prefabı aynı dalda bulunur.

## Görsel ve ses teslimi

Mevcut Çök/Kapan/Tutun sprite'ları kullanılır. Ayrı brace-left/right ve recovery kareleri eklenmedi: tutunma sprite'ının görsel kopyası kontrollü küçük açıyla eğilir; karakterin fizik/collider kökü dönmez. Sarsıntı sonrası mevcut poz temizleme/masadan çıkış kullanılır.

Mevcut eşya, lamba, toz, kitap düşme ve kitaplık devrilme efektleri tekrar kullanılır; şiddet çarpanı eklenir. Kaynak klip atanmamışsa özgün, düşük frekanslı döngü üretilir. Ayrı ahşap gıcırtısı/çarpma/success ses klipleri üretilmedi; mevcut olay kancaları korunur.

## Doğrulama

- Unity 6000.6.6f1 derlemesi: PASS; C# scriptleri Unity'de derlendi.
- Model: PASS — A/D yönleri, 30/120 FPS, düzeltilebilir dalgalar, sınırlar, pause, tuş bırakıp toparlanma.
- Entegrasyon: PASS — gerçek `EarthquakeHome`, native 640×360 Game View; sayaç/sarsıntı bir kez, hazırlık etkileşimleri kapalı, denge sonucu → sonrası → rapor.
- Input System: PASS — sanal klavyeden E, A+Space, D+Space; Space bırakıp yeniden tutma, tek sonuç callback'i; zoom/karakter görünümü geri yükleme.
- Güvenli yerinde korunma: PASS — masa altı ile aynı bölüm korunma puanı.
- Çanta durumu: PASS — otomatik senaryoda hazırlanmış durumun sarsıntıda korunması ve sonrasında alınması; çanta mini oyunu bu testte elle oynanmadı.
- Yaşam döngüsü: PASS — azaltılmış harekette kamera sabit; korunma yarıda devre dışı bırakılınca karakter görünümü ve running durumu geri yüklendi.
- Windows build / farklı bilgisayar: UNTESTED; bu görevde build alınmadı.

Tekrar çalıştırma: **Afet Uyanisi → Earthquake balance → Run model checks / Run integrated Play Mode checks**. Play Mode kontrolü gerçek sahnede otomatik oyun senaryosunu ve ayrı sanal Input System klavyesiyle E, A/D ve Space girişlerini kullanır. Testte Game View geçici olarak 640×360 olur, ardından önceki seçimine döner. Sahne asset'i kaydedilmez.

Ham raporlar: `Logs/earthquake-balance-model-checks.txt`, `Logs/earthquake-balance-play-checks.txt`; görüntü: `Logs/earthquake-balance-integrated.png`.
Ek MCP yaşam döngüsü raporu: `Logs/earthquake-balance-lifecycle-checks.txt`.

## Eğitsel kaynak ve teslim

Davranış çerçevesi: [AFAD — Deprem öncesi, anı ve sonrası](https://afad.gov.tr/deprem-oncesi-ani-ve-sonrasi-onlemini-al-guvende-kal). Sarsıntı boyunca baş/enseyi koruma ve sarsıntı sonrasında çantayı alma ilkeleri korunur. A/D oyunu resmî bir deprem tekniği olarak sunulmaz.

Sonraki sahip: Mahmut — görev dalını inceleme, gerekirse şiddet/denge ayarı, `dev` entegrasyonu ve Menü → Sel → Deprem regresyonu. Commit/push/merge otomatik yapılmadı.
