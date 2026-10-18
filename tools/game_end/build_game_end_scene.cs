// OYUN SONU sahnesini kurar: Assets/Scenes/GameEnd.unity (kamera + GameEndScreen) ve Build Settings'e (açık) ekler.
// Unity MCP ile çalıştırılır (Unity_RunCommand / node retry.js run <bu dosya>). Sahne hep Additive açılır/oluşturulur,
// kaydedilip kapatılır; kullanıcının açık sahnelerine dokunmaz (aktif sahne geçici değişir, sonra geri alınır).
// Arayüzün kendisi çalışma anında GameEndScreen tarafından kodla kurulur; burada yalnız sprite referansları atanır.
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

internal class CommandScript : IRunCommand
{
    const string ScenePath = "Assets/Scenes/GameEnd.unity";
    const string BgPath = "Assets/Art/GameEnd/GAMEEND_Background.png";
    const string CenkPath = "Assets/Art/Characters/Cenk/NoBag/CHR_Cenk_Walk_South_NoBag.png";
    const string MotherPath = "Assets/Art/Characters/CHR_Mother_Walk_South.png";
    // Menüyle aynı görünüm (Berk'in menü sanatı; yalnız referans verilir, kopyalanmaz).
    const string LogoPath = "Assets/Art/Menu/UI/LogoStacked.png";
    const string PanelPath = "Assets/Art/Menu/UI/Panel.png";
    const string PanelLitPath = "Assets/Art/Menu/UI/PanelLit.png";
    const string FontPath = "Assets/Fonts/PixelifySans/PixelifySans-Regular.ttf";
    const string BoldFontPath = "Assets/Fonts/PixelifySans/PixelifySans-Bold.ttf";

    static Sprite FirstSprite(string path)
    {
        return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>()
            .OrderBy(s => s.name.Length).ThenBy(s => s.name, System.StringComparer.Ordinal).FirstOrDefault();
    }

    public void Execute(ExecutionResult result)
    {
        if (EditorApplication.isPlaying || EditorApplication.isCompiling) { result.LogError("BUSY: playing/compiling"); return; }
        var screenType = System.Type.GetType("AfetUyanisi.Chapters.GameEnd.GameEndScreen, Assembly-CSharp");
        if (screenType == null) { result.LogError("GameEndScreen tipi yok (derleme bitmedi mi?)"); return; }

        // Arka plan: Point, sıkıştırmasız, tek sprite (meta gen_bg.js'ten gelir; yine de garanti et).
        var imp = AssetImporter.GetAtPath(BgPath) as TextureImporter;
        if (imp == null) { result.LogError("arka plan yok: " + BgPath); return; }
        if (imp.filterMode != FilterMode.Point || imp.textureCompression != TextureImporterCompression.Uncompressed || imp.mipmapEnabled)
        {
            imp.filterMode = FilterMode.Point; imp.textureCompression = TextureImporterCompression.Uncompressed; imp.mipmapEnabled = false;
            imp.SaveAndReimport();
        }
        Sprite bg = FirstSprite(BgPath), cenk = FirstSprite(CenkPath), mother = FirstSprite(MotherPath);
        result.Log("sprites bg={0} cenk={1} mother={2}", bg ? bg.name : "-", cenk ? cenk.name + " " + cenk.rect : "-", mother ? mother.name + " " + mother.rect : "-");

        var previousActive = SceneManager.GetActiveScene();
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool wasOpen = scene.IsValid() && scene.isLoaded;
        if (wasOpen && scene.isDirty) { result.LogError("GameEnd sahnesi açık ve kaydedilmemiş; dokunulmadı."); return; }
        if (!wasOpen)
        {
            if (System.IO.File.Exists(ScenePath)) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            else scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        }
        foreach (var root in scene.GetRootGameObjects()) Object.DestroyImmediate(root);

        SceneManager.SetActiveScene(scene);
        try
        {
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 2.8125f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0x0F / 255f, 0x1E / 255f, 0x33 / 255f, 1f);
            camGo.transform.position = new Vector3(0f, 0f, -10f);

            var go = new GameObject("GameEnd");
            var screen = go.AddComponent(screenType);
            var so = new SerializedObject(screen);
            so.FindProperty("background").objectReferenceValue = bg;
            so.FindProperty("cenk").objectReferenceValue = cenk;
            so.FindProperty("mother").objectReferenceValue = mother;
            so.FindProperty("logo").objectReferenceValue = FirstSprite(LogoPath);
            so.FindProperty("panel").objectReferenceValue = FirstSprite(PanelPath);
            so.FindProperty("panelLit").objectReferenceValue = FirstSprite(PanelLitPath);
            so.FindProperty("font").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
            so.FindProperty("boldFont").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Font>(BoldFontPath);
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        finally
        {
            if (previousActive.IsValid() && previousActive.isLoaded) SceneManager.SetActiveScene(previousActive);
        }

        System.IO.Directory.CreateDirectory("Assets/Scenes");
        bool saved = EditorSceneManager.SaveScene(scene, ScenePath);
        if (!wasOpen) EditorSceneManager.CloseScene(scene, true);
        result.Log("scene saved={0} path={1}", saved, ScenePath);

        // Build Settings: GameEnd açık olarak listede olsun (başkalarının satırlarına dokunmadan).
        var list = EditorBuildSettings.scenes.ToList();
        var existing = list.FirstOrDefault(s => s.path == ScenePath);
        if (existing == null) { list.Add(new EditorBuildSettingsScene(ScenePath, true)); EditorBuildSettings.scenes = list.ToArray(); result.Log("build settings: eklendi"); }
        else if (!existing.enabled) { existing.enabled = true; EditorBuildSettings.scenes = list.ToArray(); result.Log("build settings: açıldı"); }
        else result.Log("build settings: zaten açık");
        result.Log("BUILD LIST: " + string.Join(" | ", EditorBuildSettings.scenes.Select(s => (s.enabled ? "+" : "-") + System.IO.Path.GetFileNameWithoutExtension(s.path))));
    }
}
