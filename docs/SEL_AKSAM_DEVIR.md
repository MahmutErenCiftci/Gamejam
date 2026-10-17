# Akşam Sel Bölümü — Devir Belgesi (FloodEvening)

> Durum: **sağlam temel**. Akış uçtan uca kod olarak kurulu; görseller ve arayüzler geçici/sade. Bu belge ekibin (Berk, Arda, Eren) nereden devam edeceğini anlatır.
> Tasarım ve metin kuralları: [`docs/OYUN_AKISI.md` §6.0](OYUN_AKISI.md). Sahiplik: [`planning/EKIP_ROLLERI.md`](../planning/EKIP_ROLLERI.md).

## 1. Akış

```text
DEPREM (EarthquakeHome)
  └─ AFET FARKINDALIK RAPORU ──[Devam: Sel Bölümü]──► GameSceneRouter.ContinueToNextChapter(Earthquake)   (yalnız bir kez)
        │                     [Tekrar Dene] → deprem baştan     [Menüye Dön] → menü
        ▼ (kararma)
MENÜ "Sel" bulutu ──► GameSceneRouter.LoadChapter(Flood) ──► FloodEvening.unity
        ▼
FloodEveningController (tek akış/süre/sonuç sorumlusu)
  Intro ─► Warning ─► RouteChoice ─► SafeRoute ─► Gate ─► CodePanel ─► FinalApproach ─► Success / Timeout ─► Sonuç ekranı
  "SEL BÖLÜMÜ"  telefon   alçak/yüksek  söylenti,    bahçe     şifre        güvenli        süre/su durur     [Sel Bölümünü Tekrar Dene] → yalnız Sel
   kartı        uyarısı   yol (bariyer) geçit, rüzgâr kapısı   paneli       bölgeye yürü                     [Menüye Dön]
```

* **Gizli 120 sn** yalnız uyarı kapanıp kontrol açıldıktan sonra başlar. Diyalog, okuma paneli (`rumor`, `clue`, `sign`, `info`, `read`, `warning` ön ekli paneller), geçiş ve sonuç sırasında durur; aktif mini oyunlarda (`branch`, `gate`, `code`) akar. Sayısal geri sayım yok.
* **Tek ilerleme değeri:** `FloodEveningHub.Progress01` (0..1) = süre = su seviyesi göstergesi = yağmur yoğunluğu = çevresel su (`FloodEveningWaterRise`).
* Durum geri gitmez; modüllerin Hub adımlarından ve oyuncunun konumundan (`HighRoad`, `SafeZone` trigger'ları) türetilir. Modül eksikse o adım atlanır.
* Aynı karede güvenli bölgeye varış + süre dolması → **başarı** (önce güvenli bölge kontrol edilir).
* Süre dolunca açık mini oyun `ResetModule()` ile kapatılır; Cenk olduğu kuru yerde durur (suya girmez).

## 2. Hub (modüller ↔ controller), `Assets/Scripts/Chapters/FloodEvening/FloodEveningHub.cs`

| Olay / alan | Kim yazar | Değerler |
|---|---|---|
| `Progress01`, `Paused`, `FreeRoam` | Controller | 0..1; süre duruyor mu; oyuncu serbest mi (diyalog/panel/mini oyun kapalı) |
| `NotifyOpened(id)` / `NotifyClosed(id)` | Modül | `rumor`, `branch`, `gate`, `code`, `clue…` |
| `Complete(id)` | Modül | `warning_read`, `high_road`, `rumor_checked`, `branch_cleared`, `gate_opened`, `code_opened`, `safe_zone` |
| `ReportMistake(id)` | Modül | `low_road`, `rumor_trusted`, `wrong_code` |
| `Say(lineId, fallback)` | Modül | Controller diyalogla oynatır (süre durur); veride yoksa uyarı şeridinde `fallback` |
| `RequestReset()` | Controller | Modüller kendini sıfırlar |

Arayüzler (`FloodEveningContracts.cs`): `IFloodEveningInteraction` ([E] ile kullanılan her parça: `Point`, `CanUse`, `Play()`), `IFloodEveningWarning` (`ShowWarning()`), `IFloodEveningResettable`. Modüller E tuşunu **kendileri okumaz**; [E] ipucunu ve kullanımı controller'ın `RoomInteractor`'ı yönetir.

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
