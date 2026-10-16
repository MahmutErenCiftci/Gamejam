# Ekip Rolleri — AFET UYANIŞI (bağlayıcı)

> **Bu dosya, paket belgelerindeki K1/K2/K3/K4 rol tanımlarının yerine geçer.** Paketteki belgeler (AGENT_PLAYBOOK, UNITY_PLAN, story/*, START_HERE) hâlâ "K2 Sel, K3 Deprem, K4 Hikâye" der; dosya ve görev sahipliği için **sadece bu tablo** geçerlidir. Görev sahipleri `planning/TASK_BOARD.csv` içinde isimle yazılıdır.

## Kim ne yapıyor?

| Kişi | GitHub | Rol | Kısaca |
|---|---|---|---|
| **Mahmut** | MahmutErenCiftci | **Lider / Genel** (paketteki K1 + PM) | Proje ayarları, sahne geçişleri, Sel ve Deprem bölüm sahneleri, entegrasyon, karakter/arka plan görselleri, ses, test, build. **`dev`/`main`'e tek birleştiren.** |
| **Berk** | berkatac41 | **Giriş sayfası + UI** | Ana menü (07:00 intro, 4 rüya bulutu, Yeni Oyun/Ayarlar/Çıkış, kilitli bölümler) ve oyunun tüm ortak UI parçaları (diyalog kutusu görünümü, karar paneli, sonuç ekranı, ipucu, fade, font). |
| **Arda** | ardabekduz123-beep | **Diyalog** | Diyalog sistemi (JSON'dan satır okuma, konuşan adı, E/Space/mouse ile ilerleme, seçenekli kararlar ve geri bildirim) ve tüm diyalog/karar metinleri (anne, iç ses, arkadaşlar, deprem seçenekleri). |
| **Eren** | erenatacc41 | **Haber** | TV haber sekansı (Sel F03: haber bandı, uyarı ekranı), söylenti/haber kartları (Sel F05: 3 iddia + resmî duyuru kartı, kaynak etiketi), Deprem kurgu uyarı bildirimi (EQ01–EQ02), haber metinlerinin doğruluğu. |

## Dosya sahipliği (sadece sahibi yazar)

| Yol | Sahibi | Diğerleri |
|---|---|---|
| `ProjectSettings/**`, `Packages/**`, build ayarları | **Mahmut** | Değişiklik gerekiyorsa Mahmut'a sor |
| `Assets/Scripts/Core/**` (GameSceneRouter, GameSession) | **Mahmut** | API'yi çağırır, içini değiştirmez |
| `Assets/Scripts/Shared/**` (ChapterId, ortak arayüzler) | **Mahmut** | Değişiklik için talep aç |
| `Assets/Scenes/Chapters/**` (FloodChapter, EarthquakeChapter) | **Mahmut** | Prefab teslim edilir, sahneye Mahmut yerleştirir |
| `Assets/Scripts/Chapters/**` | **Mahmut** | — |
| `Assets/Art/Characters/**`, `Assets/Art/Backgrounds/**`, `Assets/Animations/**`, `Assets/Audio/**` | **Mahmut** | Referans olarak kullanılır |
| `Assets/Scenes/Menu/**` (MainMenu.unity) | **Berk** | Kimse düzenlemez |
| `Assets/Scripts/Menu/**`, `Assets/Scripts/UI/**` | **Berk** | — |
| `Assets/Prefabs/UI/**`, `Assets/Art/UI/**`, `Assets/Art/Menu/**`, `Assets/Fonts/**` | **Berk** | Arda/Eren UI prefablarını kullanır, görünümünü değiştirmez |
| `Assets/Scripts/Dialogue/**`, `Assets/Prefabs/Dialogue/**` | **Arda** | — |
| `Assets/Data/Story/**`, `story/**` | **Arda** | Diğerleri metni değiştirmez, hata bildirir |
| `Assets/Scripts/News/**`, `Assets/Prefabs/News/**`, `Assets/Art/News/**`, `Assets/Data/News/**` | **Eren** | — |
| `AGENTS.md`, `CLAUDE.md`, `planning/**`, `qa/**`, `technical/**`, `docs/**`, `visuals/**`, `tools/**` | **Mahmut** | Öneri PR'la gelebilir |

**Sahne kilidi:** `MainMenu.unity` sadece Berk; `FloodChapter.unity` ve `EarthquakeChapter.unity` sadece Mahmut. Arda ve Eren **sahne dosyası düzenlemez**; işlerini kendi klasörlerinde **prefab + script** olarak teslim eder, Mahmut bölüm sahnesine yerleştirir. Test için kendi klasörlerinde ayrı bir test sahnesi açabilirler (ör. `Assets/Scenes/Test/Arda_DialogueTest.unity`); bu sahneler build'e girmez.

## Kim kime ne teslim eder?

| Teslim | Kimden → kime | Ne | Hedef |
|---|---|---|---|
| Ortak UI prefabları | Berk → Arda, Eren, Mahmut | `DialoguePanel`, `DecisionPanel` (3 ve 5 seçenek), `ResultPanel`, `InteractionHint`, `FadePanel` (görünüm, boş mantık) | T+12 |
| Diyalog sistemi | Arda → Mahmut | `DialogueRunner` (JSON yükle, satır satır göster, bitince event) + Berk'in panelini kullanan prefab | T+12 |
| Karar mantığı | Arda → Mahmut | Seçenek göster → seçimi al → feedback göster → event (`correct` bilgisiyle) | T+16 |
| Diyalog metinleri | Arda → Mahmut | `Assets/Data/Story/flood.json`, `earthquake.json` (paketteki `data/*.json` yapısıyla) | T+12 ilk, T+18 son |
| TV haber sekansı | Eren → Mahmut | `NewsBroadcast` prefabı: haber bandı, uyarı ekranı, metin; bitince event | T+14 |
| Söylenti kartları | Eren → Mahmut | 3 iddia kartı + resmî duyuru kartı, "Kaynak: bilinmiyor" etiketleri | T+20 |
| Deprem uyarı bildirimi | Eren → Mahmut | Kurgu içi telefon/TV bildirimi prefabı (gerçek kurum logosu yok) | T+16 |
| Menü → bölüm geçişi | Berk → Mahmut | Menü, `GameSceneRouter.LoadChapter(...)` çağırır | T+10 |

Arayüz adları `technical/INTERFACE_CONTRACTS.md`'deki önerilerle uyumlu olmalı; Mahmut T+6'da ortak isimleri (`ChapterId`, `GameSceneRouter`) duyurur.

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
| NEWS-01 | Eren | T+14 | Sel TV haber sekansı (F03) |
| NEWS-02 | Eren | T+16 | Deprem kurgu uyarı bildirimi (EQ02) |
| NEWS-03 | Eren | T+20 | Söylenti/haber kartları (F05) |
| NEWS-04 | Eren | T+24 | Haber metinleri doğruluk kontrolü (renk kodu evrensel değil, sahte kurum/logoyu kullanma) |

## Not: iş yükü

Bu dağılımda Sel ve Deprem bölüm sahneleri, görseller, ses ve test Mahmut'ta. T+18'de geride kalınırsa önce `planning/RISK_AND_CUT_LIST.md` kesme listesi uygulanır; gerekirse Deprem bölümünün UI/sayaç kısmı Berk'e, görsel import Eren'e devredilir (bu dosya güncellenerek).
