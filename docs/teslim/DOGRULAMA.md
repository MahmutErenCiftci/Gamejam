# TESLİM-01 — gerçek doğrulama kaydı

18 Ekim 2026 / Europe–Istanbul. Bu kayıt oyun testi veya yarışma onayı değildir.

| Kontrol | Sonuç | Kanıt / sınır |
| --- | --- | --- |
| Resmî kurallar sayfası | OK | Tarayıcıda 1–11 maddeleri, ekler ve teslim listesi okundu; PDF'nin tamamı ayrıca okunmuş kabul edilmedi. |
| GitHub görünürlüğü | OK | Bağlayıcı `visibility=public`, varsayılan dal `dev` döndürdü; final commit/build eşleşmesi doğrulanmadı. |
| 34 temel WAV | PASS | `node tools/audio/generate_audio.js --check`; bit düzeyinde karşılaştırma, format/dalga kontrolleri. Çıktı `SES_SENTEZ_DOGRULAMA.txt`. |
| İki ZIP'teki 73 medya dosyasının kopyası | PASS | ZIP girdisi stream'i ve kopya dosyası ayrı SHA256 hesaplarıyla karşılaştırıldı: 73/73 eşleşti. |
| 73 galeri kaydı | PASS | Tarayıcı DOM kontrolü: 73 kart; Tümü filtresinde 73 görünür. |
| Karakter filtresi | PASS | Tarayıcıda filtre seçildi; 34 görünür kart. Tümü'ne dönüldü, 73 kart yeniden göründü. |
| İlk yüklenen galeri görselleri | PASS | İlk DOM kontrolünde yüklenen 18 görselin boyutları geçerli; kırık görsel yok. Kalanlar lazy load; hepsinin aynı anda render edildiği iddia edilmedi. |
| Galeri ekranı | OK | `GALERI_ONIZLEME.png` gerçek tarayıcı ekran görüntüsüdür; AI üretim arşivinin bir parçası değildir. |
| Kaynak medya envanteri | OK | `Assets/` ve `visuals/`: 1.083 dosya; 110 ses, 9 font, 964 görsel; alternatif ve referanslar dahil. |
| Unity / EXE / final ZIP | UNTESTED | Bu görev sırasında oyun çalıştırılmadı, build alınmadı, final ZIP sağlanmadı. |
| Final video | UNTESTED | Video dosyası sağlanmadı. |
| Nihai lisans ve AI beyanı | BEKLİYOR | Kesin araç/model/prompt, tekil ses kaynakları ve kullanım hakları için ekip yanıtları gerekiyor. |

İlk raporlar ve envanter çalışma ağacını tarar. Daha sonra proje/build değişirse final sürümle yeniden karşılaştırılmalıdır. `arsivleri_ekle.ps1` bir defalık ekleme aracıdır; mevcut fotoğrafların üzerine yazmaz ve hedef mevcutsa durur. Kaynak ZIP'ler değiştirilmedi. Envanter/galeri araçları rapor çıktılarını yeniden üretir.

## TESLİM-02 ek kontrolleri

| Kontrol | Sonuç | Kanıt / sınır |
| --- | --- | --- |
| Bütün public release'ler | PASS | Beş release ve beş ZIP; tamamının gerçek SHA256 değeri GitHub digest'iyle aynı. `RELEASE_KAYITLARI.json`. |
| ZIP yapıları | PASS | Beş pakette oyun EXE'si, UnityPlayer, Mono ve Assembly-CSharp bulundu; README/lisans/credits adlı belge bulunmadı. `RELEASE_ZIP_YAPILARI.json`. |
| v0.8 mimari | PASS | ESIK.exe PE machine: 0x8664, AMD64 / x86-64. EXE çalıştırılmadı. |
| Berk arşivi | PASS | 127 PNG + 5 metin; 132 dosyanın arşiv/kopya SHA256 değerleri ayrı hesaplanıp karşılaştırıldı. 127 PNG'nin tamamı mevcut repo medyasıyla da eşleşti. |
| Tüm ek dosyalar | PASS | İlk iki arşivden 73 + Berk arşivinden 132 = 205 dosyanın tamamı mevcut. Medya sayısı 200; diğer 5 dosya metin/lisans. |
| README yerel bağlantıları | PASS | 15 yerel hedef kontrol edildi, eksik hedef yok. |
| WhatsApp | OK | 304 mesaj ayrıştırıldı; 35 uzun oyun prompt/tasarım kaydı ayıklandı. Ham özel sohbet rapor deposuna kopyalanmadı. |
| PixelLab | OK | Kullanıcı 34 karakter GIF'inin aracını PixelLab olarak doğruladı; resmî hizmet koşulları okundu. Tekil üretim promptları/model alt sürümleri tam eşleştirilmedi. |
| Pixabay kaynak eşleştirmesi | PASS | 8/10 proje MP3'si İndirilenler'deki orijinaliyle aynı SHA256. Sekiz tekil resmî içerik sayfası kontrol edildi; ağaç ve ahşap adımda AI etiketi görüldü. |
| Berk ekip testleri | BEYAN | PR #18 devir notundaki testler ekip beyanı olarak saklandı; bu agent'ın testleri olarak gösterilmedi. |
| Eren Drive RAR | ERİŞİLEMEDİ | Bağlı hesap metadata sorgusu 404, tarayıcı giriş ekranı. İçerik okunmuş kabul edilmedi. |
| Oynanış / final teslim | UNTESTED | Windows uçtan uca test ve resmî teslim makbuzu bu görevde üretilmedi. |

README ve bu ekler yerel dosya olarak oluşturuldu. GitHub release'leri, paylaşımlar veya mevcut ZIP'ler değiştirilmedi. Son kaynak envanterinin HEAD'i `ENVANTER.json` içinde kayıtlıdır; çalışma sırasında insan tarafından yapılan merge ve yerel oyun değişiklikleri agent'ın belge işiyle karıştırılmamalıdır.
