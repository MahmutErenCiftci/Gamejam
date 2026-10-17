# Akşam Sel Bölümü — Devir Belgesi (FloodEvening)

> Durum: **temel kuruldu, zikzak rota ekipte**. Görseller ve arayüzler geçici/sade. Bu belge ekibin (Berk, Arda, Eren) nereden devam edeceğini anlatır.
> Tasarım ve metin kuralları: [`docs/OYUN_AKISI.md` §6.0](OYUN_AKISI.md). Sahiplik: [`planning/EKIP_ROLLERI.md`](../planning/EKIP_ROLLERI.md).

> **GÜNCEL DURUM (17 Eki 23:55, LEAD):** Lider kararıyla rota **zikzak** olarak yeniden tasarlandı (`S\newflood\PROMPT_ZIGZAG.md`, repo: `docs/SEL_ZIGZAG_*`). Controller bu yeni akışa çevrildi ve derleniyor; **harita (ENV v3 zikzak), ağaç düşüşü (FE_TreeFall) ve E'ye art arda basma işleri ekibe devredildi.** Sahnedeki dünya hâlâ ENV v2 (eski sel sokağı tabanlı, düz rota + bahçe kapısı/şifre).

## 0. Gerçek durum

**Çalışan (Unity'de gerçek girdiyle denendi):**
- Menü/Deprem → Sel yönlendirmesi kodda: `GameSceneRouter.LoadChapter(Flood)` → `FloodEvening`; deprem raporunda **[Devam: Sel Bölümü]** butonu (kod derleniyor; **deprem raporundan geçiş Unity'de uçtan uca oynanmadı — UNTESTED**).
- FloodEvening.unity açılıyor: "SEL BÖLÜMÜ" kartı → kararma açılır → FE_INTRO balonu → Eren'in uyarı bildirimi ([E] Devam ile kapanır, tek sefer) → serbest yürüyüş. Console hatası yok (err=0).
- Kavşak (Fork) → kısa yol kapalı noktası (BarrierZone): Cenk suya girmeden durur, itme/ışınlama yok, kayıt nötr ("kısa yol denendi"); yürüyerek geri dönüp uzun yola (HighRoad) girince durum LongRoad oluyor.
- Daha önceki sürümde (120 sn'li) süre dolması → sonuç ekranı → fareyle **Menüye Dön** → MainMenu çalıştı; sonuç ekranı ve butonları okunur.

**Yarım / devredilen:**
- ENV v3 zikzak harita (≥2 dönüş kavşağa kadar, uzun yolda ≥3 dönüş, TreeStopSpot/TreeFallSpot/SafeSpot çapaları) — **yok**, ENV'e devredildi.
- FE_TreeFall (tek seferlik ağaç düşüşü + kırık dalı E'ye art arda basarak itme, Hub adımları `tree_fell` / `branch_cleared`, "Yol açıldı.") — **yok**, EVENTS'e devredildi. Şu anki sahnede yerine eski `FE_BranchObstacle` (alternatif geçit, E basılı tut) duruyor.
- `flood_evening.json`'da eksik: `FE_SHORT_CLOSED` sahnesi ("Yol kapanmış. Kavşağa dönüp diğer yolu kullanmalıyım.") ve `FE_RESULT_MISTAKES` içinde `short_road_tried` satırı ("Kısa yolu denedin, kapalıydı — zaman kaybettin."). Controller bunları arıyor; yoksa Console'a uyarı yazıp geçiyor. FE_FORK metni KISA YOL / UZUN YOL'a göre güncellenmeli (Arda).
- v2 sahnesinde rota hâlâ bahçe kapısı + şifre kapısından geçiyor (ikisi sahnede ve çalışır durumda); zikzak haritada bunlar kaldırılacak.
- Uçtan uca (uyarı → kısa yol → uzun yol → dal → devam noktası → sonuç → Tekrar Dene) yeni akışla **tek parça oynanmadı — UNTESTED**.

## 1. Akış (zikzak)

```text
DEPREM (EarthquakeHome) ── rapor [Devam: Sel Bölümü] ──► GameSceneRouter.ContinueToNextChapter(Earthquake)  (yalnız bir kez)
MENÜ "Sel" ──► GameSceneRouter.LoadChapter(Flood) ──► FloodEvening.unity
FloodEveningController (tek akış/sonuç sorumlusu)
  Intro ─► Warning ─► RouteChoice ─┬─ kısa yol: köşe → BarrierZone (bir kez FE_SHORT_CLOSED, nötr kayıt) → yürüyerek kavşağa dönüş
   kart    uyarı (1 kez)  sokaklar  └─ uzun yol (HighRoad) ─► LongRoad ─► Obstacle (tree_fell) ─► FinalApproach (branch_cleared, FE_FINAL)
                                                                                                    └─► SafeZone/devam noktası ─► Success ─► sonuç
```
* **Süre baskısı yok:** geri sayım, süre dolması ve başarısızlık kaldırıldı. Zaman kaybı yalnızca gerçek yürüme süresi. Su seviyesi göstergesi varsayılan kapalı (`showWaterGauge`).
* Ortam ilerlemesi `FloodEveningHub.Progress01` (serbest dolaşmada ~150 sn'de 1'e çıkar): yalnız yağmur ve çevresel su görseli.
* Durum geri gitmez. Dal itilir itilmez bölüm bitmez; oyuncu devam noktasına yürüyünce biter.
* Rotadan çıkan modüller (ekip isterse sonra açılabilir; kod/prefab projede): şifre paneli (`CodePanelMinigame`, FE_CodePanelView), bahçe kapısı (`GateHandleMinigame`, FE_GateHandleView), ipucu levhaları (`CodeClueSign`, FE_ClueSign_1..3), söylenti (`RumorCheckPoint`, FE_RumorCheck), rüzgâr (`WindGustZone`, FE_WindPass), itmeli bariyer (`BarrierStop`, FE_LowRoadBarrier). Sahneye konurlarsa controller onları yine kaydeder.

## 2. Hub (modüller ↔ controller), `Assets/Scripts/Chapters/FloodEvening/FloodEveningHub.cs`

| Olay / alan | Kim yazar | Değerler |
|---|---|---|
| `Progress01`, `Paused`, `FreeRoam` | Controller | ortam ilerlemesi 0..1; okuma/diyalog sırasında duruyor mu; oyuncu serbest mi |
| `NotifyOpened(id)` / `NotifyClosed(id)` | Modül | ör. `treefall`, `branch`; açıkken hareket kilitli |
| `Complete(id)` | Modül | `warning_read`, `high_road`, `tree_fell`, `branch_cleared`, `safe_zone` (+ eski: `rumor_checked`, `gate_opened`, `code_opened`) |
| `ReportMistake(id)` | Modül | `low_road` artık hata sayılmaz (nötr "kısa yol denendi"); diğerleri sonuçta listelenir |
| `Say(lineId, fallback)` | Modül | Controller diyalogla oynatır; veride yoksa uyarı şeridinde `fallback` |
| `RequestReset()` | Controller | Modüller kendini sıfırlar |

Arayüzler (`FloodEveningContracts.cs`): `IFloodEveningInteraction` (`Point`, `CanUse`, `Play()`; E'yi controller'ın RoomInteractor'ı yönetir), `IFloodEveningWarning`, `IFloodEveningResettable`.
**FE_TreeFall için beklenen:** TreeStopSpot'ta tek sefer: `NotifyOpened("treefall")` → animasyon → `Complete("tree_fell")` → `NotifyClosed`; dal: `IFloodEveningInteraction` (Play içinde E'nin her ayrı basışı ilerletir) → `Complete("branch_cleared")` + `Say("FE_PATH_OPEN", "Yol açıldı.")`.

## 3. Dosyalar ve sahipler

| Dosya / prefab | Ne | Sahip (devralan) |
|---|---|---|
| `Assets/Scenes/Chapters/Flood/FloodEvening.unity` | Bölüm sahnesi (kurulum betiğiyle üretildi) | **Mahmut** |
| `Assets/Scripts/Chapters/FloodEvening/Flow/FloodEveningController.cs` | Durum makinesi, gizli süre, sonuç | Mahmut |
| `…/Flow/FloodEveningTexts.cs` | Bütün ekran metinlerini `flood_evening.json`'dan okur | Mahmut (kod) · Arda (metin) |
| `…/Flow/FloodEveningWaterRise.cs` | Çevresel su (WaterAreas) | Mahmut |
| `…/FloodEveningHub.cs`, `FloodEveningContracts.cs` | Modül sözleşmesi | Mahmut (değişiklik için sor) |
| `…/UI/FloodEveningHud.cs` + `Assets/Prefabs/FloodEvening/UI/FE_Hud.prefab` | Su seviyesi göstergesi + uyarı şeritleri | **Berk** |
| `…/UI/FloodEveningTitleCard.cs` + `FE_TitleCard.prefab` | "SEL BÖLÜMÜ" giriş kartı | **Berk** |
| `…/UI/FloodEveningResultView.cs` + `FE_ResultView.prefab` | Sonuç ve öğrenme ekranı | **Berk** |
| `…/UI/FeUi.cs`, `FeButton.cs` | Geçici kodla-çizim yardımcıları (koyu panel, turkuaz) | Berk |
| `…/Puzzle/*` + kapı/şifre prefabları | Bahçe kapısı kolu, şifre paneli, ipucu levhaları | Mahmut (mantık) · **Berk** (panel görünümü) |
| `…/Events/*` + `FE_WarningPhone`, `FE_RumorCheck`, `FE_LowRoadBarrier`, `FE_WindPass`, `FE_BranchObstacle` | Uyarı, söylenti, bariyer, rüzgâr, geçit | Mahmut (mantık) · **Eren** (uyarı/söylenti içeriği) |
| `Assets/Data/Story/flood_evening.json` | Bütün replikler + `result{}` + `ui[]` | **Arda** |
| `Assets/Data/FloodEvening/flood_evening_alert.json`, `flood_evening_rumor.json` | Telefon uyarısı ve söylenti kartları (Eren'in formatları) | **Eren** |
| `Assets/Resources/Objectives/objectives_flood_evening.json` | Görev paneli | Mahmut |
| `Assets/Prefabs/FloodEvening/EveningNeighborhood.prefab` | Akşam mahalle dünyası ve çapalar | Mahmut |
| Değişen ortak dosyalar | `GameSceneRouter.cs` (Flood → FloodEvening, `ContinueToNextChapter`, `FirstSceneOf`), `MenuChapterLoader.cs` (ilk sahne router'dan), `ResultReportView.cs` + `EarthquakeResultManager.cs` (Devam butonu), `IsoPlayerController.cs` (`SpeedMultiplier`), `ObjectiveTracker.cs` (FloodEvening'de `objectives_flood_evening`), `GameCameraSettings.cs` (akşam paleti), `AudioAutoWiring.cs` (FloodEvening müziği), Build Settings (FloodEvening açık; Bedroom/Bathroom/FloodChapter kapalı, silinmedi) | Mahmut |

## 4. Buradan devam et

**Berk (UI)**
- `FE_Hud.prefab`: su seviyesi göstergesi (sağ kenar, dikey) ve uyarı şeridi (ikon + başlık + açıklama) şu an kodla çiziliyor (`FloodEveningHud.Build`). Gerçek sprite'larla yeniden çiz; genel API'yi (`SetGaugeVisible`, `SetLevel`, `SetStage`, `SetGaugeTexts`, `ShowToast`, `HideAll`) koru. Göstergede **sayı/süre gösterme**; altındaki "Suya hiç girme!" notu kalsın.
- `FE_TitleCard.prefab`: `Play(title, subtitle, footnote)`; siyah üstünde kart, en az ~1 sn sonra geçilebilir.
- `FE_ResultView.prefab`: `SetLabels(...)`, `Show(FloodEveningResult)` (seçim `Retry`/`Menu`). Butonlar: **Sel Bölümünü Tekrar Dene** / **Menüye Dön**; klavye ←/→ + Enter, 1/2, fare.
- Kapı kolu ve şifre paneli görünümü (`Puzzle/`): mantık hazır, panel çizimi geçici.
- Deprem raporundaki üç buton (`ResultReportView`): [Devam: Sel Bölümü] [Tekrar Dene] [Menüye Dön] — görünüm geçici fallback renkleri kullanıyor.

**Arda (diyalog)**
- `Assets/Data/Story/flood_evening.json` bütün metinlerin tek kaynağı: diyalog sahneleri `FE_*`, `FE_TITLE` (speaker = kart başlığı, text = alt yazı), `FE_RESULT_STEPS` / `FE_RESULT_MISTAKES` (speaker = Hub id'si), `result{}` (başlıklar, 5 öğrenme maddesi, not) ve `ui[]` (HUD/şerit/sonuç etiketleri; anahtar listesi `FloodEveningTexts.Keys`). Kodda Türkçe metin yok; eksik anahtar Console'a uyarı yazar ve ekranda anahtar görünür.
- Güvenlik kuralları: "suya girme", "kapalı yolu kullanma", "kaynağı doğrula", "resmî yönlendirmeyi izle", "eve ulaşmak için risk alma". Cenk bu bölümde deprem modundaki görünümüyle (yağmurluk/çanta yok) — metinlerde yağmurluk geçmesin.

**Eren (haber)**
- Telefon uyarısı: `flood_evening_alert.json` (AlertNotification; otomatik kapanma yok, "Kurgusal bildirim" notu var, kurum logosu yok).
- Söylenti + resmî duyuru: `flood_evening_rumor.json` (RumorCards): *"Kestirme açıkmış, herkes oradan gidiyor."* — Kaynak: bilinmiyor; duyuru kapalı yolu ve güvenli rotayı söylüyor. İstersen dosyaları `Assets/Data/News/`'a taşı (prefab GUID ile bağlı kalır).

## 5. Test

* Unity'de `FloodEvening.unity`'yi aç → Play, ya da menüden Sel, ya da deprem raporunda [Devam: Sel Bölümü].
* Hızlı süre testi (yalnız Editor): `FloodEveningController.DebugSetElapsed(117)`.
* Console'da `[FE]` satırları adımları gösterir (durum, adım, yanlış, panel açıldı/kapandı, süre durdu/akıyor).

## 6. Bilinen eksikler

* Görseller ve paneller geçici (kodla çizilen koyu panel/turkuaz). Deprem raporu Devam butonu sprite'ı yok (fallback renk).
* Tekrar Dene sahneyi yeniden yükler (modül durumları sahneyle sıfırlanır); `RequestReset` ek güvencedir.
* AGENTS.md §1 P0 #3 ve §8 test sırası eski sabah akışına göre yazılı; lider kararıyla güncellenmeli.
