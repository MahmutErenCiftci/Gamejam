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
// eski sabah sel sahneleri (Bedroom, Bathroom, FloodChapter) enabled:0 yapılır (silinmez).
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
        // ---- Rüzgârlı geçit (J sokağı → korunaklı avlu) ----
        var windPf = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/FloodEvening/FE_WindCorridor.prefab");
        if (windPf != null)
        {
            var w = (GameObject)PrefabUtility.InstantiatePrefab(windPf, scene);
            var wc = w.GetComponent<AfetUyanisi.Chapters.FloodEvening.WindCorridor>();
            var so = new SerializedObject(wc);
            var playerC = scene.GetRootGameObjects().Select(g => g.GetComponentInChildren<AfetUyanisi.Chapters.IsoPlayerController>(true)).FirstOrDefault(p => p != null);
            so.FindProperty("player").objectReferenceValue = playerC;
            var intro = FindDeep(world, "WindIntro"); var safe = FindDeep(world, "SafeZone");
            so.FindProperty("introArea").objectReferenceValue = intro != null ? intro.GetComponent<Collider2D>() : null;
            so.FindProperty("shelterArea").objectReferenceValue = safe != null ? safe.GetComponent<Collider2D>() : null;
            so.FindProperty("entry").objectReferenceValue = FindDeep(world, "WindEntry");
            so.FindProperty("exit").objectReferenceValue = FindDeep(world, "WindExit");
            so.FindProperty("shelterSpot").objectReferenceValue = FindDeep(world, "SafeSpot");
            so.ApplyModifiedPropertiesWithoutUndo();
            var fx = w.GetComponentInChildren<AfetUyanisi.Chapters.FloodEvening.WindFx>(true);
            if (fx != null && playerC != null) { var fso = new SerializedObject(fx); fso.FindProperty("follow").objectReferenceValue = playerC.transform; fso.ApplyModifiedPropertiesWithoutUndo(); }
            result.Log("rüzgâr: intro=" + (intro != null && intro.GetComponent<Collider2D>() != null) + " shelter=" + (safe != null) + " player=" + (playerC != null));
        }
        else result.Log("UYARI: FE_WindCorridor.prefab yok");
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
        var tree =AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/FloodEvening/FE_TreeFall.prefab");
        var stop = FindDeep(world, "TreeStopSpot");
        if (tree != null && stop != null) { var t = (GameObject)PrefabUtility.InstantiatePrefab(tree, scene); t.transform.position = stop.position; result.Log("FE_TreeFall yerleşti"); }
        else result.Log("FE_TreeFall henüz yok (Eren) — ağaç olayı atlanır");
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
        Sprites("debrisSprites", "FX_FE_Debris_Plank", "FX_FE_Debris_Bottle", "FX_FE_Debris_Twig", "FX_FE_Debris_Foam");
        var deb = so.FindProperty("debrisSprites");
        for (int i = 1; i <= 4; i++)
        {
            var leaf = Load<Sprite>("Assets/Art/FloodEvening/Wind/FX/FX_FE_Wind_Leaf" + i + ".png");
            if (leaf == null) continue;
            deb.arraySize++; deb.GetArrayElementAtIndex(deb.arraySize - 1).objectReferenceValue = leaf;
        }
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
    // MODULES_END

    static void UpdateBuildSettings(ExecutionResult result)
    {
        var list = EditorBuildSettings.scenes.ToList();
        string[] legacy = { "Assets/Scenes/Rooms/Bedroom.unity", "Assets/Scenes/Rooms/Bathroom.unity", "Assets/Scenes/Chapters/Flood/FloodChapter.unity" };
        foreach (var s in list) if (legacy.Contains(s.path)) s.enabled = false;
        var mine = list.FirstOrDefault(s => s.path == ScenePath);
        if (mine == null) list.Add(new EditorBuildSettingsScene(ScenePath, true));
        else mine.enabled = true;
        EditorBuildSettings.scenes = list.ToArray();
        result.Log("Build Settings: " + string.Join(", ", EditorBuildSettings.scenes.Select(s => System.IO.Path.GetFileNameWithoutExtension(s.path) + (s.enabled ? "" : "(kapalı)"))));
    }
}
