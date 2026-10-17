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
    // Assets/Scenes/Menu/MainMenu.unity sahnesini modüler parçalardan kurar: Tilemap duvar/zemin, ayrı
    // mobilya ve dekor sprite'ları, katmanlı pencere, uyuyan çocuk, iki rüya bulutu, Pixel Perfect Camera,
    // arayüz ve tüm script referansları. İlk kurulum ve hızlı yeniden kurulum içindir; çalıştırıldığında
    // MainMenu sahnesinin üzerine yazar (sahnede elle yapılan değişiklikler kaybolur).
    //
    // Konumlar 480x270'lik sanal ekranda, sol alt köşeden piksel cinsindendir (1 birim = 16 piksel).
    // Sıralama tek "Default" sorting layer'ında sortingOrder bantlarıyla yapılır (yeni sorting layer
    // eklemek ProjectSettings değişikliği gerektirir):
    //   0-5 duvar/zemin/duvar dekoru · 9-17 pencere içi (maskeli) · 18-19 çerçeve/perde · 30-49 mobilya
    //   50 sarkıt lamba · 80-81 oda ışığı · 100-109 bulutlar · 120 logo · 900 karanlık · 950+ saat
    public static class MenuSceneBuilder
    {
        const string ScenePath = "Assets/Scenes/Menu/MainMenu.unity";
        const string Art = "Assets/Art/Menu/";
        const float PPU = 16f;
        const int ScreenW = 480, ScreenH = 270;

        static Material unlit;
        static Font font;

        static Vector3 P(float x, float y) => new Vector3((x - ScreenW / 2f) / PPU, (y - ScreenH / 2f) / PPU, 0f);
        static Sprite S(string path) => AssetDatabase.LoadAssetAtPath<Sprite>(Art + path + ".png");

        [MenuItem("AfetUyanisi/Menü/Sahneyi Kur (MainMenu.unity üzerine yazar)")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[MenuSceneBuilder] Play modundayken sahne kurulamaz; önce Play'den çıkın.");
                return;
            }
            unlit = AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat");
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
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
            cam.backgroundColor = Color.black;
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

            // ---------------- Oda (Tilemap)
            var room = new GameObject("Room").transform;
            var grid = new GameObject("Grid", typeof(Grid));
            grid.transform.SetParent(room, false);
            grid.transform.position = P(0, 0);
            Tilemap wall = TileLayer(grid.transform, "Wall", 0);
            Tilemap floor = TileLayer(grid.transform, "Floor", 1);
            TileBase wallTile = TileAsset("TileWall"), topTile = TileAsset("TileWallTop"), baseTile = TileAsset("TileBaseboard"), floorTile = TileAsset("TileFloor");
            // Ekranın her yanında bir karo taşar: kamera sarsıntısında kenarda boşluk görünmesin.
            for (int x = -1; x <= ScreenW / 16; x++)
            {
                for (int y = -1; y < 4; y++) floor.SetTile(new Vector3Int(x, y, 0), floorTile);
                floor.SetTile(new Vector3Int(x, 4, 0), baseTile);
                for (int y = 5; y < 16; y++) wall.SetTile(new Vector3Int(x, y, 0), wallTile);
                wall.SetTile(new Vector3Int(x, 16, 0), topTile);
                wall.SetTile(new Vector3Int(x, 17, 0), topTile);
            }

            // ---------------- Duvar dekoru
            var decor = Group(room, "WallDecor");
            var posterRocket = Place(decor, "PosterRocket", "Decor/PosterRocket", 206, 186, 5);
            var posterSaturn = Place(decor, "PosterSaturn", "Decor/PosterSaturn", 252, 202, 5);
            var worldMap = Place(decor, "WorldMap", "Decor/WorldMap", 268, 150, 5);
            var starFrame = Place(decor, "StarFrame", "Decor/StarFrame", 288, 214, 5);
            var hangingPlant = Place(decor, "HangingPlant", "Decor/HangingPlant", 470, 262, 22);
            var pendantLamp = Place(decor, "PendantLamp", "Room/PendantLamp", 196, 272, 50);

            // ---------------- Pencere
            var window = BuildWindow(room, out DynamicWindowController windowController, out Transform curtainL, out Transform curtainR);

            // ---------------- Mobilya
            var furniture = Group(room, "Furniture");
            var desk = Place(furniture, "Desk", "Room/Desk", 380, 44, 30);
            var deskLamp = Place(furniture, "DeskLamp", "Room/DeskLamp", 326, 106, 31);
            var globe = Place(furniture, "Globe", "Decor/Globe", 356, 106, 31);
            var bookStack = Place(furniture, "BookStack", "Decor/BookStack", 430, 106, 31);
            var bookshelf = Place(furniture, "Bookshelf", "Room/Bookshelf", 474, 30, 32);
            var backpack = Place(furniture, "Backpack", "Room/Backpack", 290, 26, 33);
            Place(furniture, "Plant", "Room/Plant", 30, 20, 33);
            var chair = Place(furniture, "Chair", "Room/Chair", 394, 14, 40);
            var nightstand = Place(furniture, "Nightstand", "Room/Nightstand", 108, 22, 30);
            var moonLamp = Place(furniture, "MoonLamp", "Decor/MoonLamp", 88, 78, 31);
            var roomClock = Place(furniture, "RoomClock", "Room/RoomClock", 124, 87, 31); // introda karanlığın altında; büyük saat gösterilir

            // Yatak yere yatık, üstten 3/4 bakış: başlık ve yastıklar üstte (başlık süpürgelik hizasında), ayak ucu
            // ekranın altında. Çocuk kareleri yastık + yorganı içerir; altta alçak ayak tahtası (BedFront), karelerin
            // arkasında saçın üstüne kadar yükselen başlık (BedHeadboard). Çocuk ve başlık yatağın çocuğu: sarsıntıda
            // birlikte hareket ederler. Kare pivotu yatağın alt-ortası. Saçın üstü y=126'da kalır ki iki düşünce yolu
            // bulutlara kadar sığsın; yatak sol sütundaki butonların altına girmesin diye x=201'de.
            var bedFront = Place(furniture, "Bed", "Room/BedFront", 201, 2, 35);
            float frameBase = bedFront.sprite.rect.height;
            Place(bedFront.transform, "Headboard", "Room/BedHeadboard", 0, frameBase + MenuArtBuilder.HeadboardOffset, 34, local: true);
            var boyRenderer = Place(bedFront.transform, "SleepingBoy", "Boy/BoySleep1", 0, frameBase, 37, local: true);

            var boy = boyRenderer.gameObject.AddComponent<SleepingBoy>();
            SetRef(boy, "body", boyRenderer);
            // Üst sıra: zzz'li uyku, ağzı açık, sakin uyku (nefes); göz ovuşturma (kıpırdanma).
            // Alt sıra: gerinme, gözler açık, etrafa bakma (irkilme).
            SetArray(boy, "breathFrames", S("Boy/BoySleep1"), S("Boy/BoySleep2"), S("Boy/BoySleep3"));
            SetArray(boy, "stirFrames", S("Boy/BoySleep4"));
            SetArray(boy, "startledFrames", S("Boy/BoyWake2"), S("Boy/BoyWake4"), S("Boy/BoyWake3"), S("Boy/BoyWake2"));

            // ---------------- Oda ışığı
            var light = Group(room, "RoomLight");
            var roomTint = Fill(light, "RoomTint", 0, 0, ScreenW, ScreenH, new Color(1f, 0.6f, 0.25f, 0.07f), 80);
            var roomFlash = Fill(light, "RoomFlash", 0, 0, ScreenW, ScreenH, new Color(0.85f, 0.9f, 1f, 0f), 81);
            SetRef(windowController, "roomTint", roomTint);
            SetRef(windowController, "roomFlash", roomFlash);

            // ---------------- Rüya seçimi
            var dreams = Group(null, "DreamSelection");
            var thought = Group(dreams, "ThoughtTrail");
            // Çocuğun başının üstünden iki ayrı yol: sola DEPREM'e, sağa SEL'e (konum = yolun başa en yakın baloncuğu).
            // Uçları bulutların alt kenarının birkaç piksel altına girer (bulut üstte çizilir), böylece yüzme ve seçili
            // büyüme sırasında bağlantı kopmaz. Roket posterine, dünya haritasına ve etiketlere değmez.
            var quakePath = Place(thought, "ThoughtToQuake", "UI/ThoughtPathQuake", 190, 133, 99);
            var floodPath = Place(thought, "ThoughtToFlood", "UI/ThoughtPathFlood", 212, 133, 99);
            var trail = thought.gameObject.AddComponent<ThoughtTrail>();
            SetArray(trail, "dots", quakePath, floodPath);
            var quake = Bubble(dreams, "Bubble_Deprem", MenuChapterLoader.Chapter.Earthquake, "Bubbles/BubbleQuake", "Bubbles/BubbleQuakeGlow", "Bubbles/LabelQuake", 100, 200, 100, false, 0f);
            var flood = Bubble(dreams, "Bubble_Sel", MenuChapterLoader.Chapter.Flood, "Bubbles/BubbleFlood", "Bubbles/BubbleFloodGlow", "Bubbles/LabelFlood", 266, 200, 105, true, 0.5f);

            // ---------------- Logo, karanlık, büyük saat
            var titleRenderer = Place(null, "Title", "UI/LogoStacked", 90, 222, 120);
            var title = titleRenderer.gameObject.AddComponent<SpriteFader>();
            var darkness = Fill(null, "Darkness", -16, -16, ScreenW + 32, ScreenH + 32, new Color(0.02f, 0.02f, 0.05f, 1f), 900);
            var darkFader = darkness.gameObject.AddComponent<SpriteFader>();
            var bigClock = Place(null, "IntroClock", "UI/ClockBig", 240, 140, 960);
            var bigClockFader = bigClock.gameObject.AddComponent<SpriteFader>();

            // ---------------- Arayüz
            var canvas = BuildCanvas(out RectTransform root);
            var mainButtons = Panel(root, "MainButtons", new Vector2(-203, 4), new Vector2(160, 96));
            var newGame = MenuButton(mainButtons.transform, "NewGameButton", "Yeni Oyun", new Vector2(0, 32));
            var settings = MenuButton(mainButtons.transform, "SettingsButton", "Ayarlar", new Vector2(0, 0));
            var exit = MenuButton(mainButtons.transform, "ExitButton", "Çıkış", new Vector2(0, -32));
            ChainNavigation(newGame, settings, exit);

            var selection = Panel(root, "SelectionPanel", new Vector2(0, -150), new Vector2(640, 50));
            var startChapter = MenuButton(selection.transform, "StartChapterButton", "Bölümü Başlat", new Vector2(208, 4));
            var back = MenuButton(selection.transform, "BackButton", "Geri", new Vector2(60, 4));
            ((RectTransform)back.transform).sizeDelta = new Vector2(100, 26);
            Label(selection.transform, "Hint", "Rüyanı seç: fare ya da A/D  ·  Enter: başla  ·  Esc: geri", 11, new Vector2(140, 32), new Vector2(300, 20), TextAnchor.MiddleCenter);

            var settingsPanel = UiImage(root, "SettingsPanel", S("UI/ButtonBar"), Vector2.zero, new Vector2(300, 150));
            settingsPanel.type = UnityEngine.UI.Image.Type.Sliced;
            Label(settingsPanel.transform, "Title", "Ayarlar", 18, new Vector2(0, 45), new Vector2(280, 30), TextAnchor.MiddleCenter);
            Label(settingsPanel.transform, "Info", "Ses ve tam ekran ayarları yakında.\nEsc: geri", 12, new Vector2(0, 5), new Vector2(280, 40), TextAnchor.MiddleCenter);
            var settingsBack = MenuButton(settingsPanel.transform, "BackButton", "Geri", new Vector2(0, -45));
            ((RectTransform)settingsBack.transform).sizeDelta = new Vector2(100, 26);
            settingsPanel.gameObject.SetActive(false);

            var tooltip = UiImage(root, "Tooltip", S("UI/ButtonBar"), new Vector2(0, -110), new Vector2(330, 26));
            tooltip.type = UnityEngine.UI.Image.Type.Sliced;
            var tooltipText = Label(tooltip.transform, "Text", "", 12, Vector2.zero, new Vector2(320, 24), TextAnchor.MiddleCenter);
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
                (bigClockFader, 1.2f, 0.6f, true),
                (darkFader, 1.0f, 1.2f, true),
                (title, 2.6f, 0.6f, false),
                (mainButtons, 3.2f, 0.6f, false),
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
            SetRef(earthquake, "boy", boy);
            SetRef(earthquake, "rumbleSource", rumble);
            var props = new (Component target, EarthquakePreview.Reaction reaction, float strength)[]
            {
                (bedFront, EarthquakePreview.Reaction.Jitter, 1f), // çocuk yatağın çocuğu, birlikte titrer
                (nightstand, EarthquakePreview.Reaction.Jitter, 1f),
                (roomClock, EarthquakePreview.Reaction.Jitter, 1f),
                (moonLamp, EarthquakePreview.Reaction.Tilt, 6f),
                (desk, EarthquakePreview.Reaction.Jitter, 1f),
                (deskLamp, EarthquakePreview.Reaction.Tilt, 5f),
                (globe, EarthquakePreview.Reaction.Tilt, 6f),
                (bookStack, EarthquakePreview.Reaction.Jitter, 2f),
                (bookshelf, EarthquakePreview.Reaction.Jitter, 1.5f),
                (chair, EarthquakePreview.Reaction.Jitter, 2f),
                (backpack, EarthquakePreview.Reaction.Tilt, 4f),
                (posterRocket, EarthquakePreview.Reaction.Tilt, 3f),
                (posterSaturn, EarthquakePreview.Reaction.Tilt, 3f),
                (worldMap, EarthquakePreview.Reaction.Tilt, 3f),
                (starFrame, EarthquakePreview.Reaction.Tilt, 4f),
                (pendantLamp, EarthquakePreview.Reaction.Sway, 14f),
                (hangingPlant, EarthquakePreview.Reaction.Sway, 8f),
                (curtainL, EarthquakePreview.Reaction.Sway, 2f),
                (curtainR, EarthquakePreview.Reaction.Sway, 2f),
            };
            var eso = new SerializedObject(earthquake);
            var parr = eso.FindProperty("props");
            parr.arraySize = props.Length;
            for (int i = 0; i < props.Length; i++)
            {
                var e = parr.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("target").objectReferenceValue = props[i].target.transform;
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

        // ---------------------------------------------------------------- pencere
        static Transform BuildWindow(Transform room, out DynamicWindowController controller, out Transform curtainL, out Transform curtainR)
        {
            const float cx = 380, cy = 172;
            var root = Group(room, "WindowSystem");
            // SortingGroup: pencere tek parça sıralanır ve cam maskesi sadece kendi içini etkiler
            // (özel sıralama aralıklı maske URP 2D'de üstteki katmanları gizliyordu).
            root.gameObject.AddComponent<UnityEngine.Rendering.SortingGroup>().sortingOrder = 10;
            var frameSprite = S("Window/WindowFrame");
            RectInt glass = GlassRect(frameSprite.texture);
            // Cam alanının sanal ekrandaki sol alt köşesi ve boyutu
            float gx = cx - frameSprite.texture.width / 2f + glass.x;
            float gy = cy - frameSprite.texture.height / 2f + glass.y;
            float gw = glass.width, gh = glass.height;

            var mask = new GameObject("WindowMask", typeof(SpriteMask)).GetComponent<SpriteMask>();
            mask.transform.SetParent(root, false);
            mask.sprite = S("Fx/Pixel");
            mask.transform.position = P(gx + gw / 2f, gy + gh / 2f);
            mask.transform.localScale = new Vector3(gw, gh, 1f);
            // Maske sadece WindowSystem SortingGroup'u içindeki sprite'ları etkiler.

            var clearSky = Stretched(root, "SkySunrise", "Window/SkyGradientSunrise", gx, gy, gw, gh, 10, true);
            var citySunrise = Place(root, "CitySunrise", "Window/CitySunrise", gx + gw / 2f, gy + 30, 12, true);

            var stormRoot = Group(root, "Storm");
            var stormSky = Stretched(stormRoot, "SkyStorm", "Window/SkyGradientStorm", gx, gy, gw, gh, 10, true);
            var cityStorm = Place(stormRoot, "CityStorm", "Window/CityStorm", gx + gw / 2f, gy + 30, 12, true);
            var cloud1 = Place(stormRoot, "StormCloud1", "Window/StormCloud", gx + gw * 0.3f, gy + gh - 14, 13, true);
            var cloud2 = Place(stormRoot, "StormCloud2", "Window/StormCloud", gx + gw * 0.8f, gy + gh - 24, 13, true);
            var rain = Rain(stormRoot, "Rain", gx, gy, gw, gh, 14, 9, 16);
            var droplets = Place(stormRoot, "GlassDroplets", "Fx/GlassDroplets", gx + gw / 2f, gy + gh / 2f, 15, true);
            droplets.drawMode = SpriteDrawMode.Tiled;
            droplets.size = new Vector2(gw / PPU, gh / PPU);
            var bolt = Place(stormRoot, "LightningBolt", "Fx/LightningBolt", gx + gw * 0.7f, gy + gh * 0.62f, 15, true);
            bolt.enabled = false;
            var flash = Stretched(stormRoot, "WindowFlash", "Fx/Pixel", gx, gy, gw, gh, 16, true);
            flash.color = new Color(0.9f, 0.93f, 1f, 0f);

            var glassTint = Stretched(root, "Glass", "Fx/Pixel", gx, gy, gw, gh, 17, false);
            glassTint.color = new Color(0.75f, 0.85f, 1f, 0.06f);
            Place(root, "WindowFrame", "Window/WindowFrame", cx, cy, 18);
            curtainL = Place(root, "CurtainLeft", "Window/CurtainLeft", cx - 60, 248, 19).transform;
            curtainR = Place(root, "CurtainRight", "Window/CurtainRight", cx + 60, 248, 19).transform;

            var rainAudio = root.gameObject.AddComponent<AudioSource>();
            rainAudio.playOnAwake = false;
            var thunderAudio = root.gameObject.AddComponent<AudioSource>();
            thunderAudio.playOnAwake = false;

            controller = root.gameObject.AddComponent<DynamicWindowController>();
            // Ayrı güneş sprite'ı yok: şehir görselinde ufukta zaten güneş var (ayrıca URP 2D'de arka arkaya maskeli
            // iki sprite'tan ikincisi yanlış dokuyla çiziliyordu).
            SetArray(controller, "clearLayers", clearSky, citySunrise);
            SetArray(controller, "stormLayers", stormSky, cityStorm, cloud1, cloud2);
            SetRef(controller, "stormRoot", stormRoot.gameObject);
            SetRef(controller, "rain", rain);
            SetRef(controller, "droplets", droplets);
            SetArray(controller, "driftingClouds", cloud1.transform, cloud2.transform);
            SetRef(controller, "lightningBolt", bolt);
            SetRef(controller, "windowFlash", flash);
            SetRef(controller, "rainAudio", rainAudio);
            SetRef(controller, "thunderAudio", thunderAudio);
            return root;
        }

        // Pencere çerçevesindeki şeffaf cam alanının sınırları (doku pikseli, sol alttan).
        static RectInt GlassRect(Texture2D frame)
        {
            string path = AssetDatabase.GetAssetPath(frame);
            var copy = new Texture2D(2, 2);
            copy.LoadImage(File.ReadAllBytes(path));
            int w = copy.width, h = copy.height, cx = w / 2, cy = h / 2;
            var px = copy.GetPixels32();
            int minX = cx, maxX = cx, minY = cy, maxY = cy;
            while (minX > 0 && px[cy * w + minX - 1].a == 0) minX--;
            while (maxX < w - 1 && px[cy * w + maxX + 1].a == 0) maxX++;
            while (minY > 0 && px[(minY - 1) * w + cx].a == 0) minY--;
            while (maxY < h - 1 && px[(maxY + 1) * w + cx].a == 0) maxY++;
            Object.DestroyImmediate(copy);
            return new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
        }

        static ParticleSystem Rain(Transform parent, string name, float gx, float gy, float gw, float gh, int order, int maskBack, int maskFront)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = P(gx + gw / 2f + 6, gy + gh + 4);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = true;
            main.duration = 1f;
            main.startLifetime = (gh + 8) / PPU / 9f;
            main.startSpeed = 0f;
            // Sprite modunda boyut birim cinsinden (sprite'ın kendi boyu değil): 4 px damla = 4/16 birim.
            main.startSize = 1f / PPU; // sprite modunda boyut genişliğe uygulanır: 1 px genişlik → 1x4 px damla
            main.maxParticles = 80;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = new ParticleSystem.MinMaxCurve(-2.5f);
            velocity.y = new ParticleSystem.MinMaxCurve(-9f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f);
            var emission = ps.emission;
            emission.rateOverTime = 55f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3((gw + 24) / PPU, 0.1f, 0.1f);
            var sheet = ps.textureSheetAnimation;
            sheet.enabled = true;
            sheet.mode = ParticleSystemAnimationMode.Sprites;
            if (sheet.spriteCount == 0) sheet.AddSprite(S("Fx/RainDrop")); else sheet.SetSprite(0, S("Fx/RainDrop"));
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = unlit;
            renderer.sortingOrder = order;
            renderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
            return ps;
        }

        // ---------------------------------------------------------------- bulut
        static DisasterBubble Bubble(Transform parent, string name, MenuChapterLoader.Chapter chapter, string artPath, string glowPath, string labelPath, float x, float y, int baseOrder, bool rainInside, float phase)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = P(x, y);
            go.AddComponent<UnityEngine.Rendering.SortingGroup>().sortingOrder = baseOrder;
            var visual = new GameObject("Visual").transform;
            visual.SetParent(go.transform, false);
            var glow = Place(visual, "Glow", glowPath, 0, 0, baseOrder, local: true);
            var art = Place(visual, "Art", artPath, 0, 0, baseOrder + 1, local: true);
            // Etiket bulutun altında; bulutla birlikte yüzer ve büyür, maskeden etkilenmez.
            Place(visual, "Label", labelPath, 0, -(art.sprite.rect.height / 2f + 8f), baseOrder + 4, local: true);

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
            velocity.x = new ParticleSystem.MinMaxCurve(rainInside ? -1.5f : 0f);
            velocity.y = new ParticleSystem.MinMaxCurve(rainInside ? -7f : -2f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f);
            main.startLifetime = rainInside ? 0.7f : 1.8f;
            if (!rainInside)
            {
                main.gravityModifier = 0.3f;
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

        static SpriteRenderer Place(Transform parent, string name, string spritePath, float x, float y, int order, bool masked = false, bool local = false)
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
            if (masked) sr.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
            return sr;
        }

        // Sol alt köşesi (x,y) ve boyutu (w,h) piksel olan, verilen sprite'ı esneterek dolduran dikdörtgen.
        static SpriteRenderer Stretched(Transform parent, string name, string spritePath, float x, float y, float w, float h, int order, bool masked)
        {
            var sr = Place(parent, name, spritePath, x + w / 2f, y + h / 2f, order, masked);
            var size = sr.sprite.rect.size;
            sr.transform.localScale = new Vector3(w / size.x, h / size.y, 1f);
            return sr;
        }

        static SpriteRenderer Fill(Transform parent, string name, float x, float y, float w, float h, Color color, int order)
        {
            var sr = Stretched(parent, name, "Fx/Pixel", x, y, w, h, order, false);
            sr.color = color;
            return sr;
        }

        static Tilemap TileLayer(Transform grid, string name, int order)
        {
            var go = new GameObject(name, typeof(Tilemap), typeof(TilemapRenderer));
            go.transform.SetParent(grid, false);
            var renderer = go.GetComponent<TilemapRenderer>();
            renderer.sortingOrder = order;
            renderer.sharedMaterial = unlit;
            return go.GetComponent<Tilemap>();
        }

        // Tile asset'leri sprite'larla aynı klasörde tutulur; yoksa oluşturulur.
        static TileBase TileAsset(string name)
        {
            string path = Art + "Tiles/" + name + ".asset";
            var tile = AssetDatabase.LoadAssetAtPath<Tile>(path);
            if (tile == null)
            {
                tile = ScriptableObject.CreateInstance<Tile>();
                AssetDatabase.CreateAsset(tile, path);
            }
            tile.sprite = S("Tiles/" + name);
            EditorUtility.SetDirty(tile);
            AssetDatabase.SaveAssets();
            return tile;
        }

        static void SetRef(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).objectReferenceValue = value;
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

        // ---------------------------------------------------------------- arayüz
        static Canvas BuildCanvas(out RectTransform root)
        {
            var go = new GameObject("Canvas", typeof(RectTransform));
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(640, 360);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            root = (RectTransform)go.transform;
            return canvas;
        }

        static CanvasGroup Panel(RectTransform parent, string name, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
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

        static Text Label(Transform parent, string name, string text, int size, Vector2 pos, Vector2 box, TextAnchor anchor)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>();
            t.font = font;
            t.text = text;
            t.fontSize = size;
            t.color = new Color(0.96f, 0.93f, 0.86f);
            t.alignment = anchor;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            var shadow = go.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.8f);
            shadow.effectDistance = new Vector2(1f, -1f);
            var rt = (RectTransform)go.transform;
            rt.anchoredPosition = pos;
            rt.sizeDelta = box;
            return t;
        }

        static Button MenuButton(Transform parent, string name, string label, Vector2 pos)
        {
            // Buton oranı çerçeve sprite'ına yakın; düz esnetme 9-dilimden daha temiz görünüyor.
            var img = UiImage(parent, name, S("UI/ButtonBar"), pos, new Vector2(150, 28));
            img.type = UnityEngine.UI.Image.Type.Simple;
            var button = img.gameObject.AddComponent<Button>();
            var colors = button.colors;
            colors.normalColor = new Color(0.85f, 0.85f, 0.9f, 1f);
            colors.highlightedColor = new Color(1f, 0.86f, 0.55f, 1f);
            colors.selectedColor = new Color(1f, 0.86f, 0.55f, 1f);
            colors.pressedColor = new Color(1f, 0.7f, 0.35f, 1f);
            colors.disabledColor = new Color(0.45f, 0.45f, 0.5f, 0.8f);
            button.colors = colors;
            var nav = button.navigation;
            nav.mode = Navigation.Mode.None;
            button.navigation = nav;
            Label(img.transform, "Text", label, 13, Vector2.zero, new Vector2(140, 24), TextAnchor.MiddleCenter);
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
