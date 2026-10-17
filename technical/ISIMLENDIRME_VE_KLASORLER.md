# İsimlendirme ve Klasör Yapısı (bağlayıcı)

> Herkes ve her Claude bu kurallara uyar. Dosya ve sahne **sahipliği** için [`planning/EKIP_ROLLERI.md`](../planning/EKIP_ROLLERI.md), oyunun **akışı** için [`docs/OYUN_AKISI.md`](../docs/OYUN_AKISI.md) geçerlidir. Bu belge `technical/UNITY_PLAN.md` içindeki klasör önerisinin yerine geçer.

## 1. `Assets/` klasör ağacı

```text
Assets/
├─ Scenes/
│  ├─ Menu/MainMenu.unity                          Berk
│  ├─ Chapters/Flood/FloodChapter.unity            Mahmut
│  ├─ Chapters/Earthquake/EarthquakeChapter.unity  Mahmut
│  └─ Test/                                        herkes kendi Test_<İsim>_<Konu>.unity sahnesi (build'e girmez)
├─ Scripts/
│  ├─ Core/          GameSceneRouter, GameSession, GameInput                     Mahmut
│  ├─ Shared/        ChapterId, StoryData                                        Mahmut (sözleşme)
│  │  └─ Contracts/  *Base soyut sınıfları (herkesin bağlandığı arayüzler)       Mahmut (sözleşme)
│  ├─ Menu/          ana menü kodu                                               Berk
│  ├─ UI/            paneller: DialoguePanel, DecisionPanel, ResultPanel...      Berk
│  ├─ Dialogue/      DialogueRunner, DecisionRunner                              Arda
│  ├─ News/          TV haberi, kartlar                                          Eren
│  └─ Chapters/
│     ├─ Common/     ChapterControllerBase (iki bölümün ortak akışı)             Mahmut
│     ├─ Flood/      FloodChapterController                                      Mahmut
│     └─ Earthquake/ EarthquakeChapterController, EarthquakeCameraShake          Mahmut
├─ Art/
│  ├─ Characters/    CHR_*      Mahmut
│  ├─ Backgrounds/   BG_*       Mahmut
│  ├─ Props/         PROP_*     Mahmut
│  ├─ UI/            UI_*       Berk
│  ├─ Menu/          MENU_*     Berk
│  └─ News/          NEWS_*     Eren
├─ Animations/Characters/        ANIM_*, AC_*   Mahmut
├─ Prefabs/
│  ├─ UI/            Berk
│  ├─ Dialogue/      Arda
│  ├─ News/          Eren
│  ├─ Characters/    Mahmut
│  └─ Props/         Mahmut
├─ Data/
│  ├─ Story/         flood.json, earthquake.json             Arda
│  └─ News/          flood_news.json, earthquake_news.json   Eren
├─ Audio/
│  ├─ Music/ MUS_*   ├─ SFX/ SFX_*   └─ Ambience/ AMB_*      Mahmut
├─ Fonts/            Berk
├─ Settings/         URP ve Input ayarları — dokunma (Mahmut)
└─ Welcome/          Unity şablonundan kaldı, kullanılmıyor
```

Boş klasörlerde duran `.gitkeep` dosyaları klasörün git'te görünmesi içindir, silmeyin. Klasörün içine gerçek dosya girince kalabilirler.

## 2. Dosya adları

**Genel kural:** dosya adlarında **sadece İngilizce harf, rakam ve `_`** kullanılır. Türkçe karakter (ç, ğ, ı, İ, ö, ş, ü), boşluk ve parantez kullanılmaz. Türkçe metinler veri dosyalarının ve sahnedeki yazıların **içinde** olur, dosya adında olmaz.

| Tür | Biçim | Örnek |
|---|---|---|
| Arka plan | `BG_<Mekan>_<Varyant>.png` | `BG_Kitchen_Morning.png`, `BG_Street_Rain.png` |
| Karakter sprite | `CHR_<Karakter>_<Hareket>_<Yön>.png` | `CHR_Boy_Walk_East.png`, `CHR_Mother_Walk_West.png` |
| Obje | `PROP_<Ad>.png` | `PROP_Table.png`, `PROP_EmergencyBag.png` |
| Arayüz görseli | `UI_<Ad>.png` | `UI_Panel_Dialogue.png`, `UI_Icon_Lock.png` |
| Menü görseli | `MENU_<Ad>.png` | `MENU_Bubble_Flood.png`, `MENU_Title.png` |
| Haber görseli | `NEWS_<Ad>.png` | `NEWS_Tv_Frame.png`, `NEWS_Card_Rumor.png` |
| Efekt | `FX_<Ad>.png` | `FX_Rain_Overlay.png` |
| Animasyon klibi | `ANIM_<Karakter>_<Hareket>_<Yön>.anim` | `ANIM_Boy_Walk_East.anim` |
| Animator | `AC_<Karakter>.controller` | `AC_Boy.controller` |
| Prefab | `PascalCase.prefab` (klasörü türünü söyler) | `Prefabs/UI/DialoguePanel.prefab`, `Prefabs/Characters/Boy.prefab` |
| Ses efekti / ortam / müzik | `SFX_*`, `AMB_*`, `MUS_*` | `SFX_AlarmClock.wav`, `AMB_Rain_Loop.wav`, `MUS_Menu.ogg` |
| Sahne | `PascalCase.unity` | `FloodChapter.unity` |
| Test sahnesi | `Test_<İsim>_<Konu>.unity` | `Test_Arda_Dialogue.unity` |
| C# dosyası | `PascalCase.cs`, **dosya adı = sınıf adı** | `DialogueRunner.cs` |
| Veri dosyası | `kucuk_harf.json` | `flood.json`, `earthquake_news.json` |

**Repodaki hazır görseller:** `visuals/` klasörü referanstır, Unity'ye kendiliğinden girmez. Oyunda kullanılacak görseli `Assets/Art/...` altına **sahibi** kopyalar ve kopyalarken yeniden adlandırır. Örneğin `visuals/characters/spritesheets/arda_walk_east.png` → `Assets/Art/Characters/CHR_Boy_Walk_East.png`. Aynı PNG'yi iki kişi ayrı ayrı import ederse farklı GUID oluşur ve referanslar kırılır.

## 3. Veri içindeki ID'ler

`BÜYÜK_HARF_VE_ALT_ÇİZGİ`. Kod ID'lere bakar, görünen metne bakmaz, bu yüzden metin değişebilir ama ID değişmez. Tam liste [`docs/OYUN_AKISI.md` §9](../docs/OYUN_AKISI.md#9-id-sözleşmesi-kod-bu-idlere-bakar-metinlere-değil)'da.

- Bölüm adımı / diyalog sahnesi: `F03_NEWS`, `E06_AFTER`
- Karar: `F_D2`, seçenek: `F_D2_C`
- Haber: `NEWS_F03_TV`

## 4. C# kuralları

- **Namespace:** `AfetUyanisi.<Alan>` → `AfetUyanisi.Core`, `AfetUyanisi.Shared`, `AfetUyanisi.Menu`, `AfetUyanisi.UI`, `AfetUyanisi.Dialogue`, `AfetUyanisi.News`, `AfetUyanisi.Chapters`, `AfetUyanisi.Chapters.Flood`, `AfetUyanisi.Chapters.Earthquake`.
- Her dosyada bir `MonoBehaviour` olur ve dosya adı sınıf adıyla aynıdır. Aksi halde Unity bileşeni ekleyemez.
- Kod içindeki isimler İngilizce ve ASCII olur. Yorumlar Türkçe olabilir.
- Inspector'a açılacak alanlar `[SerializeField] private` + camelCase ile yazılır (`[SerializeField] private float holdSeconds = 4f;`). Public özellikler PascalCase, olaylar `On...`/`...Clicked` biçimindedir.
- **Tuş okuma sadece `AfetUyanisi.Core.GameInput` ile yapılır.** Proje yeni Input System'i kullanır, `Input.GetKeyDown` gibi eski çağrılar **hata verir**.
- **Sahne geçişi sadece `GameSceneRouter`** ile yapılır, `SceneManager.LoadScene` başka yerde çağrılmaz.
- Başkasının sınıfına doğrudan bağımlı olunmaz. Bağlantı `Shared/Contracts` altındaki `*Base` sınıflarıyla, Inspector'dan yapılır (ör. `[SerializeField] private DialogueViewBase view;`).
- Diyalog, karar, haber ve sonuç metinleri koda gömülmez, veri dosyalarından gelir. Kısa etkileşim ipuçları (`[E] Kalk` gibi) bölüm kodunda yazılabilir, debug logları da istisnadır.
- `Update` içinde `Find...`/`GetComponent` çağrılmaz, referanslar `Awake`'te alınır ya da Inspector'dan bağlanır.
- **Static değişkenlere dikkat:** static durum (event, liste) tutan sınıf `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]` ile sıfırlanır (örnek: `GameSession`).
- Assembly definition (`.asmdef`) kullanılmıyor, bütün kod tek derlemede (Assembly-CSharp).

## 5. Bölüm sahnesi içindeki düzen (Mahmut)

```text
FloodChapter
├─ Main Camera              (ortografik, 2D URP)
├─ Global Light 2D
├─ ChapterController        FloodChapterController — akışın tamamı burada
├─ World
│  ├─ F01_WAKE              adım görselleri: o adıma girilince bu obje açılır, diğerleri kapanır
│  ├─ F02_WASH
│  └─ ...                   (adımı olmayan bir obje varsa önceki görsel açık kalır)
├─ Characters               çocuk, anne, arkadaşlar
├─ Systems                  DialogueRunner (Arda), DecisionRunner (Arda), NewsBroadcast (Eren)
└─ UI                       Berk'in panel prefabları (Canvas + EventSystem)
```

Bölüm adımları tek sahnede durumlar halinde akar, her adım ayrı bir Unity sahnesi değildir. `ChapterController` üzerindeki **Debug Auto Advance** kutusu işaretlenirse akış tuş beklemeden baştan sona akar, adımlar Console'a süreleriyle birlikte yazılır.

## 6. Hiyerarşi ve prefab

- Sahnedeki objeler PascalCase isimlendirilir, kökte `World`, `Characters`, `Systems`, `UI` grupları bulunur.
- Başkasının sahnesine bir şey eklemek gerekiyorsa **prefab** olarak teslim edilir, sahneye sahibi yerleştirir.
- Prefab'ın içini sahibi değiştirir. Sahnedeki kopyasında yapılan değişiklikler (override) prefab'a geri yazılmaz (Apply yapılmaz).

## 7. Git

- Dal: `feature/<isim>-<iş>` (ör. `feature/berk-menu`). Kurallar `CLAUDE.md`'de.
- Commit mesajı kısa ve Türkçe olur: `Ana menü bulutları eklendi`.
- Her asset `.meta` dosyasıyla birlikte commit edilir. `Library/`, `Temp/`, `Logs/`, `UserSettings/` asla commit edilmez.
