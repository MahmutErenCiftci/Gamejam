using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using AfetUyanisi.Chapters;
using AfetUyanisi.Chapters.FloodEvening;
// (Unity MCP) Assets/Prefabs/FloodEvening/FE_ApartmentFinal.prefab'ı baştan kurar (final-world ajanı).
// Görseller ve konumlar tools/sel_aksam_final/world/out/manifest.json'dan (build_world_art.js yazar): kök = cephe çapası (ızgara 59,-1),
// çocuklar yerel dünya ofsetinde. Sahne bağlantıları (oyuncu, FE_ApartmentLockUi kökü) build_scene.cs'teki APARTMAN FİNALİ bloğunda verilir.
internal class CommandScript : IRunCommand
{
    const string PrefabPath = "Assets/Prefabs/FloodEvening/FE_ApartmentFinal.prefab";
    const string Art = "Assets/Art/FloodEvening/Final/World/";
    const string CenkArt = "Assets/Art/FloodEvening/Final/Cenk/";
    const string UnlitMat = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";
    static readonly string ManifestPath = Path.Combine(Directory.GetCurrentDirectory(), "tools/sel_aksam_final/world/out/manifest.json");

    [System.Serializable] class Item { public string file; public string[] sprites; public float[] local; public int order; public bool unlit; public float[] angles; }

    static Sprite[] Sheet(string path) => AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>()
        .OrderBy(s => int.TryParse(s.name.Substring(s.name.LastIndexOf('_') + 1), out int n) ? n : 0).ToArray();
    static void Arr(SerializedObject so, string prop, Object[] items)
    {
        var p = so.FindProperty(prop); p.arraySize = items.Length;
        for (int i = 0; i < items.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
    }
    static void Set(SerializedObject so, string prop, Object v) { var p = so.FindProperty(prop); if (p == null) throw new System.Exception("alan yok: " + prop); p.objectReferenceValue = v; }

    static string json;
    static Item Get(string name)
    {
        // JsonUtility sözlük okumaz: öğenin JSON nesnesini bul, onu ayrıştır
        int k = json.IndexOf("\"" + name + "\": {"); if (k < 0) throw new System.Exception("manifest: " + name);
        int s = json.IndexOf('{', k), depth = 0, e = s;
        for (; e < json.Length; e++) { if (json[e] == '{') depth++; else if (json[e] == '}') { depth--; if (depth == 0) break; } }
        return JsonUtility.FromJson<Item>(json.Substring(s, e - s + 1));
    }
    static Vector2 Anchor(string name)
    {
        int k = json.IndexOf("\"" + name + "\": ["); if (k < 0) throw new System.Exception("manifest çapa: " + name);
        int s = json.IndexOf('[', k), e = json.IndexOf(']', s);
        var a = json.Substring(s + 1, e - s - 1).Split(',').Select(v => float.Parse(v.Trim(), System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        return new Vector2(a[0], a[1]);
    }

    public void Execute(ExecutionResult result)
    {
        if (EditorApplication.isPlaying || EditorApplication.isCompiling) { result.LogError("BUSY"); return; }
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        json = File.ReadAllText(ManifestPath);
        foreach (var dir in new[] { "Assets/Art/FloodEvening/Final/World", "Assets/Art/FloodEvening/Final/Cenk" })
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { dir }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var imp = AssetImporter.GetAtPath(path) as TextureImporter; if (imp == null) continue;
                if (imp.filterMode != FilterMode.Point || imp.textureCompression != TextureImporterCompression.Uncompressed || imp.mipmapEnabled || !Mathf.Approximately(imp.spritePixelsPerUnit, 64f))
                { imp.filterMode = FilterMode.Point; imp.textureCompression = TextureImporterCompression.Uncompressed; imp.mipmapEnabled = false; imp.spritePixelsPerUnit = 64; imp.SaveAndReimport(); }
            }
        var street = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Rooms/Street.prefab");
        var refSr = street.transform.Find("Facades/Fence_4").GetComponent<SpriteRenderer>();
        var refGlow = street.transform.Find("Lights/LampGlow_0").GetComponent<Light2D>();
        var unlit = AssetDatabase.LoadAssetAtPath<Material>(UnlitMat);

        var preview = EditorSceneManager.NewPreviewScene();
        try
        {
            var root = new GameObject("FE_ApartmentFinal"); SceneManager.MoveGameObjectToScene(root, preview);
            Transform Node(string path, Transform parent = null)
            {
                var t = parent ?? root.transform;
                foreach (var part in path.Split('/')) { var c = t.Find(part); if (c == null) { var g = new GameObject(part); g.transform.SetParent(t, false); c = g.transform; } t = c; }
                return t;
            }
            SpriteRenderer SR(string node, string item, int frame = 0, Transform parent = null)
            {
                var it = Get(item); var t = Node(node, parent);
                t.localPosition = new Vector3(it.local[0], it.local[1], 0f) - (parent != null ? parent.localPosition : Vector3.zero);
                var sr = t.gameObject.AddComponent<SpriteRenderer>();
                var sheet = Sheet(Art + it.file); sr.sprite = sheet.Length > frame ? sheet[frame] : null;
                sr.sharedMaterial = it.unlit ? unlit : refSr.sharedMaterial; sr.sortingLayerID = refSr.sortingLayerID; sr.sortingOrder = it.order;
                sr.spriteSortPoint = SpriteSortPoint.Pivot;
                if (sr.sprite == null) result.LogWarning("sprite yok: " + item);
                return sr;
            }
            Transform At(string node, string anchor) { var t = Node(node); var a = Anchor(anchor); t.localPosition = new Vector3(a.x, a.y, 0f); return t; }
            Light2D Light(string node, Vector2 pos)
            {
                var t = Node(node); t.localPosition = pos;
                var li = t.gameObject.AddComponent<Light2D>(); EditorUtility.CopySerialized(refGlow, li); return li;
            }

            // ---------------- görseller ----------------
            var interior = SR("Visual/Interior", "PROP_FE_Apt_Interior");
            var doorT = Node("Visual/Door");
            var door = doorT.gameObject.AddComponent<ApartmentDoor>();
            var leaf = SR("Visual/Door/Leaf", "PROP_FE_Apt_Door");
            var glow = SR("Visual/Door/GlassGlow", "PROP_FE_Apt_DoorGlow");
            var handle = SR("Visual/Door/Handle", "PROP_FE_Apt_Handle");
            var keypad = SR("Visual/Door/Keypad", "PROP_FE_Apt_Keypad", 1);
            SR("Visual/Door/Recess", "PROP_FE_Apt_Recess");
            var spillGlow = SR("Visual/SpillGlow", "FX_FE_Apt_SpillGlow");
            SR("Visual/Mailbox", "PROP_FE_Apt_Mailbox");
            var lid = SR("Visual/MailLid", "PROP_FE_Apt_MailLid");
            var tip = SR("Visual/EnvelopeTip", "PROP_FE_Apt_EnvelopeTip");
            SR("Visual/Lamp", "PROP_FE_Apt_Lamp");
            SR("Visual/LampBulb", "PROP_FE_Apt_LampBulb");
            SR("Visual/Mat", "PROP_FE_Apt_Mat");
            // Cenk içeri girerken onun yerine çizilen kopya (sprite çalışma anında kopyalanır; başta kapalı)
            var proxy = Node("Visual/CenkProxy").gameObject.AddComponent<SpriteRenderer>();
            proxy.sharedMaterial = refSr.sharedMaterial; proxy.sortingLayerID = refSr.sortingLayerID; proxy.sortingOrder = -51;
            proxy.spriteSortPoint = SpriteSortPoint.Pivot; proxy.enabled = false;
            foreach (var (n, anchor) in new[] { ("Plant_A", "PlantA"), ("Plant_B", "PlantB") })
            {
                var sr = SR("Visual/" + n, "PROP_FE_Apt_" + n);
                // saksı tabanı: küçük elmas collider (izometrik, ~0.36 hücre)
                var pc = sr.gameObject.AddComponent<PolygonCollider2D>();
                float r = n == "Plant_A" ? 0.13f : 0.18f;
                Vector2 W(float du, float dv) => new Vector2((du - dv) / 2f, (du + dv) / 4f);
                pc.SetPath(0, new[] { W(-r, -r), W(r, -r), W(r, r), W(-r, r) });
            }

            // ---------------- ışıklar ----------------
            var doorLight = Light("Lights/DoorLight", Anchor("DoorLight"));
            doorLight.lightType = Light2D.LightType.Point; doorLight.color = new Color(1f, 0.78f, 0.46f, 1f); doorLight.intensity = 0f;
            doorLight.pointLightOuterRadius = 2.3f; doorLight.pointLightInnerRadius = 0.15f; doorLight.falloffIntensity = 0.6f;
            var spill = Light("Lights/SpillLight", Get("FX_FE_Apt_LightSpill").local is float[] sl ? new Vector2(sl[0], sl[1]) : Vector2.zero);
            {
                var so = new SerializedObject(spill);
                so.FindProperty("m_LightType").intValue = (int)Light2D.LightType.Sprite;
                so.ApplyModifiedPropertiesWithoutUndo();
                spill.lightCookieSprite = Sheet(Art + "FX_FE_Apt_LightSpill.png").FirstOrDefault();
                spill.color = new Color(1f, 0.80f, 0.52f, 1f); spill.intensity = 0f;
            }
            var lampLight = Light("Lights/LampLight", Anchor("LampLight") + new Vector2(0f, 0.85f));
            lampLight.lightType = Light2D.LightType.Point; lampLight.color = new Color(1f, 0.83f, 0.58f, 1f); lampLight.intensity = 0.95f;
            lampLight.pointLightOuterRadius = 1.7f; lampLight.pointLightInnerRadius = 0.1f;

            // ---------------- çapalar ----------------
            var front = At("Anchors/DoorFront", "DoorFront"); var sill = At("Anchors/Threshold", "Threshold");
            var leafPlane = At("Anchors/LeafPlane", "LeafPlane"); var inside = At("Anchors/Inside", "Inside");
            var focus = At("Anchors/CameraFocus", "CameraFocus"); var lockStand = At("Anchors/LockStand", "LockStand"); var mailStand = At("Anchors/MailStand", "MailStand");

            // ---------------- etkileşimler ----------------
            Interactable Point(string node, string anchor, string promptAnchor, string highlightItem, float radius)
            {
                var t = At(node, anchor);
                var ia = t.gameObject.AddComponent<Interactable>();
                var pa = At(node + "_Prompt", promptAnchor);
                var hl = SR("Highlight", highlightItem, 0, t);
                var so = new SerializedObject(ia);
                so.FindProperty("useRadius").floatValue = radius;
                so.FindProperty("promptAnchor").objectReferenceValue = pa;
                so.FindProperty("promptOffset").vector3Value = new Vector3(0f, 0.12f, 0f);
                so.FindProperty("highlight").objectReferenceValue = hl.gameObject;
                so.FindProperty("available").boolValue = false;
                so.ApplyModifiedPropertiesWithoutUndo();
                return ia;
            }
            var lockGo = Node("Lock"); var lockI = lockGo.gameObject.AddComponent<DoorLockInteraction>();
            var lockPoint = Point("Lock/Point", "LockPoint", "LockPrompt", "PROP_FE_Apt_LockHighlight", 0.6f);
            var mailGo = Node("Mailbox"); var mailI = mailGo.gameObject.AddComponent<MailboxInteraction>();
            var mailPoint = Point("Mailbox/Point", "MailPoint", "MailPrompt", "PROP_FE_Apt_MailHighlight", 0.55f);

            var finale = root.AddComponent<ApartmentFinale>();

            // ---------------- bağlantılar ----------------
            var dso = new SerializedObject(door);
            Set(dso, "leaf", leaf); Arr(dso, "leafFrames", Sheet(Art + "PROP_FE_Apt_Door.png"));
            var ang = Get("PROP_FE_Apt_Door").angles; var fa = dso.FindProperty("frameAngles"); fa.arraySize = ang.Length; for (int i = 0; i < ang.Length; i++) fa.GetArrayElementAtIndex(i).floatValue = ang[i];
            Set(dso, "glassGlow", glow); Arr(dso, "glowFrames", Sheet(Art + "PROP_FE_Apt_DoorGlow.png"));
            var hs = Sheet(Art + "PROP_FE_Apt_Handle.png"); Set(dso, "handle", handle); Set(dso, "handleUp", hs[0]); Set(dso, "handleDown", hs[1]);
            var ks = Sheet(Art + "PROP_FE_Apt_Keypad.png"); Set(dso, "keypad", keypad); Set(dso, "keypadOff", ks[0]); Set(dso, "keypadIdle", ks[1]); Set(dso, "keypadRed", ks[2]); Set(dso, "keypadGreen", ks[3]);
            Set(dso, "interior", interior); Set(dso, "spillGlow", spillGlow); Set(dso, "doorLight", doorLight); Set(dso, "spillLight", spill);
            dso.ApplyModifiedPropertiesWithoutUndo();

            var mso = new SerializedObject(mailI);
            Set(mso, "finale", finale); Set(mso, "point", mailPoint); Set(mso, "stand", mailStand);
            var ls = Sheet(Art + "PROP_FE_Apt_MailLid.png"); Set(mso, "lid", lid); Set(mso, "lidClosed", ls[0]); Set(mso, "lidOpenFull", ls[1]); Set(mso, "lidOpenEmpty", ls[2]);
            Set(mso, "envelopeTip", tip.gameObject);
            mso.ApplyModifiedPropertiesWithoutUndo();
            tip.gameObject.SetActive(false);

            var lso = new SerializedObject(lockI);
            Set(lso, "finale", finale); Set(lso, "point", lockPoint); Set(lso, "stand", lockStand);
            lso.ApplyModifiedPropertiesWithoutUndo();

            var fso = new SerializedObject(finale);
            Set(fso, "dataJson", AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Data/FloodEvening/apartment_final.json"));
            Set(fso, "door", door); Set(fso, "mailbox", mailI); Set(fso, "doorLock", lockI);
            Set(fso, "cenkProxy", proxy);
            Set(fso, "doorFront", front); Set(fso, "threshold", sill); Set(fso, "leafPlane", leafPlane); Set(fso, "insideSpot", inside); Set(fso, "cameraFocus", focus);
            foreach (var (prop, file) in new[] { ("inspectLock", "InspectLock"), ("reachMailbox", "ReachMailbox"), ("takeEnvelope", "TakeEnvelope"), ("readEnvelope", "ReadEnvelope"),
                ("typeKeypad", "TypeKeypad"), ("turnHandle", "TurnHandle"), ("reactDoor", "ReactDoor") })
                Arr(fso, prop, Sheet(CenkArt + "CHR_Cenk_Final_" + file + ".png"));
            fso.ApplyModifiedPropertiesWithoutUndo();

            Directory.CreateDirectory("Assets/Prefabs/FloodEvening");
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool ok);
            result.Log("FE_ApartmentFinal kaydedildi={0} kanat={1} kare, poz={2}, veri={3}, ışık(sprite)={4}", ok, Sheet(Art + "PROP_FE_Apt_Door.png").Length,
                Sheet(CenkArt + "CHR_Cenk_Final_InspectLock.png").Length, fso.FindProperty("dataJson").objectReferenceValue != null, spill.lightType);
        }
        finally { EditorSceneManager.ClosePreviewScene(preview); }
    }
}
