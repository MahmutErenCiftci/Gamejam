# EŞİK — kaynakça, AI ve kullanım yerleri

**Güncel kaynak kaydı; eksikler açıkça işaretlidir.** Bu liste bütün proje veya üçüncü taraf eserler için yeni bir lisans vermez. Hazır kaynaklar ve AI kullanımı yarışma formunda ayrıca beyan edilir.

## AI araçları

- **PixelLab:** Kullanıcı karakter GIF’lerinin burada üretildiğini doğruladı. Cenk, anne, önceki çocuk/Arda ve Zeynep yön animasyonları; 34 GIF. [PixelLab](https://www.pixellab.ai/) · [hizmet koşulları](https://www.pixellab.ai/termsofservice). Koşullar çıktıları kullanma/değiştirme/dağıtma imkânını açıklıyor; girdi ve üçüncü taraf hakları kullanıcı sorumluluğunda, Open RAIL-M koşullarına da atıf var. PixelLab çıktıları otomatik CC0 sayılmaz.
- **ChatGPT, OpenAI Codex, Claude/Claude Code:** Ekip genel geliştirme/üretim sürecinde bu araçları kullandığını bildirdi. Model isimleri kullanıcı mesajında “opus 5 .5”, “sol”, “astra” olarak geçti; bunlar doğrulanmış kesin ürün/model sürümüne dönüştürülmedi. Codex/Astra ses paketi kaydı repoda mevcut.
- **Görsel süreç:** AI konseptleri, logo ve referanslar; sprite sheet dönüşümü; kodla çevre/UI/efekt üretimi. 39 JPEG için tam araç/çıktı eşleştirmesi ayrıca tamamlanmalı. Berk’in ui_kit kaynağı Codex olarak bildirilmiş; iki afet bulutunun ilk kaynağı bilinmiyor.
- **Ses süreci:** AI destekli üretim koduyla prosedürel sentez; ayrıca aşağıdaki iki hazır sesin sağlayıcı sayfası AI etiketi taşıyor. Harici üreticinin kullandığı ses modeli bilinmiyor.
- **Metin/diğer:** Prompt/tasarım, koordinasyon ve dokümantasyon; bu README ve raporlar da AI desteğiyle hazırlandı. Oyun içi metin gruplarının araç dağılımı henüz ayrı ayrı teyit edilmedi.

## Pixabay — tekil ses kayıtları

Sekiz proje MP3’si orijinal indirme dosyasıyla SHA256 üzerinden birebir eşleştirildi. Tekil içerik sayfaları da kontrol edildi. Lisans: [Pixabay Content License](https://pixabay.com/service/license-summary/). Dosyalar yeniden adlandırılmış; bu sekizinde MP3 içeriği indirme dosyasıyla aynı. Çalışma zamanı ses düzeyi/loop davranışı dosya değiştirmekten ayrıdır.

| Proje ID | Eser / kaynak | Sanatçı / sağlayıcı | Kullanım | AI etiketi |
| --- | --- | --- | --- | --- |
| `SFX_FE_Tree_Fall` | [Falling Tree... AI Generated.](https://pixabay.com/sound-effects/nature-falling-tree-ai-generated-431321/) | Mollyroselee; Pixabay / Mollyroselee | Sel ağaç düşüşü; FloodTreeFallEvent | Kaynak sayfasında AI generated |
| `SFX_Ball_Kick` | [Soccer Ball Kick](https://pixabay.com/sound-effects/film-special-effects-soccer-ball-kick-37625/) | bittermelonheart (Freesound); Pixabay / freesound_community | Topa vurma; KickableBall | AI kullanılmadığına dair garanti verilmez |
| `SFX_Cenk_Hurt` | [male_hurt7](https://pixabay.com/sound-effects/people-male-hurt7-48124/) | micahlg (Freesound); Pixabay / freesound_community | Cenk tehlike tepkisi; FloodPlayerReaction | AI kullanılmadığına dair garanti verilmez |
| `SFX_FE_Shock` | [Electric shock](https://pixabay.com/sound-effects/film-special-effects-electric-shock-97989/) | thepodcastdoctor (Freesound); Pixabay / freesound_community | Sel elektrik tehlikesi; FloodElectricHazards | AI kullanılmadığına dair garanti verilmez |
| `SFX_Footstep_Wood_Loop` | [Walking on wood](https://pixabay.com/sound-effects/film-special-effects-walking-on-wood-363349/) | Soumages; Pixabay / Soumages | Ev içi ayak sesi; PlayerFootsteps | Kaynak sayfasında AI generated |
| `SFX_Footstep_Outdoor_Loop` | [walking on sidewalk](https://pixabay.com/sound-effects/film-special-effects-walking-on-sidewalk-432109/) | flutie8211; Pixabay / flutie8211 | Dış mekân ayak sesi; PlayerFootsteps | AI kullanılmadığına dair garanti verilmez |
| `AMB_EQ_Assembly_Outdoor_Loop` | [crowd of people talking](https://pixabay.com/sound-effects/people-crowd-of-people-talking-29054/) | SomeoneCool15 (Freesound); Pixabay / freesound_community | Deprem toplanma alanı; AudioAutoWiring | AI kullanılmadığına dair garanti verilmez |
| `SFX_FE_Sweep_Dream` | [Dream Sound Effect (Downscale)](https://pixabay.com/sound-effects/film-special-effects-dream-sound-effect-downscale-7134/) | Unicornaphobist (Freesound); Pixabay / freesound_community | Sel okul/köprü hayal geçişi; FloodSchoolController | AI kullanılmadığına dair garanti verilmez |

Özellikle ağaç ve ahşap adım sesleri dış sağlayıcının AI üretimi olarak beyan edilmeli. `male_hurt7` oyunda Cenk için kullanılsa da kaynak adı/icracısı “çocuk sesi” diye yeniden tanımlanmamalı. Ham MP3’nin public repo dağıtım hakkı lisans koşullarına göre ayrıca kontrol edilmelidir.

[Hash eşleştirmeleri](MP3_ORIJINAL_ESLESMELERI.json) · [Kaynak ayrıntıları](PIXABAY_KAYNAKLARI.json) · [110 ses dosyasının tamamı](SES_KAYNAKLARI.md)

## Kenney

[Interface Sounds](https://kenney.nl/assets/interface-sounds), Kenney, **CC0 1.0**: click_002 → SFX_UI_Click; select_001 → SFX_UI_Hover; back_001 → SFX_UI_Back; confirmation_001 → SFX_Correct; error_001 → SFX_Wrong. [Impact Sounds](https://kenney.nl/assets/impact-sounds), Kenney, **CC0 1.0**: impactWood_light_000 → SFX_Books_Fall; impactGlass_light_000 → SFX_Glass_Crack. Paket manifesti OGG içeriğinin değişmediğini, adlarının değiştirildiğini bildiriyor. Lisanslar `Assets/Audio/Licenses/`, kaynak manifesti `tools/audio/cc0_sources.json`. Runtime Overrides klasörü aynı ID’li sentez yedeğinden önce yüklenir.

## Özgün prosedürel üretim

`tools/audio/generate_audio.js --check` 34 temel WAV için bit düzeyinde eşleşti. Üç müzik döngüsü, yağmur/oda ortamı, UI, alarm/telefon ve deprem efektleri bu kümede. Diğer `tools/audio/` ve final ses üreticileri ayrı kayıtlı; tamamı için bit düzeyinde karşılaştırma yapılmadı. AI’nin yazdığı sentez kodu kullanımı ses/kod kategorilerinde açıklandı.

## Fontlar

| Font | Üretici / kaynak | Lisans | Kullanım / değişiklik |
| --- | --- | --- | --- |
| Pixel Operator | [Jayvee Enaguas / HarvettFox96](https://fontlibrary.org/en/font/pixel-operator) | CC0 1.0 | TR Bold/Heavy: Türkçe glifler, simgeler ve ek kalın kesim türetildi; diyalog/etkileşim/UI. README ve üretim scriptleri mevcut. |
| Pixelify Sans | [Proje yazarları](https://github.com/eifetx/Pixelify-Sans) | SIL OFL 1.1 | Görev HUD’u, sel PIN/UI, oyun sonu ve font kaynakları; kaynak font değişikliği doğrulanmadı. |
| Press Start 2P | [Proje yazarları / CodeMan38](https://fonts.google.com/specimen/Press+Start+2P) | SIL OFL 1.1 | Menü/pause; değişiklik kaydı bulunmadı. |
| Nunito Black | [Nunito Project Authors](https://github.com/googlefonts/nunito) | SIL OFL 1.1 | Dialogue ve Resources/Objectives kaynakları; nihai görünür kullanım ayrıca teyit edilmeli. |
| Liberation Sans | [Liberation Fonts](https://github.com/liberationfonts/liberation-fonts), yerel bildirimde Google/Red Hat | SIL OFL 1.1 | TextMesh Pro SDF/fallback; nihai görünür kullanım ayrıca teyit edilmeli. |

Yerel tam lisans ve telif metinleri korunmalıdır.

## Motor, kod ve bilgi referansları

Unity 6000.6.6f1 / Unity Technologies; URP, Input System, Tilemap, UI, animasyon ve diğer manifest bağımlılıkları. [56 paket/modül kaydı](PAKET_ENVANTERI.md); paketlerin hepsine tek lisans yakıştırılmaz. TextMesh Pro hazır shader/font/materyalleri proje içinde. Geliştirme araçları Node.js ve fontTools kullanan scriptler; bunlar oyunun özgün kodundan ayrı altyapıdır.

QA belgelerindeki bilgi referansları: [AFAD deprem önlemleri](https://www.afad.gov.tr/deprem-oncesi-ani-ve-sonrasi-onlemini-al-guvende-kal), [ilk 72 saat PDF](https://www.afad.gov.tr/kurumlar/afad.gov.tr/3473/xfiles/ilk72saat1.pdf). Bilgi referansı resmî kurumun oyunu onayladığı anlamına gelmez. Birebir metin/görsel kopyalama ve izin durumu ayrıca teyit edilmeli.

## Görsel ekleri ve gerçek promptlar

[73 AI üretim çıktısı](AI_GORSELLER_GALERI.html) · [Berk’in 127 görseli](BERK_GORSELLER_GALERI.html) · [Berk kaynak raporu](BERK_KAYNAK_RAPORU.md) · [35 WhatsApp prompt/tasarım kaydı](WHATSAPP_PROMPT_KAYITLARI.md). Ham WhatsApp kişisel dökümü public rapora alınmadı. Prompt, uygulamanın tamamlandığını göstermez.

## Açık kalan kayıtlar

1. İki MP3: `SFX_FE_Sweep_Scream.mp3`, `SFX_FE_Wade_Loop.mp3`.
2. `Assets/Audio/SFX/EmergencyBag/` altındaki 10 WAV’ın üretim/indirme kaynağı.
3. `bubble_quake_128.png` ve `bubble_flood_128.png` ilk üretim/edinme kaynağı.
4. Eren Ataç’a atfedilen [ESIK_Gorsel_Referanslari.rar](https://drive.google.com/file/d/14n6ppRbt7j0heZrHUrOcnaXMfTlyejtc/view): bağlı Drive hesabı metadata sorgusunda bulunamadı, tarayıcı giriş istiyor; içeriği henüz okunamadı.
5. Kesin model sürümleri, JPEG araç eşleştirmeleri ve nihai hak/onay/test/teslim kayıtları.

Eksikler nedeniyle nihai “eksiksiz uygunluk” onayı verilmedi.
