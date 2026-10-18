# Akşam Sel Bölümü — Devir Belgesi (FloodEvening)

> Durum: **zikzak harita kuruldu ve uçtan uca oynandı; ağaç düşüşü (Eren), art arda basma + metinler (Arda), arayüz (Berk) ekipte**. Görseller ve arayüzler geçici/sade. Bu belge ekibin (Berk, Arda, Eren) nereden devam edeceğini anlatır.
> Tasarım ve metin kuralları: [`docs/OYUN_AKISI.md` §6.0](OYUN_AKISI.md). Sahiplik: [`planning/EKIP_ROLLERI.md`](../planning/EKIP_ROLLERI.md).

> **GÜNCEL DURUM (18 Eki 01:00, Mahmut — SEL3-01/02):** Zikzak harita (ENV v3) kuruldu ve FloodEvening.unity bununla yeniden kuruldu. **Uyarı → başlangıç sokağı → 2 dönüş → kavşak → kısa yol (köşeden sonra yıkıntıyla kapalı, yürüyerek dönüş) → uzun yol (4 dönüş) → dal → son köşe → güvenli meydan → sonuç** zinciri Unity'de gerçek girdiyle baştan sona oynandı (kısa yol denemesiyle birlikte 69,8 sn, Console hatası yok). Ağaç düşüşü (Eren) ve E'ye art arda basma (Arda) hâlâ ekipte; şu an dal geçici olarak "basılı tut" (SmallBranch) kipinde.

## 0. Gerçek durum

**Çalışan (Unity'de gerçek girdiyle denendi, 18 Eki):**
- FloodEvening.unity: "SEL BÖLÜMÜ" kartı → FE_INTRO → uyarı bildirimi (tek sefer) → serbest yürüyüş.
- Zikzak harita: başlangıç sokağı (eski sel sokağı görselleri) → köşe → ara sokak → köşe → kavşak meydanı (KISA YOL → / ← UZUN YOL tabelaları).
- Kısa yol: aşağı inip köşeyi dönünce yıkıntı + bariyer + "YOL KAPALI"; ötesi derin sel (görsel). BarrierZone'a girince kayıt nötr (`short_road_tried`), oyuncu aynı yoldan yürüyerek kavşağa döner.
- Uzun yol: 4 belirgin dönüş → I sokağında dal (BranchSpot) → dal açılınca son köşe → güvenli meydan (SafeZone) → sonuç ekranı.
- Deprem raporundaki **[Devam: Sel Bölümü]** geçişi Unity'de uçtan uca oynanmadı (**UNTESTED**).

**Çapalar (EveningNeighborhood.prefab, dünya koordinatı):**

| Çapa | Konum | Ne için |
|---|---|---|
| `Spawn` | (-5.05, -1.23) | Cenk'in başladığı yer |
| `Fork` | (6.25, 10.38) | Kavşak; FE_FORK repliği 1,6 birim yarıçapta bir kez |
| `HighRoad` (+ trigger) | (3.25, 12.13) | Uzun yolun başı → `high_road` adımı |
| `LowRoad/BarrierZone` (trigger) | (14.05, 9.28) | Kısa yolun kapalı ucu → `short_road_tried` |
| `TreeStopSpot` (+ trigger) | (15.85, 15.68) | **Eren:** ağaç olayını başlatan güvenli durma noktası (I sokağına dönünce) |
| `TreeFallSpot` | (17.75, 16.63) | **Eren:** ağacın düşeceği yol bölümü (I sokağı gy 13..18 boyunca) |
| `BranchSpot` | (17.75, 16.63) | **Arda:** FE_BranchObstacle burada; I sokağını kesen `BranchBlock` collider'ı açılınca kapanır |
| `SafeZone` (+ trigger) / `SafeSpot` | (27.75, 14.13) | Devam noktası (güvenli meydan) → bölüm sonu |

**Yarım / ekipte:**
- FE_TreeFall (Eren, SEL3-10): yok. Gelince sahne kurulum betiği onu `TreeStopSpot`'a koyar.
- Art arda basma (Arda, SEL3-20): yok; dal şimdilik "basılı tut" kipinde (SmallBranch).
- `flood_evening.json` (Arda, SEL3-21): `FE_SHORT_CLOSED` ve `FE_RESULT_MISTAKES/short_road_tried` hâlâ eksik (Console uyarısı veriyor).

**Harita aracı (Mahmut'ta):** harita bir ızgara tanımından üretiliyor (zemin parçaları, su, duvar/korkuluk hatları, yıkıntı ve tabela sprite'ları, collider'lar) ve prefab + sahne MCP betiğiyle baştan kuruluyor. Haritada değişiklik gerekirse Mahmut'a söyleyin; prefab'ı elle düzenlemeyin (bir sonraki kurulumda üzerine yazılır).

## 1. Akış (zikzak)

```text
DEPREM (EarthquakeHome) ── rapor [Devam: Sel Bölümü] ──► GameSceneRouter.ContinueToNextChapter(Earthquake)  (yalnız bir kez)
MENÜ "Sel" ──► GameSceneRouter.LoadChapter(Flood) ──► FloodEvening.unity
FloodEveningController (tek akış/sonuç sorumlusu)
  Intro ─► Warning ─► RouteChoice ─┬─ kısa yol: köşe → BarrierZone (bir kez FE_SHORT_CLOSED, nötr kayıt) → yürüyerek kavşağa dönüş
   kart    uyarı (1 kez)  sokaklar  └─ uzun yol (HighRoad) ─► LongRoad ─► Obstacle (tree_fell) ─► FinalApproach (branch_cleared, FE_FINAL)
                                                                                                    └─► SafeZone/devam noktası ─► Success ─► sonuç
```
* **120 sn sel sayacı (lider kararı 18 Eki 01:45):** ayrıntılar §1b. Avluya (SafeZone) varınca sayaç durur → apartman finali → bitiş. Süre dolarsa başarısız ekranı ([R] Tekrar Dene).
* Durum geri gitmez. Dal itilir itilmez bölüm bitmez; oyuncu devam noktasına yürüyünce biter.
* Rotadan çıkan modüller (ekip isterse sonra açılabilir; kod/prefab projede): şifre paneli (`CodePanelMinigame`, FE_CodePanelView), bahçe kapısı (`GateHandleMinigame`, FE_GateHandleView), ipucu levhaları (`CodeClueSign`, FE_ClueSign_1..3), söylenti (`RumorCheckPoint`, FE_RumorCheck), rüzgâr (`WindGustZone`, FE_WindPass), itmeli bariyer (`BarrierStop`, FE_LowRoadBarrier). Sahneye konurlarsa controller onları yine kaydeder.

## 1b. 120 sn sel sistemi (Mahmut)

- **Tek başlangıç:** telefon bildirimi kapanınca `FloodEveningController.StartFloodSequence()` bir kez çalışır (tekrar çağrılırsa hiçbir şey olmaz). Bildirim sırasında süre akmaz.
- **Tek zaman kaynağı:** `FloodEveningHub.Progress01 = geçen / 120`. Gösterge, yağmur, harita suyu, yol kapanmaları ve sesler yalnız bunu okur.
- **Süre ne zaman akar:**
  - Akar: serbest yürüyüş, dal itme, rüzgâr koridoru.
  - Durur: diyalog, okuma panelleri (`info*`, `read*`, `warning*`…), zorunlu sinematik (`treefall*`, `cine*`), pause (timeScale 0), başarı/başarısızlık.
- **Ölçülen süreler (bot, kusursuz yürüyüş):**
  - En kısa rota: güvenli bölgeye sayaç 58 sn'de (%48).
  - Kısa yolu deneyip dönme + rüzgârda karışık tepki: 77 sn (%64).
  - İnsan oyuncu için ~90–100 sn bekleniyor.
- **Aşamalar:**
  - %25: YAĞIŞ ARTIYOR.
  - %50: SU YÜKSELİYOR.
  - %75: KRİTİK TEHLİKE (uyarı sesi).
  - %90: SU HIZLA YÜKSELİYOR.
  - %100: SEL KRİTİK SEVİYEYE ULAŞTI, başarısız.
- **Gösterge:** `UI/FloodGaugeView.cs`, sağ kenarda SEL DURUMU paneli.
  - Tüp her kare dokuya çizilir; rakamlar elle çizilmiş piksel rakamlar.
  - Çerçeve rengi: mavi, %75'te turuncu, %100'de kırmızı ve taşma; güvenli bölgede yeşil.
  - Ölçek (0–1,5 m) temsilîdir.
- **Harita suyu:** `Flow/FloodWaterField.cs` + `Assets/Art/FloodEvening/ENV/Flood/FE_FloodWater.shader` (haritayı kaplayan tek sprite, 2D ışıklı).
  - Veri dosyası: `FE_FloodData.png`, `tools/sel_aksam_harita/flood.js` üretir.
  - Dolma sırası: kısa yol ve kanal kıyısı ilk dolar, uzun yol giderek, rüzgâr koridoru en son; avlu kuru kalır.
  - Görünüm: ıslak zemin → birikinti → sığ → derin su, kenar köpüğü, akış yönünde dalga ve yağmur halkaları.
  - Oyuncu: ayak halkaları ve suya gömülen ayak çizgisi; derin suda hafif yavaşlama (0,85 / 0,72).
- **Yol kapanmaları (tasarlanmış):**
  - Kısa yol girişi %42'de kapanır.
  - Geldiğin sokak (C → kavşak) %75'te kapanır.
  - Oyuncu kapının arkasında kalacaksa o yol kapanmaz; hedefe giden yol hiç kesilmez.
- **Ses:**
  - Yağmur hafiften sağanağa geçer (`AudioAutoWiring`).
  - Akan su sesi `AMB_Flood_Water_Loop` ilerledikçe artar.
  - Uyarı sesleri: `SFX_Flood_Warn`, `SFX_Flood_Critical`. Sesler `tools/audio/generate_flood.js` ile üretilir.
- **Test kısayolları (yalnız Editor/Development):**
  - F5 / F6 / F7 / F8 / F9 = %0 / 25 / 50 / 75 / 100.
  - F10 = sayaç ×4.
  - F11 = su katmanını aç/kapa.
  - FloodWaterField seçiliyken sahne görünümünde kapılar çizilir.
- **Metinler:** controller'da Türkçe varsayılanlarla tanımlı. `flood_evening.json` "ui" altına şu anahtarlar eklenirse onlar kullanılır:
  - `flood_fail_title`, `flood_fail_body`, `flood_fail_retry`
  - `toast_critical_*`, `toast_hurry_*`, `toast_home_*`
  - `end_title`, `end_subtitle`
- **Apartman finali:** `IFloodEveningFinale` (`Begin()`, `IsComplete`). Avluya varınca sayaç durur, ardından `Begin()` çağrılır. `home_entered` gelince kararma ve bitiş ekranı ("GÜVENLİ BÖLGEYE ULAŞTIN!" / "AFET UYANIŞI — BÖLÜM TAMAMLANDI").

## 2. Hub (modüller ↔ controller), `Assets/Scripts/Chapters/FloodEvening/FloodEveningHub.cs`

| Olay / alan | Kim yazar | Değerler |
|---|---|---|
| `Progress01`, `Paused`, `FreeRoam`, `FloodStarted`, `FloodStopped`, `FloodSeconds`, `OutdoorAudio01` | Controller | sel ilerlemesi 0..1 (120 sn); sayaç duruyor mu; oyuncu serbest mi; sayaç başladı / durdu; toplam süre; dış ses çarpanı |
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
