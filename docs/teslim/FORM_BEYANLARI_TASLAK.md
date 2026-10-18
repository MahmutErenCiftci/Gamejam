# EŞİK — web formu beyan taslakları

**TASLAK.** Köşeli parantezli alanlar cevaplanmadan gönderilmez. Bunlar kabul edilmiş uygunluk beyanı değildir. AI ve hazır kaynaklar formda ayrı yanıtlanır. Araç/model ismi bilinmiyorsa uydurulmaz. Gerçek kullanım başka kategoriye de dokunuyorsa o kategoriye de yazılır.

## Oyun bilgisi

**Oyun adı:** EŞİK  
**Platform:** Windows [final build ile doğrula]  
**Public repo:** https://github.com/MahmutErenCiftci/Gamejam  
**Teslim kaynak sürümü:** [FINAL COMMIT HASH / BRANCH]  
**Kısa açıklama taslağı:** EŞİK, deprem ve sel sırasında verilen kararları konu alan Türkçe bir 2D pixel art farkındalık oyunudur. Oyuncu ev içi hazırlık, korunma ve mahallede güvenli rota seçimi gibi etkileşimlerle kararlarının sonuçlarını görür. [Final buildte bulunmayan davranışları açıklamadan çıkar.]

## A. AI beyanı — beş alanın tamamı

### Görsel — Kullanıldı

Görsel tasarım ve üretimde AI kullanıldı. Ekip tarafından hazırlanan promptlar ve yönlendirmelerle konsept panoları, sahne/arayüz referansları, logo ve karakter animasyonları üretildi. Karakter GIF üretim hizmeti: **PixelLab — https://www.pixellab.ai/**. Ekip genel üretim araçlarını ChatGPT, OpenAI Codex ve Claude/Claude Code olarak bildirdi; her JPEG’in tekil araç/model eşleştirmesi henüz tam değil. Çıktılar seçildi, oyuna uygun parçalar/sprite sheet'ler halinde işlendi ve Unity'ye entegre edildi. AI destekli üretim/dönüştürme kodları da kullanıldı. Kullanım yerleri: menü ve EŞİK logosu, karakterler, çevre/prop/UI görselleri **[EKİP: her grubu gerçek araçla eşleştir]**. Üretim ekinde 39 JPEG ve 34 karakter GIF'inin tamamı, orijinal dosyaları ve SHA256 kayıtları bulunur. Referans panoları final oyun ekran görüntüsü olarak sunulmamaktadır. **[GİRDİ GÖRSELLERİ / HİZMET KULLANIM HAKLARI / YAPILAN DÜZENLEMELER TAMAMLANACAK]**.

### Ses — Kullanıldı

AI desteği ses üretim sürecinde de kullanıldı: OpenAI Codex **[DİĞER ARAÇLAR/MODELLER]** yardımıyla yazılan üretim scriptleriyle müzik döngüleri, ortamlar ve efektler sinüs, gürültü, filtre ve katmanlama yöntemleriyle WAV olarak sentezlendi. Bu, doğrudan bir ses üretim modelinden alınmış ses dosyasıyla aynı yöntem değildir. Kullanım yerleri menü/oynanış müziği, yağmur/oda ortamı, deprem gürlemesi, telefon/alarm ve UI/etkileşim efektleridir. Temel 34 WAV üretim koduyla bit düzeyinde karşılaştırılmıştır. Hazır edinilen Mollyroselee ağaç düşme ve Soumages ahşap adım seslerinin Pixabay sayfaları AI generated etiketi taşıyor; dış üretici modelleri açıklanmıyor. **[TAKIM AYRICA DOĞRUDAN SES/MÜZİK MODELİ KULLANDIYSA ARAÇ VE DOSYA EKLENECEK; YOKLUĞU TAHMİN EDİLMEYECEK]**. Kenney ve dışarıdan alınan sesler ayrıca hazır kaynak beyanında açıklanmıştır.

### Kod — Kullanıldı

Proje yoğun AI desteği/vibe coding yöntemiyle geliştirildi. ChatGPT, Claude/Claude Code (Opus ailesi) ve OpenAI Codex (ekibin kullandığı arayüz adları Sol/Astra), ekipçe yazılan görevler ve promptlar doğrultusunda Unity C# oynanış, sahne geçişi, UI, diyalog/haber entegrasyonu, etkileşim, ses, üretim araçları ve hata düzeltme çalışmalarında kullanıldı. Ekip tasarım yönlendirmelerini, seçme/düzeltme ve entegrasyon kararlarını üstlendi. Repoda görev/devir kayıtları ve WhatsApp’tan ayıklanmış 35 uzun prompt/tasarım kaydı bulunur. Model/sürümün kaydı olmayan yerde kesin model adı verilmemiştir. Hazır Unity motor/paketleri ayrıca hazır kod kaynaklarında belirtilmiştir.

### Metin — Kullanıldı

AI; oyun tasarımı, planlama, görev/prompt belgeleri ve teslim raporlarının hazırlanması/düzenlenmesinde kullanıldı. Belgelerde Claude ve Codex kullanım kayıtları mevcuttur. Oyun içi diyalog, haber, soru, seçenek, feedback ve sonuç metinlerinde kullanılan araçlar ve kapsam: **[ARDA / EREN / MAHMUT: HANGİ ARAÇ, HANGİ METİN GRUBU, ÜRETİM Mİ DÜZENLEME Mİ?]**. Ekip promptları ve yaratıcı yönlendirmeleri hazırladığını beyan etmektedir. Resmî afet rehberlerinden yararlanılan bilgi, AI çıktısından ayrı kaynak olarak belirtilir; bu rehberlerin oyunu onayladığı iddia edilmez.

### Diğer — Kullanıldı

Claude/Codex **[VARSA DİĞER ARAÇLAR]**; teknik planlama, görev dağılımı, üretim/dönüştürme araçları, dokümantasyon, kaynak envanteri ve teslim beyanının hazırlanması için kullanıldı. Bu rapor da Codex yardımıyla hazırlandı. Üretilen çıktılar, ekip kararları ve doğrulanmış test kayıtları ayrı tutuldu. **[UNITY AI / MCP / DİĞER OTOMASYON HİZMETLERİNİN GERÇEK KULLANIMI EKİPÇE TAMAMLANACAK; KURULU PAKET TEK BAŞINA KULLANIM SAYILMADI]**.

## B. Hazır kaynak beyanı — beş alanın tamamı

### Görsel — [Ekip teyidiyle Kullanıldı / Kullanılmadı]

AI ile takım için üretilen görseller AI beyanında açıklandı. Dışarıdan edinilmiş hazır sprite/tile/ikon/arka plan/texture veya AI'ye verilen üçüncü taraf referans görsel olup olmadığı henüz kesinleşmedi. **[HER KAYNAK: AD, ÜRETİCİ/SAĞLAYICI, TEKİL URL, LİSANS/İZİN, OYUNDAKİ YERİ, DEĞİŞİKLİK]**. Projede bulunan Unity/TextMesh Pro hazır grafik materyallerinin final kullanımı da açıklanmalı. Bu alan eksik inceleme nedeniyle otomatik “Kullanılmadı” seçilmemelidir.

### Ses — Kullanıldı

Kenney **Interface Sounds 1.0** (https://kenney.nl/assets/interface-sounds) ve **Impact Sounds 1.0** (https://kenney.nl/assets/impact-sounds), **CC0 1.0** lisansı ile kullanıldı. Interface paketinden click_002, select_001, back_001, confirmation_001, error_001; Impact paketinden impactWood_light_000 ve impactGlass_light_000 dosyaları alındı. UI tıklama/seçme/geri/doğru/yanlış ve kitap/cam efektlerinde kullanılır; dosyalar runtime ID'lerine göre yeniden adlandırıldı. Yerel lisans metinleri ve kaynak eşleştirme manifesti repoda bulunur.

Pixabay’dan kullanılan sekiz sesin tekil kaynakları, sanatçıları ve kullanım yerleri [KAYNAKLAR.md](KAYNAKLAR.md) ile [PIXABAY_KAYNAKLARI.json](PIXABAY_KAYNAKLARI.json) içinde listelenmiştir. Ağaç: Mollyroselee; top: bittermelonheart; Cenk acı tepkisi: micahlg; elektrik: thepodcastdoctor; ahşap adım: Soumages; dış adım: flutie8211; kalabalık: SomeoneCool15; rüya geçişi: Unicornaphobist. Freesound topluluğu kayıtlarının sağlayıcısı Pixabay/freesound_community olarak ayrıca belirtilir. Lisans **Pixabay Content License**; orijinal indirilen MP3’lerle byte içeriği aynı, yalnız dosya adları runtime ID’lerine dönüştürülmüş. Ağaç ve ahşap adım kayıtları AI etiketi taşıdığı için AI ses alanında da açıklanmıştır.

**[KALAN İKİ MP3: SFX_FE_Sweep_Scream / SFX_FE_Wade_Loop; 10 EmergencyBag WAV: KAYNAK, ÜRETİCİ, LİSANS TAMAMLANACAK]**. Ham MP3’lerin public repo dağıtım hakkı ayrıca kontrol edilecek.

### Kod — Kullanıldı

Oyun Unity **6000.6.6f1**, Unity Technologies motoru ve proje manifestinde kayıtlı Unity paketleriyle geliştirildi. URP 17.6.0, Input System 1.20.0, Unity UI 2.6.0, Tilemap/2D ve animasyon bileşenleri kullanılan altyapıdır; TextMesh Pro hazır font/shader/materyalleri proje içinde bulunur. Bu bileşenler takımın özgün kodu olarak sunulmaz. Paket sürümleri ile LICENSE/Third Party Notices konumları `PAKET_ENVANTERI.md` içindedir; lisanslar paket bazında değerlendirilir. **[BAŞKA HAZIR C# KODU, ASSET STORE, GITHUB, TUTORIAL VEYA KÜTÜPHANE KULLANILDIYSA AYNI ALANLARLA EKLE; YOKSA EKİP TEYİDİYLE YOK YAZ]**.

### Metin — [Ekip teyidiyle Kullanıldı / Kullanılmadı]

Afet davranışlarına ilişkin bilgi referansı olarak AFAD'ın deprem önlemleri ve ilk 72 saat rehberleri görev/QA belgelerinde gösterilmiştir: https://www.afad.gov.tr/deprem-oncesi-ani-ve-sonrasi-onlemini-al-guvende-kal ve https://www.afad.gov.tr/kurumlar/afad.gov.tr/3473/xfiles/ilk72saat1.pdf. **[SADECE BİLGİ REFERANSI MI, BİREBİR ALINTI/ÇEVİRİ Mİ? OYUNDAKİ YERİ VE VARSA İZİN KOŞULU]**. Haricî metin örneği, senaryo veya haber kopyası varsa eklenmelidir. Bu beyan resmî kurum onayı içermez.

### Diğer — Kullanıldı

Yazı tipleri: **Pixel Operator / Jayvee Enaguas (HarvettFox96), CC0 1.0** — https://fontlibrary.org/en/font/pixel-operator; Türkçe glifler/simgeler ve TR Heavy kesimi kodla türetildi, değişiklik kaydı ve CC0 metni repoda. **Pixelify Sans / proje yazarları, SIL OFL 1.1** — https://github.com/eifetx/Pixelify-Sans; **Press Start 2P / proje yazarları, SIL OFL 1.1** — https://fonts.google.com/specimen/Press+Start+2P; **Nunito / proje yazarları, SIL OFL 1.1** — https://github.com/googlefonts/nunito; **Liberation Sans / yerel telif bildirimindeki sahipler, SIL OFL 1.1** — https://github.com/liberationfonts/liberation-fonts. Fontlar menü, diyalog ve oyun arayüzü/ilgili font kaynaklarında yer alır; Resources/TMP kaynaklarının final görünür kullanımı ayrıca doğrulanır. Lisans/telif metinleri proje içinde korunur. Üretim araçları Node.js ve fontTools kullanan scriptlerdir; başka hizmet/araç veya dağıtılan içerik varsa eklenmelidir.

## Son gönderimden önce

Tüm `[ ... ]` alanlarını gerçek bilgilerle değiştir. Her kaynağın lisansını ve kullanım yerini belirt; doğrulanmamış “tamamı özgün / tamamı CC0 / tümü elle çizildi / AI kullanılmadı / test edildi” ifadelerini kullanma. Hazır görsel ve hazır metin kategorileri ekip cevabı olmadan sonlandırılamaz. Ayrıntılı rapor web formundaki alanların yerini tutmaz. Güncel koşullar ve hak sahipleri onayı temsilci tarafından incelenip kabul edilmelidir.

## Berk’in ek kaydı

Berk’in kaynakça paketine göre ui_kit.png Codex ile üretilen kaynak sayfası; BubbleFlood/Quake görselleri kırpma/kenar temizliğiyle türetilmiş, glow ve düşünce yolları kodla hazırlanmış. Menü şehir/efekt ve sel kapağı görselleri kodla üretilmiş. **bubble_flood_128.png ve bubble_quake_128.png ilk kaynakları henüz bilinmiyor**; bu nedenle hazır görsel alanı kesinleştirilmeden tamamlanmalı. [Berk kaynak raporu](BERK_KAYNAK_RAPORU.md).
