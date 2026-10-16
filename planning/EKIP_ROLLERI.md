# Ekip Rolleri — AFET UYANIŞI (bağlayıcı)

> **Bu dosya, paket belgelerindeki K1/K2/K3/K4 rol tanımlarının yerine geçer.** Paketteki belgeler (AGENT_PLAYBOOK, UNITY_PLAN, story/*, START_HERE) hâlâ "K2 Sel, K3 Deprem, K4 Hikâye" der; dosya ve görev sahipliği için **sadece bu tablo** geçerlidir. Görev sahipleri `planning/TASK_BOARD.csv` içinde isimle yazılıdır.
>
> Oyunun akışı: [`docs/OYUN_AKISI.md`](../docs/OYUN_AKISI.md) · İsimlendirme ve klasörler: [`technical/ISIMLENDIRME_VE_KLASORLER.md`](../technical/ISIMLENDIRME_VE_KLASORLER.md) · Kod sözleşmeleri: `Assets/Scripts/Shared/Contracts/`

## Kim ne yapıyor?

| Kişi | GitHub | Rol | Kısaca |
|---|---|---|---|
| **Mahmut** | MahmutErenCiftci | **Lider / Genel** (paketteki K1 + PM) | Proje ayarları, sahne geçişleri, Sel ve Deprem bölüm sahneleri, entegrasyon, karakter/arka plan görselleri, ses, test, build. **`dev`/`main`'e tek birleştiren.** |
| **Berk** | berkatac41 | **Giriş sayfası + UI** | Ana menü (07:00 intro, 4 rüya bulutu, Yeni Oyun/Ayarlar/Çıkış, kilitli bölümler) ve oyunun tüm ortak UI parçaları (diyalog kutusu görünümü, karar paneli, sonuç ekranı, ipucu, fade, font). |
| **Arda** | ardabekduz123-beep | **Diyalog** | Diyalog sistemi (JSON'dan satır okuma, konuşan adı, E/Space/mouse ile ilerleme, seçenekli kararlar ve geri bildirim) ve tüm diyalog/karar metinleri (anne, iç ses, arkadaşlar, deprem seçenekleri). |
| **Eren** | erenatacc41 | **Haber** | TV haberleri (Sel F03: yağmur uyarısı, Deprem E03: sarsıntı haberi), resmî duyuru kartları (Sel F04, F06), söylenti kartları (Sel F06: 3 iddia, "Kaynak: bilinmiyor"), haber metinlerinin doğruluğu. |

## Dosya sahipliği (sadece sahibi yazar)

| Yol | Sahibi | Diğerleri |
|---|---|---|
| `ProjectSettings/**`, `Packages/**`, build ayarları | **Mahmut** | Değişiklik gerekiyorsa Mahmut'a sor |
| `Assets/Scripts/Core/**` (GameSceneRouter, GameSession) | **Mahmut** | API'yi çağırır, içini değiştirmez |
| `Assets/Scripts/Shared/**` (ChapterId, ortak arayüzler) | **Mahmut** | Değişiklik için talep aç |
| `Assets/Scenes/Chapters/**` (FloodChapter, EarthquakeChapter) | **Mahmut** | Prefab teslim edilir, sahneye Mahmut yerleştirir |
| `Assets/Scripts/Chapters/**` | **Mahmut** | — |
| `Assets/Art/Characters/**`, `Assets/Art/Backgrounds/**`, `Assets/Art/Props/**`, `Assets/Animations/**`, `Assets/Prefabs/Characters/**`, `Assets/Prefabs/Props/**`, `Assets/Audio/**` | **Mahmut** | Referans olarak kullanılır |
| `Assets/Scenes/Menu/**` (MainMenu.unity) | **Berk** | Kimse düzenlemez |
| `Assets/Scripts/Menu/**`, `Assets/Scripts/UI/**` | **Berk** | — |
| `Assets/Prefabs/UI/**`, `Assets/Art/UI/**`, `Assets/Art/Menu/**`, `Assets/Fonts/**` | **Berk** | Arda/Eren UI prefablarını kullanır, görünümünü değiştirmez |
| `Assets/Scripts/Dialogue/**`, `Assets/Prefabs/Dialogue/**` | **Arda** | — |
| `Assets/Data/Story/**`, `story/**` | **Arda** | Diğerleri metni değiştirmez, hata bildirir. `flood.json` ve `earthquake.json` ilk taslak olarak OYUN_AKISI'na göre hazırlandı, Arda buradan devam eder. |
| `Assets/Scripts/News/**`, `Assets/Prefabs/News/**`, `Assets/Art/News/**`, `Assets/Data/News/**` | **Eren** | — |
| `AGENTS.md`, `CLAUDE.md`, `planning/**`, `qa/**`, `technical/**`, `docs/**`, `visuals/**`, `tools/**` | **Mahmut** | Öneri PR'la gelebilir |

**Sahne kilidi:** `MainMenu.unity` sadece Berk; `FloodChapter.unity` ve `EarthquakeChapter.unity` sadece Mahmut. Arda ve Eren **sahne dosyası düzenlemez**; işlerini kendi klasörlerinde **prefab + script** olarak teslim eder, Mahmut bölüm sahnesine yerleştirir. Herkes deneme için `Assets/Scenes/Test/Test_<İsim>_<Konu>.unity` adında kendi test sahnesini açabilir (ör. `Test_Arda_Dialogue.unity`). Başkasının test sahnesine dokunulmaz, bu sahneler build'e girmez.

## Kim kime ne teslim eder?

| Teslim | Kimden → kime | Ne | Hedef |
|---|---|---|---|
| Ortak UI prefabları | Berk → Arda, Eren, Mahmut | `DialoguePanel` (`DialogueViewBase`'ten), `DecisionPanel` (`ChoiceViewBase`, 2–5 seçenek + sayaç), `ResultPanel` (`ResultViewBase`), `InteractionHint` (`InteractionHintBase`), `FadePanel` (`ScreenFaderBase`) | T+12 |
| Diyalog sistemi | Arda → Mahmut | `DialogueRunner` (`DialogueRunnerBase`'ten): JSON'u yükler, satırları Berk'in paneliyle sırayla gösterir | T+12 |
| Karar mantığı | Arda → Mahmut | `DecisionRunner` (`DecisionRunnerBase`'ten): seçenek → seçim → geri bildirim → yanlışta tekrar sor; E_D1'de 5 sn sayaç | T+16 |
| Diyalog metinleri | Arda → Mahmut | `Assets/Data/Story/flood.json`, `earthquake.json` (taslak hazır, yapı `Shared/StoryData.cs`) | T+12 ilk, T+18 son |
| TV haberleri | Eren → Mahmut | `NEWS_F03_TV` ve `NEWS_E03_TV` (`NewsPlayerBase`'ten): haber bandı, başlık, alt yazı, harita | T+14 |
| Duyuru ve söylenti kartları | Eren → Mahmut | `NEWS_F04_OFFICIAL`, `NEWS_F06_RUMORS`, `NEWS_F06_OFFICIAL_ROAD` (gerçek kurum logosu yok) | T+20 |
| Menü → bölüm geçişi | Berk → Mahmut | Menü, `GameSceneRouter.LoadChapter(ChapterId.Flood / Earthquake)` çağırır (**hazır**) | T+10 |

Ortak isimler ve sözleşmeler **yayımlandı**: `ChapterId`, `GameSceneRouter`, `GameInput` ve `Assets/Scripts/Shared/Contracts/*Base`. Bölüm akışı, bağlı olmayan parçayı atlayıp Console'a yazar, böylece herkes kendi parçasını diğerlerini beklemeden bölüm sahnesinde deneyebilir. Parçanın sahneye bağlanması için Mahmut'a haber verilir.

## Dal adları

`feature/<isim>-<iş>` — ör. `feature/berk-menu`, `feature/arda-dialogue`, `feature/eren-news`, `feature/mahmut-flood`. PR'lar `dev`'e açılır, birleştirmeyi Mahmut yapar.

## Pakette karşılığı olmayan yeni görevler

Bunlar `planning/TASK_BOARD.csv` dosyasına eklendi:

| ID | Sahip | Hedef | İş |
|---|---|---|---|
| UI-01 | Berk | T+12 | Ortak UI prefabları (yukarıdaki liste) |
| UI-02 | Berk | T+08 | Türkçe karakter destekli, lisansı net pixel font |
| DIALOG-01 | Arda | T+12 | Diyalog sistemi + JSON yükleyici |
| DIALOG-02 | Arda | T+16 | Seçenekli karar mantığı ve feedback |
| NEWS-01 | Eren | T+14 | Sel TV haberi (`NEWS_F03_TV`) |
| NEWS-02 | Eren | T+16 | Deprem TV haberi (`NEWS_E03_TV`) |
| NEWS-03 | Eren | T+20 | Duyuru ve söylenti kartları (`NEWS_F04_OFFICIAL`, `NEWS_F06_RUMORS`, `NEWS_F06_OFFICIAL_ROAD`) |
| NEWS-04 | Eren | T+24 | Haber metinleri doğruluk kontrolü (renk kodu evrensel değil, sahte kurum/logoyu kullanma) |

## Not: iş yükü

Bu dağılımda Sel ve Deprem bölüm sahneleri, görseller, ses ve test Mahmut'ta. T+18'de geride kalınırsa önce `planning/RISK_AND_CUT_LIST.md` kesme listesi uygulanır; gerekirse Deprem bölümünün UI/sayaç kısmı Berk'e, görsel import Eren'e devredilir (bu dosya güncellenerek).
