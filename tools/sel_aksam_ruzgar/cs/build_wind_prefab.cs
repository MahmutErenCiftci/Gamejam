using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
// (Unity MCP) Assets/Prefabs/FloodEvening/FE_WindCorridor.prefab'ı baştan kurar: WindCorridor + Fx (WindFx) + Hud (WindHud), sprite'lar ve font bağlı.
// Sahne bağlantıları (oyuncu, WindIntro, SafeZone, WindEntry/WindExit, SafeSpot) sahne kurulumunda verilir.
internal class CommandScript : IRunCommand
{
    const string PrefabPath = "Assets/Prefabs/FloodEvening/FE_WindCorridor.prefab";
    const string Art = "Assets/Art/FloodEvening/Wind/";
    static Sprite[] Frames(string file, int take = 0)
    {
        var all = AssetDatabase.LoadAllAssetsAtPath(Art + "Cenk/" + file + ".png").OfType<Sprite>()
            .OrderBy(s => int.Parse(s.name.Substring(s.name.LastIndexOf('_') + 1))).ToArray();
        return take > 0 ? all.Take(take).ToArray() : all;
    }
    static Sprite One(string path) => AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault();
    static void Arr(SerializedObject so, string prop, Object[] items)
    {
        var p = so.FindProperty(prop); p.arraySize = items.Length;
        for (int i = 0; i < items.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
    }
    public void Execute(ExecutionResult result)
    {
        if (EditorApplication.isPlaying || EditorApplication.isCompiling) { result.LogError("BUSY"); return; }
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        // içe aktarma ayarları: piksel sanat
        foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Art/FloodEvening/Wind" }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var imp = AssetImporter.GetAtPath(path) as TextureImporter; if (imp == null) continue;
            if (imp.filterMode != FilterMode.Point || imp.textureCompression != TextureImporterCompression.Uncompressed || imp.mipmapEnabled || !Mathf.Approximately(imp.spritePixelsPerUnit, 64f))
            { imp.filterMode = FilterMode.Point; imp.textureCompression = TextureImporterCompression.Uncompressed; imp.mipmapEnabled = false; imp.spritePixelsPerUnit = 64; imp.SaveAndReimport(); }
        }
        var preview = EditorSceneManager.NewPreviewScene();
        try
        {
            var root = new GameObject("FE_WindCorridor"); SceneManager.MoveGameObjectToScene(root, preview);
            var fxGo = new GameObject("Fx"); fxGo.transform.SetParent(root.transform, false);
            var fx = fxGo.AddComponent<AfetUyanisi.Chapters.FloodEvening.WindFx>();
            var hudGo = new GameObject("Hud"); hudGo.transform.SetParent(root.transform, false);
            var hud = hudGo.AddComponent<AfetUyanisi.Chapters.FloodEvening.WindHud>();
            var wc = root.AddComponent<AfetUyanisi.Chapters.FloodEvening.WindCorridor>();

            var so = new SerializedObject(fx);
            Arr(so, "leafSprites", Enumerable.Range(1, 4).Select(i => (Object)One(Art + "FX/FX_FE_Wind_Leaf" + i + ".png")).ToArray());
            so.FindProperty("streakSprite").objectReferenceValue = One(Art + "FX/FX_FE_Wind_Streak.png");
            so.ApplyModifiedPropertiesWithoutUndo();

            so = new SerializedObject(hud);
            foreach (var (prop, file) in new[] { ("banner", "UI_FE_Wind_Banner"), ("arrowLeft", "UI_FE_Wind_ArrowL"), ("arrowRight", "UI_FE_Wind_ArrowR"), ("hintA", "UI_FE_Wind_HintA"),
                ("hintD", "UI_FE_Wind_HintD"), ("alert", "UI_FE_Wind_Alert"), ("chevLeft", "UI_FE_Wind_ChevL"), ("chevRight", "UI_FE_Wind_ChevR") })
                so.FindProperty(prop).objectReferenceValue = One(Art + "UI/" + file + ".png");
            so.FindProperty("font").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/PixelifySans/PixelifySans-Regular.ttf");
            so.ApplyModifiedPropertiesWithoutUndo();

            so = new SerializedObject(wc);
            Arr(so, "braceLeanLeft", Frames("CHR_Cenk_Wind_Brace_L", 4)); Arr(so, "braceLeanRight", Frames("CHR_Cenk_Wind_Brace_R", 4));
            Arr(so, "stumbleLeft", Frames("CHR_Cenk_Wind_Stumble_L", 4)); Arr(so, "stumbleRight", Frames("CHR_Cenk_Wind_Stumble_R", 4));
            Arr(so, "recoverLeft", Frames("CHR_Cenk_Wind_Recover_L", 6)); Arr(so, "recoverRight", Frames("CHR_Cenk_Wind_Recover_R", 6));
            Arr(so, "reactLeft", Frames("CHR_Cenk_Wind_React_L", 2));
            so.FindProperty("hud").objectReferenceValue = hud;
            so.FindProperty("fx").objectReferenceValue = fx;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool ok);
            result.Log("FE_WindCorridor kaydedildi=" + ok + " brace=" + Frames("CHR_Cenk_Wind_Brace_L").Length + " font=" + (so.FindProperty("hud") != null));
        }
        finally { EditorSceneManager.ClosePreviewScene(preview); }
    }
}
