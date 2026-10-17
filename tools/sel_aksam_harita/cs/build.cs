using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering.Universal;
// ENV: Assets/Prefabs/FloodEvening/EveningNeighborhood.prefab'ı build_data.txt'den baştan kurar (yalnız bu prefab).
internal class CommandScript : IRunCommand
{
    static readonly string Data = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "tools/sel_aksam_harita/build_data.txt");
    const string PrefabPath = "Assets/Prefabs/FloodEvening/EveningNeighborhood.prefab";
    static float F(string s) => float.Parse(s, CultureInfo.InvariantCulture);
    static Transform root;
    static Transform Node(string path)
    {
        var t = root;
        foreach (var part in path.Split('/'))
        {
            if (part.Length == 0) continue;
            var c = t.Find(part);
            if (c == null) { var go = new GameObject(part); go.transform.SetParent(t, false); c = go.transform; }
            t = c;
        }
        return t;
    }
    static Vector2[] Pts(string s, Transform local)
    {
        return s.Split(';').Select(p => { var a = p.Split(','); var w = new Vector3(F(a[0]), F(a[1]), 0f); var l = local.InverseTransformPoint(w); return new Vector2(l.x, l.y); }).ToArray();
    }
    public void Execute(ExecutionResult result)
    {
        if (EditorApplication.isPlaying || EditorApplication.isCompiling) { result.Log("BUSY - abort"); return; }
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var lines = File.ReadAllLines(Data);
        // import settings check/fix for our own textures
        int fixedImp = 0;
        foreach (var l in lines)
        {
            var f = l.Split('|');
            string p = f[0] == "SPR" ? f[3] : f[0] == "SKY" ? f[1] : null;
            if (p == null || !p.StartsWith("Assets/Art/FloodEvening/ENV/")) continue;
            var imp = AssetImporter.GetAtPath(p) as TextureImporter;
            if (imp == null) { result.Log("importer yok: " + p); continue; }
            if (imp.filterMode != FilterMode.Point || imp.textureCompression != TextureImporterCompression.Uncompressed || !Mathf.Approximately(imp.spritePixelsPerUnit, 64f) || imp.mipmapEnabled)
            { imp.filterMode = FilterMode.Point; imp.textureCompression = TextureImporterCompression.Uncompressed; imp.spritePixelsPerUnit = 64; imp.mipmapEnabled = false; imp.SaveAndReimport(); fixedImp++; }
        }
        var street = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Rooms/Street.prefab");
        var school = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Rooms/SchoolRoute.prefab");
        var refSr = street.transform.Find("Facades/Fence_4").GetComponent<SpriteRenderer>();
        var refGlow = street.transform.Find("Lights/LampGlow_0").GetComponent<Light2D>();
        var spriteCache = new Dictionary<string, Sprite>();
        Sprite Spr(string path, string name)
        {
            var key = path + "#" + name;
            if (spriteCache.TryGetValue(key, out var s)) return s;
            s = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault(x => x.name == name) ?? AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault();
            spriteCache[key] = s; return s;
        }
        Directory.CreateDirectory("Assets/Prefabs/FloodEvening");
        // upsert: varsa prefab içeriğini yükle (iç fileID'ler korunur), yoksa önizleme sahnesinde yeni kur
        bool exists = false; // v3: her seferinde temiz kur (eski v2 parçaları kalmasın); aynı yola kaydetmek prefab GUID'ini korur
        Scene preview = default;
        GameObject rootGo;
        if (exists) rootGo = PrefabUtility.LoadPrefabContents(PrefabPath);
        else { preview = EditorSceneManager.NewPreviewScene(); rootGo = new GameObject("EveningNeighborhood"); SceneManager.MoveGameObjectToScene(rootGo, preview); }
        root = rootGo.transform;
        var colIdx = new Dictionary<Transform, int>();
        T GetOrAdd<T>(GameObject g) where T : Component { var c = g.GetComponent<T>(); return c != null ? c : g.AddComponent<T>(); }
        PolygonCollider2D Poly(Transform t) { colIdx.TryGetValue(t, out int i); var all = t.GetComponents<PolygonCollider2D>(); colIdx[t] = i + 1; if (i < all.Length) { all[i].enabled = true; return all[i]; } return t.gameObject.AddComponent<PolygonCollider2D>(); }
        int edgeI = 0;
        int nSpr = 0, nCol = 0, missing = 0;
        try
        {
            foreach (var l in lines)
            {
                if (l.Length == 0) continue;
                var f = l.Split('|');
                switch (f[0])
                {
                    case "ANC": { var t = Node(f[1]); t.position = new Vector3(F(f[2]), F(f[3]), 0f); break; }
                    case "SPR":
                    {
                        var parent = Node(f[1]);
                        var ex = parent.Find(f[2]); var go = ex != null ? ex.gameObject : new GameObject(f[2]); if (ex == null) go.transform.SetParent(parent, false);
                        go.transform.position = new Vector3(F(f[5]), F(f[6]), 0f);
                        var sr = GetOrAdd<SpriteRenderer>(go);
                        sr.sprite = Spr(f[3], f[4]);
                        if (sr.sprite == null) { missing++; result.Log("sprite yok: " + f[3] + " " + f[4]); }
                        sr.sharedMaterial = refSr.sharedMaterial; sr.sortingLayerID = refSr.sortingLayerID;
                        sr.sortingOrder = int.Parse(f[7]); sr.spriteSortPoint = SpriteSortPoint.Pivot; sr.flipX = f[8] == "1";
                        nSpr++; break;
                    }
                    case "COL":
                    {
                        var t = Node(f[1]);
                        var pc = Poly(t);
                        pc.isTrigger = f[2] == "trigger";
                        pc.SetPath(0, Pts(f[3], t));
                        nCol++; break;
                    }
                    case "BOX":
                    {
                        var t = Node(f[1]); t.position = new Vector3(F(f[2]), F(f[3]), 0f); t.rotation = Quaternion.Euler(0f, 0f, F(f[6]));
                        var bc = GetOrAdd<BoxCollider2D>(t.gameObject); bc.isTrigger = true; bc.size = new Vector2(F(f[4]), F(f[5]));
                        nCol++; break;
                    }
                    case "INACTIVE": Node(f[1]).gameObject.SetActive(false); break;
                    case "LIGHT":
                    {
                        var lp = Node(f[1]); var lx = lp.Find(f[2]); var go = lx != null ? lx.gameObject : new GameObject(f[2]); if (lx == null) go.transform.SetParent(lp, false); go.transform.position = new Vector3(F(f[3]), F(f[4]), 0f);
                        var li = go.GetComponent<Light2D>(); if (li == null) { li = go.AddComponent<Light2D>(); EditorUtility.CopySerialized(refGlow, li); }
                        li.intensity = 1.25f; li.pointLightOuterRadius = 2.2f; li.pointLightInnerRadius = 0.1f; li.color = new Color(1f, 0.83f, 0.58f, 1f);
                        break;
                    }
                    case "GLOBAL":
                    {
                        var t = Node(f[1]); var li = t.GetComponent<Light2D>(); if (li == null) { li = t.gameObject.AddComponent<Light2D>(); EditorUtility.CopySerialized(refGlow, li); }
                        li.lightType = Light2D.LightType.Global; li.color = new Color(0.47f, 0.54f, 0.88f, 1f); li.intensity = 0.72f;
                        break;
                    }
                    case "FRAMELOOP":
                    {
                        var t = Node(f[1]); var src = school.transform.Find("Props/StuckCar");
                        var loop = src.GetComponents<Component>().FirstOrDefault(c => c.GetType().Name == "SpriteFrameLoop");
                        if (loop != null && t.GetComponent(loop.GetType()) == null) { var nc = t.gameObject.AddComponent(loop.GetType()); EditorUtility.CopySerialized(loop, nc); }
                        break;
                    }
                    case "EDGE":
                    {
                        var t = Node("WalkBounds");
                        var eall = t.GetComponents<EdgeCollider2D>(); var ec = edgeI < eall.Length ? eall[edgeI] : t.gameObject.AddComponent<EdgeCollider2D>(); ec.enabled = true; edgeI++;
                        var pts = Pts(f[1], t).ToList(); pts.Add(pts[0]); ec.points = pts.ToArray(); ec.edgeRadius = 0f;
                        nCol++; break;
                    }
                    case "CAM":
                    {
                        var t = Node("CameraBounds"); t.position = new Vector3(F(f[1]), F(f[2]), 0f);
                        var cb = GetOrAdd<AfetUyanisi.Chapters.CameraBounds>(t.gameObject); cb.SetSize(new Vector2(F(f[3]), F(f[4])));
                        break;
                    }
                    case "SKY":
                    {
                        var skyT = root.Find("Sky"); GameObject sky; if (skyT != null) sky = skyT.gameObject; else { sky = Object.Instantiate(street.transform.Find("Sky").gameObject, root); sky.name = "Sky"; }
                        foreach (var sr in sky.GetComponentsInChildren<SpriteRenderer>(true))
                        {
                            var nm = sr.sprite != null ? sr.sprite.name.Replace("PROP_Street_Sky", "PROP_FE_Sky") : "PROP_FE_Sky";
                            sr.sprite = Spr(f[1], nm);
                        }
                        break;
                    }
                }
            }
            // water rise (LEAD component) on both water roots
            foreach (var wp in new[] { "WaterAreas", "LowRoad/Water" }) { var t = Node(wp); if (t.GetComponent<AfetUyanisi.Chapters.FloodEvening.FloodEveningWaterRise>() == null) t.gameObject.AddComponent<AfetUyanisi.Chapters.FloodEvening.FloodEveningWaterRise>();
                var so = new SerializedObject(t.GetComponent<AfetUyanisi.Chapters.FloodEvening.FloodEveningWaterRise>()); var ma = so.FindProperty("minAlpha"); if (ma != null) { ma.floatValue = wp == "LowRoad/Water" ? 0.92f : 0.75f; so.ApplyModifiedPropertiesWithoutUndo(); } }
            { var eall = Node("WalkBounds").GetComponents<EdgeCollider2D>(); for (int i = edgeI; i < eall.Length; i++) eall[i].enabled = false; }
            foreach (var kv in colIdx) { var all = kv.Key.GetComponents<PolygonCollider2D>(); for (int i = kv.Value; i < all.Length; i++) all[i].enabled = false; }
            // child order: anchors first is fine; keep Ground first visually in hierarchy
            Node("Ground").SetSiblingIndex(0);
            PrefabUtility.SaveAsPrefabAsset(rootGo, PrefabPath, out bool ok);
            result.Log("EveningNeighborhood kaydedildi={0}: {1} sprite, {2} collider, eksik sprite {3}, import düzeltme {4}", ok, nSpr, nCol, missing, fixedImp);
        }
        finally { if (exists) PrefabUtility.UnloadPrefabContents(rootGo); else EditorSceneManager.ClosePreviewScene(preview); }
    }
}
