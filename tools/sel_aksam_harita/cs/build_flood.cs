using System.IO;
using UnityEditor;
using UnityEngine;

// 120 sn sel (LEAD): flood.js çıktılarının içe aktarma ayarları + FE_FloodWater.mat. Sahneye dokunmaz.
// FE_FloodData: veri dokusu (sRGB kapalı, Read/Write açık, çift doğrusal, sıkıştırmasız, mipmap yok, kenarda kıskaç).
// FX_* / PROP_FE_FloodBarrier: sprite, PPU 64, nokta filtre, sıkıştırmasız; pivotlar efekt türüne göre.
internal class CommandScript : IRunCommand
{
    const string Dir = "Assets/Art/FloodEvening/ENV/Flood/";

    public void Execute(ExecutionResult result)
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        int n = 0;
        foreach (var file in Directory.GetFiles(Dir, "*.png"))
        {
            string path = file.Replace('\\', '/');
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null) { result.Log("importer yok: " + path); continue; }
            string name = Path.GetFileNameWithoutExtension(path);
            if (name == "FE_FloodData")
            {
                ti.textureType = TextureImporterType.Default;
                ti.sRGBTexture = false;
                ti.isReadable = true;
                ti.alphaIsTransparency = false;
                ti.alphaSource = TextureImporterAlphaSource.FromInput;
                ti.filterMode = FilterMode.Bilinear;
                ti.wrapMode = TextureWrapMode.Clamp;
                ti.npotScale = TextureImporterNPOTScale.None;
            }
            else
            {
                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single;
                ti.spritePixelsPerUnit = 64f;
                ti.filterMode = FilterMode.Point;
                ti.alphaIsTransparency = true;
                var st = new TextureImporterSettings();
                ti.ReadTextureSettings(st);
                st.spriteMeshType = SpriteMeshType.FullRect;
                st.spriteAlignment = (int)SpriteAlignment.Custom;
                Vector2 pivot = new Vector2(0.5f, 0.5f);
                if (name.StartsWith("FX_FE_Splash")) pivot = new Vector2(0.5f, 0.2f);
                else if (name == "FX_FE_FeetWater") pivot = new Vector2(0.5f, 0.6f);
                else if (name == "PROP_FE_FloodBarrier") pivot = new Vector2(0.5f, 0.03f);
                st.spritePivot = pivot;
                ti.SetTextureSettings(st);
            }
            ti.mipmapEnabled = false;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.SaveAndReimport();
            n++;
        }

        string matPath = Dir + "FE_FloodWater.mat";
        var shader = Shader.Find("AfetUyanisi/FloodEvening/FloodWater2D");
        if (shader == null) { result.LogError("shader bulunamadı (derleme hatası?)"); return; }
        var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, matPath); }
        else mat.shader = shader;
        mat.SetTexture("_FloodData", AssetDatabase.LoadAssetAtPath<Texture2D>(Dir + "FE_FloodData.png"));
        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssets();
        var msgs = ShaderUtil.GetShaderMessages(shader);
        foreach (var m in msgs) result.Log("shader: " + m.severity + " " + m.message + " (satır " + m.line + ")");
        result.Log("sel içe aktarma: " + n + " doku, malzeme=" + (mat != null) + ", shader hata=" + ShaderUtil.ShaderHasError(shader));
    }
}
