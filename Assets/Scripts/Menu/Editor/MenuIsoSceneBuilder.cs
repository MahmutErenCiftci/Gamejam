using System.Collections.Generic;
using System.IO;
using AfetUyanisi.Menu;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AfetUyanisi.Menu.EditorTools
{
    /// <summary>
    /// İzometrik ana menünün kaynağı (MainMenu.unity bununla kurulur; Berk'in eski MenuSceneBuilder'ı ön görünüm odayı kurar).
    ///  1. Assets/Art/Menu/Iso/*.png içe aktarma ayarları (64 PPU, nokta filtre, sıkıştırmasız).
    ///  2. Assets/Prefabs/Menu/MenuBedroom.prefab: Bedroom.unity'deki oda objelerinin KOPYASI (Environment, Grid, Furniture),
    ///     collider/oyun scriptleri yok, yatakta uyuyan Cenk (PROP_Bedroom_Bed_Sleeping), pencere fırtına/yağmur katmanları,
    ///     toz zerreleri ve MenuRoomPreview. Bedroom.unity değiştirilmez (açık değilse eklemeli açılır, kaydedilmeden kapatılır).
    ///  3. Assets/Scenes/Menu/MainMenu.unity: Bedroom kamerasının kopyası (PixelPerfectCamera 64 PPU, 640x360) sola kaydırılmış,
    ///     MenuBedroom, 640x360 referanslı Screen Space Overlay arayüz (1 sanat pikseli = 1 referans birimi), EventSystem.
    /// Sahne yeni bir ek sahnede kurulur ve dosyanın üzerine kaydedilir; açık sahnelere dokunulmaz.
    /// </summary>
    public static class MenuIsoSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/Menu/MainMenu.unity";
        private const string BedroomPath = "Assets/Scenes/Rooms/Bedroom.unity";
        private const string PrefabDir = "Assets/Prefabs/Menu";
        private const string PrefabPath = PrefabDir + "/MenuBedroom.prefab";
        private const string Art = "Assets/Art/Menu/Iso/";
        private const string EqUi = "Assets/Art/UI/Earthquake/";
        private const string FontPath = "Assets/Art/Dialogue/Fonts/Nunito-Black.ttf";
        private const string UnlitMat = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";

        // Kamera: Bedroom kadrajı (0.25, 2.375) → menüde oda sağa ve aşağı kayar, solda logo/butonlara yer açılır.
        private static readonly Vector3 BedroomCamera = new Vector3(0.25f, 2.375f, -10f);
        private static readonly Vector3 MenuCamera = new Vector3(-77f / 64f, 192f / 64f, -10f);

        private static readonly Color Ink = Hex("14141F"), Navy = Hex("263559"), Cream = Hex("F4EBD8"), Gold = Hex("FFD27E"), Amber = Hex("F2A33A");

        private static Font font;

        [MenuItem("AfetUyanisi/Menü/İzometrik Menüyü Kur (MenuBedroom + MainMenu.unity)")]
        public static void BuildAll()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[MenuIsoSceneBuilder] Play modundayken kurulamaz.");
                return;
            }
            ImportArt();
            Build();
        }

        // ------------------------------------------------------------------ 1. sanat

        public static void ImportArt()
        {
            foreach (string file in Directory.GetFiles(Art, "*.png"))
            {
                string path = file.Replace('\\', '/');
                var ti = AssetImporter.GetAtPath(path) as TextureImporter;
                if (ti == null) { AssetDatabase.ImportAsset(path); ti = AssetImporter.GetAtPath(path) as TextureImporter; }
                if (ti == null) continue;
                string name = Path.GetFileNameWithoutExtension(path);
                bool world = name.StartsWith("MENU_Window") || name == "MENU_Dust";
                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single;
                ti.spritePixelsPerUnit = world ? 64 : 100;
                ti.filterMode = FilterMode.Point;
                ti.textureCompression = TextureImporterCompression.Uncompressed;
                ti.mipmapEnabled = false;
                ti.alphaIsTransparency = true;
                ti.wrapMode = TextureWrapMode.Clamp;
                var settings = new TextureImporterSettings();
                ti.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                settings.spriteAlignment = (int)(name.StartsWith("MENU_Window") ? SpriteAlignment.BottomCenter : SpriteAlignment.Center);
                ti.SetTextureSettings(settings);
                ti.SaveAndReimport();
            }
        }

        // ------------------------------------------------------------------ 2+3. oda prefab'ı ve sahne

        public static void Build()
        {
            font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
            Scene previousActive = SceneManager.GetActiveScene();
            Scene bedroom = SceneManager.GetSceneByPath(BedroomPath);
            bool openedBedroom = false;
            if (!bedroom.isLoaded) { bedroom = EditorSceneManager.OpenScene(BedroomPath, OpenSceneMode.Additive); openedBedroom = true; }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scene);
                GameObject bedroomCamera = FindIn(bedroom, "Environment/Main Camera");

                // Kamera (Bedroom kamerasının kopyası: aynı PixelPerfectCamera ve URP ayarları)
                GameObject cam = Object.Instantiate(bedroomCamera);
                cam.name = "Main Camera";
                cam.tag = "MainCamera";
                cam.transform.position = MenuCamera;
                if (cam.GetComponent<AudioListener>() == null) cam.AddComponent<AudioListener>();

                MenuRoomPreview preview = BuildRoomPrefab(bedroom, out GameObject roomInstance);
                var so = new SerializedObject(preview);
                so.FindProperty("cameraTransform").objectReferenceValue = cam.transform;
                so.ApplyModifiedPropertiesWithoutUndo();

                var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                BuildUi(preview, cam.transform);

                EditorSceneManager.SaveScene(scene, ScenePath);
                Debug.Log("[MenuIsoSceneBuilder] MainMenu.unity ve MenuBedroom.prefab kuruldu.");
            }
            finally
            {
                if (previousActive.IsValid() && previousActive.isLoaded) SceneManager.SetActiveScene(previousActive);
                EditorSceneManager.CloseScene(scene, true);
                if (openedBedroom) EditorSceneManager.CloseScene(bedroom, true);
            }
        }

        private static MenuRoomPreview BuildRoomPrefab(Scene bedroom, out GameObject instance)
        {
            var root = new GameObject("MenuBedroom");
            foreach (string name in new[] { "Environment", "Grid", "Furniture" })
            {
                GameObject src = FindIn(bedroom, name);
                GameObject copy = Object.Instantiate(src, root.transform);
                copy.name = name;
            }
            // Oyun parçaları çıkarılır: kamera (sahnede ayrı), oda sınırı, collider'lar, saat/ses scriptleri.
            Object.DestroyImmediate(root.transform.Find("Environment/Main Camera").gameObject);
            Transform bounds = root.transform.Find("Environment/RoomBounds");
            if (bounds != null) Object.DestroyImmediate(bounds.gameObject);
            foreach (var c in root.GetComponentsInChildren<Collider2D>(true)) Object.DestroyImmediate(c);
            foreach (var mb in root.GetComponentsInChildren<MonoBehaviour>(true))
                if (!(mb is Light2D)) Object.DestroyImmediate(mb);
            foreach (var a in root.GetComponentsInChildren<AudioSource>(true)) Object.DestroyImmediate(a);

            // Yatakta uyuyan Cenk (Bedroom açılışındaki sprite)
            var bed = root.transform.Find("Furniture/Bed").GetComponent<SpriteRenderer>();
            bed.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Props/Bedroom/PROP_Bedroom_Bed_Sleeping.png");

            // Pencere katmanları
            var window = root.transform.Find("Environment/Walls/Window").GetComponent<SpriteRenderer>();
            Material unlit = AssetDatabase.LoadAssetAtPath<Material>(UnlitMat);
            SpriteRenderer storm = Child(window.transform, "Storm", Sprite("MENU_Window_Storm"), window.sharedMaterial, window.sortingOrder + 1);
            SpriteRenderer rain = Child(window.transform, "Rain", Sprite("MENU_Window_Rain_0"), unlit, window.sortingOrder + 2);
            SpriteRenderer flash = Child(window.transform, "Flash", Sprite("MENU_Window_Flash"), unlit, window.sortingOrder + 3);
            foreach (var r in new[] { storm, rain, flash }) { r.color = new Color(1f, 1f, 1f, 0f); r.enabled = false; }

            // Toz zerreleri
            var fx = new GameObject("FX").transform;
            fx.SetParent(root.transform, false);
            var dust = new List<SpriteRenderer>();
            for (int i = 0; i < 12; i++)
            {
                SpriteRenderer d = Child(fx, "Dust" + i, Sprite("MENU_Dust"), window.sharedMaterial, 300);
                d.enabled = false;
                dust.Add(d);
            }

            // Sesler
            var rainSource = root.AddComponent<AudioSource>();
            rainSource.playOnAwake = false;
            rainSource.loop = true;
            var sfxSource = root.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;

            var preview = root.AddComponent<MenuRoomPreview>();
            var so = new SerializedObject(preview);
            so.FindProperty("globalLight").objectReferenceValue = root.transform.Find("Environment/Global Light 2D").GetComponent<Light2D>();
            SetArray(so, "windowLights", root.transform.Find("Environment/Lights/WindowGlowFloor").GetComponent<Light2D>(),
                root.transform.Find("Environment/Lights/WindowGlow").GetComponent<Light2D>());
            so.FindProperty("windowStorm").objectReferenceValue = storm;
            so.FindProperty("windowRain").objectReferenceValue = rain;
            so.FindProperty("windowFlash").objectReferenceValue = flash;
            SetArray(so, "rainFrames", Sprite("MENU_Window_Rain_0"), Sprite("MENU_Window_Rain_1"), Sprite("MENU_Window_Rain_2"), Sprite("MENU_Window_Rain_3"));
            SetArray(so, "dust", dust.ToArray());
            so.FindProperty("rainSource").objectReferenceValue = rainSource;
            so.FindProperty("sfxSource").objectReferenceValue = sfxSource;
            so.ApplyModifiedPropertiesWithoutUndo();

            if (!AssetDatabase.IsValidFolder(PrefabDir)) AssetDatabase.CreateFolder("Assets/Prefabs", "Menu");
            // SaveAsPrefabAssetAndConnect prefab VARLIĞINI döndürür; sahnedeki örnek root'un kendisidir.
            PrefabUtility.SaveAsPrefabAssetAndConnect(root, PrefabPath, InteractionMode.AutomatedAction);
            instance = root;
            return root.GetComponent<MenuRoomPreview>();
        }

        // ------------------------------------------------------------------ arayüz (640x360, sol-üst koordinat)

        private static void BuildUi(MenuRoomPreview preview, Transform cam)
        {
            var canvasGo = new GameObject("MenuCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.layer = 5;
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(640f, 360f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            scaler.referencePixelsPerUnit = 100f;
            Transform c = canvasGo.transform;

            // ---- ana arayüz grubu
            RectTransform ui = Stretch(Rect("UI", c));
            var uiGroup = ui.gameObject.AddComponent<CanvasGroup>();

            // Düşünce noktaları + "z" (Cenk'in kafası ≈ (263, 227))
            Img("ThoughtXS", ui, Sprite("MENU_Thought_XS"), Center(266, 212, 9, 9));
            Img("ThoughtS", ui, Sprite("MENU_Thought_S"), Center(272, 195, 11, 11));
            Img("ThoughtM", ui, Sprite("MENU_Thought_M"), Center(279, 174, 14, 14));
            Img("ThoughtL", ui, Sprite("MENU_Thought_L"), Center(285, 148, 18, 18));
            Text sleepZ = Txt("SleepZ", ui, "z", 9, Cream, TextAnchor.MiddleCenter, Center(255, 216, 12, 12));

            // Rüya bulutları: Deprem, Sel (açık) · Orman Yangını, Heyelan (kilitli)
            var bubbles = new List<MenuChoiceBubble>
            {
                Bubble(ui, "BubbleQuake", "DEPREM", MenuChapterLoader.Chapter.Earthquake, false, Sprite("MENU_Bubble_Quake"), 274, 70, 0.0f),
                Bubble(ui, "BubbleFlood", "SEL", MenuChapterLoader.Chapter.Flood, false, Sprite("MENU_Bubble_Flood"), 384, 58, 0.35f),
                Bubble(ui, "BubbleFire", "ORMAN YANGINI", MenuChapterLoader.Chapter.Earthquake, true, Sprite("MENU_Bubble_Fire_Locked"), 488, 64, 0.6f),
                Bubble(ui, "BubbleLandslide", "HEYELAN", MenuChapterLoader.Chapter.Earthquake, true, Sprite("MENU_Bubble_Landslide_Locked"), 580, 80, 0.85f),
            };

            // Logo + slogan
            Img("Logo", ui, AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Menu/UI/LogoStacked.png"), TL(26, 40, 147, 76));
            Txt("Slogan", ui, "Küçük kararlar, büyük fark yaratır.", 8, new Color(Cream.r, Cream.g, Cream.b, 0.8f), TextAnchor.UpperLeft, TL(28, 122, 190, 12));

            // Sol liste
            MenuListItem newGame = ListItem(ui, "NewGame", "Yeni Oyun", TL(20, 156, 132, 22), 12, Cream, Gold);
            MenuListItem settingsButton = ListItem(ui, "Settings", "Ayarlar", TL(20, 182, 132, 22), 12, Cream, Gold);
            MenuListItem exit = ListItem(ui, "Exit", "Çıkış", TL(20, 208, 132, 22), 12, Cream, Gold);

            // Alt ipucu ve bildirim
            Text hint = Txt("Hint", ui, "", 8, new Color(Cream.r, Cream.g, Cream.b, 0.6f), TextAnchor.MiddleLeft, TL(22, 332, 320, 14));
            RectTransform toastRt = Rect("Toast", ui);
            Center(toastRt, 320, 312, 280, 20);
            var toast = toastRt.gameObject.AddComponent<CanvasGroup>();
            toast.blocksRaycasts = false;
            Sliced("Plate", toastRt, EqSprite("UI_EQ_Button"), null);
            Text toastText = Txt("Text", toastRt, "", 9, Cream, TextAnchor.MiddleCenter, null);
            Stretch(toastText.rectTransform, 2f);

            // ---- ayarlar paneli
            MenuSettingsPanel settings = BuildSettings(c);

            // ---- kararma (en üstte)
            RectTransform black = Stretch(Rect("Blackout", c));
            var blackImg = black.gameObject.AddComponent<Image>();
            blackImg.sprite = Sprite("MENU_White");
            blackImg.color = Hex("07080D");
            var blackout = black.gameObject.AddComponent<CanvasGroup>();

            // ---- kontrolcü
            var ctrlGo = new GameObject("MenuController");
            var ctrl = ctrlGo.AddComponent<MainMenuScreen>();
            var so = new SerializedObject(ctrl);
            so.FindProperty("uiGroup").objectReferenceValue = uiGroup;
            so.FindProperty("blackout").objectReferenceValue = blackout;
            so.FindProperty("newGameButton").objectReferenceValue = newGame;
            so.FindProperty("settingsButton").objectReferenceValue = settingsButton;
            so.FindProperty("exitButton").objectReferenceValue = exit;
            SetArray(so, "bubbles", bubbles.ToArray());
            so.FindProperty("hint").objectReferenceValue = hint;
            so.FindProperty("toast").objectReferenceValue = toast;
            so.FindProperty("toastText").objectReferenceValue = toastText;
            so.FindProperty("settings").objectReferenceValue = settings;
            so.FindProperty("sleepZ").objectReferenceValue = sleepZ.rectTransform;
            so.FindProperty("room").objectReferenceValue = preview;
            so.FindProperty("cameraTransform").objectReferenceValue = cam;
            so.FindProperty("gameplayCameraPosition").vector3Value = BedroomCamera;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static MenuChoiceBubble Bubble(RectTransform parent, string name, string title, MenuChapterLoader.Chapter chapter,
            bool locked, Sprite art, float cx, float cy, float phase)
        {
            Vector2 size = art.rect.size;
            RectTransform root = Rect(name, parent);
            Center(root, cx, cy, size.x, size.y);
            RectTransform visual = Rect("Visual", root);
            Stretch(visual);

            Image glow = null;
            if (!locked)
            {
                glow = Img("Glow", visual, Sprite("MENU_Bubble_Glow"), null);
                Stretch(glow.rectTransform);
                glow.enabled = false;
            }
            Image body = Img("Art", visual, art, null);
            Stretch(body.rectTransform);
            body.raycastTarget = true;

            // Etiket plakası: bulutun alt kenarına biner.
            int textW = Mathf.CeilToInt(title.Length * (locked ? 5.2f : 6.4f));
            float plateW = Mathf.Max(40f, textW + 14f), plateH = locked ? 15f : 17f;
            RectTransform plateRt = Rect("Label", visual);
            plateRt.anchorMin = plateRt.anchorMax = new Vector2(0.5f, 0f);
            plateRt.pivot = new Vector2(0.5f, 0.5f);
            plateRt.anchoredPosition = new Vector2(0f, locked ? 6f : 7f);
            plateRt.sizeDelta = new Vector2(Mathf.Round(plateW / 2f) * 2f, plateH);
            Image plate = Sliced("Plate", plateRt, EqSprite("UI_EQ_Button"), null);
            Text label = Txt("Text", plateRt, title, locked ? 8 : 10, locked ? Hex("B9BFCC") : Cream, TextAnchor.MiddleCenter, null);
            Stretch(label.rectTransform);
            label.rectTransform.offsetMin += new Vector2(0f, 1f);

            if (locked)
            {
                Image lockIcon = Img("Lock", visual, Sprite("MENU_Lock"), null);
                lockIcon.rectTransform.anchorMin = lockIcon.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                lockIcon.rectTransform.anchoredPosition = new Vector2(0f, 8f);
                lockIcon.rectTransform.sizeDelta = new Vector2(13f, 14f);
                Text soon = Txt("Soon", visual, "Yakında", 8, Hex("D5D9E2"), TextAnchor.MiddleCenter, null);
                soon.rectTransform.anchorMin = soon.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                soon.rectTransform.anchoredPosition = new Vector2(0f, -8f);
                soon.rectTransform.sizeDelta = new Vector2(60f, 12f);
            }

            var bubble = root.gameObject.AddComponent<MenuChoiceBubble>();
            var so = new SerializedObject(bubble);
            so.FindProperty("chapter").enumValueIndex = (int)chapter;
            so.FindProperty("locked").boolValue = locked;
            so.FindProperty("visual").objectReferenceValue = visual;
            so.FindProperty("glow").objectReferenceValue = glow;
            so.FindProperty("labelPlate").objectReferenceValue = plate;
            so.FindProperty("label").objectReferenceValue = label;
            so.FindProperty("plateIdle").objectReferenceValue = EqSprite("UI_EQ_Button");
            so.FindProperty("plateSelected").objectReferenceValue = locked ? null : EqSprite("UI_EQ_NamePlate");
            so.FindProperty("labelIdle").colorValue = locked ? Hex("B9BFCC") : Cream;
            so.FindProperty("floatPhase").floatValue = phase;
            so.ApplyModifiedPropertiesWithoutUndo();
            return bubble;
        }

        private static MenuListItem ListItem(RectTransform parent, string name, string text, System.Action<RectTransform> place,
            int size, Color idle, Color focus)
        {
            RectTransform rt = Rect(name, parent);
            place(rt);
            Image plate = Sliced("Plate", rt, EqSprite("UI_EQ_Button_Hover"), null);
            plate.raycastTarget = true;
            Image hit = plate;
            Image marker = Img("Marker", rt, Sprite("MENU_White"), null);
            marker.color = Amber;
            marker.rectTransform.anchorMin = marker.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            marker.rectTransform.pivot = new Vector2(0f, 0.5f);
            marker.rectTransform.anchoredPosition = new Vector2(6f, 1f);
            marker.rectTransform.sizeDelta = new Vector2(3f, 10f);
            Text label = Txt("Text", rt, text, size, idle, TextAnchor.MiddleLeft, null);
            Stretch(label.rectTransform);
            label.rectTransform.offsetMin = new Vector2(14f, 1f);

            // Odakta değilken plaka gizli; fare için görünmez bir tıklama alanı gerekir.
            var hitArea = Img("HitArea", rt, Sprite("MENU_White"), null);
            hitArea.color = new Color(1f, 1f, 1f, 0f);
            hitArea.raycastTarget = true;
            Stretch(hitArea.rectTransform);
            hitArea.transform.SetAsFirstSibling();
            plate.raycastTarget = false;

            var item = rt.gameObject.AddComponent<MenuListItem>();
            var so = new SerializedObject(item);
            so.FindProperty("plate").objectReferenceValue = plate;
            so.FindProperty("marker").objectReferenceValue = marker;
            so.FindProperty("label").objectReferenceValue = label;
            so.FindProperty("idleColor").colorValue = idle;
            so.FindProperty("focusColor").colorValue = focus;
            so.ApplyModifiedPropertiesWithoutUndo();
            return item;
        }

        private static MenuSettingsPanel BuildSettings(Transform canvas)
        {
            RectTransform root = Stretch(Rect("SettingsPanel", canvas));
            var group = root.gameObject.AddComponent<CanvasGroup>();
            Image dim = Img("Dim", root, Sprite("MENU_White"), null);
            dim.color = new Color(Ink.r, Ink.g, Ink.b, 0.65f);
            dim.raycastTarget = true;
            Stretch(dim.rectTransform);

            RectTransform panel = Rect("Panel", root);
            Center(panel, 320, 180, 220, 124);
            Sliced("Card", panel, EqSprite("UI_EQ_InfoCard"), null);
            Text title = Txt("Title", panel, "AYARLAR", 10, Cream, TextAnchor.MiddleCenter, null);
            title.rectTransform.anchorMin = new Vector2(0f, 1f);
            title.rectTransform.anchorMax = new Vector2(1f, 1f);
            title.rectTransform.pivot = new Vector2(0.5f, 1f);
            title.rectTransform.anchoredPosition = new Vector2(0f, -1f);
            title.rectTransform.sizeDelta = new Vector2(0f, 18f);

            MenuListItem volume = ListItem(panel, "Volume", "Ses", TL(12, 30, 196, 22), 11, Ink, Gold);
            MenuListItem full = ListItem(panel, "Fullscreen", "Tam ekran", TL(12, 56, 196, 22), 11, Ink, Gold);
            MenuListItem back = ListItem(panel, "Back", "Geri", TL(12, 90, 196, 22), 11, Ink, Gold);

            var settings = root.gameObject.AddComponent<MenuSettingsPanel>();
            var so = new SerializedObject(settings);
            so.FindProperty("group").objectReferenceValue = group;
            so.FindProperty("volumeRow").objectReferenceValue = volume;
            so.FindProperty("fullscreenRow").objectReferenceValue = full;
            so.FindProperty("backRow").objectReferenceValue = back;
            so.ApplyModifiedPropertiesWithoutUndo();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            return settings;
        }

        // ------------------------------------------------------------------ yardımcılar

        private static GameObject FindIn(Scene scene, string path)
        {
            string[] parts = path.Split('/');
            foreach (GameObject r in scene.GetRootGameObjects())
            {
                if (r.name != parts[0]) continue;
                if (parts.Length == 1) return r;
                Transform t = r.transform.Find(string.Join("/", parts, 1, parts.Length - 1));
                if (t != null) return t.gameObject;
            }
            throw new System.Exception("[MenuIsoSceneBuilder] Bedroom.unity içinde bulunamadı: " + path);
        }

        private static SpriteRenderer Child(Transform parent, string name, Sprite sprite, Material mat, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sharedMaterial = mat;
            sr.sortingOrder = order;
            return sr;
        }

        private static void SetArray(SerializedObject so, string field, params Object[] values)
        {
            SerializedProperty p = so.FindProperty(field);
            p.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        private static Sprite Sprite(string name)
        {
            var s = AssetDatabase.LoadAssetAtPath<Sprite>(Art + name + ".png");
            if (s == null) throw new System.Exception("[MenuIsoSceneBuilder] Sprite yok: " + name);
            return s;
        }

        private static Sprite EqSprite(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(EqUi + name + ".png");

        private static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static RectTransform Stretch(RectTransform rt, float inset = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
            return rt;
        }

        // Sol-üst orijinli yerleşim (640x360 maket koordinatı)
        private static System.Action<RectTransform> TL(float x, float y, float w, float h) => rt =>
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(Mathf.Round(x), -Mathf.Round(y));
            rt.sizeDelta = new Vector2(w, h);
        };

        private static System.Action<RectTransform> Center(float cx, float cy, float w, float h) => rt => Center(rt, cx, cy, w, h);

        // Merkez noktası verilir; tek boyutlu kenarlar piksele otursun diye köşe tam sayıya yuvarlanır.
        private static void Center(RectTransform rt, float cx, float cy, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(Mathf.Round(cx - w / 2f), -Mathf.Round(cy - h / 2f));
            rt.sizeDelta = new Vector2(w, h);
        }

        private static Image Img(string name, Transform parent, Sprite sprite, System.Action<RectTransform> place)
        {
            RectTransform rt = Rect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.raycastTarget = false;
            place?.Invoke(rt);
            return img;
        }

        private static Image Sliced(string name, RectTransform parent, Sprite sprite, System.Action<RectTransform> place)
        {
            Image img = Img(name, parent, sprite, place);
            if (place == null) Stretch(img.rectTransform);
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = sprite != null ? 100f / sprite.pixelsPerUnit : 1f;
            return img;
        }

        private static Text Txt(string name, Transform parent, string text, int size, Color color, TextAnchor align, System.Action<RectTransform> place)
        {
            RectTransform rt = Rect(name, parent);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = font;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            t.supportRichText = false;
            place?.Invoke(rt);
            return t;
        }

        private static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out Color c);
            return c;
        }
    }
}
