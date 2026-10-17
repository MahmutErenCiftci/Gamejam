using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

namespace AfetUyanisi.Menu.EditorTools
{
    // Assets/Scenes/Menu/MainMenu.unity sahnesini kurar. Menünün arka planı oyunun kendi izometrik yatak odasıdır:
    // Assets/Scenes/Rooms/Bedroom.unity (Mahmut) sahnesinden duvarlar, zemin ve eşyalar kopyalanır (dosyalar
    // kopyalanmaz, aynı sprite ve tile'lar kullanılır; oda değişirse menüyü yeniden kurmak yeter). Odanın üstüne
    // şafak gökyüzü, yıldızlar, uyuyan çocuğun "z" harfleri, rüya bulutları, düşünce yolları ve piksel fontlu arayüz
    // eklenir. Çalıştırıldığında MainMenu sahnesinin üzerine yazar (sahnede elle yapılan değişiklikler kaybolur).
    //
    // Konumlar 640x360'lık sanal ekranda, sol alt köşeden piksel cinsindendir (1 birim = 64 piksel, odanın
    // sanatıyla aynı yoğunluk; 1080p'de tam 3 kat büyür). Sıralama tek "Default" sorting layer'ında:
    //   -200 gökyüzü · -190 yıldızlar · -150 fırtına yağmuru (odanın arkasında)
    //   odanın kendi sıraları (-90 halı … 0-1 eşyalar) · pencere fırtınası pencerenin
    //   hemen üstünde · 60 "z" harfleri · 80-81 oda ışığı · 95 seçim karartması · 99 düşünce yolları
    //   100-109 bulutlar · 120 logo · 900 karanlık · 958-960 açılış saati
    public static class MenuSceneBuilder
    {
        const string ScenePath = "Assets/Scenes/Menu/MainMenu.unity";
        const string RoomScene = "Assets/Scenes/Rooms/Bedroom.unity";
        const string Art = "Assets/Art/Menu/";
        const string RoomArt = "Assets/Art/Props/Bedroom/";
        const float PPU = 64f;
        const int ScreenW = 640, ScreenH = 360;

        // Oda: Bedroom sahnesindeki dünya koordinatları bu kadar kaydırılır; oda ekranda x 199-614, y 20-321
        // arasına gelir, solda başlık ve butonlara yer kalır.
        static readonly Vector3 RoomOffset = new Vector3(71f / PPU, -162f / PPU, 0f);

        static readonly Color Ink = new Color(0.95f, 0.92f, 0.85f);
        static readonly Color Warm = new Color(1f, 0.74f, 0.4f);
        static readonly Color Muted = new Color(0.7f, 0.7f, 0.86f);

        static Material unlit;
        static Font regular, bold;

        static Vector3 P(float x, float y) => new Vector3((x - ScreenW / 2f) / PPU, (y - ScreenH / 2f) / PPU, 0f);
        static Vector2 V(float x, float y) => new Vector2(x - ScreenW / 2f, y - ScreenH / 2f); // UI: ekran ortasına göre
        static Sprite S(string path) => AssetDatabase.LoadAssetAtPath<Sprite>(Art + path + ".png");

        [MenuItem("AfetUyanisi/Menü/Sahneyi Kur (MainMenu.unity üzerine yazar)")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[MenuSceneBuilder] Play modundayken sahne kurulamaz; önce Play'den çıkın.");
                return;
            }
            if (!File.Exists(RoomScene))
            {
                Debug.LogError("[MenuSceneBuilder] Oda sahnesi yok: " + RoomScene);
                return;
            }
            unlit = AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat");
            regular = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/PixelifySans/PixelifySans-Regular.ttf");
            bold = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/PixelifySans/PixelifySans-Bold.ttf");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // ---------------- Kamera
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cameraRig = new GameObject("CameraRig").transform;
            camGo.transform.SetParent(cameraRig, false);
            camGo.transform.localPosition = new Vector3(0, 0, -10);
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = ScreenH / 2f / PPU;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.04f, 0.05f, 0.11f);
            var ppc = camGo.AddComponent<PixelPerfectCamera>();
            ppc.assetsPPU = (int)PPU;
            ppc.refResolutionX = ScreenW;
            ppc.refResolutionY = ScreenH;
            ppc.gridSnapping = PixelPerfectCamera.GridSnapping.UpscaleRenderTexture;
            ppc.cropFrame = PixelPerfectCamera.CropFrame.StretchFill;
            camGo.AddComponent<Physics2DRaycaster>();
            camGo.AddComponent<AudioListener>();
            var ambience = camGo.AddComponent<AudioSource>();
            ambience.playOnAwake = false;
            camGo.AddComponent<AmbientLoop>();

            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();

            // ---------------- Gökyüzü ve yıldızlar
            var world = Group(null, "World");
            Stretched(world, "Sky", "Fx/Sky", -16, -16, ScreenW + 32, ScreenH + 32, -200);
            Stars(world);

            // ---------------- Oda (Bedroom.unity'den)
            var room = Group(world, "Room");
            room.position = RoomOffset;
            var parts = CopyRoom(room);
            SpriteRenderer bed = parts["Bed"];
            bed.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(RoomArt + "PROP_Bedroom_Bed_Sleeping.png");
            SpriteRenderer clock = parts["AlarmClock"];
            SpriteRenderer window = parts["Window"];

            // Uyuyan çocuğun başı (yatak sprite'ının pivotuna göre, piksel): baş ortası (-20, 41), saçın üstü 46.
            Vector3 bedPos = bed.transform.position;
            var zzz = new GameObject("SleepZzz").AddComponent<SleepZzz>();
            zzz.transform.SetParent(bed.transform, false);
            zzz.transform.position = bedPos + new Vector3(-10f, 48f, 0f) / PPU;
            SetArray(zzz, "letters", S("Fx/ZSmall"), S("Fx/ZSmall"), S("Fx/ZBig"));
            SetRef(zzz, "material", unlit);

            // ---------------- Oda ışığı (fırtınada kararır, şimşekte parlar)
            var light = Group(world, "RoomLight");
            var roomTint = Fill(light, "RoomTint", -16, -16, ScreenW + 32, ScreenH + 32, new Color(0.08f, 0.12f, 0.3f, 0f), 80);
            var roomFlash = Fill(light, "RoomFlash", -16, -16, ScreenW + 32, ScreenH + 32, new Color(0.85f, 0.9f, 1f, 0f), 81);
            var windowController = BuildWeather(world, window, roomTint, roomFlash);

            // ---------------- Rüya seçimi
            var dreams = Group(null, "DreamSelection");
            var dim = Fill(dreams, "SelectionDim", -16, -16, ScreenW + 32, ScreenH + 32, new Color(0.03f, 0.04f, 0.1f, 0.62f), 95);
            var dimFader = dim.gameObject.AddComponent<SpriteFader>();
            SetFloat(dimFader, "alpha", 0f);
            var thought = Group(dreams, "ThoughtTrail");
            // Çocuğun başının üstünden iki ayrı yol: sol-yukarı DEPREM'e, sağ-yukarı SEL'e (konum = yolun başa en
            // yakın baloncuğu). Uçları bulutların alt kenarının birkaç piksel altına girer (bulut üstte çizilir).
            Vector3 headTop = bedPos + new Vector3(-20f, 46f, 0f) / PPU;
            var quakePath = Place(thought, "ThoughtToQuake", "UI/ThoughtPathQuake", 0, 0, 99);
            quakePath.transform.position = headTop + new Vector3(-6f, 6f, 0f) / PPU;
            var floodPath = Place(thought, "ThoughtToFlood", "UI/ThoughtPathFlood", 0, 0, 99);
            floodPath.transform.position = headTop + new Vector3(6f, 6f, 0f) / PPU;
            var trail = thought.gameObject.AddComponent<ThoughtTrail>();
            SetArray(trail, "dots", quakePath, floodPath);
            var quake = Bubble(dreams, "Bubble_Deprem", MenuChapterLoader.Chapter.Earthquake, "Bubbles/BubbleQuake", "Bubbles/BubbleQuakeGlow", 184, 254, 100, false, 0f);
            var flood = Bubble(dreams, "Bubble_Sel", MenuChapterLoader.Chapter.Flood, "Bubbles/BubbleFlood", "Bubbles/BubbleFloodGlow", 342, 254, 105, true, 0.5f);

            // ---------------- Logo, karanlık, açılış saati
            var titleRenderer = Place(null, "Title", "UI/LogoStacked", 120, 290, 120);
            var title = titleRenderer.gameObject.AddComponent<SpriteFader>();
            var darkness = Fill(null, "Darkness", -16, -16, ScreenW + 32, ScreenH + 32, new Color(0.01f, 0.01f, 0.03f, 1f), 900);
            var darkFader = darkness.gameObject.AddComponent<SpriteFader>();
            // Karanlıkta yalnızca komodindeki saat parlar: odadaki saatin kopyası karanlığın üstünde, arkasında kırmızı hale.
            var introClock = Group(null, "IntroClock");
            introClock.position = clock.transform.position;
            Place(introClock, "Glow", "Fx/ClockGlow", 0, 8, 958, local: true);
            var introClockFace = Place(introClock, "Clock", "Fx/Pixel", 0, 0, 960, local: true);
            introClockFace.sprite = clock.sprite;
            var introClockFader = introClock.gameObject.AddComponent<SpriteFader>();

            // ---------------- Arayüz
            var canvas = BuildCanvas(out RectTransform root);
            var mainButtons = Panel(root, "MainButtons");
            Label(mainButtons.transform, "Slogan", "Küçük kararlar,\nbüyük fark yaratır.", regular, 11, Muted, V(120, 232), new Vector2(200, 28), TextAnchor.MiddleCenter);
            var newGame = MenuButton(mainButtons.transform, "NewGameButton", "Yeni Oyun", V(120, 186), new Vector2(128, 22));
            var settings = MenuButton(mainButtons.transform, "SettingsButton", "Ayarlar", V(120, 158), new Vector2(128, 22));
            var exit = MenuButton(mainButtons.transform, "ExitButton", "Çıkış", V(120, 130), new Vector2(128, 22));
            ChainNavigation(newGame, settings, exit);

            var selection = Panel(root, "SelectionPanel");
            Label(selection.transform, "Header", "BİR RÜYA SEÇ", bold, 16, Warm, V(320, 340), new Vector2(300, 20), TextAnchor.MiddleCenter);
            Label(selection.transform, "Hint", "A / D ya da fare ile seç  ·  Enter: başla  ·  Esc: geri", regular, 10, Muted, V(320, 324), new Vector2(400, 14), TextAnchor.MiddleCenter);
            var infoPanel = UiImage(selection.transform, "InfoPanel", S("UI/Panel"), V(320, 52), new Vector2(400, 24));
            infoPanel.type = UnityEngine.UI.Image.Type.Sliced;
            var info = Label(infoPanel.transform, "Info", "", regular, 11, Ink, Vector2.zero, new Vector2(390, 20), TextAnchor.MiddleCenter);
            info.supportRichText = true;
            var back = MenuButton(selection.transform, "BackButton", "Geri", V(252, 20), new Vector2(104, 22));
            var startChapter = MenuButton(selection.transform, "StartChapterButton", "Bölümü Başlat", V(388, 20), new Vector2(150, 22));

            var settingsPanel = UiImage(root, "SettingsPanel", S("UI/Panel"), V(320, 180), new Vector2(260, 120));
            settingsPanel.type = UnityEngine.UI.Image.Type.Sliced;
            Label(settingsPanel.transform, "Title", "Ayarlar", bold, 16, Warm, new Vector2(0, 36), new Vector2(240, 20), TextAnchor.MiddleCenter);
            Label(settingsPanel.transform, "Info", "Ses ve tam ekran ayarları yakında.\nEsc: geri", regular, 11, Ink, new Vector2(0, 4), new Vector2(240, 30), TextAnchor.MiddleCenter);
            var settingsBack = MenuButton(settingsPanel.transform, "BackButton", "Geri", new Vector2(0, -36), new Vector2(104, 22));
            settingsPanel.gameObject.SetActive(false);

            var tooltip = UiImage(root, "Tooltip", S("UI/Panel"), V(320, 86), new Vector2(320, 22));
            tooltip.type = UnityEngine.UI.Image.Type.Sliced;
            var tooltipText = Label(tooltip.transform, "Text", "", regular, 11, Ink, Vector2.zero, new Vector2(310, 18), TextAnchor.MiddleCenter);
            tooltip.gameObject.SetActive(false);

            var fade = UiImage(root, "FadePanel", null, Vector2.zero, Vector2.zero);
            Stretch(fade.rectTransform);
            fade.color = Color.black;
            fade.raycastTarget = true;
            var fadeGroup = fade.gameObject.AddComponent<CanvasGroup>();
            fadeGroup.alpha = 0f;
            fadeGroup.blocksRaycasts = false;

            // ---------------- Kontrolcüler
            var controllerGo = new GameObject("MenuController");
            var menu = controllerGo.AddComponent<MainMenuController>();
            SetRef(menu, "title", title);
            SetRef(menu, "mainButtons", mainButtons);
            SetRef(menu, "newGameButton", newGame);
            SetRef(menu, "settingsButton", settings);
            SetRef(menu, "exitButton", exit);
            SetArray(menu, "bubbles", quake, flood);
            SetRef(menu, "thoughtTrail", trail);
            SetRef(menu, "selectionPanel", selection);
            SetRef(menu, "startChapterButton", startChapter);
            SetRef(menu, "backButton", back);
            SetRef(menu, "selectionDim", dimFader);
            SetRef(menu, "selectionInfo", info);
            SetRef(menu, "sleepZzz", zzz);
            SetStrings(menu, "bubbleInfo", "Sabah evde, yer birden sarsılmaya başlıyor.", "Yağmur dinmiyor, sokaklar suyla doluyor.");
            SetRef(menu, "settingsPanel", settingsPanel.gameObject);
            SetRef(menu, "settingsBackButton", settingsBack);
            SetRef(menu, "tooltip", tooltip.gameObject);
            SetRef(menu, "tooltipText", tooltipText);
            SetRef(menu, "fadePanel", fadeGroup);

            var intro = controllerGo.AddComponent<MenuIntroController>();
            SetRef(intro, "menu", menu);
            var so = new SerializedObject(intro);
            var steps = so.FindProperty("steps");
            var defs = new (Object target, float start, float duration, bool fadeOut)[]
            {
                (darkFader, 1.0f, 1.2f, true),
                (introClockFader, 1.4f, 0.6f, true),
                (title, 2.4f, 0.6f, false),
                (mainButtons, 3.0f, 0.6f, false),
            };
            steps.arraySize = defs.Length;
            for (int i = 0; i < defs.Length; i++)
            {
                var e = steps.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("group").objectReferenceValue = defs[i].target as CanvasGroup;
                e.FindPropertyRelative("sprites").objectReferenceValue = defs[i].target as SpriteFader;
                e.FindPropertyRelative("startTime").floatValue = defs[i].start;
                e.FindPropertyRelative("duration").floatValue = defs[i].duration;
                e.FindPropertyRelative("fadeOut").boolValue = defs[i].fadeOut;
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            var quakeGo = new GameObject("EarthquakePreview");
            quakeGo.transform.SetParent(controllerGo.transform, false);
            var rumble = quakeGo.AddComponent<AudioSource>();
            rumble.playOnAwake = false;
            var earthquake = quakeGo.AddComponent<EarthquakePreview>();
            SetRef(earthquake, "cameraTransform", camGo.transform);
            SetRef(earthquake, "rumbleSource", rumble);
            var props = new List<(string name, EarthquakePreview.Reaction reaction, float strength)>
            {
                ("Bed", EarthquakePreview.Reaction.Jitter, 1f),
                ("Nightstand", EarthquakePreview.Reaction.Jitter, 1f),
                ("AlarmClock", EarthquakePreview.Reaction.Jitter, 1f),
                ("Desk", EarthquakePreview.Reaction.Jitter, 1f),
                ("Chair", EarthquakePreview.Reaction.Jitter, 2f),
                ("Bookshelf", EarthquakePreview.Reaction.Jitter, 1.5f),
                ("Backpack", EarthquakePreview.Reaction.Tilt, 4f),
                ("Ball", EarthquakePreview.Reaction.Jitter, 2f),
                ("Slippers", EarthquakePreview.Reaction.Jitter, 1f),
                ("Plant", EarthquakePreview.Reaction.Tilt, 5f),
                ("Umbrella", EarthquakePreview.Reaction.Tilt, 4f),
                ("Poster", EarthquakePreview.Reaction.Tilt, 3f),
                ("Frame", EarthquakePreview.Reaction.Tilt, 3f),
            };
            props.RemoveAll(p => !parts.ContainsKey(p.name));
            var eso = new SerializedObject(earthquake);
            var parr = eso.FindProperty("props");
            parr.arraySize = props.Count;
            for (int i = 0; i < props.Count; i++)
            {
                var e = parr.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("target").objectReferenceValue = parts[props[i].name].transform;
                e.FindPropertyRelative("reaction").enumValueIndex = (int)props[i].reaction;
                e.FindPropertyRelative("strength").floatValue = props[i].strength;
            }
            eso.ApplyModifiedPropertiesWithoutUndo();

            var effects = controllerGo.AddComponent<MenuEffectsController>();
            SetRef(effects, "menu", menu);
            SetRef(effects, "window", windowController);
            SetRef(effects, "earthquake", earthquake);

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log("[MenuSceneBuilder] MainMenu kuruldu: " + ScenePath);
        }

        // ---------------------------------------------------------------- oda
        // Bedroom sahnesini kaydetmeden açar, duvarları, zemini ve eşyaları menü sahnesine kopyalar, kapatır.
        // Kopyalarda yalnızca görüntü kalır (oyun scriptleri, çarpışma kutuları, ışıklar atılır); hepsi ışıktan
        // etkilenmeyen malzemeyle çizilir, oda menüde kendi renkleriyle görünür. Dönüş: isim → sprite.
        static Dictionary<string, SpriteRenderer> CopyRoom(Transform parent)
        {
            var active = EditorSceneManager.GetActiveScene();
            var source = EditorSceneManager.OpenScene(RoomScene, OpenSceneMode.Additive);
            EditorSceneManager.SetActiveScene(active);
            var wanted = new[] { "Environment/Walls", "Environment/Rug", "Grid", "Furniture" };
            foreach (GameObject rootGo in source.GetRootGameObjects())
                foreach (string path in wanted)
                {
                    string[] names = path.Split('/');
                    if (rootGo.name != names[0]) continue;
                    Transform t = names.Length == 1 ? rootGo.transform : rootGo.transform.Find(names[1]);
                    if (t == null) continue;
                    var copy = Object.Instantiate(t.gameObject);
                    copy.name = t.name;
                    copy.transform.SetParent(parent, false);
                    copy.transform.localPosition = t.position;
                    copy.transform.localRotation = t.rotation;
                }
            EditorSceneManager.CloseScene(source, true);

            // Yalnızca görüntü bileşenleri kalır: önce scriptler, sonra çarpışma, en son geri kalanlar (fizik vb.).
            var keep = new HashSet<System.Type> { typeof(Transform), typeof(SpriteRenderer), typeof(Grid), typeof(Tilemap), typeof(TilemapRenderer), typeof(UnityEngine.Rendering.SortingGroup) };
            for (int pass = 0; pass < 3; pass++)
                foreach (var c in parent.GetComponentsInChildren<Component>(true))
                {
                    if (c == null || keep.Contains(c.GetType())) continue;
                    if (pass == 0 && !(c is MonoBehaviour)) continue;
                    if (pass == 1 && !(c is Collider2D)) continue;
                    Object.DestroyImmediate(c);
                }
            var parts = new Dictionary<string, SpriteRenderer>();
            foreach (var r in parent.GetComponentsInChildren<Renderer>(true))
            {
                r.sharedMaterial = unlit;
                if (r is SpriteRenderer sr && !parts.ContainsKey(sr.name)) parts[sr.name] = sr;
            }
            return parts;
        }

        static void Stars(Transform parent)
        {
            var go = new GameObject("Stars");
            go.transform.SetParent(parent, false);
            go.transform.position = P(ScreenW / 2f, ScreenH / 2f);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.prewarm = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(3f, 6f);
            main.startSpeed = 0f;
            main.startSize = 1f / PPU;
            main.maxParticles = 70;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 1f, 1f, 1f), new Color(0.75f, 0.82f, 1f, 1f));
            var emission = ps.emission;
            emission.rateOverTime = 12f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(ScreenW / PPU, ScreenH / PPU, 0.1f);
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.9f, 0.3f), new GradientAlphaKey(0.9f, 0.7f), new GradientAlphaKey(0f, 1f) });
            col.color = gradient;
            var sheet = ps.textureSheetAnimation;
            sheet.enabled = true;
            sheet.mode = ParticleSystemAnimationMode.Sprites;
            if (sheet.spriteCount == 0) sheet.AddSprite(S("Fx/Pixel")); else sheet.SetSprite(0, S("Fx/Pixel"));
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = unlit;
            renderer.sortingOrder = -190;
        }

        // Sel önizlemesindeki fırtına. Pencerenin camında: gün batımı fırtına tonlarına döner, yağmur yalnızca camın
        // içine düşer, şimşekte cam parlar (SortingGroup pencerenin hemen üstünde, eşyaların altında; maske sadece bu
        // grubun içini etkiler). Ayrıca havada duran odanın etrafındaki boşlukta, odanın arkasında yağmur yağar; bulut
        // pencerenin önüne geldiğinde de fırtına hemen görünür. Kontrolcü hep açık kalan Weather'da, fırtına parçaları
        // açılıp kapanan Storm'un altında.
        static DynamicWindowController BuildWeather(Transform world, SpriteRenderer window, SpriteRenderer roomTint, SpriteRenderer roomFlash)
        {
            var weather = Group(world, "Weather");
            var storm = Group(weather, "Storm");
            SkyRain(storm);

            var root = Group(storm, "WindowStorm");
            root.position = window.transform.position;
            root.gameObject.AddComponent<UnityEngine.Rendering.SortingGroup>().sortingOrder = window.sortingOrder + 1;
            var glass = S("Fx/WindowGlass");
            var mask = new GameObject("GlassMask", typeof(SpriteMask)).GetComponent<SpriteMask>();
            mask.transform.SetParent(root, false);
            mask.sprite = glass;
            mask.alphaCutoff = 0.5f;

            var stormGlass = Place(root, "StormGlass", "Fx/WindowStorm", 0, 0, 1, local: true);
            SetAlpha(stormGlass, 0f);
            Rect bounds = GlassBounds(glass);
            var rain = new GameObject("Rain");
            rain.transform.SetParent(root, false);
            rain.transform.localPosition = new Vector3(bounds.center.x + 3f, bounds.yMax + 4f, 0f) / PPU;
            var ps = rain.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = true;
            main.startLifetime = (bounds.height + 8f) / PPU / 2.4f;
            main.startSpeed = 0f;
            main.startSize = 1f / PPU; // sprite modunda boyut genişliğe uygulanır: 1 px genişlik → 1x4 px damla
            main.maxParticles = 60;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.6f);
            velocity.y = new ParticleSystem.MinMaxCurve(-2.4f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f);
            var emission = ps.emission;
            emission.rateOverTime = 40f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3((bounds.width + 12f) / PPU, 0.1f, 0.1f);
            var sheet = ps.textureSheetAnimation;
            sheet.enabled = true;
            sheet.mode = ParticleSystemAnimationMode.Sprites;
            if (sheet.spriteCount == 0) sheet.AddSprite(S("Fx/RainDrop")); else sheet.SetSprite(0, S("Fx/RainDrop"));
            var renderer = rain.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = unlit;
            renderer.sortingOrder = 2;
            renderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
            var flash = Place(root, "GlassFlash", "Fx/WindowGlass", 0, 0, 3, local: true);
            flash.color = new Color(0.9f, 0.93f, 1f, 0f);

            var rainAudio = weather.gameObject.AddComponent<AudioSource>();
            rainAudio.playOnAwake = false;
            var thunderAudio = weather.gameObject.AddComponent<AudioSource>();
            thunderAudio.playOnAwake = false;

            var controller = weather.gameObject.AddComponent<DynamicWindowController>();
            SetArray(controller, "clearLayers");
            SetArray(controller, "stormLayers", stormGlass);
            SetRef(controller, "stormRoot", storm.gameObject);
            SetRef(controller, "rain", ps);
            SetArray(controller, "driftingClouds");
            SetRef(controller, "windowFlash", flash);
            SetRef(controller, "roomTint", roomTint);
            SetRef(controller, "roomFlash", roomFlash);
            SetRef(controller, "rainAudio", rainAudio);
            SetRef(controller, "thunderAudio", thunderAudio);
            var so = new SerializedObject(controller);
            so.FindProperty("clearTint").colorValue = new Color(0.08f, 0.12f, 0.3f, 0f);
            so.ApplyModifiedPropertiesWithoutUndo();
            return controller;
        }

        // Odanın arkasında, bütün ekrana yağan eğik yağmur. Fırtına açılınca (Storm etkinleşince) kendiliğinden başlar,
        // önceden ısıtıldığı için ekran ilk anda dolu görünür.
        static void SkyRain(Transform parent)
        {
            var go = new GameObject("SkyRain");
            go.transform.SetParent(parent, false);
            go.transform.position = P(ScreenW / 2f + 30f, ScreenH + 8f);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = true;
            main.loop = true;
            main.prewarm = true;
            main.startLifetime = 2f;
            main.startSpeed = 0f;
            main.startSize = 1f / PPU;
            main.maxParticles = 260;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.7f, 0.8f, 1f, 0.55f), new Color(0.75f, 0.85f, 1f, 0.9f));
            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.6f);
            velocity.y = new ParticleSystem.MinMaxCurve(-3.3f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f);
            var emission = ps.emission;
            emission.rateOverTime = 110f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3((ScreenW + 80f) / PPU, 0.1f, 0.1f);
            var sheet = ps.textureSheetAnimation;
            sheet.enabled = true;
            sheet.mode = ParticleSystemAnimationMode.Sprites;
            if (sheet.spriteCount == 0) sheet.AddSprite(S("Fx/RainDrop")); else sheet.SetSprite(0, S("Fx/RainDrop"));
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = unlit;
            renderer.sortingOrder = -150;
        }

        // Cam maskesinin opak piksellerinin kutusu, sprite pivotuna göre piksel.
        static Rect GlassBounds(Sprite sprite)
        {
            var copy = new Texture2D(2, 2);
            copy.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(sprite)));
            int w = copy.width, h = copy.height;
            Color32[] px = copy.GetPixels32();
            Object.DestroyImmediate(copy);
            int x0 = w, x1 = -1, y0 = h, y1 = -1;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    if (px[y * w + x].a > 0) { x0 = Mathf.Min(x0, x); x1 = Mathf.Max(x1, x); y0 = Mathf.Min(y0, y); y1 = Mathf.Max(y1, y); }
            Vector2 pivot = sprite.pivot;
            return x1 < 0 ? new Rect(0, 0, 1, 1) : Rect.MinMaxRect(x0 - pivot.x, y0 - pivot.y, x1 + 1 - pivot.x, y1 + 1 - pivot.y);
        }

        // ---------------------------------------------------------------- bulut
        static DisasterBubble Bubble(Transform parent, string name, MenuChapterLoader.Chapter chapter, string artPath, string glowPath, float x, float y, int baseOrder, bool rainInside, float phase)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = P(x, y);
            go.AddComponent<UnityEngine.Rendering.SortingGroup>().sortingOrder = baseOrder;
            var visual = new GameObject("Visual").transform;
            visual.SetParent(go.transform, false);
            var glow = Place(visual, "Glow", glowPath, 0, 0, baseOrder, local: true);
            var art = Place(visual, "Art", artPath, 0, 0, baseOrder + 1, local: true);

            var mask = new GameObject("Mask", typeof(SpriteMask)).GetComponent<SpriteMask>();
            mask.transform.SetParent(visual, false);
            mask.sprite = art.sprite;
            mask.alphaCutoff = 0.5f;
            // Maske sadece bu bulutun SortingGroup'u içindeki parçacıkları etkiler.

            var fx = new GameObject(rainInside ? "BubbleRain" : "BubbleDebris");
            fx.transform.SetParent(visual, false);
            var ps = fx.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.startSize = (rainInside ? 1f : 3f) / PPU; // genişlik: damla 1 px, moloz 3 px
            main.startSpeed = 0f;
            main.maxParticles = rainInside ? 40 : 14;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            var emission = ps.emission;
            emission.rateOverTime = rainInside ? 30f : 6f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(art.sprite.rect.width * 0.8f / PPU, 0.1f, 0.1f);
            fx.transform.localPosition = new Vector3(0f, art.sprite.rect.height * 0.42f / PPU, 0f);
            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = new ParticleSystem.MinMaxCurve(rainInside ? -0.4f : 0f);
            velocity.y = new ParticleSystem.MinMaxCurve(rainInside ? -1.8f : -0.5f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f);
            main.startLifetime = rainInside ? 1.1f : 3f;
            if (!rainInside)
            {
                main.gravityModifier = 0.08f;
                main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            }
            var sheet = ps.textureSheetAnimation;
            sheet.enabled = true;
            sheet.mode = ParticleSystemAnimationMode.Sprites;
            if (sheet.spriteCount == 0) sheet.AddSprite(S(rainInside ? "Fx/RainDrop" : "Fx/Rubble")); else sheet.SetSprite(0, S(rainInside ? "Fx/RainDrop" : "Fx/Rubble"));
            var renderer = fx.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = unlit;
            renderer.sortingOrder = baseOrder + 2;
            renderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;

            var collider = go.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(art.sprite.rect.width / PPU, art.sprite.rect.height / PPU);
            var bubble = go.AddComponent<DisasterBubble>();
            var so = new SerializedObject(bubble);
            so.FindProperty("chapter").enumValueIndex = (int)chapter;
            so.FindProperty("visualRoot").objectReferenceValue = visual;
            so.FindProperty("art").objectReferenceValue = art;
            so.FindProperty("glow").objectReferenceValue = glow;
            so.FindProperty("floatPhase").floatValue = phase;
            var arr = so.FindProperty("activeWhileVisible");
            arr.arraySize = 1;
            arr.GetArrayElementAtIndex(0).objectReferenceValue = fx;
            so.ApplyModifiedPropertiesWithoutUndo();
            return bubble;
        }

        // ---------------------------------------------------------------- yardımcılar
        static Transform Group(Transform parent, string name)
        {
            var t = new GameObject(name).transform;
            if (parent != null) t.SetParent(parent, false);
            return t;
        }

        static SpriteRenderer Place(Transform parent, string name, string spritePath, float x, float y, int order, bool local = false)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            if (local) go.transform.localPosition = new Vector3(x / PPU, y / PPU, 0f);
            else go.transform.position = P(x, y);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = S(spritePath);
            if (sr.sprite == null) Debug.LogWarning("[MenuSceneBuilder] Sprite yok: " + spritePath);
            sr.sharedMaterial = unlit;
            sr.sortingOrder = order;
            return sr;
        }

        // Sol alt köşesi (x,y) ve boyutu (w,h) piksel olan, verilen sprite'ı esneterek dolduran dikdörtgen.
        static SpriteRenderer Stretched(Transform parent, string name, string spritePath, float x, float y, float w, float h, int order)
        {
            var sr = Place(parent, name, spritePath, x + w / 2f, y + h / 2f, order);
            var size = sr.sprite.rect.size;
            sr.transform.localScale = new Vector3(w / size.x, h / size.y, 1f);
            return sr;
        }

        static SpriteRenderer Fill(Transform parent, string name, float x, float y, float w, float h, Color color, int order)
        {
            var sr = Stretched(parent, name, "Fx/Pixel", x, y, w, h, order);
            sr.color = color;
            return sr;
        }

        static void SetAlpha(SpriteRenderer sr, float alpha)
        {
            Color c = sr.color;
            c.a = alpha;
            sr.color = c;
        }

        static void SetRef(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetFloat(Object target, string field, float value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetArray(Object target, string field, params Object[] values)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(field);
            p.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetStrings(Object target, string field, params string[] values)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(field);
            p.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).stringValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------------------------------------------------------------- arayüz
        // 640x360 referans: 1080p'de tam 3 kat, piksel font ve paneller keskin kalır.
        static Canvas BuildCanvas(out RectTransform root)
        {
            var go = new GameObject("Canvas", typeof(RectTransform));
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = true;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(ScreenW, ScreenH);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            root = (RectTransform)go.transform;
            return canvas;
        }

        // Ekranı kaplayan grup; çocuklar V(x, y) ile sanal ekran koordinatında yerleşir.
        static CanvasGroup Panel(RectTransform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(ScreenW, ScreenH);
            return go.AddComponent<CanvasGroup>();
        }

        static UnityEngine.UI.Image UiImage(Transform parent, string name, Sprite sprite, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<UnityEngine.UI.Image>();
            img.sprite = sprite;
            var rt = (RectTransform)go.transform;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return img;
        }

        static Text Label(Transform parent, string name, string text, Font font, int size, Color color, Vector2 pos, Vector2 box, TextAnchor anchor)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>();
            t.font = font;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = anchor;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.lineSpacing = 1.15f;
            var shadow = go.AddComponent<Shadow>();
            shadow.effectColor = new Color(0.02f, 0.02f, 0.06f, 0.9f);
            shadow.effectDistance = new Vector2(1f, -1f);
            var rt = (RectTransform)go.transform;
            rt.anchoredPosition = pos;
            rt.sizeDelta = box;
            return t;
        }

        // Piksel panel buton: koyu panel, piksel font; seçilince turuncu çerçeve, sıcak yazı ve solda ok (MenuButtonFx).
        static Button MenuButton(Transform parent, string name, string label, Vector2 pos, Vector2 size)
        {
            var img = UiImage(parent, name, S("UI/Panel"), pos, size);
            img.type = UnityEngine.UI.Image.Type.Sliced;
            var button = img.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            var nav = button.navigation;
            nav.mode = Navigation.Mode.None;
            button.navigation = nav;
            var text = Label(img.transform, "Text", label, regular, 16, Ink, new Vector2(0, 1), new Vector2(size.x - 8, size.y), TextAnchor.MiddleCenter);
            var arrow = Label(img.transform, "Arrow", "▶", regular, 11, Warm, new Vector2(-size.x / 2f + 10f, 1), new Vector2(12, size.y), TextAnchor.MiddleCenter);
            arrow.enabled = false;
            var fx = img.gameObject.AddComponent<MenuButtonFx>();
            SetRef(fx, "background", img);
            SetRef(fx, "normalSprite", S("UI/Panel"));
            SetRef(fx, "highlightSprite", S("UI/PanelLit"));
            SetRef(fx, "label", text);
            SetRef(fx, "arrow", arrow);
            return button;
        }

        static void ChainNavigation(params Button[] buttons)
        {
            for (int i = 0; i < buttons.Length; i++)
            {
                var nav = new Navigation { mode = Navigation.Mode.Explicit };
                nav.selectOnUp = buttons[(i - 1 + buttons.Length) % buttons.Length];
                nav.selectOnDown = buttons[(i + 1) % buttons.Length];
                buttons[i].navigation = nav;
            }
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
