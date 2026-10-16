# Unity 2D Teknik Planı ve Sahne Mimarisı

**Teknik lider:** K1 · **Sel sahnesi:** K2 · **Deprem sahnesi:** K3 · **Veri/metin:** K4

## 1. Teknoloji kararları

- **Unity 2D**, var olan projenin Unity 6 / LTS sürümünü **değiştirmeyin**.
- **C#** ve yerleşik Unity API; gerekli olmayan paket kurmayın.
- 16:9 referans `640×360`; Canvas Scaler `Scale With Screen Size`; referans çözünürlük `640×360`; ekran boyutu genişlese bile güvenli UI sınırları.
- Pixel art import: `Sprite (2D and UI)`, `Filter Mode = Point`, `Compression = None` veya uygun düşük sıkıştırma, `Mip Maps = off`, `Pixels Per Unit` tutarlı seçilmiş (ör. 32/64 PPU). *Görsel boyutlarının büyük üretilmiş olması tek başına gerçek pixel-perfect sprite sheet olduğu anlamına gelmez.*
- Varsayılan `Orthographic Camera`; Pixel Perfect Camera paketinin mevcut olup olmadığını K1 kontrol eder; yoksa paket eklemek zorunlu değil.
- Mevcut Input Manager/Input System'i bir günde değiştirmeyin; proje hangisini kullanıyorsa ona uygun tek adaptör oluşturun.
- Gizli `DontDestroyOnLoad` singleton karmaşası kurmayın; sahne geçişi ve seçilen chapter için tek küçük `GameSession` statik/state objesi olabilir.

## 2. Önerilen sahne listesi

```text
Assets/
  Scenes/
    Menu/MainMenu.unity                  # yalnız K1
    Chapters/Flood/FloodChapter.unity   # yalnız K2
    Chapters/Earthquake/EarthquakeChapter.unity # yalnız K3
  Scripts/
    Core/                               # yalnız K1
      GameSceneRouter.cs
      GameSession.cs
    Menu/                               # yalnız K1
      MainMenuController.cs
      DisasterBubbleButton.cs
      IntroSequence.cs
    Shared/                             # K1 API sahibi
      ChapterId.cs
      ResultPanel.cs
      DialoguePanel.cs
    Chapters/Flood/                     # yalnız K2
      FloodChapterController.cs
      FloodInteraction.cs
    Chapters/Earthquake/                # yalnız K3
      EarthquakeChoiceController.cs
      EarthquakeFeedback.cs
      EarthquakeCameraShake.cs
  Art/                                   # yalnız K3
    Characters/
    Backgrounds/
    Menu/
    UI/
  Animations/                            # yalnız K3
  Prefabs/Visual/                        # yalnız K3
  Data/                                  # yalnız K4
    Story/
    Settings/
  Audio/                                 # yalnız K4
```

Mevcut Unity klasör yapısı farklıysa K1 bunu belgeler ve **projeyi sırf şablona uydurmak için toplu taşımayla bozmaz**.

## 3. Minimum hiyerarşiler

**MainMenu.unity:**

```text
MainMenu
  MainCamera
  BedroomBackground (SpriteRenderer)
  SleepingBoy (SpriteRenderer / Animator veya küçük Coroutine)
  DigitalClock (SpriteRenderer)
  AmbientLightOverlay (CanvasGroup / SpriteRenderer)
  DreamBubbleContainer (4 child object)
  Canvas (Screen Space Overlay)
    GameTitle (Image veya TMP)
    NewGameButton
    SettingsButton
    ExitButton
    SettingsPanel (default inactive)
    LockedTooltip (default inactive)
    FadePanel (CanvasGroup)
  EventSystem
  MainMenuController
  SceneRouter
```

**FloodChapter.unity:**

```text
FloodChapter
  MainCamera
  StateController (F01..F07)
  World (bedroom / bathroom cue / TV / rainy street swapped)
  Player
  NPCs (Mother / Friends visual instances)
  Canvas
    DialoguePanel
    InteractionHint
    DecisionPanel (3 options)
    FeedbackPanel
    ResultPanel
```

**EarthquakeChapter.unity:**

```text
EarthquakeChapter
  MainCamera
  RoomBackground
  AdultSprite
  TableSprite
  WifeVisual
  EarthquakeChoiceController
  Canvas
    DialoguePanel
    ChoicesPanel (5 buttons)
    TimerText (5..0)
    FeedbackPanel
    ResultPanel
```

**Tercih:** Sel için 7 ayrı Unity `.unity` sahnesi yerine 1 bölüm sahnesinde 7 story state çok daha hızlıdır; ilerleme `FadePanel` + sprite/background değişimleriyle yapılabilir. K2 karar verir, K1 API'yi korur.

## 4. State machine önerisi

```text
MenuState = Intro, Choose, Settings, Transition
FloodState = Wake, Wash, News, MotherDecision, Rain, Rumors, RoadDecision, Result
EarthquakeState = Morning, Warning, TimedChoice, TremorFeedback, Result
```

State her değiştiğinde UI önce kaldırılmalı/temizlenmeli. Aynı buton listener'ı iki kez eklenmemeli. İkinci girişte timer ve seçim resetlenmeli.

## 5. Menü etkileşimleri

- İki açık afet = `Flood`, `Earthquake`; diğer iki `locked=true`.
- Dört görsel düğme sırası referans için: Deprem, Sel, Orman Yangını, Heyelan.
- `SelectChapter(ChapterId)` → id kilitliyse tooltip, açıksa vurguyu değiştir.
- `StartSelectedChapter()` → FadeOut → `SceneRouter.LoadChapter`.
- `SettingsPanel` master volume + fullscreen + back. Build'de `Application.Quit`, Editor'de uygun simülasyon/log.
- Intro ilk 5 sn; `Space/Enter` atlama; skip sonrası input bir kez işlenmeli (Enter ile hem skip hem start tetiklenmesin).

## 6. Minimum game feel

- Nefes: uyuyan çocuğun Sprite Transform Y'sinde 1 pixel ölçeğe eşdeğer hareket.
- Bulutların yüzmesi: farklı fazlarda 1–2 pixel Y, süre 2–3 sn.
- Saat: hafif alpha/glow pulse; metin `07:00` okunur.
- Yağmur: parallax/particle yerine tek tileable overlay + 1 ses loop yeterli.
- Deprem: kamera local offset sine/random küçük değerler (1–3 fizik piksel); motion sickness azaltmak için kapatılabilir opsiyon.
- Geçiş: CanvasGroup Fade. Görseli çok büyük/çok bulanık zoom yapmayın.

## 7. İçerik verisi

`data/` örnekleri JSON'dur, sadece referans olarak hazırlanmıştır. K4 metinleri değiştirir; K2/K3 bu yapıyı Unity `TextAsset`/`JsonUtility` veya mevcut içerik sistemiyle okur. `JsonUtility` dizi köklerini doğrudan parse etmez; wrapper object kullanın. Veri kökleri `chapter_id`, `scenes` gibi id'lere dayanır, görünen metne değil.

## 8. Build adımları

1. Unity Console temizlenir.
2. `MainMenu`, `FloodChapter`, `EarthquakeChapter` build listesinde olduğundan emin olun.
3. `File > Build Profiles` (veya mevcut Unity sürümünde `Build Settings`) ile `Windows x86_64`.
4. Output: `Builds/Windows/AFET_UYANISI/`; `Builds/` Git'e eklenmez. Final ZIP'e ayrıca konur.
5. Build'ı Unity Editor kapalı farklı bir Windows PC'de test etmek tercih edilir.
6. Test sonucu `qa/TEST_PLAN.md` kayıt tablosuna işlenir.

## 9. Yaşanabilecek sık hata ve önlem

| Hata | Neden | Çözüm |
|---|---|---|
| Pikseller bulanık | Bilinear + zoom | Point, scale, PPU kontrolü |
| TMP'de Türkçe karakterler kare | Font atlasında glyph yok | TR karakterleri içeren font asset |
| Ana menüde ikinci tık yanlış scene | Listener iki kez bağlandı | `onClick` bağlamasını tek yerde kur |
| Seçenekler sığmıyor | Uzun Türkçe metin | Deprem panelinde kısa label + feedback ayrımı |
| `Missing Script` | `.meta`/asset yanlış merge | Owner PR'ı geri alın, GUID'i koruyun |
| 5 saniye sayaç birden fazla başlıyor | Coroutine resetlenmedi | Sadece `StartChoice()`'de başlat, state guard |
| Ana menü değişti ama Sel kayboldu | K2 sahnesine rastgele edit | Owner matrix uygulayın |

## 10. Acceptance

K1, K2, K3 her biri kendi scene'inin Play Mode içinde en az bir tam çevrimini test eder; K4 bağımsız QA yapar. Editör/batch test yapmadan `PASS` yazmak yasak.
