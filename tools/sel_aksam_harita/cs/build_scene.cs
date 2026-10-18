using AfetUyanisi.Chapters.FloodEvening;
using UnityEngine.Rendering;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;

// v3 (zikzak) — LEAD: Assets/Scenes/Chapters/Flood/FloodEvening.unity'yi baştan kurar (Additive yeni sahne → kaydet → kapat).
// Kullanıcının açık sahnelerine dokunmaz; aktif sahneyi geri verir. Build Settings: FloodEvening eklenir (enabled),
// diğer sahnelerin durumuna dokunulmaz.
internal class CommandScript : IRunCommand
{
    const string ScenePath = "Assets/Scenes/Chapters/Flood/FloodEvening.unity";
    const string EnvPrefab = "Assets/Prefabs/FloodEvening/EveningNeighborhood.prefab";
    const string PlaceholderPrefab = "Assets/Prefabs/Rooms/Street.prefab";

    static T Load<T>(string p) where T : Object => AssetDatabase.LoadAssetAtPath<T>(p);

    public void Execute(ExecutionResult result)
    {
        if (EditorApplication.isPlaying || EditorApplication.isCompiling) { result.LogError("BUSY playing/compiling"); return; }
        var existing = SceneManager.GetSceneByPath(ScenePath);
        if (existing.IsValid() && existing.isLoaded) { result.LogError("FloodEvening açık; önce kapatın (kullanıcı sahnesi olabilir)."); return; }

        var prevActive = SceneManager.GetActiveScene();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        try
        {
            Build(scene, result);
            EditorSceneManager.SaveScene(scene, ScenePath);
            result.Log("kaydedildi: " + ScenePath);
        }
        finally
        {
            if (prevActive.IsValid() && prevActive.isLoaded) SceneManager.SetActiveScene(prevActive);
            EditorSceneManager.CloseScene(scene, true);
        }
        UpdateBuildSettings(result);
        AssetDatabase.SaveAssets();
    }

    static GameObject Inst(string path, Scene scene, string name = null)
    {
        var prefab = Load<GameObject>(path);
        if (prefab == null) return null;
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        if (name != null) go.name = name;
        return go;
    }

    static Transform FindDeep(Transform root, string name)
    {
        if (root == null) return null;
        foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
        return null;
    }

    static void Build(Scene scene, ExecutionResult result)
    {
        // --- Kamera (PixelPerfect 640x360 → GameCameraSettings 480x270'e çeker, ChapterCamera/backdrop ekler) ---
        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 2.8125f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.03f, 0.05f, 0.1f, 1f);
        cam.nearClipPlane = -10f; cam.farClipPlane = 100f;
        camGo.transform.position = new Vector3(0f, 0f, -10f);
        camGo.AddComponent<AudioListener>();
        var ppc = camGo.AddComponent<PixelPerfectCamera>();
        ppc.assetsPPU = 64; ppc.refResolutionX = 640; ppc.refResolutionY = 360;
        cam.GetUniversalAdditionalCameraData();

        // --- Dünya ---
        GameObject world = Inst(EnvPrefab, scene, "World");
        bool placeholder = world == null;
        if (placeholder)
        {
            world = new GameObject("World");
            var street = Inst(PlaceholderPrefab, scene);
            street.transform.SetParent(world.transform, false);
            // Geçici çapalar (ENV prefabı gelene kadar)
            var anchors = new GameObject("PlaceholderAnchors").transform;
            anchors.SetParent(world.transform, false);
            Trigger(anchors, "HighRoad", new Vector2(1.6f, 2.9f), new Vector2(1.2f, 0.8f));
            Trigger(anchors, "SafeZone", new Vector2(10.6f, 6.9f), new Vector2(1.4f, 0.9f));
            var iv = new GameObject("IntroView").transform; iv.SetParent(anchors, false); iv.position = new Vector3(5f, 4.2f, 0f);
            result.Log("UYARI: " + EnvPrefab + " yok → geçici Street.prefab + çapalar");
        }

        // --- Akşam ışığı (global, lacivert) ---
        bool envGlobal = world.GetComponentsInChildren<Light2D>(true).Any(l => l.lightType == Light2D.LightType.Global);
        if (!envGlobal)
        {
        var lightGo = new GameObject("Global Light 2D");
        var light = lightGo.AddComponent<Light2D>();
        light.lightType = Light2D.LightType.Global;
        light.color = new Color(0.62f, 0.68f, 0.92f, 1f);
        light.intensity = 0.72f;
        }
        else result.Log("global ışık ENV prefabında (GlobalEvening) → sahneye ayrıca eklenmedi");

        // WaterAreas → çevresel su yükselişi
        var water = FindDeep(world.transform, "WaterAreas");
        if (water != null && water.GetComponent<AfetUyanisi.Chapters.FloodEvening.FloodEveningWaterRise>() == null)
            water.gameObject.AddComponent<AfetUyanisi.Chapters.FloodEvening.FloodEveningWaterRise>();

        // --- Cenk ---
        var spawn = FindDeep(world.transform, "Spawn");
        var cenk = Inst("Assets/Prefabs/Characters/Cenk.prefab", scene, "Cenk");
        if (spawn != null) cenk.transform.position = spawn.position;

        // --- Yağmur ---
        var rain = Inst("Assets/Prefabs/FX/Rain.prefab", scene, "Rain");

        // --- Diyalog (Arda'nın DialogueRunner'ı + bölüm adaptörü) ---
        var dlg = Inst("Assets/Prefabs/Dialogue/DialogueRunner.prefab", scene, "DialogueRunner");
        var runner = dlg.GetComponentInChildren<AfetUyanisi.Dialogue.DialogueRunner>(true);
        var story = Load<TextAsset>("Assets/Data/Story/flood_evening.json");
        if (runner != null && story != null)
        {
            var so = new SerializedObject(runner);
            so.FindProperty("storyJson").objectReferenceValue = story;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        else result.Log("UYARI: flood_evening.json ya da DialogueRunner yok (diyaloglar Console'a yazılır)");
        var adapterGo = new GameObject("DialogueAdapter");
        var adapter = adapterGo.AddComponent<AfetUyanisi.Chapters.DialogueRunnerAdapter>();
        { var so = new SerializedObject(adapter); so.FindProperty("runner").objectReferenceValue = runner; so.ApplyModifiedPropertiesWithoutUndo(); }

        // --- Bölüm akışı ---
        var managers = new GameObject("Managers");
        var ctrl = managers.AddComponent<AfetUyanisi.Chapters.FloodEvening.FloodEveningController>();
        managers.AddComponent<AfetUyanisi.Chapters.Rooms.RoomInteractor>();
        // Arayüzler ayrı prefablarda (Berk devralır): Assets/Prefabs/FloodEvening/UI/
        var hud = Inst("Assets/Prefabs/FloodEvening/UI/FE_Hud.prefab", scene).GetComponent<AfetUyanisi.Chapters.FloodEvening.FloodEveningHud>();
        var card = Inst("Assets/Prefabs/FloodEvening/UI/FE_TitleCard.prefab", scene).GetComponent<AfetUyanisi.Chapters.FloodEvening.FloodEveningTitleCard>();
        var res = Inst("Assets/Prefabs/FloodEvening/UI/FE_ResultView.prefab", scene).GetComponent<AfetUyanisi.Chapters.FloodEvening.FloodEveningResultView>();
        {
            var so = new SerializedObject(ctrl);
            so.FindProperty("player").objectReferenceValue = cenk.GetComponent<AfetUyanisi.Chapters.IsoPlayerController>();
            so.FindProperty("spawn").objectReferenceValue = spawn;
            so.FindProperty("rain").objectReferenceValue = rain != null ? rain.GetComponentInChildren<AfetUyanisi.Chapters.Flood.RainController>(true) : null;
            so.FindProperty("dialogue").objectReferenceValue = adapter;
            so.FindProperty("hud").objectReferenceValue = hud;
            so.FindProperty("titleCard").objectReferenceValue = card;
            so.FindProperty("resultView").objectReferenceValue = res;
            so.FindProperty("storyJson").objectReferenceValue = story;
            var cb = FindDeep(world.transform, "CameraBounds");
            if (cb != null) so.FindProperty("cameraBounds").objectReferenceValue = cb.GetComponent<AfetUyanisi.Chapters.CameraBounds>();
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // --- Modüller (EVENTS / PUZZLE prefabları; ready_*.txt'ye göre) ---
        PlaceModules(scene, world.transform, result);

        // --- EventSystem (fare tıklamaları) ---
        var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

        result.Log("kuruldu: dünya=" + (placeholder ? "GEÇİCİ Street" : EnvPrefab) + " spawn=" + (spawn ? spawn.position.ToString() : "YOK") +
                   " cameraBounds=" + (FindDeep(world.transform, "CameraBounds") != null) + " su=" + (water != null));
    }

    static void Trigger(Transform parent, string name, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        var box = go.AddComponent<BoxCollider2D>();
        box.isTrigger = true;
        box.size = size;
    }

    static GameObject PlaceEv(Scene scene, string prefab, Transform at, Vector3 fallback, ExecutionResult result)
    {
        var pf = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/FloodEvening/" + prefab + ".prefab");
        if (pf == null) { result.Log("UYARI: modül prefabı yok: " + prefab); return null; }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(pf, scene);
        go.transform.position = at != null ? at.position : fallback;
        return go;
    }

    static void AddGo(SerializedObject so, string prop, Transform t)
    {
        if (t == null) return;
        var arr = so.FindProperty(prop);
        arr.arraySize++;
        arr.GetArrayElementAtIndex(arr.arraySize - 1).objectReferenceValue = t.gameObject;
    }

    // MODULES_BEGIN (zikzak v3: uyarı + I sokağında küçük dal; ağaç olayı Eren'in FE_TreeFall'ı gelince TreeStopSpot'a konur)
    static void PlaceModules(Scene scene, Transform world, ExecutionResult result)
    {
        PlaceEv(scene, "FE_WarningPhone", null, Vector3.zero, result);
        var spot = FindDeep(world, "BranchSpot");
        var br = PlaceEv(scene, "FE_BranchObstacle", spot, new Vector3(17.75f, 16.6f, 0f), result);
        if (br != null)
        {
            var comp = br.GetComponent<AfetUyanisi.Chapters.FloodEvening.BranchObstacle>();
            // Geçici: Arda'nın "art arda bas" kipi gelene kadar küçük dal kipi (basılı tut). Alternatif geçit görselleri kapalı.
            foreach (var n in new[] { "AltPassage", "AltSign", "Chain_Closed", "Chain_Open" }) { var t = FindDeep(br.transform, n); if (t != null) t.gameObject.SetActive(false); }
            var branch = FindDeep(br.transform, "Branch");
            // Yol kesici: I sokağını (gy 13..18) gx 50.7..51.3 boyunca kapatan çokgen (dünya → yerel)
            var blockGo = new GameObject("BranchBlock");
            blockGo.transform.SetParent(br.transform, false);
            var poly = blockGo.AddComponent<PolygonCollider2D>();
            Vector2 W(float gx, float gy) => new Vector2((gx - gy) / 2f, (gx + gy) / 4f);
            var pts = new[] { W(50.7f, 12.9f), W(51.4f, 12.9f), W(51.4f, 18.1f), W(50.7f, 18.1f) };
            for (int i = 0; i < pts.Length; i++) pts[i] = (Vector2)blockGo.transform.InverseTransformPoint(pts[i]);
            poly.SetPath(0, pts);
            var so = new SerializedObject(comp);
            so.FindProperty("mode").enumValueIndex = 1; // SmallBranch
            var bl = so.FindProperty("blockers"); bl.arraySize = 0;
            bl.arraySize = 1; bl.GetArrayElementAtIndex(0).objectReferenceValue = poly;
            if (branch != null) foreach (var c in branch.GetComponentsInChildren<Collider2D>(true)) { if (c.isTrigger) continue; bl.arraySize++; bl.GetArrayElementAtIndex(bl.arraySize - 1).objectReferenceValue = c; }
            so.FindProperty("showWhenOpen").arraySize = 0;
            var hw = so.FindProperty("hideWhenOpen"); hw.arraySize = 0;
            if (branch != null) { hw.arraySize = 1; hw.GetArrayElementAtIndex(0).objectReferenceValue = branch.gameObject; }
            so.ApplyModifiedPropertiesWithoutUndo();
            if (branch != null) branch.position = W(51.05f, 15.5f);
            result.Log("dal: " + br.transform.position + " blockers=" + bl.arraySize);
        }
        // (18 Eki, FLOOD-MECH) rüzgâr koridoru (FE_WindCorridor) kaldırıldı: J sokağı normal sokak.
        // ===== APARTMAN FİNALİ (final-world agent) =====
        // Avlunun arkasındaki apartman girişi: FE_ApartmentFinal (kapı, posta kutusu, kilit, sinematik; kök = AptFacade çapası) +
        // FE_ApartmentLockUi (UI ajanı: mektup + kilit yakın planları). Controller finali (IFloodEveningFinale) ve [E] modüllerini Awake'te bulur.
        {
            var aptPf = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/FloodEvening/FE_ApartmentFinal.prefab");
            var uiPf = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/FloodEvening/FE_ApartmentLockUi.prefab");
            var aptAt = FindDeep(world, "AptFacade");
            var uiGo = uiPf != null ? (GameObject)PrefabUtility.InstantiatePrefab(uiPf, scene) : null;
            if (aptPf != null && aptAt != null)
            {
                var apt = (GameObject)PrefabUtility.InstantiatePrefab(aptPf, scene);
                apt.transform.position = aptAt.position;
                var fin = apt.GetComponent<AfetUyanisi.Chapters.FloodEvening.ApartmentFinale>();
                var so = new SerializedObject(fin);
                so.FindProperty("player").objectReferenceValue = scene.GetRootGameObjects().Select(g => g.GetComponentInChildren<AfetUyanisi.Chapters.IsoPlayerController>(true)).FirstOrDefault(p => p != null);
                if (uiGo != null) so.FindProperty("ui").objectReferenceValue = uiGo.GetComponent<AfetUyanisi.Chapters.FloodEvening.ApartmentFinalUi>();
                so.ApplyModifiedPropertiesWithoutUndo();
                result.Log("apartman finali: " + apt.transform.position + " kilit/mektup arayüzü=" + (uiGo != null));
            }
            else result.Log("UYARI: FE_ApartmentFinal.prefab ya da AptFacade çapası yok — apartman finali atlanır");
        }
        // ===== /APARTMAN FİNALİ =====
        PlaceFlood(scene, world, result); // 120 sn sel: harita suyu, yol kapanmaları, SEL DURUMU göstergesi
        PlaceEnvironment(scene, world, result); // ağaç salınımı, şimşek, yüzen enkaz, elektrikli su bölgeleri (FLOOD-MECH)
        // (P-FLOOD 18 Eki) ağaç düşüşü (Eren'in FE_TreeFall'ı, I sokağını tamamen kesen yatık gövde) + ritimle it/çek (dal engeli Rhythm kipi),
        // 80 sn sel / final hızı, isabet parlaması malzemesi — ayrıntılar PFloodTreeAndRhythm'de (scratch patch_scene.cs ile aynı).
        PFloodTreeAndRhythm(scene, AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/FloodEvening/ENV/Hazards/FE_SpriteFlash.mat"), result);
    }

    // ---- 120 sn sel sistemi (LEAD): FloodWaterField (su katmanı + kapanan yollar) ve SEL DURUMU göstergesinin piksel fontu ----
    static void PlaceFlood(Scene scene, Transform world, ExecutionResult result)
    {
        const string Dir = "Assets/Art/FloodEvening/ENV/Flood/";
        var go = new GameObject("FloodWater");
        SceneManager.MoveGameObjectToScene(go, scene);
        var field = go.AddComponent<AfetUyanisi.Chapters.FloodEvening.FloodWaterField>();
        var so = new SerializedObject(field);
        so.FindProperty("waterMaterial").objectReferenceValue = Load<Material>(Dir + "FE_FloodWater.mat");
        so.FindProperty("floodData").objectReferenceValue = Load<Texture2D>(Dir + "FE_FloodData.png");
        so.FindProperty("mapRoot").objectReferenceValue = world;
        var playerC = scene.GetRootGameObjects().Select(g => g.GetComponentInChildren<AfetUyanisi.Chapters.IsoPlayerController>(true)).FirstOrDefault(p => p != null);
        so.FindProperty("player").objectReferenceValue = playerC;
        void Sprites(string prop, params string[] names)
        {
            var arr = so.FindProperty(prop); arr.arraySize = names.Length;
            for (int i = 0; i < names.Length; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = Load<Sprite>(Dir + names[i] + ".png");
        }
        Sprites("rippleFrames", "FX_FE_Ripple_0", "FX_FE_Ripple_1", "FX_FE_Ripple_2", "FX_FE_Ripple_3");
        Sprites("splashFrames", "FX_FE_Splash_0", "FX_FE_Splash_1", "FX_FE_Splash_2");
        // arka plan süsü: köpük + yapraklar (çarpışan tahta/kasa/şişe/dal ayrı: FloodDebrisField, PlaceEnvironment)
        Sprites("debrisSprites", "FX_FE_Debris_Foam", "FX_FE_Debris_Leaf1", "FX_FE_Debris_Leaf2", "FX_FE_Debris_Leaf3", "FX_FE_Debris_Leaf4");
        var deb = so.FindProperty("debrisSprites");
        so.FindProperty("feetWater").objectReferenceValue = Load<Sprite>(Dir + "FX_FE_FeetWater.png");
        so.FindProperty("barrierSprite").objectReferenceValue = Load<Sprite>(Dir + "PROP_FE_FloodBarrier.png");
        // Kapanan yollar (ızgara): kısa yol girişi %42'de, geri dönüş sokağı (C → kavşak) %75'te. Oyuncu kapının arkasındaysa kapanmaz.
        var cl = so.FindProperty("closures"); cl.arraySize = 2;
        void Closure(int i, string id, Vector4 gate, Vector2 at, float th, Vector4[] forbid, string title, string body)
        {
            var e = cl.GetArrayElementAtIndex(i);
            e.FindPropertyRelative("id").stringValue = id;
            e.FindPropertyRelative("gate").vector4Value = gate;
            e.FindPropertyRelative("barrierAt").vector2Value = at;
            e.FindPropertyRelative("threshold").floatValue = th;
            var f = e.FindPropertyRelative("forbidIfPlayerIn"); f.arraySize = forbid.Length;
            for (int k = 0; k < forbid.Length; k++) f.GetArrayElementAtIndex(k).vector4Value = forbid[k];
            e.FindPropertyRelative("toastTitle").stringValue = title;
            e.FindPropertyRelative("toastBody").stringValue = body;
        }
        Closure(0, "short_road", new Vector4(25f, 9.5f, 30f, 10.1f), new Vector2(27.5f, 10.7f), 0.42f,
            new[] { new Vector4(25f, 2f, 30f, 10.1f), new Vector4(30f, 2f, 34f, 7f) },
            "KISA YOL SU ALTINDA", "Kısa yol tamamen suyla kapandı. Uzun yoldan devam et.");
        Closure(1, "back_street", new Vector4(22.4f, 12f, 23f, 17f), new Vector2(23.5f, 14.5f), 0.75f,
            new[] { new Vector4(-10f, 0f, 16f, 5f), new Vector4(11f, 5f, 16f, 17f), new Vector4(16f, 12f, 23f, 17f) },
            "GERİ DÖNÜŞ YOLU KAPANDI", "Geldiğin sokak su altında. İleriye, güvenli bölgeye git.");
        so.ApplyModifiedPropertiesWithoutUndo();

        // SEL DURUMU göstergesi: HUD objesine eklenir, Pixelify piksel fontu atanır
        var hud = scene.GetRootGameObjects().Select(g => g.GetComponentInChildren<AfetUyanisi.Chapters.FloodEvening.FloodEveningHud>(true)).FirstOrDefault(h => h != null);
        if (hud != null)
        {
            var gauge = hud.GetComponent<AfetUyanisi.Chapters.FloodEvening.FloodGaugeView>();
            if (gauge == null) gauge = hud.gameObject.AddComponent<AfetUyanisi.Chapters.FloodEvening.FloodGaugeView>();
            var gso = new SerializedObject(gauge);
            gso.FindProperty("font").objectReferenceValue = Load<Font>("Assets/Fonts/PixelifySans/PixelifySans-Regular.ttf");
            gso.ApplyModifiedPropertiesWithoutUndo();
            var hso = new SerializedObject(hud);
            hso.FindProperty("gauge").objectReferenceValue = gauge;
            hso.ApplyModifiedPropertiesWithoutUndo();
        }
        result.Log("sel suyu: malzeme=" + (so.FindProperty("waterMaterial").objectReferenceValue != null) + " veri=" + (so.FindProperty("floodData").objectReferenceValue != null) +
                   " enkaz=" + deb.arraySize + " gösterge=" + (hud != null));
    }
    // ===== ÇEVRE TEHLİKELERİ (FLOOD-MECH, 18 Eki) =====
    // Sprite'lar: tools/sel_aksam_harita/hazards.js → Assets/Art/FloodEvening/ENV/Hazards + ENV/Trees. Çapalar: layout.js (World/Hazards/Zone_n,
    // World/Debris/<Tür>_<n>). Hepsi FloodEveningHub.Progress01'i okur; Hub.RequestReset'te sıfırlanır, sayaç durunca durur.
    static Sprite[] SheetSprites(string path)
    {
        return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>()
            .OrderBy(s => { int u = s.name.LastIndexOf('_'); int k; return u >= 0 && int.TryParse(s.name.Substring(u + 1), out k) ? k : 0; }).ToArray();
    }

    static void PlaceEnvironment(Scene scene, Transform world, ExecutionResult result)
    {
        const string Hz = "Assets/Art/FloodEvening/ENV/Hazards/", Tr = "Assets/Art/FloodEvening/ENV/Trees/";
        var env = new GameObject("FloodEnvironment");
        SceneManager.MoveGameObjectToScene(env, scene);
        var playerC = scene.GetRootGameObjects().Select(g => g.GetComponentInChildren<AfetUyanisi.Chapters.IsoPlayerController>(true)).FirstOrDefault(p => p != null);
        var field = scene.GetRootGameObjects().Select(g => g.GetComponentInChildren<AfetUyanisi.Chapters.FloodEvening.FloodWaterField>(true)).FirstOrDefault(f => f != null);

        var reaction = env.AddComponent<AfetUyanisi.Chapters.FloodEvening.FloodPlayerReaction>();
        { var so = new SerializedObject(reaction); so.FindProperty("player").objectReferenceValue = playerC; so.ApplyModifiedPropertiesWithoutUndo(); }

        // ağaç salınımı
        var sway = env.AddComponent<AfetUyanisi.Chapters.FloodEvening.FloodTreeSway>();
        int nSets;
        {
            var so = new SerializedObject(sway);
            so.FindProperty("mapRoot").objectReferenceValue = world;
            var trees = new[] { ("Tree_Round", "Assets/Art/Props/Street/Surroundings/PROP_StreetBG_Tree_Round.png"), ("Tree_Small", "Assets/Art/Props/Street/Surroundings/PROP_StreetBG_Tree_Small.png"),
                ("Tree_Pine", "Assets/Art/Props/Street/Surroundings/PROP_StreetBG_Tree_Pine.png"), ("Street_Tree", "Assets/Art/Props/Street/PROP_Street_Tree.png"), ("Bush", "Assets/Art/Props/Street/Surroundings/PROP_StreetBG_Bush.png") };
            var sets = so.FindProperty("sets"); sets.arraySize = trees.Length; nSets = 0;
            for (int i = 0; i < trees.Length; i++)
            {
                var e = sets.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("source").objectReferenceValue = AssetDatabase.LoadAllAssetsAtPath(trees[i].Item2).OfType<Sprite>().FirstOrDefault();
                foreach (var (prop, tag) in new[] { ("calm", "Calm"), ("storm", "Storm") })
                {
                    var fr = SheetSprites(Tr + "FX_FE_Sway_" + trees[i].Item1 + "_" + tag + ".png");
                    var arr = e.FindPropertyRelative(prop); arr.arraySize = fr.Length;
                    for (int k = 0; k < fr.Length; k++) arr.GetArrayElementAtIndex(k).objectReferenceValue = fr[k];
                    if (prop == "calm" && fr.Length == 8) nSets++;
                }
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // şimşek
        var lightning = env.AddComponent<AfetUyanisi.Chapters.FloodEvening.FloodLightning>();
        {
            var so = new SerializedObject(lightning);
            so.FindProperty("sky").objectReferenceValue = FindDeep(world, "Sky");
            var bolts = SheetSprites(Hz + "FX_FE_SkyBolt.png");
            var arr = so.FindProperty("boltSprites"); arr.arraySize = bolts.Length;
            for (int k = 0; k < bolts.Length; k++) arr.GetArrayElementAtIndex(k).objectReferenceValue = bolts[k];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // yüzen enkaz
        var debris = env.AddComponent<AfetUyanisi.Chapters.FloodEvening.FloodDebrisField>();
        int nDebris = 0;
        {
            var so = new SerializedObject(debris);
            so.FindProperty("water").objectReferenceValue = field;
            so.FindProperty("reaction").objectReferenceValue = reaction;
            var root = FindDeep(world, "Debris");
            var items = so.FindProperty("items"); items.arraySize = 0;
            var spr = new System.Collections.Generic.Dictionary<string, string> { { "Plank", "PROP_FE_Float_Plank" }, { "Crate", "PROP_FE_Float_Crate" }, { "Bottle", "PROP_FE_Float_Bottle" },
                { "Branch", "PROP_FE_Float_Branch" }, { "Stone", "PROP_FE_Stone_A" }, { "Rubble", "PROP_FE_Rubble_S" } };
            if (root != null)
                foreach (Transform a in root)
                {
                    string kind = a.name.Split('_')[0];
                    if (!spr.ContainsKey(kind)) continue;
                    string file = spr[kind];
                    if (kind == "Stone" && (nDebris % 2) == 1) file = "PROP_FE_Stone_B";
                    items.arraySize++;
                    var e = items.GetArrayElementAtIndex(items.arraySize - 1);
                    e.FindPropertyRelative("kind").enumValueIndex = (int)System.Enum.Parse(typeof(AfetUyanisi.Chapters.FloodEvening.FloodDebrisField.DebrisKind), kind);
                    e.FindPropertyRelative("anchor").objectReferenceValue = a;
                    e.FindPropertyRelative("sprite").objectReferenceValue = Load<Sprite>(Hz + file + ".png");
                    nDebris++;
                }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // elektrikli su bölgeleri
        var power = env.AddComponent<AfetUyanisi.Chapters.FloodEvening.FloodElectricHazards>();
        int nZones = 0;
        {
            var so = new SerializedObject(power);
            so.FindProperty("water").objectReferenceValue = field;
            so.FindProperty("reaction").objectReferenceValue = reaction;
            void Arr(string prop, Sprite[] s) { var a = so.FindProperty(prop); a.arraySize = s.Length; for (int k = 0; k < s.Length; k++) a.GetArrayElementAtIndex(k).objectReferenceValue = s[k]; }
            Arr("sparkFrames", SheetSprites(Hz + "FX_FE_Spark.png"));
            Arr("arcFrames", SheetSprites(Hz + "FX_FE_Arc.png"));
            so.FindProperty("warnIcon").objectReferenceValue = Load<Sprite>(Hz + "UI_FE_PowerWarn.png");
            var root = FindDeep(world, "Hazards");
            var zs = so.FindProperty("zones"); zs.arraySize = 0;
            if (root != null)
                foreach (Transform z in root)
                {
                    if (!z.name.StartsWith("Zone_")) continue;
                    zs.arraySize++;
                    var e = zs.GetArrayElementAtIndex(zs.arraySize - 1);
                    e.FindPropertyRelative("id").stringValue = z.name;
                    e.FindPropertyRelative("contact").objectReferenceValue = z;
                    // hazards_def.js: core 0,4 / water 1,0 hücre → zemin birimi (1 hücre = 1/√2)
                    e.FindPropertyRelative("coreRadius").floatValue = 0.3f;
                    e.FindPropertyRelative("waterRadius").floatValue = 0.72f;
                    var safe = z.Cast<Transform>().Where(c => c.name.StartsWith("Safe_")).ToArray();
                    var sp = e.FindPropertyRelative("safePoints"); sp.arraySize = safe.Length;
                    for (int k = 0; k < safe.Length; k++) sp.GetArrayElementAtIndex(k).objectReferenceValue = safe[k];
                    nZones++;
                }
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        result.Log("çevre: ağaç seti=" + nSets + " şimşek gök=" + (FindDeep(world, "Sky") != null) + " enkaz=" + nDebris + " elektrik bölgesi=" + nZones +
                   " su=" + (field != null) + " oyuncu=" + (playerC != null));
    }
    // ===== /ÇEVRE TEHLİKELERİ =====
    // MODULES_END

    static void UpdateBuildSettings(ExecutionResult result)
    {
        // (18 Eki) Sahne listesinin geri kalanı GameFlow/FLOW'un: burada yalnız FloodEvening'in listede ve açık olduğu sağlanır
        // (eski sabah sel sahneleri artık kapatılmaz; okul + köprü başlangıcı FloodChapter'dan türetilen sahneyi kullanır).
        var list = EditorBuildSettings.scenes.ToList();
        var mine = list.FirstOrDefault(s => s.path == ScenePath);
        if (mine == null) list.Add(new EditorBuildSettingsScene(ScenePath, true));
        else mine.enabled = true;
        EditorBuildSettings.scenes = list.ToArray();
        result.Log("Build Settings: " + string.Join(", ", EditorBuildSettings.scenes.Select(s => System.IO.Path.GetFileNameWithoutExtension(s.path) + (s.enabled ? "" : "(kapalı)"))));
    }

    // ===== P-FLOOD (18 Eki): ağaç + ritim =====
    static Vector2 PfW(float gx, float gy) => new Vector2((gx - gy) / 2f, (gx + gy) / 4f);
        static T PfFind<T>(Scene s) where T : Component => s.GetRootGameObjects().Select(g => g.GetComponentInChildren<T>(true)).FirstOrDefault(c => c != null);

    static PolygonCollider2D PfPoly(Transform parent, string name, params Vector2[] world)
    {
        var old = parent.Find(name);
        if (old != null) Object.DestroyImmediate(old.gameObject);
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var poly = go.AddComponent<PolygonCollider2D>();
        poly.SetPath(0, world.Select(p => (Vector2)go.transform.InverseTransformPoint(p)).ToArray());
        return poly;
    }

    static void PFloodTreeAndRhythm(Scene scene, Material flashMat, ExecutionResult result)
    {
        var world = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "World")?.transform;
        if (world == null) { result.LogError("World yok"); return; }

        // ---- 80 sn sel + final hızı
        var ctrl = PfFind<FloodEveningController>(scene);
        { var so = new SerializedObject(ctrl); so.FindProperty("floodSeconds").floatValue = 80f; so.FindProperty("finaleRate").floatValue = 0.15f; so.ApplyModifiedPropertiesWithoutUndo(); }

        // ---- isabet parlaması
        var react = PfFind<FloodPlayerReaction>(scene);
        if (react != null) { var so = new SerializedObject(react); so.FindProperty("flashMaterial").objectReferenceValue = flashMat; so.ApplyModifiedPropertiesWithoutUndo(); }

        // ---- ağaç: kök (kütük) I sokağının ön kenarının dışında (ızgara ~50.7, 11.8), aynalı (sola, sokağın üstüne devrilir);
        // yatan gövde tam +gy ekseninde gx = 51'de gy 12.75 → 18.4 boyunca uzanır (sokak gy 13..18).
        var treePf = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/FloodEvening/FE_TreeFall.prefab");
        var tree = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "FE_TreeFall");
        if (tree == null) { tree = (GameObject)PrefabUtility.InstantiatePrefab(treePf, scene); tree.name = "FE_TreeFall"; }
        var ev = tree.GetComponent<FloodTreeFallEvent>();
        Vector2 brokenEnd = PfW(51f, 12.75f);
        tree.transform.position = brokenEnd - new Vector2(-0.3f, 0.314375f); // Fallen yerel (0.3, 0.314), aynalı
        tree.transform.localScale = new Vector3(-1f, 1f, 1f);
        var fallen = FindDeep(tree.transform, "Fallen");
        fallen.localRotation = Quaternion.Euler(0f, 0f, 12.6f); // aynalı: dünyada saat yönünde 12.6° → gövde 153.4° (= +gy ekseni)
        var sg = tree.GetComponent<SortingGroup>();
        if (sg != null) sg.sortingOrder = -45; // binaların (-50) önünde, Cenk/sokak eşyalarının (0) arkasında
        foreach (var n in new[] { "TrunkBlock", "FallZoneBlock", "TreeStopSpot" })
        { var t = FindDeep(tree.transform, n); if (t != null) foreach (var c in t.GetComponents<Collider2D>()) c.enabled = false; }
        var trunk = PfPoly(fallen, "TreeBlock_FE", PfW(50.55f, 12.4f), PfW(51.45f, 12.4f), PfW(51.45f, 18.7f), PfW(50.55f, 18.7f));
        var fallZone = PfPoly(tree.transform, "FallZoneBlock_FE", PfW(49.6f, 12.6f), PfW(52.4f, 12.6f), PfW(52.4f, 18.4f), PfW(49.6f, 18.4f));
        Collider2D stop = null;
        foreach (var t in world.GetComponentsInChildren<Transform>(true)) if (t.name == "TreeStopSpot") { stop = t.GetComponent<Collider2D>(); if (stop != null) break; }

        // ---- dal engeli → ritim (Rhythm): itilen = yatan gövde (Fallen), etkileşim noktası gövdenin solunda
        var br = scene.GetRootGameObjects().FirstOrDefault(g => g.GetComponent<BranchObstacle>() != null);
        if (br == null) { result.LogError("FE_BranchObstacle yok"); return; }
        br.transform.position = PfW(49.9f, 15.5f);
        var oldBlock = br.transform.Find("BranchBlock"); if (oldBlock != null) Object.DestroyImmediate(oldBlock.gameObject);
        foreach (var n in new[] { "Branch", "AltPassage", "AltSign", "Chain_Closed", "Chain_Open" }) { var t = FindDeep(br.transform, n); if (t != null) t.gameObject.SetActive(false); }
        var inter = br.GetComponent<AfetUyanisi.Chapters.Interactable>();
        if (inter != null) { var so = new SerializedObject(inter); so.FindProperty("useRadius").floatValue = 1.8f; so.ApplyModifiedPropertiesWithoutUndo(); }
        var view = br.GetComponent<FloodRhythmView>();
        if (view == null) view = br.AddComponent<FloodRhythmView>();
        {
            var so = new SerializedObject(view);
            so.FindProperty("font").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/PixelOperator/PixelOperatorTR-Bold.ttf");
            so.FindProperty("panelSprite").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Earthquake/QuestHUD/UI_EQ_QuestPanel.png");
            so.FindProperty("barBackSprite").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Earthquake/UI_EQ_HoldBar_Back.png");
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        var bo = br.GetComponent<BranchObstacle>();
        {
            var so = new SerializedObject(bo);
            so.FindProperty("mode").enumValueIndex = 3; // Rhythm
            so.FindProperty("startArmed").boolValue = false;
            so.FindProperty("branchRoot").objectReferenceValue = fallen;
            so.FindProperty("rhythmView").objectReferenceValue = view;
            so.FindProperty("requiredBeats").intValue = 6;
            so.FindProperty("beatSeconds").floatValue = 0.75f;
            so.FindProperty("windowHalf").floatValue = 0.32f;
            so.FindProperty("clearOffsetWorld").vector2Value = PfW(0f, 2f) - PfW(0f, 0f); // +gy yönünde 2 hücre (sokağın arka kenarına)
            so.FindProperty("blockers").arraySize = 0;
            so.FindProperty("showWhenOpen").arraySize = 0;
            so.FindProperty("hideWhenOpen").arraySize = 0;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---- ağaç olayı ayarları
        {
            var so = new SerializedObject(ev);
            so.FindProperty("stopZone").objectReferenceValue = stop;
            so.FindProperty("fallenAngle").floatValue = -63.4f;
            so.FindProperty("fallSeconds").floatValue = 0.8f;
            so.FindProperty("startledPose").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Characters/Cenk/Poses/CHR_Cenk_Startled_SouthEast.png");
            so.FindProperty("impactFrom").vector2Value = new Vector2(0.6f, 0.3f);
            so.FindProperty("impactTo").vector2Value = new Vector2(2.92f, 1.46f);
            so.FindProperty("fallZoneBlock").objectReferenceValue = fallZone;
            var ci = so.FindProperty("closeOnImpact"); ci.arraySize = 1; ci.GetArrayElementAtIndex(0).objectReferenceValue = trunk;
            { var bso = new SerializedObject(bo); bso.FindProperty("treeFx").objectReferenceValue = ev; bso.ApplyModifiedPropertiesWithoutUndo(); }
            var ao = so.FindProperty("armOnFallen"); ao.arraySize = 1; ao.GetArrayElementAtIndex(0).objectReferenceValue = br;
            so.FindProperty("crackSfx").stringValue = "SFX_FE_Tree_Crack";
            so.FindProperty("creakSfx").stringValue = "";
            so.FindProperty("fallSfx").stringValue = "SFX_FE_Tree_Fall";
            so.FindProperty("impactSfx").stringValue = "";
            so.FindProperty("impactSfx2").stringValue = "";
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        result.Log($"ağaç kök={tree.transform.position} gövde ucu={brokenEnd} dur={(stop != null ? stop.name : "YOK")} dal={br.transform.position} " +
                   $"sel={ctrl.FloodSeconds} parlama={(react != null)}");
    }
    // ===== /P-FLOOD =====

}
