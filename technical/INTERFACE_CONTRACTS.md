# Entegrasyon Kontratları — Kimin Kodu Kimin Koduyla Konuşur?

> ✅ **Sözleşmeler uygulandı:** `Assets/Scripts/Core/` (`GameSceneRouter`, `GameSession`, `GameInput`) ve `Assets/Scripts/Shared/` (`ChapterId`, `StoryData`, `Contracts/*Base`). Bu belgedeki örnek isimler yerine koddakiler geçerlidir; özet: [`docs/OYUN_AKISI.md` §9](../docs/OYUN_AKISI.md).

**İlk yayın sahibi:** K1 · **Dondurma hedefi:** T+6 · **Değişiklik:** K2/K3'e haber verilmeden API kırılmaz.

Bu dosya **tasarım sözleşmesidir**, mevcut Unity repo içinde kod mevcut olduğunu iddia etmez. K1 bu isimleri uygular veya eşdeğer sınıf isimlerini yazılı ilan eder.

## 1. Scene yolları ve ID'leri

```text
ChapterId.Flood      -> Assets/Scenes/Chapters/Flood/FloodChapter.unity
ChapterId.Earthquake -> Assets/Scenes/Chapters/Earthquake/EarthquakeChapter.unity
ChapterId.Wildfire   -> LOCKED
ChapterId.Landslide  -> LOCKED
MainMenu             -> Assets/Scenes/Menu/MainMenu.unity
```

*İpucu:* Unity `SceneManager.LoadScene()` build listesine eklenmiş kısa scene adını kullanabilir. Dosya taşıma/build configuration sadece K1.

## 2. Minimum public API sözleşmesi (örnek)

```csharp
public enum ChapterId { Flood, Earthquake, Wildfire, Landslide }

public static class GameSceneRouter
{
    public static bool IsUnlocked(ChapterId id);
    public static void LoadChapter(ChapterId id);
    public static void ReturnToMenu();
}
```

**K1 uygular.** Bölümler `GameSceneRouter.ReturnToMenu()` çağırır; Router'ın içeriğini edit etmez.

```csharp
public interface IChapterResult
{
    void ShowResult(bool safeChoice, string teachingMessage);
    void RestartChapter();
    void ReturnToMenu();
}
```

Bu interface zorunlu sınıf adı değil; K1 T+6'da tam sözleşmeyi sabitler. UI gerekiyorsa `ResultPanel.Show(title, body)` gibi hafif adaptör yeter.

## 3. Veri contract'ı

**K4 content owner**, dosyaları `Assets/Data/Story/` altında teslim eder. K2/K3 yalnızca okur. JSON örnekleri `data/flood_story_example.json`, `data/earthquake_story_example.json`.

Kurallar:
- ID'ler sabittir ve **kod bunlara bakar**; metin değişebilir.
- `choice.id` benzersiz; `correct` true/false içerik etiketidir, mantıkta skor zorunlu değildir.
- 5 Deprem seçeneğinin `key` değerleri 1..5; `timeout` fallback metni var.
- Kullanıcıya gösterilen metin alanları K4 tarafından doğrulanır.
- JSON Unity'de otomatik okunmaz; K2/K3 uygun script ile yükler veya ilk MVP'de Inspector üzerinden girer. Aynı anda hem JSON hem Inspector manuel kopya oluşturup çelişki yaratmayın.

## 4. Görsel kontratı (K3 → K1/K2)

| Asset | Önerilen isim | Beklenen çıktı |
|---|---|---|
| Ana oda | `BG_Bedroom_Morning.png` | 16:9 statik arka plan |
| Uyuyan çocuk | `CHR_Boy_Sleep.png` | RGBA gerçek alfa |
| Saat | `PROP_Clock_0700.png` | RGBA gerçek alfa |
| Deprem bulutu | `UI_Bubble_Earthquake.png` | RGBA, okunabilir label |
| Sel bulutu | `UI_Bubble_Flood.png` | RGBA |
| Yangın bulutu | `UI_Bubble_Wildfire.png` | RGBA |
| Heyelan bulutu | `UI_Bubble_Landslide.png` | RGBA |
| Ana oyun logosu | `UI_Title.png` | RGBA |
| Menü butonları | Unity Button + görsel (ayrı state daha iyi) | Tıklanabilir Unity UI |

PNG'ler gerçek `alpha` içermeli; bazı mevcut görseller büyütülmüş pixel art/konsept olabilir. Ölçüleri uygun diye otomatik Sprite Sheet animation olarak bölünmez.

## 5. Event kontratı ve scene teslimi

- K1 menü seçiminden `LoadChapter(Flood | Earthquake)` çağırır.
- K2/K3 sonuç ekranında `ReturnToMenu()` çağırır.
- K2/K3 kendi StateController'larını içerde tutar, başkasının scene dosyasına zorla prefab yerleştirmez.
- K4 veri değişikliğini PR'la yapar; içerik güncellemesi diğer branch'leri bozmamalı.

## 6. Değişiklik talep formatı

```
CONTRACT CHANGE REQUEST
İsteyen: K__  | Dosya/API: __
Neden: __ | Eski kullanım: __ | Yeni kullanım: __
Etkilenen kişiler: __ | Test: __ | K1 onayı: __
```

**T+18'den sonra gereksiz contract değişikliği yapılmaz.** Kırıcı değişiklikse K1, K2, K3 birlikte kararlaştırır.
