using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
// (Unity MCP) Assets/Prefabs/FloodEvening/FE_ApartmentLockUi.prefab'ı baştan kurar:
//   kök: Canvas (Screen Space Overlay, sıra 70, pixelPerfect) + CanvasScaler (ConstantPixelSize) + GraphicRaycaster + ApartmentFinalUi
//   Letter: ApartmentLetterView    Lock: ApartmentLockView    — sprite'lar, Pixelify fontları ve apartment_final.json bağlı.
// Önce görsellerin içe aktarma ayarlarını denetler (Point, sıkıştırmasız, mipmap yok, PPU 64). Prefab yoksa önizleme sahnesinde kurar;
// varsa LoadPrefabContents ile YERİNDE günceller (iç fileID'ler korunur → sahnedeki/diğer asset'lerdeki bağlantılar kopmaz).
// Mevcut hiçbir asset silinmez, kullanıcının açık sahnelerine dokunulmaz.
internal class CommandScript : IRunCommand
{
    const string PrefabPath = "Assets/Prefabs/FloodEvening/FE_ApartmentLockUi.prefab";
    const string Art = "Assets/Art/FloodEvening/Final/UI/";
    const string DataPath = "Assets/Data/FloodEvening/apartment_final.json";
    static readonly string[] KeyIds = { "1", "2", "3", "4", "5", "6", "7", "8", "9", "Star", "0", "Hash" };

    static Sprite[] Sheet(string file) => AssetDatabase.LoadAllAssetsAtPath(Art + file + ".png").OfType<Sprite>().ToArray();
    static Sprite One(string file) => AssetDatabase.LoadAllAssetsAtPath(Art + file + ".png").OfType<Sprite>().FirstOrDefault();
    static Sprite Named(Sprite[] all, string name) => all.FirstOrDefault(s => s.name == name);
    static void Arr(SerializedObject so, string prop, Object[] items)
    {
        var p = so.FindProperty(prop); p.arraySize = items.Length;
        for (int i = 0; i < items.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
    }
    static void Set(SerializedObject so, string prop, Object value) => so.FindProperty(prop).objectReferenceValue = value;

    public void Execute(ExecutionResult result)
    {
        if (EditorApplication.isPlaying || EditorApplication.isCompiling) { result.LogError("BUSY (play/compile)"); return; }
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        int fixedCount = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Art/FloodEvening/Final/UI" }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var imp = AssetImporter.GetAtPath(path) as TextureImporter; if (imp == null) continue;
            if (imp.filterMode != FilterMode.Point || imp.textureCompression != TextureImporterCompression.Uncompressed || imp.mipmapEnabled || !Mathf.Approximately(imp.spritePixelsPerUnit, 64f) || imp.textureType != TextureImporterType.Sprite)
            {
                imp.textureType = TextureImporterType.Sprite; imp.filterMode = FilterMode.Point; imp.textureCompression = TextureImporterCompression.Uncompressed;
                imp.mipmapEnabled = false; imp.spritePixelsPerUnit = 64; imp.SaveAndReimport(); fixedCount++;
            }
        }

        var keys = Sheet("UI_FE_Final_Keys"); var lcd = Sheet("UI_FE_Final_Lcd"); var led = Sheet("UI_FE_Final_Led");
        var handle = Sheet("UI_FE_Final_Handle"); var glow = Sheet("UI_FE_Final_HandleGlow"); var digits = Sheet("UI_FE_Final_LetterDigits");
        var keyArr = new Object[36];
        string[] states = { "N", "H", "P" };
        for (int s = 0; s < 3; s++) for (int k = 0; k < 12; k++) keyArr[s * 12 + k] = Named(keys, "UI_FE_Final_Key_" + KeyIds[k] + "_" + states[s]);
        var lcdArr = new[] { "0", "1", "2", "3", "4", "5", "6", "7", "8", "9", "Ghost", "Dash", "Under" }.Select(n => (Object)Named(lcd, "UI_FE_Final_Lcd_" + n)).ToArray();
        var ledArr = new[] { "Off", "Red", "Green" }.Select(n => (Object)Named(led, "UI_FE_Final_Led_" + n)).ToArray();
        var handleArr = Enumerable.Range(0, 16).Select(i => (Object)Named(handle, "UI_FE_Final_Handle_" + i.ToString("00"))).ToArray();
        var glowArr = Enumerable.Range(0, 16).Select(i => (Object)Named(glow, "UI_FE_Final_HandleGlow_" + i.ToString("00"))).ToArray();
        var digitArr = Enumerable.Range(0, 10).Select(i => (Object)Named(digits, "UI_FE_Final_LetterDigit_" + i)).ToArray();
        int missing = keyArr.Count(o => o == null) + lcdArr.Count(o => o == null) + ledArr.Count(o => o == null) + handleArr.Count(o => o == null) + glowArr.Count(o => o == null) + digitArr.Count(o => o == null);
        var regular = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/PixelifySans/PixelifySans-Regular.ttf");
        var bold = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/PixelifySans/PixelifySans-Bold.ttf");
        var json = AssetDatabase.LoadAssetAtPath<TextAsset>(DataPath);

        bool existed = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null;
        var preview = existed ? default(Scene) : EditorSceneManager.NewPreviewScene();
        GameObject root = null;
        try
        {
            if (existed) root = PrefabUtility.LoadPrefabContents(PrefabPath);
            else { root = new GameObject("FE_ApartmentLockUi", typeof(RectTransform)); SceneManager.MoveGameObjectToScene(root, preview); }
            root.layer = 5;
            T Ensure<T>(GameObject go) where T : Component { var c = go.GetComponent<T>(); return c != null ? c : go.AddComponent<T>(); }
            var canvas = Ensure<Canvas>(root); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 70; canvas.pixelPerfect = true;
            var scaler = Ensure<CanvasScaler>(root); scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize; scaler.scaleFactor = 1f;
            Ensure<GraphicRaycaster>(root);
            var ui = Ensure<AfetUyanisi.Chapters.FloodEvening.ApartmentFinalUi>(root);

            GameObject Child(string name)
            {
                var t = root.transform.Find(name);
                var go = t != null ? t.gameObject : new GameObject(name, typeof(RectTransform));
                go.layer = 5; go.transform.SetParent(root.transform, false);
                var rt = (RectTransform)go.transform; rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
                return go;
            }
            var letter = Ensure<AfetUyanisi.Chapters.FloodEvening.ApartmentLetterView>(Child("Letter"));
            var lockView = Ensure<AfetUyanisi.Chapters.FloodEvening.ApartmentLockView>(Child("Lock"));

            var so = new SerializedObject(letter);
            Set(so, "paper", One("UI_FE_Final_Letter")); Set(so, "envelopeBack", One("UI_FE_Final_EnvBack")); Set(so, "envelopeFront", One("UI_FE_Final_EnvFront"));
            Set(so, "flapClosed", One("UI_FE_Final_EnvFlapClosed")); Set(so, "flapOpen", One("UI_FE_Final_EnvFlapOpen"));
            Arr(so, "digits", digitArr); Set(so, "font", regular); Set(so, "boldFont", bold);
            so.FindProperty("dimAlpha").floatValue = 0.55f;
            so.ApplyModifiedPropertiesWithoutUndo();

            so = new SerializedObject(lockView);
            Set(so, "background", One("UI_FE_Final_LockBg")); Set(so, "body", One("UI_FE_Final_LockBody")); Set(so, "rosette", One("UI_FE_Final_Rosette"));
            Set(so, "latchBolt", One("UI_FE_Final_LatchBolt")); Set(so, "deadbolt", One("UI_FE_Final_Deadbolt")); Set(so, "dragArrow", One("UI_FE_Final_DragArrow"));
            Set(so, "mouseIcon", One("UI_FE_Final_MouseL"));
            Arr(so, "keySprites", keyArr); Arr(so, "lcdSprites", lcdArr); Arr(so, "ledSprites", ledArr); Arr(so, "handleFrames", handleArr); Arr(so, "handleGlowFrames", glowArr);
            Set(so, "font", regular); Set(so, "boldFont", bold);
            so.FindProperty("dimAlpha").floatValue = 0.72f;
            so.ApplyModifiedPropertiesWithoutUndo();

            so = new SerializedObject(ui);
            Set(so, "letter", letter); Set(so, "lockView", lockView); Set(so, "dataJson", json);
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool ok);
            result.Log((existed ? "[yerinde güncellendi] " : "[yeni] ") + "FE_ApartmentLockUi kaydedildi={0} importDüzeltilen={1} eksikSprite={2} font={3}/{4} json={5} keys={6} handle={7}",
                ok, fixedCount, missing, regular != null, bold != null, json != null, keys.Length, handle.Length);
        }
        finally
        {
            if (existed) { if (root != null) PrefabUtility.UnloadPrefabContents(root); }
            else EditorSceneManager.ClosePreviewScene(preview);
        }
    }
}
